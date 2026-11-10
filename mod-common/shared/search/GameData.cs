using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Nobirds.OldenEra.Modding.Common;

// The game's data roots (#56). The comments name each member as it is on 0.82.08.
public static partial class Game
{
    // The roots (bkp.byxl): the game's own list of mod folders, static. Each is {enabled, name, folder} (bko); the game
    // mounts each folder's *.zip and loads its DB after the roots before it (an entry of the same id replaces the
    // earlier one). The game builds it with one root, "core" = StreamingAssets. Found as the one static array of a
    // class with exactly that shape: one bool and two strings.
    static readonly Lookup<PropertyInfo> dataRoots = Lookup<PropertyInfo>.Among("data roots",
        () => Types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(p => !p.Name.StartsWith("_") && p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Il2CppReferenceArray<>) &&
                        IsDataRoot(p.PropertyType.GetGenericArguments()[0])),
        Name, onlyOne: true).Generate("DataRoots", writable: true);

    static bool IsDataRoot(Type t)
    {
        var own = Properties(t).Where(p => p.DeclaringType == t).Select(p => p.PropertyType).ToList();
        return own.Count == 3 && own.Count(p => p == typeof(bool)) == 1 && own.Count(p => p == typeof(string)) == 2;
    }

    // A root's class (bko).
    static readonly Lookup<Type> dataRoot = Lookup<Type>.Among("data root",
        () => dataRoots.Value is { } roots ? new[] { roots.PropertyType.GetGenericArguments()[0] } : null, t => t.Name).Generate("GameDataRoot");

    // Its fields in their order: enabled (byxi), name (byxj), folder (byxk).
    static readonly Lookup<PropertyInfo> dataRootEnabled = NthField("data root enabled", () => dataRoot.Value, typeof(bool), 0, 1).Generate("Enabled", writable: true);
    static readonly Lookup<PropertyInfo> dataRootName = NthField("data root name", () => dataRoot.Value, typeof(string), 0, 2).Generate("Name", writable: true);
    static readonly Lookup<PropertyInfo> dataRootFolder = NthField("data root folder", () => dataRoot.Value, typeof(string), 1, 2).Generate("Folder", writable: true);

    // The builders of the list (bkp.nbv and its clone htm): the static void M() of the list's class. The core root is
    // put there; ours are added after each of them.
    static readonly Lookup<List<MethodInfo>> dataRootsBuilders = new Lookup<List<MethodInfo>>("data roots builders",
        () => dataRoots.Value is { } roots
            ? roots.DeclaringType.Methods().Static().Returns(typeof(void)).Takes().ToList() is { Count: > 0 } found ? found : null
            : null,
        Members.Names).GenerateTarget("DataRootsBuilders");
}
