using System.Numerics;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Physics;

/// <summary>One cloth helper bone: a rigid cluster of free sim particles that the render mesh is skinned to.</summary>
public sealed class ClothBoneCluster
{
  public required string Cloth { get; init; }
  public required int Bone { get; init; }
  public required int Parent { get; init; }
  public required int[] Particles { get; init; }
  public required Vector3 BindCenter { get; init; }
  /// <summary>Particles the rotation is fitted on: <see cref="Particles"/> plus their one-ring sim neighbours, so a
  ///   small or collinear cluster still gets the local cloth frame.</summary>
  public int[]? FitParticles { get; init; }
}

/// <summary>What <see cref="PhysicsBake.AddClothBones"/> added to the export skeleton.</summary>
public sealed class ClothBoneRig
{
  public List<ClothBoneCluster> Clusters { get; } = new();
  public int SkinnedVertices { get; set; }
  public List<string> Notes { get; } = new();
}

/// <summary>Result of baking one clip.</summary>
public sealed class PhysicsBakeReport
{
  public int Frames { get; set; }
  public int JiggleBones { get; set; }
  public int ClothBones { get; set; }
  /// <summary>Largest swing of a jiggle bone away from its animated orientation, degrees.</summary>
  public float MaxJiggleDegrees { get; set; }
  public float MaxClusterFitError { get; set; }
  public double MeanStepMs { get; set; }
  /// <summary>mtl1-43: the clip's first and last poses match (<see cref="PhysicsBake.IsLoop"/>), so it was baked loop-seamless.</summary>
  public bool Loop { get; set; }
  /// <summary>Full clip cycles simulated before recording (loop clips only).</summary>
  public int WarmupCycles { get; set; }
  /// <summary>Largest last-vs-first frame mismatch of a baked bone before the seam correction, degrees / cm.</summary>
  public float SeamDegrees { get; set; }
  public float SeamCm { get; set; }
}

/// <summary>
///   Carries the game's secondary motion into exported animation files, so any importer (Unreal, Blender) plays it
///   without a runtime solver:
///   <list type="bullet">
///     <item>Jiggle bones: the exact jiggle port (<see cref="JiggleObject"/>, loop 0x14147e5b5) is run over the clip at
///     60 Hz and the result is written as those bones' local curves.</item>
///     <item>Cloth: the free sim particles of each cloth are grouped into rigid clusters (default one particle each, rotation fitted over its one-ring), one helper bone each
///     (<see cref="AddClothBones"/>). The <c>_REND</c> vertices the cloth drives are re-skinned to those bones with the same
///     closest-triangle weight the preview uses, and every frame the cluster's rigid best fit (polar decomposition of the
///     particle covariance) is baked as the helper bone's curve.</item>
///   </list>
///   The simulation is the one the browser preview runs (<see cref="PhysicsRuntime"/>), so the export matches it.
/// </summary>
public static class PhysicsBake
{
  public const float Rate = 60f;
  /// <summary>Frame 0 held this long before recording, so the springs and cloth start settled (no drop at frame 0).</summary>
  public const float Preroll = 1.5f;
  /// <summary>
  ///   Default cap of helper bones per cloth. mtl1-42: 4096 (was 1024) so every free particle of the largest retail cloth
  ///   gets its own bone (calgar cloak_SIM: 1133 free particles; 1024 merged 109 of them into 2-particle clusters, fit
  ///   error up to 3.9 cm at the cloak hem). UE skeletal meshes take &gt;256 bones per section with 16-bit indices.
  /// </summary>
  public const int MaxBonesPerCloth = 4096;
  public const string ClothBoneSuffix = "_phys";

