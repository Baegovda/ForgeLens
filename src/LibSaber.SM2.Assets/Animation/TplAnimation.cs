using System.Numerics;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SpaceMarine2.Structures;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>One named sequence of a template's embedded track (<c>animTPL.trackAnim.seqList</c>).</summary>
/// <param name="Index">Position in <c>seqList</c>.</param>
/// <param name="Seconds"><c>timeSec</c>: the play length. The rate is per sequence (<see cref="Fps" />), not a fixed fps:
///   the chainsword's <c>chain_anim</c>..<c>chain_anim4</c> are the same frames 0-126 at 3 / 2 / 1 / 0.75 s.</param>
public sealed record TplSequence(int Index, string Name, uint Layer, float StartFrame, float EndFrame, float Seconds)
{
  public float Frames => Math.Max(0f, EndFrame - StartFrame);
  public float Fps => Seconds > 1e-4f && Frames > 0 ? Frames / Seconds : TplAnimation.DefaultFps;
  public float Duration => Seconds > 1e-4f ? Seconds : Frames / TplAnimation.DefaultFps;
}

/// <summary>One play of a sequence on a timeline (<see cref="TplAnimation.Compose" />).</summary>
/// <param name="Start">Timeline second the sequence starts.</param>
/// <param name="Loop">Wrap (game <c>cycle</c>/<c>loop</c>); else clamp on the last frame.</param>
/// <param name="Hold">Non-looping: keep the last frame until <paramref name="End" /> (else the play ends with the sequence and
///   whatever was playing under it shows again).</param>
/// <param name="OnlyMoving">Drive only the objects the sequence moves within its frame window (sequences on different
///   layers share objects: the chainsword strikes and the teeth spin).</param>
public sealed record TplPlay(TplSequence Seq, float Start, bool Loop = false, float End = float.PositiveInfinity, bool Hold = false,
  bool OnlyMoving = true, string Why = "", IReadOnlySet<int>? Mask = null)
{
  /// <summary>The play may drive object <paramref name="index" /> (<see cref="Mask" />: the class <c>anim.layerList</c> root subtree).</summary>
  public bool Allows(int index) => Mask is null || Mask.Contains(index);
}

/// <summary>
///   propanim: decoder for the animation embedded in a template (<c>animTPL.trackAnim</c>: <c>seqList</c>,
///   <c>objAnimList</c> per <c>animNmb</c>, <c>rootAnim</c>), as <see cref="AnimationClip" />s on the template's own skeleton.
///   <para>Layout, DATA-PROVEN on all 14,149 retail tpls (684 with tracks; REA was down, so not REA-PROVEN):</para>
///   <list type="bullet">
///     <item>A spline key is <c>[frame, v0..v(n-1)]</c>, n = <c>Unk_02</c> (T/S 3, R 4, visibility 1), see
///       <see cref="m3dSPL.Stride" />. 100% of 7,762 T / 7,826 R / 331 S / 3,415 V splines fit, frames are integers,
///       increasing, and inside the sequence windows.</item>
///     <item>Rotations (keys and <c>iniRotation</c>) are stored w-first and conjugated against the row-vector matrices the
///       skeleton uses: <c>Quaternion(-x, -y, -z, w)</c> reproduces the rest <c>MatrixModel</c> of 2,642 of 2,830 objects
///       with a non-trivial rotation (plain wxyz 264, xyzw 0); against <c>MatrixLT</c> 1,617 / 1,627.</item>
///     <item>Visibility is 0..1 per frame (11.5% of keys fractional: fades). Shown as scale 0 below 0.5 (HEURISTIC: the
///       preview has no per-object fade).</item>
///     <item><c>rootAnim</c> is present on 877 tracks and identity everywhere (one garbage value); applied only when finite.</item>
///   </list>
///   Tracks bind like the game binds a .sani to a template (<see cref="ClipBinding" />: objects with <c>animNmb</c> != -1
///   in animNmb order), or explicitly when the caller passes the bones (merged meshes).
/// </summary>
public sealed class TplAnimation
{
  public const float DefaultFps = 30f;

  public List<TplSequence> Sequences { get; } = new();
  public List<TplObjectAnim> Objects { get; } = new();
  public TplChannel? RootTranslation { get; private set; }
  public TplChannel? RootRotation { get; private set; }
  public List<string> Notes { get; } = new();

