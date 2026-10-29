using System.Text;
using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Scripting;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>datapars: SSLB reader + the power fist hand rule read from script bytecode (synthetic files; retail via LIBSABER_SM2_SSL).</summary>
public class PowerFistHandRuleTests
{
  // --- minimal protobuf writer for synthetic SSLB files ---
  private sealed class Pb
  {
    private readonly List<byte> _b = new();
    private static void V(List<byte> b, ulong v) { while (v >= 0x80) { b.Add((byte)(v | 0x80)); v >>= 7; } b.Add((byte)v); }
    public Pb I(int f, ulong v) { V(_b, (ulong)(f << 3)); V(_b, v); return this; }
    public Pb M(int f, Pb m) => Bytes(f, m.ToArray());
    public Pb Fixed32(int f, int v) { V(_b, (ulong)((f << 3) | 5)); _b.AddRange(BitConverter.GetBytes(v)); return this; }
    public Pb S(int f, string s) => Bytes(f, Encoding.UTF8.GetBytes(s));
    private Pb Bytes(int f, byte[] d) { V(_b, (ulong)((f << 3) | 2)); V(_b, (ulong)d.Length); _b.AddRange(d); return this; }
    public byte[] ToArray() => _b.ToArray();
  }

  private static ulong Ins(int op, int arg) => (ulong)(uint)(op | ((arg + 0x800000) << 8));

  private static Pb Code(int id, string[] strs, string name, params (int op, int arg)[] ops)
  {
    var body = new Pb();
    foreach (var (op, arg) in ops)
      body.I(1, Ins(op, arg));
    return new Pb().I(1, (ulong)id).M(2, new Pb().I(1, (ulong)Array.IndexOf(strs, name))).M(5, body);
  }

  private static byte[] File(string[] strs, Pb[] classes, Pb[] codes)
  {
    var top = new Pb();
    foreach (var s in strs)
      top.S(6, s);
    foreach (var c in classes)
      top.M(8, c);
    foreach (var c in codes)
      top.M(9, c);
    return Encoding.ASCII.GetBytes("SSLB").Concat(new byte[12]).Concat(top.ToArray()).ToArray();
  }

  private static Pb Member(string[] strs, string name, int slot, int kind, int? code = null, long? intConst = null)
  {
    var m = new Pb().I(1, (ulong)Array.IndexOf(strs, name)).I(2, (ulong)slot).I(3, (ulong)kind);
    if (code is { } c)
      m.I(5, (ulong)c);
    if (intConst is { } v)
      m.M(6, new Pb().I(1, 1).I(2, (ulong)v));
    return m;
  }

  /// <summary>Same shape as the retail file; <paramref name="section"/> is the int the hand rule passes.</summary>
  private static (byte[] Base, byte[] State, byte[] Enum) Scripts(int section)
  {
    var bs = new[] { "m", "PropPowerFistCustomizerBase", "visibilityUnchangableMeshes", "SetRightHandVisibility", "FilterRightHandMeshes" };
    var baseFile = File(bs,
      new[] { new Pb().I(1, 1).M(4, Member(bs, "visibilityUnchangableMeshes", 52, 6)).M(4, Member(bs, "SetRightHandVisibility", 58, 2, 0)).M(4, Member(bs, "FilterRightHandMeshes", 64, 2, 1)) },
      new[]
      {
        // propCustomizerState(54).GetMeshesByBodySection(section) -> FilterRightHandMeshes -> handRegion.SetMeshes -> Show/Hide
        Code(3, bs, "SetRightHandVisibility", (0x00, 2), (0x06, 0), (0x0a, 54), (0x03, section), (0x07, 1), (0x03, 1), (0x0e, 82), (0x46, -1), (0x06, 0), (0x06, -1), (0x07, 1), (0x03, 1), (0x0e, 64), (0x46, -2)),
        Code(4, bs, "FilterRightHandMeshes", (0x06, 0), (0x0a, 52), (0x15, 2)),
      });
    var ss = new[] { "m", "PropCharacterCustomizationStateBase", "GetMeshesByBodySection" };
    var state = File(ss, new[] { new Pb().I(1, 1).M(4, Member(ss, "GetMeshesByBodySection", 82, 2, 0)) }, Array.Empty<Pb>());
    var es = new[] { "m", "CharacterBodySection", "HELMET", "CHEST", "RIGHT_GAUNTLET" };
    var en = File(es, new[] { new Pb().I(1, 1).M(4, Member(es, "HELMET", 5, 7, intConst: 0)).M(4, Member(es, "CHEST", 6, 7, intConst: 2)).M(4, Member(es, "RIGHT_GAUNTLET", 7, 7, intConst: 6)) }, Array.Empty<Pb>());
    return (baseFile, state, en);
  }

