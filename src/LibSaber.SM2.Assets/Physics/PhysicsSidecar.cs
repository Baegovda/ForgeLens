using System.Numerics;
using System.Text.Json;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Physics;

/// <summary>
///   <c>{model}.physics.json</c>: the parsed class physics (the same <see cref="PhysicsSetup"/> the browser simulates),
///   what was baked into the exported clips, and the Unreal parameter mapping that <c>ue_sm2_physics.py</c> and a manual
///   live setup (AnimDynamics, Chaos Cloth) use. Source values are meters, Y-up; <c>ue</c> blocks are centimeters, Z-up.
/// </summary>
public static class PhysicsSidecar
{
  private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = true };

  /// <summary>True when a visible part named like the cloth's sim mesh is in the export (the _SIM section CreateChaosCloth looks for).</summary>
  public static bool SimSectionExported(ExportedMesh mesh, string? simMeshName) =>
    !string.IsNullOrEmpty(simMeshName) &&
    mesh.Parts.Any(p => p.Visible && string.Equals(p.Name, simMeshName, StringComparison.OrdinalIgnoreCase));

  public static string Serialize(ExportedMesh mesh, PhysicsSetup setup, ClothBoneRig? rig, IReadOnlyDictionary<string, PhysicsBakeReport> baked)
  {
    static float[] V(Vector3 v) => new[] { v.X, v.Y, v.Z };
    static float[] Ue(Vector3 v) => V(AxisConvert.Direction(v, false) * 100f);
    // mtl1-11: positions as UE shows the imported FBX mesh. Measured in UE 5.7: the imported sections sit at (-x, y, z) of Ue(p)
    // (calgar ribbon_02_L sec centre 18.7 vs sidecar -18.7, same extents), so the X axis is mirrored here.
    static float[] UeMesh(Vector3 v) { var a = Ue(v); a[0] = -a[0]; return a; }
    var bones = mesh.Bones;
    string BoneName(int i) => i >= 0 && i < bones.Count ? bones[i].Name : "";

    var jiggles = setup.Jiggles.Select(j =>
    {
      var bone = ClothInstance.FindBone(bones, j.Name);
      var invMass = j.Mass != 0 ? 1f / j.Mass : 0f;
      var d = j.OffsetDistance;
      var dir = j.OffsetDirection.LengthSquared() > 1e-12f ? Vector3.Normalize(j.OffsetDirection) : Vector3.UnitZ;
      float Deg(float c) => MathF.Atan(c) * 180f / MathF.PI;
      return new
      {
        j.Group,
        j.Name,
        found = bone >= 0,
        parent = bone >= 0 ? BoneName(bones[bone].Parent) : "",
        source = new
        {
          j.Stiffness, j.Mass, j.Damping, j.Gravity,
          offsetDirection = V(j.OffsetDirection), j.OffsetDistance,
          constraintMin = V(j.ConstraintMin), constraintMax = V(j.ConstraintMax),
          j.WriteBack, j.JumpsIntensity, j.JumpsConstraintMin, j.JumpsConstraintMax,
        },
        derived = new
        {
          naturalFrequencyHz = j.Stiffness > 0 && invMass > 0 ? MathF.Sqrt(j.Stiffness * invMass) / (2 * MathF.PI) : 0,
          retentionPerSecond = MathF.Exp(-j.Damping),
          gravityAcceleration = j.Gravity * invMass,
        },
        ue = new
        {
          animDynamics = new
          {
            boundBone = j.Name,
            note = "Single-body AnimDynamics on boundBone. LocalJointOffset is minus the offset in the bone's source frame (cm, convert with the FBX bone axes). Spring constant assumes AnimDynamics unit body mass. Jumps (translation by offset * clamp(v.y * jumpsIntensity)) have no AnimDynamics equivalent and are only in the baked curves.",
            pendulumOffsetSourceCm = V(-dir * d * 100f),
            bLinearSpring = true,
            linearSpringConstant = j.Stiffness * invMass,
            bOverrideLinearDamping = true,
            linearDampingOverride = j.Damping,
            bEnableGravity = j.Gravity != 0,
            gravityScale = j.Gravity * invMass / -9.8f,
            linearLimitAlongOffsetCm = new[] { j.ConstraintMin.Z * d * 100f, j.ConstraintMax.Z * d * 100f },
            angularLimitsDeg = new
            {
              min = new[] { Deg(j.ConstraintMin.X), Deg(j.ConstraintMin.Y), 0f },
              max = new[] { Deg(j.ConstraintMax.X), Deg(j.ConstraintMax.Y), 0f },
            },
          },
        },
      };
    }).ToList();

    var cloths = setup.Cloths.Select(c =>
    {
      var shell = mesh.ClothSims.FirstOrDefault(s => string.Equals(s.Name, c.SimMeshName, StringComparison.OrdinalIgnoreCase));
      var cfg = c.Config;
      var maxDistance = shell?.Colors.Select(col => col.X * cfg.Motion.DistanceScale).ToArray() ?? Array.Empty<float>();
      return new
      {
        c.SimMeshName,
        c.RenderMeshes,
        c.MaxInfluenceDist,
        c.DistanceRemapPower,
        simVertices = shell?.Positions.Count ?? 0,
        // mtl1-28: false when the _SIM shell was left out of the FBX (no --include-cloth-sim). CreateChaosCloth then uses the
        // primary render section as its own sim mesh (render-as-sim fallback, HEURISTIC) instead of converting 0 cloths.
        simSectionExported = SimSectionExported(mesh, c.SimMeshName),
        pinned = maxDistance.Count(m => m <= 1e-4f),
        maxDistanceMeters = maxDistance,
        // mtl1-9: sim particle bind positions in the Unreal FBX space (cm, Z-up), same order as maxDistanceMeters.
        // SM2PhysicsSetup.CreateChaosCloth uses them to find the _SIM section and paint MaxDistance on its cloth particles.
        simPositionsSpace = "ueMesh",
        simPositionsUeCm = shell?.Positions.SelectMany(p => UeMesh(p)).ToArray() ?? Array.Empty<float>(),
        helperBones = rig?.Clusters.Where(k => string.Equals(k.Cloth, c.SimMeshName, StringComparison.OrdinalIgnoreCase)).Select(k => new { bone = BoneName(k.Bone), parent = BoneName(k.Parent), particles = k.Particles.Length }).ToList(),
        source = cfg,
        ue = new
        {
          chaosCloth = new
          {
            note = "Live Chaos Cloth alternative to the baked helper bones: Create Cloth Asset from Selection on the render sections, paint MaxDistance from maxDistanceMeters (x100 cm, nearest sim vertex), then set these on the ChaosClothConfig. Stiffness values are NvCloth per-iteration fractions; Chaos uses 0..1 stiffness too, so they are copied, not converted.",
            maxDistanceScaleCm = cfg.Motion.DistanceScale * 100f,
            edgeStiffness = Math.Clamp((cfg.Vertical.Stiffness + cfg.Horizontal.Stiffness) * 0.5f * cfg.StiffnessMultiplier, 0f, 1f),
            bendingStiffness = Math.Clamp(cfg.Bend.Stiffness * cfg.StiffnessMultiplier, 0f, 1f),
            areaStiffness = Math.Clamp(cfg.Shear.Stiffness * cfg.StiffnessMultiplier, 0f, 1f),
            tetherStiffness = Math.Clamp(cfg.Tether.Stiffness, 0f, 1f),
            tetherScale = cfg.Tether.Scale,
            dampingCoefficient = cfg.Damping,
            bUseGravityOverride = true,
            gravityOverrideCm = Ue(cfg.Gravity),
            drag = cfg.Environment.AirDrag,
            lift = cfg.Environment.AirLift,
            linearVelocityScale = cfg.LinearInertia,
            angularVelocityScale = cfg.AngularInertia,
            fictitiousAngularScale = cfg.CentrifugalInertia,
            collisionThicknessCm = cfg.CollisionThickness * 100f,
            frictionCoefficient = cfg.Friction,
            subdivisionCount = Math.Max(1, (int)MathF.Ceiling(cfg.SolverFrequency / 60f)),
            bUseSelfCollisions = cfg.SelfCollision.Enable,
            localWindCm = UeMesh(cfg.Environment.LocalWindVelocity),
            windSpace = "ueMesh", // component-space vector: X-mirrored like simPositionsUeCm
            collision = new
            {
              note = "PhysicsAsset bodies for the cloth: capsules as tapered capsules (Chaos Cloth only) when radii differ, spheres, boxes (size is the full extent).",
              source = cfg.CollisionFromShared ? "sharedCDT (HEURISTIC: config has no cdt lists)" : "config",
              space = "boneLocal", // mtl1-14d: bone-local offsets are NOT mirrored (imported bone frames absorb the X reflection; mirroring blew up the sorcerer cloak). Plugin also shrinks shapes off free rest particles.
              capsules = cfg.Capsules.Select(k => new { k.Name, k.BoneFirst, radiusFirstCm = k.RadiusFirst * 100f, offsetFirstCm = Ue(k.BoneFirstOffset), k.BoneSecond, radiusSecondCm = k.RadiusSecond * 100f, offsetSecondCm = Ue(k.BoneSecondOffset) }),
              spheres = cfg.Spheres.Select(k => new { k.Name, k.Bone, radiusCm = k.Radius * 100f, offsetCm = Ue(k.BoneOffset) }),
              boxes = cfg.Boxes.Select(k => new { k.Name, k.Bone, sizeCm = V(new Vector3(k.Size.X, k.Size.Z, k.Size.Y) * 100f), offsetCm = Ue(k.BoneOffset) }),
            },
          },
        },
      };
    }).ToList();

    return JsonSerializer.Serialize(new
    {
      setup.SourceClass,
      model = mesh.Name,
      note = "Secondary physics of the character class (prop_cloth_sim, prop_phys_jiggle). baked lists the clips whose FBX/glTF already carry the simulated jiggle and cloth helper bones (same solver as the SM2Browser preview), so Unreal needs no runtime setup. ue blocks map the source parameters for a live AnimDynamics / Chaos Cloth setup instead (see HANDOFF.md, Physics).",
      setup.Notes,
      clothBoneNotes = rig?.Notes,
      clothSkinnedVertices = rig?.SkinnedVertices ?? 0,
      baked = baked.Select(kv => new { clip = kv.Key, kv.Value.Frames, rate = PhysicsBake.Rate, kv.Value.JiggleBones, kv.Value.ClothBones, kv.Value.MaxJiggleDegrees, kv.Value.MaxClusterFitError, kv.Value.MeanStepMs }),
      jiggles,
      cloths,
    }, Options);
  }
}
