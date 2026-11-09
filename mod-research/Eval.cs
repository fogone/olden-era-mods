using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.Research;

// Live reflection over the game's objects: an expression is a chain of steps from a root.
//   hero                       the hero selected in the HUD (Game.SelectedHero)
//   $name, $                   a variable (set $name <expr>), the last result
//   Type                       a game or Unity type: its static members follow
//   .name                      a property or field (instance, or static after a type); a method without arguments
//   .name(1, "text", true, $x) a method with arguments (literals, variables, nested expressions)
//   [i]                        an element of an array or a list (Il2Cpp arrays, List`1 via get_Item)
//   .as(Type)                  the IL2CPP object as a wrapper of another type (TryCast)
//   e.g. hero.cqei.cwfa   dcz.me.yup().cwkq   hero.as(fgd).blsy()   feh.me.cpyd
internal static class Eval
{
    public static readonly Dictionary<string, object> Variables = new();
    public static object Last;

    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    public static object Run(string expression)
    {
        var parser = new Parser(expression.Trim());
        var value = parser.Expression();
        if (!parser.AtEnd)
            throw new FormatException($"unexpected '{parser.Rest}'");
        return Last = value;
    }

    // A value and its kind, in one line.
    public static string Show(object value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        Type t => $"type {t.FullName}",
        Il2CppObjectBase o => $"{Game.ClassName(o.Pointer)}@{o.Pointer.ToInt64():X} (as {o.GetType().Name})",
        _ => $"{value} ({value.GetType().Name})",
    };

