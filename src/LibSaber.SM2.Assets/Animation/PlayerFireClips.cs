using System.Text.RegularExpressions;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>One player fire clip choice. <see cref="Evidence"/> is INFERRED (decoded network) or HEURISTIC (fallback/default).</summary>
public sealed record PlayerFireClip(string Aim, string Blind, string Evidence, string Why);

/// <summary>
///   Player (Primaris actor) fire clip selection, decoded from the Morpheme network <c>morpheme/primaris/primaris.nmb</c>
///   with <c>tools/re/nmb_reader.py</c> (R38, docs/handoff/FIRE_CLIP.md).
///   <list type="bullet">
///     <item>Aimed fire = state <c>LOCO|minizoom|shoot|shoot_state</c>, hip fire = <c>LOCO|loco2|blind_shoot|blind_whole_anim|shoot|shoot_state</c>.</item>
///     <item>Switch nodes keyed by control parameter <c>wpn_type</c> = the firearm's <c>prop_firearm_type_settings.wpnType</c>
///       (cls DATA): &lt;1 pistols, 1.x rifles, ≥2 heavies (blind only; floor over weights 2/2.1/2.2/2.3).</item>
///     <item>Expression nodes (bytecode, semantics INFERRED) map exact wpnType values (+ <c>plasma_charged</c>) to the clip index.
///       Values no branch assigns use the expression's default (unknown, HEURISTIC 0).</item>
///   </list>
///   The game never names these clips in ssl/cls (DATA): the shot itself is the <c>ho_shoot</c> float pulse from
///   <c>RecoilAnimationController</c>.
/// </summary>
public static class PlayerFireClips
{
  private static readonly Regex ClipRx = new(@"(?:^|/)tacticus_sht(\d+)_shoot_(aim|blind)(?:_melta)?(?:_intercessor)?$",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

  /// <summary>True for the 31 player fire clips the network switches between (<c>tacticus_shtN_shoot_aim|blind</c>, DATA).</summary>
  public static bool IsFireClip(string? clipName) => clipName is { Length: > 0 } && ClipRx.IsMatch(StripExt(clipName));

  /// <summary>shtN of a fire clip name, else null.</summary>
  public static int? ShtIndex(string? clipName) =>
    clipName is { Length: > 0 } && ClipRx.Match(StripExt(clipName)) is { Success: true } m ? int.Parse(m.Groups[1].Value) : null;

  private static string StripExt(string s)
  {
    s = s.Replace('\\', '/');
    foreach (var ext in new[] { ".sani_data", ".sani.resource", ".sani" })
      if (s.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return s[..^ext.Length];
    return s;
  }

  private static bool Eq(double a, double b) => (float)a == (float)b;   // network compares float32

  /// <summary>Clips the player network plays for a firearm <c>wpnType</c>.</summary>
  public static PlayerFireClip For(double wpnType, bool plasmaCharged = false, bool auxVision = false)
  {
    static string A(int n) => $"tacticus_sht{n}_shoot_aim";
    static string B(int n) => $"tacticus_sht{n}_shoot_blind";
    var w = wpnType;
    if (w < 1)
    {
      // Expression_1 (aim and blind identical): 0 → sht6, 1 → sht7, 2/3 → sht14 (via ModifyJointTransform; 3 clamps to the last weight).
      int n; string ev = "INFERRED", why;
      if (Eq(w, 0.1) || (Eq(w, 0.2) && !plasmaCharged)) { n = 6; why = "wpn_type 0.1 | (0.2 & !plasma_charged) → 0"; }
      else if (Eq(w, 0) || (Eq(w, 0.2) && plasmaCharged)) { n = 7; why = "wpn_type 0 | (0.2 & plasma_charged) → 1"; }
      else if (Eq(w, 0.3) || Eq(w, 0.4)) { n = 14; why = "wpn_type 0.3 → 2, 0.4 → 3 (clamped to last child)"; }
      else { n = 6; ev = "HEURISTIC"; why = $"wpn_type {w} unassigned in Expression_1 (default 0 assumed)"; }
      return new PlayerFireClip(A(n), B(n), ev, why);
    }
    if (w < 2)
    {
      int? group = null;   // shared head of Expression_2 (aim and blind)
      if (Eq(w, 1.03) || Eq(w, 1.11) || Eq(w, 1.2) || Eq(w, 1.21) || (Eq(w, 1.3) && !plasmaCharged)) group = 1;
      else if (Eq(w, 1.1) || Eq(w, 1.13) || (Eq(w, 1.3) && plasmaCharged) || Eq(w, 1.6)) group = 2;
      else if (Eq(w, 1.12)) group = 3;
      if (group is { } g) return new PlayerFireClip(A(g), B(g), "INFERRED", $"Expression_2: wpn_type {w} → sht{g}");
      // Tails differ between the aim and the blind program.
      string aim, blind, ev = "INFERRED", why;
      if (Eq(w, 1.4)) { aim = A(5); blind = B(5); ev = "HEURISTIC"; why = "1.4 → 3 (aim sht5; blind child is a SubtractiveBlend, not indexed, assumed sht5; nmb_verify R40)"; }
      else if (Eq(w, 1.41)) { aim = A(1); blind = B(5); ev = "HEURISTIC"; why = "1.41: blind → 3 (sht5 SubtractiveBlend); aim unassigned (default 0 assumed)"; }
      else if (Eq(w, 1.25)) { aim = A(15); blind = B(15); why = "1.25 → 4"; }
      else if (Eq(w, 1.51)) { aim = auxVision ? "tacticus_sht16_shoot_aim_melta" : A(16); blind = B(16); ev = "HEURISTIC"; why = "1.51 → 5 (aim: Switch_2 input SimpleTypeConvert not indexed, assumed isInAuxVision → melta clip; nmb_verify R40)"; }
      else if (Eq(w, 1.7)) { aim = A(15); blind = B(16); ev = "HEURISTIC"; why = "1.7: aim → 4 behind an undecoded isInAuxVision test; blind else-branch → 5"; }
      else if (Eq(w, 1.5)) { aim = A(2); blind = B(16); ev = "HEURISTIC"; why = "1.5: aim → 1 behind an undecoded isInAuxVision test; blind else-branch → 5"; }
      else { aim = A(1); blind = B(16); ev = "HEURISTIC"; why = $"wpn_type {w}: blind else-branch → 5 (sht16, INFERRED); aim unassigned (default 0 → sht1 assumed)"; }
      return new PlayerFireClip(aim, blind, ev, why);
    }
    // Heavies: blind Switch_10 floor over [2, 2.1, 2.2, 2.3]; the aim network has no heavy branch.
    int h = w >= 2.3 - 1e-6 ? 13 : w >= 2.2 - 1e-6 ? 10 : w >= 2.1 - 1e-6 ? (plasmaCharged ? 12 : 11) : 9;
    return new PlayerFireClip(A(1), B(h), "HEURISTIC", $"heavy wpn_type {w}: blind sht{h} (Switch_10, INFERRED); aim has no heavy branch (sht1 default assumed)");
  }
}
