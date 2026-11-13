using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.Research;

// The generated names (docs/game-names.md): the lookups marked with Lookup.Generate are run on the live game and
// written as C#:
//   a type         `global using GameHero = global::fgd;`
//   an instance    an extension member with the readable name on its owner:
//                  `extension(GameHero hero) { public global::Hex.Session.Logic.Hero Logic => hero.cqei; }`, a method
//                  as `public int Strength() => squad.mh();`
//   a static       a member of GameNames: `public static bool AlwaysBattle => global::lh.bvdw;`
// A lookup must find exactly one: none, or several (look-alikes, clones with other code), stops the generator, which
// lists the candidates. Several are settled by a pin in GameNames.pins.txt next to the written file ("GameHero.Data =
// fgd.cwtl  # why"); a pin whose member is no longer among the candidates stops it too. Nothing is written then.
internal static class NamesGenerator
{
    sealed record Found(GeneratedNames.Entry Entry, object Member);

    // A list name (Lookup.GenerateAll): the paths of properties from one owner to values of one type.
    // A list name (Lookup.GenerateAll): the paths from one owner to values of one type, each step a property or a method
    // without parameters.
    sealed record Paths(List<MemberInfo[]> All, Type Owner, Type Element);

    static Type TypeOf(MemberInfo step) => step is PropertyInfo p ? p.PropertyType : ((MethodInfo)step).ReturnType;

    // A step as C#: a property by its name, a method called, a method that is a property's getter under its own name (eji.clyl's
    // getter) as that property.
    static string Step(MemberInfo step) =>
        step is MethodInfo method ? GetterOf(method)?.Name ?? method.Name + "()" : step.Name;

    static PropertyInfo GetterOf(MethodInfo method) =>
        method.GetParameters().Length == 0
            ? method.DeclaringType?.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .FirstOrDefault(p => p.GetMethod?.Name == method.Name)
            : null;

    static string PathName(MemberInfo[] path) => path == null ? "?" : $"{path[0].ReflectedType?.Name}.{string.Join(".", path.Select(Step))}";

