using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

/// <summary>
///   ui-44 Kitbasher (right column, "Kitbash" tab): faction → class → preset, then one part per slot from
///   <c>character_customization_library.sso</c> (<see cref="KitbashLibrary"/>). The class tpl loads in the viewer and the
///   combination is applied with <see cref="ArmorVariant"/> (the exporter's filter), so the preview shows exactly what
///   "Export combination…" writes.
/// </summary>
public partial class MainWindow
{
  private KitbashLibrary? _kit;
  private Task<KitbashLibrary?>? _kitLoad;
  private bool _kitOpen;
  private bool _kitSuppress;
  private KitbashClass? _kitClass;
  private KitbashPreset? _kitPreset;
  private int _kitIndex = -1;
  private string? _kitTemplate;
  private readonly Dictionary<string, string?> _kitSlots = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, ComboBox> _kitBoxes = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>Visibility of every surface right after the tpl loaded (cloth SIM shells already off), restored before each apply.</summary>
  private List<(MeshPart Part, bool Visible, string? Reason)>? _kitBaseline;
  private ExportedMesh? _kitBaselineMesh;
  private HashSet<string>? _kitObjects;
  private double _kitSavedColumn = 240;

  private sealed class KitOption
  {
    public KitbashPart? Part { get; init; }
    public bool Missing { get; init; }
  }

  private void InitKitbash()
  {
    KitbashClassOnlyBox.IsChecked = !_settings.KitbashAllClasses;
    KitbashMissingBox.IsChecked = _settings.KitbashShowMissing;
    if (_settings.KitbashSlots is { } slots)
    {
      foreach (var (k, v) in slots)
        _kitSlots[k] = v == KitbashLibrary.NoneUid ? null : v;
    }

    InitNpc();
    KitbashModelState.Text = "Open the game folder to read the customization library.";
    if (_settings.KitbashOpen)
      ShowKitbashTab(true, save: false);
  }

  private void MeshesTabClicked(object? sender, RoutedEventArgs e) => ShowKitbashTab(false);
  private void KitbashTabClicked(object? sender, RoutedEventArgs e) => ShowKitbashTab(true);

  private void ShowKitbashTab(bool open, bool save = true)
  {
    _kitOpen = open;
    KitbashPanel.IsVisible = open;
    MeshList.IsVisible = !open;
    MeshesTab.Classes.Set("active", !open);
    KitbashTab.Classes.Set("active", open);
    var column = ViewerColumns.ColumnDefinitions[2];
    if (open)
    {
      _kitSavedColumn = column.ActualWidth > 0 ? column.ActualWidth : column.Width.Value;
      if (column.Width.Value < 330)
        column.Width = new GridLength(330);
    }
    else if (_kitSavedColumn > 0 && _kitSavedColumn < 330)
    {
      column.Width = new GridLength(_kitSavedColumn);
    }

    if (save)
      SaveKitbash();
    if (open)
      _ = EnsureKitbashAsync(loadModel: true);
  }

  private void KitbashCatalogChanged()
  {
    _kit = null;
    _kitLoad = null;
    _npc = null;
    _npcLoad = null;
    _melee = null;
    _kitIndex = -1;
    _kitTemplate = null;
    if (_kitOpen)
      _ = EnsureKitbashAsync(loadModel: true);
  }

  private async Task EnsureKitbashAsync(bool loadModel)
  {
    if (_catalog is not { } catalog || _store is not { } store)
      return;
    if (NpcMode)
    {
      await EnsureNpcAsync();
      if (_npc is not null && loadModel)
        await LoadKitbashModelAsync();
      return;
    }

    if (_kit is null)
    {
      KitbashModelState.Text = "Reading " + CustomizationLibrary.LibraryPath + "…";
      _kitLoad ??= Task.Run(() => CustomizationLibrary.Load(catalog, store) is { } lib ? KitbashLibrary.From(lib) : null);
      var kit = await _kitLoad;
      if (!ReferenceEquals(catalog, _catalog))
        return;
      _kit = kit;
      if (kit is null)
      {
        KitbashTemplate.Text = "";
        KitbashModelState.Text = CustomizationLibrary.LibraryPath + " is not in this game folder, so there are no parts to pick.";
        return;
      }

      if (_melee is null)
        _melee = await Task.Run(() => AssetExporter.MeleeLibrary(catalog, store));
      FillFistBox();
      FillKitbashTeams();
    }

    if (loadModel)
      await LoadKitbashModelAsync();
  }

