using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hex.Configs;
using Hex.Session.World;

namespace Nobirds.OldenEra.Modding.Common;

// The encounter of a hero and a map squad, and the Diplomacy points: readable names for the game's members (Lookup,
// Members, GameCode; found once, said in the log). The comments name each member as it is on 0.82.7.
//
// Most of them are what the game's encounter (dkw.zki) reads: Game.MeetingOutcome (GameEncounter.cs)
// asks the same questions in the same order, without the encounter's actions. Each is found among the encounter's
// own calls and reads, so they are the members the game uses there.
public static partial class Game
{
    // ---------- the encounter ----------

    // The encounter: static void (hero, squad) the game calls when a hero steps onto a squad (dkw.zki). It decides and
    // acts at once (Game.MeetingOutcome has its walk-through). Of static void (hero, squad), the one calling a
    // static bool (hero, squad) (the Diplomacy check) and the lock; of the look-alikes (dkw.lkv: the same calls, other
    // lock types), the one the game calls.
    static readonly Lookup<MethodInfo> encounter = new Lookup<MethodInfo>("encounter", () =>
    {
        if (Game.HeroType is not { } hero || Game.SquadType is not { } squad)
            return null;
        bool Diplomacy(MethodInfo m) => m.IsStatic && m.ReturnType == typeof(bool) && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { hero, squad });
        bool Decides(MethodInfo m) => GameCode.Calls(m).Select(c => GameCode.At(c.target)).ToList() is var calls &&
                                      calls.Any(found => found.Any(Diplomacy)) && calls.Any(found => found.Any(IsLock));
        var deciding = Game.Types.Methods().Static().Returns(typeof(void)).Takes(hero, squad).DistinctCode().Where(Decides).ToList();
        if (deciding.Count <= 1)
            return deciding.FirstOrDefault();
        var called = GameCode.CallersOf(signature => deciding.Any(m => GameCode.Signature(m) == signature)).Select(c => c.target).ToHashSet();
        return deciding.Where(m => called.Contains(GameCode.Signature(m))).OnlyOne();
    }, Members.Name).Generate("Encounter").GenerateTarget("Encounter");

    internal static MethodInfo Encounter => encounter.Value;

    // The lock of a hero's and a squad's entities for an interaction: void M(ELockType, entity, entity) (fhu.bmer).
    static bool IsLock(MethodInfo m) =>
        !m.IsStatic && m.ReturnType == typeof(void) && m.GetParameters() is { Length: 3 } ps && ps[0].ParameterType == typeof(ELockType);

    internal static bool IsEncounterLock(MethodInfo m) => IsLock(m);

    // What the encounter calls directly, and the offsets it reads at. Read from its code once, before anything patches it.
    static readonly Lookup<List<MethodInfo>> encounterCalls = new Lookup<List<MethodInfo>>("encounter calls",
        () => Encounter is { } e ? GameCode.Calls(e).SelectMany(c => GameCode.At(c.target)).Distinct().ToList() : null, Members.Names);

    static List<MethodInfo> EncounterCalls => encounterCalls.Value ?? new List<MethodInfo>();

    static readonly Lookup<HashSet<int>> encounterOffsets = new Lookup<HashSet<int>>("encounter offsets",
        () => Encounter is { } e ? GameCode.Offsets(e) : null, s => string.Join(" ", s.OrderBy(o => o).Select(o => $"0x{o:X}")));

    static bool EncounterReads(PropertyInfo p) => encounterOffsets.Value is { } offsets && p != null &&
        GameCode.FieldOf(p) is { } f && offsets.Contains(f.offset);

    // What the encounter does (each void, called directly): the lock, the windows, the joining, the battle. The mod
    // only watches them (the decision journal).
    static readonly Lookup<List<MethodInfo>> encounterActions = new Lookup<List<MethodInfo>>("encounter actions",
        () => Encounter != null ? EncounterCalls.Where(m => m.ReturnType == typeof(void)).ToList() : null, Members.Names).GenerateTarget("EncounterActions");

    internal static List<MethodInfo> EncounterActions => encounterActions.Value ?? new List<MethodInfo>();

    // Of the encounter's actions, the battle (dkw.zkj on 0.82.7, dkw.zki on 0.82.8): the one choosing the arena (it calls
    // ArenasInfo/ArenaInfo), else the one static void (hero, squad) (joining at once takes (squad, hero)).
    static readonly Lookup<MethodInfo> encounterBattle = new Lookup<MethodInfo>("encounter battle",
        () => EncounterActions.Where(a => GameCode.Calls(a).Any(c => GameCode.At(c.target).Any(m => m.DeclaringType?.Name is "ArenasInfo" or "ArenaInfo"))).OnlyOne()
              ?? (Game.HeroType is { } hero && Game.SquadType is { } squad
                  ? EncounterActions.Where(a => a.IsStatic && a != Encounter && ParamTypes(a).SequenceEqual(new[] { hero, squad })).OnlyOne()
                  : null),
        Members.Name).GenerateTarget("EncounterBattle");

    internal static MethodInfo EncounterBattle => encounterBattle.Value;

    // The squad joining at once (dkw.zkh, static void (squad, hero)): of the encounter's actions, the one taking them.
    static readonly Lookup<MethodInfo> joinAtOnce = Lookup<MethodInfo>.Among("encounter: joins at once",
        () => Game.SquadType is { } squad && Game.HeroType is { } hero
            ? EncounterCalls.Where(m => m.IsStatic && m.ReturnType == typeof(void) && Game.ParamTypes(m).SequenceEqual(new[] { squad, hero })).DistinctCode()
            : null, Members.Name).GenerateTarget("EncounterJoinAtOnce");

    // The share of the squad's creatures that join: the static float (squad, hero) the joining calls (ffd.blor), found
    // in its code; it was told by a trial call before (the value in (0, 1]).
    static readonly Lookup<MethodInfo> joinShare = Lookup<MethodInfo>.Among("join share",
        () => joinAtOnce.Value is { } join && Game.SquadType is { } squad && Game.HeroType is { } hero
            ? GameCode.Calls(join).SelectMany(c => GameCode.At(c.target))
                .Where(m => m.IsStatic && m.ReturnType == typeof(float) && Game.ParamTypes(m).SequenceEqual(new[] { squad, hero })).DistinctCode()
            : null, Members.Name).Generate("JoinShare");

    // ---------- what the encounter reads ----------

    // The meeting check of two map entities: bool M(entity) of the entity class (fhf.bmdt), called on the hero's
    // entity with the squad's. False: nothing happens. Its code: both entities' owners (a virtual getter), then, when
    // they differ, the squad entity's virtual check against the hero's owner.
    static readonly Lookup<MethodInfo> meetCheck = Lookup<MethodInfo>.Among("meeting check",
        () => EncounterCalls.Where(m => !m.IsStatic && m.ReturnType == typeof(bool) && m.GetParameters() is { Length: 1 } ps && ps[0].ParameterType == m.DeclaringType).DistinctCode(),
        Members.Name, onlyOne: true).Generate("Meets");

    // The hero's and the squad's map entities, of the meeting check's class (fgd.cqer, frc.crom).
    static readonly Lookup<PropertyInfo> encounterHeroEntity = Lookup<PropertyInfo>.Among("hero -> entity",
        () => meetCheck.Value is { } m ? Game.PropertiesOfType(Game.HeroType, t => t == m.DeclaringType) : null, Members.Name, onlyOne: true).Generate("Entity", "GameEntity");

    static readonly Lookup<PropertyInfo> encounterSquadEntity = Lookup<PropertyInfo>.Among("squad -> entity",
        () => meetCheck.Value is { } m ? Game.PropertiesOfType(Game.SquadType, t => t == m.DeclaringType) : null, Members.Name, onlyOne: true).Generate("Entity");

    // The squad's strength: int M() of the squad (frc.mh -> DataSquad.bkmt), and the hero's army's: int M(bool) of the
    // hero (fgd.blwd(true), the army's strength times a hero factor).
    static readonly Lookup<MethodInfo> squadStrength = Lookup<MethodInfo>.Among("squad strength",
        () => EncounterCalls.Where(m => !m.IsStatic && m.DeclaringType == Game.SquadType && m.ReturnType == typeof(int) && m.GetParameters().Length == 0).DistinctCode(),
        Members.Name, onlyOne: true).Generate("SquadStrength");

    static readonly Lookup<MethodInfo> heroStrength = Lookup<MethodInfo>.Among("hero strength",
        () => EncounterCalls.Where(m => !m.IsStatic && m.DeclaringType == Game.HeroType && m.ReturnType == typeof(int) &&
                                        m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(bool) })).DistinctCode(),
        Members.Name, onlyOne: true).Generate("ArmyStrength");

    // The weak squad's share of the hero's strength: the float the encounter compares with (0.5 on 0.82.7).
    static readonly Lookup<object> weakShare = new Lookup<object>("weak squad share",
        () => Encounter is { } e ? GameCode.Floats(e).Distinct().ToList() is { Count: 1 } f ? f[0] : null : null, v => $"{v}").GenerateConstant("WeakShare");

    // The Diplomacy check: static bool (hero, squad) (ffd.bloq: ffd.blop without the explanation).
    static readonly Lookup<MethodInfo> encounterJoinCheck = Lookup<MethodInfo>.Among("diplomacy check",
        () => EncounterCalls.Where(m => m.IsStatic && m.ReturnType == typeof(bool) &&
                                        m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { Game.HeroType, Game.SquadType })).DistinctCode(),
        Members.Name, onlyOne: true).Generate("JoinsByDiplomacy");

    // ---------- the Diplomacy points ----------

    // The Diplomacy check explained: static bool (hero, squad, out string) that the encounter's check calls (ffd.blop,
    // called by ffd.bloq). Its code on 0.82.7: squad = blou(hero, squad, config), hero = blow(hero, squad, config),
    // joins = hero >= squad, the explanation "hero points: {hero} squad points: {squad}". The config is the game's
    // DiplomacyInfoConfig (cnp.cdqh); the check passes it itself.
    static readonly Lookup<MethodInfo> explainedCheck = Lookup<MethodInfo>.Among("diplomacy check explained",
        () => encounterJoinCheck.Value is { } check
            ? GameCode.Calls(check).SelectMany(c => GameCode.At(c.target))
                .Where(m => m.IsStatic && m.ReturnType == typeof(bool) && m.GetParameters() is { Length: 3 } ps && ps[2].ParameterType == typeof(string).MakeByRefType())
                .DistinctCode()
            : null,
        Members.Name, onlyOne: true).Generate("JoinsByDiplomacyExplained");

    // The points, in the order the explained check calls them: the squad's, then the hero's (static int (hero, squad, config)).
    static readonly Lookup<List<MethodInfo>> pointsMethods = new Lookup<List<MethodInfo>>("diplomacy points (squad, hero)",
        () => explainedCheck.Value is { } check
            ? GameCode.Calls(check).Select(c => GameCode.At(c.target).FirstOrDefault(m => m.IsStatic && m.ReturnType == typeof(int) &&
                    m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { Game.HeroType, Game.SquadType, typeof(DiplomacyInfoConfig) })))
                .Where(m => m != null).ToList() is { Count: 2 } found ? found : null
            : null,
        Members.Names).GenerateTarget("DiplomacyPoints");

    // The points methods (squad's, hero's), for watching what the explained check counts (DiplomacyPoints). The
    // check counts them only on its last way: a squad that always joins (isCampaignFreeDiplomacy, reaction type 5, ...),
    // never joins (reaction type 0) or a hero without Diplomacy are answered before, with no points.

    // Of the hero's side (Game.HeroSideOwner, fgd.cqej of type fqu): is it a person's, bool M() (fqu.bnyp); its AI, the
    // property of a type with a DifficultyAi (fqu.crmt, fzh; null for a person); its state, the property of a type with
    // a StateCheatClickNode (fqu.crmh, fmr).
    static readonly Lookup<MethodInfo> isPerson = Lookup<MethodInfo>.Among("side is a person's",
        () => EncounterCalls.Where(m => !m.IsStatic && m.DeclaringType == Game.SideOwnerType && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0).DistinctCode(),
        Members.Name, onlyOne: true).Generate("IsPerson");

    static readonly Lookup<PropertyInfo> sideAi = Lookup<PropertyInfo>.Among("side -> AI",
        () => Game.SideOwnerType is { } s ? Game.PropertiesOfType(s, t => Game.HasProperty(t, typeof(DifficultyAi))).Where(EncounterReads) : null,
        Members.Name, onlyOne: true).Generate("Ai", "GameSideAi");

    static readonly Lookup<PropertyInfo> sideState = Lookup<PropertyInfo>.Among("side -> state",
        () => Game.SideOwnerType is { } s ? Game.PropertiesOfType(s, t => Game.HasProperty(t, typeof(StateCheatClickNode))).Where(EncounterReads) : null,
        Members.Name, onlyOne: true).Generate("State", "GameSideState");

    // The state's bool the encounter reads (fmr.cqyh, +0x37): when set, no squad offers to flee. Of the state's bools,
    // the one at an offset the encounter reads.
    static readonly Lookup<PropertyInfo> noFleeOffers = Lookup<PropertyInfo>.Among("side state: no flee offers",
        () => sideState.Value is { } state
            ? GameCode.Fields(state.PropertyType).Where(f => !f.isStatic && f.type == "System.Boolean").Select(f => GameCode.PropertyOf(state.PropertyType, f.name))
                .Where(EncounterReads)
            : null,
        Members.Name, onlyOne: true).Generate("NoFleeOffers");

    // A game-wide static bool the encounter reads (lh.bvdw): when set, a person's hero always fights. Its class shows in
    // the encounter's code only once the encounter has run (IL2CPP sets the class there on the first call): until then
    // it is not known and taken as not set (it is 0 in a normal game), and looked for again every few seconds.
    static PropertyInfo alwaysBattle;
    static float nextAlwaysBattleSearch;

    // Into the generated names as GameNames.AlwaysBattle (found once the encounter has run).
    static readonly bool alwaysBattleGenerated = Register();

    static bool Register()
    {
        GeneratedNames.All.Add(new GeneratedNames.Entry("AlwaysBattle", null, "always battle (static)",
            () => FindAlwaysBattle() is { } p ? new object[] { p } : Array.Empty<object>()));
        return true;
    }

    static PropertyInfo FindAlwaysBattle()
    {
        if (alwaysBattle != null || Encounter is not { } e || Environment.TickCount64 / 1000f < nextAlwaysBattleSearch)
            return alwaysBattle;
        nextAlwaysBattleSearch = Environment.TickCount64 / 1000f + 5f;   // not Unity's clock: the generator runs these searches outside the game too
        alwaysBattle = GameCode.StaticReads(e)
            .Where(r => r.type != null)
            .SelectMany(r => GameCode.Fields(r.type).Where(f => f.isStatic && f.offset == r.offset && f.type == "System.Boolean").Select(f => GameCode.PropertyOf(r.type, f.name)))
            .Where(p => p != null).Distinct().OnlyOne();
        if (alwaysBattle != null)
            Game.Log?.Invoke($"Game: always battle (static) = {Members.Name(alwaysBattle)}");
        return alwaysBattle;
    }
}
