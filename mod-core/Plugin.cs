using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Hex.Session.Data;
using Hex.Session.World;
using Hex.UI;
using Hex.UI.Tooltips;
using Il2CppInterop.Runtime.InteropTypes;
using OldenEra.Modding.Common;
using UnityEngine;

namespace OldenEra.Modding.Core;

// The core: the game's events for our mods (ModEvents in OldenEraCommon.dll). It patches the game's methods once, for
// all the mods, and raises each event with its typed context; a mod only subscribes (ModBase.On). A method of the game
// not found: its event does not come, and the log says which.
//
// The game's members through Game (mod-common/shared/Game*.cs). This plugin reloads like the others (devplugins).
[BepInPlugin("nobirds.oldenera.core", "Olden Era Core", "0.7.3")]
public class Plugin : ModBase
{
    static ModLog log;

    protected override bool HasMenu => false;

    protected override void OnLoad()
    {
        log = Logs;
        Game.Log = log.Info;
        ModEvents.Log = log.Error;
        var harmony = Harmony;
        harmony.PatchOne(GameTargets.SquadTooltipFill, "squad tooltip filling / filled",
            prefix: Patch(nameof(SquadTooltipPrefix)), postfix: Patch(nameof(SquadTooltipPostfix)));
        harmony.PatchOne(GameTargets.TooltipSetTexts, "tooltip texts", prefix: Patch(nameof(TooltipTextsPrefix)));
        harmony.PatchOne(GameTargets.SkullsLevel, "the skulls' strengths", postfix: Patch(nameof(SkullsLevelPostfix)));
        harmony.PatchOne(GameTargets.SkullsForSquad, "skulls filled (a squad)", prefix: Patch(nameof(SkullsPrefix)), postfix: Patch(nameof(SkullsForSquadPostfix)));
        harmony.PatchOne(GameTargets.SkullsForParty, "skulls filled (an army)", prefix: Patch(nameof(SkullsPrefix)), postfix: Patch(nameof(SkullsForPartyPostfix)));
        harmony.PatchOne(GameTargets.HireTooltipFill, "dwelling tooltip filling / filled",
            prefix: Patch(nameof(DwellingTooltipPrefix)), postfix: Patch(nameof(DwellingTooltipPostfix)));
        harmony.PatchOne(GameTargets.LanguageChanged, "language changed", prefix: Patch(nameof(LanguageChangedPrefix)));
        PatchEncounter(harmony);
        PatchMapTooltip(harmony);
        PatchBattle(harmony);
        OnTick(Watch);
        ModMenu.NotSinglePlayer = NotSinglePlayer;
        // The library of a manager before 0.3.4 has no HotseatGame: then the core goes on without it.
        try { AnswerHotseat(HotseatGame); }
        catch (MissingMemberException) { log.Info("the library has no hotseat answer yet (an older manager): hotseat counts as not single player"); }
        ModEvents.SourceReady = true;
        log.Info("The game's events ready");
    }

    protected override void OnUnload()
    {
        ModEvents.SourceReady = false;
        ModMenu.NotSinglePlayer = null;
        try { AnswerHotseat(null); }
        catch (MissingMemberException) { }
    }

    // Apart, so that a library without the property fails here (when this is compiled), not in Load.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void AnswerHotseat(Func<bool> answer) => ModMenu.HotseatGame = answer;

    // ---------- single player or not (the mods' mode) ----------

    // Asked by every mod's switches, so: the game's session is looked at once a frame, and the answer is worked out
    // again only when the session or its map data is another object (a new game, a load, back to the menu).
    static int sessionFrame = -1;
    static (IntPtr session, IntPtr data) seenSession = (IntPtr.Zero, IntPtr.Zero);
    static bool notSinglePlayer, hotseat;

    static bool HotseatGame()
    {
        NotSinglePlayer();
        return hotseat;
    }

    static bool NotSinglePlayer()
    {
        if (Time.frameCount == sessionFrame)
            return notSinglePlayer;
        sessionFrame = Time.frameCount;
        var session = GameNames.Session;
        var seen = (session?.Pointer ?? IntPtr.Zero, session?.TryCast<GameMapGameSession>()?.MapData?.Pointer ?? IntPtr.Zero);
        if (seen == seenSession)
            return notSinglePlayer;
        seenSession = seen;
        notSinglePlayer = !Game.SinglePlayer(session);
        hotseat = Game.Hotseat(session);
        if (session != null)
            log.Info($"session: {session.Play}, hotseat sides {session.TryCast<GameMapGameSession>()?.MapData?.sides?.hotseatSides?.Length ?? 0}: " +
                     (hotseat ? "hotseat" : notSinglePlayer ? "not single player" : "single player"));
        return notSinglePlayer;
    }