    // `version`: the game's Application.version, `build`: its GameBuild, which the written file is for (ModBase checks
    // both at load).
    public static IEnumerable<string> Generate(string path, string version, string build)
    {
        _ = Game.HeroType;   // Game's lookups are made (and registered) with its first use
        _ = Game.Encounter;
        var pinsPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", "GameNames.pins.txt");
        var pins = ReadPins(pinsPath);
        var entries = GeneratedNames.All.Where(e => e.Kind == GeneratedNames.Kind.Name).ToList();
        List<string> problems;
        Dictionary<Type, string> aliases;
        List<Found> found;
        HashSet<string> usedPins;
        // In rounds: a pinned member is put into its lookup and the lookups not pinned are made again, so those built on
        // it use it too (the hero's data -> its army); until a round takes no new pin.
        var pinned = new HashSet<GeneratedNames.Entry>();
        for (var round = 0; ; round++)
        {
            problems = new List<string>();
            aliases = new Dictionary<Type, string>();
            found = new List<Found>();
            usedPins = new HashSet<string>();
            var pinnedNow = new List<(GeneratedNames.Entry entry, object member)>();

            // The types first (a member's key is its owner's alias), then the members.
            var candidates = entries.ToDictionary(e => e, e => e.Candidates());
            foreach (var entry in entries.Where(e => IsTypeName(e.Name)))
            {
                if (Pick(entry, entry.Name, candidates[entry], pins, usedPins, problems) is Type type && Pinned(entry, entry.Name, type))
                {
                    if (aliases.TryGetValue(type, out var other) && other != entry.Name)
                        problems.Add($"one class, two names: {type.FullName} = {other}, {entry.Name}");
                    aliases[type] = entry.Name;
                    found.Add(new Found(entry, type));
                }
            }
            // The names of property types (the side wrapper, the stores) before the keys are made: a pin names its owner so.
            foreach (var entry in entries.Where(e => e.TypeName != null && !IsTypeName(e.Name)))
                if (candidates[entry].OfType<PropertyInfo>().Select(p => p.PropertyType).Distinct().ToList() is { Count: 1 } named)
                    aliases.TryAdd(named[0], entry.TypeName);
            foreach (var entry in entries.Where(e => !IsTypeName(e.Name) && !e.All))
            {
                var list = candidates[entry];
                var owner = list.OfType<MemberInfo>().Select(m => m.DeclaringType).FirstOrDefault();
                var key = IsStatic(list.FirstOrDefault()) ? $"GameNames.{entry.Name}" : $"{(owner != null ? Alias(owner, aliases) : "?")}.{entry.Name}";
                if (Pick(entry, key, list, pins, usedPins, problems) is MemberInfo member && Pinned(entry, key, member))
                {
                    found.Add(new Found(entry, member));
                    if (member is PropertyInfo property && entry.TypeName != null)
                        aliases[property.PropertyType] = entry.TypeName;
                }
            }
            foreach (var entry in entries.Where(e => e.All))
            {
                var paths = candidates[entry].Select(c => c switch { MemberInfo m => new[] { m }, MemberInfo[] path => path, _ => null })
                    .Select(p => p != null && p.All(s => s is PropertyInfo || s is MethodInfo { IsStatic: false } m && m.GetParameters().Length == 0) ? p : null).ToList();
                if (paths.Count == 0 || paths.Any(p => p is not { Length: > 0 }))
                {
                    problems.Add(paths.Count == 0 ? $"not found: {entry.Name} ({entry.What})" : $"not a property or a parameterless method, or a path of them: {entry.Name} ({entry.What})");
                    continue;
                }
                var owners = paths.Select(p => p[0].ReflectedType).Distinct().ToList();
                var elements = paths.Select(p => TypeOf(p[^1])).Distinct().ToList();
                if (owners.Count != 1 || elements.Count != 1)
                {
                    problems.Add($"a list of several owners or types: {entry.Name} ({entry.What}): {string.Join("; ", paths.Select(PathName))}");
                    continue;
                }
                found.Add(new Found(entry, new Paths(paths, owners[0], elements[0])));
            }

            // A picked member that came from a pin: noted, to be put into its lookup (always true: it only notes).
            bool Pinned(GeneratedNames.Entry entry, string key, object member)
            {
                if (usedPins.Contains(key))
                    pinnedNow.Add((entry, member));
                return true;
            }

            var fresh = pinnedNow.Where(p => p.entry.Pin != null && pinned.Add(p.entry)).ToList();
            if (fresh.Count == 0 || round >= 4)
                break;
            foreach (var (entry, member) in fresh)
                entry.Pin(member);
            GeneratedNames.ResetAll();
        }
        foreach (var pin in pins.Keys.Where(k => !usedPins.Contains(k)))
            problems.Add($"pin for nothing: {pin} (no such generated name now; remove it from {Path.GetFileName(pinsPath)})");
        foreach (var duplicate in aliases.GroupBy(a => a.Value).Where(g => g.Count() > 1))
            problems.Add($"one name for several classes: {duplicate.Key} = {string.Join(", ", duplicate.Select(a => a.Key.FullName))}");
        var members = found.Where(f => f.Member is not Type).ToList();
        foreach (var f in members)
            if (Unwritable(f.Member as MemberInfo) is { } why)
                problems.Add($"cannot be written: {f.Entry.Name} ({f.Entry.What}) = {Describe(f.Member)}: {why}");
        foreach (var clash in members.Where(f => !IsStatic(f.Member)).GroupBy(f => (Owner(f.Member), f.Entry.Name, Arity(f.Member))).Where(g => g.Count() > 1))
            problems.Add($"one name twice on {Alias(clash.Key.Item1, aliases)}: {clash.Key.Name} = {string.Join(", ", clash.Select(f => Describe(f.Member)))}");

        // The patch targets and the constants, from the lookups as the last round left them.
        var targets = new List<(GeneratedNames.Entry entry, List<MethodInfo> methods)>();
        var constants = new List<(GeneratedNames.Entry entry, object value)>();
        foreach (var entry in GeneratedNames.All.Where(e => e.Kind != GeneratedNames.Kind.Name))
        {
            var values = entry.Candidates();
            if (entry.Kind == GeneratedNames.Kind.Target)
            {
                var methods = values.SelectMany(v => v switch { MethodInfo m => new[] { m }, IEnumerable<MethodInfo> l => l, _ => Enumerable.Empty<MethodInfo>() }).ToList();
                if (methods.Count == 0 && !values.Any(v => v is IEnumerable<MethodInfo>))   // a list may be empty (none on this version)
                    problems.Add($"not found: target {entry.Name} ({entry.What})");
                else
                    targets.Add((entry, methods));
            }
            else if (values is [var value] && value is float or int or bool or string)
                constants.Add((entry, value));
            else
                problems.Add($"not one value: constant {entry.Name} ({entry.What}): {string.Join(", ", values)}");
        }

        // Every class the readable names show (owners, property types, parameters, results, list elements) has a name of
        // its own: an obfuscated one written as it is would change with the next version and break the mods' code.
        foreach (var f in found.Where(f => f.Member is not Type))
            foreach (var type in Exposed(f.Member).SelectMany(Parts).Where(t => string.IsNullOrEmpty(t.Namespace) && !t.IsGenericParameter && !aliases.ContainsKey(t)).Distinct())
                problems.Add($"an obfuscated class with no name: {type.Name} in {f.Entry.Name} ({f.Entry.What}); give it one (Generate(name, typeName) or a type lookup)");

        if (problems.Count > 0)
        {
            yield return $"not written ({problems.Count} problems, solve them before building; a pin goes into {pinsPath}):";
            foreach (var problem in problems)
                yield return "  " + problem;
            yield break;
        }

        // The names of the game's obfuscated classes, in a file of their own: the library compiles it, and so do the
        // plugins that work on the game's code itself (the core, the research console), which stay bound to the version.
        var typeNames = new StringBuilder();
        typeNames.AppendLine($"// Generated for the game {version} with GameNames.g.cs (docs/game-names.md). Do not edit. The game's obfuscated");
        typeNames.AppendLine("// classes under names of their own; they change with every patch, so the library's public members never show them.");
        typeNames.AppendLine();
        foreach (var (type, name) in aliases.OrderBy(a => a.Value))
            typeNames.AppendLine($"global using {name} = {CSharp(type, null)};");
        typeNames.AppendLine();
        typeNames.AppendLine("// A plugin compiling this file works on the game's code itself: built for this version only (ModBase checks it).");
        typeNames.AppendLine($"[assembly: global::System.Reflection.AssemblyMetadata(\"Nobirds.OldenEra.GameVersion\", \"{version}\")]");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", "GameTypes.g.cs"), typeNames.ToString(), new UTF8Encoding(false));

        var code = new StringBuilder();
        code.AppendLine($"// Generated for the game {version} (tools/game-names from the game's files, or the research plugin's `aliases`). Do not edit:");
        code.AppendLine("// make it again (docs/game-names.md). The lookups behind each name are the Lookup.Generate ones in Game*.cs,");
        code.AppendLine("// the choices among several candidates are in GameNames.pins.txt. A name is public when it shows no obfuscated class");
        code.AppendLine("// (GameTypes.g.cs): it then stays the same on the next version of the game; the others are the library's own.");
        code.AppendLine();
        code.AppendLine("namespace Nobirds.OldenEra.Modding.Common;");
        code.AppendLine();
        code.AppendLine("public static class GameNames");
        code.AppendLine("{");
        code.AppendLine($"    public const string GameVersion = \"{version}\";");
        code.AppendLine();
        code.AppendLine("    // the build of that version (GameBuild): the Steam and Game Pass builds have other obfuscated names");
        code.AppendLine($"    public const string GameBuild = \"{build}\";");
        foreach (var (entry, value) in constants.OrderBy(c => c.entry.Name))
        {
            code.AppendLine();
            code.AppendLine($"    // {entry.What}");
            code.AppendLine($"    public const {CSharp(value.GetType(), null)} {entry.Name} = {Literal(value)};");
        }
        if (members.Any(f => f.Member is Paths))
        {
            code.AppendLine();
            code.AppendLine("    // The values the getters give, those that are there (a list name: Lookup.GenerateAll).");
            code.AppendLine("    static global::System.Collections.Generic.IEnumerable<T> Each<T>(params global::System.Func<T>[] getters) where T : class");
            code.AppendLine("    {");
            code.AppendLine("        foreach (var getter in getters)");
            code.AppendLine("        {");
            code.AppendLine("            T value = null;");
            code.AppendLine("            try { value = getter(); }");
            code.AppendLine("            catch (global::System.Exception) { }");
            code.AppendLine("            if (value != null)");
            code.AppendLine("                yield return value;");
            code.AppendLine("        }");
            code.AppendLine("    }");
        }
        foreach (var f in members.Where(f => IsStatic(f.Member)).OrderBy(f => f.Entry.Name))
        {
            code.AppendLine();
            code.AppendLine($"    // {f.Entry.What}: {Describe(f.Member)}");
            code.AppendLine($"    {Access(f.Member, aliases)} static " + Member(f.Entry, (MemberInfo)f.Member, CSharp(f.Member is MemberInfo m ? m.DeclaringType : null, null), aliases));
        }
        foreach (var owner in members.Where(f => !IsStatic(f.Member)).GroupBy(f => Owner(f.Member)).OrderBy(g => Alias(g.Key, aliases)))
        {
            var receiver = Receiver(Alias(owner.Key, aliases));
            code.AppendLine();
            code.AppendLine($"    extension({Alias(owner.Key, aliases)} {receiver})");
            code.AppendLine("    {");
            var first = true;
            foreach (var f in owner.OrderBy(f => f.Entry.Name))
            {
                if (!first)
                    code.AppendLine();
                first = false;
                code.AppendLine($"        // {f.Entry.What}: {Describe(f.Member)}");
                code.AppendLine($"        {Access(f.Member, aliases)} " + (f.Member is Paths paths ? List(f.Entry, paths, receiver, aliases) : Member(f.Entry, (MemberInfo)f.Member, receiver, aliases)));
            }
            code.AppendLine("    }");
        }
        code.AppendLine("}");
        if (targets.Count > 0)
        {
            code.AppendLine();
            code.AppendLine("// The game's methods the core and the mods patch, as the exact members of the game's classes (no search while the");
            code.AppendLine("// game runs); found by the lookups in search/Game*.cs marked GenerateTarget.");
            code.AppendLine("internal static class GameTargets");
            code.AppendLine("{");
            code.AppendLine("    const global::System.Reflection.BindingFlags Any = global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic |");
            code.AppendLine("        global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.DeclaredOnly;");
            code.AppendLine();
            code.AppendLine("    static global::System.Reflection.MethodInfo M(global::System.Type type, string name, params global::System.Type[] parameters) =>");
            code.AppendLine("        type.GetMethod(name, Any, null, parameters, null);");
            foreach (var (entry, methods) in targets.OrderBy(t => t.entry.Name))
            {
                code.AppendLine();
                code.AppendLine($"    // {entry.What}: {(methods.Count > 0 ? string.Join(", ", methods.Select(m => Describe(m))) : "none on this version")}");
                code.AppendLine(methods.Count == 1 && entry.Candidates() is [MethodInfo]
                    ? $"    public static readonly global::System.Reflection.MethodInfo {entry.Name} = {Target(methods[0], aliases)};"
                    : methods.Count == 0
                        ? $"    public static readonly global::System.Reflection.MethodInfo[] {entry.Name} = global::System.Array.Empty<global::System.Reflection.MethodInfo>();"
                        : $"    public static readonly global::System.Reflection.MethodInfo[] {entry.Name} =\n    {{\n        {string.Join(",\n        ", methods.Select(m => Target(m, aliases)))},\n    }};");
            }
            code.AppendLine("}");
        }
        File.WriteAllText(path, code.ToString(), new UTF8Encoding(false));
        yield return $"written {path}: {aliases.Count} types, {members.Count(f => f.Member is PropertyInfo)} properties, " +
                     $"{members.Count(f => f.Member is MethodInfo)} methods ({members.Count(f => IsStatic(f.Member))} static), " +
                     $"{members.Count(f => f.Member is Paths)} lists, {targets.Count} patch targets, {constants.Count} constants, {usedPins.Count} pinned, for the game {version}";
    }


