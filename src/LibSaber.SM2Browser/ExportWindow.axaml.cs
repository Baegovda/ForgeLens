using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public sealed class ExportChoice
{
  public required string Folder { get; init; }
  public required ExportSettings Settings { get; init; }
  public ClipChoice Clips { get; init; }
  /// <summary>oneclick: set when "Direct to Unreal" is checked.</summary>
  public UnrealDirectChoice? Unreal { get; init; }
}

public partial class ExportWindow : Window
{

  private TaskCompletionSource<ExportChoice?>? _result;

  public ExportWindow()
  {
    InitializeComponent();
  }

  private VariantResult? _parts;
  private ComboBox? _presetBox;
  private readonly Dictionary<string, ComboBox> _sectionBoxes = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, CheckBox> _regionBoxes = new(StringComparer.OrdinalIgnoreCase);
  private Dictionary<string, List<string>> _skinRegions = new(StringComparer.OrdinalIgnoreCase);

  private string? _fixedVariant;

  private string _defaultContent = "";
  private bool _contentEdited;
  private bool _settingContent;

  private string _directAsset = "asset";

  /// <summary>oneclick: last project and the asset the suggested /Game folder is named after (Direct-to-Unreal option).</summary>
  public void SetDirect(string? lastProject, string asset)
  {
    ProjectBox.Text = string.IsNullOrWhiteSpace(lastProject) ? UnrealContentPath.DefaultProject : lastProject;
    _directAsset = asset;
    SuggestContent();
    MannyBox.IsCheckedChanged -= MannyChanged;
    MannyBox.IsCheckedChanged += MannyChanged;
  }

  private void MannyChanged(object? sender, RoutedEventArgs e)
  {
    if (!_contentEdited)
      SuggestContent();
  }

  private void SuggestContent()
  {
    _defaultContent = UnrealContentPath.Default(_directAsset, MannyBox.IsChecked == true);
    _settingContent = true;
    ContentBox.Text = _defaultContent;
    _settingContent = false;
  }

  public Task<ExportChoice?> Choose(Window owner, string? initialFolder, VariantResult? parts = null, Dictionary<string, List<string>>? skinRegions = null, string? fixedVariant = null, string? fixedSummary = null)
  {
    FolderBox.Text = initialFolder ?? "";
    if (string.IsNullOrEmpty(ProjectBox.Text))
      SetDirect(null, "asset");
    if (fixedVariant is not null)
      BuildFixed(fixedVariant, fixedSummary);
    else if (parts is not null)
      BuildParts(parts, skinRegions);
    FitToScreen(owner);
    _result = new TaskCompletionSource<ExportChoice?>();
    ApplyQaEnvironment();
    ShowDialog(owner).ContinueWith(_ => _result.TrySetResult(null), TaskScheduler.FromCurrentSynchronizationContext());
    return _result.Task;
  }

  private void FitToScreen(Window owner)
  {
    var screen = owner.Screens?.ScreenFromVisual(owner) ?? owner.Screens?.Primary;
    if (screen is null || screen.Scaling < 0.5 || screen.WorkingArea.Height < 1)
    {
      OptionsScroll.MaxHeight = 480;
      return;
    }

    // WorkingArea is physical pixels. Window sizes are device-independent.
    var height = screen.WorkingArea.Height / screen.Scaling;
    var width = screen.WorkingArea.Width / screen.Scaling;
    var availableHeight = Math.Max(240, height - 48);
    var availableWidth = Math.Max(360, width - 48);
    if (availableHeight < MinHeight)
      MinHeight = availableHeight;
    MaxHeight = availableHeight;
    MaxWidth = Math.Min(760, availableWidth);
    if (Width > MaxWidth)
      Width = MaxWidth;
    OptionsScroll.MaxHeight = Math.Max(120, availableHeight - 240);
  }