    static HarmonyMethod Patch(string name) => new(typeof(Plugin), name);

    // ---------- the language ----------

    // The game tells each of its UI texts that the language changed (cpz.ihb), after loading the new language's table:
    // at the first of them in a frame our texts go into that table (Texts.Refresh), so the game's texts showing our keys
    // find them; when the language is another one, LanguageChanged.
    static int languageFrame = -1;

    static void LanguageChangedPrefix()
    {
        if (Time.frameCount == languageFrame)
            return;
        languageFrame = Time.frameCount;
        if (log.Guard("language", Texts.Refresh) is { } changedTo)
            ModEvents.LanguageChanged.Raise(changedTo);
    }

    static MapHero? SelectedHero => Game.SelectedHero is Il2CppObjectBase hero ? new MapHero(hero) : null;

    // ---------- the squad tooltip ----------

    static void SquadTooltipPrefix(BhTooltipSquad __instance, ESquadTooltipType __0, Il2CppSystem.Object __1, out SquadTooltipFill __state)
    {
        __state = null;
        if (!ModEvents.SquadTooltipFilling.Listened && !ModEvents.SquadTooltipFilled.Listened)
            return;
        __state = log.Guard("squad tooltip", () => new SquadTooltipFill
        {
            Tooltip = __instance,
            Kind = __0,
            Target = __1,
            Squad = __1?.AsSquadEntity()?.Squad is { } squad ? new MapSquad(squad) : null,
            MapObject = __1?.AsMapObject() is Il2CppObjectBase mapObject ? new MapObject(mapObject) : null,
            Hero = SelectedHero,
        });
        if (__state != null)
            ModEvents.SquadTooltipFilling.Raise(__state);
    }

    static void SquadTooltipPostfix(SquadTooltipFill __state)
    {
        if (__state != null)
            ModEvents.SquadTooltipFilled.Raise(__state);
    }

    // ---------- a tooltip's texts ----------

    // The texts the game puts into a tooltip, changed by the handlers: sent again, changed, in place of the game's call.
    static bool resendingTexts;

    static bool TooltipTextsPrefix(BhTooltip __instance, BasicTooltip __0, GameTooltipOptions __1)
    {
        if (resendingTexts || !ModEvents.TooltipTexts.Listened || __instance == null || __0 == null)
            return true;
        var texts = new TooltipTexts { Tooltip = __instance, Header = __0.Header, SubHeader = __0.SubHeader, Content = __0.Content, Extra = __0.Extra };
        ModEvents.TooltipTexts.Raise(texts);
        if (texts.Header == __0.Header && texts.SubHeader == __0.SubHeader && texts.Content == __0.Content && texts.Extra == __0.Extra)
            return true;
        return log.Guard("tooltip texts", () =>
        {
            var changed = new BasicTooltip
            {
                Header = texts.Header, SubHeader = texts.SubHeader, Content = texts.Content, Extra = texts.Extra,
                localizationIsNeeded = __0.localizationIsNeeded,
            };
            resendingTexts = true;
            try { __instance.SetTexts(changed, __1); }
            finally { resendingTexts = false; }
            return false;
        }, true);
    }

    // ---------- the skulls widget ----------

    // The strengths the game compares for the level (BhSkulls.ump), called while it fills the widget.
    static (int hero, int squad)? powers;

    static void SkullsPrefix() => powers = null;

    static void SkullsLevelPostfix(int __0, int __1) => powers = (__0, __1);

    static void SkullsForSquadPostfix(BhSkulls __instance, object __0, int __result) =>
        RaiseSkulls(__instance, __0 is Il2CppObjectBase squad ? new MapSquad(squad) : null, null, __result);

    static void SkullsForPartyPostfix(BhSkulls __instance, DataParty __0, int __result) => RaiseSkulls(__instance, null, __0, __result);

    static void RaiseSkulls(BhSkulls skulls, MapSquad? squad, DataParty party, int level)
    {
        if (!ModEvents.SkullsFilled.Listened || skulls == null)
            return;
        var fill = log.Guard("skulls", () => new SkullsFill
        {
            Skulls = skulls, Squad = squad, Party = party, Level = level,
            HeroPower = powers?.hero, SquadPower = powers?.squad,
            InSquadTooltip = skulls.GetComponentInParent<BhTooltipSquad>(true) != null,
        });
        if (fill != null)
            ModEvents.SkullsFilled.Raise(fill);
    }

    // ---------- the dwelling tooltip ----------

