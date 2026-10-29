using System.Numerics;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser.Effects;

/// <summary>
///   Effects drawn over the model view: one effect attached to the model's sockets (its own looping clock), and/or
///   the effects the playing clip's morpheme events start (<see cref="AnimEventEffects"/>, on the clip clock).
///   Set from the UI thread, read by the GL thread through <see cref="Frame"/>.
/// </summary>
internal sealed class HostVfx
{
  private readonly object _gate = new();
  private readonly Dictionary<string, EffectDefinition?> _defs = new(StringComparer.OrdinalIgnoreCase);
  private ExportedMesh? _mesh;
  private EffectHost? _host;
  private Matrix4x4[]? _bind;
  private EffectInstance? _single;
  private string _attach = "";
  private AnimEventEffects? _events;
  private AnimationClip? _eventsFor;
  private bool _eventsOn;
  private int _frames;
  private static readonly bool Debug = Environment.GetEnvironmentVariable("SM2_HOST_DEBUG") == "1";
  private SweptPoseCache? _poses;
  private bool _posesInPlace;
  /// <summary>mtl1-53: weapon swing trails (sfx_trail_triangle) in context playback; <c>SM2_SWING_TRAILS=0</c> hides them.</summary>
  public static bool SweptOn { get; set; } = Environment.GetEnvironmentVariable("SM2_SWING_TRAILS") != "0";

  public required EffectLibrary Library { get; init; }
  public required PartAliases Aliases { get; init; }
  public required EffectTextures Textures { get; init; }
  public EffectMeshes Meshes { get; init; } = EffectMeshes.None;
  public AnimEventVfxMap Map { get; set; } = new();
  public string Status { get; private set; } = "";
  /// <summary>Comma-separated substrings of blueprint paths to use for anim events ("" = all).</summary>
  public string SourceFilter { get; set; } = "";
  /// <summary>mtl1-49 context playback rules (owned blueprints, sfx.list resolution, weapon socket scope, Shoot cues); used when <see cref="SourceFilter"/> is empty.</summary>
  public HostVfxContext? Context { get; set; }
  public bool Active { get { lock (_gate) return _single is not null || (_eventsOn && _events is { Count: > 0 }); } }
  public EffectDefinition? Single { get { lock (_gate) return _single?.Definition; } }
  public AnimEventEffects? Events { get { lock (_gate) return _events; } }

  public EffectDefinition? Build(string name)
  {
    lock (_defs)
    {
      if (_defs.TryGetValue(name, out var cached)) return cached;
      EffectDefinition? def = null;
      if (Library.Resolve(name) is { } tree)
      {
        def = EffectDefinition.Build(name, tree);
        if (def.Emitters.Count == 0 && def.Lights.Count == 0 && def.Trails.Count == 0 && def.SweptTrails.Count == 0) def = null;
        else
        {
          Textures.Prefetch(def.Textures);
          Meshes.Prefetch(def.Emitters.Select(e => e.Geom?.Template));
        }
      }

      return _defs[name] = def;
    }
  }

  /// <summary>
  ///   mtl1-53: <paramref name="effect"/> with the owner's sfx.list entry for <paramref name="key"/> merged on top
  ///   (<see cref="HostVfxContext.Overrides"/>), or null without an entry.
  /// </summary>
  public EffectDefinition? BuildKeyed(string key, string effect)
  {
    if (Context?.Overrides?.Invoke(key) is not { } entry) return null;
    var id = $"{effect}#{key}#{entry.ToJsonString().GetHashCode()}";
    lock (_defs)
    {
      if (_defs.TryGetValue(id, out var cached)) return cached;
      var node = (System.Text.Json.Nodes.JsonObject)entry.DeepClone();
      node["__type"] = effect;
      var def = EffectDefinition.Build(effect, Library.ResolveNode(node));
      if (def.Emitters.Count == 0 && def.Lights.Count == 0 && def.Trails.Count == 0 && def.SweptTrails.Count == 0) def = null;
      else Textures.Prefetch(def.Textures);
      return _defs[id] = def;
    }
  }

  private EffectHost HostFor(ExportedMesh mesh)
  {
    if (!ReferenceEquals(_mesh, mesh) || _host is null)
    {
      _mesh = mesh;
      _host = new EffectHost(mesh.Bones.Select(b => b.Name).ToList(), mesh.Bones.Select(b => b.SourceName).ToList());
      _bind = null;
      _events = null;
      _eventsFor = null;
      _poses = null;
      _single = _single is null ? null : _host.Attach(_single.Definition, _attach);
    }

    return _host;
  }

