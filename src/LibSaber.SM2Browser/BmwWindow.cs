// BMW M1: Black Myth: Wukong source window (viewport toolbar "Wukong"). Lists the .glb files exported by tools/bmw
// (CUE4Parse -> glTF + PNG + material .json) and shows the selected one in the main ForgeLens preview, like the PoE2 window.
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LibSaber.SM2.Assets.Games.Bmw;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

internal sealed class BmwWindow : Window
{
  private readonly MainWindow _main;
  private readonly TextBox _dir = new() { Text = Environment.GetEnvironmentVariable("FL_BMW_EXPORT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SM2_Work", "BMW", "exp"), Width = 480 };
  private readonly TextBox _search = new() { Watermark = "Search exported meshes (e.g. SK_Wukong)", Width = 560 };
  private readonly ListBox _files = new() { Height = 600 };
  private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#9FD0FF")) };
  private List<string> _all = new();

  public BmwWindow(MainWindow main)
  {
    _main = main;
    Title = "Black Myth: Wukong";
    Width = 600; Height = 820;
    var refresh = new Button { Content = "Refresh" };
    refresh.Click += (_, _) => Scan();
    _search.TextChanged += (_, _) => Filter();
    _files.SelectionChanged += async (_, _) => { if (_files.SelectedItem is string s) await ShowAsync(s); };
    Content = new StackPanel
    {
      Margin = new Thickness(10), Spacing = 6,
      Children =
      {
        new TextBlock { Text = "Export folder (tools/bmw: pak -> glTF + PNG)" },
        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _dir, refresh } },
        _search, _files, _status,
      },
    };
    Opened += (_, _) =>
    {
      Scan();
      var auto = Environment.GetEnvironmentVariable("FL_BMW_FILE"); // test hook: select the first match on open
      if (!string.IsNullOrEmpty(auto)) { _search.Text = auto; Filter(); Avalonia.Threading.DispatcherTimer.RunOnce(() => _files.SelectedIndex = 0, TimeSpan.FromSeconds(12)); } // after the main window restored its own last asset
    };
  }

  private void Scan()
  {
    var d = _dir.Text ?? "";
    _all = Directory.Exists(d) ? Directory.EnumerateFiles(d, "*.glb", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(d, f)).OrderBy(x => x).ToList() : new();
    _status.Text = Directory.Exists(d) ? $"{_all.Count} meshes" : "Folder not found";
    Filter();
  }

  private void Filter()
  {
    var q = _search.Text ?? "";
    _files.ItemsSource = _all.Where(f => q.Length == 0 || f.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(2000).ToList();
  }

  private async Task ShowAsync(string rel)
  {
    var root = _dir.Text ?? "";
    _status.Text = "Loading " + rel;
    try
    {
      var (mesh, albedo) = await Task.Run(() =>
      {
        var m = BmwGlb.Load(Path.Combine(root, rel), root);
        var a = new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in m.Parts.Select(p => p.Texture).Where(t => t != null).Distinct())
          if (LoadPng(t!) is { } img) a[t!] = img;
        return (m, a);
      });
      _main.ShowPoe2(mesh, albedo, rel);
      _status.Text = mesh.Summary;
    }
    catch (Exception x) { _status.Text = "Failed: " + x.Message; }
  }

  /// <summary>Decodes a PNG to BGRA, longest edge at most 1024 px (preview only).</summary>
  internal static PreviewImage? LoadPng(string path)
  {
    try
    {
      using var fs = File.OpenRead(path);
      using var full = new Bitmap(fs);
      var scale = Math.Min(1.0, 1024.0 / Math.Max(full.PixelSize.Width, full.PixelSize.Height));
      using var bmp = scale < 1 ? full.CreateScaledBitmap(new PixelSize(Math.Max(1, (int)(full.PixelSize.Width * scale)), Math.Max(1, (int)(full.PixelSize.Height * scale)))) : null;
      var src = bmp ?? full;
      var w = src.PixelSize.Width; var h = src.PixelSize.Height;
      var bytes = new byte[w * h * 4];
      var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      try { src.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), bytes.Length, w * 4); }
      finally { handle.Free(); }
      if (src.Format == PixelFormat.Rgba8888)
        for (var i = 0; i < bytes.Length; i += 4) (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]);
      return new PreviewImage { Width = w, Height = h, Bgra = bytes };
    }
    catch (Exception) { return null; }
  }
}