  private const string Prop = "powerFistIds = [ \"powerfist_01_grp\", \"ch_powerfist_01_grp\" ]\nvisibilityUnchangableMeshes = [ \"torso_r_gauntlet\" ]\n__type = \"prop_base\"";

  [Fact]
  public void Sslb_reads_strings_members_constants_and_ops()
  {
    var (_, _, en) = Scripts(6);
    var s = Sslb.Parse(en);
    Assert.Equal("m", s.Module);
    var c = Assert.Single(s.Classes);
    Assert.Equal("CharacterBodySection", c.Name);
    Assert.Equal(6L, c.Member("RIGHT_GAUNTLET")!.Constant);
    var (b, _, _) = Scripts(6);
    var code = Sslb.Parse(b).Codes[0];
    Assert.Equal("SetRightHandVisibility", code.Name);
    Assert.Equal(new SslbOp(0x03, 6), code.Ops[3]);
    Assert.Equal(new SslbOp(0x46, -1), code.Ops[7]);
  }

  [Theory]
  [InlineData(6, "RIGHT_GAUNTLET")]
  [InlineData(2, "CHEST")]
  public void Section_comes_from_the_bytecode_not_a_name(int value, string expected)
  {
    var (b, st, en) = Scripts(value);
    var rule = PowerFistHandRule.FromScripts(b, st, en, Prop);
    Assert.True(rule.Proven, rule.Evidence);
    Assert.Equal(expected, rule.Section);
    Assert.Equal(new[] { "torso_r_gauntlet" }, rule.Keep);
    Assert.Equal(new[] { "powerfist_01_grp", "ch_powerfist_01_grp" }, rule.FistIds);
  }

  [Fact]
  public void Missing_scripts_fall_back_labelled_heuristic()
  {
    var rule = PowerFistHandRule.FromScripts(null, null, null, Prop);
    Assert.False(rule.Proven);
    Assert.StartsWith("HEURISTIC", rule.Evidence);
    Assert.Equal("RIGHT_GAUNTLET", rule.Section);
    Assert.Equal(new[] { "torso_r_gauntlet" }, rule.Keep);
    var (b, st, _) = Scripts(6);
    Assert.False(PowerFistHandRule.FromScripts(b, st, new byte[] { 1, 2, 3 }, Prop).Proven); // bad enum file -> fallback, no throw
  }

  [Fact]
  public void Power_fist_request_carries_rule_and_fist_ids()
  {
    var (b, st, en) = Scripts(2);
    var melee = new MeleeCustomization { HandRule = PowerFistHandRule.FromScripts(b, st, en, Prop) };
    var request = VariantRequest.Parse("powerfist=01");
    melee.AddPowerFist(request, "cc_sm_constructor_tank"); // unknown skin in this empty library: note, nothing set
    Assert.Null(request.HideHandSection);
    Assert.Equal("CHEST", melee.HandSection);
  }

  /// <summary>Retail sslbin when LIBSABER_SM2_SSL points at an extracted ssl/ folder.</summary>
  [Fact]
  public void Retail_power_fist_hides_right_gauntlet_by_data()
  {
    var root = Environment.GetEnvironmentVariable("LIBSABER_SM2_SSL");
    string P(string rel) => Path.Combine(root ?? "", rel["ssl/".Length..]);
    if (string.IsNullOrWhiteSpace(root) || !System.IO.File.Exists(P(PowerFistHandRule.BasePath)))
      return;
    var rule = PowerFistHandRule.FromScripts(System.IO.File.ReadAllBytes(P(PowerFistHandRule.BasePath)), System.IO.File.ReadAllBytes(P(PowerFistHandRule.StatePath)),
      System.IO.File.ReadAllBytes(P(PowerFistHandRule.SectionEnumPath)), System.IO.File.ReadAllText(P(PowerFistHandRule.PropPath)));
    Assert.True(rule.Proven, rule.Evidence);
    Assert.Equal("RIGHT_GAUNTLET", rule.Section);
    Assert.Equal(6, rule.SectionValue);
    Assert.Equal(new[] { "torso_r_gauntlet", "upper_arm_librarium_T", "upper_arm_librarium_G" }, rule.Keep);
    Assert.Equal(9, rule.FistIds.Count);
  }

