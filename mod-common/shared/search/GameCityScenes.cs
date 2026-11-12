using System;
using System.Linq;
using System.Reflection;
using Hex.GameScenes;

namespace Nobirds.OldenEra.Modding.Common;

// The scenes of a town (#62). A session loads the six city scenes with it and keeps each in a registry by its EScene
// (a city's "sceneName" parsed); opening a town activates the registry's entry. A mod's town scene, loaded by its own
// name from its bundle, is given an EScene number of its own and put into that registry through the registry's own
// method, so the town whose "sceneName" is that number opens it. The comments name each member as it is on 0.82.08.
public static partial class Game
{
    static readonly BindingFlags Own = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    static Type SceneDictionaryValue(PropertyInfo p) =>
        p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Il2CppSystem.Collections.Generic.Dictionary<,>)
        && p.PropertyType.GetGenericArguments()[0] == typeof(EScene) ? p.PropertyType.GetGenericArguments()[1] : null;

    // A registry of loaded scenes (cht): one dictionary EScene -> entry, and a method adding entries (cht.rau(chs[])).
    static readonly Lookup<Type> sceneRegistry = Lookup<Type>.Among("scene registry",
        () => Types.Where(t => t.GetProperties(Own).Count(p => !p.Name.StartsWith("_") && SceneDictionaryValue(p) != null) == 1
            && t.GetProperties(Own).Where(p => SceneDictionaryValue(p) != null).Select(SceneDictionaryValue).Any(entry =>
                t.GetMethods(Own).Any(m => m.ReturnType == typeof(void) && m.GetParameters() is { Length: 1 } ps
                    && ps[0].ParameterType == typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<>).MakeGenericType(entry)))),
        t => t.Name, onlyOne: true).Generate("GameSceneRegistry");

    // cht.cclw
    static readonly Lookup<PropertyInfo> registryScenes = Lookup<PropertyInfo>.Among("scene registry: scenes",
        () => sceneRegistry.Value?.GetProperties(Own).Where(p => !p.Name.StartsWith("_") && SceneDictionaryValue(p) != null), Name).Generate("Scenes");

    // A registry's entry (chs): the scene's EScene, the Unity scene, its root objects.
    static readonly Lookup<Type> sceneEntry = Lookup<Type>.Among("scene registry: entry",
        () => registryScenes.Value is { } p ? new[] { SceneDictionaryValue(p) } : null, t => t.Name).Generate("GameSceneEntry");

    // cht.rau(chs[]): entries added by their EScene.
    static readonly Lookup<MethodInfo> registryAdd = Lookup<MethodInfo>.Among("scene registry: add",
        () => sceneRegistry.Value is { } r && sceneEntry.Value is { } e
            ? r.GetMethods(Own).Where(m => m.ReturnType == typeof(void) && m.GetParameters() is { Length: 1 } ps
                && ps[0].ParameterType == typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<>).MakeGenericType(e))
            : null, Name).Generate("AddScenes");

    // chs.rat(): the entry takes its scene's root objects (Scene.GetRootGameObjects) and turns them off, as the game does
    // once its scene is loaded.
    // Of the methods turning the scene's roots off (clones on 0.82.09), the one that also keeps them in the entry's
    // list: its one call more (List.Add, shared generic code).
    static readonly Lookup<MethodInfo> entryTakeRoots = Lookup<MethodInfo>.Among("scene entry: take the roots",
        () => sceneEntry.Value?.GetMethods(Own).Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0
                && GameCode.Calls(m).Any(c => GameCode.At(c.target).Any(t => t.Name == "GetRootGameObjects")))
            .GroupBy(m => GameCode.Calls(m).Count()).OrderByDescending(g => g.Key).FirstOrDefault()?.DistinctCode(),
        Name).Generate("TakeRoots");

    static Lookup<PropertyInfo> EntryProperty(string what, Type type, string name) => Lookup<PropertyInfo>.Among(what,
        () => sceneEntry.Value?.GetProperties(Own).Where(p => !p.Name.StartsWith("_") && p.PropertyType == type), Name).Generate(name, writable: true);

    // chs.ccls, chs.cclt, chs.cclv
    static readonly Lookup<PropertyInfo> entryScene = EntryProperty("scene entry: EScene", typeof(EScene), "SceneId");
    static readonly Lookup<PropertyInfo> entryUnityScene = EntryProperty("scene entry: scene", typeof(UnityEngine.SceneManagement.Scene), "UnityScene");
    static readonly Lookup<PropertyInfo> entryRoots = EntryProperty("scene entry: roots",
        typeof(Il2CppSystem.Collections.Generic.List<UnityEngine.GameObject>), "Roots");

    // A session's town scenes (eja): the static sets of scenes a session loads, the town's registry, and the method
    // opening a town by its scene's name (eja.bepd(String) on 0.82.09, with clones), the one the game's town opener calls
    // (eir.mmk(fpi): a method of a town, the map object with an ObjCityConfig). The battle's and the arenas' scene parts
    // (emv, elr) have registries, scene sets and methods of a string too.
    static readonly Lookup<Type> cityScenes = Lookup<Type>.Among("town scenes",
        () => sceneRegistry.Value is { } r
            ? TownOpenerCallees().Select(m => m.DeclaringType).Distinct().Where(t => t.GetProperties(Own).Any(p => p.PropertyType == r)
                && t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Any(p => p.PropertyType == typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<EScene>)))
            : null, t => t.Name, onlyOne: true).Generate("GameCityScenes");

    // What the methods of a town (a type with an ObjCityConfig) as their one parameter call that takes a string.
    static System.Collections.Generic.IEnumerable<MethodInfo> TownOpenerCallees()
    {
        var towns = Types.Where(t => t.GetProperties(Own).Any(p => p.PropertyType == typeof(Hex.Configs.ObjCityConfig))).ToHashSet();
        return Types.SelectMany(t => t.GetMethods(Own))
            .Where(m => m.ReturnType == typeof(void) && m.GetParameters() is { Length: 1 } ps && towns.Contains(ps[0].ParameterType))
            .SelectMany(m => GameCode.Calls(m).SelectMany(c => GameCode.At(c.target)))
            .OfType<MethodInfo>()
            .Where(m => m.GetParameters() is { Length: 1 } ps && ps[0].ParameterType == typeof(string));
    }

    // eja.cluu
    static readonly Lookup<PropertyInfo> cityRegistry = Lookup<PropertyInfo>.Among("town scenes: registry",
        () => cityScenes.Value is { } c && sceneRegistry.Value is { } r ? c.GetProperties(Own).Where(p => !p.Name.StartsWith("_") && p.PropertyType == r) : null,
        Name).Generate("Registry");

    // The map session's scenes (dcz.chmk: dkh) and their town part (dkh.ciik).
    static readonly Lookup<PropertyInfo> sessionScenes = Lookup<PropertyInfo>.Among("map session: its scenes",
        () => mapGameSession.Value is { } s && cityScenes.Value is { } c
            ? s.GetProperties(Own).Where(p => !p.Name.StartsWith("_") && p.PropertyType.GetProperties(Own).Any(q => q.PropertyType == c))
            : null, Name).Generate("Scenes", "GameSessionScenes");

    static readonly Lookup<PropertyInfo> sessionCity = Lookup<PropertyInfo>.Among("session scenes: town",
        () => sessionScenes.Value is { } p && cityScenes.Value is { } c
            ? p.PropertyType.GetProperties(Own).Where(q => !q.Name.StartsWith("_") && q.PropertyType == c)
            : null, Name).Generate("City");
}