    // M(typeof(Owner), nameof(Owner.member), typeof(P1), ...): the name checked by the compiler (a getter under its
    // own name cannot be named so: its name as text).
    static string Target(MethodInfo method, Dictionary<Type, string> aliases)
    {
        var owner = Alias(method.DeclaringType, aliases);
        var name = GetterOf(method) != null || method.IsGenericMethodDefinition ? $"\"{method.Name}\"" : $"nameof({owner}.{method.Name})";
        var parameters = method.GetParameters().Select(p => p.ParameterType.IsByRef
            ? $"typeof({Alias(p.ParameterType.GetElementType(), aliases)}).MakeByRefType()"
            : $"typeof({Alias(p.ParameterType, aliases)})");
        return $"M(typeof({owner}), {string.Join(", ", parameters.Prepend(name))})";
    }

    static string Literal(object value) => value switch
    {
        float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f",
        bool b => b ? "true" : "false",
        string s => $"\"{s}\"",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
    };

    // A generated member is public when none of the classes it shows is an obfuscated one (an alias of GameTypes.g.cs).
    static string Access(object member, Dictionary<Type, string> aliases) =>
        Exposed(member).SelectMany(Parts).Any(aliases.ContainsKey) ? "internal" : "public";
    // The classes a generated member shows.
    static IEnumerable<Type> Exposed(object member) => member switch
    {
        Paths paths => new[] { paths.Owner, paths.Element },
        PropertyInfo p => IsStatic(p) ? new[] { p.PropertyType } : new[] { p.DeclaringType, p.PropertyType },   // a static one is GameNames'
        MethodInfo m => m.GetParameters().Select(x => x.ParameterType).Append(m.ReturnType).Concat(m.IsStatic ? Enumerable.Empty<Type>() : new[] { m.DeclaringType }),
        _ => Enumerable.Empty<Type>(),
    };

