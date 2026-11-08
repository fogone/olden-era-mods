using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OldenEra.Modding.Common;

// The game's machine code, for the searches: which methods share one native code, what a method calls and reads.
// Nothing here runs unless called. The research console's own tools (disassembly, virtual slots, hidden strings) are in
// mod-research (CodeResearch).
//
//   GameCode.SharedWith(method)                       // other methods with the same native code (a patch hits them all)
//   GameCode.Calls(method), GameCode.Offsets(method)  // its direct calls; the offsets it reads at
//   GameCode.CallersOf(name => name.StartsWith("Shader.SetGlobal"))   // who calls these, by reading every method's code
internal static class GameCode
{
    const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // Native code starts of the game's, Unity's and Il2CppSystem's methods: a sorted table and every name per address.
    // Built once, on first use (a few seconds).
    sealed class Table
    {
        public long[] Starts;
        public string[] Names;          // the first name per address
        public Dictionary<long, List<MethodInfo>> All;
    }

    static Table table;

    static Table Methods()
    {
        if (table != null)
            return table;
        var all = new Dictionary<long, List<MethodInfo>>();
        var assemblies = new[] { typeof(Hex.UI.Tooltips.BhTooltip).Assembly, typeof(UnityEngine.Object).Assembly, typeof(UnityEngine.Camera).Assembly,
                                 typeof(UnityEngine.Rendering.CommandBuffer).Assembly, typeof(UnityEngine.RectTransformUtility).Assembly,
                                 typeof(UnityEngine.Canvas).Assembly, typeof(TMPro.TMP_Text).Assembly, typeof(Il2CppSystem.String).Assembly };
        foreach (var method in assemblies.Distinct().SelectMany(Game.SafeTypes).SelectMany(t => t.GetMethods(Declared)))
        {
            if (method.IsGenericMethodDefinition)
                continue;
            long address;
            try { address = Game.NativeAddress(method).ToInt64(); }
            catch (Exception) { continue; }   // not a generated IL2CPP method
            if (address == 0)
                continue;
            if (!all.TryGetValue(address, out var list))
                all[address] = list = new List<MethodInfo>();
            list.Add(method);
        }
        var starts = all.Keys.OrderBy(a => a).ToArray();
        table = new Table { Starts = starts, Names = starts.Select(a => Signature(all[a][0])).ToArray(), All = all };
        Game.Log?.Invoke($"Code: {starts.Length} native codes of {all.Values.Sum(l => l.Count)} methods");
        return table;
    }

    public static string Signature(MethodInfo m) =>
        $"{m.DeclaringType?.Name}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})";

    // The methods whose native code starts at the address (several when folded); none when the address starts none.
    public static IReadOnlyList<MethodInfo> At(long address) =>
        Methods().All.TryGetValue(address, out var list) ? list : Array.Empty<MethodInfo>();

    // The name of what starts at the address, or the address.
    public static string NameAt(long address) => At(address) is { Count: > 0 } found ? Signature(found[0]) : $"0x{address:X}";

    // The other methods with the same native code as this one: a patch on it runs for all of them.
    public static List<MethodInfo> SharedWith(MethodInfo method) =>
        At(Game.NativeAddress(method).ToInt64()).Where(m => m != method).ToList();

    // The method's code: from its start to the next method's start (at most `max` bytes).
    public static byte[] Bytes(MethodInfo method, int max = 0x3000)
    {
        var t = Methods();
        var start = Game.NativeAddress(method).ToInt64();
        var index = Array.BinarySearch(t.Starts, start);
        if (start == 0 || index < 0)
            return Array.Empty<byte>();
        var length = (int)Math.Min(index + 1 < t.Starts.Length ? t.Starts[index + 1] - start : max, max);
        return CodeSource.Current.Read(start, (length + 15) / 16 * 16);
    }

    // The direct calls and jumps of the method's code to known method starts: (offset, target, is a jump).
    public static IEnumerable<(int offset, long target, bool jump)> Calls(MethodInfo method)
    {
        var start = Game.NativeAddress(method).ToInt64();
        var code = Bytes(method);
        var t = Methods();
        for (var i = 0; i + 5 <= code.Length; i++)
        {
            if (code[i] != 0xE8 && code[i] != 0xE9)
                continue;
            var target = start + i + 5 + BitConverter.ToInt32(code, i + 1);
            if (t.All.ContainsKey(target))
                yield return (i, target, code[i] == 0xE9);
        }
    }

