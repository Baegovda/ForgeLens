using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   Effects on a posed model. Socket names (<c>nameParent</c>: <c>tip</c>, <c>flame_area</c>, <c>jet_big_l</c>,
///   <c>sfx_stomp_effect</c>, ...) are bones/nodes of the host template, matched case-insensitively; a trailing
///   <c>*</c> matches a prefix. <c>sfx_source_pt</c> and unknown names fall back to the instance root (the
///   attach bone, or the model origin). Bone worlds are set once per frame (<see cref="SetPose"/>).
/// </summary>
public sealed class EffectHost
{
  private readonly Dictionary<string, int> _bones = new(StringComparer.OrdinalIgnoreCase);
  private readonly string[] _names;
  private Matrix4x4[] _worlds = [];

  /// <param name="sourceNames">mtl1-49: the bones' template names (<see cref="Meshes.Bone.SourceName"/>): a merged weapon's
  ///   <c>wpn_muzzle</c> also answers to <c>muzzle</c>. Host names win.</param>
  public EffectHost(IReadOnlyList<string> boneNames, IReadOnlyList<string>? sourceNames = null)
  {
    _names = boneNames.ToArray();
    for (var i = 0; i < _names.Length; i++) _bones.TryAdd(_names[i], i);
    if (sourceNames is not null)
      for (var i = 0; i < Math.Min(_names.Length, sourceNames.Count); i++)
        if (sourceNames[i] is { Length: > 0 } alias) _bones.TryAdd(alias, i);
  }

  public IReadOnlyList<string> BoneNames => _names;

  /// <summary>Bone world matrices for this frame (same order as the names).</summary>
  public void SetPose(Matrix4x4[] worlds) => _worlds = worlds;

  public int IndexOf(string name)
  {
    if (name.Length == 0 || name.Equals("sfx_source_pt", StringComparison.OrdinalIgnoreCase)) return -1;
    if (_bones.TryGetValue(name, out var i)) return i;
    if (name.EndsWith('*'))
    {
      var prefix = name[..^1];
      for (var k = 0; k < _names.Length; k++)
        if (_names[k].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return k;
    }

    return -1;
  }

  /// <summary>World of a bone without scale (effects are authored in metres on a rigid frame).</summary>
  public Matrix4x4? World(string name) => IndexOf(name) is var i and >= 0 && i < _worlds.Length ? Rigid(_worlds[i]) : null;

  public Func<string, float, Matrix4x4?> Socket => (name, _) => World(name);

  /// <summary>Whether any emitter, light or trail of the effect binds to a socket the host has.</summary>
  public bool Binds(EffectDefinition def)
    => def.Emitters.Any(e => IndexOf(e.NameParent) >= 0) || def.Lights.Any(l => IndexOf(l.NameParent) >= 0) || def.Trails.Any(t => IndexOf(t.NameParent) >= 0);

  public static Matrix4x4 Rigid(Matrix4x4 m)
  {
    if (!Matrix4x4.Decompose(m, out _, out var r, out var t)) return m;
    return Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
  }

  /// <summary>An instance placed on this host: sockets resolve to bones; the root follows <paramref name="attach"/> (or the origin).</summary>
  /// <param name="scope">mtl1-49: bone prefix tried first for sockets (<c>wpn</c> → <c>wpn_muzzle</c>), so a held weapon's effect
  ///   lands on the weapon even when the character has a socket of the same name.</param>
  public EffectInstance Attach(EffectDefinition def, string? attach = null, ulong seed = 1, string? scope = null)
  {
    Func<string, float, Matrix4x4?> socket = scope is { Length: > 0 }
      ? (name, _) => (name.Length > 0 ? World(scope + "_" + name) : null) ?? World(name)
      : Socket;
    var instance = new EffectInstance(def, seed) { Socket = socket, Ground = null };
    if (attach is { Length: > 0 } && IndexOf(attach) >= 0)
      instance.RootMotion = _ => World(attach) ?? Matrix4x4.Identity;
    return instance;
  }
}

/// <summary>
///   Plays effects from a clip's morpheme events: each event (point, or span start) whose tag maps to effects
///   spawns an instance at its start time; instances are re-simulated from their own start, so scrubbing and
///   looping are deterministic (same as the standalone view).
/// </summary>
public sealed class AnimEventEffects
{
  /// <param name="Stop">Clip time a later stop event ends its emission (+inf = none).</param>
  public sealed record Cue(float Time, string Event, string Track, string Effect, string Source, float Stop = float.PositiveInfinity);

  private readonly List<(Cue Cue, EffectInstance Instance)> _instances = [];
  public IReadOnlyList<Cue> Cues { get; }
  public IReadOnlyList<string> Unmapped { get; }

  /// <param name="include">Blueprint filter: a character runs only the blueprints its cls / weapon own, while the map
  ///   unions every extracted one (melee_damaging_window is wired by several weapons).</param>
  /// <param name="resolve">sfx.list key → effect for the clip's owner (character + held weapon chains); default <see cref="AnimEventVfxMap.Resolve"/>.</param>
  /// <param name="scope">Socket scope for a cue (effect, source) → (bone prefix, root bone), e.g. a held weapon's effects.</param>
  /// <param name="trackCues">Cues not from blueprints: every event on a track named <see cref="Context.TrackCue.Track"/> starts the effect (HEURISTIC, e.g. Shoot → muzzle).</param>
  public AnimEventEffects(AnimationClip clip, AnimEventVfxMap map, Func<string, EffectDefinition?> build, EffectHost host, Func<EventVfx, bool>? include = null,
    Func<string, string>? resolve = null, Func<string, string, (string Prefix, string Root)?>? scope = null, IReadOnlyList<Context.TrackCue>? trackCues = null)
  {
    resolve ??= map.Resolve;
    var cues = new List<Cue>();
    var unmapped = new SortedSet<string>(StringComparer.Ordinal);
    var length = clip.Duration > 0 ? clip.Duration : 1f;
    var stops = new List<(float Time, EventStop Stop)>();
    foreach (var track in clip.EventTracks)
      foreach (var ev in track.Events)
        foreach (var tag in ev.Tags)
          foreach (var stop in map.StopsFor(tag))
            if (include?.Invoke(new EventVfx("", stop.Source)) ?? true)
              stops.Add(((ev.Start + (stop.AtEnd ? ev.Duration : 0f)) * length, stop));
    StopCount = stops.Count;
    foreach (var track in clip.EventTracks)
      foreach (var ev in track.Events)
        foreach (var tag in ev.Tags)
        {
          var hits = map.For(tag).Where(h => include?.Invoke(h) ?? true).ToList();
          if (hits.Count == 0) { unmapped.Add(tag); continue; }
          var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
          foreach (var hit in hits)
          {
            var name = resolve(hit.Effect);
            if (!seen.Add(name + "@" + hit.Locator)) continue;
            if (build(name) is not { } def) { unmapped.Add($"{tag}->{name}?"); continue; }
            var time = (ev.Start + (hit.AtEnd ? ev.Duration : 0f)) * length;
            // A stop at the same instant is the "stop old, start new" restart idiom: only later stops count.
            var stopAt = stops.Where(s => s.Time > time + 1e-4f && s.Stop.Matches(name, resolve)).Select(s => s.Time).DefaultIfEmpty(float.PositiveInfinity).Min();
            var cue = new Cue(time, tag, track.Name, name, hit.Source, stopAt);
            cues.Add(cue);
            var sc = scope?.Invoke(name, hit.Source);
            var locator = hit.Locator.Length > 0 ? hit.Locator : sc?.Root ?? "";
            var inst = host.Attach(def, locator, (ulong)(cues.Count * 7919), sc?.Prefix);
            inst.StopTime = stopAt - time;
            _instances.Add((cue, inst));
          }
        }

    if (trackCues is { Count: > 0 })
      foreach (var track in clip.EventTracks)
        foreach (var tc in trackCues.Where(c => c.Track.Equals(track.Name, StringComparison.OrdinalIgnoreCase)))
        {
          if (build(tc.Effect) is not { } def) { unmapped.Add($"{track.Name}->{tc.Effect}?"); continue; }
          foreach (var ev in track.Events)
          {
            var time = ev.Start * length;
            var cue = new Cue(time, track.Name, track.Name, tc.Effect, tc.Source);
            cues.Add(cue);
            var sc = scope?.Invoke(tc.Effect, tc.Source);
            _instances.Add((cue, host.Attach(def, sc?.Root ?? "", (ulong)(cues.Count * 7919), sc?.Prefix)));
          }
        }

    Cues = cues;
    Unmapped = unmapped.ToList();
  }

  public int Count => _instances.Count;
  /// <summary>Stop cues on this clip (after the blueprint filter).</summary>
  public int StopCount { get; }

  /// <summary>Instances alive at clip time <paramref name="t"/>, each seeked to its local time.</summary>
  public IEnumerable<EffectInstance> At(float t)
  {
    foreach (var (cue, inst) in _instances)
    {
      var local = t - cue.Time;
      if (local < 0 || local > Math.Max(0.1f, Math.Min(inst.Definition.Duration, inst.StopTime)) + 2f) continue;
      inst.Seek(local);
      yield return inst;
    }
  }
}
