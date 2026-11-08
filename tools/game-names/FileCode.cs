using System.Reflection;
using System.Reflection.Emit;
using LibCpp2IL;
using LibCpp2IL.Metadata;
using LibCpp2IL.Reflection;
using OldenEra.Modding.Common;

namespace OldenEra.Modding.GameNamesTool;

// The game's code read from its files (GameAssembly.dll and global-metadata.dat) with LibCpp2IL, the library BepInEx
// itself makes the interop assemblies with: the same addresses, code and field offsets the running game has (checked
// 05.11: DataFogOfWar.bkin at RVA 0x23554E0, BhSkulls.umq at 0x2F16B40 in both). The interop assemblies' types are
// matched to the metadata's by namespace and name, the methods by name and parameters.
internal sealed class FileCode : CodeSource
{
    readonly byte[] binary;
    readonly Dictionary<string, Il2CppTypeDefinition> typesByKey = new();
    readonly Dictionary<Il2CppTypeDefinition, Type> interopTypes = new();
    readonly Dictionary<MethodInfo, long> addresses = new();

    public FileCode(string gameDir, IEnumerable<Type> interop)
    {
        var data = Path.Combine(gameDir, "HeroesOldenEra_Data");
        var unity = LibCpp2IlMain.DetermineUnityVersion(Path.Combine(gameDir, "UnityPlayer.dll"), data);
        if (!LibCpp2IlMain.LoadFromFile(Path.Combine(gameDir, "GameAssembly.dll"), Path.Combine(data, @"il2cpp_data\Metadata\global-metadata.dat"), unity))
            throw new InvalidOperationException("LibCpp2IL could not read the game's files");
        binary = LibCpp2IlMain.Binary.GetRawBinaryContent();
        foreach (var definition in LibCpp2IlMain.TheMetadata.typeDefs)
            typesByKey.TryAdd(Key(definition), definition);
        foreach (var type in interop)
            if (Definition(type) is { } definition)
                interopTypes.TryAdd(definition, type);
    }