    // The game's methods calling (directly) one of the methods whose signature matches: every game method's code read.
    public static IEnumerable<(MethodInfo caller, int offset, string target)> CallersOf(Func<string, bool> targetSignature)
    {
        var targets = Methods().All.Where(p => p.Value.Any(m => targetSignature(Signature(m)))).Select(p => p.Key).ToHashSet();
        foreach (var caller in Game.Types.SelectMany(t => t.GetMethods(Declared)).Where(m => !m.IsGenericMethodDefinition))
        {
            List<(int offset, long target, bool jump)> calls;
            try { calls = At(Game.NativeAddress(caller).ToInt64()).FirstOrDefault() == caller ? Calls(caller).ToList() : null; }
            catch (Exception) { continue; }
            if (calls == null)
                continue;
            foreach (var call in calls.Where(c => targets.Contains(c.target)))
                yield return (caller, call.offset, NameAt(call.target));
        }
    }

    // The class's fields as the game's code sees them, its own and its bases': name, offset (in the object, or in the
    // class's static data), static, type. Code reading [obj+0xB8] reads the field at offset 0xB8 here; the interop type has
    // a property of the same name.
    public static IEnumerable<(string name, int offset, bool isStatic, string type)> Fields(Type type) => CodeSource.Current.Fields(type);

    static IEnumerable<Iced.Intel.Instruction> Instructions(MethodInfo method)
    {
        var start = (ulong)Game.NativeAddress(method).ToInt64();
        var code = Bytes(method);
        var decoder = Iced.Intel.Decoder.Create(64, new Iced.Intel.ByteArrayCodeReader(code));
        decoder.IP = start;
        while (decoder.IP < start + (ulong)code.Length)
            yield return decoder.Decode();
    }

    static bool ReadsMemory(Iced.Intel.Instruction i) =>
        Enumerable.Range(0, i.OpCount).Any(n => i.GetOpKind(n) == Iced.Intel.OpKind.Memory);

    // The offsets the method's code reads or writes at, [reg+offset] (not the stack's): which fields of its objects it
    // touches. A field the code reads has its offset here (GameCode.Fields gives the offsets of a class's fields).
    public static HashSet<int> Offsets(MethodInfo method) =>
        Instructions(method).Where(i => ReadsMemory(i) && i.MemoryBase is not (Iced.Intel.Register.RSP or Iced.Intel.Register.RIP or Iced.Intel.Register.None))
            .Select(i => (int)i.MemoryDisplacement64).ToHashSet();

    // The offsets of the 4-byte values the method's code reads from an object it has just loaded from a field at
    // `holderOffset` (mov r64,[x+holder] ... mov r32,[r64+offset]): which int fields of that held object it reads. The
    // register counts as holding the object till it is written again.
    public static HashSet<int> IntReadsThrough(MethodInfo method, int holderOffset)
    {
        var found = new HashSet<int>();
        var held = new HashSet<Iced.Intel.Register>();
        foreach (var i in Instructions(method))
        {
            var target = i.OpCount > 0 && i.Op0Kind == Iced.Intel.OpKind.Register ? i.Op0Register : Iced.Intel.Register.None;
            if (i.Mnemonic == Iced.Intel.Mnemonic.Mov && ReadsMemory(i) && held.Contains(i.MemoryBase) &&
                target != Iced.Intel.Register.None && Iced.Intel.RegisterExtensions.GetSize(target) == 4)
                found.Add((int)i.MemoryDisplacement64);
            // test and cmp only read their registers.
            if (target == Iced.Intel.Register.None || i.Mnemonic is Iced.Intel.Mnemonic.Test or Iced.Intel.Mnemonic.Cmp)
                continue;
            var full = Iced.Intel.RegisterExtensions.GetFullRegister(target);
            if (i.Mnemonic == Iced.Intel.Mnemonic.Mov && Iced.Intel.RegisterExtensions.GetSize(target) == 8 && ReadsMemory(i) &&
                i.MemoryBase is not (Iced.Intel.Register.RSP or Iced.Intel.Register.RIP or Iced.Intel.Register.None) &&
                (int)i.MemoryDisplacement64 == holderOffset)
                held.Add(full);
            else
                held.Remove(full);
        }
        return found;
    }

