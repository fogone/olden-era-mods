using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Hex.Session.Data;
using Hex.UI;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace OldenEra.Modding.Common;
// The game's code as the plugins use it: readable names over the generated ones (GameNames.g.cs), typed, with the
// game's own types (GameHero, GameSquad, GameMapObject, ...). The searches behind the generated names are in
// search/Game.cs; this side is compiled by the plugins only (the generator, tools/game-names, makes the names it uses).
//
//   var hero = Game.SelectedHero;              // the hero selected in the HUD (GameHero)
//   var side = hero?.HeroSide;                 // its player's Hex.Session.Data.Side
internal static partial class Game
{
    extension(GameMapObject mapObject)
    {
        // The map object's scene GameObject: the one of its model holder's GameObjects that is set.
        public GameObject ObjectModel => mapObject.Model?.SceneObjects.FirstOrDefault(go => go != null);
    }

    extension(Il2CppObjectBase value)
    {
        // An object of the game as the map's map object (a building's wrapper as fql), its hero (fgd), its squad (frc),
        // the squad entity under the cursor (fgy); null when it is none.
        public GameMapObject AsMapObject() => value?.TryCast<GameMapObject>();
        public GameHero AsHero() => value?.TryCast<GameHero>();
        public GameSquad AsSquad() => value?.TryCast<GameSquad>();
        public GameSquadEntity AsSquadEntity() => value?.TryCast<GameSquadEntity>();
    }

    // Whether the game played now is a single player one (fogone 07.11: the mods' mode "only in a single player game"):
    // its session played here (EPlay.Local, not Net nor Replay) and, on a map, at most one side played by a person at this
    // computer (the map data's hotseat sides). No session (the main menu): single player. Read once per session and
    // map data (the core caches it by their objects).
    public static bool SinglePlayer(GameSession session)
    {
        if (session == null)
            return true;
        if (session.Play != Hex.EPlay.Local)
            return false;
        var data = session.TryCast<GameMapGameSession>()?.MapData;
        return (data?.sides?.hotseatSides?.Length ?? 0) <= 1;
    }

    // Every map object (null entries possible); null when there is no map session now.
    public static IEnumerable<GameMapObject> MapObjects() => GameNames.MapSession?.MapObjects?.ById is { } byId ? ValuesOf(byId) : null;

    // Every squad (null entries possible); null when there is no map session now.
    public static IEnumerable<GameSquad> Squads() => GameNames.MapSession?.Squads?.ById is { } byId ? ValuesOf(byId) : null;

    // The values of a store's dictionary (null entries possible).
    static IEnumerable<V> ValuesOf<V>(Il2CppSystem.Collections.Generic.Dictionary<int, V> byId)
    {
        foreach (var value in byId.Values)
            yield return value;
    }

    public static GameHero SelectedHero => GameNames.HudPanel is { } panel && panel != null ? panel.Hero : null;


    public static Hex.Settings.Data.SettingsData Settings => GameNames.SettingsService?.Settings;

    // The game's setting "hide the battle danger": the skulls widget shows its "danger hidden" row instead of the skulls,
    // and nothing about the fight's outcome is to be told.
    public static bool DangerHidden => Settings?.hideDangerIndication == true;

    // The game's text of its key in the game's language, as its UI gets it (the localization service's cav.qmk on
    // 0.82.7, what iv.ihi calls); "LOC:<key>" for a key it does not know, null before the service is there.
    public static string Translate(string key) => GameNames.Localization?.Translate(key);

    extension(GameHero hero)
    {
        // The hero's player: Hex.Session.Data.Side (index, fogData, ...).
        public Side HeroSide => hero.SideOwner?.Side;

        // The hero's army (DataParty).
        public DataParty HeroArmy => hero.Data?.Army;


        // Would the squad join the hero, and the game's explanation ("hero points: N squad points: M"): the game's
        // explained Diplomacy check (ffd.blop, the one its encounter's check calls).
        public (bool joins, string reason) WouldJoin(GameSquad squad) =>
            (GameNames.JoinsByDiplomacyExplained(hero, squad, out var reason), reason);

        // The share of the squad's creatures that would join (ffd.blor, what the encounter's joining counts with).
        public float JoinShare(GameSquad squad) => GameNames.JoinShare(squad, hero);
    }

