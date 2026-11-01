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
    // mtl1-37: box edge directions in the UE bone frame, in sizeCm order (source X, Z, Y); null when unrotated.
    static float[][]? BoxAxes(Quaternion q) => q.IsIdentity ? null : new[] { Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY }.Select(a => V(AxisConvert.Direction(Vector3.Transform(a, q), false))).ToArray();
    var bones = mesh.Bones;
    string BoneName(int i) => i >= 0 && i < bones.Count ? bones[i].Name : "";

    var jiggles = setup.Jiggles.Select(j =>
    {
      var bone = ClothInstance.FindBone(bones, j.Name);
      var invMass = j.Mass != 0 ? 1f / j.Mass : 0f;
      var d = j.OffsetDistance;
      var dir = j.OffsetDirection.LengthSquared() > 1e-12f ? Vector3.Normalize(j.OffsetDirection) : Vector3.UnitZ;
      float Deg(float c) => MathF.Atan(c) * 180f / MathF.PI;
      // phys52: RigidBody (PhysicsAsset) mapping used by SM2PhysicsSetup. Rest direction = offset direction moved onto the
      // clamp box point nearest the target (the spring's equilibrium when the box excludes the target, e.g. Hip_plate x in
      // [-0.4,-0.35]) DERIVED; swing = widest angle from it to a box corner (clamped 0.5..60 deg, clamp HEURISTIC).
      Vector3 Box(Vector3 u) => dir + u; // in offsetDistance units, bone-local source frame
      var rest = Box(Vector3.Clamp(Vector3.Zero, j.ConstraintMin, j.ConstraintMax));
      rest = rest.LengthSquared() > 1e-12f ? Vector3.Normalize(rest) : dir;
      var swing = 0f;
      for (var c = 0; c < 8; c++)
      {
        var corner = Box(new Vector3((c & 1) != 0 ? j.ConstraintMax.X : j.ConstraintMin.X, (c & 2) != 0 ? j.ConstraintMax.Y : j.ConstraintMin.Y, (c & 4) != 0 ? j.ConstraintMax.Z : j.ConstraintMin.Z));
        if (corner.LengthSquared() > 1e-12f)
          swing = MathF.Max(swing, MathF.Acos(Math.Clamp(Vector3.Dot(rest, Vector3.Normalize(corner)), -1f, 1f)) * 180f / MathF.PI);
      }
      var gDown = -j.Gravity * invMass; // m/s^2, gravity is added on world Y (negative = down)
      var locked = d <= 0;
      return new
      {
        j.Group,
        j.Name,
        bone = j.Name, // phys52: the plugin reads "Name" and falls back to "bone"
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
          rigidBody = new
          {
            note = "phys52 SM2PhysicsSetup mapping (DERIVED unless noted): simulated body on the jiggle bone, joint at the bone origin, twist locked (the engine only re-aims the offset axis); SLERP drive in acceleration mode toward restDirUe with stiffness k/m [1/s^2] and damping = game damping [1/s] (exp(-damping dt) velocity decay); gravity as a point mass at comDistanceCm so 980/comDistanceCm equals the game's (gravity/mass)/offsetDistance. locked: offsetDistance 0 (no rotation in the game). Mass per chain depth and the 0.5..60 deg clamp are HEURISTIC.",
            space = "boneLocal",
            offsetDirUe = V(AxisConvert.Direction(dir, false)),
            restDirUe = V(AxisConvert.Direction(rest, false)),
            swingDeg = Math.Clamp(swing, 0.5f, 60f),
            locked,
            driveStiffness = j.Stiffness * invMass,
            driveDamping = j.Damping,
            gravityEnabled = gDown > 0 && d > 0,
            comDistanceCm = gDown > 0 && d > 0 ? Math.Clamp(980f * d / gDown, 2f, 300f) : Math.Clamp(d * 100f, 2f, 50f),
            sourceMassKg = j.Mass,
          },
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
        c.UseTopologicalDistance,
        // mtl1-37: authored value only; retail ignores it (no string in the exe).
        c.MaxInfluenceVert,
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
            // HEURISTIC rest-fit (R76/R91/R93/R97k): plugin prefers these over sm2.RestFit* CVar defaults so reimport keeps the decision.
            restFit = new
            {
              evidence = RestFitPolicy.Evidence,
              floor = RestFitPolicy.Floor,
              deepFrac = RestFitPolicy.DeepFrac,
              shallowFrac = RestFitPolicy.ShallowFracFor(c.SimMeshName),
            },
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
              note = "PhysicsAsset bodies for the cloth: capsules as tapered capsules (Chaos Cloth only) when radii differ, spheres, boxes (size is the full extent). ResolvedFrom: the shape named a tpl_markup locator, now expressed on the locator's parent bone (mtl1-37).",
              source = cfg.SharedShapesAdded == 0 ? "config" : cfg.CollisionFromShared ? "sharedCDT (HEURISTIC)" : "config+sharedCDT (HEURISTIC)",
              sharedShapesAdded = cfg.SharedShapesAdded,
              space = "boneLocal", // mtl1-14d: bone-local offsets are NOT mirrored (imported bone frames absorb the X reflection; mirroring blew up the sorcerer cloak). Plugin also shrinks shapes off free rest particles.
              // Duplicated from chaosCloth.restFit so AddCollision(Coll) can read without the parent object.
              restFitFloor = RestFitPolicy.Floor,
              restFitDeepFrac = RestFitPolicy.DeepFrac,
              restFitShallowFrac = RestFitPolicy.ShallowFracFor(c.SimMeshName),
              restFitEvidence = RestFitPolicy.Evidence,
              capsules = cfg.Capsules.Select(k => new { k.Name, k.ResolvedFrom, k.BoneFirst, radiusFirstCm = k.RadiusFirst * 100f, offsetFirstCm = Ue(k.BoneFirstOffset), k.BoneSecond, radiusSecondCm = k.RadiusSecond * 100f, offsetSecondCm = Ue(k.BoneSecondOffset) }),
              spheres = cfg.Spheres.Select(k => new { k.Name, k.ResolvedFrom, k.Bone, radiusCm = k.Radius * 100f, offsetCm = Ue(k.BoneOffset) }),
              boxes = cfg.Boxes.Select(k => new { k.Name, k.ResolvedFrom, k.Bone, sizeCm = V(new Vector3(k.Size.X, k.Size.Z, k.Size.Y) * 100f), offsetCm = Ue(k.BoneOffset), axesUe = BoxAxes(k.Rotation) }),
            },
          },
        },
      };
    }).ToList();

    // phys52: prop_motion_damper_controller -> per-joint angular spring-damper (SM2PhysicsSetup second RigidBody node).
    int Child(int b) { for (var i = 0; i < bones.Count; i++) if (bones[i].Parent == b) return i; return -1; }
    bool IsAncestor(int a, int b) { for (var i = b; i >= 0; i = bones[i].Parent) if (i == a) return true; return false; }
    var springDampers = setup.SpringDampers.Select((sd, di) => new
    {
      index = di,
      sd.TimeScale,
      windFlutter = sd.WindFlutter is not null,
      joints = sd.Joints.Select((jt, ji) =>
      {
        var b = ClothInstance.FindBone(bones, jt.Name);
        var next = ji + 1 < sd.Joints.Count ? ClothInstance.FindBone(bones, sd.Joints[ji + 1].Name) : -1;
        var seg = b >= 0 && next >= 0 && IsAncestor(b, next) ? next : b >= 0 ? Child(b) : -1;
        var m = jt.Mass > 0 ? jt.Mass : 1f;
        // rea-batch PROVEN (FUN_1414828a0): per-axis RK4 on the joint position, a = -(k/m) x - d v, step h = dt * timeScale.
        // In real time that is stiffness (k/m) s^2 and damping d s (d is a plain 1/s coefficient, not a ratio).
        var w0 = MathF.Sqrt(MathF.Max(jt.Stiffness, 0) / m) * sd.TimeScale;
        return new
        {
          jt.Name,
          found = b >= 0,
          parent = b >= 0 ? BoneName(bones[b].Parent) : "",
          segmentTo = seg >= 0 ? BoneName(seg) : "",
          source = new { jt.Stiffness, jt.DampingRation, jt.Mass, jt.JointAngle, jt.Gravity, jt.IsParent, jt.IsInvertedLook, defaulted = jt.Defaulted },
          ue = new
          {
            omegaRadPerSec = w0,
            driveStiffness = w0 * w0,
            driveDamping = MathF.Max(jt.DampingRation, 0) * sd.TimeScale,
            coneDeg = SpringDamperCone(jt.JointAngle),
            gravityScale = Math.Clamp(jt.Gravity, 0f, 1f),
            massKg = m,
          },
        };
      }).ToList(),
      constrains = sd.Constrains.Select(k => new
      {
        k.UpLocatorName, k.DownLocatorName, k.HasExternalLocators, k.ResolvedFrom,
        upBone = k.UpBone, upOffsetCm = Ue(k.UpOffset), downBone = k.DownBone, downOffsetCm = Ue(k.DownOffset),
        upFound = ClothInstance.FindBone(bones, k.UpBone) >= 0,
        downFound = ClothInstance.FindBone(bones, k.DownBone) >= 0,
        radiusCm = k.Radius * 100f,
      }).ToList(),
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
      springDamperSource = setup.SpringDamperSource,
      springDamperNote = "phys52 prop_motion_damper_controller. Exe step PROVEN (rea-batch, FUN_1414828a0): each joint position follows a target on its parent; the rest direction is first bent toward world down (0,-1,0) by gravity * angle(rest, down) (gravity clamped to 1, <= 0 means none: gravity is a bend fraction, not a force), the current direction is limited to jointAngle degrees from it (jointAngle 0 = unlimited), then a per-axis RK4 spring a = -(stiffness/mass) x - dampingRation v advances with h = dt * timeScale; velocities above 2e6 or NaN reset to 0. UE mapping (INFERRED): drive stiffness (k/m) timeScale^2 = omega0^2, damping dampingRation * timeScale [1/s], swing cone = jointAngle, gravityScale = bend fraction (no UE equivalent: aim the rest pose instead). constrains = kinematic capsule up->down locator (radius) the joint bodies collide with. isParent / isInvertedLook / hasExternalLocators / windFlutter are recorded, not simulated. Not baked into clips.",
      springDampers,
    }, Options);
  }

  /// <summary>rea-batch PROVEN: FUN_1427ab3c0 clamps the swing to jointAngle degrees; jointAngle &lt;= 1e-6 skips the clamp (FUN_1427aab90).</summary>
  public static float SpringDamperCone(float jointAngle) => jointAngle <= 1e-6f ? 180f : MathF.Min(jointAngle, 180f);
}
