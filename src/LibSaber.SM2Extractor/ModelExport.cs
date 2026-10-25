using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Extractor;

public static class ModelExport
{

  public static int Run(ExtractorOptions options, TextWriter stdout, TextWriter stderr)
  {
    var asset = options.Asset ?? "";
    var output = options.OutputPath ?? "";
    GameCatalog catalog;
    try
    {
      catalog = GameCatalog.Load(options.InputPath, recursive: options.Recursive);
    }
    catch (Exception ex)
    {
      stderr.WriteLine(ex.Message);
      return 1;
    }

    if (catalog.FileCount == 0)
    {
      stderr.WriteLine($"No .pak or .pck archives found at '{options.InputPath}'.");
      return 1;
    }

    foreach (var warning in catalog.Warnings)
      stderr.WriteLine(warning);

    if (!TryResolveModel(catalog, asset, out var index, out var error))
    {
      stderr.WriteLine(error);
      return 1;
    }

    var file = catalog.GetFile(index);
    if (!options.Quiet)
      stdout.WriteLine($"Exporting {file.Path} as {options.Format} lod {options.Lod}");

    using var store = new ArchiveStore(catalog);
    var report = AssetExporter.Export(
      catalog,
      store,
      new[] { index },
      output,
      Settings(options),
      options.Quiet ? null : new Progress<string>(path => stdout.WriteLine(path)));

    foreach (var message in report.Messages)
      stdout.WriteLine(message);

    stdout.WriteLine(
      $"Summary: archivesOpened={catalog.Archives.Count} archivesSkipped={catalog.ArchivesSkipped} filesWritten={report.Written} filesSkipped={report.Skipped}");

    if (report.Skipped > 0 && report.Written <= 1)
      return 1;
    return report.Skipped > 0 ? 2 : 0;
  }

  internal static bool TryResolveModel(GameCatalog catalog, string asset, out int index, out string error)
  {
    index = -1;
    error = "";
    var normalized = asset.Replace('\\', '/').Trim().Trim('/');
    if (normalized.Length == 0)
    {
      error = "Asset path is empty.";
      return false;
    }

    if (catalog.TryGetByPath(normalized, out var exact) && catalog.GetFile(exact).Kind == AssetKind.Model)
    {
      index = exact;
      return true;
    }

    var fileName = Path.GetFileName(normalized.Replace('/', Path.DirectorySeparatorChar));
    var nested = normalized + "/" + fileName;
    if (catalog.TryGetByPath(nested, out var nestedIndex) && catalog.GetFile(nestedIndex).Kind == AssetKind.Model)
    {
      index = nestedIndex;
      return true;
    }

    var hits = catalog.FindByFileName(fileName)
      .Where(hit => catalog.GetFile(hit).Kind == AssetKind.Model)
      .ToList();
    if (normalized.Contains('/'))
    {
      var under = hits.Where(hit =>
      {
        var path = catalog.GetFile(hit).Path;
        return path.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase)
          || string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase);
      }).ToList();
      if (under.Count > 0)
        hits = under;
    }

    hits = hits
      .OrderBy(hit => catalog.GetFile(hit).Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
      .ThenBy(hit => catalog.GetFile(hit).Path, StringComparer.OrdinalIgnoreCase)
      .ToList();

    if (hits.Count == 1)
    {
      index = hits[0];
      return true;
    }

    if (hits.Count == 0)
    {
      error = $"No model named '{asset}' in this folder.";
      return false;
    }

    // A folder-style name can match both the .tpl and the .tpl.resource. Prefer the .tpl.
    var preferred = hits.Where(hit => !catalog.GetFile(hit).Path.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)).ToList();
    if (preferred.Count == 1)
    {
      index = preferred[0];
      return true;
    }

    var listed = (preferred.Count > 0 ? preferred : hits).Select(hit => catalog.GetFile(hit).Path);
    error = "More than one model matches '" + asset + "': " + string.Join(", ", listed);
    return false;
  }

  private static ExportSettings Settings(ExtractorOptions options)
  {
    var format = options.Format ?? "";
    var fbx = format is "fbx-unreal" or "both";
    var gltf = format is "gltf" or "both";
    return new ExportSettings
    {
      WriteDds = true,
      WritePng = true,
      WriteTga = true,
      WriteGltf = gltf,
      WriteFbx = fbx,
      WriteMaterials = true,
      WriteModelTextures = true,
      CopyOriginalBytes = false,
      Space = ExportSpace.Unreal,
      Lod = options.Lod,
    };
  }

}
