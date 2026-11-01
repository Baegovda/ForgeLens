namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   Decide whether a material slot samples the packed specular map (UE <c>UseSpecMap</c> / viewer <c>uHasSpec</c>).
///   <para>
///   <b>DATA</b> (rea-batch 4, armory capture 128197, 140 draws): <c>OBJ_MTLBLEND_LAYER_CONST[15].x</c> bits
///   <c>0x2000</c> ↔ packed MSCRGHAO spec td 68/68, <c>0x40</c> ↔ legacy <c>*_spec</c> td 66/67. When no spec
///   texture is bound the PS uses metal 0 / rough 1 raw. mtl1-26 chameleon draws: those bits are 0 (7/7), so the
///   PS replaces packed metal with 0 and packed roughness with 1 (<c>metal = sat(bias)</c>, <c>rough = sat(scale+bias)</c>).
///   </para>
///   <para>
///   <b>HEURISTIC:</b> the CPU writer of <c>OBJ_MTLBLEND_LAYER_CONST[15].x</c> (CB_OBJ_MATERIAL_DATA reg 21 / byte 336)
///   was not found (shaders are CPU-only for this value — docs/handoff/SHADERS.md §5; SYMBOL_DB materials;
///   commit b6cf3a1). Closing evidence = REA/Ghidra store into that CB field from a texture-bind / usage-mask path.
///   </para>
/// </summary>
public static class UseSpecMapPolicy
{
  public const string Evidence =
    "HEURISTIC (DATA: armory 128197 0x2000↔MSCRGHAO 68/68, 0x40↔*_spec 66/67; chameleon flags 0; " +
    "writer of OBJ_MTLBLEND_LAYER_CONST[15].x not found — SHADERS.md §5 / SYMBOL_DB materials / b6cf3a1)";

  /// <summary>True when the slot should sample the packed specular map for metalness/roughness.</summary>
  public static bool Decide(bool hasSpecularMap, bool chameleonEnabled)
    => hasSpecularMap && !chameleonEnabled;

  /// <summary>Convenience for a <see cref="MeshPart"/> (specular name set and chameleon off).</summary>
  public static bool Decide(MeshPart part)
    => Decide(!string.IsNullOrEmpty(part.Specular), part.Chameleon is not null);
}
