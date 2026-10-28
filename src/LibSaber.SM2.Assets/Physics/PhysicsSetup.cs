using System.Numerics;

namespace LibSaber.SM2.Assets.Physics;

// Data model of the two secondary-motion systems a character class (.cls) drives, with the engine's
// defaults. Every default below is read from sm2_retail.exe (see HANDOFF.md "Physics" for the full
// address list); field order is the engine's registration order.

/// <summary>
///   NvCloth phase (vertical, horizontal, bend, shear). Defaults from the phase constructor at
///   0x141089140: stiffness 0.8, stiffnessMultiplier 1, compressionLimit 0.9, stretchLimit 1.1.
/// </summary>
public sealed class ClothPhase
{
  public float Stiffness { get; set; } = 0.8f;
  public float StiffnessMultiplier { get; set; } = 1f;
  public float CompressionLimit { get; set; } = 0.9f;
  public float StretchLimit { get; set; } = 1.1f;
}

/// <summary>Config +0x40: tether {stiffness, scale, useTopologicalDistance}. Defaults 0x14108a660: 0.5, 1.</summary>
public sealed class ClothTether
{
  public float Stiffness { get; set; } = 0.5f;
  public float Scale { get; set; } = 1f;
  public bool UseTopologicalDistance { get; set; }
}

/// <summary>Config +0x4c: motion {enable, stiffness, distanceScale}. Defaults 0x141089966: stiffness 0.5, distanceScale 1.</summary>
public sealed class ClothMotion
{
  public bool Enable { get; set; } = true;
  public float Stiffness { get; set; } = 0.5f;
  public float DistanceScale { get; set; } = 1f;
}

/// <summary>Config +0x58: separation {enable, radiusScale, offsetScale}. Defaults 0x141089fe6: off, 1, 1.</summary>
public sealed class ClothSeparation
{
  public bool Enable { get; set; }
  public float RadiusScale { get; set; } = 1f;
  public float OffsetScale { get; set; } = 1f;
}

/// <summary>Config +0x64: selfCollision {enable, stiffness, distance}. Defaults (ctor 0x14108efce): off, 0.5, 0.</summary>
public sealed class ClothSelfCollision
{
  public bool Enable { get; set; }
  public float Stiffness { get; set; } = 0.5f;
  public float Distance { get; set; }
}

/// <summary>A tapered capsule between two bones (offsets are in each bone's local frame).</summary>
public sealed class ClothCapsule
{
  public string Name { get; set; } = "";
  public string BoneFirst { get; set; } = "";
  public float RadiusFirst { get; set; }
  public Vector3 BoneFirstOffset { get; set; }
  public string BoneSecond { get; set; } = "";
  public float RadiusSecond { get; set; }
  public Vector3 BoneSecondOffset { get; set; }
  /// <summary>mtl1-37: set when a bone name was a tpl_markup locator (see <see cref="CdtObjectResolver"/>), e.g. "BoneFirst=Backpack_CDT_L".</summary>
  public string ResolvedFrom { get; set; } = "";
}

public sealed class ClothSphere
{
  public string Name { get; set; } = "";
  public string Bone { get; set; } = "";
  public float Radius { get; set; }
  public Vector3 BoneOffset { get; set; }
  /// <summary>mtl1-37: see <see cref="ClothCapsule.ResolvedFrom"/>.</summary>
  public string ResolvedFrom { get; set; } = "";
}

public sealed class ClothBox
{
  public string Name { get; set; } = "";
  public string Bone { get; set; } = "";
  /// <summary>Full edge lengths in the bone frame (treated as full size, centred on the bone plus offset).</summary>
  public Vector3 Size { get; set; }
  public Vector3 BoneOffset { get; set; }
  /// <summary>mtl1-37: box orientation in the bone frame (identity unless it came from a rotated tpl_markup locator).</summary>
  public Quaternion Rotation { get; set; } = Quaternion.Identity;
  /// <summary>mtl1-37: see <see cref="ClothCapsule.ResolvedFrom"/>.</summary>
  public string ResolvedFrom { get; set; } = "";
}