    // A type and the types it is made of (an array's element, a generic's arguments, a ref's target).
    static IEnumerable<Type> Parts(Type type)
    {
        if (type == null)
            yield break;
        if (type.HasElementType)
        {
            foreach (var part in Parts(type.GetElementType()))
                yield return part;
            yield break;
        }
        if (type.IsGenericType)
            foreach (var part in type.GetGenericArguments().SelectMany(Parts))
                yield return part;
        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
    }
    // ---------- choosing ----------

    // The one candidate, or the pinned one; null with the problem said.
    static object Pick(GeneratedNames.Entry entry, string key, IReadOnlyList<object> candidates, Dictionary<string, (string member, string why)> pins,
                       HashSet<string> usedPins, List<string> problems)
    {
        if (pins.TryGetValue(key, out var pin))
        {
            usedPins.Add(key);
            var pinned = candidates.Where(c => PinName(c) == pin.member).ToList();
            // A pin written with the game's own names (eqe.bhqa) while BepInEx names them by signature: taken, and said
            // with the interop's name to write into the pins instead.
            if (pinned.Count == 0)
            {
                pinned = candidates.Where(c => GamePinName(c) == pin.member).Distinct().ToList();
                if (pinned.Count == 1)
                    Console.WriteLine($"  pin {key} = {pin.member} is {PinName(pinned[0])}");
            }
            if (pinned.Count == 1)
                return pinned[0];
            problems.Add($"pinned {key} = {pin.member} ({pin.why}) is not among the candidates now: {List(candidates)}");
            return null;
        }
        if (candidates.Count == 1)
            return candidates[0];
        problems.Add(candidates.Count == 0
            ? $"not found: {key} ({entry.What})"
            : $"several for {key} ({entry.What}), pin one: {List(candidates)}");
        return null;
    }

