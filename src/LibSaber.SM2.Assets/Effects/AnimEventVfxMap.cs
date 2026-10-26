using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>One effect a morpheme event starts, with where the link was found.</summary>
/// <param name="AtEnd">Reached from a duration event's <c>OnDeactivate</c>/<c>OnExit</c>: fires at the event's end.</param>
public sealed record EventVfx(string Effect, string Source, string Locator = "", bool AtEnd = false);

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
  private static readonly HashSet<string> EndPins = new(StringComparer.Ordinal) { "OnDeactivate", "OnExit" };

  public IReadOnlyDictionary<string, List<EventVfx>> Events => _map;
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
      if (!text.Contains("MorphemeEvent", StringComparison.Ordinal) && !text.Contains("MorphemeDurationEvent", StringComparison.Ordinal)) continue;
      map.AddBlueprint(Path.GetFileNameWithoutExtension(file), text);
    }

    return map;
  }

  public void AddBlueprint(string source, string text)
  {
    JsonObject root;
    try { root = SslText.Parse(text); }
    catch { return; }
    if (root.Obj("nodes") is not { } nodes) return;
    Files++;

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
          foreach (var effect in Names(target, name, pipes))
            Add(evName, new EventVfx(effect, source, locator, atEnd));
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
    if (!list.Any(x => x.Effect.Equals(vfx.Effect, StringComparison.OrdinalIgnoreCase) && x.AtEnd == vfx.AtEnd)) list.Add(vfx);
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
