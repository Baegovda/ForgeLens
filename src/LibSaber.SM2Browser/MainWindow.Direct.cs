using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Context;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Export;

namespace LibSaber.SM2Browser;

/// <summary>oneclick (원클릭 내보내기 옵션): "Direct to Unreal" after an export.</summary>
public partial class MainWindow
{
  /// <summary>Name the suggested /Game folder after: the first selected model (else the first file).</summary>
  private string DirectAssetName(IReadOnlyList<int> indices)
  {
    if (_catalog is not { } catalog || indices.Count == 0)
      return "asset";
    var pick = indices.FirstOrDefault(i => catalog.GetFile(i).Kind == AssetKind.Model, indices[0]);
    var path = catalog.GetFile(pick).Path.Replace('\\', '/');
    var name = path[(path.LastIndexOf('/') + 1)..];
    var dot = name.IndexOf('.');
    return dot > 0 ? name[..dot] : name;
  }

  /// <summary>
  ///   QA (oneclick, screenshots/tests without clicks): SM2_SEQ=name part plays that embedded prop sequence (▶ row);
  ///   SM2_EXPORT_QA=export folder then opens the Export window for the previewed file (ExportWindow reads
  ///   SM2_DIRECT_PROJECT / SM2_DIRECT_PATH / SM2_EXPORT_CLIPS / SM2_EXPORT_GO).
  /// </summary>
  private async Task ExportFromEnvironmentAsync()
  {
    if (Environment.GetEnvironmentVariable("SM2_SEQ") is { Length: > 0 } seqName && _mesh is not null)
    {
      AnimChoice? row = null;
      for (var i = 0; i < 300 && (row = _animAll.FirstOrDefault(c => c.Seq is { } s && s.Name.Contains(seqName, StringComparison.OrdinalIgnoreCase))) is null; i++)
        await Task.Delay(100);
      if (row?.Seq is { } seq)
      {
        _propCond = seq.Name;
        SyncConditionBoxes();
        await PlayPropStandaloneAsync(new PropCondition(seq.Name, PropAnimStates.Label(seq.Name), seq), autoPlay: false);
      }
      else
        StatusText.Text = "SM2_SEQ: no sequence matches " + seqName;
    }

    if (Environment.GetEnvironmentVariable("SM2_EXPORT_QA") is not { Length: > 0 } folder || _previewFileIndex < 0)
      return;
    if (Directory.Exists(folder))
      _settings.LastExportFolder = folder;
    await Task.Delay(500);
    await ExportIndicesAsync(new[] { _previewFileIndex });
  }

  private async Task RunDirectImportAsync(ExportReport report, ExportChoice choice, UnrealDirectChoice direct)
  {
    var models = report.Models.Where(m => m.Fbx is not null || m.Clips.Count > 0 || m.Manny.Count > 0).ToList();
    if (models.Count == 0)
    {
      StatusText.Text += " Direct to Unreal: nothing to import (no FBX was written).";
      return;
    }

    UnrealDirectManifest.Paths paths;
    try
    {
      paths = UnrealDirectManifest.Write(choice.Folder, models, direct, choice.Settings.AnimationOnly);
    }
    catch (Exception ex)
    {
      StatusText.Text = "Direct to Unreal: " + ex.Message;
      return;
    }

    var window = new UnrealImportWindow();
    var targets = string.Join(", ", models.Select(m => UnrealContentPath.ForModel(direct.ContentPath, direct.ContentPathIsDefault, m.Name, models.Count, choice.Settings.AnimationOnly)));
    window.Start($"{Path.GetFileNameWithoutExtension(direct.Project)} → {targets}", paths.Folder);
    window.Show(this);
    StatusText.Text = "Importing into Unreal… " + targets;
    var progress = new Progress<string>(window.Append);
    UnrealImportResult result;
    try
    {
      result = await Task.Run(() => UnrealDirectImporter.Run(paths, direct, progress, CancellationToken.None));
    }
    catch (Exception ex)
    {
      result = new UnrealImportResult { Summary = "Import FAILED: " + ex.Message, LogPath = paths.Log };
    }

    window.Finish(result);
    StatusText.Text = "Direct to Unreal: " + result.Summary;
  }
}
