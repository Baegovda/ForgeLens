using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

/// <summary>
///   mtl1-49: in-game style colour scheme picker. Entries = the diffuse td <c>materials</c> variants of the previewed weapon
///   (what ApplyMaterialSuffix selects in game); names from strings_english.str (Korean in the tooltip); swatches from
///   armor_color_library.sso (<see cref="ColourSchemes"/>). Picking one re-binds the preview textures to the variant's
///   diffOverride / NM / spec; Export adds <c>material=</c> for the previewed tpl.
/// </summary>
public partial class MainWindow
{
  private List<ColourScheme> _colourSchemes = new();
  private string? _colourMaterial;
  private int _colourGeneration;
  private Dictionary<string, PreviewImage>? _baseAlbedo, _baseNormals, _baseSpecular;
  private readonly Dictionary<string, (Dictionary<string, PreviewImage> A, Dictionary<string, PreviewImage> N, Dictionary<string, PreviewImage> S)> _colourCache = new(StringComparer.OrdinalIgnoreCase);

  private void ResetColours()
  {
    _colourGeneration++;
    _colourSchemes = new();
    _colourMaterial = null;
    _colourCache.Clear();
    _baseAlbedo = _baseNormals = _baseSpecular = null;
    ColourPanel.IsVisible = false;
    ColourSwatches.Children.Clear();
  }

  private async Task LoadColoursAsync(ExportedMesh mesh, int index, string fileName, int previewGeneration)
  {
    _baseAlbedo = _albedo;
    _baseNormals = _normals;
    _baseSpecular = _specular;
    var template = AssetClassification.BaseName(fileName);
    if (_catalog is not { } catalog || _store is not { } store || !template.StartsWith("wpn_", StringComparison.OrdinalIgnoreCase))
      return;
    var generation = ++_colourGeneration;
    List<ColourScheme> schemes;
    try
    {
      schemes = await Task.Run(() => AssetExporter.ColourSchemesFor(catalog, store, template, mesh));
    }
    catch (Exception)
    {
      return;
    }

    if (generation != _colourGeneration || previewGeneration != _previewGeneration || schemes.Count == 0)
      return;
    _colourSchemes = schemes;
    BuildSwatches();
    ColourPanel.IsVisible = true;
    ShowColourName(null);
  }

  private void BuildSwatches()
  {
    ColourSwatches.Children.Clear();
    ColourSwatches.Children.Add(Swatch(null));
    foreach (var scheme in _colourSchemes)
      ColourSwatches.Children.Add(Swatch(scheme));
    ColourCount.Text = $"{_colourSchemes.Count + 1} VARIANTS";
    MarkSelected();
  }

  private Button Swatch(ColourScheme? scheme)
  {
    var grid = new Grid { Width = 40, Height = 40 };
    var ring = new Ellipse { Width = 40, Height = 40, Classes = { "ring" } };
    grid.Children.Add(ring);
    if (scheme is null)
    {
      // Default: the base look ("no colour" disc with a slash)
      grid.Children.Add(new Ellipse { Width = 30, Height = 30, Fill = new SolidColorBrush(Color.Parse("#2E3034")), Stroke = new SolidColorBrush(Color.Parse("#6A6D74")), StrokeThickness = 1.2 });
      grid.Children.Add(new Line { StartPoint = new Point(11, 29), EndPoint = new Point(29, 11), Stroke = new SolidColorBrush(Color.Parse("#9A9CA2")), StrokeThickness = 1.6 });
    }
    else
    {
      var c = Color.FromRgb(scheme.R, scheme.G, scheme.B);
      var metallic = scheme.SwatchSource.Contains("metalness", StringComparison.OrdinalIgnoreCase);
      // paint-pot sheen: lighter top-left, base colour, darker rim (stronger highlight on metallic colours)
      var fill = new RadialGradientBrush
      {
        GradientOrigin = new RelativePoint(0.32, 0.28, RelativeUnit.Relative),
        Center = new RelativePoint(0.4, 0.38, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
        GradientStops =
        {
          new GradientStop(Shade(c, metallic ? 1.65 : 1.3), 0),
          new GradientStop(c, metallic ? 0.45 : 0.55),
          new GradientStop(Shade(c, 0.55), 1),
        },
      };
      grid.Children.Add(new Ellipse { Width = 30, Height = 30, Fill = fill, Stroke = new SolidColorBrush(Color.Parse("#40FFFFFF")), StrokeThickness = 1 });
    }

    var tick = new Border
    {
      Classes = { "tick" }, Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = (IBrush?)this.FindResource("FL.Accent") ?? Brushes.Orange,
      HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -6),
    };
    var stack = new StackPanel { Spacing = 2 };
    stack.Children.Add(grid);
    stack.Children.Add(tick);
    var button = new Button { Classes = { "swatch" }, Content = stack, Tag = scheme?.Material ?? "", Focusable = false };
    ToolTip.SetTip(button, Tip(scheme));
    button.Click += (_, _) => _ = SelectColourAsync(scheme?.Material);
    button.PointerEntered += (_, _) => ShowColourName(scheme?.Material);
    button.PointerExited += (_, _) => ShowColourName(_colourMaterial);
    return button;
  }

