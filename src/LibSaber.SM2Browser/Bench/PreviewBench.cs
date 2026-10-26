using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   Headless timing of the mesh preview: <c>--bench &lt;game folder&gt; &lt;model file name&gt; &lt;out folder&gt; [frames]</c>.
///   Renders the same model with the previous renderer (<see cref="MeshPreviewLegacy"/>) and the current one,
///   writes <c>bench.txt</c> and PNG frames, and exits without opening a window.
/// </summary>
internal static class PreviewBench
{
  public static int Run(string[] args)
  {
    var game = args.Length > 1 ? args[1] : "";
    var model = args.Length > 2 ? args[2] : "cc_titus.tpl";
    var output = args.Length > 3 ? args[3] : Path.Combine(Environment.CurrentDirectory, "bench");
    var frames = args.Length > 4 && int.TryParse(args[4], out var f) ? f : 16;
    Directory.CreateDirectory(output);
    var log = new StringBuilder();
    void Line(string text)
    {
      log.AppendLine(text);
      File.WriteAllText(Path.Combine(output, "bench.txt"), log.ToString());
    }

    try
    {
      Line($"bench {DateTime.Now:yyyy-MM-dd HH:mm:ss} cores={Environment.ProcessorCount} model={model}");
      var watch = Stopwatch.StartNew();
      var catalog = GameCatalog.Load(game);
      using var store = new ArchiveStore(catalog);
      Line($"catalog {catalog.FileCount:N0} files in {watch.Elapsed.TotalSeconds:0.0}s");
      var hits = catalog.FindByFileName(model);
      if (hits.Count == 0)
      {
        Line("model not found");
        return 2;
      }

      var index = hits[0];
      var mesh = TemplateMeshReader.Load(catalog, store, index);
      var textures = MainWindow.ResolvePreview(catalog, store, mesh, index);
      var verts = mesh.Positions.Count;
      var tris = mesh.Parts.Sum(p => p.IndexCount / 3);
      Line($"{catalog.GetFile(index).Path}: LOD0 {verts:N0} verts, {tris:N0} tris (parts), bones {mesh.Bones.Count}, albedo {textures.Albedo.Count}, normal {textures.Normals.Count}, spec {textures.Specular.Count}");
      DumpParts(mesh, Line);
      if (Environment.GetEnvironmentVariable("SM2_BENCH_TESS") == "1")
        ScanTessellation(catalog, store, mesh, Line);

      AnimationClip? clip = null;
      if (mesh.Bones.Count > 0)
      {
        var headers = SaniIndex.ReadHeaders(catalog, store);
        var names = mesh.Bones.Select(b => b.Name).ToArray();
        var compatible = SaniIndex.Compatible(headers, names);
        var pick = compatible.FirstOrDefault(h => h.Path.Contains("titus", StringComparison.OrdinalIgnoreCase) && h.Duration > 1)
          ;
        if (pick.Path is null && compatible.Count > 0)
          pick = compatible[0];
        if (pick.Path is not null)
        {
          clip = SaniReader.Load(catalog, store, pick.Index);
          Line($"clip {pick.Path} {pick.Duration:0.00}s ({compatible.Count} compatible)");
          mesh.Clip = clip;
          var rb = MeshPreview.InPlaceRebase(mesh);
          Line($"in-place rebase translation ({rb.M41:0.00}, {rb.M42:0.00}, {rb.M43:0.00}) rot diag ({rb.M11:0.00},{rb.M22:0.00},{rb.M33:0.00})");
          mesh.Clip = null;
        }
      }

      var settingsOld = new PreviewSettings { Albedo = textures.Albedo, Normals = textures.Normals, Specular = textures.Specular, MaxEdge = 1400 };
      var aspectW = 1100;
      var aspectH = 1000;
      var fit = MeshPreview.FitDistance(mesh, aspectW / (float)aspectH);

      // Same camera for every pass: the default orbit, and a close view of the left pauldron side.
      var views = new (string Name, OrbitCamera Camera)[]
      {
        ("default", new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f, Distance = 0 }),
        ("shoulder", new OrbitCamera { Yaw = 1.1f, Pitch = 0.25f, Distance = fit * 0.42f }),
      };
      if (PartCenter(mesh, out var pauldron, out var pauldronName))
      {
        Line($"pauldron view target {pauldronName} at {pauldron}");
        views = views.Append(("pauldron", new OrbitCamera { Yaw = 1.45f, Pitch = 0.12f, Distance = fit * 0.30f, Target = pauldron }))
          .Append(("pauldron_close", new OrbitCamera { Yaw = 1.45f, Pitch = 0.12f, Distance = fit * 0.16f, Target = pauldron }))
          .ToArray();
      }
      foreach (var (name, camera) in views)
      {
        var old = MeshPreviewLegacy.Render(mesh, aspectW, aspectH, camera, false, 0, out _, settingsOld);
        Png(Path.Combine(output, $"{name}_old_{aspectW}x{aspectH}.png"), old, aspectW, aspectH);
        var cur = MeshPreview.Render(mesh, aspectW, aspectH, camera, false, 0, out _, with2(1));
        Png(Path.Combine(output, $"{name}_new_{aspectW}x{aspectH}_1x.png"), cur, aspectW, aspectH);
        var aa = MeshPreview.Render(mesh, aspectW, aspectH, camera, false, 0, out _, with2(2));
        Png(Path.Combine(output, $"{name}_new_{aspectW}x{aspectH}_ssaa2.png"), aa, aspectW, aspectH);
      }

      PreviewSettings with2(int ss) => new()
      {
        Albedo = textures.Albedo, Normals = textures.Normals, Specular = textures.Specular, MaxEdge = 2048, Supersample = ss, InPlace = true,
      };

      void Time(string label, Func<int, byte[]> render)
      {
        render(0);
        var times = new List<double>();
        for (var i = 0; i < frames; i++)
        {
          var sw = Stopwatch.StartNew();
          render(i + 1);
          times.Add(sw.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        var median = times[times.Count / 2];
        Line($"{label,-48} median {median,7:0.0} ms  ({1000 / median,6:0.0} fps)  min {times[0]:0.0}  max {times[^1]:0.0}{(label.StartsWith("NEW") ? "  | " + MeshPreview.LastStages : "")}");
      }

      Line("");
      Line("-- orbit (static pose), camera yaw changes every frame --");
      foreach (var (w, h) in new[] { (1100, 1000), (1600, 1200) })
      {
        Time($"OLD {w}x{h}", i => MeshPreviewLegacy.Render(mesh, w, h, new OrbitCamera { Yaw = 0.7f + i * 0.05f, Pitch = 0.18f }, false, 0, out _, settingsOld with { MaxEdge = 2048 }));
        Time($"NEW {w}x{h} 1x", i => MeshPreview.Render(mesh, w, h, new OrbitCamera { Yaw = 0.7f + i * 0.05f, Pitch = 0.18f }, false, 0, out _, with2(1)));
        Time($"NEW {w}x{h} 2x2 SSAA", i => MeshPreview.Render(mesh, w, h, new OrbitCamera { Yaw = 0.7f + i * 0.05f, Pitch = 0.18f }, false, 0, out _, with2(2)));
      }

      if (clip is not null)
      {
        mesh.Clip = clip;
        Line("");
        Line("-- playback (skinned every frame) --");
        foreach (var (w, h) in new[] { (1100, 1000), (1600, 1200) })
        {
          Time($"OLD play {w}x{h}", i => MeshPreviewLegacy.Render(mesh, w, h, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, false, i * 0.033f, out _, settingsOld with { MaxEdge = 2048 }));
          Time($"NEW play {w}x{h} 1x", i => MeshPreview.Render(mesh, w, h, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, false, i * 0.033f, out _, with2(1)));
        }

        var playOld = MeshPreviewLegacy.Render(mesh, aspectW, aspectH, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, false, 0.5f, out _, settingsOld);
        Png(Path.Combine(output, "play_old_t0.5.png"), playOld, aspectW, aspectH);
        var playNew = MeshPreview.Render(mesh, aspectW, aspectH, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, false, 0.5f, out _, with2(2));
        Png(Path.Combine(output, "play_new_inplace_t0.5.png"), playNew, aspectW, aspectH);
        mesh.Clip = null;
      }

      Line("done");
      return 0;
    }
    catch (Exception ex)
    {
      Line(ex.ToString());
      return 1;
    }
  }

  /// <summary>
  ///   Which assets carry the tessellation_parameters block parsed by sm2_retail.exe fcn.1421df540
  ///   (smooth = pn_triangles | phong, displace = heightmap). Scans the model's own files, the .td
  ///   definitions its parts name, and counts every .td in the catalog.
  /// </summary>
  private static void ScanTessellation(GameCatalog catalog, ArchiveStore store, ExportedMesh mesh, Action<string> line)
  {
    var needles = new[] { "tessellation", "pn_triangles", "phong", "isAdaptiveSubdivision", "displacementMapping", "heightmap" };
    var partTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var part in mesh.Parts)
    {
      foreach (var name in new[] { part.Texture, part.Normal, part.Specular })
      {
        if (string.IsNullOrEmpty(name))
          continue;
        var stem = name;
        foreach (var suffix in new[] { "_nm", "_spec", "_dif", "_diff", "_albedo", "_alb" })
          if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            stem = stem[..^suffix.Length];
        partTextures.Add(stem);
      }
    }

    var tdTotal = 0;
    var tdHits = new Dictionary<string, int>();
    var examples = new List<string>();
    for (var i = 0; i < catalog.FileCount; i++)
    {
      var info = catalog.GetFile(i);
      var path = info.Path;
      var isTd = path.EndsWith(".td", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".td.resource", StringComparison.OrdinalIgnoreCase);
      var isTitus = path.Contains("cc_titus.tpl/", StringComparison.OrdinalIgnoreCase);
      var isPartTd = isTd && partTextures.Any(t => path.Contains(t, StringComparison.OrdinalIgnoreCase));
      if (!isTd && !isTitus)
        continue;
      if (info.Size > 64L * 1024 * 1024)
        continue;
      byte[] bytes;
      try
      {
        bytes = store.Read(i);
      }
      catch
      {
        continue;
      }

      var text = Encoding.Latin1.GetString(bytes);
      var found = needles.Where(n => text.Contains(n, StringComparison.OrdinalIgnoreCase)).ToArray();
      if (isTd)
      {
        tdTotal++;
        foreach (var n in found)
          tdHits[n] = tdHits.GetValueOrDefault(n) + 1;
        if (found.Contains("tessellation") && examples.Count < 8)
          examples.Add(path);
      }

      if (isTitus || isPartTd)
        line($"  tess-scan {path} ({bytes.Length:N0} B): {(found.Length == 0 ? "none" : string.Join(",", found))}");
    }

    line($"  tess-scan .td total {tdTotal:N0}; hits " + string.Join(", ", tdHits.Select(kv => $"{kv.Key}={kv.Value}")));
    foreach (var example in examples)
      line($"  tess-scan example td with 'tessellation': {example}");
  }

