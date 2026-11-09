using System;
using System.Collections.Generic;
using System.Linq;
using Hex.Configs;
using Hex.Session.Battle;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.BattleCounter;

// An attack as the game plays it (issue #8, fogone 06.11): the defender's counter before the strike (counter_before_attack),
// the strike, the counter after it (also a second one for counter_before_and_after_attack, the fauns' law), the second
// strike of Double Strike. What happens is the game's own decisions: the counter (ModEvents.CounterDecided), the counter
// first (GameBattleLogic.AnswersBeforeStrike, eqe.bhpz on 0.82.7), twice (the unit's sequence effect, Unit.bjnz), the
// strike again (StrikesAgainAfterCounter, eqe.bhqa). Each step's numbers are the game's forecast of that hit for the
// health both stacks have then: each step takes off the target's health what the step before did, wounded top creature
// and all (the stack's count and its top creature's health set for the run). The range: the most health left after
// the least damage, the least after the most.
public partial class Plugin
{
    public enum SecondStrikeShown { Off, InStrike, Column }

    Choice<SecondStrikeShown> secondShown;

    // One hit of the attack: by the defender (a counter) or the attacker, its numbers, the target's whole health before
    // it (min, max) and whether that is still the target's health now (nothing hit it before).
    sealed record Step(bool ByDefender, Numbers Numbers, Range TargetHealth, bool TargetUntouched);

    // An attack's steps, worked out for the game's decisions it was made with.
    sealed record Sequence(List<Step> Steps, bool Counters, SecondStrikeShown Mode);

    // By forecast: the attack's parameters (to run it again), the game's own numbers of the strike, and its sequence.
    readonly Dictionary<IntPtr, (DamageUiParams strike, bool flag, Numbers game)> strikes = new();
    readonly Dictionary<IntPtr, Sequence> sequences = new();

    void Forget(IntPtr forecast)
    {
        fights.Remove(forecast);
        strikes.Remove(forecast);
        sequences.Remove(forecast);
    }

    Sequence SequenceOf(CounterDecided decided, GameForecast forecast, (Unit attacker, Unit defender) fight, SecondStrikeShown mode)
    {
        if (sequences.TryGetValue(forecast.Pointer, out var known) && known.Counters == decided.Counters && known.Mode == mode)
            return known;
        if (!strikes.TryGetValue(forecast.Pointer, out var strike))
            return null;
        var (attacker, defender) = fight;
        var logic = decided.Battle?.TryCast<GameBattleLogic>();
        var ability = decided.Ability?.TryCast<GameAbility>();
        var counter = decided.Counters ? CounterOf(strike.strike, defender) : null;
        var first = counter != null && logic != null && logic.AnswersBeforeStrike(defender);
        var twice = first && defender.HasSequenceEffect("counter_before_and_after_attack");
        var again = mode != SecondStrikeShown.Off && logic != null && ability != null && logic.StrikesAgainAfterCounter(attacker, ability);
        var counterParams = counter != null
            ? new DamageUiParams(new GameCaster(defender.Pointer), new GameCastSource(counter.Pointer), new GameCastTarget(attacker.Pointer), 1f, strike.strike.sideCastType, 0)
            : null;

        var steps = new List<Step>();
        Range attackerHealth = Whole(attacker), defenderHealth = Whole(defender);
        bool attackerHit = false, defenderHit = false;
        void Hit(bool byDefender, DamageUiParams hit, Numbers? known)
        {
            var (by, target) = byDefender ? (defenderHealth, attackerHealth) : (attackerHealth, defenderHealth);
            if (by.Max <= 0 || target.Max <= 0)
                return;
            Numbers? Run(int a, int d) => a > 0 && d > 0 ? WithHealth(attacker, a, () => WithHealth(defender, d, () => Forecast(hit, strike.flag))) : new Numbers();
            var most = known ?? Run(attackerHealth.Max, defenderHealth.Max);
            var least = known ?? Run(attackerHealth.Min, defenderHealth.Min);
            if (most is not { } high || least is not { } low)
                return;
            var numbers = new Numbers(low.DamageMin, high.DamageMax, low.KilledMin, high.KilledMax);
            steps.Add(new Step(byDefender, numbers, target, !(byDefender ? attackerHit : defenderHit)));
            var left = new Range(Math.Max(0, target.Min - numbers.DamageMax), Math.Max(0, target.Max - numbers.DamageMin));
            if (byDefender)
                (attackerHealth, attackerHit) = (left, true);
            else
                (defenderHealth, defenderHit) = (left, true);
        }
        if (first)
            Hit(true, counterParams, null);
        // The strike: the game's own numbers when nothing came before it.
        Hit(false, strike.strike, steps.Count == 0 ? strike.game : null);
        if (counterParams is { } c && (!first || twice))
            Hit(true, c, null);
        if (again)
        {
            var count = steps.Count;
            Hit(false, strike.strike, null);
            // Into the strike: both strikes in the strike's step.
            if (mode == SecondStrikeShown.InStrike && steps.Count > count && steps.FindIndex(s => !s.ByDefender) is var at and >= 0 && at < count)
            {
                var both = steps[at] with { Numbers = Sum(steps[at].Numbers, steps[^1].Numbers, Creatures(defender.Data)) };
                steps.RemoveAt(steps.Count - 1);
                steps[at] = both;
            }
        }
        return sequences[forecast.Pointer] = new Sequence(steps, decided.Counters, mode);
    }

