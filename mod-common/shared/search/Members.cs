using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Nobirds.OldenEra.Modding.Common;

// One lookup of a game member, done on first use, once; `describe` names what was found, for the log (Game.Log).
//   static readonly Lookup<MethodInfo> setTexts = new("tooltip set texts", () => ..., Members.Name);
//   setTexts.Value
internal sealed class Lookup<T> where T : class
{
    readonly string what;
    readonly Func<T> find;
    readonly Func<T, string> describe;
    bool done;
    T value;

    public Lookup(string what, Func<T> find, Func<T, string> describe)
    {
        this.what = what;
        this.find = find;
        this.describe = describe;
        GeneratedNames.Resets.Add(Reset);
    }

    // The generator's pin (GameNames.pins.txt) put in as this lookup's value, so the lookups built on it use the pinned
    // member too; and every lookup not pinned made again after that.
    bool pinned;

    internal void Pin(object member)
    {
        value = (T)member;
        done = pinned = true;
    }

    void Reset()
    {
        if (!pinned)
            (done, value) = (false, null);
    }

    // All that a search finds (Among), for the generator: it takes only exactly one, or the one pinned.
    Func<IEnumerable<T>> candidates;

    // A search that may find several: in the game the first one found (or, `onlyOne`, nothing while there are several);
    // the generator (Generate) stops on several unless a pin picks one (docs/game-names.md).
    public static Lookup<T> Among(string what, Func<IEnumerable<T>> candidates, Func<T, string> describe, bool onlyOne = false) =>
        new(what, () => candidates() is { } found ? (onlyOne ? found.OnlyOne() : found.FirstOrDefault()) : null, describe) { candidates = candidates };

    IReadOnlyList<object> Candidates()
    {
        if (candidates == null)
            return Value is { } value ? new object[] { value } : Array.Empty<object>();
        try { return candidates()?.Cast<object>().ToList() ?? new List<object>(); }
        catch (Exception e)
        {
            Game.Log?.Invoke($"Game: {what} candidates: {e.GetType().Name}: {e.Message}");
            return Array.Empty<object>();
        }
    }

    public T Value
    {
        get
        {
            if (done)
                return value;
            done = true;
            try { value = find(); }
            catch (Exception e) { Game.Log?.Invoke($"Game: {what}: {e.GetType().Name}: {e.Message}"); }
            Game.Log?.Invoke($"Game: {what} = {(value != null ? describe(value) : "not found")}");
            return value;
        }
    }

    public bool Known => Value != null;

    // Into the generated names (GameNames.g.cs, the research plugin's `aliases`, docs/game-names.md) as `name`: a type
    // as `global using name = <its class>;`; an instance property or method as an extension `name` of its owner; a
    // static one as GameNames.name. With `typeName`, a property's type gets that alias too (the side wrapper, known
    // only as the hero's property's type).
    public Lookup<T> Generate(string name, string typeName = null, bool writable = false)
    {
        GeneratedNames.All.Add(new GeneratedNames.Entry(name, typeName, what, Candidates, writable, Pin: Pin));
        return this;
    }

    // Into the generated names as a list `name` of its owner: every candidate, in the order found (a property, or a path
    // of properties as PropertyInfo[]), as IEnumerable<T> of the values that are there (GameNames.Each: a getter that
    // throws or gives null is left out). For what the game keeps in several places (the hero's stats).
    public Lookup<T> GenerateAll(string name)
    {
        GeneratedNames.All.Add(new GeneratedNames.Entry(name, null, what, Candidates, All: true, Pin: Pin));
        return this;
    }

    // Into the generated patch targets (GameTargets in GameNames.g.cs) as `name`: a method (or a list of them) the core
    // patches, written as the exact member of the game's class, so the plugins do not search for it while the game runs.
    public Lookup<T> GenerateTarget(string name)
    {
        GeneratedNames.All.Add(new GeneratedNames.Entry(name, null, what, Candidates, Kind: GeneratedNames.Kind.Target));
        return this;
    }

    // Into the generated constants (GameNames) as `name`: a number read from the game's code (the encounter's weak share).
    public Lookup<T> GenerateConstant(string name)
    {
        GeneratedNames.All.Add(new GeneratedNames.Entry(name, null, what, Candidates, Kind: GeneratedNames.Kind.Constant));
        return this;
    }
}

// The lookups that go into the generated names (Lookup.Generate), for the generator.
internal static class GeneratedNames
{
    public sealed record Entry(string Name, string TypeName, string What, Func<IReadOnlyList<object>> Candidates, bool Writable = false, bool All = false,
                               Action<object> Pin = null, Kind Kind = Kind.Name);

    public enum Kind { Name, Target, Constant }

    // Every lookup's reset (all made again but the pinned ones), for the generator's next round.
    public static readonly List<Action> Resets = new();

    public static void ResetAll()
    {
        foreach (var reset in Resets)
            reset();
    }

    public static readonly List<Entry> All = new();
}

// A property of the game, found once and used with its own types: the alias reads and writes a TValue of a TOwner,
// with no casts at the place of use. `Known` false when it was not found (Get gives default then, Set does nothing).
//   static readonly GameProperty<BhSettingsTab, Il2CppSystem.Action<ESettingsType>> tabAction =
//       new("tab click action", p => p.PropertyType == typeof(Il2CppSystem.Action<ESettingsType>));
internal sealed class GameProperty<TOwner, TValue> where TOwner : class
{
    readonly Lookup<PropertyInfo> lookup;

