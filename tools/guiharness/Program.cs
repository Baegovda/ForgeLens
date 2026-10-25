using LibSaber.SM2.Assets.Meshes;
// Drives the browser's Export path: list selection -> ExportPlan.Apply (SelectedClips) -> AssetExporter.Export,
// with the ExportWindow defaults for an Unreal FBX export.
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Export;
var catalog = GameCatalog.Load(args[0], recursive: true);
using var store = new ArchiveStore(catalog);
var model = catalog.FindByFileName(args[1]).Where(i => catalog.GetFile(i).Kind == AssetKind.Model)
  .OrderBy(i => catalog.GetFile(i).Path.EndsWith(".resource") ? 1 : 0).First();
var headers = SaniIndex.ReadHeaders(catalog, store);
var selection = new List<int> { model };
foreach (var token in args.Skip(3)) { var h = SaniIndex.Match(headers, token); Console.WriteLine($"{token} -> {string.Join(", ", h.Select(x => x.Path))}"); selection.Add(h[0].Index); }
var settings = new ExportSettings { WriteDds = true, WritePng = true, WriteTga = true, WriteGltf = false, WriteFbx = true, WriteMaterials = true, WriteModelTextures = true, Space = ExportSpace.Unreal };
var notes = new List<string>();
var files = ExportPlan.Apply(catalog, store, selection, settings, ClipChoice.SelectedClips, -1, notes);
var report = AssetExporter.Export(catalog, store, files, args[2], settings);
foreach (var m in notes.Concat(report.Messages)) Console.WriteLine(m);
Console.WriteLine($"written={report.Written} skipped={report.Skipped} clips={settings.Clips.Count}");
