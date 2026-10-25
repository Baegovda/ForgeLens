using System.Text;
using System.Text.Json;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Textures;

namespace LibSaber.SM2.Assets.Export;

public sealed class ExportSettings
{
  public bool WriteDds { get; init; } = true;
  public bool WritePng { get; init; } = true;
  public bool WriteTga { get; init; } = true;
  public bool WriteGltf { get; init; } = true;
  public bool WriteFbx { get; init; } = true;
  public bool WriteMaterials { get; init; } = true;
  public bool WriteModelTextures { get; init; } = true;
  public bool CopyOriginalBytes { get; init; }
  public ExportSpace Space { get; init; } = ExportSpace.Unreal;
  /// <summary>
  ///   0 exports the meshes Index leaves checked (LOD0 plus shells such as <c>_SIM</c>).
  ///   1 and up export that smaller vertex pool, which Index hides as a lower LOD or a collision shell.
  /// </summary>
  public int Lod { get; init; }
  /// <summary>Checked meshes for a previewed catalog index. Other files keep Index's defaults.</summary>
  public Dictionary<int, MeshVisibility>? PartVisibility { get; set; }
  /// <summary>Clips written beside the bind mesh as <c>{model}__{clip}.fbx</c> and <c>.glb</c>. The bind file itself stays unanimated.</summary>
  public IReadOnlyList<AnimationClip> Clips { get; init; } = Array.Empty<AnimationClip>();
}

public sealed class ExportReport
{
  public int Written { get; set; }
  public int Skipped { get; set; }
  public List<string> Messages { get; } = new();
}

public static class AssetExporter
{

  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  public static ExportReport Export(
    GameCatalog catalog,
    ArchiveStore store,
    IReadOnlyList<int> files,
    string outputDirectory,
    ExportSettings settings,
    IProgress<string>? progress = null,
    CancellationToken cancellation = default)
  {
    Directory.CreateDirectory(outputDirectory);
    var report = new ExportReport();
    var notePath = Path.Combine(outputDirectory, "export-notes.txt");
    File.WriteAllText(notePath, Notes(settings));
    report.Written++;
    var batch = new string[files.Count];
    for (var i = 0; i < files.Count; i++)
      batch[i] = catalog.GetFile(files[i]).Path;

    foreach (var index in files)
    {
      cancellation.ThrowIfCancellationRequested();
      var file = catalog.GetFile(index);
      progress?.Report(file.Path);
      try
      {
        switch (file.Kind)
        {
          case AssetKind.Texture:
            ExportTexture(catalog, store, index, file, outputDirectory, settings, report, batch);
            break;
          case AssetKind.Model:
            ExportModel(catalog, store, index, file, outputDirectory, settings, report, batch);
            break;
          case AssetKind.Animation:
            ExportAnimation(catalog, store, index, file, outputDirectory, settings, report, batch);
            break;
          case AssetKind.Effect:
            ExportEffect(catalog, store, index, file, outputDirectory, settings, report, batch);
            break;
          default:
            WriteOriginal(store, index, file, outputDirectory, report, file.Kind == AssetKind.TextureMip
              ? "Loose mip payload. Preview and image export use the .pct.resource that names this file."
              : null);
            break;
        }

        if (settings.CopyOriginalBytes && file.Kind is AssetKind.Texture or AssetKind.Model or AssetKind.Animation)
          WriteOriginal(store, index, file, outputDirectory, report, null);
      }
      catch (Exception ex)
      {
        report.Skipped++;
        report.Messages.Add(file.Path + ": " + ex.Message);
      }
    }

    return report;
  }