  [Fact]
  public void Field_defaults_come_from_class_field_10()
  {
    var strs = new[] { "m", "InstConstructorMeshTemplateVariant", "probability", "template" };
    var cls = new Pb().I(1, 1).M(4, Member(strs, "probability", 5, 6)).M(4, Member(strs, "template", 6, 6))
      .M(10, new Pb().I(1, 5).M(2, new Pb().I(1, 3).Fixed32(4, BitConverter.SingleToInt32Bits(0.5f))));
    var bytes = File(strs, new[] { cls }, Array.Empty<Pb>());
    var s = Sslb.Parse(bytes);
    Assert.Equal(0.5f, s.FieldDefault(s.Classes[0], "probability"));
    Assert.Null(s.FieldDefault(s.Classes[0], "template"));

    var lib = InstConstructorLibrary.FromText(LayerTemplates, LayerMeshes);
    var head = lib.Template("T")!.Layers.Single(l => l.Name == "head");
    Assert.Equal(1.0, head.Variants[0].Weight);
    lib.ApplyScriptDefaults(bytes);
    Assert.Equal(0.5, head.Variants[0].Weight);
    Assert.Equal(0.25, head.Variants[1].Weight); // written probability wins
    Assert.StartsWith("PROVEN", lib.ProbabilityDefaultEvidence);
    lib.ApplyScriptDefaults(null);
    Assert.Equal(0.5, head.Variants[0].Weight);
  }

  private const string LayerTemplates = """
templates   =   {
   T   =   {
      bodyLayers   =   {
         hair   =   {
            disabledByLayer   =   "head"
            templateVariants   =   [ { template   =   { __type   =   "InstConstructorMeshTemplatesHair01" } } ]
         }
         head   =   {
            requiredLayer   =   "helmet"
            templateVariants   =   [
               { template   =   { __type   =   "InstConstructorMeshTemplatesHead01" } },
               { probability   =   0.25
                 template   =   { __type   =   "InstConstructorMeshTemplatesHead01" } }
            ]
         }
         helmet   =   {
            templateVariants   =   [ { template   =   { __type   =   "InstConstructorMeshTemplatesHelmet01" } } ]
         }
      }
   }
}
""";

  private const string LayerMeshes = """
classes   =   {
   EMPTY   =   { sslModule   =   "x" }
   Hair_01   =   { geomObjNames   =   [ "hair_01" ] }
   Head_01   =   { geomObjNames   =   [ "head_01" ] }
   Helmet_01   =   { geomObjNames   =   [ "helmet_01" ] }
}
""";

  /// <summary>GenerateRandomPreset: one pass in bodyLayers order; a removal is seen only by later layers.</summary>
  [Fact]
  public void Layer_rules_are_one_ordered_pass()
  {
    var lib = InstConstructorLibrary.FromText(LayerTemplates, LayerMeshes);
    var t = lib.Template("T")!;
    var picks = InstConstructorLibrary.DefaultPicks(t);
    Assert.Equal(new[] { "hair" }, lib.DisabledLayers(t, picks)); // head is filled
    picks["helmet"] = "EMPTY";
    // head goes (no helmet), but hair was checked before head went, so hair stays off (a fixed point would bring it back).
    Assert.Equal(new[] { "hair", "head" }, lib.DisabledLayers(t, picks).Order());
  }

  /// <summary>Retail sslbin when LIBSABER_SM2_SSL points at an extracted ssl/ folder.</summary>
  [Fact]
  public void Retail_probability_default_is_one()
  {
    var root = Environment.GetEnvironmentVariable("LIBSABER_SM2_SSL");
    var path = Path.Combine(root ?? "", InstConstructorLibrary.MeshTemplateVariantScriptPath["ssl/".Length..]);
    if (string.IsNullOrWhiteSpace(root) || !System.IO.File.Exists(path))
      return;
    var s = Sslb.Parse(System.IO.File.ReadAllBytes(path));
    Assert.Equal(1.0f, s.FieldDefault(s.Class("InstConstructorMeshTemplateVariant")!, "probability"));
  }
}
