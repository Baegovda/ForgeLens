using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>One effect a morpheme event starts, with where the link was found.</summary>
/// <param name="AtEnd">Reached from a duration event's <c>OnDeactivate</c>/<c>OnExit</c>: fires at the event's end.</param>
/// <param name="Edge">mtl1-53: a two-locator (weapon swing) trail starter, null for every other starter.</param>
public sealed record EventVfx(string Effect, string Source, string Locator = "", bool AtEnd = false, TrailEdge? Edge = null);

/// <summary>
///   mtl1-53: the two points a <c>sfx_trail_triangle</c> sweeps, as the starter node names them (PROVEN from data):
///   <c>BlueprintSfxLibraryStartsfxtrailtriangle</c> has <c>locator1</c>/<c>locator1Offset</c>/<c>locator2</c>/<c>locator2Offset</c>
///   (chainsword: <c>sfx_trail3</c> / <c>sfx_trail2</c>, offsets 0); <c>BlueprintNodeVfxTriangleRestart</c> has
///   <c>locator1Ref</c>/<c>locator1OffsetRef</c>/<c>locator2Ref</c>/<c>locator2OffsetRef</c> (marine_shared:
///   <c>floorplace</c> z -0.5 / <c>tip</c> z 0.2). Offsets are <c>Vector3d</c> (missing members are 0), taken in the
///   locator's frame (HEURISTIC: the runtime that applies them was not traced).
/// </summary>
public sealed record TrailEdge(string Locator1, Vector3 Offset1, string Locator2, Vector3 Offset2);

/// <summary>
///   An event that stops effects: <c>BlueprintNodeVfxStop</c> / <c>BlueprintSfxLibraryStopsfx</c> (one name),
///   <c>BlueprintNodeStopPackOfVfx</c> (<c>vfxList</c>), <c>BlueprintNodeStopAllSfx</c> (<see cref="All"/> but
///   <c>ignoreSfxList</c>). Stopping ends emission; live particles finish their life.
/// </summary>
public sealed record EventStop(string Source, IReadOnlyList<string> Names, bool All = false, bool AtEnd = false)
{
  public bool Matches(string effect, Func<string, string> resolve) =>
    All ? !Names.Any(n => Same(n, effect, resolve)) : Names.Any(n => Same(n, effect, resolve));

