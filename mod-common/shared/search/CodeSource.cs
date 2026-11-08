using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;

namespace OldenEra.Modding.Common;

// Where GameCode reads the game's machine code and the layout of its classes: the running game (LiveCode, in the
// plugins), or the game's files, GameAssembly.dll and global-metadata.dat (tools/game-names: the generated names are
// made at build time, with no game running; docs/game-names.md). The searches (Game*.cs) are the same for both.
internal abstract class CodeSource
{
    public static CodeSource Current { get; set; } = new LiveCode();

    // Start of the method's native code, or 0 (not a generated IL2CPP method, or shared generic code).
    public abstract long Address(MethodInfo method);

    // `length` bytes of the game's module at the address (code, constants).
    public abstract byte[] Read(long address, int length);

    // The class whose pointer the game's code loads from a global slot (mov reg,[rip+slot]): the slot of a class
    // reference in IL2CPP code. Null when the slot holds no known class (or, in the game, it is not set up yet).
    public abstract Type ClassAt(long slot);

    // The class's fields as the game's code sees them, its own and its bases': name, offset (in the object, or in the
    // class's static data), static, type (as "System.Boolean").
    public abstract IEnumerable<(string name, int offset, bool isStatic, string type)> Fields(Type type);

    // The game's own name of the field behind an interop property (BepInEx names it field_Public_Int32_0 when it renames
    // by signature, UnhollowerDeobfuscationRegex), or null when the property is not a field's. Worked out once each.
    readonly Dictionary<PropertyInfo, string> fieldNames = new();

    public string FieldName(PropertyInfo property)
    {
        if (property == null)
            return null;
        if (!fieldNames.TryGetValue(property, out var name))
            fieldNames[property] = name = FindFieldName(property);
        return name;
    }

    protected abstract string FindFieldName(PropertyInfo property);

    // The game's own name of an interop type or member ("eqe", "bhqa"), where BepInEx renamed it by its signature; the
    // interop's name else, null when not known. For the pins written with the game's names (GameNames.pins.txt).
    // A property here is a field's (its name); the sources tell a real property's by the game's property list.
    readonly Dictionary<MemberInfo, string> gameNames = new();

    public string GameName(MemberInfo member)
    {
        if (member == null)
            return null;
        if (!gameNames.TryGetValue(member, out var name))
            gameNames[member] = name = FindGameName(member);
        return name;
    }

    protected virtual string FindGameName(MemberInfo member) => member switch
    {
        Type type => type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "ObfuscatedNameAttribute")?.ConstructorArguments.FirstOrDefault().Value as string ?? type.Name,
        MethodInfo method => MethodName(method),
        PropertyInfo property => FieldName(property),
        _ => null,
    };

    // The game's own name of the method behind an interop method; null when not known.
    protected abstract string MethodName(MethodInfo method);

    // Whether an interop property gives what it says: a field's always; a real property's getter calls a game method
    // that returns the property's type. Not so where BepInEx's signature names clash: on 0.82.7 fgd's two properties
    // named Hero (Logic.Hero cqei, Data.Hero cwtl) both call cwtl's getter, and the Logic.Hero one would hand out a
    // Data.Hero. Worked out once each; true when it cannot be told.
    readonly Dictionary<PropertyInfo, bool> fits = new();

    public bool GetterFits(PropertyInfo property)
    {
        if (property == null)
            return false;
        if (!fits.TryGetValue(property, out var fit))
            fits[property] = fit = FieldName(property) != null || FindGetterFits(property);
        return fit;
    }

    protected abstract bool FindGetterFits(PropertyInfo property);
}

// The running game: IL2CPP's own data in memory.
internal sealed class LiveCode : CodeSource
{
    public override long Address(MethodInfo method)
    {
        var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if (field?.GetValue(null) is not IntPtr methodInfo || methodInfo == IntPtr.Zero)
            return 0;
        return Marshal.ReadIntPtr(methodInfo).ToInt64(); // Il2CppMethodInfo starts with the native code pointer
    }

    public override byte[] Read(long address, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(new IntPtr(address), bytes, 0, length);
        return bytes;
    }

    // The slot's value is only compared with the known classes' pointers, never followed: until a method first runs,
    // IL2CPP keeps an encoded token where it later puts the class, and taking that for a class crashes the game (05.11,
    // at the start before any encounter).
    Dictionary<IntPtr, Type> classes;

