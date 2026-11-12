using System;
using System.Linq;
using System.Reflection;

namespace Nobirds.OldenEra.Modding.Common;

// The game's components on its prefabs whose class names the obfuscator changes with every patch (#62). A mod's
// bundle refers to them by readable names (BundleNames), and the library puts this version's names in when it takes
// the bundle. Found by what a prefab of the game has filled in on them: their serialized fields keep their names. The
// comments name each class as it is on 0.82.08.
public static partial class Game
{
    static bool IsComponent(Type t) => typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract;

    static bool HasField(Type t, string name, Type type) =>
        t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is { } p && p.PropertyType == type;

    // A battle unit's view helper (emp): the points its projectiles start from and land at, its shield's point, its
    // skinned mesh, its idle effects. The battle drawer takes it from the prefab's root.
    static readonly Lookup<Type> unitViewHelper = Lookup<Type>.Among("unit view helper",
        () => Types.Where(t => IsComponent(t) && HasField(t, "projectileSpawnPoint", typeof(UnityEngine.Transform))
            && HasField(t, "projectileEndPoint", typeof(UnityEngine.Transform)) && HasField(t, "shieldPoint", typeof(UnityEngine.Transform))),
        t => t.Name, onlyOne: true).Generate("GameUnitViewHelper");

    // The receiver of a unit model's animation events (cf): the sounds its clips call by name (PlaySound,
    // PlayStepSound, PlaySpatialSound), on the object with the Animator.
    static readonly Lookup<Type> unitAnimationEvents = Lookup<Type>.Among("unit animation events",
        () => Types.Where(t => IsComponent(t) && new[] { "PlaySound", "PlayStepSound", "PlaySpatialSound" }
            .All(n => t.GetMethod(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(string) }, null) != null)),
        t => t.Name, onlyOne: true).Generate("GameUnitAnimationEvents");

    // A slot of a town's buildings (eiq): its buildings by level (buildingsInSlot) and its name, the one the city's data
    // calls its "sceneSlot" (an auto-property's backing field, <clsg>k__BackingField: obfuscated, serialized). The
    // buildings' manager finds its slot by that name (BhBuildingsManager.bemb(String)).
    static readonly Lookup<Type> buildingSlot = Lookup<Type>.Among("town building slot",
        () => Types.Where(t => IsComponent(t) && HasField(t, "buildingsInSlot", typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Hex.Session.City.BhBuilding>))),
        t => t.Name, onlyOne: true).Generate("GameBuildingSlot");

    // eiq's slot name: its one string field, the backing field of an auto-property (the serialized name of the field
    // a bundle sets).
    static readonly Lookup<object> buildingSlotName = new Lookup<object>("town building slot: name field",
        () => buildingSlot.Value is { } t
            ? GameCode.Fields(t).Where(f => !f.isStatic && f.type == "System.String" && f.name.EndsWith(">k__BackingField"))
                .Select(f => f.name).ToList() is { Count: 1 } names ? names[0] : null
            : null, v => $"{v}").GenerateConstant("BuildingSlotNameField");

    // A town camera's point (eis, on the scene's CameraPoint): its speed, angles and the point the camera goes to.
    static readonly Lookup<Type> cityCameraPoint = Lookup<Type>.Among("town camera point",
        () => Types.Where(t => IsComponent(t) && HasField(t, "camPoint", typeof(UnityEngine.Transform)) && HasField(t, "speed", typeof(float))),
        t => t.Name, onlyOne: true).Generate("GameCityCameraPoint");

    public static Type UnitViewHelperType => unitViewHelper.Value;
    public static Type UnitAnimationEventsType => unitAnimationEvents.Value;
}
