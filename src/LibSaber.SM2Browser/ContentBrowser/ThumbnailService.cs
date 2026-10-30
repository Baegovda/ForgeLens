using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2Browser.Gpu;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SM2Browser.Effects;

namespace LibSaber.SM2Browser.ContentBrowser;

/// <summary>A decoded thumbnail: one frame (still) or a horizontal strip of frames (animations, effects).</summary>
public sealed class Thumb
{
  public Thumb(Bitmap strip, int frames, int size)
  {
    Strip = strip;
    Frames = Math.Max(1, frames);
    Size = size;
  }

  private IImage[]? _images;

  /// <summary>One image per frame. CroppedBitmap is an AvaloniaObject, so these are made lazily on the UI thread.</summary>
  public IImage[] Images
  {
    get
    {
      if (_images is null)
      {
        var images = new IImage[Frames];
        for (var i = 0; i < Frames; i++)
          images[i] = Frames == 1 ? Strip : new CroppedBitmap(Strip, new PixelRect(i * Size, 0, Size, Size));
        _images = images;
      }

      return _images;
    }
  }

  public Bitmap Strip { get; }
  public int Frames { get; }
  public int Size { get; }
  /// <summary>The still shown when not hovering (a frame ~40 % in, where clips and effects are usually busy).</summary>
  public IImage Still => Images[Frames == 1 ? 0 : (int)(Frames * 0.4)];
  internal long Bytes => (long)Strip.PixelSize.Width * Strip.PixelSize.Height * 4;
}

/// <summary>
///   ui-43: Content-Browser thumbnails. Rendered on two low-priority worker threads (newest request first, so the tiles
///   on screen win), kept in a byte-capped LRU in memory and as PNGs on disk under
///   <c>%LOCALAPPDATA%/LibSaber/SM2Browser/thumbs</c> (key = path + archive + size + kind + renderer version).
///   Textures: a small mip. Models: CPU render with the model's albedo / normal / spec maps (R75, t159u; was untextured
///   clay since ui-43, which is why the grid showed blue-grey silhouettes) (same resolver as the viewer). Animations: the clip played on a
///   model whose class uses a Morpheme bundle that links the clip (<see cref="CategoryTree.OwnerModel"/>), 10 frames.
///   Effects: a CPU sprite splat of the simulated particles (approximation of the GPU view; no lights, meshes, distortion), 10 frames.
/// </summary>
public sealed class ThumbnailService : IDisposable
{
  public const int StillSize = 192;
  public const int StripSize = 160;
  public const int StripFrames = 10;
  private const string Version = "r75-tex1"; // R75 (t159u): models now textured (old clay PNGs are simply not matched any more)
  private const long MemoryCap = 320L * 1024 * 1024;

  private readonly GameCatalog _catalog;
  private readonly ArchiveStore _store;
  private readonly Func<CategoryTree?> _tree;
  private readonly string _cacheRoot;
  private readonly object _gate = new();
  private readonly LinkedList<int> _pending = new();
  private readonly Dictionary<int, LinkedListNode<int>> _pendingNodes = new();
  private readonly HashSet<int> _running = new();
  private readonly Dictionary<int, Thumb?> _memory = new();
  private readonly LinkedList<int> _lru = new();
  private readonly Dictionary<int, LinkedListNode<int>> _lruNodes = new();
  private long _bytes;
  private readonly Thread[] _workers;
  private volatile bool _disposed;
  private readonly SemaphoreSlim _signal = new(0);

  // Owner meshes for animation thumbnails (a clip is applied to the mesh while rendering: one at a time per mesh).
  private readonly Dictionary<int, ExportedMesh?> _owners = new();
  // Effects
  private EffectLibrary? _fxLibrary;
  private PartAliases _fxAliases = PartAliases.Empty;
  private EffectTextures? _fxTextures;