  private void FillKitbashTeams()
  {
    if (_kit is null)
      return;
    _kitSuppress = true;
    KitbashTeamBox.ItemsSource = _kit.Teams.ToList();
    KitbashTeamBox.SelectedItem = _kit.Teams.FirstOrDefault(t => t.Equals(_settings.KitbashTeam, StringComparison.OrdinalIgnoreCase)) ?? _kit.Teams.FirstOrDefault();
    _kitSuppress = false;
    FillKitbashClasses(keepSlots: true);
  }

  private static bool Pickable(KitbashClass c) => c.HasParts || c.Presets.Count > 0;

  private void FillKitbashClasses(bool keepSlots)
  {
    if (_kit is null || KitbashTeamBox.SelectedItem is not string team)
      return;
    var classes = _kit.Classes.GetValueOrDefault(team)?.Where(Pickable).ToList() ?? new();
    _kitSuppress = true;
    KitbashClassBox.ItemsSource = classes;
    KitbashClassBox.SelectedItem = classes.FirstOrDefault(c => c.Name.Equals(_settings.KitbashClass, StringComparison.OrdinalIgnoreCase))
                                   ?? classes.FirstOrDefault(c => c.Name.EndsWith("TANK", StringComparison.OrdinalIgnoreCase))
                                   ?? classes.FirstOrDefault();
    _kitSuppress = false;
    FillKitbashPresets(keepSlots);
  }

  private void FillKitbashPresets(bool keepSlots)
  {
    _kitClass = KitbashClassBox.SelectedItem as KitbashClass;
    var presets = _kitClass?.Presets ?? new List<KitbashPreset>();
    _kitSuppress = true;
    KitbashPresetBox.ItemsSource = presets;
    KitbashPresetBox.IsEnabled = presets.Count > 0;
    KitbashPresetBox.SelectedItem = presets.FirstOrDefault(p => p.Name.Equals(_settings.KitbashPreset, StringComparison.OrdinalIgnoreCase)) ?? presets.FirstOrDefault();
    _kitSuppress = false;
    _kitPreset = KitbashPresetBox.SelectedItem as KitbashPreset;
    if (!keepSlots || _kitSlots.Count == 0)
      ResetKitbashSlots();
    FillKitbashSlots();
  }

  /// <summary>The preset whose regions fill the slots: the chosen one, or (a champion preset shown on the constructor because its
  /// own tpl is missing) the first preset that lists regions.</summary>
  private KitbashPreset? RegionPreset
    => _kitPreset is { Regions.Count: > 0 } ? _kitPreset : _kitClass?.Presets.FirstOrDefault(p => p.Regions.Count > 0) ?? _kitPreset;

  private void ResetKitbashSlots()
  {
    _kitSlots.Clear();
    if (_kit is null || _kitClass is null)
      return;
    var preset = RegionPreset;
    foreach (var slot in _kit.Slots(_kitClass, classOnly: false))
      _kitSlots[slot] = preset?.Regions.GetValueOrDefault(slot);
  }

  private bool KitChampion => _kitPreset is { IsChampion: true } && _kitTemplate is { } t && !CustomizationLibrary.IsConstructorTemplate(t);