  /// <summary>Last frame any channel keys (the whole timeline).</summary>
  public float LastFrame { get; private set; }

  public TplSequence? Find(string name) =>
    Sequences.FirstOrDefault(s => s.Name.Equals(name, StringComparison.Ordinal)) ?? Sequences.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

  /// <summary>Null when the template has no track (or no animated objects and no sequences).</summary>
  public static TplAnimation? From(animTRACK? track)
  {
    if (track is null || (track.objAnimList is not { Count: > 0 } && track.seqList is not { Count: > 0 }))
      return null;
    var anim = new TplAnimation();
    if (track.seqList is not null)
      for (var i = 0; i < track.seqList.Count; i++)
      {
        var s = track.seqList[i];
        if (s is null) continue;
        var start = Finite(s.startFrame) ? s.startFrame : 0f;
        var end = Finite(s.endFrame) ? s.endFrame : start;
        anim.Sequences.Add(new TplSequence(i, string.IsNullOrEmpty(s.name) ? "seq" + i : s.name, s.layerId, start, Math.Max(start, end),
          Finite(s.timeSec) && s.timeSec > 0 ? s.timeSec : 0f));
      }

    var bad = 0;
    if (track.objAnimList is not null)
      for (var i = 0; i < track.objAnimList.Count; i++)
      {
        var o = track.objAnimList[i] ?? new animOBJ_ANIM();
        var obj = new TplObjectAnim(i)
        {
          Translation = TplChannel.From(o.pTranslation, 3, false, ref bad),
          Rotation = TplChannel.From(o.pRotation, 4, true, ref bad),
          Scale = TplChannel.From(o.pScale, 3, false, ref bad),
          Visibility = TplChannel.From(o.pVisibility, 1, false, ref bad),
          IniTranslation = o.HasIniTranslation && Finite(o.iniTranslation) ? o.iniTranslation : null,
          IniRotation = o.HasIniRotation && o.iniRotation.LengthSquared() > 1e-8f && Finite(o.iniRotation) ? FileQuat(o.iniRotation.X, o.iniRotation.Y, o.iniRotation.Z, o.iniRotation.W) : null,
          IniScale = o.HasIniScale && Finite(o.iniScale) ? o.iniScale : null,
          IniVisibility = o.HasIniVisibility && float.IsFinite(o.iniVisibility) ? o.iniVisibility : null,
        };
        anim.Objects.Add(obj);
        anim.LastFrame = Math.Max(anim.LastFrame, obj.LastFrame);
      }

    if (track.rootAnim is { } root)
    {
      var rootBad = 0;
      var t = TplChannel.From(root.PTranslation, 3, false, ref rootBad);
      var r = TplChannel.From(root.PRotation, 4, true, ref rootBad);
      if (rootBad == 0 && t is not null && t.Values.All(v => MathF.Abs(v) < 1e5f)) anim.RootTranslation = t;
      if (rootBad == 0 && r is not null) anim.RootRotation = r;
      if (rootBad > 0) anim.Notes.Add("rootAnim had a spline that is not [frame, values] keys; skipped.");
    }

    if (bad > 0)
      anim.Notes.Add($"{bad} spline(s) did not split into [frame, values] keys (stride 1 + Unk_02) and were skipped.");
    foreach (var s in anim.Sequences) anim.LastFrame = Math.Max(anim.LastFrame, s.EndFrame);
    return anim;
  }

  /// <summary>File quaternion (w, x, y, z) to the System.Numerics rotation the skeleton matrices use (conjugate). DATA-PROVEN.</summary>
  public static Quaternion FileQuat(float w, float x, float y, float z)
  {
    var q = new Quaternion(-x, -y, -z, w);
    var l = q.Length();
    return l > 1e-8f && float.IsFinite(l) ? q * (1f / l) : Quaternion.Identity;
  }