  /// <summary>
  ///   Adds helper bones for every cloth of <paramref name="setup"/> found in <paramref name="mesh"/> and re-skins the
  ///   driven render vertices. Call once before the bind mesh is written; every clip then bakes the same bones.
  /// </summary>
  /// <param name="particlesPerBone">Target free sim particles per helper bone (fewer = closer to the sim, more bones).</param>
  public static ClothBoneRig AddClothBones(ExportedMesh mesh, PhysicsSetup setup, int particlesPerBone = 1, int maxBonesPerCloth = MaxBonesPerCloth)
  {
    var rig = new ClothBoneRig();
    if (mesh.Bones.Count == 0 || !mesh.HasSkin)
      return rig;
    var bindWorld = BindWorlds(mesh.Bones);
    foreach (var entry in setup.Cloths)
    {
      var shell = mesh.ClothSims.FirstOrDefault(s => string.Equals(s.Name, entry.SimMeshName, StringComparison.OrdinalIgnoreCase));
      if (shell is null || shell.Positions.Count < 3 || shell.Indices.Count < 3)
      {
        rig.Notes.Add($"{entry.SimMeshName}: no sim shell in the mesh, cloth not baked");
        continue;
      }

      // Every vertex pool that draws the cloth: the base buffer and each LOD level (cc_calgar.tpl keeps its drawn
      // cloak in a level, the base buffer's parts are the _LOD1 copies).
      var pools = new List<(IReadOnlyList<Vector3> Positions, IReadOnlyList<int> Indices, List<SkinWeights> Skin, List<MeshPart> Parts)>
      {
        (mesh.Positions, mesh.Indices, mesh.Skin, entry.DrivenParts(mesh.Parts, p => p.Name)),
      };
      foreach (var level in mesh.Levels)
        pools.Add((level.Positions, level.Indices, level.Skin, entry.DrivenParts(level.Parts, p => p.Name)));
      pools.RemoveAll(pool => pool.Parts.Count == 0 || pool.Skin.Count == 0);
      if (pools.Count == 0)
      {
        rig.Notes.Add($"{entry.SimMeshName}: render meshes {string.Join(", ", entry.RenderMeshes)} not exported, cloth not baked");
        continue;
      }

      var bind = shell.Positions.ToArray();
      var free = Enumerable.Range(0, bind.Length).Where(i => (i < shell.Colors.Count ? shell.Colors[i].X : 1f) > 1e-4f).ToArray();
      if (free.Length == 0)
        continue;
      var count = Math.Clamp(free.Length / Math.Max(1, particlesPerBone), 1, maxBonesPerCloth);
      var seeds = FarthestPoints(bind, free, count);
      var owner = new int[bind.Length];
      Array.Fill(owner, -1);
      var members = seeds.Select(_ => new List<int>()).ToArray();
      foreach (var i in free)
      {
        var best = 0;
        var bestD = float.MaxValue;
        for (var s = 0; s < seeds.Length; s++)
        {
          var d = Vector3.DistanceSquared(bind[i], bind[seeds[s]]);
          if (d < bestD)
          {
            bestD = d;
            best = s;
          }
        }

        members[best].Add(i);
      }

      var ring = Enumerable.Range(0, bind.Length).Select(_ => new HashSet<int>()).ToArray();
      for (var t = 0; t + 2 < shell.Indices.Count; t += 3)
        for (var e = 0; e < 3; e++)
        {
          int a = shell.Indices[t + e], b = shell.Indices[t + (e + 1) % 3];
          if (a < bind.Length && b < bind.Length)
          {
            ring[a].Add(b);
            ring[b].Add(a);
          }
        }

      var clusterBone = new int[seeds.Length];
      for (var s = 0; s < seeds.Length; s++)
      {
        var list = members[s];
        if (list.Count == 0)
        {
          clusterBone[s] = -1;
          continue;
        }

        var center = list.Aggregate(Vector3.Zero, (a, i) => a + bind[i]) / list.Count;
        var parent = DominantBone(shell, list, mesh.Bones.Count);
        var world = Matrix4x4.CreateTranslation(center);
        Matrix4x4.Invert(bindWorld[parent], out var parentInverse);
        Matrix4x4.Invert(world, out var inverseBind);
        var index = mesh.Bones.Count;
        mesh.Bones.Add(new Bone
        {
          Name = $"{Sanitize(entry.SimMeshName)}{ClothBoneSuffix}{s:00}",
          Parent = parent,
          Local = world * parentInverse,
          InverseBind = inverseBind,
          Influences = true,
          FileBind = true,
        });
        bindWorld = bindWorld.Append(world).ToArray();
        clusterBone[s] = index;
        foreach (var i in list)
          owner[i] = index;
        rig.Clusters.Add(new ClothBoneCluster { Cloth = entry.SimMeshName, Bone = index, Parent = parent, Particles = list.ToArray(), BindCenter = center,
          FitParticles = list.Concat(list.SelectMany(i => ring[i])).Distinct().ToArray() });
      }

      var simTris = shell.Indices.ToArray();
      foreach (var pool in pools)
        rig.SkinnedVertices += Reskin(pool.Positions, pool.Indices, pool.Skin, pool.Parts, entry, bind, simTris, owner);
    }

    return rig;
  }