  private async void BrowseClicked(object? sender, RoutedEventArgs e)
  {
    var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
    {
      Title = "Export folder",
      AllowMultiple = false,
    });
    if (folders.Count == 0)
      return;
    var path = folders[0].TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    FolderBox.Text = path;
    FolderError.Text = "";
  }

  /// <summary>QA (oneclick): SM2_DIRECT_PROJECT / SM2_DIRECT_PATH check "Direct to Unreal" with those values;
  ///   SM2_EXPORT_CLIPS = clip choice index; SM2_EXPORT_MANNY=1 checks Anim Only (Manny); SM2_EXPORT_GO = seconds after
  ///   which Export is pressed.</summary>
  private void ApplyQaEnvironment()
  {
    if (Environment.GetEnvironmentVariable("SM2_EXPORT_QA") is not { Length: > 0 })
      return;
    if (Environment.GetEnvironmentVariable("SM2_DIRECT_PROJECT") is { Length: > 0 } project)
    {
      DirectBox.IsChecked = true;
      DirectOptions.IsEnabled = true;
      ProjectBox.Text = project;
    }

    // Manny first: checking it re-suggests the content folder, which must not replace SM2_DIRECT_PATH.
    if (Environment.GetEnvironmentVariable("SM2_EXPORT_MANNY") == "1")
      MannyBox.IsChecked = true;
    if (Environment.GetEnvironmentVariable("SM2_DIRECT_PATH") is { Length: > 0 } path)
    {
      ContentBox.Text = path;
      _contentEdited = !string.Equals(UnrealContentPath.Normalize(path), UnrealContentPath.Normalize(_defaultContent), StringComparison.Ordinal);
    }

    if (int.TryParse(Environment.GetEnvironmentVariable("SM2_EXPORT_CLIPS"), out var clips) && clips >= 0 && clips < ClipBox.ItemCount)
      ClipBox.SelectedIndex = clips;
    if (double.TryParse(Environment.GetEnvironmentVariable("SM2_EXPORT_GO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var go))
      Avalonia.Threading.DispatcherTimer.RunOnce(() => ExportClicked(this, new RoutedEventArgs()), TimeSpan.FromSeconds(Math.Max(0.1, go)));
  }

  private void DirectChanged(object? sender, RoutedEventArgs e)
  {
    DirectOptions.IsEnabled = DirectBox.IsChecked == true;
    FolderError.Text = "";
  }

  private void ContentChanged(object? sender, TextChangedEventArgs e)
  {
    if (!_settingContent)
      _contentEdited = !string.Equals(UnrealContentPath.Normalize(ContentBox.Text), UnrealContentPath.Normalize(_defaultContent), StringComparison.Ordinal);
  }

  private async void BrowseProjectClicked(object? sender, RoutedEventArgs e)
  {
    IStorageFolder? start = null;
    try
    {
      var dir = Path.GetDirectoryName(ProjectBox.Text ?? "");
      if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        start = await StorageProvider.TryGetFolderFromPathAsync(dir);
    }
    catch
    {
      // no start folder
    }

    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
    {
      Title = "Unreal project",
      AllowMultiple = false,
      SuggestedStartLocation = start,
      FileTypeFilter = new[] { new FilePickerFileType("Unreal project") { Patterns = new[] { "*.uproject" } } },
    });
    if (files.Count == 0)
      return;
    var path = files[0].TryGetLocalPath();
    if (!string.IsNullOrWhiteSpace(path))
      ProjectBox.Text = path;
  }

  /// <summary>oneclick: the Direct-to-Unreal choice, or an error text.</summary>
  internal (UnrealDirectChoice? Choice, string? Error) DirectChoice()
  {
    if (DirectBox.IsChecked != true)
      return (null, null);
    var project = ProjectBox.Text?.Trim().Trim('"') ?? "";
    if (!project.EndsWith(".uproject", StringComparison.OrdinalIgnoreCase) || !File.Exists(project))
      return (null, "Direct to Unreal: choose an existing .uproject file.");
    var content = UnrealContentPath.Normalize(ContentBox.Text);
    if (UnrealContentPath.Validate(content) is { } err)
      return (null, "Direct to Unreal: " + err);
    return (new UnrealDirectChoice { Project = project, ContentPath = content, ContentPathIsDefault = !_contentEdited }, null);
  }

  private void ExportClicked(object? sender, RoutedEventArgs e)
  {
    var folder = FolderBox.Text?.Trim() ?? "";
    if (folder.Length == 0 || !Directory.Exists(folder))
    {
      FolderError.Text = "Choose a folder that exists.";
      return;
    }

    var (direct, directError) = DirectChoice();
    if (directError is not null)
    {
      FolderError.Text = directError;
      return;
    }
    if (direct is not null && (FbxBox.IsChecked != true || SpaceBox.SelectedIndex != 0))
    {
      FolderError.Text = "Direct to Unreal needs \"Models as FBX\" and the Unreal target (FBX Z-up centimeters).";
      return;
    }

    var settings = new ExportSettings
    {
      WriteDds = DdsBox.IsChecked == true,
      WritePng = PngBox.IsChecked == true,
      WriteTga = TgaBox.IsChecked == true,
      WriteGltf = GltfBox.IsChecked == true,
      WriteFbx = FbxBox.IsChecked == true,
      WriteMaterials = MaterialBox.IsChecked == true,
      WriteModelTextures = ModelTexturesBox.IsChecked == true,
      CopyOriginalBytes = RawBox.IsChecked == true,
      Space = SpaceBox.SelectedIndex == 1 ? ExportSpace.Blender : ExportSpace.Unreal,
      IncludeClothSim = ClothBox.IsChecked == true,
      RootMotion = RootMotionBox.IsChecked == true,
      InPlace = AnimRootBox.SelectedIndex == 1,
      MannequinReference = MannyBox.IsChecked == true ? "builtin" : null,
      AnimationOnly = MannyBox.IsChecked == true,
      Physics = MannyBox.IsChecked != true,
      Variant = PartsSpec(),
    };
    _result?.TrySetResult(new ExportChoice { Folder = folder, Settings = settings, Clips = (ClipChoice)Math.Max(0, ClipBox.SelectedIndex), Unreal = direct });
    Close();
  }

  private void CancelClicked(object? sender, RoutedEventArgs e)
  {
    _result?.TrySetResult(null);
    Close();
  }


  /// <summary>Part picker for the previewed model: one combo per armor section, or a skin combo plus one toggle per weapon region.</summary>
  private void BuildParts(VariantResult parts, Dictionary<string, List<string>>? skinRegions)
  {
    _parts = parts;
    _skinRegions = skinRegions ?? new(StringComparer.OrdinalIgnoreCase);
    var weapon = parts.Team is "WEAPON" or "MELEE"; // ui-45: melee region skins use the same skin + region picker
    PartsPanel.IsVisible = true;
    PartsPanel.Children.Add(new TextBlock
    {
      TextWrapping = Avalonia.Media.TextWrapping.Wrap,
      FontWeight = Avalonia.Media.FontWeight.SemiBold,
      Text = weapon
        ? $"Parts: {parts.Template} ({parts.Appearance}). Pick a skin, then toggle regions. Only the checked regions are exported."
        : $"Parts: {parts.Template} ({parts.Team}/{parts.Appearance}). Pick one region per slot. Only that combination is exported.",
    });
    var options = new List<string> { "(all parts, no filter)" };
    options.AddRange(parts.Presets);
    _presetBox = new ComboBox { ItemsSource = options, SelectedIndex = Math.Max(0, options.IndexOf(parts.Preset)), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    PartsPanel.Children.Add(Row(weapon ? "Skin" : "Preset", _presetBox));
    if (weapon)
    {
      var wrap = new WrapPanel();
      foreach (var region in parts.AvailableRegions.Keys.Where(r => !r.Equals("main", StringComparison.OrdinalIgnoreCase)).OrderBy(r => r))
      {
        var box = new CheckBox { Content = region, IsChecked = parts.Regions.ContainsKey(region), Margin = new Avalonia.Thickness(0, 0, 12, 0) };
        _regionBoxes[region] = box;
        wrap.Children.Add(box);
      }

      PartsPanel.Children.Add(wrap);
      _presetBox.SelectionChanged += (_, _) =>
      {
        if (_presetBox.SelectedItem is string skin && _skinRegions.TryGetValue(skin, out var on))
        {
          foreach (var (r, b) in _regionBoxes)
            b.IsChecked = on.Contains(r, StringComparer.OrdinalIgnoreCase);
        }
      };
    }
    else
    {
      foreach (var (section, regions) in parts.AvailableRegions.OrderBy(p => p.Key))
      {
        var items = new List<string>(regions);
        var box = new ComboBox { ItemsSource = items, SelectedIndex = parts.Regions.TryGetValue(section, out var cur) ? items.IndexOf(cur) : -1, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        _sectionBoxes[section] = box;
        PartsPanel.Children.Add(Row(section, box));
      }
    }

    PartsPanel.Children.Add(new Separator());
  }

  /// <summary>ui-44 Kitbasher: the combination was picked in the main window; show it read-only and export exactly it.</summary>
  private void BuildFixed(string spec, string? summary)
  {
    _fixedVariant = spec;
    PartsPanel.IsVisible = true;
    PartsPanel.Children.Add(new TextBlock
    {
      TextWrapping = Avalonia.Media.TextWrapping.Wrap,
      FontWeight = Avalonia.Media.FontWeight.SemiBold,
      Text = "Kitbash combination (only these parts are exported):",
    });
    PartsPanel.Children.Add(new TextBlock
    {
      TextWrapping = Avalonia.Media.TextWrapping.Wrap,
      FontSize = 12,
      Opacity = 0.8,
      Text = summary ?? spec.Replace(",", "\n"),
    });
    PartsPanel.Children.Add(new Separator());
  }

  private static Control Row(string label, Control control)
  {
    var dock = new DockPanel();
    var text = new TextBlock { Text = label, Width = 150, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    DockPanel.SetDock(text, Dock.Left);
    dock.Children.Add(text);
    dock.Children.Add(control);
    return dock;
  }

  /// <summary>--variant spec for the picked combination (null keeps the exporter default).</summary>
  internal string? PartsSpec()
  {
    if (_fixedVariant is not null)
      return _fixedVariant;
    if (_parts is null || _presetBox is null)
      return null;
    if (_presetBox.SelectedIndex <= 0)
      return "all";
    var preset = (string)_presetBox.SelectedItem!;
    if (_parts.Team is "WEAPON" or "MELEE")
    {
      var on = _regionBoxes.Where(p => p.Value.IsChecked == true).Select(p => p.Key).ToList();
      var skin = _skinRegions.GetValueOrDefault(preset) ?? new();
      var same = on.Count == skin.Count(r => _regionBoxes.ContainsKey(r)) && on.All(r => skin.Contains(r, StringComparer.OrdinalIgnoreCase));
      return same ? "skin=" + preset : "skin=" + preset + ",regions=" + string.Join("|", on);
    }

    var parts = new List<string> { "preset=" + preset.Replace(CustomizationLibrary.PresetPrefix, "") };
    foreach (var (section, box) in _sectionBoxes)
    {
      if (box.SelectedItem is string uid)
        parts.Add(section + "=" + uid);
    }

    return string.Join(",", parts);
  }
}
