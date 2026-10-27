using System.Numerics;
using System.Text.Json;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Retarget;

/// <summary>
///   Retargets an SM2 humanoid clip onto the UE5 Mannequin skeleton (SK_Mannequin) in the extractor, so the anim-only FBX
///   imports with Skeleton=SK_Mannequin and plays with no IK Retargeter. The Mannequin reference pose comes from
///   <c>tools/ue/ue_dump_skeleton.py</c> (UE cm, Z-up, +Y front; the same space AxisConvert writes for ExportSpace.Unreal).
///   Method (HEURISTIC, world-delta): for a mapped bone, W_m(t) = Rref_m * A * Rref_s^-1 * R_s(t) (row vectors), where
///   A turns the Mannequin ref bone direction onto the SM2 ref bone direction (shortest arc). Unmapped bones keep their
///   Mannequin ref locals (twist and corrective bones follow their parent). Root and pelvis translations are scaled by the
///   pelvis height ratio. ik_foot_* / ik_hand_* copy the foot / hand worlds.
/// </summary>
public sealed class MannequinReference
{
  public sealed record RefBone(string Name, int Parent, Matrix4x4 Local, Matrix4x4 World);
  public List<RefBone> Bones { get; } = new();
  public string Skeleton { get; private set; } = "/Game/Characters/Mannequins/Meshes/SK_Mannequin";

  /// <summary>Built-in SK_Mannequin (UE 5.7 SKM_Manny) reference, or a dump from tools/ue/ue_dump_skeleton.py.</summary>
  public static MannequinReference Load(string? path)
  {
    string text;
    if (string.IsNullOrEmpty(path) || path == "builtin")
    {
      using var stream = typeof(MannequinReference).Assembly.GetManifestResourceStream("SK_Mannequin.ref.json")!;
      text = new StreamReader(stream).ReadToEnd();
    }
    else
      text = File.ReadAllText(path);
    using var doc = JsonDocument.Parse(text);
    var r = new MannequinReference();
    if (doc.RootElement.TryGetProperty("skeleton", out var sk))
      r.Skeleton = sk.GetString()!.Split('.')[0];
    var index = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var b in doc.RootElement.GetProperty("bones").EnumerateArray())
    {
      var name = b.GetProperty("name").GetString()!;
      var parent = b.GetProperty("parent").GetString() ?? "";
      index[name] = r.Bones.Count;
      r.Bones.Add(new RefBone(name, index.TryGetValue(parent, out var p) ? p : -1, Mirror(Trs(b.GetProperty("local"))), Mirror(Trs(b.GetProperty("world")))));
    }
    return r;
  }

  /// <summary>
  ///   UE editor space is the FBX (AxisConvert Unreal) space mirrored in X: the FBX importer flips handedness, so SM2 left
  ///   bones (FBX -X) read +X in the editor, like SK_Mannequin's _l bones. M -> S M S with S = diag(-1, 1, 1) (measured:
  ///   titus HANDL FBX x -85 vs editor +85).
  /// </summary>
  private static Matrix4x4 Mirror(Matrix4x4 m)
  {
    var s = Matrix4x4.CreateScale(-1, 1, 1);
    return s * m * s;
  }

  private static Matrix4x4 Trs(JsonElement e)
  {
    float[] F(string k) => e.GetProperty(k).EnumerateArray().Select(v => (float)v.GetDouble()).ToArray();
    var t = F("t"); var q = F("q");
    var m = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(new Quaternion(q[0], q[1], q[2], q[3])));
    m.Translation = new Vector3(t[0], t[1], t[2]);
    return m;
  }
}

public static class MannequinRetarget
{
  public const string Suffix = "_Manny";

