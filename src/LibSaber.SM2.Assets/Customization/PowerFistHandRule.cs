using System.Text;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Scripting;

namespace LibSaber.SM2.Assets.Customization;

/// <summary>
///   datapars (2026-10-29): which hand meshes a power fist hides, read from the compiled ssl scripts instead of a built-in guess.
///   PROVEN chain (disassembly of the retail sslbin, <see cref="Sslb"/>):
///   <c>PropPowerFistCustomizerBase.SetRightHandVisibility(isVisible)</c> =
///   <c>handRegion.SetMeshes(FilterRightHandMeshes(propCustomizerState.GetMeshesByBodySection(K)))</c>, then
///   <c>propGeomCustomizer.Show/Hide(handRegion)</c>; <c>FilterRightHandMeshes</c> = copy minus <c>visibilityUnchangableMeshes</c>;
///   <c>GetMeshesByBodySection</c> returns the meshes of the outfit region the settings pick for that section; K is the int
///   pushed for the call (6) and names the <c>CharacterBodySection</c> enum member (RIGHT_GAUNTLET = 6).
///   Callers: by_melee_state ShowFist / by_loadout ApplyCustomizationByVersion call SetRightHandVisibility(false); Init /
///   HideFist hide every <c>powerFistIds</c> object (HideAllFists).
///   Nothing here names a section or mesh: the section, keep list and fist ids all come from the files.
/// </summary>
public sealed record PowerFistHandRule(string Section, int? SectionValue, IReadOnlyList<string> Keep, IReadOnlyList<string> FistIds, bool Proven, string Evidence)
{
  public const string BasePath = "ssl/body/customization/prop_power_fist_customizer_base_release.sslbin";
  public const string StatePath = "ssl/body/customization/prop_character_customization_state_base_release.sslbin";
  public const string SectionEnumPath = "ssl/body/customization/character_body_section_release.sslbin";
  public const string PropPath = "ssl/body/customization/prop_power_fist_customizer_base.prop";

  /// <summary>Fallback when the scripts are missing or do not match the expected shape (HEURISTIC, the mtl1-47 reading).</summary>
  public static PowerFistHandRule Fallback(string why, IReadOnlyList<string>? keep = null, IReadOnlyList<string>? fists = null)
    => new("RIGHT_GAUNTLET", null, keep ?? new[] { "torso_r_gauntlet", "upper_arm_librarium_T", "upper_arm_librarium_G" }, fists ?? Array.Empty<string>(), false,
      "HEURISTIC built-in RIGHT_GAUNTLET: " + why);

  /// <summary>Rule from the three scripts (+ the .prop text for the keep / fist lists). Never throws.</summary>
  public static PowerFistHandRule FromScripts(byte[]? baseSslb, byte[]? stateSslb, byte[]? sectionSslb, string? propText)
  {
    var keep = new List<string>();
    var fists = new List<string>();
    if (propText is not null)
    {
      try
      {
        var p = SslText.Parse(propText);
        keep.AddRange(SslText.Strings(p.GetValueOrDefault("visibilityUnchangableMeshes")).Where(n => n.Length > 0));
        fists.AddRange(SslText.Strings(p.GetValueOrDefault("powerFistIds")).Where(n => n.Length > 0));
      }
      catch
      {
        // lists stay empty; reported below
      }
    }

    if (baseSslb is null || stateSslb is null || sectionSslb is null)
      return Fallback("scripts not in the catalog", keep.Count > 0 ? keep : null, fists);
    try
    {
      var b = Sslb.Parse(baseSslb);
      var st = Sslb.Parse(stateSslb);
      var en = Sslb.Parse(sectionSslb);
      var cls = b.Classes.FirstOrDefault(c => c.Member("SetRightHandVisibility") is not null && c.Member("FilterRightHandMeshes") is not null);
      var getMeshes = st.Classes.Select(c => c.Member("GetMeshesByBodySection")).FirstOrDefault(m => m is not null);
      if (cls is null || getMeshes is null)
        return Fallback("SetRightHandVisibility / GetMeshesByBodySection not found", keep.Count > 0 ? keep : null, fists);
      var code = b.Code(cls, "SetRightHandVisibility");
      var filter = cls.Member("FilterRightHandMeshes")!;
      var keepMember = cls.Member("visibilityUnchangableMeshes");
      if (code is null)
        return Fallback("SetRightHandVisibility has no code", keep.Count > 0 ? keep : null, fists);
      var ops = code.Ops;
      int? section = null;
      var filtered = false;
      for (var i = 0; i < ops.Count; i++)
      {
        if (ops[i].Op == SslbOp.Call && ops[i].Arg == getMeshes.Slot && section is null)
        {
          var j = i - 1;
          if (j >= 1 && ops[j].Op == SslbOp.PushInt && ops[j - 1].Op == SslbOp.ArgCount)
            j -= 2; // "07 n, 03 n" argument count pair
          if (j >= 0 && ops[j].Op == SslbOp.PushInt)
            section = ops[j].Arg;
        }
        else if (section is not null && ops[i].Op == SslbOp.Call && ops[i].Arg == filter.Slot)
        {
          filtered = true;
        }
      }

      if (section is null)
        return Fallback("no int section argument before GetMeshesByBodySection", keep.Count > 0 ? keep : null, fists);
      var filterCode = b.Code(cls, "FilterRightHandMeshes");
      var filterReadsKeep = keepMember is not null && filterCode is not null && filterCode.Ops.Any(o => o.Op == SslbOp.GetMember && o.Arg == keepMember.Slot);
      var enumCls = en.Classes.FirstOrDefault(c => c.Name == "CharacterBodySection");
      var name = enumCls?.Members.FirstOrDefault(m => m.Constant is long v && v == section)?.Name;
      if (name is null)
        return Fallback($"CharacterBodySection has no member = {section}", keep.Count > 0 ? keep : null, fists);
      var proven = filtered && filterReadsKeep && keep.Count > 0;
      var evidence = $"{BasePath} {cls.Name}.SetRightHandVisibility: GetMeshesByBodySection(slot {getMeshes.Slot}, int {section}) = {name} ({SectionEnumPath})"
        + (filtered ? $", FilterRightHandMeshes (slot {filter.Slot})" : ", FilterRightHandMeshes call NOT seen")
        + (filterReadsKeep ? " minus visibilityUnchangableMeshes" : ", keep list read NOT seen")
        + (keep.Count > 0 ? $" [{string.Join(", ", keep)}] ({PropPath})" : " (no .prop keep list)");
      return new PowerFistHandRule(name, section, keep, fists, proven, (proven ? "PROVEN " : "PARTIAL ") + evidence);
    }
    catch (Exception e)
    {
      return Fallback("SSLB parse failed: " + e.Message, keep.Count > 0 ? keep : null, fists);
    }
  }

  public static PowerFistHandRule Load(GameCatalog catalog, ArchiveStore store)
  {
    byte[]? Bytes(string path) => catalog.TryGetByPath(path, out var f) ? store.Read(f) : null;
    var prop = Bytes(PropPath);
    return FromScripts(Bytes(BasePath), Bytes(StatePath), Bytes(SectionEnumPath), prop is null ? null : Encoding.UTF8.GetString(prop));
  }
}