/// <summary>
///   Config +0xb0 environment. Defaults (ctor 0x14108f03a..0x14108f0a4): airDrag 0.5, airLift 0.5, winds 0,
///   useWindSystem true, windSystemInfluenceScale 1, fluidDensity 1, collisionWithEnvironment false.
/// </summary>
public sealed class ClothEnvironment
{
  public float AirDrag { get; set; } = 0.5f;
  public float AirLift { get; set; } = 0.5f;
  public Vector3 LocalWindVelocity { get; set; }
  public Vector3 GlobalWindVelocity { get; set; }
  public bool UseWindSystem { get; set; } = true;
  public float WindSystemInfluenceScale { get; set; } = 1f;
  public float FluidDensity { get; set; } = 1f;
}

/// <summary>
///   One <c>clothes[].config</c> block (engine struct registered at 0x141090c39..0x141091847, defaults in the
///   constructor ending 0x14108f119).
/// </summary>
public sealed class ClothConfig
{
  public ClothPhase Vertical { get; set; } = new();
  public ClothPhase Horizontal { get; set; } = new();
  public ClothPhase Bend { get; set; } = new();
  public ClothPhase Shear { get; set; } = new();
  public ClothTether Tether { get; set; } = new();
  public ClothMotion Motion { get; set; } = new();
  public ClothSeparation Separation { get; set; } = new();
  public ClothSelfCollision SelfCollision { get; set; } = new();
  /// <summary>+0x70, default 1.</summary>
  public float StiffnessMultiplier { get; set; } = 1f;
  /// <summary>+0x74, default (0, -9.8, 0) (0x14108f112).</summary>
  public Vector3 Gravity { get; set; } = new(0, -9.8f, 0);
  public List<ClothCapsule> Capsules { get; } = new();
  public List<ClothSphere> Spheres { get; } = new();
  public List<ClothBox> Boxes { get; } = new();
  /// <summary>mtl1-30 HEURISTIC: true when the config had no cdt lists and the shapes are all shared CDT shapes.</summary>
  public bool CollisionFromShared { get; set; }
  /// <summary>mtl1-31 HEURISTIC: number of prop-level shared CDT shapes added to this cloth (shared shapes apply to every cloth).</summary>
  public int SharedShapesAdded { get; set; }
  public ClothEnvironment Environment { get; set; } = new();
  /// <summary>+0xe0, default 30 Hz.</summary>
  public float SolverFrequency { get; set; } = 30f;
  /// <summary>
  ///   NvCloth stiffness frequency. The engine field is <c>stiffnessFrequencyScale</c> (+0xe4, default 0.5);
  ///   retail classes write <c>stiffnessFrequency</c> (for example 90), which is kept here. Default 10 is NvCloth's.
  /// </summary>
  public float StiffnessFrequency { get; set; } = 10f;
  /// <summary>+0xe8, default 30.</summary>
  public int AccelerationFilterWidth { get; set; } = 30;
  public float CollisionThickness { get; set; }
  public float Friction { get; set; }
  /// <summary>+0xf4, default 0.05.</summary>
  public float Damping { get; set; } = 0.05f;
  public float LinearDrag { get; set; }
  public float AngularDrag { get; set; }
  /// <summary>+0x100..0x108, default 1, 1, 1.</summary>
  public float LinearInertia { get; set; } = 1f;
  public float AngularInertia { get; set; } = 1f;
  public float CentrifugalInertia { get; set; } = 1f;
  /// <summary>+0x110, default 20.</summary>
  public float CollisionMassScale { get; set; } = 20f;
}

/// <summary>One entry of <c>prop_cloth_sim.clothes</c>: the sim mesh, the render meshes it drives and the solver config.</summary>
public sealed class ClothEntry
{
  public string SimMeshName { get; set; } = "";
  public List<string> RenderMeshes { get; } = new();