    extension(GameSquad squad)
    {

        // The squad's scene GameObject.
        public GameObject SquadModel => squad.View?.Model;
    }

    // A hidden copy of the game's skulls widget named `name`, to fill for a danger level without repainting an open
    // tooltip; null until the game has made one to copy.
    static readonly Dictionary<string, BhSkulls> hiddenSkulls = new();
    static float nextSkullsSearch;

    public static BhSkulls HiddenSkulls(string name)
    {
        if (hiddenSkulls.TryGetValue(name, out var widget) && widget != null)
            return widget;
        // The search goes through every loaded object: while there is no widget to copy (the main menu), every 2 s.
        if (Time.realtimeSinceStartup < nextSkullsSearch)
            return null;
        nextSkullsSearch = Time.realtimeSinceStartup + 2f;
        if (Resources.FindObjectsOfTypeAll<BhSkulls>().FirstOrDefault(s => s.skullActive != null && !s.name.StartsWith(name)) is not { } template)
            return null;
        widget = UnityEngine.Object.Instantiate(template.gameObject).GetComponent<BhSkulls>();
        widget.gameObject.name = name;
        widget.gameObject.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(widget.gameObject);
        return hiddenSkulls[name] = widget;
    }

    // ---------- sprites ----------

    // The unit portrait the game shows in its windows (efr.bdbb on 0.82.7, pinned: the sprite loaders all take a string),
    // null for an unknown unit.
    public static Sprite UnitPortrait(string sid)
    {
        try { return GameNames.UnitPortrait(sid); }
        catch (Exception) { return null; }
    }

    // Every sprite loaded now (UI icons are, once the UI is up). Slow: looks through every loaded object.
    public static IEnumerable<Sprite> LoadedSprites()
    {
        foreach (var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
            if (obj.TryCast<Sprite>() is { } sprite)
                yield return sprite;
    }

    // The game's loaded sprites by name, gathered in one pass over the loaded objects (slow) for every name asked; gathered
    // again, at most every ten seconds, while something asked for is not there yet (UI icons load with their UI).
    static Dictionary<string, Sprite> sprites;
    static float spritesAt = float.MinValue;

    static Dictionary<string, Sprite> Sprites(bool missed)
    {
        if (sprites == null || missed && Time.realtimeSinceStartup >= spritesAt + 10f)
        {
            spritesAt = Time.realtimeSinceStartup;
            sprites = new Dictionary<string, Sprite>();
            foreach (var sprite in LoadedSprites())
                sprites.TryAdd(sprite.name, sprite);
        }
        return sprites;
    }

    // A sprite of the game's by name; null if not loaded yet.
    public static Sprite SpriteNamed(string name) =>
        Sprites(false).TryGetValue(name, out var sprite) && sprite != null ? sprite
        : Sprites(true).TryGetValue(name, out sprite) && sprite != null ? sprite : null;

    // The loaded sprite whose name matches, the shortest such name (a base icon before its variants: skill_diplomacy
    // before skill_diplomacy_2); null if none yet.
    public static Sprite SpriteMatching(Func<string, bool> match)
    {
        Sprite Best(Dictionary<string, Sprite> all) =>
            all.Where(p => p.Value != null && match(p.Key)).OrderBy(p => p.Key.Length).Select(p => p.Value).FirstOrDefault();
        return Best(Sprites(false)) ?? Best(Sprites(true));
    }

    // Debug: the names of the loaded sprites that match, to find one of the game's icons by its name.
    public static List<string> SpriteNames(Func<string, bool> match) =>
        Sprites(true).Keys.Where(match).OrderBy(n => n).ToList();

    // The game's Diplomacy skill icon (skill_diplomacy), by its sprite's name: the game loads it with the hero's skills
    // window; null till then.
    static Sprite diplomacyIcon;

    public static Sprite DiplomacyIcon => diplomacyIcon != null ? diplomacyIcon
        : diplomacyIcon = SpriteMatching(name => name.IndexOf("diplomacy", StringComparison.OrdinalIgnoreCase) >= 0);

    // The game's escape icon, the one its "weak squad" window shows.
    public static Sprite EscapeIcon => SpriteNamed("Icon_Escape");
}
