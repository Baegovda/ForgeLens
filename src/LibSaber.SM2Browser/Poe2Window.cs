// M2: PoE2 source window (viewport toolbar "PoE2"). Opens the PoE2 Content.ggpk read-only through poe_data_tools, lists
// meshes (.sm/.fmt) and effects (.ao) with a search box, and lists skills (ActiveSkills linked to their effect .ao through
// SkillArtVariations). Selecting an entry prepares a cache folder and shows the asset in the main ForgeLens preview.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Games.Poe2;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

internal sealed class Poe2Window : Window
{
  private readonly MainWindow _main;
  private readonly TextBox _ggpk = new() { Text = Environment.GetEnvironmentVariable("FL_POE2_GGPK") ?? @"D:\Daum Games\Path of Exile2\Content.ggpk", Width = 520 };
  private readonly TextBox _search = new() { Watermark = "Search files (e.g. monsters/basicskeleton .sm)", Width = 520 };
  private readonly ListBox _files = new() { Height = 420 };
  private readonly TextBox _skillSearch = new() { Watermark = "Search skills", Width = 520 };
  private readonly ListBox _skills = new() { Height = 220 };
  private readonly ListBox _effects = new() { Height = 160 };
  private readonly ComboBox _clips = new() { Width = 260 };
  private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#9FD0FF")) };
  private Poe2Source? _source;
  private List<string> _all = new();
  private HashSet<string> _known = new();
  private Poe2Skills? _skillData;
  private string? _current;
  private string? _currentAst;

  public static string ToolDir()
  {
    var local = Path.Combine(AppContext.BaseDirectory, "poe2tools");
    return Directory.Exists(local) ? local : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SM2_Work", "PoE2", "tools");
  }

  public static string CacheDir() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForgeLens", "poe2cache");

