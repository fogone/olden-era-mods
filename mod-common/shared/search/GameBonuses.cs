using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hex.Configs;

namespace OldenEra.Modding.Common;

// Bonuses on the map (a dwelling's "+N weekly growth in the town" and the like) and who receives them: readable names
// for the game's members (the generator's side, docs/game-names.md). The comments name each member as it is on 0.82.7.
//
// How the game keeps them (found 06.11, issue #7): a bonus (dwa, subclasses per EBonusType, dys for
// CityUnitsIncrementFromBarrack) works once a receiver (dwc; the side's is dyj) holds it: the receiver's add (dwc.bavk(dwa,
// false)) files it in its list for the bonus's type, sets the bonus's receiver (dwa.cjes) and switches it on. A dwelling
// (fpq) keeps its bonuses (fpq.crih); when it changes hands (fpq.bnox(new side, old side)) the old side's receiver lets
// them go and the new side's (fqu.crly.cjyr) takes them. A loaded game makes the dwelling's bonuses anew (fpq.Init) and
// gives it its owner without that step: the bonuses of a dwelling captured before the save have no receiver.
internal static partial class Game
{
    static bool IsListOf(Type list, Type item) =>
        list.IsGenericType && list.GetGenericTypeDefinition() == typeof(Il2CppSystem.Collections.Generic.List<>) && list.GetGenericArguments()[0] == item;

    // The bonus (dwa): the class that declares both a BonusConfig and an EBonusType property.
    static readonly Lookup<Type> bonusType = Lookup<Type>.Among("bonus",
        () => Types.Where(t => t.IsClass && t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is var ps &&
                               ps.Any(p => p.PropertyType == typeof(BonusConfig)) && ps.Any(p => p.PropertyType == typeof(EBonusType))),
        t => t.Name).Generate("GameBonus");

    public static Type BonusType => bonusType.Value;

    // Bonus -> its receiver (dwa.cjes : dwc): the bonus's property of a class that keeps a dictionary by EBonusType.
    static readonly Lookup<PropertyInfo> bonusReceiver = Lookup<PropertyInfo>.Among("bonus -> receiver",
        () => BonusType is { } b
            ? PropertiesOfType(b, t => t.IsClass && t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericArguments().FirstOrDefault() == typeof(EBonusType)))
            : null,
        p => $"{Name(p)} ({p.PropertyType.Name})").Generate("Receiver", "GameBonusReceiver");

    public static Type BonusReceiverType => bonusReceiver.Value?.PropertyType;

    // The receiver's add: void M(bonus, bool) of the receiver (dwc.bavk; epe, ecq, obg the same code), the one a dwelling
    // changing hands calls with false.
    static readonly Lookup<MethodInfo> receiverAdd = Lookup<MethodInfo>.Among("bonus receiver add",
        () => BonusReceiverType is { } r && BonusType is { } b
            ? r.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.ReturnType == typeof(void) && ParamTypes(m).SequenceEqual(new[] { b, typeof(bool) })).DistinctCode()
            : null, Name).Generate("Add");

    // Dwelling -> its bonuses (fpq.crih : List<dwa>).
    static readonly Lookup<PropertyInfo> dwellingBonuses = Lookup<PropertyInfo>.Among("dwelling -> bonuses",
        () => dwellingType.Value is { } d && BonusType is { } b ? PropertiesOfType(d, t => IsListOf(t, b)) : null, Name).Generate("Bonuses");

    // Map object -> the side that owns it (fql.crkr : fqu).
    static readonly Lookup<PropertyInfo> objectOwner = Lookup<PropertyInfo>.Among("map object -> owner side",
        () => MapObjectType is { } o && SideOwnerType is { } s ? PropertiesOfType(o, t => t == s) : null, Name).Generate("Owner");

    // Side -> its logic (fqu.crly : edc): the side's property of a class that has a property of a receiver's subclass.
    static readonly Lookup<PropertyInfo> sideLogic = Lookup<PropertyInfo>.Among("side -> logic with the bonus receiver",
        () => SideOwnerType is { } s && BonusReceiverType is { } r
            ? PropertiesOfType(s, t => t.IsClass && HasProperty(t, typeof(Hex.Session.Data.Side)) && Properties(t).Any(p => p.PropertyType != r && r.IsAssignableFrom(p.PropertyType)))
            : null,
        p => $"{Name(p)} ({p.PropertyType.Name})").Generate("Logic", "GameSideLogic");

    // Side logic -> the side's bonus receiver (edc.cjyr : dyj).
    static readonly Lookup<PropertyInfo> sideReceiver = Lookup<PropertyInfo>.Among("side logic -> bonus receiver",
        () => sideLogic.Value?.PropertyType is { } l && BonusReceiverType is { } r
            ? PropertiesOfType(l, t => t != r && r.IsAssignableFrom(t))
            : null,
        p => $"{Name(p)} ({p.PropertyType.Name})").Generate("BonusReceiver", "GameSideBonusReceiver");
}
