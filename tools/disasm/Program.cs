using Iced.Intel;
using System.Globalization;
// args: log file, method signature prefix after "BYTES "
var lines = File.ReadAllLines(args[0]);
var i = Array.FindIndex(lines, l => l.Contains("BYTES " + args[1]));
var head = lines[i];
var addr = ulong.Parse(head.Substring(head.IndexOf("@0x") + 3).TrimEnd(':'), NumberStyles.HexNumber);
var bytes = new List<byte>();
for (var j = i + 1; j < lines.Length && lines[j].Length > 8 && lines[j].StartsWith("  ") && lines[j][6] == ':'; j++)
    foreach (var h in lines[j].Substring(8).Trim().Split(' ')) bytes.Add(byte.Parse(h, NumberStyles.HexNumber));
var dec = Decoder.Create(64, new ByteArrayCodeReader(bytes.ToArray()));
dec.IP = addr;
var fmt = new IntelFormatter();
var outp = new StringOutput();
while (dec.IP < addr + (ulong)bytes.Count)
{
    var ins = dec.Decode();
    fmt.Format(ins, outp);
    var extra = ins.IsIPRelativeMemoryOperand ? $"   ; [rip]={ins.IPRelativeMemoryAddress - addr:X}" : "";
    var br = ins.IsCallNear || ins.IsJmpNear || ins.IsJccNear || ins.IsJccShort || ins.IsJmpShort ? $"   ; -> {ins.NearBranchTarget - addr:X}" : "";
    Console.WriteLine($"{ins.IP - addr:X4}  {outp.ToStringAndReset()}{extra}{br}");
}