    static void DwellingTooltipPrefix(BhTooltipHire __instance, object __0, out DwellingTooltipFill __state)
    {
        __state = __0 is Il2CppObjectBase dwelling ? new DwellingTooltipFill { Tooltip = __instance, Dwelling = new MapObject(dwelling) } : null;
        if (__state != null)
            ModEvents.DwellingTooltipFilling.Raise(__state);
    }

    static void DwellingTooltipPostfix(DwellingTooltipFill __state)
    {
        if (__state != null)
            ModEvents.DwellingTooltipFilled.Raise(__state);
    }

    // ---------- the encounter ----------

    // The game's encounter (Game.Encounter, dkw.zki) and its actions (the lock, the windows, the joining, the battle),
    // watched (the prefixes only note the call): what the game did, by them.
    static EncounterSeen encounter;
    static ELockType encounterLocks;
    static readonly List<MethodBase> encounterActions = new();

    static void PatchEncounter(Harmony harmony)
    {
        var actions = GameTargets.EncounterActions;
        var locks = actions.Where(Game.IsEncounterLock).ToList();
        harmony.PatchOne(GameTargets.Encounter, "encounter", prefix: Patch(nameof(EncounterPrefix)), postfix: Patch(nameof(EncounterPostfix)));
        harmony.PatchEach(locks, prefix: Patch(nameof(EncounterLockPrefix)));
        harmony.PatchEach(actions.Except(locks), prefix: Patch(nameof(EncounterActionPrefix)));
    }

    static void EncounterPrefix(GameHero __0, GameSquad __1)
    {
        encounter = null;
        encounterLocks = ELockType.None;
        encounterActions.Clear();
        if (__0 is not { } hero || __1 is not { } squad)
            return;
        encounter = log.Guard("encounter", () => new EncounterSeen
        {
            Hero = new MapHero(hero), Squad = new MapSquad(squad),
            ByPerson = hero.SideOwner is { } side && side.IsPerson(),   // the game's own "a person plays the side" (fqu.cwyd on 0.82.7)
        });
        if (encounter != null)
            ModEvents.EncounterStarting.Raise(encounter);
    }

    static void EncounterLockPrefix(ELockType __0)
    {
        if (encounter != null)
            encounterLocks |= __0;
    }

    static void EncounterActionPrefix(MethodBase __originalMethod)
    {
        if (encounter != null)
            encounterActions.Add(__originalMethod);
    }

    static void EncounterPostfix()
    {
        if (encounter == null)
            return;
        var seen = encounter;
        encounter = null;
        // By what it did: the lock it put (a window for a person: the "weak squad" one, the hire one), the joining at once
        // (dkw.zkh, a free campaign Diplomacy), the battle (dkw.zkj); none of them: nothing happened.
        seen.Outcome = encounterLocks.HasFlag(ELockType.WeakSquadWarning) ? EncounterOutcome.Flees
            : encounterLocks.HasFlag(ELockType.Diplomacy) || Did(GameTargets.EncounterJoinAtOnce) ? EncounterOutcome.Joins
            : Did(GameTargets.EncounterBattle) ? EncounterOutcome.Battle
            : EncounterOutcome.Nothing;
        ModEvents.Encounter.Raise(seen);
    }

    // Whether the encounter called the action (Harmony may hand the patched method as another MethodBase of it).
    static bool Did(MethodInfo action) => action != null &&
        encounterActions.Any(a => a == action || (a.Name == action.Name && a.DeclaringType == action.DeclaringType));

    // ---------- a map object's tooltip: shown, hidden, every frame, its place ----------

    static void PatchMapTooltip(Harmony harmony)
    {
        var tooltip = typeof(BhTooltip);
        harmony.PatchOne(AccessTools.Method(tooltip, nameof(BhTooltip.Show), new[] { typeof(Camera), typeof(GameObject), typeof(float) }),
            "tooltip shown", postfix: Patch(nameof(TooltipShowPostfix)));
        harmony.PatchOne(AccessTools.Method(tooltip, nameof(BhTooltip.Hide), new[] { typeof(bool) }), "tooltip hidden", prefix: Patch(nameof(TooltipHidePrefix)));
        harmony.PatchOne(AccessTools.Method(tooltip, nameof(BhTooltip.LateUpdate)), "tooltip late update", postfix: Patch(nameof(TooltipLatePostfix)));
        if (harmony.PatchEach(GameTargets.TooltipPlaceForPoint, postfix: Patch(nameof(TooltipPlacePostfix))).Count == 0)
            log.Warn("Patch: tooltip place: the positioner's place for a point not found, so off");
    }

    static void TooltipShowPostfix(BhTooltip __instance, Camera __0, GameObject __1)
    {
        if (__instance != null && __1 != null)
            ModEvents.TooltipShown.Raise(new TooltipShown { Tooltip = __instance, Camera = __0, Target = __1 });
    }

