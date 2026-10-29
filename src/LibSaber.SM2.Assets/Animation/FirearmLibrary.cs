using System.Text.Json.Nodes;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>One barrel's shoot settings (<c>BarrelDescription.shootSettings</c>, DATA) plus the firearm's reload settings.</summary>
/// <param name="AffectsShootAnim"><c>fireRateAffectShootAnim</c>; null = not written (its default lives in the
///   shoot_settings bytecode initializer, not decoded).</param>
public sealed record FirearmFireRate(string Uid, string Barrel, double ShotsPerMin, bool? AffectsShootAnim, string? ShootLogic,
  double? ReloadAnimSpeedScale, double? ReloadTimeLength)
{
  /// <summary>Seconds between shots: 60 / shotsPerMin (FixedFireRate, units "shots/min" in its bytecode).</summary>
  public double Interval => ShotsPerMin > 0 ? 60.0 / ShotsPerMin : 0;
}

/// <summary>
///   propanim: <c>ssl/weapons/creator/firearm_library_pve.sso</c> → <c>firearms.&lt;uid&gt;</c> (the uid is the
///   firearm class name, e.g. <c>arifle_bolt_rifle</c>; its <c>mgs/&lt;uid&gt;/&lt;uid&gt;_client.cls</c> names the tpl in
///   <c>geom.nameTpl</c>). DATA.
/// </summary>
public static class FirearmLibrary
{
  public const string PathSuffix = "weapons/creator/firearm_library_pve.sso";

  public static Dictionary<string, List<FirearmFireRate>> Parse(string text)
  {
    var map = new Dictionary<string, List<FirearmFireRate>>(StringComparer.OrdinalIgnoreCase);
    if (SslText.Parse(text)["firearms"] is not JsonObject firearms) return map;
    foreach (var (uid, node) in firearms)
    {
      if (node is not JsonObject fa || fa["barrelDescriptions"] is not JsonObject barrels) continue;
      var reload = fa["reloadSettings"] as JsonObject;
      var list = new List<FirearmFireRate>();
      foreach (var (barrel, bn) in barrels)
      {
        if (bn is not JsonObject b || b["shootSettings"] is not JsonObject ss) continue;
        var spm = Num((ss["fireRate"] as JsonObject)?["shotsPerMin"]);
        if (spm is not > 0) continue;
        list.Add(new FirearmFireRate(uid, barrel, spm.Value, Bool(ss["fireRateAffectShootAnim"]), Str(ss["shootLogicUid"]),
          Num(reload?["reloadAnimSpeedScale"]), Num(reload?["reloadTimeLength"])));
      }

      if (list.Count > 0) map.TryAdd(uid, list);
    }

    return map;
  }

  /// <summary>INFERRED (naming): firearm class <c>&lt;uid&gt;_client</c> / <c>_authority</c> / <c>_server</c> → uid.</summary>
  public static string UidOf(string className)
  {
    foreach (var s in new[] { "_client", "_authority", "_server" })
      if (className.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return className[..^s.Length];
    return className;
  }

  private static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : n is JsonValue l && l.TryGetValue<long>(out var x) ? x : null;
  private static bool? Bool(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
  private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