  private static void ExportTexture(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var document = TextureLoader.Load(catalog, store, index);
    if (document.Payload.Length == 0)
    {
      report.Skipped++;
      report.Messages.Add(file.Path + ": " + (document.Message.Length == 0 ? "No texture payload." : document.Message));
      return;
    }

    if (document.DxgiFormat == 0)
    {
      WriteOriginal(store, index, file, outputDirectory, report, "No DDS mapping for this texture format. Original bytes were copied.");
      return;
    }

    if (settings.WriteDds)
    {
      var path = Destination(outputDirectory, file.Path, ".dds", batch);
      DdsWriter.Write(
        path,
        document.Payload,
        document.Width,
        document.Height,
        document.MipCount,
        document.FaceCount,
        document.DxgiFormat,
        TextureInfo.IsBlock(TextureInfo.PreviewOf(document.Format)));
      report.Written++;
    }

    if (!settings.WritePng && !settings.WriteTga)
      return;

    try
    {
      var pixels = document.Decode(0, 0, out var width, out var height, out var note);
      if (note.Length > 0)
        report.Messages.Add(file.Path + ": " + note);
      if (settings.WritePng)
      {
        ImageWriters.WritePng(Destination(outputDirectory, file.Path, ".png", batch), pixels, width, height);
        report.Written++;
      }

      if (settings.WriteTga)
      {
        ImageWriters.WriteTga(Destination(outputDirectory, file.Path, ".tga", batch), pixels, width, height);
        report.Written++;
      }
    }
    catch (Exception ex)
    {
      report.Messages.Add(file.Path + ": image preview export skipped (" + ex.Message + "). DDS was still requested separately.");
    }
  }

  private static void ExportModel(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var mesh = TemplateMeshReader.Load(catalog, store, index);
    if (mesh.Message.Length > 0)
      report.Messages.Add(file.Path + ": " + mesh.Message);
    if (settings.PartVisibility is not null && settings.PartVisibility.TryGetValue(index, out var visibility))
      visibility.Apply(mesh);
    var exportMesh = SelectExportGeometry(mesh, settings.Lod);
    AttachResolvedNormals(catalog, index, exportMesh);

    if (!exportMesh.HasTriangles && exportMesh.Bones.Count == 0)
    {
      report.Skipped++;
      if (settings.CopyOriginalBytes || file.Extension is ".tpl_data" or ".lods_base" or ".geom_dbg" or ".tpl_markup")
        WriteOriginal(store, index, file, outputDirectory, report, null);
      return;
    }

    exportMesh.Clip = null;
    IReadOnlyDictionary<string, string>? textureFiles = null;
    if (settings.WriteGltf)
    {
      GltfWriter.Write(Destination(outputDirectory, file.Path, ".glb", batch), exportMesh, settings.Space);
      report.Written++;
    }

    if (settings.WriteFbx && exportMesh.HasTriangles)
    {
      var fbxPath = Destination(outputDirectory, file.Path, ".fbx", batch);
      textureFiles = TextureFiles(catalog, store, index, exportMesh, fbxPath, outputDirectory, settings, report, batch);
      FbxWriter.Write(fbxPath, exportMesh, settings.Space, textureFiles);
      report.Written++;
      if (settings.WriteMaterials)
      {
        WriteMaterialSidecar(fbxPath, exportMesh, textureFiles, report);
        WriteUnrealScript(Path.Combine(Path.GetDirectoryName(fbxPath)!, "import_sm2_materials.py"), report);
      }
    }

    var usedClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var clip in settings.Clips)
    {
      if (!clip.HasCurves || exportMesh.Bones.Count == 0)
        continue;
      var token = ClipToken(clip.Name);
      var unique = token;
      var suffix = 2;
      while (!usedClips.Add(unique))
        unique = token + "_" + suffix++;
      exportMesh.Clip = clip;
      var relative = AnimationRelative(file.Path, unique);
      if (settings.WriteGltf)
      {
        GltfWriter.Write(Destination(outputDirectory, relative, ".glb", batch), exportMesh, settings.Space);
        report.Written++;
      }

      if (settings.WriteFbx && exportMesh.HasTriangles)
      {
        var fbxPath = Destination(outputDirectory, relative, ".fbx", batch);
        FbxWriter.Write(fbxPath, exportMesh, settings.Space, textureFiles);
        report.Written++;
      }

      var bound = clip.Tracks.Count(track => exportMesh.Bones.Exists(bone => bone.Name == track.BoneName));
      report.Messages.Add($"{clip.Name}: {bound} of {clip.Tracks.Count} tracks match {exportMesh.Name}, {clip.Duration:0.###}s at {clip.SampleRate:0.###} fps.");
    }

    exportMesh.Clip = null;