    // Every property and field of the object (or of a type: its static ones) with its value; methods listed by signature.
    public static string Inspect(object value, bool methods)
    {
        var text = new StringBuilder(Show(value)).Append('\n');
        var type = value as Type ?? value?.GetType();
        if (type == null)
            return text.ToString();
        var flags = value is Type ? BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy : BindingFlags.Public | BindingFlags.Instance;
        var target = value is Type ? null : value;
        foreach (var p in type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0 && !p.Name.StartsWith("_")).OrderBy(p => p.Name))
        {
            string shown;
            try { shown = Show(p.GetValue(target)); }
            catch (Exception e) { shown = $"!{(e.InnerException ?? e).GetType().Name}"; }
            text.Append($"  {p.PropertyType.Name} {p.Name} = {shown}\n");
        }
        if (methods)
            foreach (var m in type.GetMethods(flags).Where(m => !m.IsSpecialName && m.DeclaringType?.Namespace?.StartsWith("Il2CppInterop") != true && m.DeclaringType != typeof(object)).OrderBy(m => m.Name))
                text.Append($"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {GameCode.Signature(m)}\n");
        if (Elements(value) is { } elements)
        {
            var i = 0;
            foreach (var element in elements.Take(50))
                text.Append($"  [{i++}] = {Show(element)}\n");
        }
        return text.ToString();
    }

    // The elements of an array or a list, null when the value is neither.
    static IEnumerable<object> Elements(object value)
    {
        if (value == null || value is string || value is Type)
            return null;
        var type = value.GetType();
        var count = type.GetProperty("Count")?.GetValue(value) ?? type.GetProperty("Length")?.GetValue(value);
        var item = type.GetProperty("Item");
        if (count is not int n || item == null || item.GetIndexParameters().Length != 1 || item.GetIndexParameters()[0].ParameterType != typeof(int))
            return null;
        return Enumerable.Range(0, n).Select(i => item.GetValue(value, new object[] { i }));
    }

    // The game's and Unity's types by name.
    public static IEnumerable<Type> TypesNamed(string name) => Types().Where(t => t.Name == name || t.FullName == name);

    static Type TypeNamed(string name) => TypesNamed(name).FirstOrDefault();

    static Type[] types;

    static Type[] Types() => types ??= new[] { typeof(Hex.UI.Tooltips.BhTooltip).Assembly, typeof(UnityEngine.Object).Assembly, typeof(UnityEngine.Camera).Assembly,
                                               typeof(UnityEngine.UI.Image).Assembly, typeof(UnityEngine.Canvas).Assembly, typeof(TMPro.TMP_Text).Assembly, typeof(Il2CppSystem.String).Assembly }
        .Distinct().SelectMany(Game.SafeTypes).ToArray();

    sealed class Parser
    {
        readonly string text;
        int at;

        public Parser(string text) => this.text = text;
        public bool AtEnd => Skip() >= text.Length;
        public string Rest => text[at..];

        int Skip()
        {
            while (at < text.Length && char.IsWhiteSpace(text[at]))
                at++;
            return at;
        }

        bool Take(char c)
        {
            if (Skip() < text.Length && text[at] == c)
            {
                at++;
                return true;
            }
            return false;
        }

        string Name()
        {
            Skip();
            var start = at;
            while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] == '_'))
                at++;
            if (start == at)
                throw new FormatException($"a name expected at '{Rest}'");
            return text[start..at];
        }

        // A root and its steps.
        public object Expression()
        {
            var (value, isType) = Root();
            while (true)
            {
                if (Take('.'))
                {
                    var name = Name();
                    if (Take('('))
                    {
                        var args = Arguments();
                        if (name == "as" && args.Count == 1 && args[0] is Type type)
                        {
                            value = value.CastTo(type) ?? throw new InvalidCastException($"not a {type.Name}");
                            isType = false;
                            continue;
                        }
                        value = CallMethod(value, isType, name, args);
                    }
                    else
                        value = Member(value, isType, name);
                    isType = false;
                }
                else if (Take('['))
                {
                    var index = Expression();
                    if (!Take(']'))
                        throw new FormatException("']' expected");
                    value = Element(value, index);
                }
                else
                    return value;
            }
        }

        (object value, bool isType) Root()
        {
            Skip();
            if (Take('$'))
            {
                if (AtEnd || !(char.IsLetterOrDigit(text[at]) || text[at] == '_'))
                    return (Last, false);
                var variable = Name();
                return Variables.TryGetValue(variable, out var v) ? (v, false) : throw new KeyNotFoundException($"${variable} is not set");
            }
            if (Take('"'))
            {
                var end = text.IndexOf('"', at);
                if (end < 0)
                    throw new FormatException("unclosed string");
                var s = text[at..end];
                at = end + 1;
                return (s, false);
            }
            if (at < text.Length && (char.IsDigit(text[at]) || text[at] == '-'))
            {
                var start = at++;
                while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] == '.'))
                    at++;
                var literal = text[start..at];
                if (literal.StartsWith("0x"))
                    return (Convert.ToInt64(literal[2..], 16), false);
                return literal.Contains('.') ? (float.Parse(literal, CultureInfo.InvariantCulture), false) : (int.Parse(literal, CultureInfo.InvariantCulture), false);
            }
            var name = Name();
            switch (name)
            {
                case "null": return (null, false);
                case "true": return (true, false);
                case "false": return (false, false);
                case "hero": return (Game.SelectedHero, false);
            }
            return TypeNamed(name) is { } type ? (type, true) : throw new KeyNotFoundException($"no type or root '{name}'");
        }

        List<object> Arguments()
        {
            var args = new List<object>();
            if (Take(')'))
                return args;
            do args.Add(Expression());
            while (Take(','));
            if (!Take(')'))
                throw new FormatException("')' expected");
            return args;
        }

        static object Member(object value, bool isType, string name)
        {
            var type = isType ? (Type)value : value?.GetType() ?? throw new NullReferenceException($"null before .{name}");
            var target = isType ? null : value;
            if (type.GetProperty(name, Any) is { } property && property.GetIndexParameters().Length == 0)
                return property.GetValue(target);
            if (type.GetField(name, Any) is { } field)
                return field.GetValue(target);
            return CallMethod(value, isType, name, new List<object>());
        }

        static object CallMethod(object value, bool isType, string name, List<object> args)
        {
            var type = isType ? (Type)value : value?.GetType() ?? throw new NullReferenceException($"null before .{name}()");
            var candidates = type.GetMethods(Any).Where(m => m.Name == name && m.GetParameters().Length == args.Count && m.IsStatic == isType).ToList();
            foreach (var method in candidates)
            {
                var parameters = method.GetParameters();
                var converted = new object[args.Count];
                var fits = true;
                for (var i = 0; i < args.Count && fits; i++)
                    fits = TryConvert(args[i], parameters[i].ParameterType, out converted[i]);
                if (fits)
                    return method.Invoke(isType ? null : value, converted);
            }
            throw new MissingMethodException($"{type.Name}.{name} with {args.Count} argument(s): {(candidates.Count == 0 ? "none" : string.Join("; ", candidates.Select(GameCode.Signature)))}");
        }

        static bool TryConvert(object arg, Type to, out object converted)
        {
            converted = arg;
            if (arg == null)
                return !to.IsValueType;
            if (to.IsInstanceOfType(arg))
                return true;
            if (arg is Il2CppObjectBase && (converted = arg.CastTo(to)) != null)
                return true;
            try
            {
                if (to.IsEnum)
                    converted = Enum.ToObject(to, Convert.ToInt64(arg));
                else if (to.IsPrimitive)
                    converted = Convert.ChangeType(arg, to, CultureInfo.InvariantCulture);
                else
                    return false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static object Element(object value, object index)
        {
            var type = value?.GetType() ?? throw new NullReferenceException("null before [ ]");
            var item = type.GetProperty("Item") ?? throw new MissingMemberException($"{type.Name} has no indexer");
            var parameter = item.GetIndexParameters()[0].ParameterType;
            return TryConvert(index, parameter, out var key) ? item.GetValue(value, new[] { key }) : throw new InvalidCastException($"index is not a {parameter.Name}");
        }
    }
}