  /// <summary>
  ///   Runs the preview simulation over <paramref name="clip"/> and stores the jiggle and cloth-helper locals in
  ///   <see cref="AnimationClip.Baked"/>. The clip's rebase and root-motion settings must already be set.
  /// </summary>
  public static PhysicsBakeReport Bake(ExportedMesh mesh, AnimationClip clip, PhysicsSetup setup, ClothBoneRig? rig, float preroll = Preroll)
  {
    var report = new PhysicsBakeReport();
    clip.Baked = null;
    var previous = mesh.Clip;
    mesh.Clip = clip;
    try
    {
      var runtime = new PhysicsRuntime(mesh, setup);
      var bones = mesh.Bones;
      var duration = Math.Max(clip.Duration, 1f / Rate);
      var frames = (int)MathF.Ceiling(duration * Rate) + 1;
      var times = new float[frames];
      for (var f = 0; f < frames; f++)
        times[f] = Math.Min(duration, f / Rate);
      var baked = new BakedBones { Times = times };
      var jiggleBones = setup.Jiggles.Select(j => ClothInstance.FindBone(bones, j.Name)).Where(b => b >= 0).Distinct().ToArray();
      var tracks = new Dictionary<int, Matrix4x4[]>();
      foreach (var b in jiggleBones)
        tracks[b] = new Matrix4x4[frames];
      var clusters = rig?.Clusters ?? new List<ClothBoneCluster>();
      foreach (var c in clusters)
        tracks[c.Bone] = new Matrix4x4[frames];
      var cloths = runtime.Cloths.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
      var rotation = clusters.ToDictionary(c => c.Bone, _ => Quaternion.Identity);

      // mtl1-43 hem spikes: a one-particle cluster's rotation is fitted on its 3..7-particle one-ring, which the cloth
      // crumples by ~1 cm on a 3 cm ring in a run, so the unregularised fit hopped up to 180 deg between frames (titus
      // run: 330 of 956 helpers stepped > 90 deg in one frame) and swung the re-skinned _REND vertices into spikes.
      // The first fit of a cluster converges without a prior (no identity bias); every later fit adds FitPrior x the
      // ring's bind energy of the previous rotation to the covariance (a temporal prior, Müller 2016 warm start + damping).
      var fitted = new HashSet<int>();
      Matrix4x4 FitCluster(ClothBoneCluster c, ClothInstance cloth)
      {
        var particles = c.FitParticles ?? c.Particles;
        Matrix4x4 m;
        Quaternion q;
        if (fitted.Add(c.Bone))
        {
          q = rotation[c.Bone];
          m = Matrix4x4.Identity;
          for (var pass = 0; pass < 8; pass++)
            m = Fit(cloth.BindPositions, cloth.Positions, particles, c.BindCenter, q, out q, out _);
        }
        else
        {
          var before = rotation[c.Bone];
          m = Fit(cloth.BindPositions, cloth.Positions, particles, c.BindCenter, before, out q, out _, FitPrior);
          // mtl1-44 fast-run hem spikes: the prior still let a crumpled ring flip a helper 100-170 deg in one 60 Hz frame
          // (calgar shield run: 12 helpers > 90 deg). Cap the per-frame rotation step and re-place the bone about the
          // bind center with the capped rotation (centroid kept, so translation is unchanged).
          if (MaxFitStepDegrees > 0 && Angle(Quaternion.Normalize(before), Quaternion.Normalize(q)) > MaxFitStepDegrees)
          {
            var b = Quaternion.Normalize(before);
            var n = Quaternion.Normalize(q);
            if (Quaternion.Dot(b, n) < 0) n = -n;
            var capped = Quaternion.Normalize(Quaternion.Slerp(b, n, MaxFitStepDegrees / Angle(b, n)));
            // m = R(q) about the bind center then translated: keep the bind-center image, swap the rotation.
            m = Matrix4x4.CreateFromQuaternion(capped) * Matrix4x4.CreateTranslation(m.Translation + Vector3.Transform(Fit0(cloth, particles, c.BindCenter), q) - Vector3.Transform(Fit0(cloth, particles, c.BindCenter), capped));
            q = capped;
          }
        }

        rotation[c.Bone] = q;
        return m;
      }

      var first = MeshPreview.BoneWorlds(mesh, 0);
      // Preroll: hold frame 0's pose so the springs and cloth settle, then play the clip right after it.
      var prerollSteps = (int)MathF.Round(Math.Max(0, preroll) * Rate);
      double total = 0;
      for (var p = 0; p < prerollSteps; p++)
      {
        runtime.Step((Matrix4x4[])first.Clone(), p / Rate, 0);
        total += runtime.LastStepMs;
      }

      var start = prerollSteps / Rate;
      // mtl1-43 loop-seamless bake: a looping clip (first pose == last pose) is played WarmupCycles times before the
      // recorded cycle, each warm-up pass offset by the clip's root delta so it ends exactly where the next pass starts
      // (no teleport), so the recorded cycle begins in the periodic state instead of the settled frame-0 state. The small
      // remaining last-vs-first mismatch is then distributed linearly over the cycle (see SeamCorrect).
      var loop = Environment.GetEnvironmentVariable("SM2_PHYS_LOOP") switch { "0" => false, "1" => true, _ => IsLoop(mesh, clip) };
      report.Loop = loop;
      if (loop && frames > 2)
      {
        var end = MeshPreview.BoneWorlds(mesh, duration);
        var rootBone = MeshPreview.RootMotionBone(mesh) is >= 0 and var rm ? rm : RootBone(bones);
        Matrix4x4.Invert(end[rootBone], out var endInverse);
        var offset = endInverse * first[rootBone];
        for (var cycle = 0; cycle < WarmupCycles; cycle++)
        {
          for (var f = 0; f < frames - 1; f++)
          {
            var worlds = MeshPreview.BoneWorlds(mesh, times[f]);
            for (var b = 0; b < worlds.Length; b++)
              worlds[b] *= offset;
            runtime.Step(worlds, start + times[f], 0);
            total += runtime.LastStepMs;
            foreach (var c in clusters)
              if (cloths.TryGetValue(c.Cloth, out var cloth))
                FitCluster(c, cloth);
          }

          start += duration;
        }

        report.WarmupCycles = WarmupCycles;
      }

      for (var f = 0; f < frames; f++)
      {
        var worlds = MeshPreview.BoneWorlds(mesh, times[f]);
        var before = jiggleBones.Select(b => Rotation(worlds[b])).ToArray();
        runtime.Step(worlds, start + times[f], 0);
        total += runtime.LastStepMs;
        for (var k = 0; k < jiggleBones.Length; k++)
        {
          var b = jiggleBones[k];
          var dot = Math.Min(1f, MathF.Abs(Quaternion.Dot(before[k], Rotation(worlds[b]))));
          report.MaxJiggleDegrees = Math.Max(report.MaxJiggleDegrees, 2 * MathF.Acos(dot) * 180 / MathF.PI);
          tracks[b][f] = LocalOf(worlds, bones, b);
        }

        foreach (var c in clusters)
        {
          if (!cloths.TryGetValue(c.Cloth, out var cloth))
          {
            tracks[c.Bone][f] = bones[c.Bone].Local;
            continue;
          }

          var fit = FitCluster(c, cloth);
          // Land the cluster's own centroid exactly on its simulated centroid (the ring only steers the rotation).
          var centroid = Vector3.Zero;
          foreach (var i in c.Particles)
            centroid += cloth.Positions[i];
          centroid /= c.Particles.Length;
          fit.Translation = centroid;
          var error = 0f;
          foreach (var i in c.Particles)
            error = Math.Max(error, Vector3.Distance(Vector3.Transform(cloth.BindPositions[i] - c.BindCenter, fit), cloth.Positions[i]));
          report.MaxClusterFitError = Math.Max(report.MaxClusterFitError, error);
          var parentWorld = c.Parent >= 0 ? worlds[c.Parent] : Matrix4x4.Identity;
          Matrix4x4.Invert(parentWorld, out var parentInverse);
          tracks[c.Bone][f] = fit * parentInverse;
        }
      }

      if (loop && frames > 2)
        SeamCorrect(tracks.Values, report);
      report.Frames = frames;
      report.JiggleBones = jiggleBones.Length;
      report.ClothBones = clusters.Count;
      report.MeanStepMs = total / Math.Max(1, frames + prerollSteps + report.WarmupCycles * (frames - 1));
      foreach (var (bone, frameLocals) in tracks)
        baked.Local[bones[bone].Name] = frameLocals;
      clip.Baked = baked;
    }
    finally
    {
      mesh.Clip = previous;
    }

    return report;
  }