  private static Color Shade(Color c, double f)
  {
    byte S(byte v) => (byte)Math.Clamp(f >= 1 ? v + (255 - v) * (f - 1) * 0.6 : v * f, 0, 255);
    return Color.FromRgb(S(c.R), S(c.G), S(c.B));
  }

  private static string Tip(ColourScheme? s)
  {
    if (s is null)
      return "Default\\nThe tpl's own textures (no material suffix).";
    var lines = new List<string> { s.Name };
    if (s.FullName.Length > 0) lines.Add(s.FullName);
    if (s.LocalName.Length > 0) lines.Add(s.LocalName);
    lines.Add($"material: {s.Material}   swatch {s.Hex}");
    lines.Add("name: " + s.NameKey);
    lines.Add("swatch: " + s.SwatchSource);
    return string.Join("\n", lines).Replace("\\n", "\n");
  }

  private void ShowColourName(string? material)
  {
    var s = _colourSchemes.FirstOrDefault(x => x.Material.Equals(material ?? "\0", StringComparison.OrdinalIgnoreCase));
    if (s is null)
    {
      ColourName.Text = "DEFAULT";
      ColourSub.Text = "Base textures of the tpl";
      return;
    }

    ColourName.Text = s.Name.ToUpperInvariant();
    var sub = new List<string>();
    if (s.FullName.Length > 0) sub.Add(s.FullName);
    if (s.LocalName.Length > 0) sub.Add(s.LocalName);
    if (sub.Count == 0) sub.Add("material " + s.Material);
    ColourSub.Text = string.Join("  ·  ", sub);
  }

  private void MarkSelected()
  {
    foreach (var child in ColourSwatches.Children.OfType<Button>())
      child.Classes.Set("selected", string.Equals((string?)child.Tag, _colourMaterial ?? "", StringComparison.OrdinalIgnoreCase));
  }

  private async Task SelectColourAsync(string? material)
  {
    if (string.IsNullOrEmpty(material))
      material = null;
    _colourMaterial = material;
    MarkSelected();
    ShowColourName(material);
    if (_mesh is not { } mesh || _catalog is not { } catalog || _store is not { } store || _baseAlbedo is null)
      return;
    if (material is null)
    {
      _albedo = _baseAlbedo;
      _normals = _baseNormals;
      _specular = _baseSpecular;
      DrawMesh();
      return;
    }

    var generation = _colourGeneration;
    if (!_colourCache.TryGetValue(material, out var maps))
    {
      var hint = _previewFileIndex;
      var (ba, bn, bs) = (_baseAlbedo, _baseNormals ?? new(), _baseSpecular ?? new());
      StatusText.Text = "Loading colour scheme " + material + "…";
      maps = await Task.Run(() => BuildColourMaps(catalog, store, mesh, material, hint, ba, bn, bs));
      if (generation != _colourGeneration)
        return;
      _colourCache[material] = maps;
      StatusText.Text = "";
    }

    if (_colourMaterial != material)
      return;
    _albedo = maps.A;
    _normals = maps.N;
    _specular = maps.S;
    DrawMesh();
  }