  /// <summary>Attach <paramref name="name"/> to the mesh (null clears). <paramref name="attach"/> = root bone ("" = sockets only, else origin).</summary>
  public string SetEffect(ExportedMesh mesh, string? name, string attach)
  {
    lock (_gate)
    {
      var host = HostFor(mesh);
      _attach = attach;
      if (string.IsNullOrWhiteSpace(name)) { _single = null; return Status = ""; }
      if (Build(name.Trim()) is not { } def) { _single = null; return Status = $"{name}: no such effect"; }
      _single = host.Attach(def, attach);
      var sockets = def.Emitters.Select(e => e.NameParent).Concat(def.Lights.Select(l => l.NameParent)).Concat(def.Trails.Select(t => t.NameParent))
        .Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
      var bound = sockets.Where(s => host.IndexOf(s) >= 0).ToList();
      var missing = sockets.Where(s => host.IndexOf(s) < 0 && !s.Equals("sfx_source_pt", StringComparison.OrdinalIgnoreCase)).ToList();
      return Status = $"{def.Name} on {mesh.Name}: sockets {(bound.Count > 0 ? string.Join(", ", bound) : "none")}"
                      + (missing.Count > 0 ? $"; not on this model (root): {string.Join(", ", missing)}" : "")
                      + (attach.Length > 0 ? $"; root on {attach}" : "");
    }
  }

  public string SetEvents(ExportedMesh mesh, bool on)
  {
    lock (_gate)
    {
      HostFor(mesh);
      _eventsOn = on;
      _eventsFor = null;
      _events = null;
      return on ? EnsureEvents(mesh) : "";
    }
  }

  private string EnsureEvents(ExportedMesh mesh)
  {
    if (!_eventsOn || mesh.Clip is not { } clip) return "";
    if (ReferenceEquals(_eventsFor, clip) && _events is not null) return "";
    _eventsFor = clip;
    var filters = SourceFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var ctx = filters.Length == 0 ? Context : null;
    _events = new AnimEventEffects(clip, Map, Build, _host!,
      filters.Length > 0 ? h => filters.Any(f => h.Source.Contains(f, StringComparison.OrdinalIgnoreCase)) : ctx?.Include,
      ctx?.Resolve, ctx?.Scope, ctx?.TrackCues, ctx is null ? null : BuildKeyed);
    _poses = null;
    var tags = clip.EventTracks.Sum(t => t.Events.Count);
    return Status = tags == 0
      ? $"{clip.Name}: no morpheme events"
      : $"{clip.Name}: {tags} events, {_events.Cues.Count} effect cues, {_events.StopCount} stops"
        + (_events.Swept.Count > 0 ? $", {_events.Swept.Count} swing trails ({string.Join(", ", _events.Swept.Take(4).Select(c => $"{c.Def.Name}@{c.Start:0.00}-{c.End:0.00}s"))})" : "")
        + $"  ({string.Join(", ", _events.Cues.Select(c => $"{c.Event}@{c.Time:0.00}s->{c.Effect}").Take(6))})"
        + (_events.Unmapped.Count > 0 ? $"; no blueprint link: {string.Join(", ", _events.Unmapped.Take(8))}" : "")
        + $" [{Map.Files} blueprints]";
  }

