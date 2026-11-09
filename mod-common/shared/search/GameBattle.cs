using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hex.Configs;
using Hex.Session.Battle;

namespace Nobirds.OldenEra.Modding.Common;

// The searches for the game's battle members (see search/Game.cs): a unit's data, config, abilities and buffs, an
// attack's forecast and the counter decision. Used through the generated names (GameNames.g.cs): unit.Data,
// unit.LogicConfig, unit.Abilities()?.Ability(type), ability.LogicConfig, ability.CastConfigs, ability.BuffConfigs,
// unit.Buffs?.Lists, forecast.DamageMin...KilledMax, GameNames.BuildForecast(params, flag).
public static partial class Game
{
    static Type[] BattleTypes => SafeTypes(typeof(BhHealDamageForecastPanel).Assembly);

    // Unit -> UnitData (fullStacks and the other changing numbers of the stack).
    static readonly Lookup<PropertyInfo> unitData = Lookup<PropertyInfo>.Among("Unit -> UnitData",
        () => typeof(Unit).GetProperties().Where(p => p.PropertyType == typeof(UnitData) && !p.Name.StartsWith("_")), Name, onlyOne: true).Generate("Data");

    // Unit -> UnitLogicConfig (its passives).
    static readonly Lookup<PropertyInfo> unitConfig = Lookup<PropertyInfo>.Among("Unit -> UnitLogicConfig",
        () => typeof(Unit).GetProperties().Where(p => p.PropertyType == typeof(UnitLogicConfig) && !p.Name.StartsWith("_")), Name).Generate("LogicConfig");


