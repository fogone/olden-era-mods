using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Hex.Session.Data;
using Hex.Session.Hud;
using Hex.UI;
using Hex.UI.Tooltips;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace OldenEra.Modding.Common;

// The searches for the game's members (the build-time side, docs/game-names.md): run by the generator on the game's
// files (tools/game-names) and, where a plugin needs a member itself (a patch), on first use in the game. Each finds by
// signature and readable types; what it found goes to the log (Game.Log). Nothing here uses the generated names: the
// generator compiles this side to make them. The comments name each member as it is on 0.82.7.
internal static partial class Game
{
    // Where the lookups say what they found; set by the plugin.
    public static Action<string> Log { get; set; }

    // The lookups are Lookup<T> (Members.cs): on first use, once, said in the log.

    static string Name(PropertyInfo p) => $"{p.DeclaringType?.Name}.{p.Name}";
    static string Name(MethodInfo m) => $"{m.DeclaringType?.Name}.{m.Name}";

    // ---------- reflection helpers ----------

    // The game's code (Hex.dll).
    static Type[] types;
    public static Type[] Types => types ??= SafeTypes(typeof(BhTooltipSquad).Assembly);

    public static Type[] SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
    }

    // Public instance properties without the compiler's backing-field duplicates ("_..." names).
    public static IEnumerable<PropertyInfo> Properties(Type t) => Usable(t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

    // The properties, each of the game's once: an auto-property's backing field (_x_k__BackingField) only when its
    // property is not there. With BepInEx's signature names the interop can give two properties of a class the same
    // getter (0.82.7: fgd's Logic.Hero cqei and Data.Hero cwtl both call cwtl's): the one whose getter returns its type
    // stays (CodeSource.GetterFits), the other goes, and the property it stood for is read through its backing field (by
    // the field's name, which does not clash).
    public static IEnumerable<PropertyInfo> Usable(IEnumerable<PropertyInfo> properties)
    {
        var list = properties.ToList();
        var real = list.Where(p => !p.Name.StartsWith("_") && CodeSource.Current.GetterFits(p))
            .GroupBy(p => (p.DeclaringType, p.NameInGame())).Select(g => g.OrderBy(p => p.Name, StringComparer.Ordinal).First()).ToList();
        var names = real.Select(p => (p.DeclaringType, p.NameInGame())).ToHashSet();
        foreach (var p in list.Where(p => p.Name.StartsWith("_") && p.Name.EndsWith("_k__BackingField")))
            if (!names.Contains((p.DeclaringType, p.Name[1..^"_k__BackingField".Length])))
                real.Add(p);
        return real;
    }

    public static IEnumerable<PropertyInfo> PropertiesOfType(Type owner, Func<Type, bool> match) =>
        Properties(owner).Where(p => match(p.PropertyType));

    public static bool HasProperty(Type t, Type propertyType) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => p.PropertyType == propertyType);

    public static Type[] ParamTypes(MethodBase m) => m.GetParameters().Select(p => p.ParameterType).ToArray();

    // The IL2CPP object as a wrapper of `type` (TryCast), null when it is not one.
    public static object CastTo(this object value, Type type)
    {
        if (value == null || type == null)
            return null;
        if (type.IsInstanceOfType(value))
            return value;
        if (value is not Il2CppObjectBase il2cpp)
            return null;
        var tryCast = typeof(Il2CppObjectBase).GetMethod(nameof(Il2CppObjectBase.TryCast)).MakeGenericMethod(type);
        return tryCast.Invoke(il2cpp, null);
    }

    // Start of a method's native code. IL2CPP folds identical methods into one address: detour each address once.
    // In the game or in its files (CodeSource).
    public static IntPtr NativeAddress(MethodInfo method) => new(CodeSource.Current.Address(method));

    // The IL2CPP class name of an object.
    public static string ClassName(IntPtr obj) =>
        Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(IL2CPP.il2cpp_object_get_class(obj)));

    // ---------- the map session's stores ----------

    // The map session (feh on 0.82.7): a MonoBehaviour with a static instance of itself (me); it holds stores, each a
    // type with a Dictionary<int, wrapper> of one kind of things on the map by id (fqm.crlf: map objects, fre.crox: squads).
    // Used through the generated names: GameNames.MapSession?.MapObjects?.ById.
    static bool IsById(Type t, Type valueType) =>
        valueType != null && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Il2CppSystem.Collections.Generic.Dictionary<,>) &&
        t.GetGenericArguments()[0] == typeof(int) && t.GetGenericArguments()[1] == valueType;

    static IEnumerable<PropertyInfo> DeclaredProperties(Type t) =>
        Usable(t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

    static bool IsMapSession(Type t) =>
        typeof(MonoBehaviour).IsAssignableFrom(t) && t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Any(p => p.PropertyType == t);

    // A store's dictionary of the things by id, and the map session's property holding the store.
    static Lookup<PropertyInfo> StoreById(string what, Func<Type> valueType) => Lookup<PropertyInfo>.Among(what,
        () => valueType() is { } v ? Types.SelectMany(DeclaredProperties).Where(p => IsById(p.PropertyType, v)) : null, Name);

    static Lookup<PropertyInfo> StoreHolder(string what, Lookup<PropertyInfo> byId) => Lookup<PropertyInfo>.Among(what,
        () => byId.Value is { } b ? Types.Where(IsMapSession).SelectMany(DeclaredProperties).Where(p => p.PropertyType == b.DeclaringType) : null, Name);

    static readonly Lookup<PropertyInfo> mapObjectsById = StoreById("map objects by id", () => MapObjectType).Generate("ById");
    static readonly Lookup<PropertyInfo> mapObjectStore = StoreHolder("map session -> map objects", mapObjectsById).Generate("MapObjects", "GameMapObjectStore");
    static readonly Lookup<PropertyInfo> squadsById = StoreById("squads by id", () => SquadType).Generate("ById");
    static readonly Lookup<PropertyInfo> squadStore = StoreHolder("map session -> squads", squadsById).Generate("Squads", "GameSquadStore");

    // The map session's static instance of itself (feh.me).
    static readonly Lookup<PropertyInfo> mapSession = Lookup<PropertyInfo>.Among("map session",
        () => mapObjectStore.Value is { } holder
            ? holder.DeclaringType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(p => p.PropertyType == holder.DeclaringType)
            : null, Name).Generate("MapSession", "GameMapSession");

    // ---------- the session: single player or not (fogone 07.11: the mods only in a single player game) ----------

    // The game's session (dkd on 0.82.7; the map's session dcz and the custom battle's derive from it): the MonoBehaviour
    // with a property of how it is played (Hex.EPlay: Local, Net, Replay; dkd.cwbi) and a static instance of itself (dkd.Me).
    static readonly Lookup<PropertyInfo> sessionPlay = Lookup<PropertyInfo>.Among("session -> how it is played (EPlay)",
        () => Types.Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType == typeof(Hex.EPlay) && !p.Name.StartsWith("_")), Name).Generate("Play");

    static readonly Lookup<PropertyInfo> session = Lookup<PropertyInfo>.Among("session",
        () => sessionPlay.Value is { } play
            ? play.DeclaringType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(p => p.PropertyType == play.DeclaringType)
            : null, Name).Generate("Session", "GameSession");

    // A session's map data (dcz.chmi, Hex.Session.Data.Data: its start info, its sides with the hotseat ones): the
    // property of that type on a session type.
    static readonly Lookup<PropertyInfo> sessionData = Lookup<PropertyInfo>.Among("session -> map data",
        () => session.Value is { } s
            ? Types.Where(t => s.DeclaringType.IsAssignableFrom(t)).SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(p => p.PropertyType == typeof(Hex.Session.Data.Data) && !p.Name.StartsWith("_"))
            : null, Name).Generate("MapData");

    // The session of a map game (dcz), the one with the map data.
    static readonly Lookup<Type> mapGameSession = Lookup<Type>.Among("map game session",
        () => sessionData.Value is { } p ? new[] { p.DeclaringType } : null, t => t.Name).Generate("GameMapGameSession");

    // ---------- map objects (buildings, mines, dwellings, pickups) ----------

    // The map object wrapper (fql on 0.82.7), base of every building's wrapper: the base class of the event bank's
    // wrapper (fpk), the type with an ObjEventBank property.
    static readonly Lookup<Type> mapObjectType = Lookup<Type>.Among("map object",
        () => Types.Where(t => Properties(t).Any(p => p.PropertyType == typeof(ObjEventBank))).Select(t => t.BaseType).Distinct(), t => t.Name).Generate("GameMapObject");

    public static Type MapObjectType => mapObjectType.Value;

    // Map object -> its data, DataObject (fql.crkp).
    static readonly Lookup<PropertyInfo> objectData = Lookup<PropertyInfo>.Among("map object -> DataObject",
        () => MapObjectType is { } t ? Properties(t).Where(p => p.PropertyType == typeof(DataObject)) : null, Name).Generate("ObjectData");

    // Map object -> its model holder (fql.crko, a bye), the type that has the scene GameObject and Transform.
    static readonly Lookup<PropertyInfo> objectModel = Lookup<PropertyInfo>.Among("map object -> model",
        () => MapObjectType is { } t
            ? Properties(t).Where(p => Properties(p.PropertyType).Any(q => q.PropertyType == typeof(GameObject)) &&
                                                Properties(p.PropertyType).Any(q => q.PropertyType == typeof(Transform)))
            : null, Name).Generate("Model", "GameModelHolder");

    // The model holder's GameObjects (bye's, its backing fields too): the one that is set is the map object's scene
    // object (mapObject.Model.SceneObjects).
    static readonly Lookup<PropertyInfo> modelObjects = Lookup<PropertyInfo>.Among("map object model -> GameObjects",
        () => objectModel.Value is { } model
            ? model.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.PropertyType == typeof(GameObject))
            : null, Name).GenerateAll("SceneObjects");

    // The garrison row of a tooltip (BhTooltipGarnison) is filled by void M(party wrapper, bool) (unj(eco, bool) on
    // 0.82.7), the party wrapper being a map object's property type; the flag picks how squad sizes are shown.
    static readonly Lookup<MethodInfo> garrisonFill = Lookup<MethodInfo>.Among("BhTooltipGarnison fill (party, bool)",
        () => MapObjectType is { } o
            ? typeof(BhTooltipGarnison).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(bool) &&
                                     m.GetParameters()[0].ParameterType.IsClass && Properties(o).Any(p => p.PropertyType == m.GetParameters()[0].ParameterType))
            : null,
        m => $"{m.Name}({m.GetParameters()[0].ParameterType.Name}, bool)").Generate("FillWith");

    // Map object -> its party wrapper, the one the garrison row takes (fql.crkq).
    static readonly Lookup<PropertyInfo> objectParty = Lookup<PropertyInfo>.Among("map object -> party",
        () => garrisonFill.Value is { } fill ? Properties(MapObjectType).Where(p => p.PropertyType == fill.GetParameters()[0].ParameterType) : null, Name, onlyOne: true).Generate("Party", "GameParty");

    // The unit dwelling's tooltip (BhTooltipHire) is filled by void M(map object) (wae(fpq) on 0.82.7).
    static readonly Lookup<MethodInfo> hireTooltipFill = Lookup<MethodInfo>.Among("BhTooltipHire fill (map object)",
        () => MapObjectType is { } o
            ? typeof(BhTooltipHire).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 1 && o.IsAssignableFrom(m.GetParameters()[0].ParameterType))
            : null, Name).Generate("FillFor").GenerateTarget("HireTooltipFill");


    // The dwelling the dwelling's tooltip is filled for (fpq on 0.82.7): the fill's parameter.
    static readonly Lookup<Type> dwellingType = Lookup<Type>.Among("dwelling",
        () => hireTooltipFill.Value is { } fill ? new[] { fill.GetParameters()[0].ParameterType } : null, t => t.Name).Generate("GameDwelling");

    // The squad tooltip (BhTooltipSquad, shown for squads and for buildings too) is filled by
    // M(ESquadTooltipType, Il2CppSystem.Object target) (weh on 0.82.7); not found when there is not exactly one.
    static readonly Lookup<MethodInfo> squadTooltipFill = Lookup<MethodInfo>.Among("BhTooltipSquad fill (ESquadTooltipType, object)",
        () => typeof(BhTooltipSquad).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => ParamTypes(m).SequenceEqual(new[] { typeof(ESquadTooltipType), typeof(Il2CppSystem.Object) })), Name, onlyOne: true).Generate("FillFor").GenerateTarget("SquadTooltipFill");


    // ---------- heroes ----------

    // The hero on the map (fgd on 0.82.7): the first parameter of the map object's (hero, ...) methods that wraps
    // Hex.Session.Logic.Hero.
    static readonly Lookup<Type> heroType = Lookup<Type>.Among("hero",
        () => MapObjectType?.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.GetParameters())
            .Where(ps => ps.Length >= 1)
            .Select(ps => ps[0].ParameterType)
            .Where(t => Properties(t).Any(p => p.PropertyType == typeof(Hex.Session.Logic.Hero))).Distinct(), t => t.Name).Generate("GameHero");

    public static Type HeroType => heroType.Value;

    // Hero -> Hex.Session.Logic.Hero (fgd.cqei).
    static readonly Lookup<PropertyInfo> heroLogic = Lookup<PropertyInfo>.Among("hero -> Logic.Hero",
        () => HeroType is { } h ? Properties(h).Where(p => p.PropertyType == typeof(Hex.Session.Logic.Hero)) : null, Name).Generate("Logic");

    // The hero selected in the HUD: the HUD hero panel's static instance (BhHeroHUDPanel.me) and its hero (ciku).
    static readonly Lookup<PropertyInfo> hudPanel = Lookup<PropertyInfo>.Among("HUD hero panel",
        () => typeof(BhHeroHUDPanel).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == typeof(BhHeroHUDPanel)), Name).Generate("HudPanel");

    static readonly Lookup<PropertyInfo> hudHero = Lookup<PropertyInfo>.Among("HUD hero panel -> hero",
        () => HeroType is { } h ? Properties(typeof(BhHeroHUDPanel)).Where(p => p.PropertyType == h) : null, Name).Generate("Hero");

    // ---------- the fog of war ----------

    // The fog's check of a cell: DataFogOfWar's only bool M(int) (bkin on 0.82.7): true while the cell is in the fog
    // (its visibility byte is 0). The game asks it with an object's cell (DataSquad.node).
    static readonly Lookup<MethodInfo> fogHides = Lookup<MethodInfo>.Among("fog: cell hidden",
        () => typeof(DataFogOfWar).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(bool) && ParamTypes(m).SequenceEqual(new[] { typeof(int) })),
        Name, onlyOne: true).Generate("InFog");

    // ---------- the game's settings ----------

    // The player's settings (Hex.Settings.Data.SettingsData), from the settings service: a type with a SettingsData
    // property and a static property of its own type (bak.bwsf -> bak.bwsg on 0.82.7; BhSkulls reads it the same way).
    static readonly Lookup<PropertyInfo> settingsData = Lookup<PropertyInfo>.Among("settings",
        () => Types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType == typeof(Hex.Settings.Data.SettingsData) && !p.Name.EndsWith("k__BackingField"))
            .Where(p => p.DeclaringType.GetProperties(BindingFlags.Public | BindingFlags.Static).Any(s => s.PropertyType == p.DeclaringType)),
        Name).Generate("Settings");

    // The settings service itself: its static property of its own type (bak.bwsf).
    static readonly Lookup<PropertyInfo> settingsService = Lookup<PropertyInfo>.Among("settings service",
        () => settingsData.Value is { } data ? data.DeclaringType.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(s => s.PropertyType == data.DeclaringType) : null,
        Name).Generate("SettingsService", "GameSettingsService");

    // Hero -> its player's side wrapper (fqu on 0.82.7): the hero's property whose type points back to the hero and
    // carries a Side.
    static readonly Lookup<PropertyInfo> heroSideOwner = Lookup<PropertyInfo>.Among("hero -> side wrapper",
        () => HeroType is { } h ? PropertiesOfType(h, t => t.IsClass && HasProperty(t, h) && HasProperty(t, typeof(Side))) : null,
        p => $"{Name(p)} ({p.PropertyType.Name})").Generate("SideOwner", "GameSide");

    public static Type SideOwnerType => heroSideOwner.Value?.PropertyType;

    // Side wrapper -> Hex.Session.Data.Side (fqu.cwxl).
    static readonly Lookup<PropertyInfo> sideData = Lookup<PropertyInfo>.Among("side wrapper -> Side",
        () => SideOwnerType is { } s ? Properties(s).Where(p => p.PropertyType == typeof(Side)) : null, Name).Generate("Side");

    // Hero -> party wrapper -> DataParty: the hero's army (a squad's too, through its hero-like owner).
    static readonly Lookup<PropertyInfo> heroParty = Lookup<PropertyInfo>.Among("hero -> its data (Hex.Session.Data.Hero, with the party)",
        () => HeroType is { } h ? PropertiesOfType(h, t => HasProperty(t, typeof(DataParty))) : null, Name).Generate("Data");

    static readonly Lookup<PropertyInfo> partyData = Lookup<PropertyInfo>.Among("party wrapper -> DataParty",
        () => heroParty.Value is { } p ? PropertiesOfType(p.PropertyType, t => t == typeof(DataParty)) : null, Name).Generate("Army");

    // The hero's stats (HeroStat: base, per level, bonus, totals; they carry enableDiplomacy): HeroStat properties of the
    // hero wrapper and one level below it, all of them (hero.Stats).
    static readonly Lookup<PropertyInfo[]> heroStatPaths = Lookup<PropertyInfo[]>.Among("hero -> HeroStat paths", () =>
        HeroType is { } h
            ? Properties(h).SelectMany(property =>
                property.PropertyType == typeof(HeroStat) ? new[] { new[] { property } }
                : property.PropertyType.IsClass && property.PropertyType != typeof(string)
                    ? PropertiesOfType(property.PropertyType, t => t == typeof(HeroStat)).Select(inner => new[] { property, inner })
                    : Enumerable.Empty<PropertyInfo[]>())
            : null,
        path => string.Join(".", path.Select(p => p.Name))).GenerateAll("HeroStats");

    // ---------- squads on the map ----------

    // The game's diplomacy check, static bool M(hero, squad, out string reason) (ffd.blop on 0.82.7): its hero parameter
    // wraps Logic.Hero, its squad parameter DataSquad. Another method has the same signature (ffd.bfj); the real check
    // explains itself with "... squad points: N" (WouldJoin picks it).
    internal sealed class DiplomacyCheck
    {
        public readonly MethodInfo Method;
        public readonly int HeroIndex, SquadIndex, ReasonIndex;

        public DiplomacyCheck(MethodInfo method, int heroIndex, int squadIndex, int reasonIndex)
        {
            Method = method;
            HeroIndex = heroIndex;
            SquadIndex = squadIndex;
            ReasonIndex = reasonIndex;
        }
    }

    static readonly Lookup<List<DiplomacyCheck>> diplomacyChecks = new Lookup<List<DiplomacyCheck>>("diplomacy check candidates", () =>
    {
        var checks = new List<DiplomacyCheck>();
        foreach (var type in Types)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var ps = method.GetParameters();
                if (method.ReturnType != typeof(bool) || ps.Length != 3)
                    continue;
                var reason = Array.FindIndex(ps, p => p.ParameterType == typeof(string).MakeByRefType());
                var hero = Array.FindIndex(ps, p => HasProperty(p.ParameterType, typeof(Hex.Session.Logic.Hero)));
                var squad = Array.FindIndex(ps, p => HasProperty(p.ParameterType, typeof(DataSquad)));
                if (reason >= 0 && hero >= 0 && squad >= 0)
                    checks.Add(new DiplomacyCheck(method, hero, squad, reason));
            }
        }
        return checks.Count > 0 ? checks : null;
    }, l => string.Join(", ", l.Select(c => Name(c.Method))));


    // The map squad (frc on 0.82.7): the squad parameter of the diplomacy checks (all of them take the same one).
    static readonly Lookup<Type> squadType = Lookup<Type>.Among("squad",
        () => diplomacyChecks.Value?.Select(c => c.Method.GetParameters()[c.SquadIndex].ParameterType).Distinct(), t => t.Name, onlyOne: true).Generate("GameSquad");

    public static Type SquadType => squadType.Value;

    // Squad -> DataSquad (frc's DataSquad property; its units list holds the creatures).
    static readonly Lookup<PropertyInfo> squadData = Lookup<PropertyInfo>.Among("squad -> DataSquad",
        () => SquadType is { } s ? PropertiesOfType(s, t => t == typeof(DataSquad)) : null, Name).Generate("SquadData");

    // Squad -> its hero-like owners (the wrappers of the hero type it has), which own the squad's army (squad.Owners).
    static readonly Lookup<PropertyInfo> squadOwners = Lookup<PropertyInfo>.Among("squad -> hero-like owners",
        () => SquadType is { } s && HeroType is { } h ? PropertiesOfType(s, t => t == h) : null, Name).GenerateAll("SquadOwners");

    // Squad -> its view holder (frc.croh), whose type has the scene GameObject (crcj) and points back to the squad.
    static readonly Lookup<PropertyInfo> squadView = Lookup<PropertyInfo>.Among("squad -> view",
        () => SquadType is { } s ? PropertiesOfType(s, t => HasProperty(t, typeof(GameObject)) && HasProperty(t, s)) : null, Name).Generate("View", "GameSquadView");

    static readonly Lookup<PropertyInfo> viewObject = Lookup<PropertyInfo>.Among("squad view -> GameObject",
        () => squadView.Value is { } v ? PropertiesOfType(v.PropertyType, t => t == typeof(GameObject)) : null, Name).Generate("Model");

    // The map squad entity (fgy on 0.82.7, the thing the cursor is over): has a squad property (cqhe) and the cursor
    // colour methods CursorColor M(hero, int), computed for the selected hero on every hover.
    static IEnumerable<MethodInfo> CursorColorMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType.Name == "CursorColor")
            .Where(m =>
            {
                var ps = m.GetParameters();
                return ps.Length == 2 && ps[0].ParameterType == HeroType && ps[1].ParameterType == typeof(int);
            });

    static readonly Lookup<Type> squadEntityType = Lookup<Type>.Among("squad entity",
        () => SquadType is { } s ? Types.Where(t => PropertiesOfType(t, p => p == s).Any() && CursorColorMethods(t).Any()) : null, t => t.Name).Generate("GameSquadEntity");

    static readonly Lookup<PropertyInfo> squadEntitySquad = Lookup<PropertyInfo>.Among("squad entity -> squad",
        () => squadEntityType.Value is { } e && SquadType is { } s ? PropertiesOfType(e, p => p == s) : null, Name).Generate("Squad");


    // The entity's cursor colour methods (hero, int); empty when the entity is not found.

    // ---------- the danger skulls widget (BhSkulls) ----------

    static IEnumerable<MethodInfo> SkullsMethods(Type result, params Type[] parameters) =>
        typeof(BhSkulls).Methods().Instance().Returns(result).Takes(parameters).DistinctCode();

    // int M(DataParty) (umo on 0.82.7): fills the widget for an enemy party against the selected hero's army, the way
    // the dwelling tooltip shows its guard, and returns the danger level.
    static readonly Lookup<MethodInfo> skullsForParty = Lookup<MethodInfo>.Among("BhSkulls fill for a party",
        () => SkullsMethods(typeof(int), typeof(DataParty)), m => m.Name).Generate("ShowDangerOfParty").GenerateTarget("SkullsForParty");

    // int M(squad) (umn): the same for a map squad, as the squad's tooltip shows it.
    static readonly Lookup<MethodInfo> skullsForSquad = Lookup<MethodInfo>.Among("BhSkulls fill for a squad",
        () => SquadType is { } s ? SkullsMethods(typeof(int), s) : null, m => m.Name).Generate("ShowDangerOfSquad").GenerateTarget("SkullsForSquad");

    // Color M(int level) (umq): the colour of a danger level.
    static readonly Lookup<MethodInfo> skullsColor = Lookup<MethodInfo>.Among("BhSkulls level colour",
        () => SkullsMethods(typeof(Color), typeof(int)), m => m.Name).Generate("DangerColor");

    // int M(int heroPower, int squadPower) (ump): the danger level of the battle; not found when there are several.
    static readonly Lookup<MethodInfo> skullsLevel = Lookup<MethodInfo>.Among("BhSkulls level (hero power, squad power)",
        () => SkullsMethods(typeof(int), typeof(int), typeof(int)), m => m.Name, onlyOne: true).Generate("DangerLevel").GenerateTarget("SkullsLevel");



    // ---------- sprites ----------

    // The game's static Sprite M(string key) loaders; for unknown keys they give a "?" placeholder or throw. Which one
    // gives the unit portraits (efr.bdbb on 0.82.7) is pinned: told once in the game by asking each for a unit's sid.
    static readonly Lookup<MethodInfo> unitPortraits = Lookup<MethodInfo>.Among("unit portraits",
        () => Types.Methods().Static().Returns(typeof(Sprite)).Takes(typeof(string)).DistinctCode(), Name).Generate("UnitPortrait");
}
