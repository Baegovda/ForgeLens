using System.Text.Json;

namespace LibSaber.SM2.Assets.Games.Poe2;

/// <summary>
///   PoE2 skill -> visual effect link from the game tables (PROVEN on Firebolt, M2 step 2), replacing the M1 name match:
///   ActiveSkills.Id &lt;- SkillArtVariations.ActiveSkill (Variant "Default" = variant 0)
///     .ProjectileArt -> ProjectilesArtVariations (Id, Variant 0) .Projectiles -> Projectiles.AOFiles (+ LoopAnimationIds / ImpactAnimationIds)
///     .AnimatedArt   -> MiscAnimatedArtVariations (Id, Variant 0) .Animation -> MiscAnimated.AOFile (explosions, flashes)
///   ActiveSkills.GrantedEffect -> GrantedEffects.Animation = the character cast animation id (Animation table).
///   Input: the dump-tables JSON folder (<c>&lt;prepared&gt;/tables/data/balance</c>).
/// </summary>
public sealed class Poe2Skills
{
  public sealed record Effect(string Kind, string Id, string Ao, string Loop, string Impact);
  public sealed record Skill(string Id, string Name, string GrantedEffect, string CastAnimation, string Types, List<Effect> Effects);

  public List<Skill> Skills { get; } = new();

  private static List<JsonElement> Rows(string dir, string table)
  {
    var p = Path.Combine(dir, table + ".datc64.json");
    if (!File.Exists(p)) return new();
    using var d = JsonDocument.Parse(File.ReadAllBytes(p));
    return d.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
  }

  private static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
  private static int Int(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : -1;
  private static IEnumerable<string> Ids(JsonElement e, string k)
  {
    if (!e.TryGetProperty(k, out var v)) yield break;
    if (v.ValueKind == JsonValueKind.Object)
    {
      if (v.TryGetProperty("Ids", out var ids) && ids.ValueKind == JsonValueKind.Array)
        foreach (var i in ids.EnumerateArray()) { if (i.ValueKind == JsonValueKind.String) yield return i.GetString()!; }
      else if (v.TryGetProperty("Id", out var id) && id.ValueKind == JsonValueKind.String) yield return id.GetString()!;
    }
    else if (v.ValueKind == JsonValueKind.Array)
      foreach (var i in v.EnumerateArray()) if (i.ValueKind == JsonValueKind.String) yield return i.GetString()!;
    else if (v.ValueKind == JsonValueKind.String) yield return v.GetString()!;
  }

  public static Poe2Skills Load(string tablesDir)
  {
    var r = new Poe2Skills();
    var ge = Rows(tablesDir, "grantedeffects").GroupBy(x => Str(x, "Id")).ToDictionary(g => g.Key, g => g.First());
    var art = Rows(tablesDir, "skillartvariations");
    var pav = Rows(tablesDir, "projectilesartvariations").Where(x => Int(x, "Variant") <= 0).GroupBy(x => Str(x, "Id")).ToDictionary(g => g.Key, g => g.ToList());
    var proj = Rows(tablesDir, "projectiles").GroupBy(x => Str(x, "Id")).ToDictionary(g => g.Key, g => g.First());
    var mav = Rows(tablesDir, "miscanimatedartvariations").Where(x => Int(x, "Variant") <= 0).GroupBy(x => Str(x, "Id")).ToDictionary(g => g.Key, g => g.ToList());
    var misc = Rows(tablesDir, "miscanimated").GroupBy(x => Str(x, "Id")).ToDictionary(g => g.Key, g => g.First());
    var artBySkill = new Dictionary<string, List<JsonElement>>();
    foreach (var a in art)
      foreach (var s in Ids(a, "ActiveSkill"))
        (artBySkill.TryGetValue(s, out var l) ? l : artBySkill[s] = new()).Add(a);
    foreach (var s in Rows(tablesDir, "activeskills"))
    {
      var id = Str(s, "Id");
      var g = Str(s, "GrantedEffect");
      var cast = g.Length > 0 && ge.TryGetValue(g, out var gr) ? string.Join(";", Ids(gr, "Animation")) : "";
      var effects = new List<Effect>();
      foreach (var a in artBySkill.GetValueOrDefault(id) ?? new())
      {
        foreach (var pa in Ids(a, "ProjectileArt"))
          foreach (var row in pav.GetValueOrDefault(pa) ?? new())
            foreach (var pid in Ids(row, "Projectiles"))
              if (proj.TryGetValue(pid, out var p))
                foreach (var ao in Ids(p, "AOFiles"))
                  effects.Add(new Effect("projectile", pid, ao, string.Join(";", Ids(p, "LoopAnimationIds")), string.Join(";", Ids(p, "ImpactAnimationIds"))));
        foreach (var ma in Ids(a, "AnimatedArt"))
          foreach (var row in mav.GetValueOrDefault(ma) ?? new())
            foreach (var mid in Ids(row, "Animation"))
              if (misc.TryGetValue(mid, out var m) && Str(m, "AOFile") is { Length: > 0 } ao)
                effects.Add(new Effect("animated", mid, ao, "", ""));
      }
      r.Skills.Add(new Skill(id, Str(s, "DisplayedName"), g, cast, string.Join(",", Ids(s, "ActiveSkillTypes")),
        effects.DistinctBy(e => e.Kind + e.Id + e.Ao).ToList()));
    }
    return r;
  }

  public string Tsv()
  {
    var sb = new System.Text.StringBuilder("skill_id\tname\tgranted_effect\tcast_animation\ttypes\teffect_kind\teffect_id\tao\tloop\timpact\n");
    foreach (var s in Skills)
    {
      if (s.Effects.Count == 0) sb.Append($"{s.Id}\t{s.Name}\t{s.GrantedEffect}\t{s.CastAnimation}\t{s.Types}\t\t\t\t\t\n");
      foreach (var e in s.Effects)
        sb.Append($"{s.Id}\t{s.Name}\t{s.GrantedEffect}\t{s.CastAnimation}\t{s.Types}\t{e.Kind}\t{e.Id}\t{e.Ao}\t{e.Loop}\t{e.Impact}\n");
    }
    return sb.ToString();
  }
}