  /// <summary>Bone per animNmb: the first bone whose <see cref="Bone.AnimIndex" /> is that number (-1 when none).</summary>
  public int[] BoneMap(IReadOnlyList<Bone>? bones)
  {
    var map = new int[Objects.Count];
    Array.Fill(map, -1);
    if (bones is null) return map;
    for (var i = 0; i < bones.Count; i++)
    {
      var a = bones[i].AnimIndex;
      if (a >= 0 && a < map.Length && map[a] < 0) map[a] = i;
    }

    return map;
  }

  /// <summary>
  ///   One sequence as a clip: a key per frame of <c>[startFrame, endFrame]</c> at the sequence's own rate, every animated
  ///   object (a channel with no spline holds its <c>ini*</c> value, a channel with neither keeps the bind).
  /// </summary>
  /// <param name="bones">The skeleton the clip will play on. Given: tracks carry explicit bone indices (+ <paramref name="boneOffset" />,
  ///   for a template merged into another mesh). Null: tracks bind by animNmb rank (<see cref="ClipBinding" />).</param>
  public AnimationClip ToClip(TplSequence seq, IReadOnlyList<Bone>? bones = null, int boneOffset = 0, IReadOnlySet<int>? mask = null)
  {
    var clip = NewClip(seq.Name, seq.Duration, seq.Fps);
    clip.Sequences.Add(Describe(seq));
    var map = BoneMap(bones);
    var frames = FrameList(seq.StartFrame, seq.EndFrame);
    foreach (var obj in Objects)
    {
      if (mask is not null && !mask.Contains(obj.Index)) continue;
      var track = NewTrack(obj, map, bones, boneOffset);
      var bindScale = BindScale(bones, map[obj.Index]);
      foreach (var f in frames)
        AddSample(track, obj, f, (f - seq.StartFrame) / seq.Fps, bindScale, f == frames[0]);
      Trim(track);
      clip.Tracks.Add(track);
    }

    AddRoot(clip, frames, seq.StartFrame, seq.Fps);
    clip.Notes.Add($"tpl sequence {Describe(seq)}. Keys [frame, values] (DATA-PROVEN layout); visibility < 0.5 shown as scale 0 (HEURISTIC).");
    return clip;
  }

  /// <summary>The whole track as one clip (frame 0 to the last key) at <paramref name="fps" />: legacy loose animTRACK .sani.</summary>
  public AnimationClip Timeline(string name, float fps = DefaultFps)
  {
    fps = fps > 0 ? fps : DefaultFps;
    var seq = new TplSequence(-1, name, 0, 0, LastFrame, LastFrame / fps);
    var clip = ToClip(seq);
    foreach (var s in Sequences) clip.Sequences.Add(Describe(s));
    return clip;
  }

  /// <summary>
  ///   Several sequence plays on one timeline (context view: the weapon's own animation while the character shoots or
  ///   swings). At each sample time an object follows the latest play that is running and moves it; before any, the
  ///   bind pose. Sampled at the fastest play's rate (30..120 Hz).
  /// </summary>
  public AnimationClip Compose(IReadOnlyList<TplPlay> plays, float duration, IReadOnlyList<Bone>? bones = null, int boneOffset = 0, string name = "prop anim")
  {
    duration = duration > 0 ? duration : plays.Select(p => p.Start + p.Seq.Duration).DefaultIfEmpty(1f).Max();
    var rate = Math.Clamp(plays.Select(p => p.Seq.Fps).DefaultIfEmpty(DefaultFps).Max(), DefaultFps, 120f);
    var clip = NewClip(name, duration, rate);
    var ordered = plays.OrderBy(p => p.Start).ToList();
    foreach (var p in ordered) clip.Sequences.Add($"{p.Start:0.00}s {p.Seq.Name}{(p.Loop ? " (loop)" : "")}{(p.Why.Length > 0 ? " — " + p.Why : "")}");
    var map = BoneMap(bones);
    var count = (int)MathF.Ceiling(duration * rate) + 1;
    foreach (var obj in Objects)
    {
      var moves = ordered.Select(p => p.Allows(obj.Index) && (!p.OnlyMoving || Moves(obj, p.Seq))).ToArray();
      if (!moves.Any(m => m)) continue;
      var track = NewTrack(obj, map, bones, boneOffset);
      var bindScale = BindScale(bones, map[obj.Index]);
      var bind = map[obj.Index] >= 0 && bones is not null ? bones[map[obj.Index]].Local : (Matrix4x4?)null;
      for (var k = 0; k < count; k++)
      {
        var t = Math.Min(duration, k / rate);
        float? frame = null;
        for (var i = ordered.Count - 1; i >= 0; i--)
        {
          var p = ordered[i];
          if (!moves[i] || t < p.Start - 1e-5f || t >= p.End) continue;
          var local = (t - p.Start) * p.Seq.Fps;
          if (p.Loop)
            local = p.Seq.Frames > 0 ? local % p.Seq.Frames : 0;
          else if (local > p.Seq.Frames + 1e-4f && !p.Hold)
            continue;
          frame = p.Seq.StartFrame + Math.Min(local, p.Seq.Frames);
          break;
        }

        if (frame is { } f)
          AddSample(track, obj, f, t, bindScale, k == 0);
        else
          AddBind(track, obj, t, bind, bindScale);
      }

      Trim(track);
      clip.Tracks.Add(track);
    }

    clip.Notes.Add("Composed tpl sequences (context view). Keys [frame, values] DATA-PROVEN; visibility < 0.5 as scale 0 (HEURISTIC).");
    return clip;
  }

