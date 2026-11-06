namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   SM2 Morpheme (exe path morpheme_7_0_0) AssetType ids → public Morpheme4 SDK names. NAME-ONLY PROVEN (t98):
///   exact enum id in public mrManager.h AssetType + a second reader/datashape/string match per id
///   (sm2_re\map\a2anim\morpheme_public_a2_types_t98_evidence.md). Field layouts are NOT taken from M4:
///   type 1 SM2 differs (+0x28 optional ptr, extra ptrs +0x40..+0x70); do not apply M4 offsetof to SM2 M7.
/// </summary>
public static class Sm2MorphemeAssetTypes
{
  public static string? Name(int id) => id switch
  {
    1 => "kAsset_Rig (AnimRigDef)",
    2 => "kAsset_RigToAnimMap",
    6 => "kAsset_PhysicsRigDef",
    10 => "kAsset_NetworkDef",
    11 => "kAsset_NetworkPredictionDef",
    _ => null,
  };

  public const string Scope = "Name-only PROVEN; M7 field layouts OPEN (L64/L351)";
}