  private void FillKitbashSlots()
  {
    if (NpcMode)
    {
      FillNpcLayers();
      return;
    }

    KitbashSlotsPanel.Children.Clear();
    _kitBoxes.Clear();
    if (_kit is null || _kitClass is null)
      return;
    var classOnly = KitbashClassOnlyBox.IsChecked == true;
    var showMissing = KitbashMissingBox.IsChecked == true;
    var champion = KitChampion;
    KitbashEmpty.IsVisible = champion || !_kitClass.HasParts && _kit.Slots(_kitClass, classOnly).Count == 0;
    KitbashEmpty.Text = champion
      ? $"{_kitPreset!.Label}: the champion armor is its own model ({_kitTemplate}). It loads and exports complete; there are no slots to pick."
      : "This class has no body sections in the library.";
    if (champion)
      return;

    foreach (var slot in _kit.Slots(_kitClass, classOnly))
    {
      var chosen = _kitSlots.GetValueOrDefault(slot);
      var noneItem = new KitOption();
      var options = new List<KitOption> { noneItem };
      KitOption? selected = chosen is null ? noneItem : null;
      foreach (var part in _kit.Options(_kitClass, slot, classOnly))
      {
        var missing = _kitObjects is { } objects && part.Meshes.Count > 0 && !part.Meshes.Any(objects.Contains);
        var isChosen = chosen is not null && part.Uid.Equals(chosen, StringComparison.OrdinalIgnoreCase);
        if (missing && !showMissing && !isChosen)
          continue;
        var item = new KitOption { Part = part, Missing = missing };
        options.Add(item);
        if (isChosen)
          selected = item;
      }

      var box = new ComboBox { ItemsSource = options, ItemTemplate = KitOptionTemplate, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 420 };
      box.SelectedItem = selected ?? noneItem;
      if (selected is null && chosen is not null)
        _kitSlots[slot] = null; // a saved uid the library no longer has
      box.SelectionChanged += (_, _) =>
      {
        if (_kitSuppress || box.SelectedItem is not KitOption option)
          return;
        _kitSlots[slot] = option.Part?.Uid;
        UpdateSlotCard(box, slot);
        ApplyKitbash();
      };
      _kitBoxes[slot] = box;
      var card = new Border { Classes = { "kbslot" }, Tag = slot };
      var stack = new StackPanel { Spacing = 2 };
      stack.Children.Add(new TextBlock { Text = SlotName(slot), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = (IBrush)this.FindResource("FL.TextDim")! });
      stack.Children.Add(box);
      card.Child = stack;
      KitbashSlotsPanel.Children.Add(card);
      UpdateSlotCard(box, slot);
    }
  }

  private void UpdateSlotCard(ComboBox box, string slot)
  {
    if (box.Parent?.Parent is not Border card)
      return;
    var uid = _kitSlots.GetValueOrDefault(slot);
    var preset = RegionPreset?.Regions.GetValueOrDefault(slot);
    card.Classes.Set("changed", !string.Equals(uid, preset, StringComparison.OrdinalIgnoreCase));
    card.Classes.Set("empty", uid is null);
    ToolTip.SetTip(box, box.SelectedItem is KitOption { Part: { } part } option ? PartTip(part, option.Missing) : "Empty slot");
  }

