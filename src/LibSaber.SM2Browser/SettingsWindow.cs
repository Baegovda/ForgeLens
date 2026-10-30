// R95: Settings window (viewport toolbar "Settings"): Performance = worker threads and max working memory (PerfBudget).
// Both apply immediately (next model open / next decode), persisted in settings.json.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LibSaber.SM2Browser;

internal sealed class SettingsWindow : Window
{
  private readonly BrowserSettings _settings;
  private readonly CheckBox _threadsAuto = new() { Content = "Auto" };
  private readonly NumericUpDown _threads = new() { Minimum = 1, Increment = 1, FormatString = "0", Width = 130 };
  private readonly CheckBox _memAuto = new() { Content = "Auto" };
  private readonly NumericUpDown _mem = new() { Increment = 0.5m, FormatString = "0.0", Width = 130 };
  private readonly TextBlock _effective = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#9FD0FF")) };
  private bool _loading = true;

  public SettingsWindow(BrowserSettings settings)
  {
    _settings = settings;
    Title = "Settings";
    Width = 640; SizeToContent = SizeToContent.Height; CanResize = false;
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _threads.Maximum = PerfBudget.MaxManualThreads;
    _mem.Minimum = PerfBudget.MinMemoryMB / 1024m;
    _mem.Maximum = Math.Round(PerfBudget.MaxManualMemoryMB / 1024m, 1);
    _threadsAuto.IsChecked = PerfBudget.SettingThreads == 0;
    _threads.Value = PerfBudget.SettingThreads > 0 ? PerfBudget.SettingThreads : PerfBudget.Logical;
    _memAuto.IsChecked = PerfBudget.SettingMemoryMB == 0;
    _mem.Value = Math.Round((PerfBudget.SettingMemoryMB > 0 ? PerfBudget.SettingMemoryMB : PerfBudget.TotalRamMB / 2) / 1024m, 1);
    _threadsAuto.IsCheckedChanged += (_, _) => Changed();
    _memAuto.IsCheckedChanged += (_, _) => Changed();
    _threads.ValueChanged += (_, _) => Changed();
    _mem.ValueChanged += (_, _) => Changed();
    var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, Width = 90 };
    close.Click += (_, _) => Close();
    StackPanel Row(string label, Control a, Control b, string unit) => new()
    {
      Orientation = Orientation.Horizontal, Spacing = 10,
      Children = { new TextBlock { Text = label, Width = 140, VerticalAlignment = VerticalAlignment.Center }, a, b, new TextBlock { Text = unit, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray } },
    };
    Content = new StackPanel
    {
      Margin = new Thickness(16), Spacing = 12,
      Children =
      {
        new TextBlock { Text = "Performance", FontSize = 16, FontWeight = FontWeight.SemiBold },
        Row("Worker threads", _threadsAuto, _threads, $"1-{PerfBudget.MaxManualThreads} (Auto = {PerfBudget.Logical})"),
        Row("Max working memory", _memAuto, _mem, $"GB (Auto = 50 % of {PerfBudget.TotalRamMB / 1024.0:0} GB)"),
        new TextBlock { Text = "Applied immediately to the next model open. Environment variables SM2_THREADS / SM2_MAXMEM override these values.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, FontSize = 11 },
        _effective,
        close,
      },
    };
    _loading = false;
    Changed();
  }

  private void Changed()
  {
    _threads.IsEnabled = _threadsAuto.IsChecked != true;
    _mem.IsEnabled = _memAuto.IsChecked != true;
    if (_loading) return;
    var t = _threadsAuto.IsChecked == true ? 0 : (int)(_threads.Value ?? PerfBudget.Logical);
    var m = _memAuto.IsChecked == true ? 0 : (int)((_mem.Value ?? 4) * 1024);
    PerfBudget.Apply(t, m);
    _settings.WorkerThreads = t == 0 ? null : t;
    _settings.MaxMemoryMB = m == 0 ? null : m;
    try { _settings.Save(); } catch { /* best effort */ }
    _effective.Text = $"In use now: {PerfBudget.Threads} worker threads ({PerfBudget.ThreadSource}), memory limit {PerfBudget.BudgetMB / 1024.0:0.0} GB ({PerfBudget.MemorySource}; currently {PerfBudget.EffectiveBudgetMB() / 1024.0:0.0} GB with 4 GB kept free for the system). Process working set {PerfBudget.WorkingSetMB()} MB.";
  }
}