  /// <summary>True when any channel of <paramref name="obj" /> changes inside the sequence window.</summary>
  public bool Moves(TplObjectAnim obj, TplSequence seq)
  {
    var key = seq.Index;
    lock (obj.MovesCache)
    {
      if (obj.MovesCache.TryGetValue(key, out var cached)) return cached;
      var result = (obj.Translation?.Varies(seq.StartFrame, seq.EndFrame) ?? false) || (obj.Rotation?.Varies(seq.StartFrame, seq.EndFrame) ?? false)
                   || (obj.Scale?.Varies(seq.StartFrame, seq.EndFrame) ?? false) || (obj.Visibility?.Varies(seq.StartFrame, seq.EndFrame) ?? false);
      obj.MovesCache[key] = result;
      return result;
    }
  }

  public static string Describe(TplSequence s) => $"{s.Name}  frames {s.StartFrame:0}-{s.EndFrame:0}  {s.Duration:0.###}s  ({s.Fps:0.#} fps, layer {s.Layer})";

  // ---- sampling ---------------------------------------------------------------------------------------------

  private static AnimationClip NewClip(string name, float duration, float rate) => new()
  {
    Name = name, Duration = duration > 0 ? duration : 1f / DefaultFps, SampleRate = rate, RootMotion = false,
  };

  private static BoneTrack NewTrack(TplObjectAnim obj, int[] map, IReadOnlyList<Bone>? bones, int offset)
  {
    var bone = map[obj.Index];
    return new BoneTrack
    {
      Bone = bone >= 0 ? bone + offset : -1,
      BoneName = bone >= 0 && bones is not null ? bones[bone].Name : "",
    };
  }

  private static Vector3 BindScale(IReadOnlyList<Bone>? bones, int bone) =>
    bones is not null && bone >= 0 && Matrix4x4.Decompose(bones[bone].Local, out var s, out _, out _) && Finite(s) ? s : Vector3.One;

  private static List<float> FrameList(float start, float end)
  {
    var frames = new List<float> { start };
    for (var f = MathF.Floor(start) + 1; f < end - 1e-4f; f++) frames.Add(f);
    if (end > start + 1e-4f) frames.Add(end);
    return frames;
  }

  private static void AddSample(BoneTrack track, TplObjectAnim obj, float frame, float time, Vector3 bindScale, bool first)
  {
    if (obj.TranslationAt(frame) is { } t) track.Translation.Add(new VectorKey { Time = time, Value = t });
    if (obj.RotationAt(frame) is { } r)
    {
      if (track.Rotation.Count > 0 && Quaternion.Dot(track.Rotation[^1].Value, r) < 0) r = -r;
      track.Rotation.Add(new QuatKey { Time = time, Value = r });
    }

    var scale = obj.ScaleAt(frame);
    var visible = obj.VisibilityAt(frame) is not { } v || v >= 0.5f;
    if (scale is not null || !visible || track.Scale.Count > 0 || obj.HidesSometimes)
      track.Scale.Add(new VectorKey { Time = time, Value = visible ? scale ?? bindScale : Vector3.Zero });
  }

