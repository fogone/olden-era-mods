using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using OldenEra.Modding.Common;

namespace OldenEra.Modding.Research;

// The research console's tools for reading the game's code (GameCode has what the searches need): a method as
// instructions, the method in a class's virtual slot, a hidden string constant.
internal static class CodeResearch
{
    // The method's code as instructions (Iced), offsets from its start, calls and jumps to methods by name,
    // rip-relative operands by their offset in the module.
    public static IEnumerable<string> Disassemble(MethodInfo method)
    {
        var start = (ulong)Game.NativeAddress(method).ToInt64();
        var code = GameCode.Bytes(method);
        var decoder = Iced.Intel.Decoder.Create(64, new Iced.Intel.ByteArrayCodeReader(code));
        decoder.IP = start;
        var formatter = new Iced.Intel.IntelFormatter();
        var output = new Iced.Intel.StringOutput();
        while (decoder.IP < start + (ulong)code.Length)
        {
            var instruction = decoder.Decode();
            if (instruction.Code == Iced.Intel.Code.Int3)
                continue;
            formatter.Format(instruction, output);
            var note = instruction.IsCallNear || instruction.IsJmpNear ? $"   ; {GameCode.NameAt((long)instruction.NearBranchTarget)}"
                     : instruction.IsJccShort || instruction.IsJccNear || instruction.IsJmpShort ? $"   ; -> +{instruction.NearBranchTarget - start:X}"
                     : "";
            yield return $"{instruction.IP - start:X4}  {output.ToStringAndReset()}{note}";
        }
    }

    // The method's code in the log as instructions (Disassemble).
    public static void DumpDisassembly(MethodInfo method, Action<string> log) =>
        log($"CODE {GameCode.Signature(method)} @0x{Game.NativeAddress(method).ToInt64():X}:\n  " + string.Join("\n  ", Disassemble(method)));

    // The method a class has in a virtual slot: code that calls [klass+offset] (the method info is at offset + 8).
    public static string VirtualSlot(Type type, int offset)
    {
        var store = typeof(Il2CppInterop.Runtime.Il2CppClassPointerStore<>).MakeGenericType(type);
        var klass = (IntPtr)store.GetField("NativeClassPtr").GetValue(null);
        IL2CPP.il2cpp_runtime_class_init(klass);
        // A virtual slot is {code, method info} in the virtual table after the class's own data; anything else at the
        // offset (the class's header, a field) is no method, and reading it as one crashes the game (05.11: slot fhf 0x0).
        if (offset < 0x100)
            return $"{type.Name}+0x{offset:X}: not a virtual slot (the table starts after the class's data: use the offset the code calls, e.g. call [rax+1C8h])";
        var code = Marshal.ReadIntPtr(klass + offset);
        if (code == IntPtr.Zero)
            return $"{type.Name}+0x{offset:X}: empty (the class's virtual table is set up when its first object is made: try once the map is loaded)";
        // Only a known method's code: then the next pointer is its method info.
        if (GameCode.At(code.ToInt64()).Count == 0)
            return $"{type.Name}+0x{offset:X}: 0x{code.ToInt64():X} is no method's start: not a virtual slot";
        var info = Marshal.ReadIntPtr(klass + offset + 8);
        return $"{type.Name}+0x{offset:X}: {(info != IntPtr.Zero ? Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(info)) : "?")} = {GameCode.NameAt(code.ToInt64())}";
    }

    // A string constant of the game's code: the obfuscator keeps them behind static string M() of
    // <PrivateImplementationDetails>.a (a.qfz() = "tooltipVisited" on 0.82.7); null when there is no such method.
    public static string HiddenString(string method) =>
        Game.Types.FirstOrDefault(t => t.Name == "a" && t.FullName.StartsWith("<PrivateImplementationDetails>"))
            ?.GetMethod(method, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)?.Invoke(null, null) as string;

    // All the hidden strings (`strings <regex>`: which method gives a text, then `callers a\.<method>` for who uses it).
    // The game calls these itself wherever it needs the text; they only build the string.
    static List<(string method, string value)> hiddenStrings;

    public static List<(string method, string value)> HiddenStrings() => hiddenStrings ??=
        (Game.Types.FirstOrDefault(t => t.Name == "a" && t.FullName.StartsWith("<PrivateImplementationDetails>"))
            ?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(string) && m.GetParameters().Length == 0)
            .Select(m => (m.Name, m.Invoke(null, null) as string ?? ""))
            .ToList()) ?? new();


}