    static void TooltipHidePrefix(BhTooltip __instance)
    {
        if (__instance != null)
            ModEvents.TooltipHidden.Raise(__instance);
    }

    static void TooltipLatePostfix(BhTooltip __instance)
    {
        if (__instance != null && ModEvents.TooltipLateUpdate.Listened)
            ModEvents.TooltipLateUpdate.Raise(__instance);
    }

    static void TooltipPlacePostfix(Il2CppObjectBase __instance, Vector2 __0, ref Vector2 __result)
    {
        if (__instance == null || !ModEvents.TooltipPlace.Listened)
            return;
        var place = new TooltipPlace { Positioner = __instance, Point = __0, Place = __result };
        ModEvents.TooltipPlace.Raise(place);
        __result = place.Place;
    }

    // ---------- the battle: forecasts and the counter decision ----------

    static void PatchBattle(Harmony harmony)
    {
        if (harmony.PatchEach(GameTargets.ForecastBuilders, postfix: Patch(nameof(ForecastBuiltPostfix))).Count == 0)
            log.Warn("Patch: forecast built: the game's forecast builders not found, so off");
        harmony.PatchOne(AccessTools.Method(typeof(Hex.Session.Battle.BhHealDamageForecastPanel), nameof(Hex.Session.Battle.BhHealDamageForecastPanel.Show)),
            "forecast shown", postfix: Patch(nameof(ForecastShownPostfix)));
        harmony.PatchOne(GameTargets.CounterDecision, "counter decided", postfix: Patch(nameof(CounterDecidedPostfix)));
        harmony.PatchEach(GameTargets.OtherForecastMakers, postfix: Patch(nameof(OtherForecastPostfix)));
    }

    static void ForecastBuiltPostfix(MethodBase __originalMethod, Hex.Session.Battle.DamageUiParams __0, bool __1, object __result)
    {
        if (__result is Il2CppObjectBase forecast && ModEvents.ForecastBuilt.Listened)
            ModEvents.ForecastBuilt.Raise(new ForecastBuilt { Builder = (MethodInfo)__originalMethod, Params = __0, Flag = __1, Forecast = new BattleForecast(forecast) });
    }

    static void OtherForecastPostfix(object __result)
    {
        if (__result is Il2CppObjectBase forecast && ModEvents.OtherForecast.Listened)
            ModEvents.OtherForecast.Raise(new BattleForecast(forecast));
    }

    static void ForecastShownPostfix(Hex.Session.Battle.BhHealDamageForecastPanel __instance, object __0)
    {
        if (ModEvents.ForecastShown.Listened)
            ModEvents.ForecastShown.Raise(new ForecastShown { Panel = __instance, Forecast = __0 is Il2CppObjectBase f ? new BattleForecast(f) : null });
    }

    static void CounterDecidedPostfix(object __instance, object __0, object __1, object __2, bool __result)
    {
        if (ModEvents.CounterDecided.Listened)
            ModEvents.CounterDecided.Raise(new CounterDecided
            {
                Battle = __instance as Il2CppObjectBase, Ability = __2 as Il2CppObjectBase,
                Attacker = __0 as Il2CppObjectBase, Defender = __1 as Il2CppObjectBase, Counters = __result,
            });
    }

    // ---------- every frame: the map, the hero, the alt info key ----------

    static bool mapOpen, altInfo;
    static IntPtr shownHero;
    static int nextMapCheck;

    static void Watch()
    {
        if (Time.frameCount >= nextMapCheck)
        {
            nextMapCheck = Time.frameCount + 30;
            var open = Game.MapObjects() != null;
            if (open != mapOpen)
            {
                mapOpen = open;
                if (!open)
                {
                    // The map's state is over: a mod loaded later does not start from it.
                    ModEvents.MapLoaded.Forget();
                    ModEvents.HeroSelected.Forget();
                    shownHero = IntPtr.Zero;
                }
                (open ? ModEvents.MapLoaded : ModEvents.MapClosed).Raise(new MapSession { At = DateTime.Now });
                log.Info(open ? "map loaded" : "map closed");
            }
        }
        if (!mapOpen)
            return;
        var hero = SelectedHero;
        if ((hero?.Pointer ?? IntPtr.Zero) != shownHero)
        {
            shownHero = hero?.Pointer ?? IntPtr.Zero;
            ModEvents.HeroSelected.Raise(new HeroSelection { Hero = hero });
        }
        var alt = MapMarkers.Wanted(MarkersShown.Alt);
        if (alt != altInfo)
        {
            altInfo = alt;
            ModEvents.AltInfo.Raise(alt);
        }
    }
}