  private static void AddBind(BoneTrack track, TplObjectAnim obj, float time, Matrix4x4? bind, Vector3 bindScale)
  {
    if (bind is not { } m || !Matrix4x4.Decompose(m, out _, out var r, out var t))
    {
      // No skeleton: the rest is frame 0 of the track.
      AddSample(track, obj, 0, time, bindScale, false);
      return;
    }

    track.Translation.Add(new VectorKey { Time = time, Value = t });
    if (track.Rotation.Count > 0 && Quaternion.Dot(track.Rotation[^1].Value, r) < 0) r = -r;
    track.Rotation.Add(new QuatKey { Time = time, Value = r });
    if (track.Scale.Count > 0 || obj.HidesSometimes || obj.Scale is not null)
      track.Scale.Add(new VectorKey { Time = time, Value = bindScale });
  }

  /// <summary>A channel whose keys are all equal keeps one key.</summary>
  private static void Trim(BoneTrack track)
  {
    static void TrimV(List<VectorKey> keys)
    {
      if (keys.Count > 1 && keys.All(k => Vector3.DistanceSquared(k.Value, keys[0].Value) < 1e-12f)) keys.RemoveRange(1, keys.Count - 1);
    }

    TrimV(track.Translation);
    TrimV(track.Scale);
    var r = track.Rotation;
    if (r.Count > 1 && r.All(k => MathF.Abs(Quaternion.Dot(k.Value, r[0].Value)) > 1 - 1e-7f)) r.RemoveRange(1, r.Count - 1);
  }

  private void AddRoot(AnimationClip clip, List<float> frames, float start, float fps)
  {
    if (RootTranslation is null && RootRotation is null) return;
    var any = false;
    foreach (var f in frames)
    {
      var time = (f - start) / fps;
      var t = RootTranslation?.Vector(f) ?? Vector3.Zero;
      var r = RootRotation?.Quat(f) ?? Quaternion.Identity;
      any |= t.LengthSquared() > 1e-10f || MathF.Abs(r.W) < 1 - 1e-6f;
      clip.RootTranslation.Add(new VectorKey { Time = time, Value = t });
      clip.RootRotation.Add(new QuatKey { Time = time, Value = r });
    }

    if (!any)
    {
      clip.RootTranslation.Clear();
      clip.RootRotation.Clear();
    }
    else clip.RootMotion = true;
  }

  internal static bool Finite(float v) => float.IsFinite(v);
  internal static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
  internal static bool Finite(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
}

/// <summary>Decoded channels of one <c>objAnimList</c> entry (index = the object's <c>animNmb</c>).</summary>
public sealed class TplObjectAnim(int index)
{
  public int Index { get; } = index;
  public TplChannel? Translation { get; init; }
  public TplChannel? Rotation { get; init; }
  public TplChannel? Scale { get; init; }
  public TplChannel? Visibility { get; init; }
  public Vector3? IniTranslation { get; init; }
  public Quaternion? IniRotation { get; init; }
  public Vector3? IniScale { get; init; }
  public float? IniVisibility { get; init; }
  internal Dictionary<int, bool> MovesCache { get; } = new();

  public float LastFrame => new[] { Translation, Rotation, Scale, Visibility }.Where(c => c is not null).Select(c => c!.LastFrame).DefaultIfEmpty(0).Max();

  /// <summary>Visibility below 0.5 somewhere (spline or ini).</summary>
  public bool HidesSometimes => (Visibility?.Values.Any(v => v < 0.5f) ?? false) || (Visibility is null && IniVisibility is < 0.5f);

  public Vector3? TranslationAt(float frame) => Translation?.Vector(frame) ?? IniTranslation;
  public Quaternion? RotationAt(float frame) => Rotation?.Quat(frame) ?? IniRotation;
  public Vector3? ScaleAt(float frame) => Scale?.Vector(frame) ?? IniScale;
  public float? VisibilityAt(float frame) => Visibility?.Scalar(frame) ?? IniVisibility;
}

/// <summary>A spline as sorted keys: frames plus <see cref="Dimension" /> values each (rotations already converted, x y z w).</summary>
public sealed class TplChannel
{
  public float[] Frames { get; }
  public float[] Values { get; }
  public int Dimension { get; }
  public float LastFrame => Frames.Length > 0 ? Frames[^1] : 0;