  public Poe2Window(MainWindow main)
  {
    _main = main;
    Title = "Path of Exile 2";
    Width = 600; Height = 900;
    var open = new Button { Content = "Open GGPK" };
    open.Click += async (_, _) => await OpenAsync();
    _search.TextChanged += (_, _) => Filter();
    _skillSearch.TextChanged += (_, _) => FilterSkills();
    _files.SelectionChanged += async (_, _) => { if (_files.SelectedItem is string s) await ShowAsync(s, null, null); };
    _skills.SelectionChanged += (_, _) =>
    {
      if (_skills.SelectedItem is SkillRow r)
        _effects.ItemsSource = r.Skill.Effects.Select(e => new EffectRow(e)).ToList();
    };
    _effects.SelectionChanged += async (_, _) =>
    {
      if (_effects.SelectedItem is EffectRow e)
        await ShowAsync(e.Effect.Ao, null, e.Effect.Loop.Split(';').FirstOrDefault(x => x.Length > 0));
    };
    _clips.SelectionChanged += async (_, _) =>
    {
      if (_clips.SelectedItem is string c && _current != null && !_loadingClips)
        await ShowAsync(_current, _currentAst, c, keepClips: true);
    };
    var tabs = new TabControl
    {
      Items =
      {
        new TabItem { Header = "Files", Content = new StackPanel { Spacing = 6, Children = { _search, _files } } },
        new TabItem { Header = "Skills", Content = new StackPanel { Spacing = 6, Children = { _skillSearch, _skills, new TextBlock { Text = "Effects (projectile / animated)" }, _effects } } },
      },
    };
    Content = new StackPanel
    {
      Margin = new Thickness(12), Spacing = 8,
      Children =
      {
        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _ggpk, open } },
        tabs,
        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = "Animation", VerticalAlignment = VerticalAlignment.Center }, _clips } },
        _status,
      },
    };
    if (Environment.GetEnvironmentVariable("FL_POE2_AUTOOPEN") == "1")
      Opened += async (_, _) => await OpenAsync();
  }

  private bool _loadingClips;

  private sealed record SkillRow(Poe2Skills.Skill Skill)
  {
    public override string ToString() => $"{(Skill.Name.Length > 0 ? Skill.Name : Skill.Id)}  [{Skill.Id}]  {Skill.Effects.Count} effects";
  }

  private sealed record EffectRow(Poe2Skills.Effect Effect)
  {
    public override string ToString() => $"{Effect.Kind}: {Effect.Id} -> {Effect.Ao}";
  }

  private void Status(string s) => Dispatcher.UIThread.Post(() => _status.Text = s);

  public async Task OpenAsync()
  {
    var ggpk = _ggpk.Text ?? "";
    if (!File.Exists(ggpk)) { Status("GGPK not found: " + ggpk); return; }
    _source = new Poe2Source(ggpk, CacheDir(), ToolDir());
    if (!File.Exists(_source.Tool)) { Status("poe_data_tools.exe not found in " + ToolDir()); return; }
    Status("Listing files (first time only, about a minute)...");
    var src = _source;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    _all = await Task.Run(src.Files);
    _known = new HashSet<string>(_all);
    Status($"{_all.Count:N0} entries ({sw.Elapsed.TotalSeconds:0.0} s). Loading skill tables...");
    Filter();
    try
    {
      var dir = await Task.Run(src.Tables);
      _skillData = await Task.Run(() => Poe2Skills.Load(dir));
      FilterSkills();
      Status($"{_all.Count(f => !f.EndsWith(".ast")):N0} meshes/effects, {_skillData.Skills.Count:N0} skills ({_skillData.Skills.Count(s => s.Effects.Count > 0):N0} with effects). Ready.");
    }
    catch (Exception e) { Status("Skill tables failed: " + e.Message); }
    // Test hooks (no clicks needed): FL_POE2_SKILL=<skill id> selects that skill and its first effect; FL_POE2_SELECT=<path> opens a file.
    if (Environment.GetEnvironmentVariable("FL_POE2_SKILL") is { Length: > 0 } sk && _skills.ItemsSource is IEnumerable<SkillRow> rows)
    {
      _skillSearch.Text = sk;
      FilterSkills();
      var row = (_skills.ItemsSource as IEnumerable<SkillRow>)?.FirstOrDefault(r => r.Skill.Id == sk);
      if (row != null) { if (Content is StackPanel sp && sp.Children[1] is TabControl tc) tc.SelectedIndex = 1; _skills.SelectedItem = row; _effects.SelectedIndex = 0; }
    }
    else if (Environment.GetEnvironmentVariable("FL_POE2_SELECT") is { Length: > 0 } sel)
    {
      _search.Text = sel;
      Filter();
      _files.SelectedItem = sel;
    }
  }

  private void Filter()
  {
    var q = (_search.Text ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    _files.ItemsSource = _all.Where(f => !f.EndsWith(".ast") && q.All(f.Contains)).Take(3000).ToList();
  }

  private void FilterSkills()
  {
    if (_skillData == null) return;
    var q = (_skillSearch.Text ?? "").Trim().ToLowerInvariant();
    _skills.ItemsSource = _skillData.Skills.Where(s => s.Effects.Count > 0 && (q.Length == 0 || s.Id.Contains(q) || s.Name.ToLowerInvariant().Contains(q)))
      .Select(s => new SkillRow(s)).ToList();
  }

  public async Task ShowAsync(string path, string? ast, string? clip, bool keepClips = false)
  {
    if (_source == null) return;
    path = path.ToLowerInvariant();
    var isEffect = path.EndsWith(".ao");
    ast ??= isEffect ? null : Poe2Source.GuessSkeleton(path, _known);
    Status($"Preparing {path}...");
    var src = _source;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
      var (mesh, albedo, clips, log) = await Task.Run(() =>
      {
        src.Prepare(path, ast);
        var reader = new Poe2Reader(src.Cache);
        var m = isEffect ? reader.LoadEffect(path, clip) : reader.Load(path, ast, clip ?? (ast != null ? "?" : null));
        var a = new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in m.Parts.Concat(m.Levels.SelectMany(l => l.Parts)).Select(p => p.Texture).Where(t => t != null).Distinct())
          if (reader.Texture(t!) is { } img) a[t!] = img;
        List<string> c = new();
        var skel = isEffect ? reader.ReadAo(path).Skeleton : ast;
        if (skel != null) { try { c = reader.Animations(skel); } catch (Exception) { } }
        return (m, a, c, reader.Log);
      });
      _current = path; _currentAst = ast;
      if (!keepClips)
      {
        _loadingClips = true;
        _clips.ItemsSource = clips;
        _clips.SelectedItem = mesh.Clip?.Name;
        _loadingClips = false;
      }
      // M2 D: effects play as particles (SM2 simulation); the M1 rig/emitter-mesh view is the fallback (or FL_POE2_RIGVIEW=1).
      if (!(isEffect && await _main.ShowPoe2EffectAsync(new Poe2Reader(src.Cache), path, clip)))
        _main.ShowPoe2(mesh, albedo, path);
      else log.Add("effect particles: SM2 simulation");
      Status($"{string.Join("  ", log.Where(l => l.Contains("verts") || l.Contains("effect ")).Take(2))}  ({sw.Elapsed.TotalSeconds:0.0} s)");
    }
    catch (Exception e)
    {
      Status($"Failed: {path}: {e.GetType().Name}: {e.Message}");
    }
  }
}
