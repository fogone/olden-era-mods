using System;
using System.Linq;
using System.Reflection;

namespace Nobirds.OldenEra.Modding.Common;

// The game's resource manager (Hex.ResManager, an assembly of its own; #62). The game takes its prefabs, sounds and
// the like by path through it: Get<T>(path) looks the path up in its table of the assets of the AssetBundles it loaded
// (ResManager.Load(name): StreamingAssets/bundles/<name>, every asset of it, keyed "assets/resources/<path>" when the
// bundle was built from Assets/Resources), then in Resources. The game ships no bundles of its own: the table is
// empty until a mod's bundle is loaded. The comments name each member as it is on 0.82.08.
public static partial class Game
{
    // Its instance (ResManager.bsbr(): the one the static Get and Load use, made when there is none).
    static readonly Lookup<MethodInfo> resourceManager = Lookup<MethodInfo>.Among("resource manager instance",
        () => typeof(Hex.ResManager).Methods().Static().Returns(typeof(Hex.ResManager)).Takes(), Name, onlyOne: true)
        .Generate("ResourceManager");

    // Its table of the bundles' assets (ResManager.ctyt): key (gpt: path, bundle, type) -> entry (gpu: key, asset).
    static readonly Lookup<PropertyInfo> resourceTable = Lookup<PropertyInfo>.Among("resource table",
        () => Properties(typeof(Hex.ResManager)).Where(p => p.DeclaringType == typeof(Hex.ResManager) && !p.Name.StartsWith("_")
            && p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Il2CppSystem.Collections.Generic.Dictionary<,>)),
        Name).Generate("Assets");

    static Type TableArgument(int index) =>
        resourceTable.Value?.PropertyType.GetGenericArguments() is { Length: 2 } a ? a[index] : null;

    static readonly Lookup<Type> resourceKey = Lookup<Type>.Among("resource key",
        () => TableArgument(0) is { } t ? new[] { t } : null, t => t.Name).Generate("GameResourceKey");

    static readonly Lookup<Type> resourceEntry = Lookup<Type>.Among("resource entry",
        () => TableArgument(1) is { } t ? new[] { t } : null, t => t.Name).Generate("GameResourceEntry");

    // A key's strings by their place in it: the asset's path, then its bundle's name (gpt.ctyd, gpt.ctye; the order
    // ResManager's bundle loading fills them in).
    static PropertyInfo KeyString(int index) =>
        resourceKey.Value is { } key
            ? GameCode.Fields(key).Where(f => !f.isStatic && f.type == "System.String").OrderBy(f => f.offset)
                .Skip(index).Select(f => GameCode.PropertyOf(key, f.name)).FirstOrDefault()
            : null;

    static readonly Lookup<PropertyInfo> resourcePath = Lookup<PropertyInfo>.Among("resource key: path",
        () => KeyString(0) is { } p ? new[] { p } : null, Name).Generate("Path", writable: true);

    static readonly Lookup<PropertyInfo> resourceBundle = Lookup<PropertyInfo>.Among("resource key: bundle",
        () => KeyString(1) is { } p ? new[] { p } : null, Name).Generate("Bundle", writable: true);

    // gpt.ctyf
    static readonly Lookup<PropertyInfo> resourceType = Lookup<PropertyInfo>.Among("resource key: type",
        () => resourceKey.Value is { } key ? Properties(key).Where(p => p.DeclaringType == key && p.PropertyType == typeof(Il2CppSystem.Type)) : null,
        Name).Generate("AssetType", writable: true);

    // gpu.ctyg, gpu.ctyh
    static readonly Lookup<PropertyInfo> entryKey = Lookup<PropertyInfo>.Among("resource entry: key",
        () => resourceEntry.Value is { } e && resourceKey.Value is { } key ? Properties(e).Where(p => p.DeclaringType == e && p.PropertyType == key) : null,
        Name).Generate("Key", writable: true);

    static readonly Lookup<PropertyInfo> entryAsset = Lookup<PropertyInfo>.Among("resource entry: asset",
        () => resourceEntry.Value is { } e ? Properties(e).Where(p => p.DeclaringType == e && p.PropertyType == typeof(UnityEngine.Object)) : null,
        Name).Generate("Asset", writable: true);
}