  /// <summary>
  ///   mtl1-44: largest helper rotation step per baked frame (60 Hz). 30 deg = 1800 deg/s; the titus/calgar runs only exceed
  ///   it in the fit flips. APPROXIMATION (generic); SM2_PHYS_FIT_MAXSTEP overrides, 0 = no cap.
  /// </summary>
  public static readonly float MaxFitStepDegrees = float.TryParse(Environment.GetEnvironmentVariable("SM2_PHYS_FIT_MAXSTEP"),
    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var step) ? step : 30f;

  /// <summary>Fit-particle bind centroid relative to the bind center (the point the fit keeps on the simulated centroid).</summary>
  private static Vector3 Fit0(ClothInstance cloth, int[] particles, Vector3 bindCenter)
  {
    var cb = Vector3.Zero;
    foreach (var i in particles)
      cb += cloth.BindPositions[i];
    return cb / particles.Length - bindCenter;
  }

  /// <summary>Warm-up cycles of a loop bake (mtl1-43). Two cycles bring the calgar_run cloak seam under 1 degree.</summary>
  public const int WarmupCycles = 2;
  /// <summary>
  ///   Temporal prior weight of the cluster rotation fit (mtl1-43). 0.25 cut the titus run helpers stepping > 90 deg per
  ///   frame 330 -> 7 (> 30 deg 645 -> 228) for +0.25 cm mean ring fit RMS (0.76 -> 1.00 cm). APPROXIMATION: the game skins
  ///   _REND to the sim triangles directly; SM2_PHYS_FITPRIOR overrides (0 = the old unregularised fit).
  /// </summary>
  public static readonly float FitPrior = float.TryParse(Environment.GetEnvironmentVariable("SM2_PHYS_FITPRIOR"),
    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var prior) ? prior : 0.25f;
  /// <summary>Loop test tolerances: non-root bone locals at t = duration within this of t = 0 (up to 2 % of them may be off by &lt;= 30 deg / 5 cm).</summary>
  public const float LoopDegrees = 3f;
  public const float LoopMeters = 0.01f;

