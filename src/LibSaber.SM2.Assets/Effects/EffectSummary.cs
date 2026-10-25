using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   Niagara-oriented digest of a retail .sfx graph.
///   <para>
///     Format (read from the text and the engine's type strings in sm2_retail.exe:
///     CPU_EMITTER @0x14363bc80, GPU_EMITTER @0x14363bcf0, sfx_part @0x14363c148,
///     sfx_group @0x143646930, nameTex @0x143644760, lwiGeom @0x143645a70,
///     ageSpline @0x143644580, constantFlow @0x143644cf8, emitDir @0x1436445f0):
///     the root (and every <c>sfx_group</c>) holds <c>list = { name = node }</c>, an <c>sfx_part</c> holds <c>emitters2 = { name = node }</c>; nodes are
///     <c>CPU_EMITTER</c> / <c>GPU_EMITTER</c> / <c>sfx_part</c> / effect bases such as
///     <c>sfx_blur_base</c>. Choice fields are tagged unions: an object whose
///     <c>__type</c> is <c>local__X</c> keeps every alternative the editor ever touched and
///     only member <c>X</c> is live (e.g. <c>emission.type.__type = local__constantFlow</c>
///     means <c>constantFlow {x,y}</c> particles/s, while the stored <c>explosion</c> and
///     <c>dynamicFlow</c> are dead). Splines are flat <c>[t0, v0, t1, v1, ...]</c> over
///     normalized particle age; colors are 0..255 RGBA.
///   </para>
/// </summary>
public static class EffectSummary
{
  /// <summary>Copy of the tree with every <c>local__X</c> union reduced to its live member.</summary>
  public static JsonNode? Resolve(JsonNode? node)
  {
    switch (node)
    {
      case JsonObject o:
      {
        var type = (o["__type"] as JsonValue)?.TryGetValue<string>(out var s) == true ? s : null;
        if (type is not null && type.StartsWith("local__", StringComparison.Ordinal))
        {
          var live = type[7..];
          var r = new JsonObject { ["kind"] = live };
          if (o[live] is JsonObject body)
            foreach (var kv in body) r[kv.Key] = Resolve(kv.Value);
          else if (o[live] is { } scalar)
            r["value"] = Resolve(scalar);
          return r;
        }
        var c = new JsonObject();
        foreach (var kv in o)
          if (kv.Key != "__node" && kv.Key != "editorOutlinerColor")
            c[kv.Key] = Resolve(kv.Value);
        return c;
      }
      case JsonArray a:
      {
        var c = new JsonArray();
        foreach (var v in a) c.Add(Resolve(v));
        return c;
      }
      default:
        return node?.DeepClone();
    }
  }

  public sealed record Result(JsonObject Summary, HashSet<string> Textures, HashSet<string> Meshes);

