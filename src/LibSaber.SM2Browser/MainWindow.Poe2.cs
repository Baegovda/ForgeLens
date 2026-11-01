// M2: show a PoE2 mesh / effect (Poe2Reader -> ExportedMesh) in the same preview the SM2 models use.
using Avalonia.Interactivity;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private Poe2Window? _poe2;

  internal void OpenPoe2()
  {
    if (_poe2 is { IsVisible: true }) { _poe2.Activate(); return; }
    _poe2 = new Poe2Window(this);
    _poe2.Show(this);
  }

  /// <summary>
  ///   M2 D: play a PoE2 effect .ao through the SM2 particle view (simulation, sprites, trails). FL_POE2_RIGVIEW=1 keeps the
  ///   M1 static rig/emitter-mesh view instead.
  /// </summary>
  internal async Task<bool> ShowPoe2EffectAsync(LibSaber.SM2.Assets.Games.Poe2.Poe2Reader reader, string ao, string? animation)
  {
    if (Environment.GetEnvironmentVariable("FL_POE2_RIGVIEW") == "1" || GpuView.HasFailed) return false;
    _vfxTextures = Effects.Poe2FxTextures.For(reader);
    _vfxTextures.Loaded = () => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_fx is not null) GpuView.ShowEffect(GpuView.Effect); });
    _vfxMeshes = Effects.EffectMeshes.None;
    ++_previewGeneration;
    _playTimer.Stop();
    _mesh = null;
    _clip = null;
    _level = null;
    _texture = null;
    var why = "";
    var gen = 0;
    Gpu.EffectRenderer.GroundWritesDepth = false; // M2 H (reset to true by the SM2 effect path)
    bool ok;
    try
    {
      ok = await ShowEffectAsync(Path.GetFileNameWithoutExtension(ao), gen = ++_previewGeneration, () =>
      {
        var r = LibSaber.SM2.Assets.Games.Poe2.Poe2Particles.Build(reader, ao, animation);
        _vfxAliases = LibSaber.SM2.Assets.Effects.PartAliases.From(r.Frames);
        why = $"emitters {r.Definition.Emitters.Count} trails {r.Definition.Trails.Count}";
        return (r.Definition, LibSaber.SM2.Assets.Games.Poe2.Poe2Particles.Instance(r));
      });
    }
    catch (Exception x) { ok = false; why = x.ToString(); }
    try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "fl_poe2fx.txt"), $"{DateTime.Now:HH:mm:ss} {ao} ok={ok} gpuFailed={GpuView.HasFailed} gpuToggle={GpuToggle.IsChecked} {why}\n"); } catch (IOException) { }
    if (!ok && gen != _previewGeneration) return true; // superseded by a newer selection: no fallback view
    if (ok && Environment.GetEnvironmentVariable("FL_POE2_FULLFIT") != "1") VfxFitSpan(LibSaber.SM2.Assets.Games.Poe2.Poe2Particles.Env("FL_POE2_FIT_SEC", 0.6f)); // M2 H
    if (ok) PreviewNote.Text = "PoE2 particles: " + ao + " (HEURISTIC sizes/colours, see docs/POE2_M2.md). " + PreviewNote.Text;
    return ok;
  }

  internal void ShowPoe2(ExportedMesh mesh, Dictionary<string, PreviewImage> albedo, string path)
  {
    ++_previewGeneration;
    _playTimer.Stop();
    StopEffect();
    _level = null;
    _texture = null;
    _previewFileIndex = -1;
    _mesh = mesh;
    _albedo = albedo;
    _normals = null;
    _specular = null;
    _distance = 0; _time = 0; _yaw = 0.7f; _pitch = 0.18f;
    FlyReset(false);
    ClearPick();
    FillModelControls(mesh);
    SkeletonToggle.IsVisible = mesh.Bones.Count > 0;
    WireToggle.IsVisible = mesh.HasTriangles;
    GpuToggle.IsVisible = true;
    PreviewNote.Text = "PoE2: " + path;
    var clip = mesh.Clip;
    _clip = clip;
    _clipIndex = -1;
    AnimBar.IsVisible = clip is { HasCurves: true };
    PlayButton.IsVisible = clip is { HasCurves: true };
    if (clip != null)
      ApplyClipToMesh(clip);
    PreviewText.IsVisible = false;
    PreviewImage.IsVisible = true;
    DrawMesh();
    if (clip is { HasCurves: true } && Environment.GetEnvironmentVariable("FL_POE2_AUTOPLAY") == "1")
      PlayClicked(PlayButton, new RoutedEventArgs());
  }
}