  /// <summary>
  ///   mtl1-43: a clip is a loop when every animated non-root bone's local transform at the end equals the one at the
  ///   start (within <see cref="LoopDegrees"/> / <see cref="LoopMeters"/>). Root motion is excluded (a run cycle moves).
  ///   HEURISTIC: the .sani has no loop flag we decode, so the pose match stands in for it. SM2_PHYS_LOOP=0/1 overrides.
  /// </summary>
  public static bool IsLoop(ExportedMesh mesh, AnimationClip clip)
  {
    if (clip.Duration <= 2f / Rate || !clip.HasCurves)
      return false;
    var previous = mesh.Clip;
    mesh.Clip = clip;
    try
    {
      var a = MeshPreview.BoneWorlds(mesh, 0);
      var b = MeshPreview.BoneWorlds(mesh, clip.Duration);
      var bones = mesh.Bones;
      var names = new HashSet<string>(clip.Tracks.Where(t => t.Rotation.Count + t.Translation.Count > 1).Select(t => t.BoneName), StringComparer.OrdinalIgnoreCase);
      var animated = new HashSet<int>(Enumerable.Range(0, bones.Count).Where(i => names.Contains(bones[i].Name)));
      int checkedBones = 0, off = 0;
      for (var i = 0; i < bones.Count && i < a.Length; i++)
      {
        var parent = bones[i].Parent;
        if (parent < 0 || !animated.Contains(i))
          continue;
        var la = LocalOf(a, bones, i);
        var lb = LocalOf(b, bones, i);
        var degrees = Angle(Rotation(la), Rotation(lb));
        var meters = Vector3.Distance(la.Translation, lb.Translation);
        checkedBones++;
        if (degrees <= LoopDegrees && meters <= LoopMeters)
          continue;
        // A few prop/IK helpers (tacticus_run_fwd: GUN_L/GUN_R 10 deg, 1.3 cm) do not close; the body does.
        if (degrees > 30f || meters > 0.05f)
          return false;
        off++;
      }

      return checkedBones > 0 && off <= Math.Max(1, checkedBones / 50);
    }
    finally
    {
      mesh.Clip = previous;
    }
  }

