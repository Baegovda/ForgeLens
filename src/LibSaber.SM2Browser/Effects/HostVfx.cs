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

  public required EffectLibrary Library { get; init; }
  public required PartAliases Aliases { get; init; }
  public required EffectTextures Textures { get; init; }
  public EffectMeshes Meshes { get; init; } = EffectMeshes.None;
  public AnimEventVfxMap Map { get; set; } = new();
  public string Status { get; private set; } = "";
  /// <summary>Comma-separated substrings of blueprint paths to use for anim events ("" = all).</summary>
  public string SourceFilter { get; set; } = "";
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
        if (def.Emitters.Count == 0 && def.Lights.Count == 0 && def.Trails.Count == 0) def = null;
        else
        {
          Textures.Prefetch(def.Textures);
          Meshes.Prefetch(def.Emitters.Select(e => e.Geom?.Template));
        }
      }

      return _defs[name] = def;
    }
  }

  private EffectHost HostFor(ExportedMesh mesh)
  {
    if (!ReferenceEquals(_mesh, mesh) || _host is null)
    {
      _mesh = mesh;
      _host = new EffectHost(mesh.Bones.Select(b => b.Name).ToList());
      _bind = null;
      _events = null;
      _eventsFor = null;
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
    _events = new AnimEventEffects(clip, Map, Build, _host!,
      filters.Length == 0 ? null : h => filters.Any(f => h.Source.Contains(f, StringComparison.OrdinalIgnoreCase)));
    var tags = clip.EventTracks.Sum(t => t.Events.Count);
    return Status = tags == 0
      ? $"{clip.Name}: no morpheme events"
      : $"{clip.Name}: {tags} events, {_events.Cues.Count} effect cues, {_events.StopCount} stops ({string.Join(", ", _events.Cues.Select(c => $"{c.Event}@{c.Time:0.00}s->{c.Effect}").Take(6))})"
        + (_events.Unmapped.Count > 0 ? $"; no blueprint link: {string.Join(", ", _events.Unmapped.Take(8))}" : "")
        + $" [{Map.Files} blueprints]";
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
      if (instances.Count == 0) return null;
      var batches = new List<SpriteBatch>();
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