    public override Type ClassAt(long slot)
    {
        var klass = Marshal.ReadIntPtr(new IntPtr(slot));
        if (klass == IntPtr.Zero)
            return null;
        if (classes == null)
        {
            classes = new Dictionary<IntPtr, Type>();
            foreach (var t in Game.Types.Where(t => !t.IsGenericTypeDefinition))
            {
                try
                {
                    var pointer = (IntPtr)typeof(Il2CppClassPointerStore<>).MakeGenericType(t).GetField("NativeClassPtr").GetValue(null);
                    if (pointer != IntPtr.Zero)
                        classes[pointer] = t;
                }
                catch (Exception) { }
            }
        }
        return classes.TryGetValue(klass, out var type) ? type : null;
    }

    public override IEnumerable<(string name, int offset, bool isStatic, string type)> Fields(Type type)
    {
        var store = typeof(Il2CppClassPointerStore<>).MakeGenericType(type);
        for (var klass = (IntPtr)store.GetField("NativeClassPtr").GetValue(null); klass != IntPtr.Zero; klass = IL2CPP.il2cpp_class_get_parent(klass))
        {
            var iter = IntPtr.Zero;
            IntPtr field;
            while ((field = IL2CPP.il2cpp_class_get_fields(klass, ref iter)) != IntPtr.Zero)
            {
                var fieldType = IL2CPP.il2cpp_field_get_type(field);
                var isStatic = (IL2CPP.il2cpp_type_get_attrs(fieldType) & 0x10) != 0;   // FIELD_ATTRIBUTE_STATIC
                yield return (Marshal.PtrToStringAnsi(IL2CPP.il2cpp_field_get_name(field)), (int)IL2CPP.il2cpp_field_get_offset(field),
                              isStatic, Marshal.PtrToStringAnsi(IL2CPP.il2cpp_type_get_name(fieldType)));
            }
        }
    }

    // The interop method calls through a static NativeMethodInfoPtr_..., IL2CPP's own method: its name.
    static IntPtr MethodInfoOf(MethodInfo method)
    {
        try
        {
            return Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method)?.GetValue(null) is IntPtr info ? info : IntPtr.Zero;
        }
        catch (Exception) { return IntPtr.Zero; }
    }

    protected override string MethodName(MethodInfo method) =>
        MethodInfoOf(method) is var info && info != IntPtr.Zero ? Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(info)) : null;

    protected override bool FindGetterFits(PropertyInfo property)
    {
        if (property.GetMethod is not { } getter || MethodInfoOf(getter) is var info && info == IntPtr.Zero)
            return true;
        try
        {
            var returned = IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_method_get_return_type(info));
            var expected = (IntPtr)typeof(Il2CppClassPointerStore<>).MakeGenericType(property.PropertyType).GetField("NativeClassPtr").GetValue(null);
            return returned == IntPtr.Zero || expected == IntPtr.Zero || returned == expected;
        }
        catch (Exception) { return true; }
    }

    // A real property by IL2CPP's property of the class whose getter is the interop getter's method (the game's getters
    // are obfuscated too, not get_<name>).
    protected override string FindGameName(MemberInfo member)
    {
        if (member is not PropertyInfo property)
            return base.FindGameName(member);
        if (FieldName(property) is { } field)
            return field;
        if (property.GetMethod is not { } getter || MethodInfoOf(getter) is var info && info == IntPtr.Zero)
            return null;
        try
        {
            var klass = (IntPtr)typeof(Il2CppClassPointerStore<>).MakeGenericType(property.DeclaringType).GetField("NativeClassPtr").GetValue(null);
            var iter = IntPtr.Zero;
            IntPtr p;
            while (klass != IntPtr.Zero && (p = IL2CPP.il2cpp_class_get_properties(klass, ref iter)) != IntPtr.Zero)
                if (IL2CPP.il2cpp_property_get_get_method(p) == info)
                    return Marshal.PtrToStringAnsi(IL2CPP.il2cpp_property_get_name(p));
        }
        catch (Exception) { }
        return null;
    }

    // The interop getter reads the field through a static NativeFieldInfoPtr_..., IL2CPP's own field: its name.
    protected override string FindFieldName(PropertyInfo property)
    {
        if (property.GetMethod is not { } getter)
            return null;
        try
        {
            var pointer = Il2CppInteropUtils.GetIl2CppFieldInfoPointerFieldForGeneratedFieldAccessor(getter);
            return pointer?.GetValue(null) is IntPtr field && field != IntPtr.Zero ? Marshal.PtrToStringAnsi(IL2CPP.il2cpp_field_get_name(field)) : null;
        }
        catch (Exception) { return null; }
    }
}
