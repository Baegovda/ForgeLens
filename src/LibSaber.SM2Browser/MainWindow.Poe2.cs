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
