using System.Linq;
using System.Reflection;
using Hex.Storage;
using UnityEngine;
using SpriteTable = Il2CppSystem.Collections.Generic.Dictionary<string, UnityEngine.Sprite>;

namespace Nobirds.OldenEra.Modding.Common;

// The game's sprites by name (#56). The comments name each member as it is on 0.82.08.
public static partial class Game
{
    // The sprite registry (coi): about 40 tables name -> sprite, each filled from one Resources folder of the game
    // (icons/units/hex_portraits, icons/heroes, ...: Resources.LoadAll, keyed by the sprite's name). The places that
    // show a unit's portrait, a building's icon and the like look a name up in the table of their folder. Found as the
    // class with more than 20 such tables; its static instance (coi.cdwo) is pinned (two static properties of its type).
    static readonly Lookup<System.Type> spriteRegistry = Lookup<System.Type>.Among("sprite registry",
        () => Types.Where(t => Properties(t).Count(p => p.DeclaringType == t && p.PropertyType == typeof(SpriteTable)) > 20), t => t.Name, onlyOne: true)
        .Generate("GameSpriteRegistry");

    static readonly Lookup<PropertyInfo> spriteRegistryInstance = Lookup<PropertyInfo>.Among("sprite registry instance",
        () => spriteRegistry.Value is { } type
            ? type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(p => !p.Name.StartsWith("_") && p.PropertyType == type)
            : null, Name).Generate("SpriteRegistry");

    // Its tables (cdwp ... cdyd).
    static readonly Lookup<PropertyInfo> spriteTables = Lookup<PropertyInfo>.Among("sprite tables",
        () => spriteRegistry.Value is { } type
            ? Properties(type).Where(p => p.DeclaringType == type && !p.Name.StartsWith("_") && p.PropertyType == typeof(SpriteTable))
            : null, Name).GenerateAll("Tables");

    // A sprite made from the bytes of a PNG (IconData.jvk): how the game makes the pictures of its campaigns' archives.
    static readonly Lookup<MethodInfo> pngToSprite = Lookup<MethodInfo>.Among("PNG to sprite",
        () => typeof(IconData).Methods().Instance().Returns(typeof(Sprite)).Takes(), Name, onlyOne: true).Generate("ToSprite");
}
