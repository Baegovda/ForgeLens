using System.Text;
using LibSaber.SM2.Assets.Games.Poe2;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Textures;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   PoE2 M1: <c>--poe2export &lt;prepared folder&gt; &lt;out folder&gt; &lt;mesh.sm|.fmt&gt; [rig.ast] [clip|*]</c>.
///   Loads through <see cref="Poe2Reader"/> into the shared <see cref="ExportedMesh"/> and writes the same files the SM2
///   export writes: glTF (.gltf), Unreal-space FBX, PNG per texture, and poe2export.txt (counts, clip list).
///   Viewer render of the same mesh: <c>--glbench poe2:&lt;prepared&gt; "mesh|ast|clip" &lt;out&gt;</c>.
/// </summary>
internal static class Poe2Bench
{
  public static int Run(string[] args)
  {
    var prepared = args[1]; var output = args[2]; var meshPath = args[3];
    var ast = args.Length > 4 && args[4] != "-" ? args[4] : null;
    var clip = args.Length > 5 ? args[5] : null;
    Directory.CreateDirectory(output);
    var log = new StringBuilder();
    var reader = new Poe2Reader(prepared);
    try
    {
      var mesh = meshPath.EndsWith(".ao", StringComparison.OrdinalIgnoreCase)
        ? reader.LoadEffect(meshPath, clip == "*" ? null : clip)
        : reader.Load(meshPath, ast, clip == "*" ? null : clip);
      var name = mesh.Name.ToLowerInvariant();
      var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var tex in mesh.Parts.SelectMany(p => p.Maps.Select(m => m.Name)).Distinct())
      {
        var img = reader.Texture(tex);
        if (img == null) continue;
        var png = Path.Combine(output, Path.GetFileNameWithoutExtension(tex) + ".png");
        ImageWriters.WritePng(png, img.Bgra, img.Width, img.Height);
        files[tex] = png;
        log.AppendLine($"texture {tex} {img.Width}x{img.Height} -> {Path.GetFileName(png)}");
      }
      foreach (var p in mesh.Parts)
        log.AppendLine($"part {p.Name} tris {p.IndexCount / 3} mat {p.Variant} albedo {p.Texture} normal {p.Normal}");
      GltfWriter.Write(Path.Combine(output, name + ".gltf"), mesh, ExportSpace.Unreal);
      FbxWriter.Write(Path.Combine(output, name + ".fbx"), mesh, ExportSpace.Unreal, files);
      if (ast != null && clip == "*")
        log.AppendLine("animations: " + string.Join(", ", reader.Animations(ast)));
      if (mesh.Clip != null)
        log.AppendLine($"clip {mesh.Clip.Name} {mesh.Clip.Duration:0.000}s tracks {mesh.Clip.Tracks.Count} keys {mesh.Clip.Tracks.Sum(t => t.Rotation.Count + t.Translation.Count + t.Scale.Count)} {string.Join(";", mesh.Clip.Notes)}");
    }
    catch (Exception e)
    {
      log.AppendLine(e.ToString());
      File.WriteAllText(Path.Combine(output, "poe2export.txt"), string.Join("\n", reader.Log) + "\n" + log);
      return 1;
    }
    File.WriteAllText(Path.Combine(output, "poe2export.txt"), string.Join("\n", reader.Log) + "\n" + log);
    return 0;
  }

  /// <summary>
  ///   <c>--poe2skills &lt;prepared&gt; &lt;out.tsv&gt;</c>: every ActiveSkills row with its GrantedEffect and the Projectiles rows
  ///   whose id matches the skill's ActionType or id (HEURISTIC name match, not a proven table link), with their effect .ao files.
  /// </summary>
  public static int Skills(string[] args)
  {
    var tables = Path.Combine(args[1], "tables", "data", "balance");
    using var skills = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(tables, "activeskills.datc64.json")));
    using var projs = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(tables, "projectiles.datc64.json")));
    static string Norm(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    var projList = projs.RootElement.EnumerateArray().Select(p => (Id: p.GetProperty("Id").GetString() ?? "",
      Ao: string.Join(";", p.GetProperty("AOFiles").EnumerateArray().Select(a => a.GetString())),
      Loop: string.Join(";", p.GetProperty("LoopAnimationIds").EnumerateArray().Select(a => a.GetString())),
      Impact: string.Join(";", p.GetProperty("ImpactAnimationIds").EnumerateArray().Select(a => a.GetString())))).ToList();
    var sb = new StringBuilder("skill_id\tname\tgranted_effect\taction_type\ttypes\tprojectile\tao\tloop_anim\timpact_anim\n");
    int total = 0, linked = 0;
    foreach (var s in skills.RootElement.EnumerateArray())
    {
      total++;
      var id = s.GetProperty("Id").GetString() ?? "";
      var name = s.TryGetProperty("DisplayedName", out var dn) ? dn.GetString() : "";
      var ge = s.TryGetProperty("GrantedEffect", out var g) && g.ValueKind == System.Text.Json.JsonValueKind.String ? g.GetString() : "";
      var at = s.TryGetProperty("ActionType", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.Object && a.TryGetProperty("Id", out var ai) ? ai.GetString() ?? "" : "";
      var types = s.TryGetProperty("ActiveSkillTypes", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Object && t.TryGetProperty("Ids", out var ti)
        ? string.Join(",", ti.EnumerateArray().Where(x => x.ValueKind == System.Text.Json.JsonValueKind.String).Select(x => x.GetString())) : "";
      var keys = new[] { Norm(at), Norm(id) }.Where(k => k.Length >= 4).ToArray();
      var match = projList.FirstOrDefault(p => p.Ao.Length > 0 && keys.Any(k => Norm(p.Id.Split('/').Last()) == k));
      if (match.Id != null) linked++;
      sb.Append($"{id}\t{name}\t{ge}\t{at}\t{types}\t{match.Id}\t{match.Ao}\t{match.Loop}\t{match.Impact}\n");
    }
    File.WriteAllText(args[2], sb.ToString());
    Console.WriteLine($"skills {total}, with a name-matched projectile effect {linked}, projectiles {projList.Count} ({projList.Count(p => p.Ao.Length > 0)} with .ao)");
    return 0;
  }

  /// <summary>Shared by GpuBench: load a PoE2 mesh with albedo textures for the viewer preview.</summary>
  public static ExportedMesh Load(string prepared, string spec, out PreviewSettings settings, out List<string> log)
  {
    var parts = spec.Split('|');
    var reader = new Poe2Reader(prepared);
    var mesh = parts[0].EndsWith(".ao", StringComparison.OrdinalIgnoreCase)
      ? reader.LoadEffect(parts[0], parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null)
      : reader.Load(parts[0], parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null, parts.Length > 2 ? parts[2] : null);
    var albedo = new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    foreach (var t in mesh.Parts.Select(p => p.Texture).Where(t => t != null).Distinct())
      if (reader.Texture(t!) is { } img) albedo[t!] = img;
    settings = new PreviewSettings { Albedo = albedo, InPlace = true };
    log = reader.Log;
    return mesh;
  }
}