  /// <summary>SM2 bone for a Mannequin bone, or null. Several candidates: the first present wins (gauntlet hands lack fingers 3-5).</summary>
  private static string[]? Source(string manny)
  {
    string S(string side) => side == "l" ? "L" : "R";
    switch (manny)
    {
      case "root": return new[] { "ROOT" };
      case "pelvis": return new[] { "CENTRE" };
      case "spine_01": return new[] { "BACKdown" };
      case "spine_03": return new[] { "BACKmiddle", "BACK1" };
      case "spine_05": return new[] { "BACKup" };
      case "neck_01": return new[] { "NECK" };
      case "head": return new[] { "HEAD" };
    }
    if (manny.Length < 3 || manny[^2] != '_' || (manny[^1] != 'l' && manny[^1] != 'r'))
      return null;
    var s = S(manny[^1].ToString());
    var stem = manny[..^2];
    switch (stem)
    {
      case "clavicle": return new[] { "clavicle" + s };
      case "upperarm": return new[] { "ARM1" + s };
      case "lowerarm": return new[] { "ARM2" + s };
      case "hand": return new[] { "HAND" + s };
      case "thigh": return new[] { "LEG1" + s };
      case "calf": return new[] { "LEG2" + s };
      case "foot": return new[] { "FOOT1" + s };
      case "ball": return new[] { "FOOT2" + s };
    }
    string[] fingers = { "thumb", "index", "middle", "ring", "pinky" };
    for (var f = 0; f < 5; f++)
      for (var k = 1; k <= 3; k++)
        if (stem == $"{fingers[f]}_0{k}")
        {
          var order = f switch { 0 => new[] { 1 }, 1 => new[] { 2 }, 2 => new[] { 3, 2 }, 3 => new[] { 4, 3, 2 }, _ => new[] { 5, 4, 3, 2 } };
          return order.Select(n => $"{s}{n}_fing{k}").ToArray();
        }
    return null;
  }

  private static string? IkTarget(string manny) => manny switch
  {
    "ik_foot_l" => "foot_l", "ik_foot_r" => "foot_r", "ik_hand_gun" => "hand_r", "ik_hand_l" => "hand_l", "ik_hand_r" => "hand_r", _ => null,
  };

  private static Matrix4x4 Rot(Matrix4x4 m) { m.Translation = Vector3.Zero; return m; }

  private static Matrix4x4 Inv(Matrix4x4 m) { Matrix4x4.Invert(m, out var i); return i; }