    // Debugging the links of one class (OE_LINKS=<the game's class name>): its properties and methods with the game's names.
    public void DumpLinks(Type type)
    {
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            string pointer;
            try { pointer = Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppFieldInfoPointerFieldForGeneratedFieldAccessor(p.GetMethod)?.Name ?? "-"; }
            catch (Exception e) { pointer = e.GetType().Name; }
            string method;
            try { method = Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(p.GetMethod)?.Name ?? "-"; }
            catch (Exception e) { method = e.GetType().Name; }
            var il = p.GetMethod?.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
            var calls = new List<string>();
            for (var i = 0; i + 4 < il.Length; i++)
                if (il[i] is 0x28 or 0x6F or 0x7E)
                    try { calls.Add(il[i] == 0x7E ? "ldsfld " + p.Module.ResolveField(BitConverter.ToInt32(il, i + 1))?.Name : "call " + p.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1))?.Name); } catch (Exception) { }
            Console.WriteLine($"  {p.Name}: field ptr {pointer}, method ptr {method}, token {Token(p.GetMethod)}, game {GameName(p)}; il: {string.Join(", ", calls.Take(6))}");
        }
    }

    // ---------- types ----------

    // "Namespace.Outer/Inner": the metadata's names; the interop's own namespace for mscorlib is Il2CppSystem. A type
    // BepInEx renamed by its signature (UnhollowerDeobfuscationRegex) keeps the game's name in ObfuscatedNameAttribute.
    static string Key(Il2CppTypeDefinition definition) =>
        definition.DeclaringType is { } outer ? $"{Key(outer)}/{definition.Name}" : $"{definition.Namespace}.{definition.Name}";

    static string Key(Type type)
    {
        var name = GameName(type);
        if (type.DeclaringType is { } outer)
            return $"{Key(outer)}/{name}";
        var ns = type.Namespace ?? "";
        if (ns == "Il2CppSystem" || ns.StartsWith("Il2CppSystem."))
            ns = ns["Il2Cpp".Length..];
        return $"{ns}.{name}";
    }

    static string GameName(Type type)
    {
        var was = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "ObfuscatedNameAttribute")?.ConstructorArguments.FirstOrDefault().Value as string;
        if (string.IsNullOrEmpty(was))
            return type.Name;
        var name = was.Split('.', '/', '+').Last();
        return type.IsGenericType && !name.Contains('`') ? $"{name}`{type.GetGenericArguments().Length}" : name;
    }

    // ---------- the interop's links to the game's code ----------

    // Each interop class's static constructor sets its NativeMethodInfoPtr_... fields from IL2CPP by the method's
    // metadata token (ldc.i4 token; call GetIl2CppMethodByToken; stsfld) and its NativeFieldInfoPtr_... fields by the
    // field's name (ldstr name; call GetIl2CppField; stsfld): what ties an interop member, whatever its name, to the
    // game's own. The last int or string before each stsfld, per static field.
    readonly Dictionary<Type, Dictionary<FieldInfo, object>> natives = new();
    static Dictionary<short, OpCode> opCodes;

    Dictionary<FieldInfo, object> Natives(Type type)
    {
        if (natives.TryGetValue(type, out var found))
            return found;
        found = natives[type] = new Dictionary<FieldInfo, object>();
        byte[] il;
        try { il = type.TypeInitializer?.GetMethodBody()?.GetILAsByteArray(); }
        catch (Exception) { return found; }
        if (il == null)
            return found;
        opCodes ??= typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);
        object last = null;
        for (var i = 0; i < il.Length;)
        {
            short value = il[i] == 0xFE && i + 1 < il.Length ? (short)(0xFE00 | il[i + 1]) : il[i];
            if (!opCodes.TryGetValue(value, out var op))
                break;
            i += op.Size;
            var operand = i + 4 <= il.Length ? BitConverter.ToInt32(il, i) : 0;
            if (op == OpCodes.Ldc_I4)
                last = operand;
            else if (op == OpCodes.Ldstr)
            {
                try { last = type.Module.ResolveString(operand); } catch (Exception) { last = null; }
            }
            else if (op == OpCodes.Stsfld)
            {
                try
                {
                    if (last != null && type.Module.ResolveField(operand, type.IsGenericType ? type.GetGenericArguments() : null, null) is { } field)
                        found[field] = last;
                }
                catch (Exception) { }
                last = null;
            }
            i += OperandSize(op, il, i);
        }
        return found;
    }

    static int OperandSize(OpCode op, byte[] il, int at) => op.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, at),
        _ => 4,
    };

    object NativeOf(FieldInfo pointer) =>
        pointer?.DeclaringType is { } owner && Natives(owner).TryGetValue(pointer, out var value) ? value : null;

    protected override string FindFieldName(PropertyInfo property)
    {
        if (property.GetMethod is not { } getter)
            return null;
        try { return NativeOf(Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppFieldInfoPointerFieldForGeneratedFieldAccessor(getter)) as string; }
        catch (Exception) { return null; }
    }

    // A real property by the metadata's property whose getter is the interop getter's method (the game's getters are
    // obfuscated too, not get_<name>).
    protected override string FindGameName(MemberInfo member) => member switch
    {
        Type type => GameName(type),
        PropertyInfo property => FieldName(property) ?? (property.GetMethod is { } getter && Token(getter) is var token && token != 0
            ? Definition(property.DeclaringType)?.Properties?.FirstOrDefault(p => p.Getter?.token == (uint)token)?.Name
            : null),
        _ => base.FindGameName(member),
    };

    // The metadata method's return type against the property's type (by the type's key: namespace and the game's name).
    protected override bool FindGetterFits(PropertyInfo property)
    {
        if (property.GetMethod is not { } getter || Token(getter) is var token && token == 0)
            return true;
        var native = Definition(property.DeclaringType)?.Methods?.FirstOrDefault(m => m.token == (uint)token);
        var returned = native?.ReturnType?.baseType;
        var expected = Definition(property.PropertyType);
        return returned == null || expected == null || returned == expected;
    }

    protected override string MethodName(MethodInfo method) => Token(method) is var token && token != 0
        ? Definition(method.DeclaringType)?.Methods?.FirstOrDefault(m => m.token == (uint)token)?.Name
        : null;

    // The method's metadata token, as the interop asks IL2CPP for it; 0 when unknown.
    int Token(MethodInfo method)
    {
        try { return NativeOf(Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method)) is int token ? token : 0; }
        catch (Exception) { return 0; }
    }

    Il2CppTypeDefinition Definition(Type type)
    {
        if (type == null || type.IsGenericParameter)
            return null;
        if (type.IsConstructedGenericType)
            type = type.GetGenericTypeDefinition();
        return typesByKey.TryGetValue(Key(type), out var definition) ? definition : null;
    }

    // ---------- methods ----------

    public override long Address(MethodInfo method)
    {
        if (addresses.TryGetValue(method, out var known))
            return known;
        var found = 0L;
        if (Definition(method.DeclaringType) is { Methods: { } methods })
        {
            // By the metadata token the interop uses (whatever the interop names the method); by name and parameters else.
            if (Token(method) is var token && token != 0 && methods.Where(m => m.token == (uint)token).ToList() is [var byToken])
                return addresses[method] = (long)byToken.MethodPointer;
            var parameters = method.GetParameters();
            var named = methods.Where(m => m.Name == method.Name && m.parameterCount == parameters.Length).ToList();
            if (named.Count > 1)
                named = named.Where(m => m.Parameters.Select(p => Simple(p.Type)).SequenceEqual(parameters.Select(p => Simple(p.ParameterType)))).ToList();
            if (named.Count == 1)
                found = (long)named[0].MethodPointer;
        }
        return addresses[method] = found;
    }

    // A parameter type's simple name, the same for the interop's and the metadata's: Int32, Object, String[], List`1, T.
    static string Simple(Type type)
    {
        if (type.IsByRef)
            return Simple(type.GetElementType());
        if (type.IsGenericType && type.GetGenericTypeDefinition() is { } generic &&
            generic.Name is "Il2CppStructArray`1" or "Il2CppReferenceArray`1")
            return Simple(type.GetGenericArguments()[0]) + "[]";
        if (type.Name == "Il2CppStringArray")
            return "String[]";
        return type.IsArray ? Simple(type.GetElementType()) + "[]" : type.Name;
    }

    static string Simple(Il2CppTypeReflectionData type) =>
        type == null ? "?"
        : type.isArray ? Simple(type.arrayType) + "[]"
        : type.isType || type.isGenericType ? type.baseType?.Name ?? "?"
        : type.variableGenericParamName ?? "?";

    // ---------- reading ----------

    public override byte[] Read(long address, int length)
    {
        var bytes = new byte[length];
        if (LibCpp2IlMain.Binary.TryMapVirtualAddressToRaw((ulong)address, out var raw) && raw >= 0)
            Array.Copy(binary, raw, bytes, 0, (int)Math.Min(length, binary.Length - raw));
        return bytes;
    }

    // The metadata usage at the slot: the class IL2CPP puts there when the method first runs.
    public override Type ClassAt(long slot)
    {
        Il2CppTypeReflectionData type;
        try { type = LibCpp2IlMain.GetTypeGlobalByAddress((ulong)slot); }
        catch (Exception) { return null; }
        return type?.baseType is { } definition && interopTypes.TryGetValue(definition, out var found) ? found : null;
    }

    // ---------- fields ----------

    public override IEnumerable<(string name, int offset, bool isStatic, string type)> Fields(Type type)
    {
        for (var definition = Definition(type); definition != null; definition = definition.BaseType?.baseType)
        {
            var fields = definition.Fields ?? Array.Empty<Il2CppFieldDefinition>();
            var attributes = definition.FieldAttributes;
            for (var i = 0; i < fields.Length; i++)
            {
                var isStatic = (attributes[i] & FieldAttributes.Static) != 0;
                if ((attributes[i] & FieldAttributes.Literal) != 0)
                    continue;   // a constant has no place in memory
                var offset = LibCpp2IlMain.Binary.GetFieldOffsetFromIndex(definition.TypeIndex, i, fields[i].FieldIndex, definition.IsValueType, isStatic);
                yield return (fields[i].Name, offset, isStatic, TypeName(fields[i].FieldType));
            }
        }
    }

    // As il2cpp_type_get_name gives it: "System.Boolean".
    static string TypeName(Il2CppTypeReflectionData type) =>
        type?.baseType is { } definition && type.isType && !type.isArray ? definition.FullName : type?.ToString();
}