  private static int RootBone(List<Bone> bones)
  {
    var root = bones.FindIndex(b => b.Parent < 0);
    return root < 0 ? 0 : root;
  }

  private static float Angle(Quaternion a, Quaternion b)
    => 2 * MathF.Acos(Math.Min(1f, MathF.Abs(Quaternion.Dot(a, b)))) * 180 / MathF.PI;

  /// <summary>
  ///   Makes the last baked frame equal the first: the last-vs-first delta (rotation and translation) is ramped in
  ///   linearly over the cycle, w = f / (n - 1), so no frame moves by more than the residual over the cycle length.
  /// </summary>
  internal static void SeamCorrect(IEnumerable<Matrix4x4[]> tracks, PhysicsBakeReport report)
  {
    foreach (var track in tracks)
    {
      var n = track.Length;
      if (n < 3)
        continue;
      Matrix4x4.Decompose(track[0], out _, out var q0, out var t0);
      Matrix4x4.Decompose(track[n - 1], out _, out var q1, out var t1);
      q0 = Quaternion.Normalize(q0);
      q1 = Quaternion.Normalize(q1);
      if (Quaternion.Dot(q0, q1) < 0)
        q0 = -q0;
      report.SeamDegrees = Math.Max(report.SeamDegrees, Angle(q0, q1));
      report.SeamCm = Math.Max(report.SeamCm, Vector3.Distance(t0, t1) * 100);
      var correction = Quaternion.Normalize(Quaternion.Concatenate(Quaternion.Inverse(q1), q0));
      var shift = t0 - t1;
      for (var f = 1; f < n; f++)
      {
        var w = f / (float)(n - 1);
        Matrix4x4.Decompose(track[f], out var scale, out var q, out var t);
        q = Quaternion.Normalize(Quaternion.Concatenate(Quaternion.Normalize(q), Quaternion.Slerp(Quaternion.Identity, correction, w)));
        track[f] = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(t + shift * w);
      }

      track[n - 1] = track[0];
    }
  }

  private static Quaternion Rotation(Matrix4x4 m)
  {
    Matrix4x4.Decompose(m, out _, out var q, out _);
    return Quaternion.Normalize(q);
  }

  private static Matrix4x4 LocalOf(Matrix4x4[] worlds, List<Bone> bones, int b)
  {
    var parent = bones[b].Parent;
    if (parent < 0 || parent >= worlds.Length)
      return worlds[b];
    Matrix4x4.Invert(worlds[parent], out var inverse);
    return worlds[b] * inverse;
  }