  private static string SlotName(string slot)
  {
    var words = slot.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
    return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..])).ToUpperInvariant();
  }

  private static readonly Avalonia.Controls.Templates.FuncDataTemplate<KitOption> KitOptionTemplate = new((option, _) =>
  {
    if (option?.Part is not { } part)
    {
      var none = new TextBlock { Text = "— empty —", Foreground = Brushes.Gray, FontStyle = FontStyle.Italic, Background = Brushes.Transparent };
      ToolTip.SetTip(none, "Nothing in this slot: every object of the slot's regions is hidden.");
      return none;
    }

    var label = KitPartLabel(part, option.Missing);
    ToolTip.SetTip(label, PartTip(part, option.Missing));
    return label;
  });

  private static Panel KitPartLabel(KitbashPart part, bool missing)
  {
    var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Background = Brushes.Transparent };
    panel.Children.Add(new TextBlock { Text = part.Uid, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = missing ? 0.5 : 1 });
    if (part.Shared)
      panel.Children.Add(Badge("shared", "#4FA3E0"));
    if (part.Rarity is { Length: > 0 } rarity && !rarity.Equals("COMMON", StringComparison.OrdinalIgnoreCase))
      panel.Children.Add(Badge(rarity.ToLowerInvariant(), "#C77BE8"));
    if (part.Hidden)
      panel.Children.Add(Badge("hidden", "#9A9CA2"));
    if (missing)
      panel.Children.Add(Badge("⚠ not in tpl", "#D0574F"));
    return panel;
  }

  private static Border Badge(string text, string color)
  {
    var c = Color.Parse(color);
    return new Border
    {
      CornerRadius = new CornerRadius(3),
      Padding = new Thickness(4, 0),
      Background = new SolidColorBrush(Color.FromArgb(0x40, c.R, c.G, c.B)),
      VerticalAlignment = VerticalAlignment.Center,
      Child = new TextBlock { Text = text, FontSize = 10, Foreground = new SolidColorBrush(c) },
    };
  }

  private static string PartTip(KitbashPart part, bool missing)
  {
    var lines = new List<string>
    {
      part.Uid + (part.Shared ? "  (sharedOutfitParts)" : "  (class bodySections)"),
      "Objects: " + (part.Meshes.Count == 0 ? "none" : string.Join(", ", part.Meshes)),
    };
    if (part.Rarity is { Length: > 0 })
      lines.Add("Rarity: " + part.Rarity);
    if (part.Shared)
      lines.Add(part.ForAllCharacters || part.AppearanceUids.Count == 0 ? "For all characters" : "For: " + string.Join(", ", part.AppearanceUids));
    if (part.Hidden)
      lines.Add("isHidden in the library (not offered in the game's customizer; Random skips it)");
    if (missing)
      lines.Add("⚠ None of these objects is in the loaded tpl; picking it leaves the slot empty here.");
    return string.Join("\n", lines);
  }

  private void KitbashTeamChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_kitSuppress)
      return;
    _settings.KitbashClass = null;
    FillKitbashClasses(keepSlots: false);
    SaveKitbash();
    _ = LoadKitbashModelAsync();
  }

  private void KitbashClassChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_kitSuppress)
      return;
    _settings.KitbashPreset = null;
    FillKitbashPresets(keepSlots: false);
    SaveKitbash();
    _ = LoadKitbashModelAsync();
  }

  private void KitbashPresetChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_kitSuppress)
      return;
    var wasChampion = _kitPreset?.IsChampion == true;
    _kitPreset = KitbashPresetBox.SelectedItem as KitbashPreset;
    ResetKitbashSlots();
    SaveKitbash();
    if (wasChampion || _kitPreset?.IsChampion == true)
    {
      _ = LoadKitbashModelAsync(); // champion presets are their own tpl
      return;
    }

    FillKitbashSlots();
    ApplyKitbash();
  }

  private void KitbashFilterChanged(object? sender, RoutedEventArgs e)
  {
    if (NpcMode)
      return;
    FillKitbashSlots();
    SaveKitbash();
  }

  private void KitbashResetClicked(object? sender, RoutedEventArgs e)
  {
    if (NpcMode)
    {
      NpcDefaultClicked();
      return;
    }

    ResetKitbashSlots();
    FillKitbashSlots();
    ApplyKitbash();
  }

  private void KitbashRandomClicked(object? sender, RoutedEventArgs e)
  {
    if (NpcMode)
    {
      NpcRandomClicked();
      return;
    }

    _kitSuppress = true;
    foreach (var (slot, box) in _kitBoxes)
    {
      var parts = box.Items.OfType<KitOption>().Where(i => i is { Part.Hidden: false, Missing: false }).ToList();
      if (parts.Count == 0)
        continue;
      var pick = parts[Random.Shared.Next(parts.Count)];
      box.SelectedItem = pick;
      _kitSlots[slot] = pick.Part!.Uid;
      UpdateSlotCard(box, slot);
    }

    _kitSuppress = false;
    ApplyKitbash();
  }

  /// <summary>Finds the class tpl (<see cref="KitbashLibrary.TemplateCandidates"/>) and shows it; <see cref="KitbashModelLoaded"/> applies the parts.</summary>
  private async Task LoadKitbashModelAsync()
  {
    if (_catalog is not { } catalog)
      return;
    List<string> candidates;
    string label;
    if (NpcMode)
    {
      if (_npc is null || _npcTemplate is null)
        return;
      candidates = _npcTemplate.Models.Select(m => m.Tpl).ToList();
      label = _npcTemplate.Name;
    }
    else
    {
      if (_kit is null || _kitClass is null || KitbashTeamBox.SelectedItem is not string team)
        return;
      candidates = KitbashLibrary.TemplateCandidates(team, _kitClass.Name, _kitPreset).ToList();
      label = _kitClass.Name;
    }

    var found = -1;
    string? template = null;
    foreach (var name in candidates)
    {
      var hits = catalog.FindByFileName(name + ".tpl");
      if (hits.Count > 0)
      {
        found = hits[0];
        template = name;
        break;
      }
    }

    _kitIndex = found;
    _kitTemplate = template;
    if (found < 0)
    {
      KitbashTemplate.Text = "No tpl for " + label;
      KitbashModelState.Text = candidates.Count == 0
        ? "No .cls fixedTemplateId names this template and no tpl matches its name (cc_<name>, cc_constructor_<name>, cc_<name>_constructor)."
        : "Looked for " + string.Join(", ", candidates.Select(c => c + ".tpl")) + ". None is in this game folder.";
      _kitObjects = null;
      FillKitbashSlots();
      UpdateKitbashSpec(null);
      return;
    }

    var source = NpcMode ? _npcTemplate!.Models.FirstOrDefault(m => m.Tpl == template).Source : null;
    KitbashTemplate.Text = template + ".tpl" + (source is not null ? "  · " + source : "") + (!NpcMode && _kitPreset is { IsChampion: true } && CustomizationLibrary.IsConstructorTemplate(template!) ? "  (champion tpl not found: " + candidates[0] + ")" : "");
    KitbashModelState.Text = "Loading the model…";
    if (_previewFileIndex == found && _mesh is not null && ReferenceEquals(_mesh, _kitBaselineMesh))
    {
      FillKitbashSlots();
      ApplyKitbash();
      return;
    }

    await PreviewFileAsync(found);
  }

  /// <summary>Called by the model preview right after a tpl loaded: remembers its default visibility and applies the kitbash when it is the kitbash tpl.</summary>
  private void KitbashModelLoaded(ExportedMesh mesh, int index)
  {
    _kitBaselineMesh = mesh;
    _kitBaseline = mesh.Parts.Concat(mesh.Levels.SelectMany(l => l.Parts)).Select(p => (p, p.Visible, p.HideReason)).ToList();
    if (!_kitOpen || index != _kitIndex)
    {
      if (_kitOpen && _kitIndex >= 0)
        KitbashModelState.Text = "The viewer shows another file. Pick a class or part to load " + _kitTemplate + " again.";
      return;
    }

    _kitObjects = new HashSet<string>(mesh.Bones.Select(b => b.SourceName.Length > 0 ? b.SourceName : b.Name), StringComparer.OrdinalIgnoreCase);
    if (NpcMode && _npcPreset is not null && _npcTemplate is not null)
    {
      foreach (var (layer, key) in NpcVariantFilter.MatchPreset(mesh, _npcTemplate, _npcPreset))
        _npcPicks[layer] = key;
    }
    FillKitbashSlots();
    ApplyKitbash(draw: false);
  }

  private string? KitbashSpecText()
  {
    if (NpcMode)
      return NpcSpecText();
    if (_kitClass is null || KitbashTeamBox.SelectedItem is not string team || KitChampion)
      return null;
    var slots = _kitBoxes.Keys.ToDictionary(s => s, s => _kitSlots.GetValueOrDefault(s), StringComparer.OrdinalIgnoreCase);
    var spec = KitbashLibrary.Spec(team, _kitClass.Name, RegionPreset, slots);
    return KitbashFist is { } fist ? spec + ",powerfist=" + fist : spec;
  }

  private void ApplyKitbash(bool draw = true)
  {
    SaveKitbash();
    var spec = KitbashSpecText();
    UpdateKitbashSpec(spec);
    if ((NpcMode ? _npc is null : _kit is null) || _mesh is not { } mesh || _previewFileIndex != _kitIndex || _kitIndex < 0)
    {
      if (_kitIndex >= 0 && _previewFileIndex != _kitIndex && _kitOpen)
        _ = LoadKitbashModelAsync();
      return;
    }

    if (ReferenceEquals(mesh, _kitBaselineMesh) && _kitBaseline is { } baseline)
    {
      foreach (var (part, visible, reason) in baseline)
      {
        part.Visible = visible;
        part.HideReason = reason;
      }
    }

    if (NpcMode)
    {
      ApplyNpc(mesh, spec);
    }
    else if (spec is null || KitbashTeamBox.SelectedItem is not string team || _kitClass is null)
    {
      KitbashModelState.Text = KitChampion ? "Champion model, complete." : "";
      KitbashNotes.Text = "";
    }
    else
    {
      var request = VariantRequest.Parse(spec);
      var fistNote = _melee?.AddPowerFist(request, _kitTemplate ?? mesh.Name);
      var result = ArmorVariant.Apply(mesh, _kit.Library, team, _kitClass.Name, request);
      if (fistNote is not null)
        result.Notes.Insert(0, fistNote);
      var picked = _kitBoxes.Keys.Count(s => _kitSlots.GetValueOrDefault(s) is not null);
      KitbashModelState.Text = $"{picked} of {_kitBoxes.Count} slots filled · {result.VisibleParts} surfaces shown, {result.HiddenParts} hidden of {result.CustomizableObjects:N0} customizable objects.";
      var notes = result.Notes.Where(n => !n.Contains(" is not in " + mesh.Name, StringComparison.Ordinal)).ToList();
      var missing = result.Notes.Count - notes.Count;
      if (missing > 0)
        notes.Insert(0, $"{missing} object(s) of the picked parts are not in {mesh.Name} (marked ⚠ in the lists).");
      KitbashNotes.Text = string.Join("\n", notes.Take(4));
    }

    FillMeshes(mesh);
    if (draw)
      DrawMesh(false);
  }

  private void UpdateKitbashSpec(string? spec)
  {
    KitbashSpec.Text = spec is null ? "" : "--variant \"" + spec + "\"";
    KitbashExportButton.IsEnabled = _kitIndex >= 0 && !_exporting;
  }

  private void SaveKitbash()
  {
    _settings.KitbashOpen = _kitOpen;
    if (KitbashTeamBox.SelectedItem is string team)
      _settings.KitbashTeam = team;
    if (_kitClass is not null)
      _settings.KitbashClass = _kitClass.Name;
    if (_kitPreset is not null)
      _settings.KitbashPreset = _kitPreset.Name;
    _settings.KitbashSlots = _kitSlots.ToDictionary(p => p.Key, p => p.Value ?? KitbashLibrary.NoneUid, StringComparer.OrdinalIgnoreCase);
    _settings.KitbashAllClasses = KitbashClassOnlyBox.IsChecked != true;
    _settings.KitbashShowMissing = KitbashMissingBox.IsChecked == true;
    SaveNpc();
    try { _settings.Save(); } catch { /* best effort */ }
  }

  private async void KitbashExportClicked(object? sender, RoutedEventArgs e)
  {
    if (_catalog is not { } catalog || _store is not { } store || _kitIndex < 0 || _exporting)
      return;
    var index = _kitIndex;
    var spec = KitbashSpecText();
    var summary = new List<string>();
    if (NpcMode)
    {
      summary.Add($"{_kitTemplate}.tpl · NPC {_npcTemplate?.Name} · {_npcPreset?.Name ?? "template layers"}");
      if (_npcPreset is not null)
        summary.Add("Objects: " + string.Join(", ", _npcPreset.Objects));
      else if (spec is not null && _npc is not null && _npcTemplate is not null)
      {
        var off = _npc.DisabledLayers(_npcTemplate, _npcPicks);
        summary.AddRange(_npcTemplate.Layers.Select(l => $"{SlotName(l.Name)}: {(off.Contains(l.Name) ? "(off)" : _npcPicks.GetValueOrDefault(l.Name) ?? "— empty —")}"));
      }
    }
    else
    {
      summary.Add($"{_kitTemplate}.tpl · {KitbashTeamBox.SelectedItem}/{_kitClass?.Name} · {_kitPreset?.Label ?? "no preset"}");
      if (spec is not null)
        summary.AddRange(_kitBoxes.Keys.Select(s => $"{SlotName(s)}: {_kitSlots.GetValueOrDefault(s) ?? "— empty —"}"));
      if (KitbashFist is { } fist)
        summary.Add("Power fist: " + fist);
    }

    var window = new ExportWindow();
    var choice = await window.Choose(this, _settings.LastExportFolder, fixedVariant: spec ?? "all", fixedSummary: string.Join("\n", summary));
    if (choice is null)
      return;
    _settings.LastExportFolder = choice.Folder;
    _settings.Save();
    if (_mesh is not null && _previewFileIndex == index)
      choice.Settings.PartVisibility = new Dictionary<int, MeshVisibility> { [index] = MeshVisibility.Capture(_mesh) };

    _exporting = true;
    UpdateExportButton();
    KitbashExportButton.IsEnabled = false;
    try
    {
      var progress = new Progress<string>(path => StatusText.Text = "Exporting " + path);
      var previewClip = _clip is { HasCurves: true } ? _clipIndex : -1;
      var notes = new List<string>();
      var report = await Task.Run(() =>
      {
        var files = ExportPlan.Apply(catalog, store, new[] { index }, choice.Settings, choice.Clips, previewClip, notes);
        return AssetExporter.Export(catalog, store, files, choice.Folder, choice.Settings, progress);
      });
      report.Messages.InsertRange(0, notes);
      StatusText.Text = $"Kitbash export: {report.Written} files, skipped {report.Skipped}. {string.Join(" ", report.Messages.Take(3))}";
    }
    catch (Exception ex)
    {
      StatusText.Text = ex.Message;
    }
    finally
    {
      _exporting = false;
      UpdateExportButton();
      KitbashExportButton.IsEnabled = _kitIndex >= 0;
    }
  }
}