  public ThumbnailService(GameCatalog catalog, ArchiveStore store, Func<CategoryTree?> tree, string? cacheRoot = null)
  {
    _catalog = catalog;
    _store = store;
    _tree = tree;
    _cacheRoot = cacheRoot ?? DefaultCacheRoot();
    _workers = new Thread[2];
    for (var i = 0; i < _workers.Length; i++)
    {
      _workers[i] = new Thread(Work) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "thumbs" + i };
      _workers[i].Start();
    }
  }

  public static string DefaultCacheRoot()
    => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibSaber", "SM2Browser", "thumbs");

  /// <summary>Raised on the UI thread when a requested thumbnail is ready (null = none for this file).</summary>
  public event Action<int, Thumb?>? Ready;

  public static bool HasThumbnail(string extension)
    => extension is ".pct" or ".pct.resource" or ".tpl" or ".sani" or ".sfx";

  public static bool IsAnimated(string extension) => extension is ".sani" or ".sfx";

  /// <summary>Memory hit (true, maybe null = known to have none), else queues it (newest first) and returns false.</summary>
  public bool TryGet(int index, out Thumb? thumb)
  {
    lock (_gate)
    {
      if (_memory.TryGetValue(index, out thumb))
      {
        Touch(index);
        return true;
      }

      if (_running.Contains(index))
        return false;
      if (_pendingNodes.Remove(index, out var node))
        _pending.Remove(node);
      _pendingNodes[index] = _pending.AddFirst(index);
    }

    _signal.Release();
    return false;
  }

  /// <summary>The tile scrolled away before its thumbnail started: drop the request.</summary>
  public void Cancel(int index)
  {
    lock (_gate)
      if (_pendingNodes.Remove(index, out var node))
        _pending.Remove(node);
  }

  public int PendingCount
  {
    get { lock (_gate) return _pending.Count + _running.Count; }
  }

  private void Touch(int index)
  {
    if (_lruNodes.Remove(index, out var node))
      _lru.Remove(node);
    _lruNodes[index] = _lru.AddFirst(index);
  }

  private void Store(int index, Thumb? thumb)
  {
    lock (_gate)
    {
      if (_memory.TryGetValue(index, out var old) && old is not null)
        _bytes -= old.Bytes;
      _memory[index] = thumb;
      _bytes += thumb?.Bytes ?? 0;
      Touch(index);
      while (_bytes > MemoryCap && _lru.Last is { } last)
      {
        _lru.RemoveLast();
        _lruNodes.Remove(last.Value);
        if (_memory.Remove(last.Value, out var gone) && gone is not null)
          _bytes -= gone.Bytes;
      }
    }
  }

  private void Work()
  {
    while (!_disposed)
    {
      _signal.Wait(500);
      while (!_disposed)
      {
        int index;
        lock (_gate)
        {
          if (_pending.First is not { } first)
            break;
          index = first.Value;
          _pending.RemoveFirst();
          _pendingNodes.Remove(index);
          _running.Add(index);
        }

        Thumb? thumb = null;
        var notReady = false;
        try { thumb = Produce(index); }
        catch (NotReadyException) { notReady = true; }
        catch (Exception ex)
        {
          thumb = null;
          Dbg($"thumb {index} failed: {ex}");
        }
        lock (_gate)
          _running.Remove(index);
        if (notReady)
          continue; // the category tree (clip owners) is still building: not cached, re-requested once it is ready
        Store(index, thumb);
        if (!_disposed)
          Dispatcher.UIThread.Post(() => Ready?.Invoke(index, thumb), DispatcherPriority.Background);
      }
    }
  }

  // ------------------------------------------------------------------------------------------ disk cache

  private string KeyPath(int index, out string none)
  {
    var f = _catalog.GetFile(index);
    var key = $"{Version}|{f.Path}|{f.ArchiveName}|{f.Size}|{f.Extension}".ToLowerInvariant();
    var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
    var dir = Path.Combine(_cacheRoot, hash[..2]);
    none = Path.Combine(dir, hash + ".none");
    return Path.Combine(dir, hash + ".png");
  }

  private Thumb? Produce(int index)
  {
    var file = _catalog.GetFile(index);
    if (!HasThumbnail(file.Extension))
      return null;
    var png = KeyPath(index, out var none);
    if (File.Exists(none))
      return null;
    if (File.Exists(png))
    {
      try
      {
        var bitmap = new Bitmap(png);
        var h = bitmap.PixelSize.Height;
        return new Thumb(bitmap, Math.Max(1, bitmap.PixelSize.Width / Math.Max(1, h)), h);
      }
      catch { /* re-render */ }
    }

    var (frames, size) = file.Extension switch
    {
      ".pct" or ".pct.resource" => (Texture(index), StillSize),
      ".tpl" => (Model(index), StillSize),
      ".sani" => (Animation(index), StripSize),
      ".sfx" => (Effect(file.Path), StripSize),
      _ => (null, 0),
    };
    try { Directory.CreateDirectory(Path.GetDirectoryName(png)!); } catch { }
    if (frames is not { Count: > 0 })
    {
      try { File.WriteAllText(none, ""); } catch { }
      return null;
    }

    var strip = new WriteableBitmap(new PixelSize(size * frames.Count, size), new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    using (var fb = strip.Lock())
      for (var f = 0; f < frames.Count; f++)
        for (var y = 0; y < size; y++)
          System.Runtime.InteropServices.Marshal.Copy(frames[f], y * size * 4, fb.Address + y * fb.RowBytes + f * size * 4, size * 4);
    try
    {
      var tmp = png + ".tmp";
      strip.Save(tmp);
      File.Move(tmp, png, true);
    }
    catch { /* cache is best effort */ }
    return new Thumb(strip, frames.Count, size);
  }

  // ------------------------------------------------------------------------------------------ renderers

  private sealed class NotReadyException : Exception;

  private static readonly (byte B, byte G, byte R) Back = (0x1E, 0x1C, 0x1A);

  private List<byte[]>? Texture(int index)
  {
    var doc = TextureLoader.Load(_catalog, _store, index);
    if (!doc.CanDecode || doc.MipCount == 0)
      return null;
    var mip = 0;
    while (mip < doc.MipCount - 1 && Math.Max(doc.Width >> mip, doc.Height >> mip) > StillSize * 2)
      mip++;
    var pixels = doc.Decode(mip, 0, out var w, out var h, out _);
    return new List<byte[]> { Fit(pixels, w, h, StillSize) };
  }

  /// <summary>Box-filters a BGRA image into a size×size tile (aspect kept, dark letterbox, alpha over a checker).</summary>
  internal static byte[] Fit(byte[] bgra, int w, int h, int size)
  {
    var dst = new byte[size * size * 4];
    var scale = Math.Max(w, h) / (double)size;
    var dw = Math.Max(1, (int)Math.Round(w / scale));
    var dh = Math.Max(1, (int)Math.Round(h / scale));
    var ox = (size - dw) / 2;
    var oy = (size - dh) / 2;
    for (var y = 0; y < size; y++)
      for (var x = 0; x < size; x++)
      {
        var o = (y * size + x) * 4;
        var checker = ((x >> 3) + (y >> 3) & 1) == 0 ? 0x2A : 0x22;
        double b = Back.B, g = Back.G, r = Back.R;
        if (x >= ox && x < ox + dw && y >= oy && y < oy + dh)
        {
          var x0 = (int)((x - ox) * scale);
          var y0 = (int)((y - oy) * scale);
          var x1 = Math.Min(w, Math.Max(x0 + 1, (int)((x - ox + 1) * scale)));
          var y1 = Math.Min(h, Math.Max(y0 + 1, (int)((y - oy + 1) * scale)));
          double sb = 0, sg = 0, sr = 0, sa = 0;
          var n = 0;
          for (var sy = y0; sy < y1; sy++)
            for (var sx = x0; sx < x1; sx++)
            {
              var s = (sy * w + sx) * 4;
              if (s + 3 >= bgra.Length) continue;
              sb += bgra[s]; sg += bgra[s + 1]; sr += bgra[s + 2]; sa += bgra[s + 3];
              n++;
            }
          if (n > 0)
          {
            var a = sa / n / 255.0;
            b = sb / n * a + checker * (1 - a);
            g = sg / n * a + checker * (1 - a);
            r = sr / n * a + checker * (1 - a);
          }
        }
        dst[o] = (byte)b; dst[o + 1] = (byte)g; dst[o + 2] = (byte)r; dst[o + 3] = 255;
      }
    return dst;
  }

  private static readonly PreviewSettings ThumbSettings = new() { Supersample = 2, MaxEdge = 512, Threads = 2 };

  private List<byte[]>? Model(int index)
    => RenderModel(_catalog, _store, index, textured: true) is { } px ? new List<byte[]> { px } : null;

  /// <summary>R75: one model still (BGRA StillSize²). textured=false = the old ui-43 clay render (bench before/after).</summary>
  internal static byte[]? RenderModel(GameCatalog catalog, ArchiveStore store, int index, bool textured)
  {
    var mesh = TemplateMeshReader.Load(catalog, store, index);
    if (!mesh.HasTriangles)
      return null;
    var settings = ThumbSettings;
    if (textured)
    {
      // R75: textured like the viewer (same resolver).
      var maps = MainWindow.ResolvePreview(catalog, store, mesh, index);
      settings = ThumbSettings with { Albedo = maps.Albedo, Normals = maps.Normals, Specular = maps.Specular };
    }
    return MeshPreview.Render(mesh, StillSize, StillSize, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, false, 0, out _, settings);
  }

  private List<byte[]>? Animation(int index)
  {
    var tree = _tree() ?? throw new NotReadyException();
    var owner = tree.OwnerModel(index);
    if (owner < 0)
      return null;
    ExportedMesh? mesh;
    lock (_owners)
    {
      if (!_owners.TryGetValue(owner, out mesh))
      {
        try { mesh = TemplateMeshReader.Load(_catalog, _store, owner); }
        catch { mesh = null; }
        if (mesh is not { HasTriangles: true, Bones.Count: > 0 })
          mesh = null;
        if (_owners.Count > 24)
          _owners.Clear();
        _owners[owner] = mesh;
      }
    }

    if (mesh is null)
      return null;
    var clip = SaniReader.Load(_catalog, _store, index);
    if (!clip.HasCurves)
      return null;
    var frames = new List<byte[]>();
    lock (mesh)
    {
      var distance = MeshPreview.FitDistance(mesh, 1f) * 0.8f;
      mesh.Clip = clip;
      try
      {
        var settings = ThumbSettings with { Supersample = 1, InPlace = true };
        for (var f = 0; f < StripFrames; f++)
        {
          var t = clip.Duration * f / StripFrames;
          frames.Add(MeshPreview.Render(mesh, StripSize, StripSize, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f, Distance = distance }, false, t, out _, settings));
        }
      }
      finally { mesh.Clip = null; }
    }

    return frames;
  }

  private void EnsureEffects()
  {
    lock (_owners)
    {
      if (_fxLibrary is not null)
        return;
      _fxLibrary = new EffectLibrary(new CatalogEffectSource(_catalog, _store));
      _fxAliases = _catalog.FindByFileName("part_aliases.ps") is { Count: > 0 } a && SslText.LooksText(_store.Read(a[0]))
        ? PartAliases.Parse(Encoding.UTF8.GetString(_store.Read(a[0])))
        : PartAliases.Empty;
      _fxTextures = new EffectTextures(name =>
        _catalog.FindByFileName(name + ".pct.resource") is { Count: > 0 } t ? TextureLoader.Load(_catalog, _store, t[0]) : null);
    }
  }

  private List<byte[]>? Effect(string path)
  {
    EnsureEffects();
    var name = Path.GetFileNameWithoutExtension(path);
    if (_fxLibrary!.Resolve(name) is not { } tree)
    {
      Dbg($"fx {name}: not resolved");
      return null;
    }

    var def = EffectDefinition.Build(name, tree);
    if (def.Emitters.Count == 0 && def.Trails.Count == 0)
    {
      Dbg($"fx {name}: no emitters");
      return null;
    }
    _fxTextures!.Prefetch(def.Textures);
    _fxTextures.WaitAll(TimeSpan.FromSeconds(20));
    var instance = EffectInstance.Standalone(def, 1);
    var seconds = Math.Clamp(def.Duration, 0.3f, 3f);
    var camera = EffectCamera.FitAll(instance, Math.Min(def.Duration, 6f), 1f);
    var (view, projection, _) = camera.Matrices(StripSize, StripSize);
    var frames = new List<byte[]>();
    var any = false;
    for (var f = 0; f < StripFrames; f++)
    {
      instance.Seek(seconds * (f + 0.5f) / StripFrames);
      var batches = EffectSprites.Build(instance, _fxAliases, view);
      frames.Add(EffectSplat.Render(batches, view * projection, StripSize, _fxTextures.Get, out var drew));
      any |= drew;
    }

    Dbg($"fx {name}: emitters={def.Emitters.Count} dur={def.Duration} drew={any} tex={def.Textures.Count()}");
    return any ? frames : null;
  }

  // FL_THUMBDBG=1 prints thumbnail failures and effect-splat diagnostics to stdout (dev aid).
  private static readonly bool Debug = Environment.GetEnvironmentVariable("FL_THUMBDBG") == "1";

  private static void Dbg(string text)
  {
    if (Debug)
      Console.WriteLine(text);
  }

  public void Dispose()
  {
    _disposed = true;
    _signal.Release(_workers.Length);
  }
}