  private static Matrix4x4 Arc(Vector3 from, Vector3 to)
  {
    if (from.LengthSquared() < 1e-8f || to.LengthSquared() < 1e-8f)
      return Matrix4x4.Identity;
    from = Vector3.Normalize(from); to = Vector3.Normalize(to);
    var axis = Vector3.Cross(from, to);
    var dot = Math.Clamp(Vector3.Dot(from, to), -1f, 1f);
    if (axis.LengthSquared() < 1e-10f)
      return Matrix4x4.Identity;
    return Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(dot));
  }

  /// <summary>UE (FBX Unreal space) world back to source space: inverse of AxisConvert.ConvertWorld (the basis is its own inverse).</summary>
  private static Matrix4x4 ToSource(Matrix4x4 ue)
  {
    var basis = new Matrix4x4(-1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1);
    var t = ue.Translation;
    var m = basis * Rot(ue) * basis;
    m.Translation = new Vector3(-t.X, t.Z, t.Y) / 100f;
    return m;
  }

  private static Matrix4x4[] Worlds(IReadOnlyList<Bone> bones, Matrix4x4[] local)
  {
    // Parents can come after children (titus LEG2L is bone 4, its parent LEG1L bone 156): compose on demand.
    var w = new Matrix4x4[bones.Count];
    var done = new bool[bones.Count];
    Matrix4x4 Get(int i, int depth)
    {
      if (done[i]) return w[i];
      var p = bones[i].Parent;
      w[i] = p >= 0 && p < bones.Count && depth < 512 ? local[i] * Get(p, depth + 1) : local[i];
      done[i] = true;
      return w[i];
    }
    for (var i = 0; i < bones.Count; i++)
      Get(i, 0);
    return w;
  }

  /// <summary>Builds the Mannequin skeleton and a clip with every Mannequin bone baked. Returns null when the SM2 skeleton has no CENTRE / limbs.</summary>
  public static ExportedMesh? Build(ExportedMesh sm2, AnimationClip clip, MannequinReference reference, List<string> messages)
  {
    var sIndex = new Dictionary<string, int>(StringComparer.Ordinal);
    // Names repeat in SM2 skeletons (tpl nodes, cloth helpers): prefer the bone under CENTRE (the animated limb).
    var centreFirst = sm2.Bones.FindIndex(b => b.Name == "CENTRE");
    bool UnderCentre(int i) { for (var p = sm2.Bones[i].Parent; p >= 0; p = sm2.Bones[p].Parent) if (sm2.Bones[p].Name == "CENTRE") return true; return false; }
    var binding = ClipBinding.Resolve(sm2.Bones, clip);
    var animated = new HashSet<int>();
    for (var t = 0; t < clip.Tracks.Count; t++)
      if (clip.Tracks[t].Rotation.Count + clip.Tracks[t].Translation.Count > 0) animated.Add(binding[t]);
    int Score(int i) => (UnderCentre(i) ? 2 : 0) + (animated.Contains(i) ? 1 : 0);
    for (var i = 0; i < sm2.Bones.Count; i++)
    {
      var n = sm2.Bones[i].Name;
      if (!sIndex.TryGetValue(n, out var have) || (n != "ROOT" && n != "CENTRE" && Score(i) > Score(have)))
        sIndex[n] = i;
    }
    if (Environment.GetEnvironmentVariable("SM2_MANNY_DEBUG") == "1")
      for (var i = 0; i < sm2.Bones.Count; i++)
        if (sm2.Bones[i].Name is "LEG1L" or "LEG2L" or "FOOT1L" or "LEG2R")
          messages.Add($"DBGB {i} {sm2.Bones[i].Name} parent={(sm2.Bones[i].Parent >= 0 ? sm2.Bones[sm2.Bones[i].Parent].Name : "-")} local={sm2.Bones[i].Local.Translation} fileBind={sm2.Bones[i].FileBind} anim={animated.Contains(i)} pick={sIndex[sm2.Bones[i].Name] == i}");
    foreach (var need in new[] { "CENTRE", "LEG1L", "ARM1L", "HANDR", "FOOT1R" })
      if (!sIndex.ContainsKey(need))
      {
        messages.Add($"{clip.Name}: mannequin skipped (no {need} bone; not an SM2 humanoid).");
        return null;
      }

    var mb = reference.Bones;
    var map = new int[mb.Count];
    for (var i = 0; i < mb.Count; i++)
    {
      map[i] = -1;
      if (Source(mb[i].Name) is { } cands)
        foreach (var c in cands)
          if (sIndex.TryGetValue(c, out var si)) { map[i] = si; break; }
    }

    // SM2 ref worlds in UE space.
    var bindLocal = sm2.Bones.Select(b => b.Local).ToArray();
    var sRefSrc = Worlds(sm2.Bones, bindLocal);
    var sRef = sRefSrc.Select(w => AxisConvert.ConvertWorld(w, ExportSpace.Unreal, false)).ToArray();
    // Aim positions: the skin bind (inverse of InverseBind) when the file has one. Some limb locals (titus LEG2L) carry
    // a placeholder translation that only the skin bind corrects.
    var sAim = new Vector3[sm2.Bones.Count];
    for (var i = 0; i < sm2.Bones.Count; i++)
      sAim[i] = sm2.Bones[i].FileBind && Matrix4x4.Invert(sm2.Bones[i].InverseBind, out var bw)
        ? AxisConvert.ConvertWorld(bw, ExportSpace.Unreal, false).Translation : sRef[i].Translation;

    // Alignment per mapped bone: first mapped Mannequin descendant (depth-first order) gives the aim.
    var align = new Matrix4x4[mb.Count];
    for (var i = 0; i < mb.Count; i++)
    {
      align[i] = Matrix4x4.Identity;
      if (map[i] < 0 || mb[i].Name is "root" or "pelvis")
        continue;
      var child = -1;
      for (var j = i + 1; j < mb.Count && child < 0; j++)
        if (map[j] >= 0 && IsDescendant(mb, j, i) && map[j] != map[i]) child = j;
      if (child < 0)
        continue;
      var dm = mb[child].World.Translation - mb[i].World.Translation;
      var ds = sAim[map[child]] - sAim[map[i]];
      align[i] = Arc(Vector3.TransformNormal(dm, Matrix4x4.Identity), ds);
    }

    var pelvisM = mb.FindIndex(b => b.Name == "pelvis");
    var centre = sIndex["CENTRE"];
    var scale = pelvisM >= 0 && MathF.Abs(sRef[centre].Translation.Z) > 1f ? mb[pelvisM].World.Translation.Z / sRef[centre].Translation.Z : 1f;

    // Sample times: every key of the source clip (same grid FbxWriter would use).
    var times = new SortedSet<float>();
    foreach (var track in clip.Tracks)
    {
      foreach (var k in track.Rotation) times.Add(k.Time);
      foreach (var k in track.Translation) times.Add(k.Time);
    }
    if (clip.RootMotion)
      foreach (var k in clip.RootTranslation) times.Add(k.Time);
    if (times.Count == 0)
      return null;
    var samples = times.ToArray();

    var mesh = new ExportedMesh { Name = sm2.Name + Suffix };
    var mIndex = new Dictionary<string, int>(StringComparer.Ordinal);
    var mRefSrc = mb.Select(b => ToSource(b.World)).ToArray();
    for (var i = 0; i < mb.Count; i++)
    {
      mIndex[mb[i].Name] = i;
      var p = mb[i].Parent;
      var local = p >= 0 ? mRefSrc[i] * Inv(mRefSrc[p]) : mRefSrc[i];
      mesh.Bones.Add(new Bone { Name = mb[i].Name, Parent = p, Local = local, InverseBind = Inv(mRefSrc[i]), FileBind = true });
    }

    var baked = new BakedBones { Times = samples };
    var frames = mb.Select(_ => new Matrix4x4[samples.Length]).ToArray();
    var src = new Matrix4x4[sm2.Bones.Count];
    var world = new Matrix4x4[mb.Count];
    var root = sIndex.TryGetValue("ROOT", out var r0) ? r0 : -1;
    for (var f = 0; f < samples.Length; f++)
    {
      ClipPose.Apply(sm2.Bones, clip, samples[f], src);
      var sw = Worlds(sm2.Bones, src).Select(w => AxisConvert.ConvertWorld(w, ExportSpace.Unreal, false)).ToArray();
      for (var i = 0; i < mb.Count; i++)
      {
        var p = mb[i].Parent;
        var fk = p >= 0 ? mb[i].Local * world[p] : mb[i].Local;
        Matrix4x4 w;
        if (map[i] >= 0)
        {
          var s = map[i];
          var delta = Inv(Rot(sRef[s])) * Rot(sw[s]);
          w = Rot(mb[i].World) * align[i] * delta;
          if (mb[i].Name == "root")
            w.Translation = sw[s].Translation * scale;
          else if (mb[i].Name == "pelvis")
            w.Translation = mb[i].World.Translation + (sw[s].Translation - sRef[s].Translation) * scale;
          else
            w.Translation = fk.Translation;
        }
        else if (IkTarget(mb[i].Name) is { } target && mIndex.TryGetValue(target, out var ti) && ti < i)
          w = world[ti];
        else
          w = fk;
        world[i] = w;
      }

      if (f == 0 && Environment.GetEnvironmentVariable("SM2_MANNY_DEBUG") == "1")
        for (var i = 0; i < mb.Count; i++)
          if (map[i] >= 0)
            messages.Add($"DBG {mb[i].Name} m={world[i].Translation} s={sw[map[i]].Translation * scale} sref={sRef[map[i]].Translation} mref={mb[i].World.Translation}");
      for (var i = 0; i < mb.Count; i++)
      {
        var p = mb[i].Parent;
        var ws = ToSource(world[i]);
        frames[i][f] = p >= 0 ? ws * Inv(ToSource(world[p])) : ws;
      }
    }

    for (var i = 0; i < mb.Count; i++)
      baked.Local[mb[i].Name] = frames[i];

    var outClip = new AnimationClip { Name = clip.Name, Duration = clip.Duration, SampleRate = clip.SampleRate, RootMotion = false, RebaseFirstFrame = false, Baked = baked };
    var dummy = new BoneTrack { Bone = 0, BoneName = mb[0].Name };
    for (var f = 0; f < samples.Length; f++)
      dummy.Rotation.Add(new QuatKey { Time = samples[f], Value = Quaternion.CreateFromRotationMatrix(Rot(frames[0][f])) });
    outClip.Tracks.Add(dummy);
    outClip.Bones.AddRange(mb.Select(b => b.Name));
    mesh.Clip = outClip;
    var mapped = map.Count(m => m >= 0);
    messages.Add($"{clip.Name}: mannequin retarget {mapped}/{mb.Count} bones mapped, pelvis scale {scale:0.###} (HEURISTIC world-delta).");
    return mesh;
  }

  private static bool IsDescendant(List<MannequinReference.RefBone> bones, int bone, int ancestor)
  {
    for (var p = bones[bone].Parent; p >= 0; p = bones[p].Parent)
      if (p == ancestor) return true;
    return false;
  }
}