  /// <summary>
  ///   mtl1-53: ribbons of the swing trails alive at <paramref name="clipTime"/>. Past cross-sections need past poses: the
  ///   clip is re-posed on the <see cref="SweptTrails.SampleRate"/> grid (cached) in the same space the scene draws, i.e.
  ///   with the In-place rebase / root-motion removal when the scene has it; the head uses the drawn pose itself.
  /// </summary>
  private IEnumerable<SpriteBatch> SweptBatches(ExportedMesh mesh, EffectHost host, PreviewScene scene, float clipTime)
  {
    if (scene.BoneWorlds is not { } now || mesh.Clip is not { HasCurves: true }) yield break;
    var alive = _events!.SweptAt(clipTime).ToList();
    if (alive.Count == 0) yield break;
    if (_poses is null)
    {
      // Which space does the scene draw in? Compare the drawn pose with a raw and an in-place pose at the same time.
      var raw = MeshPreview.BoneWorlds(mesh, clipTime);
      var inPlace = InPlace(mesh, (Matrix4x4[])raw.Clone());
      var probe = Math.Max(0, host.IndexOf(alive[0].Edge.Locator1));
      float Err(Matrix4x4[] w) => probe < w.Length && probe < now.Length ? Vector3.Distance(w[probe].Translation, now[probe].Translation) : 0f;
      _posesInPlace = Err(inPlace) < Err(raw);
      var useInPlace = _posesInPlace;
      _poses = new SweptPoseCache(t => useInPlace ? InPlace(mesh, MeshPreview.BoneWorlds(mesh, t)) : MeshPreview.BoneWorlds(mesh, t));
    }

    _poses.Trim(clipTime - 1f, clipTime + 0.05f);
    foreach (var cue in alive)
    {
      var batch = SweptTrails.Build(cue, clipTime,
        s => MathF.Abs(s - clipTime) < 1e-4f ? SweptTrails.Edge(host, now, cue.Edge, cue.Scope) : _poses.Edge(host, cue.Edge, cue.Scope, s),
        scene.Eye);
      if (batch is not null) yield return batch;
    }
  }

  private static Matrix4x4[] InPlace(ExportedMesh mesh, Matrix4x4[] worlds)
  {
    var rebase = MeshPreview.InPlaceRebase(mesh);
    for (var i = 0; i < worlds.Length; i++) worlds[i] *= rebase;
    MeshPreview.RemoveRootMotion(mesh, worlds, rebase);
    return worlds;
  }

  /// <summary>GL thread: pose the host from the scene and build the overlay layer.</summary>
  public EffectLayer? Frame(ExportedMesh mesh, PreviewScene scene, float clipTime, float effectTime)
  {
    lock (_gate)
    {
      if (_single is null && !_eventsOn) return null;
      var host = HostFor(mesh);
      host.SetPose(scene.BoneWorlds ?? (_bind ??= MeshPreview.BoneWorlds(mesh, 0)));
      EnsureEvents(mesh);
      var instances = new List<EffectInstance>();
      if (_single is { } single)
      {
        var period = Math.Max(0.1f, single.Definition.Duration) + 0.25f;
        single.Seek(single.Definition.Loops ? effectTime : effectTime % period);
        instances.Add(single);
      }

      if (_eventsOn && _events is not null)
        instances.AddRange(_events.At(clipTime));
      var batches = new List<SpriteBatch>();
      if (_eventsOn && _events is { Swept.Count: > 0 } && SweptOn)
        batches.AddRange(SweptBatches(mesh, host, scene, clipTime));
      if (instances.Count == 0 && batches.Count == 0) return null;
      var meshes = new List<MeshBatch>();
      var lights = new List<LightSample>();
      foreach (var inst in instances)
      {
        batches.AddRange(EffectSprites.Build(inst, Aliases, scene.View));
        meshes.AddRange(EffectMeshParticles.Build(inst, scene.View));
        lights.AddRange(inst.Lights());
      }

      if (Debug && (++_frames % 30 == 0 || batches.Sum(b => b.QuadCount) > 4))
        Console.WriteLine($"hostvfx t{effectTime:0.00} clip{clipTime:0.00} inst {instances.Count} quads {batches.Sum(b => b.QuadCount)} meshes {meshes.Count} lights {lights.Count}");
      return new EffectLayer { Batches = batches, Lights = lights, Texture = Textures.Get, Ground = false, Meshes = meshes, Mesh = Meshes.Get };
    }
  }
}

/// <summary>mtl1-49: how a context (character + held weapon) runs a clip's events. See <see cref="LibSaber.SM2.Assets.Context.ContextResolver"/>.</summary>
internal sealed record HostVfxContext(
  Func<EventVfx, bool>? Include,
  Func<string, string>? Resolve,
  Func<string, string, (string Prefix, string Root)?>? Scope,
  IReadOnlyList<LibSaber.SM2.Assets.Context.TrackCue>? TrackCues)
{
  /// <summary>mtl1-53: blueprint name → the owner's sfx.list entry (prototype override), see <see cref="LibSaber.SM2.Assets.Context.ContextResolver.Overrides"/>.</summary>
  public Func<string, System.Text.Json.Nodes.JsonObject?>? Overrides { get; init; }
}