  /// <summary>
  ///   The parts this cloth drives: exact <see cref="RenderMeshes"/> names, or, when a template carries none of those,
  ///   the same names with a <c>_LODn</c> suffix (Calgar's cc_calgar.tpl only ships <c>cloak_REND_01_LOD1</c> etc.).
  /// </summary>
  public List<T> DrivenParts<T>(IEnumerable<T> parts, Func<T, string> name)
  {
    var all = parts.ToList();
    var exact = all.Where(p => RenderMeshes.Any(r => string.Equals(name(p), r, StringComparison.OrdinalIgnoreCase))).ToList();
    if (exact.Count > 0)
      return exact;
    return all.Where(p => RenderMeshes.Any(r => string.Equals(StripLod(name(p)), r, StringComparison.OrdinalIgnoreCase))).ToList();
  }

  private static string StripLod(string name)
  {
    var i = name.LastIndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
    return i > 0 && i + 4 < name.Length && name[(i + 4)..].All(char.IsDigit) ? name[..i] : name;
  }

  // mtl1-37: simRemapConfig defaults from the retail ClothEntry constructor 0x141093260 (exe evidence):
  //   +0x18 maxInfluenceDist = 0x3e19999a (0.15) at 0x14109326c, +0x1c distanceRemapPower = 0x40a00000 (5) at 0x141093273,
  //   +0x20 useTopologicalDistance = false at 0x14109327a. (Previously 0.05 / 1 without a citation; meta_desc says 0.15 / 10.)
  public float MaxInfluenceDist { get; set; } = 0.15f;
  public float DistanceRemapPower { get; set; } = 5f;
  public bool UseTopologicalDistance { get; set; }
  /// <summary>
  ///   mtl1-37: <c>simRemapConfig.maxInfluenceVert</c> as authored (null when absent). Informational only: the retail exe has
  ///   no "maxInfluenceVert" string and its simRemapConfig serializers (0x141092850 / 0x141093480) read only the three fields
  ///   above, so the engine ignores it (stale schema field, value 4 in all 61 occurrences of the mtl31 cls set).
  /// </summary>
  public int? MaxInfluenceVert { get; set; }
  public ClothConfig Config { get; set; } = new();
}

/// <summary>
///   <c>PhysJiggleObjectSettings</c> (settings struct read by the step at 0x14147e5b5):
///   +0x10 stiffness, +0x14 mass, +0x18 damping, +0x1c gravity, +0x20 offsetDirection (default (0,0,1) at 0x143609db8),
///   +0x28 offsetDistance, +0x30 constraintMin (default (-1,-1,-1) at 0x143609e08), +0x38 constraintMax (default (1,1,1)
///   at 0x143609dd8), +0x40 writeBack flag, +0x44 jumpsIntensity, +0x48 jumpsConstraintMin, +0x4c jumpsConstraintMax.
///   Scalars a class leaves out are 0 (no exe default was found for them).
/// </summary>
public sealed class JiggleSettings
{
  public string Group { get; set; } = "";
  public string Name { get; set; } = "";
  public float Stiffness { get; set; }
  public float Mass { get; set; } = 1f;
  public float Damping { get; set; }
  public float Gravity { get; set; }
  public Vector3 OffsetDirection { get; set; } = Vector3.UnitZ;
  public float OffsetDistance { get; set; }
  public Vector3 ConstraintMin { get; set; } = new(-1);
  public Vector3 ConstraintMax { get; set; } = new(1);
  public bool WriteBack { get; set; }
  public float JumpsIntensity { get; set; }
  public float JumpsConstraintMin { get; set; }
  public float JumpsConstraintMax { get; set; }
}

/// <summary>Everything one character class asks the engine to simulate. Shared by the preview and the exporters.</summary>
public sealed class PhysicsSetup
{
  public string SourceClass { get; set; } = "";
  public List<ClothEntry> Cloths { get; } = new();
  public List<JiggleSettings> Jiggles { get; } = new();
  public List<string> Notes { get; } = new();
  public bool IsEmpty => Cloths.Count == 0 && Jiggles.Count == 0;
}