    static string List(IReadOnlyList<object> candidates) =>
        candidates.Count == 0 ? "none" : string.Join("; ", candidates.Select(c => $"{PinName(c)} [{Describe(c)}{Code(c)}]"));

    // What a pin names: a class by its name, a member as Class.member.
    static string PinName(object candidate) => candidate switch
    {
        Type t => t.Name,
        MemberInfo m => $"{m.DeclaringType?.Name}.{m.Name}",
        _ => candidate?.ToString(),
    };

    // The same with the game's own names (CodeSource.GameName); null when not known.
    static string GamePinName(object candidate) => candidate switch
    {
        Type t => CodeSource.Current.GameName(t),
        MemberInfo m when m.DeclaringType != null && CodeSource.Current.GameName(m) is { } name => $"{CodeSource.Current.GameName(m.DeclaringType)}.{name}",
        _ => null,
    };

    // A method's native code and what it calls, to tell look-alikes apart.
    static string Code(object candidate)
    {
        if (candidate is not MethodInfo m)
            return "";
        try { return $", calls {string.Join(", ", GameCode.Calls(m).Take(8).Select(c => GameCode.NameAt(c.target)))}"; }
        catch (Exception) { return ""; }
    }

    // GameNames.pins.txt: "key = Class.member  # why", one per line; # starts a comment.
    static Dictionary<string, (string member, string why)> ReadPins(string path)
    {
        var pins = new Dictionary<string, (string, string)>();
        if (!File.Exists(path))
            return pins;
        foreach (var raw in File.ReadAllLines(path))
        {
            var hash = raw.IndexOf('#');
            var line = (hash >= 0 ? raw[..hash] : raw).Trim();
            var why = hash >= 0 ? raw[(hash + 1)..].Trim() : "";
            var equals = line.IndexOf('=');
            if (equals > 0)
                pins[line[..equals].Trim()] = (line[(equals + 1)..].Trim(), why);
        }
        return pins;
    }