    WriteMaterial(outputDirectory, file.Path, exportMesh.Material, settings, report, batch);
    if (settings.WriteModelTextures)
      ExportModelTextures(catalog, store, index, exportMesh, outputDirectory, settings, report, batch);
  }

  internal static ExportedMesh SelectExportGeometry(ExportedMesh mesh, int lod)
  {
    if (lod < 0)
      throw new InvalidDataException("LOD is negative.");
    if (lod == 0)
      return mesh.VisibleGeometry();
    if (lod > mesh.Levels.Count)
      throw new InvalidDataException($"LOD {lod} is not in this model. Lod 0 is the Index-visible set, and this model has {mesh.Levels.Count} smaller vertex pools.");

    var level = mesh.Levels[lod - 1];
    var copy = new ExportedMesh
    {
      Name = mesh.Name,
      Material = mesh.Material,
      Clip = mesh.Clip,
      Message = mesh.Message,
    };
    copy.Bones.AddRange(mesh.Bones);
    copy.Positions.AddRange(level.Positions);
    copy.Normals.AddRange(level.Normals);
    copy.Uvs.AddRange(level.Uvs);
    copy.Indices.AddRange(level.Indices);
    copy.Skin.AddRange(level.Skin);
    foreach (var part in level.Parts)
    {
      if (part.IndexCount < 3)
        continue;
      var copied = part.Copy(part.IndexStart, part.IndexCount);
      copied.Visible = true;
      copy.Parts.Add(copied);
    }

    return copy;
  }

  private static void AttachResolvedNormals(GameCatalog catalog, int index, ExportedMesh mesh)
  {
    foreach (var part in mesh.Parts)
    {
      if (!string.IsNullOrWhiteSpace(part.Normal) || string.IsNullOrWhiteSpace(part.Texture))
        continue;
      var candidate = part.Texture + "_nm";
      if (TemplateMeshReader.ResolveTexture(catalog, index, candidate) >= 0)
        part.Normal = candidate;
    }
  }

  internal static string RelativeTexturePath(string modelFile, string textureFile)
  {
    var modelDir = Path.GetDirectoryName(Path.GetFullPath(modelFile))!;
    return Path.GetRelativePath(modelDir, Path.GetFullPath(textureFile)).Replace('\\', '/');
  }

  private static Dictionary<string, string> TextureFiles(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    ExportedMesh mesh,
    string modelFile,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var extension = settings.WritePng ? ".png" : settings.WriteTga ? ".tga" : settings.WriteDds ? ".dds" : null;
    if (extension is null)
      return map;

    foreach (var name in TextureNames(mesh))
    {
      var resolved = TemplateMeshReader.ResolveTexture(catalog, index, name);
      var linkedKind = resolved < 0 ? AssetKind.Other : catalog.GetFile(resolved).Kind;
      if (resolved < 0 || linkedKind != AssetKind.Texture || !PixelsPresent(catalog, store, resolved))
      {
        if (!IsGenerated(mesh, name))
          ForgetTexture(mesh, name);
        continue;
      }

      var linked = catalog.GetFile(resolved);
      var destination = Destination(outputDirectory, linked.Path, extension, batch);
      map[name] = RelativeTexturePath(modelFile, destination);
    }

    foreach (var solid in GeneratedSolids(mesh))
    {
      if (map.ContainsKey(solid.Name))
        continue;
      var destination = Destination(outputDirectory, "pct/" + solid.Name + ".pct.resource", extension, batch);
      WriteSolid(destination, solid, extension);
      map[solid.Name] = RelativeTexturePath(modelFile, destination);
      report.Written++;
    }

    return map;
  }

  private static bool PixelsPresent(GameCatalog catalog, ArchiveStore store, int index)
  {
    try
    {
      var document = TextureLoader.Load(catalog, store, index);
      return document.CanDecode && document.Payload.Length > 0;
    }
    catch (Exception)
    {
      return false;
    }
  }

  private static bool IsGenerated(ExportedMesh mesh, string name)
  {
    foreach (var part in mesh.Parts)
    {
      if (part.Maps.Any(map => map.Generated && string.Equals(map.Name, name, StringComparison.OrdinalIgnoreCase)))
        return true;
    }

    return false;
  }

  private static void ForgetTexture(ExportedMesh mesh, string name)
  {
    foreach (var part in mesh.Parts)
    {
      if (string.Equals(part.Texture, name, StringComparison.OrdinalIgnoreCase))
        part.Texture = null;
      if (string.Equals(part.Normal, name, StringComparison.OrdinalIgnoreCase))
        part.Normal = null;
      if (string.Equals(part.Specular, name, StringComparison.OrdinalIgnoreCase))
        part.Specular = null;
      if (string.Equals(part.Emissive, name, StringComparison.OrdinalIgnoreCase))
        part.Emissive = null;
      foreach (var map in part.Maps)
      {
        if (!map.Generated && string.Equals(map.Name, name, StringComparison.OrdinalIgnoreCase))
          map.Missing = true;
      }
    }
  }

  private static void WriteSolid(string path, MaterialMap solid, string extension)
  {
    var pixels = new byte[4 * 4 * 4];
    for (var i = 0; i < pixels.Length; i += 4)
    {
      pixels[i] = solid.SolidB;
      pixels[i + 1] = solid.SolidG;
      pixels[i + 2] = solid.SolidR;
      pixels[i + 3] = solid.SolidA;
    }

    if (extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
      ImageWriters.WriteTga(path, pixels, 4, 4);
    else
      ImageWriters.WritePng(path, pixels, 4, 4);
  }

  private static IEnumerable<MaterialMap> GeneratedSolids(ExportedMesh mesh)
  {
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var part in mesh.Parts)
    {
      foreach (var map in part.Maps)
      {
        if (map.Generated && seen.Add(map.Name))
          yield return map;
      }
    }
  }

  internal static void ExportModelTextures(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    ExportedMesh mesh,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var names = TextureNames(mesh);
    if (mesh.Material is not null)
    {
      foreach (var slot in mesh.Material.Slots)
      {
        if (!string.IsNullOrWhiteSpace(slot.Texture))
          names.Add(slot.Texture);
      }
    }

    var selected = new HashSet<string>(batch, StringComparer.OrdinalIgnoreCase);
    var seen = new HashSet<int>();
    foreach (var name in names)
    {
      var resolved = TemplateMeshReader.ResolveTexture(catalog, index, name);
      if (resolved < 0 || !seen.Add(resolved))
        continue;
      var linked = catalog.GetFile(resolved);
      if (linked.Kind != AssetKind.Texture || selected.Contains(linked.Path))
        continue;
      try
      {
        ExportTexture(catalog, store, resolved, linked, outputDirectory, settings, report, batch);
      }
      catch (Exception ex)
      {
        report.Messages.Add(name + ": " + ex.Message);
      }
    }
  }

  private static void ExportAnimation(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var clip = SaniReader.Load(catalog, store, index);
    var jsonPath = Destination(outputDirectory, file.Path, ".anim.json", batch);
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
      clip.Name,
      clip.Duration,
      clip.SampleRate,
      bones = clip.Bones,
      note = clip.Notes.Count == 0
        ? "No curves were produced. Particle systems are unrelated and are not converted."
        : string.Join(" ", clip.Notes),
      sequences = clip.Sequences,
      notes = clip.Notes,
      tracks = clip.Tracks.Select(track => new
      {
        track.Bone,
        track.BoneName,
        translation = track.Translation.Select(key => new { key.Time, key.Value.X, key.Value.Y, key.Value.Z }),
        rotation = track.Rotation.Select(key => new { key.Time, key.Value.X, key.Value.Y, key.Value.Z, key.Value.W }),
        scale = track.Scale.Select(key => new { key.Time, key.Value.X, key.Value.Y, key.Value.Z }),
      }),
    }, JsonOptions));
    report.Written++;

    if (settings.WriteGltf && clip.HasCurves)
    {
      var mesh = SkeletonForClip(clip);
      GltfWriter.Write(Destination(outputDirectory, file.Path, ".glb", batch), mesh, settings.Space);
      report.Written++;
      if (settings.WriteFbx && mesh.Bones.Count > 0)
      {
        FbxWriter.Write(Destination(outputDirectory, file.Path, ".fbx", batch), mesh, settings.Space);
        report.Written++;
      }
    }
    else if (clip.Notes.Count > 0)
    {
      report.Messages.Add(file.Path + ": " + clip.Notes[0]);
    }
  }

  private static ExportedMesh SkeletonForClip(AnimationClip clip)
  {
    var mesh = new ExportedMesh { Name = clip.Name, Clip = clip };
    if (clip.Bones.Count > 0)
    {
      foreach (var name in clip.Bones)
        mesh.Bones.Add(new Bone { Name = name, Parent = -1 });
      return mesh;
    }

    var boneIds = clip.Tracks.Select(track => track.Bone).Where(bone => bone >= 0).Distinct().OrderBy(bone => bone).ToList();
    if (boneIds.Count == 0)
      boneIds.Add(0);
    foreach (var bone in boneIds)
    {
      var track = clip.Tracks.Find(item => item.Bone == bone);
      mesh.Bones.Add(new Bone { Name = track is { BoneName.Length: > 0 } ? track.BoneName : "bone" + bone, Parent = -1 });
    }

    foreach (var track in clip.Tracks)
    {
      var mapped = boneIds.IndexOf(track.Bone);
      if (mapped >= 0)
        track.Bone = mapped;
    }

    return mesh;
  }

  private static string AnimationRelative(string modelPath, string token)
  {
    var normalized = modelPath.Replace('\\', '/');
    var slash = normalized.LastIndexOf('/');
    var folder = slash >= 0 ? normalized[..(slash + 1)] : "";
    return folder + AssetClassification.BaseName(normalized) + "__" + token + ".tpl";
  }

  private static string ClipToken(string name)
  {
    var builder = new StringBuilder(name.Length);
    foreach (var c in name)
    {
      if (c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' or '\0' or ' ')
        builder.Append('_');
      else
        builder.Append(c);
    }

    var text = builder.ToString().Trim('_');
    return text.Length == 0 ? "clip" : text;
  }

  private static void ExportEffect(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    WriteOriginal(store, index, file, outputDirectory, report, null);
    var bytes = store.Read(index);
    var references = ScanReferences(bytes);
    var copied = new List<string>();
    foreach (var reference in references)
    {
      var resolved = catalog.ResolveReference(reference, index);
      if (resolved < 0)
        continue;
      var linked = catalog.GetFile(resolved);
      WriteOriginal(store, resolved, linked, outputDirectory, report, null);
      copied.Add(linked.Path);
    }

    var note = Destination(outputDirectory, file.Path, ".effects.txt", batch);
    var builder = new StringBuilder();
    builder.AppendLine("Particle systems and effects are not converted to Unreal or Blender.");
    builder.AppendLine("This folder contains the original effect bytes and any texture or model names found as plain text inside them.");
    builder.AppendLine("Those linked files are copied raw. They are not turned into meshes or materials.");
    builder.AppendLine();
    foreach (var path in copied.Distinct(StringComparer.OrdinalIgnoreCase))
      builder.AppendLine(path);
    if (copied.Count == 0)
      builder.AppendLine("No linked asset names were found in the effect bytes.");
    File.WriteAllText(note, builder.ToString());
    report.Written++;
  }

  private static HashSet<string> TextureNames(ExportedMesh mesh)
  {
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void Take(string? name)
    {
      if (!string.IsNullOrWhiteSpace(name))
        names.Add(name);
    }

    void TakePart(MeshPart part)
    {
      Take(part.Texture);
      Take(part.Normal);
      Take(part.Specular);
      Take(part.Emissive);
      foreach (var map in part.Maps)
      {
        if (!map.Missing)
          Take(map.Name);
      }
    }

    foreach (var part in mesh.Parts)
      TakePart(part);
    foreach (var level in mesh.Levels)
    {
      foreach (var part in level.Parts)
        TakePart(part);
    }

    return names;
  }

  private static void WriteMaterialSidecar(string fbxPath, ExportedMesh mesh, IReadOnlyDictionary<string, string> files, ExportReport report)
  {
    var slots = MaterialMaps.Slots(mesh).Select(slot => new
    {
      slot = slot.Name,
      variant = slot.Part.Variant,
      blend = slot.Part.Blend,
      maps = slot.Part.Maps.Select(map => new
      {
        role = map.Role,
        name = map.Name,
        file = files.TryGetValue(map.Name, out var file) ? file : null,
        channels = map.Channels,
        srgb = map.Srgb,
        generated = map.Generated,
        missing = map.Missing,
        packing = map.Packing.Select(pack => new { channel = pack.Channel, meaning = pack.Meaning }),
      }),
    });
    var jsonPath = Path.Combine(Path.GetDirectoryName(fbxPath)!, Path.GetFileNameWithoutExtension(fbxPath) + ".materials.json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
      mesh = mesh.Name,
      fbx = Path.GetFileName(fbxPath),
      note = "Each map's channels are the .td format_descr. srgb false is a normal or a packed mask, so Unreal imports it linear. The FBX links diffuse, normal, and specular. import_sm2_materials.py builds a master material and one instance per slot from this file.",
      slots,
    }, JsonOptions));
    report.Written++;
  }

  private static void WriteUnrealScript(string path, ExportReport report)
  {
    var assembly = typeof(AssetExporter).Assembly;
    var name = assembly.GetManifestResourceNames().FirstOrDefault(item => item.EndsWith("import_sm2_materials.py", StringComparison.Ordinal));
    if (name is null)
      return;
    using var stream = assembly.GetManifestResourceStream(name);
    if (stream is null)
      return;
    using var reader = new StreamReader(stream);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, reader.ReadToEnd().Replace("\r\n", "\n"));
    report.Written++;
  }

  private static void WriteMaterial(string outputDirectory, string relative, MaterialDocument? material, ExportSettings settings, ExportReport report, IReadOnlyList<string> batch)
  {
    if (!settings.WriteMaterials || material is null || (material.Slots.Count == 0 && material.Notes.Count == 0))
      return;

    var path = Destination(outputDirectory, relative, ".material.json", batch);
    File.WriteAllText(path, JsonSerializer.Serialize(new
    {
      material.Name,
      note = "Texture slots named by the Saber material or the .tpl.resource. The textures themselves are exported from their .pct.resource.",
      slots = material.Slots.Select(slot => new { slot.Slot, slot.Texture, slot.Tint }),
      notes = material.Notes,
    }, JsonOptions));
    report.Written++;
  }

  private static string Destination(string outputDirectory, string relative, string extension, IReadOnlyList<string> batch)
  {
    var plain = OutputPaths.Map(outputDirectory, relative, extension);
    foreach (var other in batch)
    {
      if (string.Equals(other, relative, StringComparison.OrdinalIgnoreCase))
        continue;
      if (OutputPaths.Map(outputDirectory, other, extension) == plain)
        return OutputPaths.Map(outputDirectory, relative, extension, keepFileName: true);
    }

    return plain;
  }

  private static void WriteOriginal(ArchiveStore store, int index, CatalogFileInfo file, string outputDirectory, ExportReport report, string? note)
  {
    var path = OutputPaths.Map(outputDirectory, file.Path, file.Extension.Length == 0 ? ".bin" : file.Extension);
    File.WriteAllBytes(path, store.Read(index));
    report.Written++;
    if (note is not null)
    {
      File.WriteAllText(OutputPaths.Map(outputDirectory, file.Path, ".txt"), note);
      report.Written++;
    }
  }

  public static List<string> ScanReferences(ReadOnlySpan<byte> data)
  {
    var found = new List<string>();
    var start = -1;
    for (var i = 0; i <= data.Length && i < 4 * 1024 * 1024; i++)
    {
      var printable = i < data.Length && data[i] is >= 32 and < 127;
      if (printable)
      {
        if (start < 0)
          start = i;
        continue;
      }

      if (start >= 0 && i - start >= 5)
      {
        var text = Encoding.ASCII.GetString(data.Slice(start, i - start));
        if (IsAssetReference(text))
          found.Add(text);
      }

      start = -1;
    }

    return found;
  }

  private static bool IsAssetReference(string text)
  {
    var lower = text.ToLowerInvariant();
    return lower.EndsWith(".pct") || lower.EndsWith(".pct_mip") || lower.EndsWith(".pct.resource")
      || lower.EndsWith(".tpl") || lower.EndsWith(".tpl_data") || lower.EndsWith(".lods_base")
      || lower.EndsWith(".sani") || lower.EndsWith(".td") || lower.Contains(".tpl") || lower.Contains(".pct");
  }

  private static string Notes(ExportSettings settings)
  {
    var space = settings.Space == ExportSpace.Blender
      ? "Blender: FBX is Z-up meters. glTF is Y-up meters."
      : "Unreal: FBX is Z-up centimeters (source meters times 100). glTF is Y-up meters, which both Unreal and Blender import.";
    return AxisConvert.Assumption + Environment.NewLine
      + space + Environment.NewLine
      + "DDS keeps the original block compression. PNG and TGA are decoded previews." + Environment.NewLine
      + "Most textures are .pct.resource plus .pct_mip. A bare .pct is uncommon." + Environment.NewLine
      + "Models are .tpl plus .tpl_data. Lod 0 keeps the meshes Index leaves checked (the largest pool, plus a shell such as _SIM) and drops _LOD and collision. A higher lod writes that smaller vertex pool. FBX is binary 7.4, Z-up, with a skinned limb hierarchy. Each exported part gets a material whose diffuse (and _nm normal, when that texture is in the catalog) is a path relative to the FBX, pointing at the PNG or TGA written beside it. .lods_base, .geom_dbg, and .cdt are not mesh payloads." + Environment.NewLine
      + "Animations: retail .sani plus .sani_data is an ETNA clip. Rotation keys are three int16 components lerped through a per-bone min/max, with w reconstructed, then composed with the bone's bind rotation. Translation keys are three 10-bit components, unwrapped across frames. Bones the clip does not name stay on the model bind pose. A clip with no .sani_data key grid is not sampled. export --anim writes {model}__{clip}.fbx and .glb beside the bind mesh." + Environment.NewLine
      + "Particle systems (.ps, .sfx) are not converted. Their raw bytes and any named meshes or textures are copied." + Environment.NewLine;
  }

}

