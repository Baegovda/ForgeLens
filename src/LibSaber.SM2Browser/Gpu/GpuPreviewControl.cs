using System.Diagnostics;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Meshes;
using Silk.NET.OpenGL;

namespace LibSaber.SM2Browser.Gpu;

/// <summary>What the next GPU frame should show.</summary>
internal sealed record GpuFrameRequest(
  ExportedMesh Mesh,
  OrbitCamera Camera,
  bool Skeleton,
  bool Wireframe,
  float Time,
  PreviewSettings Settings, Func<float>? Clock = null);

internal readonly record struct GpuFrameStats(int Width, int Height, int Samples, double CpuMs, double UploadMs, double Fps, string Info);

/// <summary>
///   Avalonia host for <see cref="GlPreview"/>. Renders on demand (one frame per <see cref="Show"/>) while idle
///   and every compositor frame (vsync) while <see cref="Continuous"/> is set. If GL cannot start, or the
///   shaders fail, <see cref="Failed"/> fires once and the window falls back to the CPU renderer.
/// </summary>
internal sealed class GpuPreviewControl : OpenGlControlBase
{
  private GlPreview? _renderer;
  private GpuFrameRequest? _request;
  private readonly Stopwatch _clock = Stopwatch.StartNew();
  private double _fpsStart;
  private int _fpsFrames;
  private double _fps;

  public bool Continuous { get; set; }
  public bool IsReady => _renderer is not null;
  public bool HasFailed { get; private set; }
  public string FailReason { get; private set; } = "";
  public bool InitAttempted { get; private set; }
  public int FramesRendered { get; private set; }
  public string Info => _renderer?.Info ?? "";

  public event Action<string>? Failed;
  public event Action<GpuFrameStats>? Rendered;

  /// <summary>Bench mode: runs instead of the normal frame, with the live renderer and the host framebuffer.</summary>
  public Func<GlPreview, int, bool>? BenchHook { get; set; }

  public void Show(GpuFrameRequest request)
  {
    _request = request;
    RequestNextFrameRendering();
  }

  protected override void OnOpenGlInit(GlInterface gl)
  {
    InitAttempted = true;
    try
    {
      var api = GL.GetApi(name => gl.GetProcAddress(name));
      _renderer = new GlPreview(api, GlVersion.Type == GlProfileType.OpenGLES);
    }
    catch (Exception exception)
    {
      Fail($"GPU preview unavailable ({GlVersion}): {exception.Message}");
    }
  }

  protected override void OnOpenGlDeinit(GlInterface gl)
  {
    try
    {
      _renderer?.Dispose();
    }
    catch (Exception)
    {
      // Context is going away anyway.
    }

    _renderer = null;
  }

  protected override void OnOpenGlLost()
  {
    _renderer = null;
    base.OnOpenGlLost();
  }

  private void Fail(string reason)
  {
    if (HasFailed)
      return;
    HasFailed = true;
    FailReason = reason;
    Dispatcher.UIThread.Post(() => Failed?.Invoke(reason));
  }

  protected override void OnOpenGlRender(GlInterface gl, int fb)
  {
    var renderer = _renderer;
    if (renderer is null)
      return;
    try
    {
      if (BenchHook is { } hook)
      {
        if (hook(renderer, fb))
          RequestNextFrameRendering();
        return;
      }

      var request = _request;
      var scale = VisualRoot?.RenderScaling ?? 1.0;
      var width = Math.Max(1, (int)(Bounds.Width * scale));
      var height = Math.Max(1, (int)(Bounds.Height * scale));
      if (request is null)
      {
        gl.BindFramebuffer(0x8D40, fb);
        gl.ClearColor(28 / 255f, 24 / 255f, 22 / 255f, 1f);
        gl.Clear(0x4000);
        return;
      }

      var start = _clock.Elapsed.TotalMilliseconds;
      var scene = MeshPreview.BuildScene(request.Mesh, width, height, request.Camera, request.Skeleton, request.Clock?.Invoke() ?? request.Time, request.Settings);
      renderer.Render(request.Mesh, scene, width, height, request.Wireframe, fb);
      var cpu = _clock.Elapsed.TotalMilliseconds - start;
      FramesRendered++;
      _fpsFrames++;
      var now = _clock.Elapsed.TotalSeconds;
      if (now - _fpsStart >= 0.5)
      {
        _fps = _fpsFrames / (now - _fpsStart);
        _fpsFrames = 0;
        _fpsStart = now;
      }

      var stats = new GpuFrameStats(width, height, renderer.Samples, cpu, renderer.LastUploadMs, _fps, renderer.Info);
      Rendered?.Invoke(stats);
      if (Continuous || renderer.Pending)
        RequestNextFrameRendering();
    }
    catch (Exception exception)
    {
      Fail($"GPU preview failed: {exception.Message}");
    }
  }
}