    // ---------- writing ----------

    static bool IsTypeName(string name) => name.StartsWith("Game") && name.Length > 4 && char.IsUpper(name[4]);

    static bool IsStatic(object member) => member switch
    {
        PropertyInfo p => (p.GetMethod ?? p.SetMethod)?.IsStatic == true,
        MethodInfo m => m.IsStatic,
        _ => false,
    };

    static Type Owner(object member) => member is Paths paths ? paths.Owner : (member as MemberInfo)?.DeclaringType;

    static int Arity(object member) => member is MethodInfo m ? m.GetParameters().Length : -1;

    static string Describe(object member) => member switch
    {
        Type t => t.FullName + InGame(t),
        Paths paths => string.Join(", ", paths.All.Select(PathName)),
        PropertyInfo p => $"{p.DeclaringType?.Name}.{p.Name} : {p.PropertyType.Name}{InGame(p)}",
        MethodInfo m => $"{m.DeclaringType?.Name}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) : {m.ReturnType.Name}{InGame(m)}",
        _ => member?.ToString(),
    };

    // The game's own name where BepInEx named it by its signature: " (in the game eqe.bhqa)"; nothing when the same.
    static string InGame(MemberInfo member)
    {
        var name = GamePinName(member);
        return name == null || name.Replace('<', '_').Replace('>', '_') == PinName(member) ? "" : $" (in the game {name})";
    }