public static class OutputPaths
{

  public static string Map(string root, string relative, string extensionWithDot, bool keepFileName = false)
  {
    if (!extensionWithDot.StartsWith('.'))
      extensionWithDot = "." + extensionWithDot;

    relative = relative.Replace('\\', '/').TrimStart('/');
    var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
      throw new InvalidDataException("Empty archive path.");

    var safe = new string[parts.Length];
    for (var i = 0; i < parts.Length; i++)
    {
      var part = parts[i];
      if (part is "." or "..")
        throw new InvalidDataException($"Refusing to export '{relative}'.");
      foreach (var c in part)
      {
        if (c < 32 || c is '<' or '>' or ':' or '"' or '|' or '?' or '*')
          throw new InvalidDataException($"Refusing to export '{relative}'.");
      }

      safe[i] = part;
    }

    var fileName = safe[^1];
    var baseName = keepFileName ? fileName : AssetClassification.BaseName(fileName);
    if (string.IsNullOrWhiteSpace(baseName))
      baseName = "file";

    var combined = new string[safe.Length + 1];
    combined[0] = root;
    for (var i = 0; i < safe.Length - 1; i++)
      combined[i + 1] = safe[i];
    combined[^1] = baseName + extensionWithDot;
    var full = Path.GetFullPath(Path.Combine(combined));
    var rootFull = Path.GetFullPath(root);
    var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException($"Export path left the output folder: {relative}");

    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    return full;
  }

}

public static class BytePreview
{

  public static string Hex(ReadOnlySpan<byte> data, int maxBytes = 4096)
  {
    var count = Math.Min(data.Length, maxBytes);
    var builder = new StringBuilder(count * 4);
    for (var i = 0; i < count; i += 16)
    {
      builder.Append(i.ToString("X8")).Append("  ");
      var line = Math.Min(16, count - i);
      for (var j = 0; j < line; j++)
        builder.Append(data[i + j].ToString("X2")).Append(' ');
      builder.AppendLine();
    }

    if (data.Length > count)
      builder.Append("… ").Append(data.Length - count).Append(" more bytes");
    return builder.ToString();
  }

  public static string Text(ReadOnlySpan<byte> data, int maxBytes = 200_000)
  {
    var count = Math.Min(data.Length, maxBytes);
    var text = Encoding.UTF8.GetString(data[..count]);
    if (data.Length > count)
      text += Environment.NewLine + $"… {data.Length - count} more bytes";
    return text;
  }

}
