using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Export;

namespace LibSaber.SM2Browser;

/// <summary>
///   ui-45 Kitbash "Enemy / NPC" mode: an inst_constructor template (<see cref="InstConstructorLibrary"/>) → its model tpl →
///   one mesh template per body layer (or a .cls custom preset). The preview and the export both run
///   <see cref="NpcVariantFilter"/> on the same <c>npc=…</c> spec, so only the picked meshes show and export.
///   Also the armor mode's power fist pick (melee_skins_library power_fist_fake).
/// </summary>
public partial class MainWindow
{
  private InstConstructorLibrary? _npc;
  private Task<InstConstructorLibrary?>? _npcLoad;
  private MeleeCustomization? _melee;
  private NpcTemplate? _npcTemplate;
  private NpcPreset? _npcPreset;
  private readonly Dictionary<string, string?> _npcPicks = new(StringComparer.OrdinalIgnoreCase);

  private bool NpcMode => KitbashModeBox.SelectedIndex == 1;

  private sealed class NpcOption
  {
    public NpcVariant? Variant { get; init; }
    public double Chance { get; init; }
    public bool Missing { get; init; }
    public string? Text { get; init; }
    public override string ToString() => Text ?? Variant?.Key ?? "— empty —";
  }

  private void InitNpc()
  {
    _kitSuppress = true;
    KitbashModeBox.SelectedIndex = _settings.KitbashMode == "npc" ? 1 : 0;
    _kitSuppress = false;
    ShowNpcControls();
  }

  private void ShowNpcControls()
  {
    var npc = NpcMode;
    KitbashNpcGrid.IsVisible = npc;
    KitbashArmorGrid.IsVisible = !npc;
    KitbashArmorFilters.IsVisible = !npc;
    KitbashResetButton.Content = npc ? "Template default" : "Preset parts";
    ToolTip.SetTip(KitbashResetButton, npc ? "Most likely variant per layer (highest probability; the first on a tie)" : "Back to the regions of the chosen preset");
    ToolTip.SetTip(KitbashRandomButton, npc
      ? "One variant per layer, drawn with the template's probabilities (a variant without one weighs 1.0, the sslbin default; HEURISTIC)"
      : "One random part per slot from the listed options");
    KitbashIntro.Text = npc
      ? "Enemy / NPC parts from ssl/inst_constructor/inst_constructor_templates.sso (body layers → mesh templates) and .cls custom presets."
      : "Parts from ssl/body/customization/character_customization_library.sso (class regions + shared outfit parts).";
  }