    // Why a member cannot be written as plain C#, or null.
    static string Unwritable(MemberInfo member) => member switch
    {
        MethodInfo { IsGenericMethodDefinition: true } => "a generic method",
        MethodInfo m when m.GetParameters().Any(p => p.ParameterType.IsPointer) => "a pointer parameter",
        PropertyInfo p when p.GetIndexParameters().Length > 0 => "an indexer",
        _ => null,
    };

    // `IEnumerable<T> Name => GameNames.Each<T>(() => target.a, () => target.b?.c);` for a list name.
    static string List(GeneratedNames.Entry entry, Paths paths, string target, Dictionary<Type, string> aliases)
    {
        var element = Alias(paths.Element, aliases);
        var getters = paths.All.Select(path => $"() => {target}.{string.Join("?.", path.Select(Step))}");
        return $"global::System.Collections.Generic.IEnumerable<{element}> {entry.Name} => GameNames.Each<{element}>({string.Join(", ", getters)});";
    }

    // `Type Name => target.member;` for a property: read only (the mods read the game's state) unless marked writable
    // (Generate(..., writable: true): our own copies of the game's widgets); `Type Name(params) => target.member(args);`
    // for a method.
    static string Member(GeneratedNames.Entry entry, MemberInfo member, string target, Dictionary<Type, string> aliases)
    {
        var name = entry.Name;
        if (member is PropertyInfo property)
            return entry.Writable && property.SetMethod != null
                ? $"{Alias(property.PropertyType, aliases)} {name} {{ get => {target}.{property.Name}; set => {target}.{property.Name} = value; }}"
                : $"{Alias(property.PropertyType, aliases)} {name} => {target}.{property.Name};";
        var method = (MethodInfo)member;
        // A method that is a property's getter under its own name (fqu.bnyp, the getter of fqu.cwyd): C# reads the
        // property, it does not call the accessor.
        if (method.GetParameters().Length == 0 && method.DeclaringType?
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .FirstOrDefault(p => p.GetMethod?.Name == method.Name) is { } owner)
            return $"{Alias(method.ReturnType, aliases)} {name}() => {target}.{owner.Name};";
        var parameters = method.GetParameters();
        string Mode(ParameterInfo p) => !p.ParameterType.IsByRef ? "" : p.IsOut ? "out " : "ref ";
        string TypeOf(ParameterInfo p) => Alias(p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType, aliases);
        var declared = string.Join(", ", parameters.Select((p, i) => $"{Mode(p)}{TypeOf(p)} a{i}"));
        var passed = string.Join(", ", parameters.Select((p, i) => $"{Mode(p)}a{i}"));
        var result = method.ReturnType == typeof(void) ? "void" : Alias(method.ReturnType, aliases);
        return $"{result} {name}({declared}) => {target}.{method.Name}({passed});";
    }

    static string Alias(Type type, Dictionary<Type, string> aliases) => aliases.TryGetValue(type, out var name) ? name : CSharp(type, aliases);

    static string Receiver(string alias) => alias.StartsWith("Game") && alias.Length > 4 && !alias.Contains('.') ? char.ToLowerInvariant(alias[4]) + alias[5..] : "it";

    // The C# name of a type: global::Namespace.Name, nested with dots, generic with its arguments.
    static string CSharp(Type type, Dictionary<Type, string> aliases)
    {
        if (type == null)
            return "?";
        if (aliases != null && aliases.TryGetValue(type, out var alias))
            return alias;
        if (type == typeof(void)) return "void";
        if (type == typeof(int)) return "int";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(float)) return "float";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (type.IsArray)
            return CSharp(type.GetElementType(), aliases) + "[]";
        var name = type.Name;
        if (type.IsGenericType)
            name = name[..name.IndexOf('`')] + "<" + string.Join(", ", type.GetGenericArguments().Select(a => CSharp(a, aliases))) + ">";
        if (type.DeclaringType != null)
            return CSharp(type.DeclaringType, null) + "." + name;
        return string.IsNullOrEmpty(type.Namespace) ? $"global::{name}" : $"global::{type.Namespace}.{name}";
    }
}
