using System.Text;

namespace LibSaber.SM2.Assets.Scripting;

/// <summary>One VM instruction: low byte = opcode, upper 24 bits = signed operand (bias 0x800000).</summary>
public readonly record struct SslbOp(int Op, int Arg)
{
  /// <summary>Push int constant <see cref="Arg"/>.</summary>
  public const int PushInt = 0x03;
  /// <summary>Push bool (Arg 0/1).</summary>
  public const int PushBool = 0x04;
  /// <summary>Argument-count marker before a call (followed by <see cref="PushInt"/> n).</summary>
  public const int ArgCount = 0x07;
  /// <summary>Read member slot <see cref="Arg"/> of the object on the stack.</summary>
  public const int GetMember = 0x0a;
  /// <summary>Call method slot <see cref="Arg"/> of the receiver's class.</summary>
  public const int Call = 0x0e;

  public override string ToString() => $"{Op:x2} {Arg}";
}

/// <summary>A class member: field / method / constant. Slots are per class (inherited slots keep the base number).</summary>
public sealed record SslbMember(string Name, int Slot, int Kind, int? CodeIndex, object? Constant);

/// <summary>A class: members plus the declared field defaults (class field 10: { 1 slot, 2 value }).</summary>
public sealed record SslbClass(string Name, IReadOnlyList<SslbMember> Members, IReadOnlyDictionary<int, object?> Defaults)
{
  public SslbClass(string name, IReadOnlyList<SslbMember> members) : this(name, members, new Dictionary<int, object?>()) { }

  public SslbMember? Member(string name) => Members.FirstOrDefault(m => m.Name == name);
}

public sealed record SslbCode(int Id, string Name, IReadOnlyList<string> Args, IReadOnlyList<SslbOp> Ops);

/// <summary>
///   datapars (2026-10-29): reader for SM2 compiled ssl scripts (<c>*_release.sslbin</c>, magic <c>SSLB</c>).
///   Layout (INFERRED from the files, every field checked against the text .ssl/.prop names it compiles):
///   16-byte header (SSLB, u16 7, u16 3, 8 bytes hash), then a protobuf message:
///   field 6 = string table; field 8 = class { 1 name, 4 member { 1 name, 2 slot, 3 kind (2 method, 6 field, 7 const),
///   5 code index, 6 const VALUE }, 10 field default { 1 slot, 2 VALUE } };
///   VALUE = { 1 type: 1 int (2 varint, -1 as 64-bit), 2 bool (3), 3 float (4 fixed32), 4 string (5 string index), 8 null };
///   field 9 = code { 1 id, 2 signature { 1 name, 2 arg { 1 name } }, 5 { repeated 1 = instruction varint } }.
///   A class's code index i is the i-th field 9 entry of the file.
/// </summary>
public sealed class Sslb
{
  public List<string> Strings { get; } = new();
  public List<SslbClass> Classes { get; } = new();
  public List<SslbCode> Codes { get; } = new();
  public string Module => Strings.Count > 0 ? Strings[0] : "";

  public static bool IsSslb(ReadOnlySpan<byte> data) => data.Length >= 16 && data[0] == (byte)'S' && data[1] == (byte)'S' && data[2] == (byte)'L' && data[3] == (byte)'B';

  public static Sslb Parse(byte[] data)
  {
    if (!IsSslb(data))
      throw new InvalidDataException("not an SSLB script");
    var top = Fields(data, 16, data.Length);
    var s = new Sslb();
    foreach (var f in top.Where(f => f.Number == 6))
      s.Strings.Add(Encoding.UTF8.GetString(data, f.Start, f.Length));
    string Str(ulong i) => i < (ulong)s.Strings.Count ? s.Strings[(int)i] : $"#{i}";
    foreach (var f in top.Where(f => f.Number == 9))
    {
      var m = Fields(data, f.Start, f.Start + f.Length);
      var sig = m.FirstOrDefault(x => x.Number == 2);
      string name = "";
      var args = new List<string>();
      if (sig.Length > 0)
      {
        var sm = Fields(data, sig.Start, sig.Start + sig.Length);
        name = Str(sm.FirstOrDefault(x => x.Number == 1).Value);
        foreach (var a in sm.Where(x => x.Number == 2))
          args.Add(Str(Fields(data, a.Start, a.Start + a.Length).FirstOrDefault(x => x.Number == 1).Value));
      }

      var ops = new List<SslbOp>();
      foreach (var body in m.Where(x => x.Number == 5 && x.Wire == 2))
      {
        foreach (var ins in Fields(data, body.Start, body.Start + body.Length).Where(x => x.Number == 1 && x.Wire == 0))
          ops.Add(new SslbOp((int)(ins.Value & 0xff), (int)((ins.Value >> 8) & 0xffffff) - 0x800000));
      }

      s.Codes.Add(new SslbCode((int)m.FirstOrDefault(x => x.Number == 1).Value, name, args, ops));
    }

    foreach (var f in top.Where(f => f.Number == 8))
    {
      var c = Fields(data, f.Start, f.Start + f.Length);
      var members = new List<SslbMember>();
      foreach (var mf in c.Where(x => x.Number == 4 && x.Wire == 2))
      {
        var mm = Fields(data, mf.Start, mf.Start + mf.Length);
        var cf = mm.FirstOrDefault(x => x.Number == 6 && x.Wire == 2);
        var constant = cf.Length > 0 ? Value(data, cf, Str) : null;
        var code = mm.Where(x => x.Number == 5 && x.Wire == 0).Select(x => (int?)x.Value).FirstOrDefault();
        members.Add(new SslbMember(Str(mm.FirstOrDefault(x => x.Number == 1).Value), (int)mm.FirstOrDefault(x => x.Number == 2).Value,
          (int)mm.FirstOrDefault(x => x.Number == 3).Value, code, constant));
      }

      var defaults = new Dictionary<int, object?>();
      foreach (var df in c.Where(x => x.Number == 10 && x.Wire == 2))
      {
        var d = Fields(data, df.Start, df.Start + df.Length);
        var vf = d.FirstOrDefault(x => x.Number == 2 && x.Wire == 2);
        defaults[(int)d.FirstOrDefault(x => x.Number == 1).Value] = vf.Length > 0 ? Value(data, vf, Str) : null;
      }

      s.Classes.Add(new SslbClass(Str(c.FirstOrDefault(x => x.Number == 1).Value), members, defaults));
    }

    return s;
  }