    // The unit plate's health bar (BhBattleUnitInfoView.bjeg(Unit) on 0.82.7): the plate's void M(Unit) that sets a fill.
    // It fills the bar with the stack's top creature's health over one creature's (unit.stats.hp).
    static readonly Lookup<MethodInfo> plateHealthBar = Lookup<MethodInfo>.Among("unit plate: health bar",
        () => typeof(BhBattleUnitInfoView).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(void) && ParamTypes(m).SequenceEqual(new[] { typeof(Unit) }) &&
                        GameCode.Calls(m).Any(c => GameCode.At(c.target).Any(t => t.Name == "set_fillAmount")))
            .DistinctCode(),
        Members.Name, onlyOne: true);

    // The health of the stack's top creature (UnitData.cpme, +0x40 on 0.82.7): the int of the unit's data the health bar
    // reads (through unit.Data); a stack's whole health is fullStacks * stats.hp + this.
    static readonly Lookup<PropertyInfo> topHealth = Lookup<PropertyInfo>.Among("UnitData: top creature's health",
        () => plateHealthBar.Value is { } bar && unitData.Value is { } data && GameCode.FieldOf(data) is { } holder
            ? GameCode.IntReadsThrough(bar, holder.offset) is var offsets
                ? typeof(UnitData).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(p => p.PropertyType == typeof(int) && GameCode.FieldOf(p) is { } f && offsets.Contains(f.offset))
                : null
            : null,
        Name, onlyOne: true).Generate("TopHealth", writable: true);

    // A unit's ability in battle (eji on 0.82.7): the type with Init(AbilityLogicConfig, AbilityViewConfig, AbilityID).
    static readonly Lookup<Type> abilityType = Lookup<Type>.Among("battle ability",
        () => BattleTypes.Where(t => t.GetMethod("Init", new[] { typeof(AbilityLogicConfig), typeof(AbilityViewConfig), typeof(AbilityID) }) != null),
        t => t.Name).Generate("GameAbility");

    public static Type AbilityType => abilityType.Value;

    // Unit -> its ability container (bgxs, an ejh), the one with M(EAbilityType) -> ability (beth).
    static readonly Lookup<MethodInfo> abilitiesGetter = Lookup<MethodInfo>.Among("Unit -> ability container",
        () => AbilityType is { } a
            ? typeof(Unit).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetParameters().Length == 0 && !m.Name.StartsWith("get_") && m.ReturnType.GetMethods().Any(x => x.ReturnType == a && ParamTypes(x).SequenceEqual(new[] { typeof(EAbilityType) })))
            : null,
        m => $"{m.Name} -> {m.ReturnType.Name}").Generate("Abilities");

    static readonly Lookup<Type> abilitiesType = Lookup<Type>.Among("ability container",
        () => abilitiesGetter.Value is { } getter ? new[] { getter.ReturnType } : null, t => t.Name).Generate("GameAbilities");


    // The class that declares the abilities getter (eon on 0.82.7, a base of the battle unit).
    static readonly Lookup<Type> abilitiesOwner = Lookup<Type>.Among("ability container's owner",
        () => abilitiesGetter.Value is { } getter ? new[] { getter.DeclaringType } : null, t => t.Name).Generate("GameUnitBase");

    static readonly Lookup<MethodInfo> abilityByType = Lookup<MethodInfo>.Among("ability container -> ability (EAbilityType)",
        () => abilitiesGetter.Value?.ReturnType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(x => x.ReturnType == AbilityType && ParamTypes(x).SequenceEqual(new[] { typeof(EAbilityType) })).DistinctCode(), Name).Generate("Ability");

    // Ability -> its logic config (AbilityLogicConfig: attackType_ "melee", "shoot", ...).
    static readonly Lookup<PropertyInfo> abilityLogic = Lookup<PropertyInfo>.Among("ability -> AbilityLogicConfig",
        () => AbilityType?.GetProperties().Where(p => p.PropertyType == typeof(AbilityLogicConfig) && !p.Name.StartsWith("_")), Name).Generate("LogicConfig");

    // Ability -> the holders of its configs (CastDealerConfig, BuffConfig...): its parameterless getters whose type has a
    // CastDealerConfig; through them its cast configs (CastDealerConfig carries triggerCounter) and its buff configs (the
    // debuff its hit puts on the target, e.g. assassin_debuff). Some of the buff getters look the buff up and throw when
    // the ability has none (seen: AbilityLogicConfig.swr): GameNames.Each leaves them out.
    static IEnumerable<MethodInfo> ConfigHolderGetters() =>
        AbilityType?.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetParameters().Length == 0 && m.ReturnType.GetProperties().Any(p => p.PropertyType == typeof(CastDealerConfig)))
        ?? Enumerable.Empty<MethodInfo>();

    static IEnumerable<MemberInfo[]> ConfigPaths(Type config) =>
        ConfigHolderGetters().SelectMany(getter => getter.ReturnType.GetProperties()
            .Where(p => p.PropertyType == config && !p.Name.StartsWith("_")).Select(p => new MemberInfo[] { getter, p }));

    static readonly Lookup<MemberInfo[]> castConfigs = Lookup<MemberInfo[]>.Among("ability -> cast configs",
        () => ConfigPaths(typeof(CastDealerConfig)), p => string.Join(".", p.Select(m => m.Name))).GenerateAll("CastConfigs");

    static readonly Lookup<MemberInfo[]> buffConfigs = Lookup<MemberInfo[]>.Among("ability -> buff configs",
        () => ConfigPaths(typeof(BuffConfig)), p => string.Join(".", p.Select(m => m.Name))).GenerateAll("BuffConfigs");

    // ---------- buffs on a unit in battle ----------

    // A buff in battle: the type with readable `config` and `dataConfig` (BuffConfig); the unit holds a container with
    // lists of them (a spell such as Masterful Parry, the hero's Formation skill, sleep, shackles...).
    static readonly Lookup<Type> buffType = Lookup<Type>.Among("battle buff",
        () => SafeTypes(typeof(Unit).Assembly).Where(t => t.GetProperty("config")?.PropertyType == typeof(BuffConfig) &&
                                                          t.GetProperty("dataConfig")?.PropertyType == typeof(BuffConfig)),
        t => t.Name).Generate("GameBuff");

    static bool IsBuffList(Type t) =>
        buffType.Value is { } buff && t.IsGenericType && t.GetGenericArguments()[0] == buff && t.Name.StartsWith("List");

    static readonly Lookup<PropertyInfo> unitBuffs = Lookup<PropertyInfo>.Among("Unit -> buffs",
        () => buffType.Value != null
            ? typeof(Unit).GetProperties().Where(p => !p.Name.StartsWith("_") && p.PropertyType.GetProperties().Any(q => IsBuffList(q.PropertyType)))
            : null, Name).Generate("Buffs", "GameBuffs");

    static readonly Lookup<PropertyInfo> buffLists = Lookup<PropertyInfo>.Among("buffs -> lists",
        () => unitBuffs.Value?.PropertyType.GetProperties().Where(q => !q.Name.StartsWith("_") && IsBuffList(q.PropertyType)), Name).GenerateAll("Lists");

    // A passive of the unit's config -> its buff data (where a passive's sequence effect is, e.g. counter_before_attack).
    static readonly Lookup<PropertyInfo> passiveBuffs = Lookup<PropertyInfo>.Among("passive -> buff data",
        () => unitConfig.Value?.PropertyType.GetProperty(nameof(UnitLogicConfig.passives))?.PropertyType is { IsGenericType: true } list
            ? list.GetGenericArguments()[0].GetProperties().Where(p => p.PropertyType == typeof(BuffData))
            : null, Name).GenerateAll("BuffDatas");

    // ---------- the forecast of an attack, the counter decision ----------

    // The forecast the panel shows (exv on 0.82.7): the type BhHealDamageForecastPanel.Show takes.
    static readonly Lookup<Type> forecastType = Lookup<Type>.Among("battle forecast",
        () => typeof(BhHealDamageForecastPanel).GetMethod(nameof(BhHealDamageForecastPanel.Show))?.GetParameters() is { Length: > 0 } ps ? new[] { ps[0].ParameterType } : null,
        t => t.Name).Generate("GameForecast");

    public static Type ForecastType => forecastType.Value;

    // The forecast's numbers: of its ints, the ones the forecast panel's Show reads (BhHealDamageForecastPanel.Show(exv),
    // what it writes into its rows), in the game's order: damage min, damage max, killed min, killed max (cozu.. on 0.82.7).
    static readonly Lookup<HashSet<int>> forecastShowOffsets = new Lookup<HashSet<int>>("forecast panel show offsets",
        () => typeof(BhHealDamageForecastPanel).GetMethod(nameof(BhHealDamageForecastPanel.Show)) is { } show ? GameCode.Offsets(show) : null,
        s => string.Join(" ", s.OrderBy(o => o).Select(o => $"0x{o:X}")));

    static PropertyInfo ForecastInt(int index) =>
        ForecastType is { } f && forecastShowOffsets.Value is { } read
            ? GameCode.Fields(f).Where(x => !x.isStatic && x.type == "System.Int32" && read.Contains(x.offset)).OrderBy(x => x.offset)
                .Skip(index).Select(x => GameCode.PropertyOf(f, x.name)).FirstOrDefault()
            : null;

    static Lookup<PropertyInfo> ForecastNumber(string what, int index, string name) => Lookup<PropertyInfo>.Among(what,
        () => ForecastInt(index) is { } p ? new[] { p } : null, Name).Generate(name, writable: true);

    static readonly Lookup<PropertyInfo> damageMin = ForecastNumber("forecast: damage min", 0, "DamageMin");
    static readonly Lookup<PropertyInfo> damageMax = ForecastNumber("forecast: damage max", 1, "DamageMax");
    static readonly Lookup<PropertyInfo> killedMin = ForecastNumber("forecast: killed min", 2, "KilledMin");
    static readonly Lookup<PropertyInfo> killedMax = ForecastNumber("forecast: killed max", 3, "KilledMax");

    // An attack's parameters (DamageUiParams): its caster, cast source and target are the game's interface wrappers (elm,
    // eln, elq on 0.82.7); a unit or an ability is one of them by its pointer (new GameCaster(unit.Pointer)).
    static Lookup<Type> ParamsType(string property, string name) => Lookup<Type>.Among($"DamageUiParams.{property}",
        () => typeof(DamageUiParams).GetProperty(property)?.PropertyType is { } t ? new[] { t } : null, t => t.Name).Generate(name);

    static readonly Lookup<Type> casterType = ParamsType(nameof(DamageUiParams.caster), "GameCaster");
    static readonly Lookup<Type> castSourceType = ParamsType(nameof(DamageUiParams.castSource), "GameCastSource");
    static readonly Lookup<Type> castTargetType = ParamsType(nameof(DamageUiParams.target), "GameCastTarget");

    // The game's builders of an attack's forecast: static forecast M(DamageUiParams, bool) (exw.bkg and clones); the
    // patches take them all, the mod calls one (GameNames.BuildForecast: the same code).
    static readonly Lookup<List<MethodInfo>> forecastBuilders = new Lookup<List<MethodInfo>>("forecast builders",
        () => ForecastType is { } f
            ? BattleTypes.Methods().Static().Returns(f).Takes(typeof(DamageUiParams), typeof(bool)).ToList() is { Count: > 0 } found ? found : null
            : null,
        Members.Names).GenerateTarget("ForecastBuilders");

    public static List<MethodInfo> ForecastBuilders => forecastBuilders.Value ?? new List<MethodInfo>();

    static readonly Lookup<MethodInfo> forecastBuilder = Lookup<MethodInfo>.Among("forecast builder",
        () => forecastBuilders.Value?.DistinctCode(), Name).Generate("BuildForecast");

    // Every other method making a forecast (spells, heals), not one with a builder's code.
    static readonly Lookup<List<MethodInfo>> otherForecasts = new Lookup<List<MethodInfo>>("other forecast producers", () =>
    {
        if (ForecastType is not { } f)
            return null;
        var builders = ForecastBuilders.Select(NativeAddress).ToHashSet();
        return BattleTypes.Methods().Returns(f).Where(m => !builders.Contains(NativeAddress(m))).ToList();
    }, l => $"{l.Count}").GenerateTarget("OtherForecastMakers");


    // The game's decision whether the defender answers: bool (Unit attacker, Unit defender, ability, hex, list), the one
    // the battle's hover handler calls from its void (DamageUiParams) step that shows the swords (epm.bhdj -> eqe.bhqj).
    static readonly Lookup<MethodInfo> counterDecision = new Lookup<MethodInfo>("counter decision", () =>
    {
        if (AbilityType is not { } ability)
            return null;
        var types = SafeTypes(typeof(Unit).Assembly).ToList();
        var candidates = types.Methods().Returns(typeof(bool))
            .Where(m => m.GetParameters() is { Length: 5 } ps && ps[0].ParameterType == typeof(Unit) && ps[1].ParameterType == typeof(Unit) &&
                        ps[2].ParameterType == ability && ps[3].ParameterType.Name == "Hex" && ps[4].ParameterType.IsGenericType)
            .ToList();
        var byAddress = candidates.GroupBy(m => NativeAddress(m).ToInt64()).ToDictionary(g => g.Key, g => g.First());
        var called = types.Methods().Instance().Returns(typeof(void)).Takes(typeof(DamageUiParams))
            .SelectMany(caller => GameCode.Calls(caller).Where(c => !c.jump && byAddress.ContainsKey(c.target)).Select(c => byAddress[c.target]))
            .Distinct().ToList();
        return called.OnlyOne();
    }, Name).GenerateTarget("CounterDecision");

    // The battle's logic (eqe on 0.82.7): the class of the counter decision; it also decides the rest of an attack's
    // sequence (the sequence effects of the units' passives and buffs: counter_before_attack, add_attack_after_counter...).
    static readonly Lookup<Type> battleLogic = Lookup<Type>.Among("battle logic",
        () => counterDecision.Value is { } d ? new[] { d.DeclaringType } : null, t => t.Name).Generate("GameBattleLogic");

    // Whether the attacker strikes again after the counter (Double Strike; eqe.bhqa(Unit, eji) on 0.82.7, called by the
    // attack's sequence): bool M(Unit, ability) of the battle logic that goes on to the check by attack type, bool
    // M(Unit, EAttackType) (eqe.bhqb: does the unit carry add_attack_after_counter_melee / _range / _shoot / _cast).
    static readonly Lookup<MethodInfo> extraStrike = Lookup<MethodInfo>.Among("battle logic: a second strike after the counter",
        () => battleLogic.Value is { } logic && AbilityType is { } a
            ? logic.Methods().Instance().Returns(typeof(bool)).Takes(typeof(Unit), a)
                .Where(m => GameCode.Calls(m).Any(c => GameCode.At(c.target).Any(t => t.ReturnType == typeof(bool) && ParamTypes(t).SequenceEqual(new[] { typeof(Unit), typeof(EAttackType) }))))
                .DistinctCode()
            : null, Name).Generate("StrikesAgainAfterCounter");

    // Whether the unit carries a sequence effect (Unit.bjnz(String) on 0.82.7: its passives' and buffs' sequenceEffect, e.g.
    // "counter_before_and_after_attack"): bool M(string) of the unit the second strike's check calls.
    static readonly Lookup<MethodInfo> hasSequenceEffect = Lookup<MethodInfo>.Among("unit: has a sequence effect",
        () => extraStrike.Value is { } check
            ? GameCode.Calls(check).SelectMany(c => GameCode.At(c.target))
                .Where(m => m.DeclaringType == typeof(Unit) && m.ReturnType == typeof(bool) && ParamTypes(m).SequenceEqual(new[] { typeof(string) })).Distinct()
            : null, Name).Generate("HasSequenceEffect");

    // Whether the defender answers before the strike (eqe.bhpz(Unit) on 0.82.7, called by the attack's sequence: the unit
    // carries counter_before_attack or counter_before_and_after_attack): bool M(Unit) of the battle logic that asks the
    // unit's sequence effects.
    static readonly Lookup<MethodInfo> countersFirst = Lookup<MethodInfo>.Among("battle logic: the defender answers before the strike",
        () => battleLogic.Value is { } logic && hasSequenceEffect.Value is { } has
            ? logic.Methods().Instance().Returns(typeof(bool)).Takes(typeof(Unit))
                .Where(m => GameCode.Calls(m).Any(c => GameCode.At(c.target).Contains(has))).DistinctCode()
            : null, Name).Generate("AnswersBeforeStrike");
}