  /// <summary>
  ///   Rigid best fit of the bind cluster onto the simulated particles: rotation from the polar decomposition of the
  ///   covariance (Müller et al. 2016, iterative, warm-started from the last frame), translation = particle centroid.
  ///   Returns the helper bone's world matrix (bind world = translation to <paramref name="bindCenter"/>).
  /// </summary>
  internal static Matrix4x4 Fit(Vector3[] bind, Vector3[] current, int[] particles, Vector3 bindCenter, Quaternion start, out Quaternion q, out float error, float prior = 0f)
  {
    var cb = Vector3.Zero;
    var cx = Vector3.Zero;
    foreach (var i in particles)
    {
      cb += bind[i];
      cx += current[i];
    }

    cb /= particles.Length;
    cx /= particles.Length;
    // A = sum (x - cx) (b - cb)^T in column-vector terms; columns a0..a2.
    var a0 = Vector3.Zero;
    var a1 = Vector3.Zero;
    var a2 = Vector3.Zero;
    foreach (var i in particles)
    {
      var x = current[i] - cx;
      var b = bind[i] - cb;
      a0 += x * b.X;
      a1 += x * b.Y;
      a2 += x * b.Z;
    }

    if (prior > 0)
    {
      // Temporal prior: maximise tr(R^T (A + k R_start)), k = prior x sum |b - cb|^2 (scale-free weight).
      var energy = 0f;
      foreach (var i in particles)
        energy += (bind[i] - cb).LengthSquared();
      var k = prior * energy;
      a0 += Vector3.Transform(Vector3.UnitX, start) * k;
      a1 += Vector3.Transform(Vector3.UnitY, start) * k;
      a2 += Vector3.Transform(Vector3.UnitZ, start) * k;
    }

    q = start;
    for (var it = 0; it < 30; it++)
    {
      var r0 = Vector3.Transform(Vector3.UnitX, q);
      var r1 = Vector3.Transform(Vector3.UnitY, q);
      var r2 = Vector3.Transform(Vector3.UnitZ, q);
      var omega = (Vector3.Cross(r0, a0) + Vector3.Cross(r1, a1) + Vector3.Cross(r2, a2))
        / (MathF.Abs(Vector3.Dot(r0, a0) + Vector3.Dot(r1, a1) + Vector3.Dot(r2, a2)) + 1e-9f);
      var w = omega.Length();
      if (w < 1e-9f)
        break;
      q = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(omega / w, w) * q);
    }

