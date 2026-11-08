// Lists the members of game types from the BepInEx interop assemblies, without the game:
// dotnet run --project tools/typedump -- <type name or regex> [assembly, default Hex]
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

var game = @"C:\XboxGames\Heroes of Might and Magic- Olden Era\Content\BepInEx";
var dirs = new[] { Path.Combine(game, "interop"), Path.Combine(game, "core"), Path.Combine(game, "..", "dotnet") };
AssemblyLoadContext.Default.Resolving += (context, name) =>
    dirs.Select(d => Path.Combine(d, name.Name + ".dll")).Where(File.Exists).Select(context.LoadFromAssemblyPath).FirstOrDefault();
var name = args.Length > 1 ? args[1] : "Hex";
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(File.Exists(name) ? Path.GetFullPath(name) : Path.Combine(dirs[0], name + ".dll"));
Type[] types;
try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
// "uses:<regex>" lists the members of any type whose signature matches, instead of the members of matching types.
var uses = args[0].StartsWith("uses:") ? new Regex(args[0][5..]) : null;
var pattern = new Regex($"^(?:{(uses != null ? ".*" : args[0])})$");
const BindingFlags all = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
// With BepInEx's signature names (UnhollowerDeobfuscationRegex) a renamed type or member keeps the game's own name in
// ObfuscatedNameAttribute: shown as "[was eqe]".
string Was(MemberInfo m) => m.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "ObfuscatedNameAttribute")?.ConstructorArguments.FirstOrDefault().Value is string s ? $" [was {s}]" : "";
string Name(Type t) => t.IsGenericType ? $"{t.Name.Split('`')[0]}<{string.Join(",", t.GetGenericArguments().Select(Name))}>" : t.Name;
foreach (var type in types.Where(t => pattern.IsMatch(t.Name) || pattern.IsMatch(t.FullName ?? "") || pattern.IsMatch(Was(t).Replace(" [was ", "").TrimEnd(']'))))
{
    var lines = new List<string>();
    void Line(string s) { if (uses == null || uses.IsMatch(s)) lines.Add(s); }
    if (type.IsEnum)
        Line("  values " + string.Join(", ", Enum.GetNames(type).Select(n => $"{n}={Convert.ToInt64(Enum.Parse(type, n))}")));
    foreach (var p in type.GetProperties(all))
        Line($"  prop {Name(p.PropertyType)} {p.Name}{Was(p)}");
    foreach (var m in type.GetMethods(all).Where(m => !m.IsSpecialName))
        Line($"  {(m.IsStatic ? "static " : "")}{Name(m.ReturnType)} {m.Name}({string.Join(", ", m.GetParameters().Select(p => Name(p.ParameterType)))}){Was(m)}");
    if (uses == null || lines.Count > 0)
        Console.WriteLine($"== {type.FullName}{Was(type)} : {(type.BaseType != null ? Name(type.BaseType) : "")}\n{string.Join("\n", lines)}");
}