/// <summary>
///   CPU sprite splat for effect thumbnails: the GPU sprite pass simplified (texture × vertex HDR colour, alpha clip,
///   CaptureTonemap at the bench exposure, add or blend far to near). An approximation for thumbnails only.
/// </summary>
internal static class EffectSplat
{
  public static byte[] Render(List<SpriteBatch> batches, Matrix4x4 viewProjection, int size, Func<string, EffectImage?> texture, out bool drew)
  {
    var acc = new float[size * size * 3];
    for (var i = 0; i < acc.Length; i += 3)
    {
      acc[i] = 0.075f; acc[i + 1] = 0.08f; acc[i + 2] = 0.09f;
    }

    drew = false;
    const float exposure = 1f / 250f;
    var verts = new (float X, float Y, float U, float V, Vector4 C, float Clip)[4];
    foreach (var batch in batches.OrderByDescending(b => b.Depth))
    {
      if (batch.Distortion || batch.QuadCount == 0)
        continue;
      var image = string.IsNullOrEmpty(batch.Emitter.Texture) ? null : texture(batch.Emitter.Texture);
      if (image is null || image.Width == 0)
        image = SoftDisc; // texture not in the open archives: a soft round particle keeps the shape / colour readable
      var add = batch.Emitter.Blend == EffectBlend.Add;
      var v = batch.Vertices;
      for (var q = 0; q < batch.QuadCount; q++)
      {
        var ok = true;
        for (var k = 0; k < 4; k++)
        {
          var o = (q * 4 + k) * SpriteBatch.Stride;
          if (o + SpriteBatch.Stride > v.Length) { ok = false; break; }
          var p = Vector4.Transform(new Vector4(v[o], v[o + 1], v[o + 2], 1), viewProjection);
          if (p.W <= 1e-4f) { ok = false; break; }
          verts[k] = ((p.X / p.W * 0.5f + 0.5f) * size, (0.5f - p.Y / p.W * 0.5f) * size, v[o + 3], v[o + 4],
            new Vector4(v[o + 5], v[o + 6], v[o + 7], v[o + 8]), v[o + 9]);
        }

        if (!ok)
          continue;
        drew |= Triangle(acc, size, image, verts[0], verts[1], verts[2], add, exposure);
        drew |= Triangle(acc, size, image, verts[0], verts[2], verts[3], add, exposure);
      }
    }

    var bgra = new byte[size * size * 4];
    for (var i = 0; i < size * size; i++)
    {
      bgra[i * 4] = (byte)(Math.Clamp(acc[i * 3 + 2], 0, 1) * 255);
      bgra[i * 4 + 1] = (byte)(Math.Clamp(acc[i * 3 + 1], 0, 1) * 255);
      bgra[i * 4 + 2] = (byte)(Math.Clamp(acc[i * 3], 0, 1) * 255);
      bgra[i * 4 + 3] = 255;
    }

    return bgra;
  }