  private static bool Same(string name, string effect, Func<string, string> resolve) =>
    name.Equals(effect, StringComparison.OrdinalIgnoreCase) || resolve(name).Equals(effect, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
///   Morpheme event name -> effects, read from blueprint graphs (<c>.sso</c>). The game has no hardcoded table
///   (no event or <c>vfx_events</c> string in the exe); characters own blueprints (<c>prop_blueprints</c> in the
///   class) whose nodes do the link:
///   <list type="bullet">
///     <item><c>BlueprintNodeBulkVfxOnMorphemeEvents.eventInfos</c>: <c>{ mrEvent.name, vfxList[] }</c> (direct).</item>
///     <item><c>BlueprintLibraryBpneventonmrevent</c> (<c>mrEvent</c>) / <c>BlueprintNodeOnMrDurationEvent</c>
///       (<c>mrDurationEvent</c>, <c>OnActivate</c>): exec links (<c>"Node/Input"</c>) to
///       <c>BlueprintNodeVfxRestart</c>, <c>BlueprintSfxLibraryRestartsfx</c>, ... whose name is a
///       <c>sfxNameRef</c>/<c>vfxUidRef</c> default or comes from a <c>StringPipe</c> (<c>Get: ["Node/vfxUidRef/cb"]</c>).</item>
///   </list>
///   Names may be keys of a class <c>sfx.list</c> (<c>muzzle = { __type = "fx_bolter_muzzle_client" }</c>);
///   <see cref="AddClassSfxList"/> registers those so <see cref="Resolve"/> returns the .sfx name.
/// </summary>
/// <summary>
///   propanim: a blueprint <c>BlueprintAnimationLibraryStartanim</c>/<c>Stopanim</c> (the prop's own embedded sequence
///   <see cref="Anim" />) and what starts it: a morpheme event name, or <c>@NodeType.Pin</c> for a graph root such as
///   <c>@BlueprintNodeOnChangeHolding.OnHoldInArms</c>. <see cref="Playback" /> is <c>typePlayback.def</c> (e.g. <c>cycle</c>,
///   <c>clamp</c>; empty = default).
/// </summary>
public sealed record EventAnim(string Trigger, string Anim, string Playback, bool Stop, bool AtEnd, string Source)
{
  public bool IsEvent => !Trigger.StartsWith('@');
  public bool Loops => Playback.Equals("cycle", StringComparison.OrdinalIgnoreCase) || Playback.Equals("loop", StringComparison.OrdinalIgnoreCase);
}

public sealed class AnimEventVfxMap
{
  private static readonly HashSet<string> Starters = new(StringComparer.Ordinal)
  {
    "BlueprintNodeVfxRestart", "BlueprintNodeVfxTriangleRestart", "BlueprintSfxLibraryRestartsfx", "BlueprintSfxLibraryStartsfx",
    "BlueprintSfxLibraryStartsfxtrailtriangle", "BlueprintNodeStartSfxTrailStatic",
  };

  private readonly Dictionary<string, List<EventVfx>> _map = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, string> _sfxKeys = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, List<EventStop>> _stops = new(StringComparer.OrdinalIgnoreCase);
  private static readonly HashSet<string> Stoppers = new(StringComparer.Ordinal)
  {
    "BlueprintNodeVfxStop", "BlueprintSfxLibraryStopsfx", "BlueprintNodeStopPackOfVfx", "BlueprintNodeStopAllSfx",
  };
  private static readonly HashSet<string> TriangleStarters = new(StringComparer.Ordinal)
  {
    "BlueprintNodeVfxTriangleRestart", "BlueprintSfxLibraryStartsfxtrailtriangle",
  };
  private static readonly HashSet<string> EndPins = new(StringComparer.Ordinal) { "OnDeactivate", "OnExit" };

  public IReadOnlyDictionary<string, List<EventVfx>> Events => _map;
  private readonly List<EventAnim> _anims = [];
  /// <summary>propanim: every Startanim/Stopanim with its trigger, all blueprints (filter by <see cref="EventAnim.Source" />).</summary>
  public IReadOnlyList<EventAnim> Anims => _anims;
  private readonly HashSet<string> _sources = new(StringComparer.OrdinalIgnoreCase);
  public bool HasSource(string source) => _sources.Contains(source);
  public int Files { get; private set; }

  public IReadOnlyList<EventVfx> For(string eventName) => _map.TryGetValue(eventName, out var list) ? list : [];
  public IReadOnlyDictionary<string, List<EventStop>> StopEvents => _stops;
  public IReadOnlyList<EventStop> StopsFor(string eventName) => _stops.TryGetValue(eventName, out var list) ? list : [];

  /// <summary>Effect file name for a blueprint name: a class <c>sfx.list</c> key's <c>__type</c>, else the name itself.</summary>
  public string Resolve(string name) => _sfxKeys.TryGetValue(name, out var type) ? type : name;

  public void AddClassSfxList(JsonObject cls)
  {
    var list = (cls["properties"] as JsonObject)?.Obj("sfx")?.Obj("list") ?? cls.Obj("sfx")?.Obj("list");
    if (list is null) return;
    foreach (var (key, value) in list)
      if (value.Str("__type") is { Length: > 0 } type) _sfxKeys[key] = type;
  }

  public static AnimEventVfxMap FromDirectory(string root)
  {
    var map = new AnimEventVfxMap();
    if (!Directory.Exists(root)) return map;
    foreach (var file in Directory.EnumerateFiles(root, "*.sso", SearchOption.AllDirectories))
    {
      var text = File.ReadAllText(file);
      if (!Relevant(text)) continue;
      map.AddBlueprint(Path.GetFileNameWithoutExtension(file), text);
    }

    return map;
  }

  /// <summary>Blueprint text worth parsing: morpheme event hooks, or prop animation starts (propanim).</summary>
  public static bool Relevant(string text) =>
    text.Contains("MorphemeEvent", StringComparison.Ordinal) || text.Contains("MorphemeDurationEvent", StringComparison.Ordinal)
    || text.Contains("BlueprintAnimationLibraryStartanim", StringComparison.Ordinal);

  public void AddBlueprint(string source, string text)
  {
    JsonObject root;
    try { root = SslText.Parse(text); }
    catch { return; }
    AddNodes(source, root);
  }

  /// <summary>
  ///   mtl1-49: a blueprint graph already parsed — an <c>.sso</c> root, or one inline <c>prop_blueprints.blueprints.X</c>
  ///   of a class (weapons wire their effects there, e.g. <c>BlueprintWpnMeleeEffects</c>).
  /// </summary>
  public void AddNodes(string source, JsonObject root)
  {
    if (root.Obj("nodes") is not { } nodes) return;
    Files++;
    _sources.Add(source);

    // Data pipes into a node: "Target/pin/cb" -> string default of the pipe.
    var pipes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (var (_, node) in nodes)
    {
      if (node is not JsonObject n || n.Obj("value")?.Str("def") is not { Length: > 0 } value) continue;
      foreach (var target in Links(n))
        if (target.EndsWith("/cb", StringComparison.Ordinal))
        {
          var name = target[..target.IndexOf('/')];
          if (!pipes.TryGetValue(name, out var l)) pipes[name] = l = [];
          l.Add(value);
        }
    }

    AddAnims(source, nodes);

    foreach (var (_, node) in nodes)
    {
      if (node is not JsonObject n) continue;
      var type = n.Str("__type") ?? "";
      if (type == "BlueprintNodeBulkVfxOnMorphemeEvents" && n.Obj("eventInfos") is { } infos)
      {
        foreach (var (_, info) in infos)
        {
          if (info.Obj("mrEvent")?.Str("name") is not { Length: > 0 } ev || info?["vfxList"] is not JsonArray vfx) continue;
          foreach (var v in vfx)
            if (v?.GetValue<string>() is { Length: > 0 } s) Add(ev, new EventVfx(s, source + " (bulk)"));
        }

        continue;
      }

      var evName = n.Obj("mrEvent")?.Str("name") ?? n.Obj("mrDurationEvent")?.Str("name");
      if (evName is not { Length: > 0 }) continue;
      // Exec BFS (OnEnter / OnActivate / OnExit ...), a few hops deep, collecting effect starters and stoppers.
      // Whatever hangs off a duration event's OnDeactivate / OnExit happens at the event's end.
      var queue = new Queue<(string, int, bool)>(Pins(n).Where(l => l.Target.EndsWith("/Input", StringComparison.Ordinal))
        .Select(l => (l.Target[..l.Target.IndexOf('/')], 0, n.ContainsKey("mrDurationEvent") && EndPins.Contains(l.Pin))));
      var seen = new HashSet<string>(StringComparer.Ordinal);
      while (queue.Count > 0)
      {
        var (name, depth, atEnd) = queue.Dequeue();
        if (!seen.Add(name + (atEnd ? "#end" : "")) || nodes[name] is not JsonObject target) continue;
        var targetType = target.Str("__type") ?? "";
        if (Starters.Contains(targetType))
        {
          var locator = target.Obj("locator1Ref")?.Str("def") ?? target.Obj("locatorRef")?.Str("def") ?? "";
          var edge = TriangleStarters.Contains(targetType) ? Edge(target) : null;
          // mtl1-53: a triangle trail has no single attach locator (it sweeps locator1 -> locator2), so the instance root stays the scope root.
          if (edge is not null) locator = "";
          foreach (var effect in Names(target, name, pipes))
            Add(evName, new EventVfx(effect, source, locator, atEnd, edge));
        }
        else if (Stoppers.Contains(targetType))
        {
          var all = targetType == "BlueprintNodeStopAllSfx";
          var names = all ? Strings(target["ignoreSfxList"]).ToList()
            : targetType == "BlueprintNodeStopPackOfVfx" ? Strings(target["vfxList"]).ToList()
            : Names(target, name, pipes).ToList();
          if (all || names.Count > 0) AddStop(evName, new EventStop(source, names, all, atEnd));
        }

        if (depth < 6)
          foreach (var l in Links(target).Where(l => l.EndsWith("/Input", StringComparison.Ordinal)))
            queue.Enqueue((l[..l.IndexOf('/')], depth + 1, atEnd));
      }
    }
  }

  /// <summary>mtl1-53: locator1/2 (+ offsets) of a triangle trail starter (both param spellings), null without both locators.</summary>
  public static TrailEdge? Edge(JsonObject node)
  {
    string? Loc(string a, string b) => node.Obj(a)?.Str("def") is { Length: > 0 } s ? s : node.Obj(b)?.Str("def") is { Length: > 0 } t ? t : null;
    Vector3 Off(string a, string b)
    {
      var d = (node.Obj(a) ?? node.Obj(b))?.Obj("def");
      return d is null ? Vector3.Zero : new Vector3(d.Float("x", 0f), d.Float("y", 0f), d.Float("z", 0f));
    }

    var l1 = Loc("locator1", "locator1Ref");
    var l2 = Loc("locator2", "locator2Ref");
    return l1 is null || l2 is null ? null : new TrailEdge(l1, Off("locator1Offset", "locator1OffsetRef"), l2, Off("locator2Offset", "locator2OffsetRef"));
  }

  /// <summary>
  ///   propanim: for each Startanim/Stopanim node, walk the exec links (pins whose target is not a <c>/cb</c> data pipe)
  ///   backwards, a few hops, to a morpheme event node or a graph root (a node nothing executes). Each one reached is a
  ///   trigger; a duration event's OnDeactivate/OnExit pin fires at the event's end. DATA (the graph as cooked).
  /// </summary>
  private void AddAnims(string source, JsonObject nodes)
  {
    List<(string Name, JsonObject Node)>? animNodes = null;
    foreach (var (name, node) in nodes)
      if (node is JsonObject n && n.Str("__type") is "BlueprintAnimationLibraryStartanim" or "BlueprintAnimationLibraryStopanim")
        (animNodes ??= []).Add((name, n));
    if (animNodes is null) return;

    var incoming = new Dictionary<string, List<(string From, string Pin)>>(StringComparer.Ordinal);
    foreach (var (name, node) in nodes)
    {
      if (node is not JsonObject n) continue;
      foreach (var (pin, target) in Pins(n))
      {
        if (target.EndsWith("/cb", StringComparison.Ordinal)) continue;
        var to = target[..target.IndexOf('/')];
        if (!incoming.TryGetValue(to, out var l)) incoming[to] = l = [];
        l.Add((name, pin));
      }
    }

    foreach (var (animName, anim) in animNodes)
    {
      if (anim.Obj("animLink")?.Str("def") is not { Length: > 0 } link) continue;
      var stop = anim.Str("__type") == "BlueprintAnimationLibraryStopanim";
      var playback = anim.Obj("typePlayback")?.Str("def") ?? "";
      var seen = new HashSet<string>(StringComparer.Ordinal) { animName };
      var queue = new Queue<(string Node, int Depth)>();
      queue.Enqueue((animName, 0));
      while (queue.Count > 0)
      {
        var (cur, depth) = queue.Dequeue();
        if (!incoming.TryGetValue(cur, out var froms)) continue;
        foreach (var (from, pin) in froms)
        {
          if (nodes[from] is not JsonObject src) continue;
          var ev = src.Obj("mrEvent")?.Str("name") ?? src.Obj("mrDurationEvent")?.Str("name");
          if (ev is { Length: > 0 })
          {
            AddAnim(new EventAnim(ev, link, playback, stop, src.ContainsKey("mrDurationEvent") && EndPins.Contains(pin), source));
            continue;
          }

          if (!incoming.ContainsKey(from))
          {
            var type = src.Str("__type") ?? from;
            AddAnim(new EventAnim("@" + type + "." + pin, link, playback, stop, false, source));
            continue;
          }

          if (depth < 8 && seen.Add(from))
            queue.Enqueue((from, depth + 1));
        }
      }
    }
  }

  private void AddAnim(EventAnim a)
  {
    if (!_anims.Contains(a)) _anims.Add(a);
  }

  /// <summary>Effect names a starter/stopper node names: <c>*sfx*</c>/<c>*vfx*</c> param defaults plus string pipes into it.</summary>
  private static IEnumerable<string> Names(JsonObject node, string nodeName, Dictionary<string, List<string>> pipes)
  {
    foreach (var (key, value) in node)
      if (value is JsonObject param && (key.Contains("sfx", StringComparison.OrdinalIgnoreCase) || key.Contains("vfx", StringComparison.OrdinalIgnoreCase))
          && param.Str("def") is { Length: > 0 } def)
        yield return def;
    if (pipes.TryGetValue(nodeName, out var piped))
      foreach (var p in piped) yield return p;
  }

  private static IEnumerable<string> Strings(JsonNode? node)
  {
    if (node is JsonArray a)
      foreach (var v in a)
        if (v is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Length > 0) yield return s;
  }

  private void AddStop(string ev, EventStop stop)
  {
    if (!_stops.TryGetValue(ev, out var list)) _stops[ev] = list = [];
    if (!list.Any(x => x.All == stop.All && x.AtEnd == stop.AtEnd && x.Names.SequenceEqual(stop.Names, StringComparer.OrdinalIgnoreCase))) list.Add(stop);
  }

  private void Add(string ev, EventVfx vfx)
  {
    if (!_map.TryGetValue(ev, out var list)) _map[ev] = list = [];
    // Same effect from another blueprint is kept: context playback filters by the blueprints a class owns.
    if (!list.Any(x => x.Effect.Equals(vfx.Effect, StringComparison.OrdinalIgnoreCase) && x.AtEnd == vfx.AtEnd && x.Source.Equals(vfx.Source, StringComparison.OrdinalIgnoreCase))) list.Add(vfx);
  }

  private static IEnumerable<(string Pin, string Target)> Pins(JsonObject node)
  {
    foreach (var (key, value) in node)
      if (value is JsonArray a)
        foreach (var item in a)
          if (item is JsonValue v && v.TryGetValue<string>(out var s) && s.Contains('/'))
            yield return (key, s);
  }

  private static IEnumerable<string> Links(JsonObject node)
  {
    foreach (var (_, value) in node)
      if (value is JsonArray a)
        foreach (var item in a)
          if (item is JsonValue v && v.TryGetValue<string>(out var s) && s.Contains('/'))
            yield return s;
  }
}
