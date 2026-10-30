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
  PreviewSettings Settings, Func<float>? Clock = null,
  Func<ExportedMesh, PreviewScene, float, EffectLayer?>? Overlay = null);

/// <summary>One standalone-VFX frame: built on the GL thread just before drawing (so playback follows vsync).</summary>
internal sealed record EffectFrame(EffectLayer Layer, System.Numerics.Matrix4x4 View, System.Numerics.Matrix4x4 Projection, System.Numerics.Vector3 Eye);

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

  /// <summary>Standalone VFX view: when set, frames come from this (width, height) callback instead of a mesh request.</summary>
  public Func<int, int, EffectFrame?>? Effect { get; private set; }

  public void ShowEffect(Func<int, int, EffectFrame?>? effect)
  {
    Effect = effect;
    if (effect is not null) _request = null;
    RequestNextFrameRendering();
  }

  /// <summary>R75: the scene of the last drawn frame (exact camera / visibility / pose) for picking.</summary>
  public PreviewScene? LastScene { get; private set; }

  /// <summary>R75: outlined parts (hover / selection); applied on the next frame.</summary>
  public IReadOnlyList<(MeshPart Part, System.Numerics.Vector3 Colour, float Px)> Highlights { get; set; } = Array.Empty<(MeshPart, System.Numerics.Vector3, float)>();

  /// <summary>R79: view mode (GlPreview.ViewMode) and Show-menu lines, applied on the next frame.</summary>
  public int ViewMode { get; set; }
  /// <summary>R83: Show menu cloth vertices (GlPreview.ShowClothVerts); LastClothVerts = count drawn in the last frame.</summary>
  public bool ShowClothVerts { get; set; }
  public int LastClothVerts { get; private set; }
  public IReadOnlyList<(System.Numerics.Vector3 A, System.Numerics.Vector3 B, System.Numerics.Vector3 Colour)> ExtraLines { get; set; } = Array.Empty<(System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector3)>();

  public void Redraw() => RequestNextFrameRendering();

  private (string Path, TaskCompletionSource Done)? _capture;

  /// <summary>R75 test hook: save the next rendered viewport frame (GPU pixels, incl. outlines) as PNG.</summary>
  public Task CaptureNextAsync(string path)
  {
    var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    _capture = (path, tcs);
    RequestNextFrameRendering();
    return tcs.Task;
  }

  public void Show(GpuFrameRequest request)
  {
    Effect = null;
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

  private void Tick()
  {
    _fpsFrames++;
    var now = _clock.Elapsed.TotalSeconds;
    if (now - _fpsStart >= 0.5)
    {
      _fps = _fpsFrames / (now - _fpsStart);
      _fpsFrames = 0;
      _fpsStart = now;
    }
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
      if (Effect is { } effectSource)
      {
        var effectStart = _clock.Elapsed.TotalMilliseconds;
        if (effectSource(width, height) is { } frame)
        {
          renderer.RenderEffect(frame.Layer, frame.View, frame.Projection, frame.Eye, width, height, fb);
          FramesRendered++;
          Tick();
          Rendered?.Invoke(new GpuFrameStats(width, height, renderer.Samples, _clock.Elapsed.TotalMilliseconds - effectStart, renderer.LastEffectMs, _fps, renderer.Info));
        }

        if (Continuous || renderer.Pending)
          RequestNextFrameRendering();
        return;
      }

      if (request is null)
      {
        gl.BindFramebuffer(0x8D40, fb);
        gl.ClearColor(28 / 255f, 24 / 255f, 22 / 255f, 1f);
        gl.Clear(0x4000);
        return;
      }

      var start = _clock.Elapsed.TotalMilliseconds;
      var time = request.Clock?.Invoke() ?? request.Time;
      var scene = MeshPreview.BuildScene(request.Mesh, width, height, request.Camera, request.Skeleton, time, request.Settings);
      var buildMs = _clock.Elapsed.TotalMilliseconds - start;
      // Effects on the model (sockets / anim events): drawn by GlPreview after the translucent pass, depth-tested.
      renderer.Effect = request.Overlay?.Invoke(request.Mesh, scene, time);
      LastScene = scene;
      renderer.Highlights = Highlights;
      renderer.ViewMode = ViewMode;
      renderer.ExtraLines = ExtraLines;
      renderer.ShowClothVerts = ShowClothVerts;
      renderer.Render(request.Mesh, scene, width, height, request.Wireframe, fb);
      LastClothVerts = renderer.LastClothVerts;
      var cpu = _clock.Elapsed.TotalMilliseconds - start;
      OpenProf.Frame(buildMs, renderer.LastUploadMs, cpu - buildMs, renderer.Pending);
      if (_capture is { } cap)
      {
        _capture = null;
        try { Bench.PreviewBench.Png(cap.Path, renderer.ReadPixels(), renderer.Width, renderer.Height); } catch { /* best effort */ }
        cap.Done.TrySetResult();
      }
      FramesRendered++;
      Tick();

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
