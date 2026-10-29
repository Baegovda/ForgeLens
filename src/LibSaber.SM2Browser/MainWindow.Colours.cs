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
///   mtl1-52: characters (any non-wpn_ tpl whose diffuse tds have a "&lt;variant&gt; CC" customization block) list the heraldry
///   chapter schemes (<see cref="ArmorColourSchemes"/>) and bake the per-layer tint / metalness into the preview maps
///   (<see cref="ArmorSchemeBake"/>); Export writes the UE material-instance parameters next to the export.
/// </summary>
public partial class MainWindow
{
  private List<ColourScheme> _colourSchemes = new();
  private string? _colourMaterial;
  private int _colourGeneration;
  private Dictionary<string, PreviewImage>? _baseAlbedo, _baseNormals, _baseSpecular;
  private readonly Dictionary<string, (Dictionary<string, PreviewImage> A, Dictionary<string, PreviewImage> N, Dictionary<string, PreviewImage> S)> _colourCache = new(StringComparer.OrdinalIgnoreCase);
  private readonly List<string> _colourCacheOrder = new();
  // mtl1-52 character mode
  private bool _armorMode;
  private List<ArmorScheme> _armorSchemes = new();
  private List<(MeshPart Part, CustomizationSlot Slot)> _ccParts = new();
  private readonly Dictionary<string, PreviewImage?> _ccImages = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Selectable keys in picker order (null = Default): td materials for weapons, scheme ids for characters.</summary>
  private List<string?> ColourKeys()
  {
    var keys = new List<string?> { null };
    keys.AddRange(_armorMode ? _armorSchemes.Select(x => (string?)x.Id) : _colourSchemes.Select(x => (string?)x.Material));
    return keys;
  }