    // World: rotate about the bind center, land the cluster centroid on the simulated centroid.
    var rotation = Matrix4x4.CreateFromQuaternion(q);
    var offset = Vector3.Transform(cb - bindCenter, q);
    var world = rotation * Matrix4x4.CreateTranslation(cx - offset);
    error = 0;
    foreach (var i in particles)
      error = Math.Max(error, Vector3.Distance(Vector3.Transform(bind[i] - bindCenter, world), current[i]));
    return world;
  }

  private static int Reskin(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices, List<SkinWeights> skin, List<MeshPart> parts, ClothEntry entry, Vector3[] sim, int[] tris, int[] owner)
  {
    var vertices = new SortedSet<int>();
    foreach (var part in parts)
    {
      var end = Math.Min(indices.Count, part.IndexStart + part.IndexCount);
      for (var i = Math.Max(0, part.IndexStart); i < end; i++)
        vertices.Add(indices[i]);
    }

    var maxInfluence = Math.Max(1e-4f, entry.MaxInfluenceDist);
    var power = Math.Max(0.1f, entry.DistanceRemapPower);
    var changed = 0;
    foreach (var v in vertices)
    {
      if (v >= skin.Count || v >= positions.Count)
        continue;
      var p = positions[v];
      var best = float.MaxValue;
      var bestTri = -1;
      var bestBary = Vector3.Zero;
      for (var t = 0; t + 2 < tris.Length; t += 3)
      {
        var q = PhysicsRuntime.ClosestOnTriangle(p, sim[tris[t]], sim[tris[t + 1]], sim[tris[t + 2]], out var bc);
        var d = (p - q).LengthSquared();
        if (d < best)
        {
          best = d;
          bestTri = t;
          bestBary = bc;
        }
      }

      if (bestTri < 0)
        continue;
      var distance = MathF.Sqrt(best);
      var weight = distance <= maxInfluence ? 1f : MathF.Pow(maxInfluence / distance, power);
      var helper = new Dictionary<int, float>();
      void Add(int corner, float bary)
      {
        var bone = owner[tris[bestTri + corner]];
        if (bone >= 0 && bary > 0)
          helper[bone] = helper.GetValueOrDefault(bone) + bary * weight;
      }

      Add(0, bestBary.X);
      Add(1, bestBary.Y);
      Add(2, bestBary.Z);
      var helperTotal = helper.Values.Sum();
      if (helperTotal <= 1e-4f)
        continue;
      var keep = 1f - helperTotal;
      var s = skin[v];
      var all = new Dictionary<int, float>(helper);
      void Keep(ushort j, float w)
      {
        if (w > 0 && keep > 0)
          all[j] = all.GetValueOrDefault(j) + w * keep;
      }

      Keep(s.J0, s.W0);
      Keep(s.J1, s.W1);
      Keep(s.J2, s.W2);
      Keep(s.J3, s.W3);
      var top = all.OrderByDescending(kv => kv.Value).Take(4).ToArray();
      var sum = top.Sum(kv => kv.Value);
      var result = new SkinWeights();
      for (var k = 0; k < top.Length; k++)
      {
        var j = (ushort)top[k].Key;
        var w = top[k].Value / sum;
        switch (k)
        {
          case 0: result.J0 = j; result.W0 = w; break;
          case 1: result.J1 = j; result.W1 = w; break;
          case 2: result.J2 = j; result.W2 = w; break;
          default: result.J3 = j; result.W3 = w; break;
        }
      }

      skin[v] = result;
      changed++;
    }

    return changed;
  }

  private static int DominantBone(ClothSimShell shell, List<int> particles, int boneCount)
  {
    var votes = new Dictionary<int, float>();
    foreach (var i in particles)
    {
      if (i >= shell.Skin.Count)
        continue;
      var s = shell.Skin[i];
      void Vote(ushort j, float w)
      {
        if (w > 0 && j < boneCount)
          votes[j] = votes.GetValueOrDefault(j) + w;
      }

      Vote(s.J0, s.W0);
      Vote(s.J1, s.W1);
      Vote(s.J2, s.W2);
      Vote(s.J3, s.W3);
    }

    return votes.Count == 0 ? 0 : votes.MaxBy(kv => kv.Value).Key;
  }

  private static int[] FarthestPoints(Vector3[] points, int[] candidates, int count)
  {
    var centroid = candidates.Aggregate(Vector3.Zero, (a, i) => a + points[i]) / candidates.Length;
    var first = candidates.MinBy(i => Vector3.DistanceSquared(points[i], centroid));
    var seeds = new List<int> { first };
    var distance = candidates.ToDictionary(i => i, i => Vector3.DistanceSquared(points[i], points[first]));
    while (seeds.Count < count)
    {
      var next = distance.MaxBy(kv => kv.Value);
      if (next.Value <= 1e-12f)
        break;
      seeds.Add(next.Key);
      foreach (var i in candidates)
        distance[i] = Math.Min(distance[i], Vector3.DistanceSquared(points[i], points[next.Key]));
    }

    return seeds.ToArray();
  }

  internal static Matrix4x4[] BindWorlds(List<Bone> bones)
  {
    var worlds = new Matrix4x4[bones.Count];
    var done = new bool[bones.Count];
    Matrix4x4 Get(int i, int guard)
    {
      if (done[i])
        return worlds[i];
      var parent = bones[i].Parent;
      worlds[i] = parent >= 0 && parent < bones.Count && guard < bones.Count ? bones[i].Local * Get(parent, guard + 1) : bones[i].Local;
      done[i] = true;
      return worlds[i];
    }

    for (var i = 0; i < bones.Count; i++)
      Get(i, 0);
    return worlds;
  }

  private static string Sanitize(string name)
  {
    var chars = name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
    return new string(chars);
  }
}