  public SslbClass? Class(string name) => Classes.FirstOrDefault(c => c.Name == name);

  /// <summary>Declared default of a field (by name); null when the class declares none.</summary>
  public object? FieldDefault(SslbClass cls, string field)
    => cls.Member(field) is { } m && cls.Defaults.TryGetValue(m.Slot, out var v) ? v : null;

  private static object? Value(byte[] data, Field f, Func<ulong, string> str)
  {
    var k = Fields(data, f.Start, f.Start + f.Length);
    return k.FirstOrDefault(x => x.Number == 1).Value switch
    {
      1 => (long)k.FirstOrDefault(x => x.Number == 2).Value,
      2 => k.FirstOrDefault(x => x.Number == 3).Value != 0,
      3 => k.FirstOrDefault(x => x.Number == 4 && x.Wire == 5) is { Length: 4 } fl ? (object)BitConverter.Int32BitsToSingle((int)(uint)fl.Value) : null,
      4 => str(k.FirstOrDefault(x => x.Number == 5).Value),
      _ => null,
    };
  }

  /// <summary>Code of a method member (by its code index; falls back to the signature name).</summary>
  public SslbCode? Code(SslbClass cls, string method)
  {
    var m = cls.Member(method);
    if (m?.CodeIndex is { } i && i >= 0 && i < Codes.Count && Codes[i].Name == method)
      return Codes[i];
    return Codes.FirstOrDefault(c => c.Name == method);
  }

  /// <summary>Text listing (one method per block) for docs / debugging.</summary>
  public string Disassemble()
  {
    var sb = new StringBuilder();
    sb.AppendLine("module " + Module);
    foreach (var c in Classes)
    {
      sb.AppendLine("class " + c.Name);
      foreach (var m in c.Members)
        sb.AppendLine($"  [{m.Slot}] kind{m.Kind} {m.Name}{(m.Constant is null ? "" : " = " + m.Constant)}{(c.Defaults.TryGetValue(m.Slot, out var dv) ? " default " + (dv ?? "null") : "")}{(m.CodeIndex is { } ci ? " code#" + ci : "")}");
    }

    foreach (var code in Codes)
    {
      sb.AppendLine($"code {code.Id} {code.Name}({string.Join(", ", code.Args)})");
      foreach (var op in code.Ops)
        sb.AppendLine("    " + op);
    }

    return sb.ToString();
  }

  private readonly record struct Field(int Number, int Wire, ulong Value, int Start, int Length);

  private static List<Field> Fields(byte[] b, int start, int end)
  {
    var list = new List<Field>();
    var i = start;
    while (i < end)
    {
      var key = Varint(b, ref i);
      var number = (int)(key >> 3);
      var wire = (int)(key & 7);
      switch (wire)
      {
        case 0:
          list.Add(new Field(number, 0, Varint(b, ref i), 0, 0));
          break;
        case 2:
          var len = (int)Varint(b, ref i);
          if (len < 0 || i + len > end)
            throw new InvalidDataException("SSLB: field runs past its message");
          list.Add(new Field(number, 2, 0, i, len));
          i += len;
          break;
        case 5:
          list.Add(new Field(number, 5, BitConverter.ToUInt32(b, i), i, 4));
          i += 4;
          break;
        case 1:
          list.Add(new Field(number, 1, BitConverter.ToUInt64(b, i), i, 8));
          i += 8;
          break;
        default:
          throw new InvalidDataException($"SSLB: wire type {wire}");
      }
    }

    return list;
  }

  private static ulong Varint(byte[] b, ref int i)
  {
    ulong r = 0;
    for (var shift = 0; shift < 64; shift += 7)
    {
      var c = b[i++];
      r |= (ulong)(c & 0x7f) << shift;
      if (c < 0x80)
        return r;
    }

    throw new InvalidDataException("SSLB: bad varint");
  }
}