  private void KitbashModeChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_kitSuppress || KitbashNpcGrid is null)
      return;
    ShowNpcControls();
    _kitIndex = -1;
    _kitTemplate = null;
    KitbashNotes.Text = "";
    SaveKitbash();
    if (_kitOpen)
      _ = EnsureKitbashAsync(loadModel: true);
  }

  /// <summary>Loads the NPC library (and the melee library for the power fist list) once per catalog.</summary>
  private async Task EnsureNpcAsync()
  {
    if (_catalog is not { } catalog || _store is not { } store)
      return;
    if (_npc is not null)
      return;
    KitbashModelState.Text = "Reading " + InstConstructorLibrary.TemplatesPath + " and the .cls presets…";
    _npcLoad ??= Task.Run(() =>
    {
      var lib = InstConstructorLibrary.Load(catalog, store);
      _melee = AssetExporter.MeleeLibrary(catalog, store);
      return lib;
    });
    var npc = await _npcLoad;
    if (!ReferenceEquals(catalog, _catalog))
      return;
    _npc = npc;
    if (npc is null)
    {
      KitbashTemplate.Text = "";
      KitbashModelState.Text = InstConstructorLibrary.TemplatesPath + " is not in this game folder, so there are no NPC templates.";
      return;
    }

    FillNpcTemplates();
  }

  private void FillNpcTemplates()
  {
    if (_npc is null)
      return;
    var list = _npc.Templates.ToList();
    _kitSuppress = true;
    KitbashNpcTemplateBox.ItemsSource = list;
    KitbashNpcTemplateBox.SelectedItem = list.FirstOrDefault(t => t.Name.Equals(_settings.KitbashNpcTemplate, StringComparison.OrdinalIgnoreCase))
                                         ?? list.FirstOrDefault(t => t.Name == "Cultist") ?? list.FirstOrDefault();
    _kitSuppress = false;
    _npcTemplate = KitbashNpcTemplateBox.SelectedItem as NpcTemplate;
    FillNpcPresets(_settings.KitbashNpcPreset);
    LoadNpcPicks();
  }

  private string? NpcModel => _npcTemplate?.Models.Select(m => m.Tpl).FirstOrDefault(t => _catalog?.FindByFileName(t + ".tpl").Count > 0)
                              ?? _npcTemplate?.Models.FirstOrDefault().Tpl;

  private void FillNpcPresets(string? keep)
  {
    var presets = new List<object> { "(template layers)" };
    if (_npc is not null)
      presets.AddRange(_npc.PresetsFor(NpcModel));
    _kitSuppress = true;
    KitbashNpcPresetBox.ItemsSource = presets;
    KitbashNpcPresetBox.IsEnabled = presets.Count > 1;
    var hit = presets.OfType<NpcPreset>().FirstOrDefault(p => p.Name.Equals(keep, StringComparison.OrdinalIgnoreCase));
    if (_npcTemplate is { PresetsOnly: true })
      hit ??= presets.OfType<NpcPreset>().FirstOrDefault(p => p.Objects.Count > 0);
    KitbashNpcPresetBox.SelectedItem = (object?)hit ?? presets[0];
    _kitSuppress = false;
    _npcPreset = hit;
  }

  /// <summary>Saved picks of this template, else the template default.</summary>
  private void LoadNpcPicks()
  {
    _npcPicks.Clear();
    if (_npcTemplate is null)
      return;
    var saved = _settings.KitbashNpcLayers?.GetValueOrDefault(_npcTemplate.Name);
    foreach (var (layer, key) in InstConstructorLibrary.DefaultPicks(_npcTemplate))
    {
      var pick = key;
      if (saved?.GetValueOrDefault(layer) is { } s)
        pick = s == KitbashLibrary.NoneUid ? null : s;
      _npcPicks[layer] = pick;
    }
  }

  private void KitbashNpcTemplateChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_kitSuppress)
      return;
    _npcTemplate = KitbashNpcTemplateBox.SelectedItem as NpcTemplate;
    FillNpcPresets(null);
    LoadNpcPicks();
    SaveKitbash();
    _ = LoadKitbashModelAsync();
  }

  private void KitbashNpcPresetChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_kitSuppress)
      return;
    _npcPreset = KitbashNpcPresetBox.SelectedItem as NpcPreset;
    if (_npcPreset is not null && _npcTemplate is not null && _mesh is { } mesh && ReferenceEquals(mesh, _kitBaselineMesh))
    {
      foreach (var (layer, key) in NpcVariantFilter.MatchPreset(mesh, _npcTemplate, _npcPreset))
        _npcPicks[layer] = key;
    }

    FillNpcLayers();
    ApplyKitbash();
  }

  private void FillNpcLayers()
  {
    KitbashSlotsPanel.Children.Clear();
    _kitBoxes.Clear();
    if (_npc is null || _npcTemplate is null)
      return;
    KitbashEmpty.IsVisible = _npcTemplate.Layers.Count == 0;
    KitbashEmpty.Text = _npcTemplate.PresetsOnly
      ? "No inst_constructor template uses this model; pick a custom preset above (its fixed object list shows)."
      : "This template has no body layers.";
    var off = _npc.DisabledLayers(_npcTemplate, _npcPicks);
    foreach (var layer in _npcTemplate.Layers)
    {
      var chosen = _npcPicks.GetValueOrDefault(layer.Name);
      var options = new List<NpcOption>();
      NpcOption? selected = null;
      foreach (var v in layer.Variants)
      {
        var missing = _kitObjects is { } objects && !v.IsEmpty && !v.Mesh!.Objects.Any(objects.Contains);
        var item = new NpcOption { Variant = v, Chance = layer.Chance(v), Missing = missing };
        options.Add(item);
        if (chosen is not null && v.Key.Equals(chosen, StringComparison.OrdinalIgnoreCase))
          selected = item;
      }

      NpcOption? noneItem = null;
      if (!layer.Variants.Any(v => v.IsEmpty))
      {
        noneItem = new NpcOption { Text = "— empty —" };
        options.Insert(0, noneItem); // the template has no Empty variant here: still allow leaving the layer out
      }

      var box = new ComboBox { ItemsSource = options, ItemTemplate = NpcOptionTemplate, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 420 };
      box.SelectedItem = selected ?? (chosen is null ? noneItem ?? options.FirstOrDefault(o => o.Variant?.IsEmpty == true) : null) ?? options.FirstOrDefault();
      if (selected is null && box.SelectedItem is NpcOption first)
        _npcPicks[layer.Name] = first.Variant?.Key;
      box.SelectionChanged += (_, _) =>
      {
        if (_kitSuppress || box.SelectedItem is not NpcOption option)
          return;
        _npcPicks[layer.Name] = option.Variant?.Key;
        if (_npcPreset is not null)
        {
          _npcPreset = null; // editing a layer leaves the fixed preset
          _kitSuppress = true;
          KitbashNpcPresetBox.SelectedIndex = 0;
          _kitSuppress = false;
        }

        FillNpcLayers();
        ApplyKitbash();
      };
      _kitBoxes[layer.Name] = box;
      var card = new Border { Classes = { "kbslot" }, Tag = layer.Name };
      var stack = new StackPanel { Spacing = 2 };
      var rule = layer.RequiredLayer is { } req ? "requires " + req : null;
      if (layer.DisabledByLayer is { } by)
        rule = (rule is null ? "" : rule + ", ") + "off when " + by + " is filled";
      var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
      head.Children.Add(new TextBlock { Text = SlotName(layer.Name), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = (IBrush)this.FindResource("FL.TextDim")! });
      if (off.Contains(layer.Name))
        head.Children.Add(Badge("off · " + rule, "#D0A040"));
      else if (rule is not null)
        head.Children.Add(Badge(rule, "#9A9CA2"));
      stack.Children.Add(head);
      stack.Children.Add(box);
      card.Child = stack;
      card.Opacity = off.Contains(layer.Name) ? 0.55 : 1;
      card.Classes.Set("empty", chosen is null || _npc.MeshTemplates.GetValueOrDefault(chosen) is { IsEmpty: true });
      ToolTip.SetTip(box, box.SelectedItem is NpcOption { Variant: { } sv } so ? NpcTip(sv, so) : "Nothing in this layer");
      KitbashSlotsPanel.Children.Add(card);
    }
  }

  private static string NpcTip(NpcVariant v, NpcOption o)
  {
    var lines = new List<string>
    {
      v.Key + "  (" + v.Link + ")",
      "Objects: " + (v.IsEmpty ? "none (empty)" : string.Join(", ", v.Mesh!.Objects)),
      v.Probability is { } p ? $"probability {p:0.###} → {o.Chance:P0} of this layer" : $"no probability (weight 1.0, sslbin default, HEURISTIC) → {o.Chance:P0}",
    };
    if (o.Missing)
      lines.Add("⚠ None of these objects is in the loaded tpl.");
    return string.Join("\n", lines);
  }

  private static readonly Avalonia.Controls.Templates.FuncDataTemplate<NpcOption> NpcOptionTemplate = new((option, _) =>
  {
    var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Background = Brushes.Transparent };
    if (option?.Variant is not { } v)
    {
      panel.Children.Add(new TextBlock { Text = "— empty —", Foreground = Brushes.Gray, FontStyle = FontStyle.Italic });
      return panel;
    }

    panel.Children.Add(new TextBlock { Text = v.IsEmpty ? "— empty —" : v.Key, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = option.Missing ? 0.5 : 1, FontStyle = v.IsEmpty ? FontStyle.Italic : FontStyle.Normal });
    panel.Children.Add(Badge(v.Probability is { } ? $"{option.Chance:P0}" : $"{option.Chance:P0}*", v.Probability is { } ? "#4FA3E0" : "#9A9CA2"));
    if (option.Missing)
      panel.Children.Add(Badge("⚠ not in tpl", "#D0574F"));
    ToolTip.SetTip(panel, NpcTip(v, option));
    return panel;
  });

  private void NpcDefaultClicked()
  {
    if (_npcTemplate is null)
      return;
    _npcPreset = null;
    _kitSuppress = true;
    KitbashNpcPresetBox.SelectedIndex = 0;
    _kitSuppress = false;
    foreach (var (layer, key) in InstConstructorLibrary.DefaultPicks(_npcTemplate))
      _npcPicks[layer] = key;
    FillNpcLayers();
    ApplyKitbash();
  }

  private void NpcRandomClicked()
  {
    if (_npcTemplate is null)
      return;
    _npcPreset = null;
    _kitSuppress = true;
    KitbashNpcPresetBox.SelectedIndex = 0;
    _kitSuppress = false;
    foreach (var (layer, key) in InstConstructorLibrary.RandomPicks(_npcTemplate, Random.Shared))
      _npcPicks[layer] = key;
    FillNpcLayers();
    ApplyKitbash();
  }

  private string? NpcSpecText()
    => _npcTemplate is null ? null
      : _npcTemplate.PresetsOnly && _npcPreset is null ? null
      : InstConstructorLibrary.Spec(_npcTemplate.Name, _npcPicks, _npcPreset?.Name);

  private void ApplyNpc(LibSaber.SM2.Assets.Meshes.ExportedMesh mesh, string? spec)
  {
    if (spec is null || _npc is null)
    {
      KitbashModelState.Text = _npcTemplate is { PresetsOnly: true } ? "Pick a custom preset; the whole model shows until then." : "";
      KitbashNotes.Text = "";
      return;
    }

    var result = NpcVariantFilter.Apply(mesh, _npc, VariantRequest.Parse(spec));
    var filled = _npcTemplate!.Layers.Count(l => result.Regions.GetValueOrDefault(l.Name) is { } r && _npc.MeshTemplates.GetValueOrDefault(r) is { IsEmpty: false });
    KitbashModelState.Text = (_npcPreset is not null ? $"Preset {_npcPreset.Name}: {_npcPreset.Objects.Count} objects" : $"{filled} of {_npcTemplate.Layers.Count} layers filled")
                             + $" · {result.VisibleParts} surfaces shown, {result.HiddenParts} hidden.";
    KitbashNotes.Text = string.Join("\n", result.Notes.Take(4));
  }

  private void FillFistBox()
  {
    var items = new List<string> { "— none —" };
    if (_melee is not null)
      items.AddRange(_melee.PowerFistSkins.Select(s => s.Key));
    _kitSuppress = true;
    KitbashFistBox.ItemsSource = items;
    KitbashFistBox.IsEnabled = items.Count > 1;
    KitbashFistBox.SelectedItem = items.FirstOrDefault(i => i.Equals(_settings.KitbashPowerFist, StringComparison.OrdinalIgnoreCase)) ?? items[0];
    _kitSuppress = false;
  }

  private string? KitbashFist => KitbashFistBox.SelectedIndex > 0 ? KitbashFistBox.SelectedItem as string : null;

  private void KitbashFistChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_kitSuppress)
      return;
    _settings.KitbashPowerFist = KitbashFist;
    ApplyKitbash();
  }

  private void SaveNpc()
  {
    _settings.KitbashMode = NpcMode ? "npc" : "armor";
    if (_npcTemplate is not null)
    {
      _settings.KitbashNpcTemplate = _npcTemplate.Name;
      _settings.KitbashNpcPreset = _npcPreset?.Name;
      _settings.KitbashNpcLayers ??= new(StringComparer.OrdinalIgnoreCase);
      if (!_npcTemplate.PresetsOnly)
        _settings.KitbashNpcLayers[_npcTemplate.Name] = _npcPicks.ToDictionary(p => p.Key, p => p.Value ?? KitbashLibrary.NoneUid, StringComparer.OrdinalIgnoreCase);
    }

    _settings.KitbashPowerFist = KitbashFist;
  }
}