  private void ResetColours()
  {
    _colourGeneration++;
    _colourSchemes = new();
    _colourMaterial = null;
    _colourCache.Clear();
    _colourCacheOrder.Clear();
    _armorMode = false;
    _armorSchemes = new();
    _ccParts = new();
    lock (_ccImages)
      _ccImages.Clear();
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
    if (_catalog is not { } catalog || _store is not { } store)
      return;
    if (!template.StartsWith("wpn_", StringComparison.OrdinalIgnoreCase))
    {
      await LoadArmorColoursAsync(catalog, store, mesh, previewGeneration);
      return;
    }

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

  /// <summary>mtl1-52: character picker. Shown when at least one part has a customization ("CC") td block.</summary>
  private async Task LoadArmorColoursAsync(GameCatalog catalog, ArchiveStore store, ExportedMesh mesh, int previewGeneration)
  {
    var generation = ++_colourGeneration;
    List<ArmorScheme> schemes;
    List<(MeshPart, CustomizationSlot)> parts;
    try
    {
      (schemes, parts) = await Task.Run(() => (AssetExporter.ArmorSchemesFor(catalog, store), AssetExporter.CustomizationParts(catalog, store, mesh)));
    }
    catch (Exception)
    {
      return;
    }

    if (generation != _colourGeneration || previewGeneration != _previewGeneration || schemes.Count == 0 || parts.Count == 0)
      return;
    _armorMode = true;
    _armorSchemes = schemes;
    _ccParts = parts;
    if (Environment.GetEnvironmentVariable("SM2_COLOUR_DUMP") is { Length: > 0 } qa)
      try
      {
        System.IO.Directory.CreateDirectory(qa);
        System.IO.File.WriteAllLines(System.IO.Path.Combine(qa, "parts.txt"),
          mesh.Parts.Concat(mesh.Levels.Take(1).SelectMany(l => l.Parts)).Select(x => $"{x.Name}\ttex={x.Texture}\tvar={x.Variant}\tspec={x.Specular}\tvis={x.Visible}\tbase={(_baseAlbedo?.ContainsKey(x.Texture ?? "") == true)}")
            .Append($"sameMesh={ReferenceEquals(mesh, _mesh)} drawParts={_mesh?.Parts.Count} drawTex={string.Join(",", (_mesh?.Parts ?? new()).Select(x => x.Texture).Distinct())}").Append("levels=" + mesh.Levels.Count + " baseKeys=" + string.Join(",", _baseAlbedo?.Keys ?? Enumerable.Empty<string>())));
      }
      catch (Exception) { }
    BuildSwatches();
    ColourPanel.IsVisible = true;
    ShowColourName(null);
  }

  private void BuildSwatches()
  {
    if (_armorMode)
    {
      ColourSwatches.Children.Clear();
      ColourSwatches.Children.Add(Swatch(null));
      foreach (var scheme in _armorSchemes)
        ColourSwatches.Children.Add(ArmorSwatch(scheme));
      {
        // PROVEN (td): only materials with a "<variant> CC" block take the scheme; the rest (e.g. campaign Titus *_02 armour)
        // carry their paint baked into the albedo in the game data too, so they stay as they are.
        var cc = _ccParts.Select(p => p.Part.Texture).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var all = (_mesh?.Parts ?? new()).Where(p => p.Visible && !string.IsNullOrEmpty(p.Texture) && !p.Texture!.StartsWith("__", StringComparison.Ordinal))
          .Select(p => p.Texture).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        ColourCount.Text = $"{_armorSchemes.Count} CHAPTERS · {cc} OF {Math.Max(all, cc)} MATERIALS CUSTOMIZABLE";
        ToolTip.SetTip(ColourCount, "Materials whose td has a customization (CC) block take the scheme through their CC mask (layer0..2 = primary/secondary/tertiary).\n" +
          "Other materials have no CC block in the game data (fixed paint, e.g. campaign Titus armour *_02) and keep their own colours.");
      }
      MarkSelected();
      return;
    }


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

  /// <summary>Chapter swatch: primary paint pot, secondary half, tertiary (trim / metal) rim.</summary>
  private Button ArmorSwatch(ArmorScheme scheme)
  {
    var p = scheme.Main[0];
    var s2 = scheme.Main[1];
    var t = scheme.Main[2];
    // metallic layers show their spec_metalnessTint (what the shader lerps to on metal)
    Color C(ArmorLayer l) => l.Metallic ? Color.FromRgb(l.MetalR, l.MetalG, l.MetalB) : Color.FromRgb(l.TintR, l.TintG, l.TintB);
    var grid = new Grid { Width = 40, Height = 40 };
    grid.Children.Add(new Ellipse { Width = 40, Height = 40, Classes = { "ring" } });
    var pc = C(p);
    grid.Children.Add(new Ellipse
    {
      Width = 30, Height = 30, StrokeThickness = 3, Stroke = new SolidColorBrush(Shade(C(t), t.Metallic ? 1.3 : 1.0)),
      Fill = new RadialGradientBrush
      {
        GradientOrigin = new RelativePoint(0.32, 0.28, RelativeUnit.Relative), Center = new RelativePoint(0.4, 0.38, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Shade(pc, 1.3), 0), new GradientStop(pc, 0.55), new GradientStop(Shade(pc, 0.55), 1) },
      },
    });
    if (s2.Colour != p.Colour)
    {
      // secondary as the lower-right half of the pot
      grid.Children.Add(new Avalonia.Controls.Shapes.Path
      {
        Data = Geometry.Parse("M 31.6,8.4 A 12,12 0 0 1 8.4,31.6 Z"), Fill = new SolidColorBrush(C(s2)), Opacity = 0.95,
      });
    }

    var tick = new Border
    {
      Classes = { "tick" }, Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = (IBrush?)this.FindResource("FL.Accent") ?? Brushes.Orange,
      HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -6),
    };
    var stack = new StackPanel { Spacing = 2 };
    stack.Children.Add(grid);
    stack.Children.Add(tick);
    var button = new Button { Classes = { "swatch" }, Content = stack, Tag = scheme.Id, Focusable = false };
    ToolTip.SetTip(button, ArmorTip(scheme));
    button.Click += (_, _) => _ = SelectColourAsync(scheme.Id);
    button.PointerEntered += (_, _) => ShowColourName(scheme.Id);
    button.PointerExited += (_, _) => ShowColourName(_colourMaterial);
    return button;
  }

