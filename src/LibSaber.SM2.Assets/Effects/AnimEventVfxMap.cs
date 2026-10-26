using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>One effect a morpheme event starts, with where the link was found.</summary>
public sealed record EventVfx(string Effect, string Source, string Locator = "");

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

  public IReadOnlyDictionary<string, List<EventVfx>> Events => _map;
  public int Files { get; private set; }

  public IReadOnlyList<EventVfx> For(string eventName) => _map.TryGetValue(eventName, out var list) ? list : [];

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
      // Exec BFS (OnEnter / OnActivate / OnExit ...), a few hops deep, collecting effect starters.
      var queue = new Queue<(string, int)>(Links(n).Where(l => l.EndsWith("/Input", StringComparison.Ordinal)).Select(l => (l[..l.IndexOf('/')], 0)));
      var seen = new HashSet<string>(StringComparer.Ordinal);
      while (queue.Count > 0)
      {
        var (name, depth) = queue.Dequeue();
        if (!seen.Add(name) || nodes[name] is not JsonObject target) continue;
        if (Starters.Contains(target.Str("__type") ?? ""))
        {
          var locator = target.Obj("locator1Ref")?.Str("def") ?? target.Obj("locatorRef")?.Str("def") ?? "";
          foreach (var (key, value) in target)
            if (value is JsonObject param && (key.Contains("sfx", StringComparison.OrdinalIgnoreCase) || key.Contains("vfx", StringComparison.OrdinalIgnoreCase))
                && param.Str("def") is { Length: > 0 } def)
              Add(evName, new EventVfx(def, source, locator));
          if (pipes.TryGetValue(name, out var piped))
            foreach (var p in piped) Add(evName, new EventVfx(p, source, locator));
        }

        if (depth < 6)
          foreach (var l in Links(target).Where(l => l.EndsWith("/Input", StringComparison.Ordinal)))
            queue.Enqueue((l[..l.IndexOf('/')], depth + 1));
      }
    }
  }

  private void Add(string ev, EventVfx vfx)
  {
    if (!_map.TryGetValue(ev, out var list)) _map[ev] = list = [];
    if (!list.Any(x => x.Effect.Equals(vfx.Effect, StringComparison.OrdinalIgnoreCase))) list.Add(vfx);
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
