using System.Numerics;
using System.Text.Json.Nodes;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>A <c>tpl_markup_locator</c> entity: a named frame attached to a template object (<c>objectInfos[].objectId.name</c>).</summary>
public sealed record TplLocator(string Name, string Parent, Vector3 Position, Vector3 RotationDegrees)
{
  /// <summary>
  ///   Locator frame relative to its parent object (row-vector, like <see cref="Bone.Local"/>): rotate, then translate.
  ///   <c>m3dTRS.rotation</c> is an euler3 in degrees (meta_desc); the X-then-Y-then-Z order is HEURISTIC (every retail
  ///   CDT locator seen so far has rotation 0).
  /// </summary>
  public Matrix4x4 Local => Rotation * Matrix4x4.CreateTranslation(Position);

  public Matrix4x4 Rotation =>
    Matrix4x4.CreateRotationX(RotationDegrees.X * MathF.PI / 180f) *
    Matrix4x4.CreateRotationY(RotationDegrees.Y * MathF.PI / 180f) *
    Matrix4x4.CreateRotationZ(RotationDegrees.Z * MathF.PI / 180f);
}

/// <summary>
///   <c>.tpl_markup</c> (SSL text next to the .tpl). The Mod Tools schema makes CDT shape bones a <c>link_tpl_obj</c>
///   request, i.e. the template's object list as the editor sees it, which includes the markup locators: calgar's shared
///   CDT box bone <c>Backpack_CDT_L</c> is not a .tpl object but a locator on <c>powerplant_backpack</c>.
/// </summary>
public static class TplMarkup
{
  public static List<TplLocator> Locators(string text)
  {
    var list = new List<TplLocator>();
    var root = SslText.Parse(text);
    if (root["objectInfos"] is not JsonArray infos)
      return list;
    foreach (var info in infos.OfType<JsonObject>())
    {
      var parent = Str(info["objectId"]?["name"]);
      if (parent.Length == 0 || info["entities"] is not JsonArray entities)
        continue;
      foreach (var e in entities.OfType<JsonObject>())
      {
        if (Str(e["__type"]) != "tpl_markup_locator")
          continue;
        var name = Str(e["name"]);
        if (name.Length == 0)
          continue;
        var t = e["localTransform"] as JsonObject;
        list.Add(new TplLocator(name, parent, Vec(t?["position"]), Vec(t?["rotation"])));
      }
    }

    return list;
  }

  /// <summary>Locators of the template at <paramref name="fileIndex"/> (.tpl or .tpl.resource); empty when it has no markup.</summary>
  public static List<TplLocator> Find(GameCatalog catalog, ArchiveStore store, int fileIndex)
  {
    var path = catalog.GetFile(fileIndex).Path;
    foreach (var name in new[] { path + "_markup", path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ? path[..^".resource".Length] + "_markup" : "" })
    {
      if (name.Length == 0)
        continue;
      var index = catalog.ResolveReference(name, fileIndex);
      if (index < 0)
        continue;
      var bytes = store.Read(index);
      return SslText.LooksText(bytes) ? Locators(System.Text.Encoding.UTF8.GetString(bytes)) : new List<TplLocator>();
    }

    return new List<TplLocator>();
  }

  private static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

  private static float Num(JsonNode? n)
  {
    if (n is not JsonValue v) return 0f;
    if (v.TryGetValue<long>(out var l)) return l;
    if (v.TryGetValue<double>(out var d)) return (float)d;
    return 0f;
  }

  private static Vector3 Vec(JsonNode? n) => n is JsonArray a ? new Vector3(a.Count > 0 ? Num(a[0]) : 0, a.Count > 1 ? Num(a[1]) : 0, a.Count > 2 ? Num(a[2]) : 0) : Vector3.Zero;
}