  /// <summary>Stand-in sprite for a missing texture: white, alpha = smooth radial falloff.</summary>
  private static readonly EffectImage SoftDisc = MakeDisc(32);

  private static EffectImage MakeDisc(int n)
  {
    var rgba = new byte[n * n * 4];
    for (var y = 0; y < n; y++)
      for (var x = 0; x < n; x++)
      {
        var dx = (x + 0.5f) / n * 2 - 1;
        var dy = (y + 0.5f) / n * 2 - 1;
        var a = Math.Clamp(1 - MathF.Sqrt(dx * dx + dy * dy), 0, 1);
        var o = (y * n + x) * 4;
        rgba[o] = rgba[o + 1] = rgba[o + 2] = 255;
        rgba[o + 3] = (byte)(a * a * 255);
      }

    return new EffectImage(n, n, rgba);
  }

  private static float Gamma(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

  private static bool Triangle(float[] acc, int size, EffectImage image,
    (float X, float Y, float U, float V, Vector4 C, float Clip) a,
    (float X, float Y, float U, float V, Vector4 C, float Clip) b,
    (float X, float Y, float U, float V, Vector4 C, float Clip) c, bool add, float exposure)
  {
    var area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    if (MathF.Abs(area) < 1e-6f)
      return false;
    var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
    var maxX = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
    var minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
    var maxY = Math.Min(size - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
    if (minX > maxX || minY > maxY)
      return false;
    var drew = false;
    var inv = 1f / area;
    for (var y = minY; y <= maxY; y++)
      for (var x = minX; x <= maxX; x++)
      {
        var px = x + 0.5f;
        var py = y + 0.5f;
        var w0 = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) * inv;
        var w1 = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) * inv;
        var w2 = 1 - w0 - w1;
        if (w0 < 0 || w1 < 0 || w2 < 0)
          continue;
        var u = a.U * w0 + b.U * w1 + c.U * w2;
        var v = a.V * w0 + b.V * w1 + c.V * w2;
        var col = a.C * w0 + b.C * w1 + c.C * w2;
        var clip = a.Clip * w0 + b.Clip * w1 + c.Clip * w2;
        var tx = Math.Clamp((int)((u - MathF.Floor(u)) * image.Width), 0, image.Width - 1);
        var ty = Math.Clamp((int)((v - MathF.Floor(v)) * image.Height), 0, image.Height - 1);
        if (u is < -0.001f or > 1.001f) tx = Math.Clamp((int)(Math.Clamp(u, 0, 1) * image.Width), 0, image.Width - 1);
        if (v is < -0.001f or > 1.001f) ty = Math.Clamp((int)(Math.Clamp(v, 0, 1) * image.Height), 0, image.Height - 1);
        var s = (ty * image.Width + tx) * 4;
        if (s + 3 >= image.Rgba.Length)
          continue;
        var tr = image.Rgba[s] / 255f; var tg = image.Rgba[s + 1] / 255f; var tb = image.Rgba[s + 2] / 255f; var ta = image.Rgba[s + 3] / 255f;
        var alphaTex = clip > 0 ? Math.Clamp((ta - clip) / (1 - clip), 0, 1) : ta;
        var alpha = Math.Clamp(col.W, 0, 1) * alphaTex;
        if (alpha <= 0.002f)
          continue;
        var r = Gamma(CaptureTonemap.Apply(col.X * tr * exposure));
        var g = Gamma(CaptureTonemap.Apply(col.Y * tg * exposure));
        var bl = Gamma(CaptureTonemap.Apply(col.Z * tb * exposure));
        var o = (y * size + x) * 3;
        if (add)
        {
          acc[o] += r * alpha; acc[o + 1] += g * alpha; acc[o + 2] += bl * alpha;
        }
        else
        {
          acc[o] = r * alpha + acc[o] * (1 - alpha);
          acc[o + 1] = g * alpha + acc[o + 1] * (1 - alpha);
          acc[o + 2] = bl * alpha + acc[o + 2] * (1 - alpha);
        }

        drew = true;
      }

    return drew;
  }
}