    // The float constants the method's code loads (movss xmm, [rip+...]).
    public static List<float> Floats(MethodInfo method) =>
        Instructions(method).Where(i => i.Mnemonic == Iced.Intel.Mnemonic.Movss && i.IsIPRelativeMemoryOperand)
            .Select(i => BitConverter.ToSingle(CodeSource.Current.Read((long)i.IPRelativeMemoryAddress, 4), 0)).ToList();

    // The static fields of the game's classes the method's code reads: (class, offset in its static data). IL2CPP code
    // reads one as  mov rA,[rip+X]  (X holds the class)  mov rB,[rA+0B8h]  (the class's static data)  ... [rB+offset].
    const int StaticDataOffset = 0xB8;   // Il2CppClass.static_fields on this Unity

    public static List<(Type type, int offset)> StaticReads(MethodInfo method)
    {
        var found = new List<(Type, int)>();
        var classIn = new Dictionary<Iced.Intel.Register, Type>();
        var staticsIn = new Dictionary<Iced.Intel.Register, Type>();
        foreach (var i in Instructions(method))
        {
            // Only a class known to the interop is followed to its static data (ClassAt: a slot not yet set up is none).
            if (ReadsMemory(i) && staticsIn.TryGetValue(i.MemoryBase, out var holder))
                found.Add((holder, (int)i.MemoryDisplacement64));
            var target = i.OpCount > 0 && i.GetOpKind(0) == Iced.Intel.OpKind.Register ? i.GetOpRegister(0) : Iced.Intel.Register.None;
            var isLoad = i.Mnemonic == Iced.Intel.Mnemonic.Mov && target != Iced.Intel.Register.None && i.OpCount == 2 && i.GetOpKind(1) == Iced.Intel.OpKind.Memory;
            var klass = isLoad && i.IsIPRelativeMemoryOperand ? CodeSource.Current.ClassAt((long)i.IPRelativeMemoryAddress) : null;
            var statics = isLoad && i.MemoryDisplacement64 == StaticDataOffset && classIn.TryGetValue(i.MemoryBase, out var c) ? c : null;
            if (i.IsCallNear || i.IsCallNearIndirect)
            {
                foreach (var r in new[] { Iced.Intel.Register.RAX, Iced.Intel.Register.RCX, Iced.Intel.Register.RDX, Iced.Intel.Register.R8,
                                          Iced.Intel.Register.R9, Iced.Intel.Register.R10, Iced.Intel.Register.R11 })
                {
                    classIn.Remove(r);
                    staticsIn.Remove(r);
                }
                continue;
            }
            if (target == Iced.Intel.Register.None)
                continue;
            classIn.Remove(target);
            staticsIn.Remove(target);
            if (klass != null)
                classIn[target] = klass;
            if (statics != null)
                staticsIn[target] = statics;
        }
        return found;
    }

    // The interop property of a field of the class (Fields), by the game's field name behind it (CodeSource.FieldName:
    // with BepInEx's signature names the property is field_Public_Int32_0; else it has the field's name, an
    // auto-property's <x>k__BackingField as _x_k__BackingField).
    public static PropertyInfo PropertyOf(Type type, string field)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        for (var t = type; t != null; t = t.BaseType)
            foreach (var p in t.GetProperties(all | BindingFlags.DeclaredOnly))
                if (CodeSource.Current.FieldName(p) == field)
                    return p;
        return type.GetProperty(field.Replace('<', '_').Replace('>', '_'), all);
    }

    // The field behind an interop property: by the game's name of it (CodeSource.FieldName), else by the property's own
    // name (its name, an auto-property's <x>k__BackingField, or that mangled).
    public static (string name, int offset, bool isStatic, string type)? FieldOf(PropertyInfo p)
    {
        if (p == null)
            return null;
        var name = CodeSource.Current.GameName(p) ?? p.Name;
        return Fields(p.DeclaringType).Where(f => f.name == name || f.name == $"<{name}>k__BackingField" || f.name.Replace('<', '_').Replace('>', '_') == name)
            .Select(f => ((string, int, bool, string)?)f).FirstOrDefault();
    }

}