  internal static bool PartCenter(ExportedMesh mesh, out System.Numerics.Vector3 center, out string name)
  {
    center = default;
    name = "";
    var best = mesh.Parts
      .Where(p => p.Visible && (p.Name.Contains("shoulder", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("pauldron", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("SHLD", StringComparison.OrdinalIgnoreCase)))
      .OrderByDescending(p => p.IndexCount)
      .FirstOrDefault();
    if (best is null)
      return false;
    var min = new System.Numerics.Vector3(float.MaxValue);
    var max = new System.Numerics.Vector3(float.MinValue);
    for (var i = best.IndexStart; i < best.IndexStart + best.IndexCount && i < mesh.Indices.Count; i++)
    {
      var p = mesh.Positions[mesh.Indices[i]];
      min = System.Numerics.Vector3.Min(min, p);
      max = System.Numerics.Vector3.Max(max, p);
    }

    center = (min + max) * 0.5f;
    name = best.Name;
    return true;
  }

  private static void DumpParts(ExportedMesh mesh, Action<string> line)
  {
    line("  LOD0 parts: " + string.Join(", ", mesh.Parts.Select(p => $"{p.Name}({p.IndexCount / 3}{(p.Visible ? "" : ",hidden")})")));
    foreach (var part in mesh.Parts.Where(p => p.Name.Contains("pauldron", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("shoulder", StringComparison.OrdinalIgnoreCase)))
    {
      var used = new HashSet<int>();
      var indices = mesh.Indices;
      for (var i = part.IndexStart; i < part.IndexStart + part.IndexCount && i < indices.Count; i++)
        used.Add(indices[i]);
      line($"  part {part.Name}: {part.IndexCount / 3:N0} tris, {used.Count:N0} unique verts, visible={part.Visible}, normal={part.Normal}");
    }
  }

  internal static void Png(string path, byte[] bgra, int width, int height)
  {
    using var raw = new MemoryStream();
    for (var y = 0; y < height; y++)
    {
      raw.WriteByte(0);
      for (var x = 0; x < width; x++)
      {
        var i = (y * width + x) * 4;
        raw.WriteByte(bgra[i + 2]);
        raw.WriteByte(bgra[i + 1]);
        raw.WriteByte(bgra[i]);
      }
    }

    using var file = File.Create(path);
    file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var header = new byte[13];
    BigEndian(header, 0, width);
    BigEndian(header, 4, height);
    header[8] = 8;
    header[9] = 2;
    Chunk(file, "IHDR", header);
    using var compressed = new MemoryStream();
    using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
      raw.WriteTo(z);
    Chunk(file, "IDAT", compressed.ToArray());
    Chunk(file, "IEND", Array.Empty<byte>());
  }

  private static void BigEndian(byte[] buffer, int offset, int value)
  {
    buffer[offset] = (byte)(value >> 24);
    buffer[offset + 1] = (byte)(value >> 16);
    buffer[offset + 2] = (byte)(value >> 8);
    buffer[offset + 3] = (byte)value;
  }

  private static void Chunk(Stream stream, string type, byte[] data)
  {
    var length = new byte[4];
    BigEndian(length, 0, data.Length);
    stream.Write(length);
    var typeBytes = Encoding.ASCII.GetBytes(type);
    stream.Write(typeBytes);
    stream.Write(data);
    var crc = Crc(typeBytes, data);
    var crcBytes = new byte[4];
    BigEndian(crcBytes, 0, (int)crc);
    stream.Write(crcBytes);
  }

  private static uint Crc(byte[] type, byte[] data)
  {
    var crc = 0xFFFFFFFFu;
    foreach (var buffer in new[] { type, data })
    {
      foreach (var b in buffer)
      {
        crc ^= b;
        for (var k = 0; k < 8; k++)
          crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
      }
    }

    return ~crc;
  }
}