  private static string ArmorTip(ArmorScheme s)
  {
    var lines = new List<string> { s.Name };
    if (s.LocalName.Length > 0) lines.Add(s.LocalName);
    lines.Add("scheme: " + s.Id + (s.Group.Length > 0 ? "   legion " + ArmorColourSchemes.Pretty(s.Group) : ""));
    lines.Add("name: " + s.NameKey);
    foreach (var l in s.Main)
      lines.Add($"{l.Slot}: {l.Name} {l.Hex}{(l.Metallic ? " metallic" : "")}  ({l.Source})");
    foreach (var (sec, ls) in s.Sections)
      lines.Add($"{sec}: {string.Join(" / ", ls.Select(l => l.Name))}");
    if (s.Lenses is { } lens)
      lines.Add("lenses: " + lens.Name + " (not baked)");
    lines.Add("mask r,g,b = primary, secondary, tertiary (INFERRED slot order)");
    return string.Join("\n", lines);
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
    if (_armorMode)
    {
      var a = _armorSchemes.FirstOrDefault(x => x.Id.Equals(material ?? "\0", StringComparison.OrdinalIgnoreCase));
      if (a is null)
      {
        ColourName.Text = "DEFAULT";
        ColourSub.Text = "Base textures of the tpl";
        return;
      }

      ColourName.Text = a.Name.ToUpperInvariant();
      var parts = new List<string> { string.Join(" / ", a.Main.Select(l => l.Name).Distinct()) };
      if (a.Sections.Count > 0) parts.Add(string.Join(", ", a.Sections.Select(p => p.Key.ToLowerInvariant() + " " + string.Join("/", p.Value.Select(l => l.Name).Distinct()))));
      if (a.LocalName.Length > 0) parts.Add(a.LocalName);
      if (a.NameKey.StartsWith("HEURISTIC")) parts.Add("name HEURISTIC");
      ColourSub.Text = string.Join("  ·  ", parts);
      return;
    }

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
    if (Environment.GetEnvironmentVariable("SM2_COLOUR_DUMP") is { Length: > 0 } qa)
      try { System.IO.Directory.CreateDirectory(qa); System.IO.File.AppendAllText(System.IO.Path.Combine(qa, "select.txt"), $"{DateTime.Now:HH:mm:ss} {material}\n{new System.Diagnostics.StackTrace(1, false).ToString().Split('\n').Take(4).Aggregate((x, y) => x + "\n" + y)}\n"); } catch (Exception) { }
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
      if (_armorMode)
      {
        if (_armorSchemes.FirstOrDefault(x => x.Id.Equals(material, StringComparison.OrdinalIgnoreCase)) is not { } scheme)
          return;
        var parts = _ccParts;
        var images = _ccImages;
        maps = await Task.Run(() => BuildArmorMaps(catalog, store, scheme, parts, hint, images, ba, bn, bs));
      }
      else
      {
        maps = await Task.Run(() => BuildColourMaps(catalog, store, mesh, material, hint, ba, bn, bs));
      }

      if (generation != _colourGeneration)
        return;
      // keep memory modest: at most 3 baked schemes
      _colourCache[material] = maps;
      _colourCacheOrder.Remove(material);
      _colourCacheOrder.Add(material);
      while (_colourCacheOrder.Count > 3)
      {
        _colourCache.Remove(_colourCacheOrder[0]);
        _colourCacheOrder.RemoveAt(0);
      }

      StatusText.Text = _armorMode ? $"Colour scheme {material}: {maps.A.Count(p => !ReferenceEquals(p.Value, ba.GetValueOrDefault(p.Key)))} albedo maps baked from the CC masks" : "";
    }