  private TplChannel(float[] frames, float[] values, int dimension)
  {
    Frames = frames;
    Values = values;
    Dimension = dimension;
  }

  /// <summary>Null when there is no spline; <paramref name="bad" /> counts splines that do not fit the layout.</summary>
  public static TplChannel? From(m3dSPL? spline, int dimension, bool rotation, ref int bad)
  {
    if (spline is null) return null;
    if (!spline.HasFrameKeys || spline.Dimension != dimension)
    {
      bad++;
      return null;
    }

    var n = spline.KeyCount;
    var frames = new float[n];
    var values = new float[n * dimension];
    for (var i = 0; i < n; i++)
    {
      frames[i] = spline.KeyFrame(i);
      if (!float.IsFinite(frames[i]) || (i > 0 && frames[i] < frames[i - 1]))
      {
        bad++;
        return null;
      }

      if (rotation)
      {
        var q = TplAnimation.FileQuat(spline.KeyValue(i, 0), spline.KeyValue(i, 1), spline.KeyValue(i, 2), spline.KeyValue(i, 3));
        if (i > 0 && q.X * values[(i - 1) * 4] + q.Y * values[(i - 1) * 4 + 1] + q.Z * values[(i - 1) * 4 + 2] + q.W * values[(i - 1) * 4 + 3] < 0) q = -q;
        values[i * 4] = q.X;
        values[i * 4 + 1] = q.Y;
        values[i * 4 + 2] = q.Z;
        values[i * 4 + 3] = q.W;
      }
      else
      {
        for (var c = 0; c < dimension; c++)
        {
          var v = spline.KeyValue(i, c);
          values[i * dimension + c] = float.IsFinite(v) ? v : 0f;
        }
      }
    }

    return n == 0 ? null : new TplChannel(frames, values, dimension);
  }

  /// <summary>Keys around <paramref name="frame" /> and the blend between them (clamped at the ends).</summary>
  private (int A, int B, float T) Locate(float frame)
  {
    if (frame <= Frames[0]) return (0, 0, 0);
    if (frame >= Frames[^1]) return (Frames.Length - 1, Frames.Length - 1, 0);
    int lo = 0, hi = Frames.Length - 1;
    while (hi - lo > 1)
    {
      var mid = (lo + hi) >> 1;
      if (Frames[mid] <= frame) lo = mid;
      else hi = mid;
    }

    var span = Frames[hi] - Frames[lo];
    return (lo, hi, span > 1e-6f ? (frame - Frames[lo]) / span : 0);
  }

  public float Scalar(float frame)
  {
    var (a, b, t) = Locate(frame);
    return Values[a] + (Values[b] - Values[a]) * t;
  }

  public Vector3 Vector(float frame)
  {
    var (a, b, t) = Locate(frame);
    var va = new Vector3(Values[a * 3], Values[a * 3 + 1], Values[a * 3 + 2]);
    var vb = new Vector3(Values[b * 3], Values[b * 3 + 1], Values[b * 3 + 2]);
    return Vector3.Lerp(va, vb, t);
  }

  public Quaternion Quat(float frame)
  {
    var (a, b, t) = Locate(frame);
    var qa = new Quaternion(Values[a * 4], Values[a * 4 + 1], Values[a * 4 + 2], Values[a * 4 + 3]);
    if (a == b || t <= 0) return qa;
    var qb = new Quaternion(Values[b * 4], Values[b * 4 + 1], Values[b * 4 + 2], Values[b * 4 + 3]);
    return Quaternion.Normalize(Quaternion.Slerp(qa, qb, t));
  }

  /// <summary>Any value inside <c>[start, end]</c> differs from the value at <paramref name="start" />.</summary>
  public bool Varies(float start, float end)
  {
    var (a0, _, _) = Locate(start);
    var (a1, b1, _) = Locate(end);
    var last = Math.Max(a1, b1);
    for (var i = a0; i <= last; i++)
      for (var c = 0; c < Dimension; c++)
        if (MathF.Abs(Values[i * Dimension + c] - Values[a0 * Dimension + c]) > 1e-5f)
          return true;
    return false;
  }
}
