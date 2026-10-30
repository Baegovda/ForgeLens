// R54b: SM2_SELECT=<catalog path> drives the file list exactly like a click (folder scope -> row selected ->
// FileSelectionChanged -> PreviewFileAsync). SM2_WINSHOT=<png> saves the real window (RenderTargetBitmap) once the
// level is shown, plus <png>.txt with the level stats and the open time; SM2_WINSHOT_EXIT=1 closes the window after.
// With SM2_SELECT the window opens without activation (no focus stealing on the PC).
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private Task SaveWindowAsync(string png)
  {
    var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
    using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
    bmp.Render(this);
    bmp.Save(png);
    return Task.CompletedTask;
  }

  private string MeshTreeDump()
  {
    var sb = new System.Text.StringBuilder();
    void Walk(IEnumerable<MeshToggle> items, int depth)
    {
      foreach (var t in items)
      {
        sb.Append(' ', depth * 2).Append(t.IsGroup ? "[group] " : "").Append(t.Label).Append(t.Visible ? " (on)" : " (off)").Append('\n');
        Walk(t.Children, depth + 1);
      }
    }
    if (MeshList.ItemsSource is IEnumerable<MeshToggle> roots)
      Walk(roots, 0);
    return sb.ToString();
  }

  private async Task SelectFromEnvironmentAsync()
  {
    if (Environment.GetEnvironmentVariable("SM2_SELECT") is not { Length: > 0 } path || _catalog is null)
      return;
    var sw = Stopwatch.StartNew();
    var showAll = _settings.ShowAllFiles;
    if (ShowAllBox.IsChecked != true) { ShowAllBox.IsChecked = true; ShowAllChanged(ShowAllBox, new Avalonia.Interactivity.RoutedEventArgs()); }
    OpenRecent(new RecentEntry { Path = path.Replace('\\', '/') });
    _settings.ShowAllFiles = showAll; try { _settings.Save(); } catch { /* best effort */ }
    if (Environment.GetEnvironmentVariable("SM2_WINSHOT") is not { Length: > 0 } shot)
      return;
    // wait for the level build and the first rendered frame
    for (var i = 0; i < 6000 && _level is null && _mesh is null; i++) // R66: meshes too
      await Task.Delay(100);
    var open = sw.Elapsed.TotalSeconds;
    // R66: SM2_WINSHOT_CPU=1: after the mesh is shown, switch to the CPU path so RenderTargetBitmap shots contain the
    // 3D view (the GPU control is not in RenderTargetBitmap). Not persisted (Click handler not raised).
    if (Environment.GetEnvironmentVariable("SM2_WINSHOT_CPU") == "1" && _level is null) { GpuToggle.IsChecked = false; GpuToggled(null, new Avalonia.Interactivity.RoutedEventArgs()); await Task.Delay(8000); }
    await Task.Delay(6000); // first frame (CPU or GPU path)
    var selected = (FileList.SelectedItem as FileRow) is { } row && _catalog is { } c ? c.GetFile(row.Index).Path : "(none)";
    try
    {
      var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
      using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
      bmp.Render(this);
      Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(shot))!);
      bmp.Save(shot);
      File.WriteAllText(shot + ".txt", $"selected {selected}\nlevel {(_level is null ? "(none)" : _level.Stats)}\nmesh {_mesh?.Name} summary {_mesh?.Summary.Replace("\n", " | ")}\nhud {HudSnapshot()}\nselect_to_level_shown {open:0.0}s gpu {UseGpu()} from_disk_cache {_level?.FromDiskCache}\ntiming {_level?.Timing}\n");
    }
    catch (Exception e) { File.WriteAllText(shot + ".txt", "winshot failed: " + e); }
    if (Environment.GetEnvironmentVariable("SM2_KITBASH") is "0" or "1")
    { ShowKitbashTab(Environment.GetEnvironmentVariable("SM2_KITBASH") == "1", save: false); await Task.Delay(1500); try { await SaveWindowAsync(Path.ChangeExtension(shot, null) + "_kitbash.png"); } catch { } }
    // R71: mesh tree dump (+ collapsed / expanded shots of the "Collision / Helpers" group).
    try { File.AppendAllText(shot + ".txt", $"note_runs {PreviewNote.Inlines?.Count ?? 0} note_text {(PreviewNote.Inlines is { Count: > 0 } il ? string.Concat(il.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)) : PreviewNote.Text)?.Replace("\n", " | ")}\n"); } catch { }
    try { File.WriteAllText(shot + ".meshes.txt", MeshTreeDump()); } catch { /* best effort */ }
    if (Environment.GetEnvironmentVariable("SM2_MESHGROUP_SHOT") == "1" && MeshList.ItemsSource is IEnumerable<MeshToggle> roots
        && roots.FirstOrDefault(r => r.IsGroup) is { } group)
    {
      MeshList.ScrollIntoView(group);
      await Task.Delay(1500);
      try { await SaveWindowAsync(Path.ChangeExtension(shot, null) + "_collapsed.png"); } catch { /* best effort */ }
      await Task.Delay(1000);
      if (MeshList.ContainerFromItem(group) is TreeViewItem tvi)
      {
        tvi.IsExpanded = true;
        await Task.Delay(1500);
        try { await SaveWindowAsync(Path.ChangeExtension(shot, null) + "_expanded.png"); } catch (Exception e) { File.AppendAllText(shot + ".txt", "expanded shot failed: " + e + "\n"); }
        MeshList.SelectedItem = group.Children[0];
        await Task.Delay(800);
        File.AppendAllText(shot + ".txt", $"group_select {(MeshList.SelectedItem as MeshToggle)?.Label}\n");
        tvi.IsExpanded = false;
      }
      else File.AppendAllText(shot + ".txt", "group container not found\n");
    }
    if (Environment.GetEnvironmentVariable("SM2_ORBITTEST") == "1")
      await OrbitTestAsync(shot);
    if (Environment.GetEnvironmentVariable("SM2_PICKTEST") == "1")
      await PickTestAsync(shot);
    if (Environment.GetEnvironmentVariable("SM2_UETEST") == "1")
      await UeTestAsync(shot); // R79
    if (Environment.GetEnvironmentVariable("SM2_FLYTEST") == "1")
      await FlyTestAsync(shot, SaveWindowAsync);
    if (Environment.GetEnvironmentVariable("SM2_WINSHOT_EXIT") == "1")
      Dispatcher.UIThread.Post(Close);
  }
}