    if (_colourMaterial != material)
      return;
    _albedo = maps.A;
    _normals = maps.N;
    _specular = maps.S;
    DrawMesh();
  }

  /// <summary>
  ///   mtl1-52: copies of the base maps where every customization part's albedo is its neutral diffOverride (_a) tinted by the
  ///   scheme layers through the CC mask, and its spec carries the layers' metalness / roughness (<see cref="ArmorSchemeBake"/>).
  /// </summary>
  private static (Dictionary<string, PreviewImage> A, Dictionary<string, PreviewImage> N, Dictionary<string, PreviewImage> S) BuildArmorMaps(
    GameCatalog catalog, ArchiveStore store, ArmorScheme scheme, List<(MeshPart Part, CustomizationSlot Slot)> parts, int hint,
    Dictionary<string, PreviewImage?> images, Dictionary<string, PreviewImage> baseAlbedo, Dictionary<string, PreviewImage> baseNormals, Dictionary<string, PreviewImage> baseSpecular)
  {
    var a = new Dictionary<string, PreviewImage>(baseAlbedo, StringComparer.OrdinalIgnoreCase);
    var s = new Dictionary<string, PreviewImage>(baseSpecular, StringComparer.OrdinalIgnoreCase);
    PreviewImage? Image(string name)
    {
      lock (images)
      {
        if (!images.TryGetValue(name, out var img))
          images[name] = img = LoadPreviewImage(catalog, store, hint, name, "");
        return img;
      }
    }

    // QA: SM2_COLOUR_DUMP=<dir> writes what the bake used (log + baked / mask / neutral albedo PNGs)
    var dump = Environment.GetEnvironmentVariable("SM2_COLOUR_DUMP") is { Length: > 0 } dd ? dd : null;
    var log = new System.Text.StringBuilder();
    void Png(string name, PreviewImage? img)
    {
      if (dump is null || img is null) return;
      try { System.IO.Directory.CreateDirectory(dump); LibSaber.SM2.Assets.Textures.ImageWriters.WritePng(System.IO.Path.Combine(dump, name + ".png"), img.Bgra, img.Width, img.Height); } catch (Exception) { }
    }

    var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var doneSpec = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (part, slot) in parts)
    {
      if (part.Texture is not { } texture || !done.Add(texture) || Image(slot.Mask) is not { } mask)
        continue;
      var layers = ArmorColourSchemes.LayersFor(scheme, part.Name, texture);
      var spec = part.Specular is { } sn ? baseSpecular.GetValueOrDefault(sn) : null;
      var albedo = (slot.DiffOverride is { Length: > 0 } d ? Image(d) : null) ?? baseAlbedo.GetValueOrDefault(texture);
      if (albedo is null)
        continue;
      a[texture] = ArmorSchemeBake.Albedo(albedo, mask, spec, layers, slot.Enabled);
      if (dump is not null)
      {
        log.AppendLine($"{part.Name}\ttex={texture}\tbaseHit={baseAlbedo.ContainsKey(texture)}\tmask={slot.Mask} {mask.Width}x{mask.Height}\tdiff={slot.DiffOverride}\tspec={part.Specular}({spec?.Channels})\tlayers={string.Join(",", layers.Select(l => l.Colour + l.Hex))}");
        Png(texture + "__" + scheme.Id, a[texture]);
        Png(texture + "__mask", mask);
        Png(texture + "__a", albedo);
      }
      if (spec is not null && doneSpec.Add(part.Specular!))
        s[part.Specular!] = ArmorSchemeBake.Specular(spec, mask, layers, slot.Enabled);
    }

    if (dump is not null)
    {
      foreach (var p in parts.Select(x => x.Part).Distinct())
        log.AppendLine($"part {p.Name}\t{p.Texture}\tvisible={p.Visible}");
      try { System.IO.File.WriteAllText(System.IO.Path.Combine(dump, "bake_" + scheme.Id + ".txt"), log.ToString()); } catch (Exception) { }
    }

    return (a, new Dictionary<string, PreviewImage>(baseNormals, StringComparer.OrdinalIgnoreCase), s);
  }

  /// <summary>mtl1-52: UE material-instance parameters of the selected character scheme (null in weapon mode / Default).</summary>
  private string? ArmorSchemeExportJson()
  {
    if (!_armorMode || _colourMaterial is not { } id || _armorSchemes.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is not { } scheme)
      return null;
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    return ArmorSchemeBake.UeParams(scheme, _ccParts.Where(p => seen.Add(p.Part.Texture + "|" + p.Part.Name)).Select(p => (p.Part.Name, p.Part.Texture, p.Slot)));
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
    var order = ColourKeys();
    var i = order.FindIndex(m => string.Equals(m, _colourMaterial, StringComparison.OrdinalIgnoreCase));
    var next = Math.Clamp(i + (e.Delta.Y < 0 ? 1 : -1), 0, order.Count - 1);
    if (next != i)
    {
      _ = SelectColourAsync(order[next]);
      ScrollSwatchIntoView(order[next]);
    }
  }

  private void ScrollSwatchIntoView(string? key)
  {
    foreach (var child in ColourSwatches.Children.OfType<Button>())
      if (string.Equals((string?)child.Tag, key ?? "", StringComparison.OrdinalIgnoreCase))
        child.BringIntoView();
  }

  private void ColourStep(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    e.Handled = true;
    var order = ColourKeys();
    var i = order.FindIndex(m => string.Equals(m, _colourMaterial, StringComparison.OrdinalIgnoreCase));
    var next = Math.Clamp(i + (Equals((sender as Button)?.Tag, "next") ? 1 : -1), 0, order.Count - 1);
    if (next != i)
    {
      _ = SelectColourAsync(order[next]);
      ScrollSwatchIntoView(order[next]);
    }
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
      for (var i = 0; i < 300 && ColourKeys().Count <= 1; i++)
        await Task.Delay(100);
      var key = colour.Equals("default", StringComparison.OrdinalIgnoreCase) ? null : colour;
      await SelectColourAsync(key);
      ScrollSwatchIntoView(key);
      if (Environment.GetEnvironmentVariable("SM2_COLOUR_HOVER") is { Length: > 0 } hover)
        ShowColourName(hover);
    }

    if (_mesh is not null)
      DrawMesh();
    await OpenClipFromEnvironmentAsync();
  }
}