  /// <summary>Copies of the base maps with each part's diffuse / NM / spec key re-bound to the td materials.&lt;material&gt; texture.</summary>
  private static (Dictionary<string, PreviewImage> A, Dictionary<string, PreviewImage> N, Dictionary<string, PreviewImage> S) BuildColourMaps(
    GameCatalog catalog, ArchiveStore store, ExportedMesh mesh, string material, int hint,
    Dictionary<string, PreviewImage> baseAlbedo, Dictionary<string, PreviewImage> baseNormals, Dictionary<string, PreviewImage> baseSpecular)
  {
    var a = new Dictionary<string, PreviewImage>(baseAlbedo, StringComparer.OrdinalIgnoreCase);
    var n = new Dictionary<string, PreviewImage>(baseNormals, StringComparer.OrdinalIgnoreCase);
    var s = new Dictionary<string, PreviewImage>(baseSpecular, StringComparer.OrdinalIgnoreCase);
    var tdCache = new Dictionary<string, Dictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);
    var images = new Dictionary<string, PreviewImage?>(StringComparer.OrdinalIgnoreCase);
    PreviewImage? Image(string name, string channels)
    {
      var key = name + "|" + channels;
      if (!images.TryGetValue(key, out var img))
        images[key] = img = LoadPreviewImage(catalog, store, hint, name, channels);
      return img;
    }

    foreach (var part in mesh.Parts.Concat(mesh.Levels.SelectMany(l => l.Parts)))
    {
      if (string.IsNullOrWhiteSpace(part.Texture))
        continue;
      if (!tdCache.TryGetValue(part.Texture, out var ov))
        tdCache[part.Texture] = ov = AssetExporter.TdText(catalog, store, part.Texture) is { } text ? SkinMaterial.TdOverrides(text, material) : null;
      if (ov is null)
        continue;
      foreach (var (key, name) in ov)
      {
        switch (SkinMaterial.Role(key))
        {
          case "diffuse" when Image(name, "") is { } img:
            a[part.Texture] = img;
            break;
          case "normal" when !string.IsNullOrEmpty(part.Normal) && Image(name, "Normalmap(rgb)") is { } img:
            n[part.Normal!] = img;
            break;
          case "specular" when !string.IsNullOrEmpty(part.Specular) && Image(name, part.SpecularChannels ?? "") is { } img:
            s[part.Specular!] = img;
            break;
        }
      }
    }

    return (a, n, s);
  }

  private void ColourPanelPointer(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => e.Handled = true;

  private void ColourPanelWheel(object? sender, PointerWheelEventArgs e)
  {
    // Wheel over the picker steps through the schemes (like the in-game shoulder buttons)
    e.Handled = true;
    var order = new List<string?> { null };
    order.AddRange(_colourSchemes.Select(x => (string?)x.Material));
    var i = order.FindIndex(m => string.Equals(m, _colourMaterial, StringComparison.OrdinalIgnoreCase));
    var next = Math.Clamp(i + (e.Delta.Y < 0 ? 1 : -1), 0, order.Count - 1);
    if (next != i)
      _ = SelectColourAsync(order[next]);
  }

  /// <summary>
  ///   QA (screenshots without clicking): SM2_OPEN=catalog path (e.g. tpl/wpn_x.tpl/wpn_x.tpl) previews that file after the
  ///   remembered game folder loads; SM2_COLOUR=material then picks that colour scheme; SM2_YAW / SM2_PITCH set the orbit.
  /// </summary>
  private async Task OpenCatalogFileFromEnvironmentAsync()
  {
    if (Environment.GetEnvironmentVariable("SM2_OPEN") is not { Length: > 0 } path || _catalog is not { } catalog)
      return;
    if (!catalog.TryGetByPath(path.Replace('\\', '/'), out var index)
        && !(!path.Contains('/') && !path.Contains('\\') && catalog.FindByFileName(path) is { Count: > 0 } byName && (index = byName[0]) >= 0))
    {
      StatusText.Text = "SM2_OPEN: not in the catalog: " + path;
      return;
    }

    await PreviewFileAsync(index);
    await AfterOpenFromEnvironmentAsync();
  }

  private async Task AfterOpenFromEnvironmentAsync()
  {
    static float? F(string name) => float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    if (F("SM2_YAW") is { } yaw) _yaw = yaw;
    if (F("SM2_PITCH") is { } pitch) _pitch = pitch;
    if (Environment.GetEnvironmentVariable("SM2_COLOUR") is { Length: > 0 } colour)
    {
      for (var i = 0; i < 300 && _colourSchemes.Count == 0; i++)
        await Task.Delay(100);
      await SelectColourAsync(colour.Equals("default", StringComparison.OrdinalIgnoreCase) ? null : colour);
      if (Environment.GetEnvironmentVariable("SM2_COLOUR_HOVER") is { Length: > 0 } hover)
        ShowColourName(hover);
    }

    if (_mesh is not null)
      DrawMesh();
    await OpenClipFromEnvironmentAsync();
  }
}