  public static Result Summarize(string name, JsonObject root)
  {
    var textures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var meshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var emitters = new JsonArray();
    var others = new JsonArray();
    Walk(root, name, emitters, others, textures, meshes);
    var summary = new JsonObject
    {
      ["effect"] = name,
      ["note"] = "Unions are resolved to the live member (kind). Splines are [t,v,...] over normalized age; colors 0..255. timeLife is seconds [min,max]; spawn constantFlow x..y is particles per second; explosion is a burst count.",
      ["emitterCount"] = emitters.Count,
      ["emitters"] = emitters,
      ["otherNodes"] = others,
      ["textures"] = new JsonArray(textures.OrderBy(x => x).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
      ["meshes"] = new JsonArray(meshes.OrderBy(x => x).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
    };
    return new Result(summary, textures, meshes);
  }

  private static void Walk(JsonObject node, string path, JsonArray emitters, JsonArray others, HashSet<string> tex, HashSet<string> mesh)
  {
    // Containers are "list" (root, sfx_group) and "emitters2" (sfx_part); walk every
    // object member so other container keys are found too.
    foreach (var kv in node)
    {
      if (kv.Value is not JsonObject child || kv.Key == "__node") continue;
      var type = Str(child["__type"]);
      var childPath = path + "/" + kv.Key;
      if (type is "CPU_EMITTER" or "GPU_EMITTER")
      {
        emitters.Add(Emitter(kv.Key, childPath, child, tex, mesh));
        continue;
      }
      if (type is not null && !type.StartsWith("local__", StringComparison.Ordinal) && child.ContainsKey("nameParent"))
        others.Add(new JsonObject { ["name"] = kv.Key, ["path"] = childPath, ["type"] = type });
      if (type is not null && type.StartsWith("local__", StringComparison.Ordinal))
      {
        Collect(child, tex, mesh, skipList: false);
        continue;
      }
      Walk(child, childPath, emitters, others, tex, mesh);
    }

    foreach (var kv in node)
    {
      var v = Str(kv.Value);
      if (v is { Length: > 0 } && v != "None")
      {
        if (kv.Key is "nameTex" or "pTex") tex.Add(v);
        else if (kv.Key == "nameGeomTpl") mesh.Add(v);
      }
    }
  }

  private static JsonObject Emitter(string name, string path, JsonObject e, HashSet<string> tex, HashSet<string> mesh)
  {
    var r = (JsonObject)Resolve(e)!;
    r.Remove("list");
    var myTex = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var myMesh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    Collect(r, myTex, myMesh, skipList: true);
    tex.UnionWith(myTex); mesh.UnionWith(myMesh);
    var tp = r["typeParams"] as JsonObject;
    var renderer = Str(tp?["kind"]) switch
    {
      "default" => "sprite",
      "lwiGeom" or "geomPrimitive" => "mesh",
      null => null,
      var k => k,
    };
    var visuals = Find(tp, "visuals") as JsonObject;
    var emission = r["emission"] as JsonObject;
    var pp = r["particleParams"] as JsonObject;
    return new JsonObject
    {
      ["name"] = name,
      ["path"] = path,
      ["sim"] = Str(e["__type"]) == "GPU_EMITTER" ? "GPU" : "CPU",
      ["visible"] = r["isVisible"]?.DeepClone(),
      ["renderer"] = renderer,
      ["blend"] = (visuals?["blendType"] ?? Find(tp, "blendType"))?.DeepClone(),
      ["textures"] = Arr(myTex),
      ["meshes"] = Arr(myMesh),
      ["spawn"] = emission?["type"]?.DeepClone(),
      ["shape"] = emission?["source"]?.DeepClone(),
      ["speedNormal"] = emission?["speedNorm"]?.DeepClone(),
      ["speedTangent"] = emission?["speedTang"]?.DeepClone(),
      ["coneAngle"] = emission?["angleEmission"]?.DeepClone(),
      ["lifetime"] = pp?["timeLife"]?.DeepClone(),
      ["size"] = pp?["size"]?.DeepClone(),
      ["color"] = visuals?["color"]?.DeepClone(),
      ["colorIntensity"] = visuals?["colorIntensity"]?.DeepClone(),
      ["gravity"] = (r["physics"] as JsonObject)?["gravity"]?.DeepClone(),
      ["emitScale"] = r["emitScale"]?.DeepClone(),
      ["timeScale"] = r["timeScale"]?.DeepClone(),
      ["prewarm"] = r["prewarmTime"]?.DeepClone(),
      ["transform"] = r["transform"]?.DeepClone(),
      ["parent"] = r["nameParent"]?.DeepClone(),
      ["full"] = r,
    };
  }

  private static JsonArray Arr(IEnumerable<string> s) => new(s.OrderBy(x => x).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());

  private static void Collect(JsonNode? n, HashSet<string> tex, HashSet<string> mesh, bool skipList)
  {
    if (n is JsonObject o)
    {
      foreach (var kv in o)
      {
        if (skipList && kv.Key == "list") continue;
        var v = Str(kv.Value);
        if (v is { Length: > 0 } && v != "None")
        {
          if (kv.Key is "nameTex" or "pTex" || kv.Key.EndsWith("Tex", StringComparison.Ordinal) && !kv.Key.StartsWith("is"))
            tex.Add(v);
          else if (kv.Key is "nameGeomTpl")
            mesh.Add(v);
        }
        Collect(kv.Value, tex, mesh, false);
      }
    }
    else if (n is JsonArray a)
      foreach (var v in a) Collect(v, tex, mesh, false);
  }

  private static JsonNode? Find(JsonNode? n, string key)
  {
    if (n is JsonObject o)
    {
      if (o[key] is { } hit) return hit;
      foreach (var kv in o) if (Find(kv.Value, key) is { } h) return h;
    }
    return null;
  }

  private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
