using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Export;

namespace LibSaber.SM2Browser;

/// <summary>oneclick: progress, log and result of a Direct-to-Unreal import (non-modal).</summary>
public partial class UnrealImportWindow : Window
{
  private string? _logFolder;
  private readonly System.Text.StringBuilder _log = new();

  public UnrealImportWindow()
  {
    InitializeComponent();
  }

  public void Start(string target, string? logFolder)
  {
    TargetText.Text = target;
    _logFolder = logFolder;
  }

  public void Append(string line)
  {
    if (!Dispatcher.UIThread.CheckAccess())
    {
      Dispatcher.UIThread.Post(() => Append(line));
      return;
    }

    _log.AppendLine(line);
    LogBox.Text = _log.ToString();
    LogBox.CaretIndex = LogBox.Text.Length;
    StatusText.Text = line;
  }

  public void Finish(UnrealImportResult result)
  {
    Bar.IsIndeterminate = false;
    Bar.Value = 100;
    TitleText.Text = result.Success ? $"Imported into Unreal ({result.Mode})" : "Unreal import failed" + (result.Mode.Length > 0 ? $" ({result.Mode})" : "");
    StatusText.Text = result.Summary;
    StatusBorder.Background = new SolidColorBrush(Color.Parse(result.Success ? "#1F3B2A" : "#4A2323"));
    Append(result.Summary);
  }

  private void OpenLogClicked(object? sender, RoutedEventArgs e)
  {
    if (_logFolder is null || !Directory.Exists(_logFolder))
      return;
    try
    {
      Process.Start(new ProcessStartInfo(_logFolder) { UseShellExecute = true });
    }
    catch
    {
      // no shell
    }
  }

  private void CloseClicked(object? sender, RoutedEventArgs e) => Close();
}
