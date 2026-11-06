using System;
using System.Numerics;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   SM2 cascade-shadow consumer math (PS ResourceId 21153, eid 8274, CB ResourceId 265255 @260608).
///   Evidence: D:\sm2_re_rd_version_tests\scratch\gpu_cascade_cb_t98\ (cascade_viewer_formula_t98.py,
///   cascade_interpret_report.json, postvs_clip_verify.json) + sm2_re\map\runtime\logs\cascade_cb_interpret_t98_20261106.md.
///   PROVEN (t98): Qj = dot((P,1), Rj) with R0..R3 = literal stored float4 registers; NUM_SPLITS int4 @768:
///     x = cascade search loop count (i &lt; x), y = primary matrix slot; matrix reg = owner*24 + (slot&lt;&lt;2) + 52 (SM_MATR @832);
///     primary slot: (Q0,Q1,Q2) = (u,v,cmp), NO divide by Q3; loop slot i: Q.xyz / Q.w.
///     P = TEXCOORD13.xyz = obj * OBJ_WORLD (+optional wind) BEFORE subtracting VIEW_POSITION (world, not view space).
///     PostVS v0/i0 CPU/GPU clip compare PASS (max_abs ~9.2e-8).
///   OPEN: CPU writer of SM_MATR, light/view/Bias composition of Rj. Viewer cannot synthesize Rj from a light yet.
///   PREDICTION ONLY: shadowQ for v0 (~0.4895,0.7291,0.3503) — PS output not contrasted.
/// </summary>
public static class Sm2CascadeShadow
{
  public const int NumSplitsOffset = 768;
  public const int SmMatrOffset = 832;
  public const string Evidence = "PROVEN t98 e8274 PS21153: Qj=dot(P,Rj); primary xyz no /w; loop xyz/w";
  public const string OpenItems = "OPEN: SM_MATR CPU writer; light/view/Bias composite; shadowQ prediction-only";

  // DERIVED t105 from the SAME e8274 bytes (cascade_light_basis_t105.py): slots 52/56/60 share one orthonormal basis
  // (|dot|<1e-7), row2 = r0 x r1 => orthographic directional light, unit axis (-0.69468,0.57358,0.43408) (sign = toward/away OPEN);
  // R3=(0,0,0,1) in all slots => w=1 (loop divide is identity in this capture). World extent per unit: slot52 88.94/88.94/88.50,
  // slot56 266.81/266.81/269.57 (uv exactly 3x slot52), slot60 130.63/171.01/177.60 (non-square). Order of light/view/Bias OPEN.
  public static readonly Vector3 E8274LightAxis = new(-0.69468f, 0.57358f, 0.43408f);

  public static Vector4 Q(Vector3 p, ReadOnlySpan<Vector4> r) => new(
    Dot(p, r[0]), Dot(p, r[1]), Dot(p, r[2]), Dot(p, r[3]));

  static float Dot(Vector3 p, Vector4 r) => p.X * r.X + p.Y * r.Y + p.Z * r.Z + r.W;

  public static int MatrixRegister(int owner, int slot) => owner * 24 + (slot << 2) + 52;

  public static Vector4 ReadReg(ReadOnlySpan<byte> cb, int reg) => new(
    BitConverter.ToSingle(cb.Slice(reg * 16, 4)), BitConverter.ToSingle(cb.Slice(reg * 16 + 4, 4)),
    BitConverter.ToSingle(cb.Slice(reg * 16 + 8, 4)), BitConverter.ToSingle(cb.Slice(reg * 16 + 12, 4)));

  public static Vector4[] ReadBundle(ReadOnlySpan<byte> cb, int reg0) =>
    new[] { ReadReg(cb, reg0), ReadReg(cb, reg0 + 1), ReadReg(cb, reg0 + 2), ReadReg(cb, reg0 + 3) };

  /// <summary>(searchCount x, primarySlot y) from NUM_SPLITS int4 of the given owner.</summary>
  public static (int SearchCount, int PrimarySlot) ReadNumSplits(ReadOnlySpan<byte> cb, int owner = 0) =>
    (BitConverter.ToInt32(cb.Slice(NumSplitsOffset + owner * 16, 4)), BitConverter.ToInt32(cb.Slice(NumSplitsOffset + owner * 16 + 4, 4)));

  /// <summary>Primary-slot shadow coord (u,v,cmp): no W divide (PROVEN).</summary>
  public static Vector3 PrimaryCoord(Vector3 worldP, ReadOnlySpan<Vector4> r) { var q = Q(worldP, r); return new(q.X, q.Y, q.Z); }

  /// <summary>Loop-slot shadow coord: xyz / w (PROVEN).</summary>
  public static Vector3 LoopCoord(Vector3 worldP, ReadOnlySpan<Vector4> r) { var q = Q(worldP, r); return new(q.X / q.W, q.Y / q.W, q.Z / q.W); }
}