    // Of TOwner's own properties, the one that matches (none or several: not found). The interop's twin of an
    // auto-property (_x_k__BackingField) is left out unless `backingField`: it is the one to write the value through.
    public GameProperty(string what, Func<PropertyInfo, bool> match, bool backingField = false) =>
        lookup = Lookup<PropertyInfo>.Among(what,
            () => typeof(TOwner).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType == typeof(TValue) && p.Name.EndsWith("k__BackingField") == backingField && match(p)),
            Members.Name, onlyOne: true);

    // Into the generated names (Lookup.Generate).
    public GameProperty<TOwner, TValue> Generate(string name, bool writable = false)
    {
        lookup.Generate(name, writable: writable);
        return this;
    }

    public bool Known => lookup.Known;
    public PropertyInfo Member => lookup.Value;
}

// A method of the game, found once and called with its own result type; `Known` false when it was not found (Call
// gives default then).
//   static readonly GameMethod<BhCheckbox, bool> isOn = new("switch is on", m => m.GetParameters().Length == 0);
internal sealed class GameMethod<TOwner, TResult> where TOwner : class
{
    readonly Lookup<MethodInfo> lookup;

    // Of TOwner's own methods returning TResult, the one that matches (clones count once; none or several: not found).
    public GameMethod(string what, Func<MethodInfo, bool> match) =>
        lookup = Lookup<MethodInfo>.Among(what,
            () => typeof(TOwner).Methods().Returns(typeof(TResult)).Where(match).DistinctCode(), Members.Name, onlyOne: true);

    public GameMethod<TOwner, TResult> Generate(string name)
    {
        lookup.Generate(name);
        return this;
    }

    public bool Known => lookup.Known;
    public MethodInfo Member => lookup.Value;
}

// The same for a method that returns nothing.
internal sealed class GameAction<TOwner> where TOwner : class
{
    readonly Lookup<MethodInfo> lookup;

    public GameAction(string what, Func<MethodInfo, bool> match) =>
        lookup = Lookup<MethodInfo>.Among(what,
            () => typeof(TOwner).Methods().Returns(typeof(void)).Where(match).DistinctCode(), Members.Name, onlyOne: true);

    public GameAction<TOwner> Generate(string name)
    {
        lookup.Generate(name);
        return this;
    }

    public bool Known => lookup.Known;
    public MethodInfo Member => lookup.Value;
}

// Finding the game's members by signature, and calling them, without spelling reflection out in every alias.
//   Game.Types.Methods().Static().Returns(typeof(void)).Takes(typeof(CommandBuffer), ...)
//   typeof(BhTooltip).Methods().Returns(typeof(void)).Takes(typeof(BasicTooltip), null).OnlyOne()
internal static class Members
{
    const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // The methods a type declares itself (not inherited, no property accessors).
    public static IEnumerable<MethodInfo> Methods(this Type type) =>
        type.GetMethods(Declared).Where(m => !m.IsSpecialName && !m.IsGenericMethodDefinition);

    public static IEnumerable<MethodInfo> Methods(this IEnumerable<Type> types) => types.SelectMany(Methods);

    public static IEnumerable<MethodInfo> Static(this IEnumerable<MethodInfo> methods) => methods.Where(m => m.IsStatic);

    public static IEnumerable<MethodInfo> Instance(this IEnumerable<MethodInfo> methods) => methods.Where(m => !m.IsStatic);

    public static IEnumerable<MethodInfo> Returns(this IEnumerable<MethodInfo> methods, Type type) => methods.Where(m => m.ReturnType == type);

    // The parameters, in order; a null stands for any type.
    public static IEnumerable<MethodInfo> Takes(this IEnumerable<MethodInfo> methods, params Type[] parameters) =>
        methods.Where(m => m.GetParameters() is var ps && ps.Length == parameters.Length &&
                           ps.Zip(parameters, (p, t) => t == null || p.ParameterType == t).All(x => x));

    public static IEnumerable<MethodInfo> Named(this IEnumerable<MethodInfo> methods, string name) => methods.Where(m => m.Name == name);

    // The game's own name of an interop member (CodeSource.GameName: BepInEx may name it by its signature, and an
    // override or an interface's method then has another interop name than the one it stands for); its name else.
    public static string NameInGame(this MemberInfo member) => CodeSource.Current.GameName(member) ?? member.Name;

    public static IEnumerable<MethodInfo> NamedInGame(this IEnumerable<MethodInfo> methods, string name) => methods.Where(m => m.NameInGame() == name);

    // One method per native code: clones (the same code under several names) count once.
    public static IEnumerable<MethodInfo> DistinctCode(this IEnumerable<MethodInfo> methods) =>
        methods.GroupBy(m => Game.NativeAddress(m) is var a && a != IntPtr.Zero ? (object)a : m)   // an unknown code (0) is its own: not a clone of another
            .Select(g => g.OrderBy(m => m.Name, StringComparer.Ordinal).First());                    // the same one in the game and in its files

    // The one found, or null when none or several (a lookup by signature must be unambiguous).
    public static T OnlyOne<T>(this IEnumerable<T> found) where T : class
    {
        var list = found.Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }

    // The game's types deriving from this one.
    public static IEnumerable<Type> Subclasses(this Type type) => Game.Types.Where(t => t != type && type.IsAssignableFrom(t));

    public static string Name(MethodInfo m) => $"{m.DeclaringType?.Name}.{m.Name}";
    public static string Name(PropertyInfo p) => $"{p.DeclaringType?.Name}.{p.Name}";
    public static string Names(IEnumerable<MethodInfo> methods) => string.Join(", ", methods.Select(Name));
}
