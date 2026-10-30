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
      var mesh = reader.Load(meshPath, ast, clip == "*" ? null : clip);
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

  /// <summary>Shared by GpuBench: load a PoE2 mesh with albedo textures for the viewer preview.</summary>
  public static ExportedMesh Load(string prepared, string spec, out PreviewSettings settings, out List<string> log)
  {
    var parts = spec.Split('|');
    var reader = new Poe2Reader(prepared);
    var mesh = reader.Load(parts[0], parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null, parts.Length > 2 ? parts[2] : null);
    var albedo = new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    foreach (var t in mesh.Parts.Select(p => p.Texture).Where(t => t != null).Distinct())
      if (reader.Texture(t!) is { } img) albedo[t!] = img;
    settings = new PreviewSettings { Albedo = albedo, InPlace = true };
    log = reader.Log;
    return mesh;
  }
}