    // The defender's counter ability, unless the strike's own debuff forbids it (the assassin's; issue #1: to check whether
    // the game's decision already covers it).
    static GameAbility CounterOf(DamageUiParams strike, Unit defender)
    {
        if (defender.Abilities()?.Ability(EAbilityType.CounterAttack) is not { } counter)
            return null;
        var counterType = counter.LogicConfig?.attackType_;
        var ability = strike.castSource?.TryCast<GameAbility>();
        return ability != null && ability.BuffConfigs.Any(b => b.data != null && DisablesCounter(b.data, counterType)) ? null : counter;
    }

    // Both strikes in one: the damage added, the kills too, at most the whole stack.
    static Numbers Sum(Numbers first, Numbers second, int creatures) =>
        new(first.DamageMin + second.DamageMin, first.DamageMax + second.DamageMax,
            Math.Min(creatures, first.KilledMin + second.KilledMin), Math.Min(creatures, first.KilledMax + second.KilledMax));

    // A stack's whole health: one creature's for every creature under the top one and the top one's own.
    static int WholeHealth(Unit unit) =>
        unit?.Data is { } data && unit.stats is { } stats ? data.fullStacks * stats.hp + data.TopHealth : 0;

    static Range Whole(Unit unit) => new(WholeHealth(unit), WholeHealth(unit));

    Numbers? Forecast(DamageUiParams hit, bool flag) => GameNames.BuildForecast(hit, flag) is { } f ? Numbers.Of(f) : null;

    // A run with the stack at `health`: its count and its top creature's health as the game keeps them, put back after
    // the run (runs nest: both stacks of a fight set).
    T WithHealth<T>(Unit unit, int health, Func<T> run)
    {
        if (unit?.Data is not { } data || unit.stats is not { } stats || stats.hp <= 0)
            return default;
        var (stacks, top) = (data.fullStacks, data.TopHealth);
        var wasBuilding = building;
        building = true;
        try
        {
            data.fullStacks = (health - 1) / stats.hp;
            data.TopHealth = health - data.fullStacks * stats.hp;
            return run();
        }
        catch (Exception e)
        {
            Logs.Report("forecast run", e);
            return default;
        }
        finally
        {
            (data.fullStacks, data.TopHealth) = (stacks, top);
            building = wasBuilding;
        }
    }

    // Creatures in the stack: UnitData.fullStacks leaves out the top one, which has its own health (2 vampires show as 1).
    static int Creatures(UnitData data) => data.fullStacks + 1;
}
