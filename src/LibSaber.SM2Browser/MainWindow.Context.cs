using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Context;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2Browser.Effects;

namespace LibSaber.SM2Browser;

/// <summary>
///   mtl1-49 context playback, both directions on one rule set (<see cref="ContextResolver"/>):
///   <list type="bullet">
///     <item>Effect → model: "With model + animation" opens the character that owns the effect, the clip whose
///       morpheme event starts it, the held weapon, and plays the effect on its socket at the event time.</item>
///     <item>Model + clip → weapon + effects: every clip played on a model gets the held weapon (picker when several
///       fit) and the effects its events start (blueprints owned by the character / weapon; Shoot → muzzle).</item>
///   </list>
///   "Show props/weapons" / "Show effects" turn either part off (model + animation only).
/// </summary>
public partial class MainWindow
{
  private ContextResolver? _ctx;
  private Task<ContextResolver?>? _ctxTask;
  private bool _ctxPlayback;
  private bool _ctxOpening;
  private bool _suppressCtx;
  private int _ctxGeneration;
  private string? _ctxEffect;
  private EffectContext? _ctxCurrent;
  private List<EffectContext> _ctxCandidates = [];
  private ExportedMesh? _ctxBase;
  private List<WeaponChoice> _ctxWeapons = [];
  private string? _ctxWeaponPick;
  private string? _ctxSlotPick;
  private int _ctxPendingClip = -1;
  private ExportedMesh? _ctxProps, _ctxPropsBase;
  private string? _ctxPropsKey, _ctxPropsRoot;
  private readonly Dictionary<string, (ExportedMesh Mesh, PreviewTextures Textures)?> _ctxWeaponMeshes = new(StringComparer.OrdinalIgnoreCase);

  private void InitContext()
  {
    AnimBar.PropertyChanged += (_, e) =>
    {
      if (e.Property != IsVisibleProperty) return;
      ContextBar.IsVisible = AnimBar.IsVisible;
    };
    _ctxPlayback = Environment.GetEnvironmentVariable("SM2_CTX") == "1";
    if (Environment.GetEnvironmentVariable("SM2_SHOW_PROPS") == "0") CtxPropsToggle.IsChecked = false;
    if (Environment.GetEnvironmentVariable("SM2_SHOW_FX") == "0") CtxFxToggle.IsChecked = false;
    _ctxWeaponPick = Environment.GetEnvironmentVariable("SM2_CTX_WEAPON") is { Length: > 0 } w ? w : null;
    _suppressCtx = true;
    VfxPlaybackBox.SelectedIndex = _ctxPlayback ? 1 : 0;
    _suppressCtx = false;
  }

  private Task<ContextResolver?> EnsureContextAsync()
  {
    if (_ctx is not null) return Task.FromResult<ContextResolver?>(_ctx);
    var catalog = _catalog;
    var store = _store;
    if (catalog is null || store is null) return Task.FromResult<ContextResolver?>(null);
    return _ctxTask ??= Task.Run(() =>
    {
      var paths = new List<string>();
      var byPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      for (var i = 0; i < catalog.FileCount; i++)
      {
        var p = catalog.GetFile(i).Path;
        if (!p.EndsWith(".cls", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".cls.resource", StringComparison.OrdinalIgnoreCase)) continue;
        byPath.TryAdd(p, i);
        if (p.EndsWith(".cls", StringComparison.OrdinalIgnoreCase)) paths.Add(p);
      }

      string? Read(string path)
      {
        if (!byPath.TryGetValue(path, out var i)) return null;
        byte[] bytes;
        lock (store) bytes = store.Read(i);
        return SslText.LooksText(bytes) ? Encoding.UTF8.GetString(bytes) : null;
      }

      return (ContextResolver?)new ContextResolver(ContextIndex.Build(paths, Read));
    }).ContinueWith(t => _ctx = t.IsCompletedSuccessfully ? t.Result : null, TaskScheduler.Default);
  }

  // ---- effect -> model + animation ---------------------------------------------------------------------------

  private async void VfxPlaybackChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressCtx || VfxPlaybackBox is null || ContextBar is null) return; // fires during InitializeComponent
    _ctxPlayback = VfxPlaybackBox.SelectedIndex == 1;
    if (_ctxPlayback && _fx is { } fx) await Guard(OpenEffectContextAsync(fx.Definition.Name));
  }

  /// <summary>Called after an .sfx is shown: in context mode, swap to the owning character + clip.</summary>
  private async Task AfterEffectShownAsync(string name)
  {
    _suppressCtx = true;
    VfxPlaybackBox.SelectedIndex = _ctxPlayback ? 1 : 0;
    _suppressCtx = false;
    if (_ctxPlayback) await OpenEffectContextAsync(name);
  }

  private async Task OpenEffectContextAsync(string effect)
  {
    if (_catalog is null || _store is null) return;
    PreviewNote.Text = $"Context playback: finding who plays {effect}…";
    var resolver = await EnsureContextAsync();
    if (resolver is null) { PreviewNote.Text = "Context playback needs a game folder."; return; }
    var map = await LoadHostVfxMapAsync();
    var candidates = await Task.Run(() => resolver.ForEffect(effect, map));
    if (candidates.Count == 0)
    {
      PreviewNote.Text = $"Context playback: no character class links {effect} (no sfx.list / cooked link / text mention reaches a class with a model and a morpheme bundle). Showing the effect only.";
      return;
    }

    _ctxCandidates = candidates;
    var pick = Environment.GetEnvironmentVariable("SM2_CTX_CHAR") is { Length: > 0 } want
      ? candidates.FirstOrDefault(c => c.Character.Contains(want, StringComparison.OrdinalIgnoreCase)) ?? candidates[0]
      : candidates[0];
    await OpenContextCandidateAsync(effect, pick);
  }

  private async Task OpenContextCandidateAsync(string effect, EffectContext context)
  {
    if (_catalog is not { } catalog || _store is not { } store || _ctx is not { } resolver) return;
    if (context.Tpl is not { Length: > 0 } tpl || catalog.FindByFileName(tpl + ".tpl") is not { Count: > 0 } tpls)
    {
      PreviewNote.Text = $"Context playback: {context.Character} has no template in the catalog ({context.Tpl}).";
      return;
    }

    var bundle = context.Bundle ?? "";
    var clips = await Task.Run(() =>
    {
      var rows = new List<(string Clip, IReadOnlyCollection<string> Tags)>();
      var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      for (var i = 0; i < catalog.FileCount; i++)
      {
        var p = catalog.GetFile(i).Path;
        if (!p.EndsWith(".sani", StringComparison.OrdinalIgnoreCase) || bundle.Length == 0
            || !Path.GetFileNameWithoutExtension(p).EndsWith("_" + bundle, StringComparison.OrdinalIgnoreCase)) continue;
        byte[] bytes;
        try { lock (store) bytes = store.Read(i); }
        catch { continue; }
        if (SaniReader.ReadEvents(bytes) is not { } ev) continue;
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in ev.Tracks)
          foreach (var e in t.Events)
            tags.UnionWith(e.Tags);
        rows.Add((p, tags));
        index.TryAdd(p, i);
      }

      return ContextResolver.RankClips(rows, context.Events, effect, 24).Select(r => (r.Clip, r.Proven, Index: index[r.Clip])).ToList();
    });

    _ctxOpening = true;
    try
    {
      _ctxEffect = effect;
      _ctxCurrent = context;
      await PreviewFileAsync(tpls[0]);
    }
    finally { _ctxOpening = false; }

    if (_mesh is null) return;
    _suppressCtx = true;
    CtxCharacterBox.ItemsSource = _ctxCandidates.Select(c => c.Label).ToList();
    CtxCharacterBox.SelectedIndex = Math.Max(0, _ctxCandidates.IndexOf(context));
    CtxCharacterBox.IsVisible = true;
    CtxBackButton.IsVisible = true;
    var clipRows = clips.Select(c => new AnimChoice { Label = $"{(c.Proven ? "" : "(HEURISTIC) ")}{Path.GetFileName(c.Clip)}", Index = c.Index, Path = c.Clip }).ToList();
    CtxClipBox.ItemsSource = clipRows;
    CtxClipBox.IsVisible = clipRows.Count > 0;
    var wantClip = Environment.GetEnvironmentVariable("SM2_CTX_CLIP");
    var first = clipRows.FirstOrDefault(c => wantClip is { Length: > 0 } && c.Path.Contains(wantClip, StringComparison.OrdinalIgnoreCase)) ?? clipRows.FirstOrDefault();
    CtxClipBox.SelectedItem = first;
    _suppressCtx = false;
    if (first is null)
    {
      CtxStatus.Text = $"{context.Label}: no clip of bundle '{bundle}' names an event or token of {effect}.";
      await UpdateModelContextAsync();
      return;
    }

    await LoadContextClipAsync(first.Index, play: true);
  }

  private async Task LoadContextClipAsync(int index, bool play)
  {
    if (_catalog is null || _store is null || _mesh is null) return;
    var generation = ++_animGeneration;
    var catalog = _catalog;
    var store = _store;
    var clip = await Task.Run(() => SaniReader.Load(catalog, store, index));
    if (generation != _animGeneration || _mesh is null) return;
    _clip = clip;
    _clipIndex = index;
    _ctxPendingClip = index;
    SelectAnimRow(index);
    ApplyClipToMesh(clip); // runs UpdateModelContextAsync (weapon + effects)
    if (play && clip.HasCurves && PlayButton.IsVisible && !_playTimer.IsEnabled)
      PlayClicked(PlayButton, new RoutedEventArgs());
    await QaPauseAsync();
  }

  private bool _qaPaused;

  /// <summary>QA: SM2_CLIP=clip file name part plays that clip on the opened model (reverse direction).</summary>
  private async Task OpenClipFromEnvironmentAsync()
  {
    if (_ctxEffect is not null || _mesh is null || _catalog is not { } catalog
        || Environment.GetEnvironmentVariable("SM2_CLIP") is not { Length: > 0 } want) return;
    for (var i = 0; i < catalog.FileCount; i++)
    {
      var p = catalog.GetFile(i).Path;
      if (p.EndsWith(".sani", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(p).Contains(want, StringComparison.OrdinalIgnoreCase))
      {
        await LoadContextClipAsync(i, play: true);
        return;
      }
    }

    StatusText.Text = "SM2_CLIP: no clip matches " + want;
  }

  /// <summary>QA: SM2_TIME=seconds pauses the context clip there (effects on clip time) once, for screenshots.</summary>
  private async Task QaPauseAsync()
  {
    if (_qaPaused || !float.TryParse(Environment.GetEnvironmentVariable("SM2_TIME"), System.Globalization.NumberStyles.Float,
          System.Globalization.CultureInfo.InvariantCulture, out var t)) return;
    _qaPaused = true;
    await Task.Delay(2500); // weapon merge + effect textures
    _playTimer.Stop();
    PlayButton.Content = "Play";
    HostVfxLoopToggle.IsChecked = false;
    _time = Math.Clamp(t, 0, _clip?.Duration ?? t);
    if (Environment.GetEnvironmentVariable("SM2_CAM") is { Length: > 0 } cam && cam.Split(':') is { Length: >= 3 } c && _mesh is not null)
    {
      var inv = System.Globalization.CultureInfo.InvariantCulture;
      _yaw = float.Parse(c[0], inv);
      _pitch = float.Parse(c[1], inv);
      _distance = MeshPreview.FitDistance(_mesh, 1f) * float.Parse(c[2], inv);
    }

    SyncScrub();
    DrawMesh();
  }

  /// <summary>Shows the context clip in the ANIM box once the compatible list is there (no reload).</summary>
  private void SelectAnimRow(int index)
  {
    if (AnimBox.ItemsSource is not IEnumerable<AnimChoice> rows) return;
    var row = rows.FirstOrDefault(r => r.Index == index);
    if (row is null) return;
    _suppressAnim = true;
    AnimBox.SelectedItem = row;
    _suppressAnim = false;
  }

  private async void CtxCharacterChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressCtx || _ctxEffect is null || CtxCharacterBox is null || CtxCharacterBox.SelectedIndex < 0 || CtxCharacterBox.SelectedIndex >= _ctxCandidates.Count) return;
    await Guard(OpenContextCandidateAsync(_ctxEffect, _ctxCandidates[CtxCharacterBox.SelectedIndex]));
  }

  private async void CtxClipChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressCtx || CtxClipBox?.SelectedItem is not AnimChoice row) return;
    _playTimer.Stop();
    PlayButton.Content = "Play";
    await Guard(LoadContextClipAsync(row.Index, play: true));
  }

  /// <summary>async void handlers must never take the app down: report instead.</summary>
  private async Task Guard(Task work)
  {
    try { await work; }
    catch (Exception ex) { CtxStatus.Text = "Context playback error: " + ex.Message; }
  }

  private async void CtxBackClicked(object? sender, RoutedEventArgs e)
  {
    if (_ctxEffect is not { } effect || _catalog is null) return;
    _ctxPlayback = false;
    _ctxEffect = null;
    _ctxCurrent = null;
    if (_catalog.FindByFileName(effect + ".sfx") is { Count: > 0 } hits) await Guard(PreviewFileAsync(hits[0]));
  }

  /// <summary>PreviewFileAsync hook: a new model replaces the context base (forward context survives while it opens it).</summary>
  private void ContextModelLoaded(ExportedMesh mesh)
  {
    _ctxBase = mesh;
    _ctxWeapons = [];
    if (_hostVfx is not null) _hostVfx.Context = null;
    if (!_ctxOpening)
    {
      _ctxEffect = null;
      _ctxCurrent = null;
      _ctxCandidates = [];
      CtxCharacterBox.IsVisible = false;
      CtxClipBox.IsVisible = false;
      CtxBackButton.IsVisible = false;
    }

    CtxSlotBox.IsVisible = false;
    CtxStatus.Text = "";
    _suppressCtx = true;
    CtxWeaponBox.ItemsSource = null;
    _suppressCtx = false;
  }

  // ---- model + clip -> weapon + effects ----------------------------------------------------------------------

  private async void CtxOptionsChanged(object? sender, RoutedEventArgs e) => await Guard(UpdateModelContextAsync());

  private async void CtxWeaponChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressCtx || CtxWeaponBox?.ItemsSource is null) return;
    _ctxWeaponPick = CtxWeaponBox.SelectedIndex <= 0 ? "-" : (CtxWeaponBox.SelectedItem as string)?.Split(' ')[0];
    _ctxSlotPick = null;
    await Guard(UpdateModelContextAsync());
  }

  private async void CtxSlotChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressCtx || CtxSlotBox?.ItemsSource is null) return;
    _ctxSlotPick = (CtxSlotBox.SelectedItem as string)?.Split(' ')[0];
    await Guard(UpdateModelContextAsync());
  }

  /// <summary>Weapon + effects for the model's current clip; re-run on clip, toggle and picker changes.</summary>
  private async Task UpdateModelContextAsync()
  {
    if (_catalog is not { } catalog || _store is not { } store || _ctxBase is not { } baseMesh || _mesh is null || _previewFileIndex < 0) return;
    var generation = ++_ctxGeneration;
    var resolver = await EnsureContextAsync();
    if (resolver is null || generation != _ctxGeneration) return;
    var clip = _clip;
    var clipPath = _clipIndex >= 0 ? catalog.GetFile(_clipIndex).Path : null;
    var tplStem = ContextResolver.Stem(catalog.GetFile(_previewFileIndex).Path);
    var forward = _ctxCurrent;
    var showProps = CtxPropsToggle.IsChecked == true;
    var showFx = CtxFxToggle.IsChecked == true;
    var shoots = clip?.EventTracks.Any(t => t.Name.Equals("Shoot", StringComparison.OrdinalIgnoreCase)) == true;
    var map = showFx ? await LoadHostVfxMapAsync() : null;
    var plan = await Task.Run(() =>
    {
      var (characters, charsProven) = forward is not null ? (new List<string> { forward.Character }, forward.Proven) : resolver.CharactersFor(tplStem, clipPath);
      var weapons = clip is null && forward is null ? new List<WeaponChoice>() : resolver.WeaponsFor(characters.Take(3).ToList(), clipPath ?? "", shoots);
      if (forward?.Weapon is { } fw && forward.WeaponTpl is { } fwt)
      {
        weapons.RemoveAll(w => w.Class.Equals(fw, StringComparison.OrdinalIgnoreCase));
        weapons.Insert(0, new WeaponChoice(fw, fwt, resolver.HoldFor(forward.Character, fw), forward.Proven, "effect owner"));
      }

      if (map is not null)
      {
        foreach (var c in characters.Take(1)) resolver.Index.AddInlineBlueprints(c, map);
        foreach (var w in weapons) resolver.Index.AddInlineBlueprints(w.Class, map);
      }

      return (characters, charsProven, weapons);
    });
    if (generation != _ctxGeneration || !ReferenceEquals(_ctxBase, baseMesh)) return;

    _ctxWeapons = plan.weapons;
    WeaponChoice? weapon = null;
    if (_ctxWeaponPick != "-")
      weapon = plan.weapons.FirstOrDefault(w => _ctxWeaponPick is not null && w.Class.Equals(_ctxWeaponPick, StringComparison.OrdinalIgnoreCase))
               ?? plan.weapons.FirstOrDefault(w => _ctxWeaponPick is not null && w.Class.Contains(_ctxWeaponPick, StringComparison.OrdinalIgnoreCase))
               ?? plan.weapons.FirstOrDefault();
    _suppressCtx = true;
    var labels = new List<string> { "(no weapon)" };
    labels.AddRange(plan.weapons.Select(w => $"{w.Class} {(w.Proven ? "· PROVEN" : "· HEURISTIC")} · {w.Why}"));
    CtxWeaponBox.ItemsSource = labels;
    CtxWeaponBox.SelectedIndex = weapon is null ? 0 : plan.weapons.IndexOf(weapon) + 1;
    CtxWeaponBox.IsEnabled = showProps || showFx;
    _suppressCtx = false;

    // Props: the weapon template merged under its hand locator (a copy of the model; the base stays for "off").
    var mesh = baseMesh;
    string? holdBone = null;
    string? weaponRoot = null;
    var notes = new List<string>();
    if (weapon is not null)
    {
      holdBone = FindHold(baseMesh, weapon.Hold, out var holdNote);
      if (holdNote.Length > 0) notes.Add(holdNote);
    }

    if (showProps && weapon is not null && holdBone is not null)
    {
      var key = $"{weapon.Tpl}@{holdBone}";
      var loaded = _ctxProps is not null && _ctxPropsKey == key && ReferenceEquals(_ctxPropsBase, baseMesh) ? null : await LoadWeaponMeshAsync(weapon.Tpl);
      if (generation != _ctxGeneration) return;
      if (_ctxProps is not null && _ctxPropsKey == key && ReferenceEquals(_ctxPropsBase, baseMesh))
      {
        mesh = _ctxProps;
        weaponRoot = _ctxPropsRoot;
      }
      else if (loaded is { } w)
      {
        var props = baseMesh.ShallowCopy();
        var first = props.Bones.Count;
        var merged = MeshAttach.Merge(props, w.Mesh.VisibleGeometry(), MeshAttach.FindNode(baseMesh, holdBone), 1f, "wpn");
        if (merged > 0)
        {
          mesh = props;
          weaponRoot = props.Bones.Count > first ? props.Bones[first].Name : null;
          MergeTextures(w.Textures);
          (_ctxProps, _ctxPropsKey, _ctxPropsBase, _ctxPropsRoot) = (props, key, baseMesh, weaponRoot);
        }
        else notes.Add($"{weapon.Tpl}: nothing merged");
      }
      else notes.Add($"{weapon.Tpl}.tpl not in the catalog");
    }

    mesh.Clip = _mesh.Clip;
    if (!ReferenceEquals(_mesh, mesh))
    {
      _mesh = mesh;
      _hostVfxSocketsFor = null;
    }

    // Effects: owned blueprints (+ inline ones), sfx.list resolution, weapon socket scope, Shoot -> muzzle.
    var cueText = "";
    if (await EnsureHostVfxAsync() is { } vfx)
    {
      if (showFx && map is not null && plan.characters.Count > 0)
      {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        owned.UnionWith(resolver.Index.BlueprintsOf(plan.characters[0]));
        HashSet<string>? weaponBps = null;
        if (weapon is not null)
        {
          weaponBps = resolver.Index.BlueprintsOf(weapon.Class);
          owned.UnionWith(weaponBps);
        }

        var resolve = resolver.Resolver(plan.characters[0], weapon?.Class, map);
        var prefix = mesh == baseMesh ? null : "wpn";
        var root = weaponRoot ?? holdBone ?? "";
        Func<string, string, (string, string)?> scope = (effect, source) =>
          weapon is not null && ((weaponBps is not null && ContextResolver.OwnedSource(weaponBps, source)) || resolver.WeaponOwns(weapon.Class, effect) || source.Equals(weapon.Class, StringComparison.OrdinalIgnoreCase))
            ? (prefix ?? "", root)
            : null;
        var cues = new List<TrackCue>();
        var slots = weapon is null ? [] : resolver.ShootCues(weapon.Class);
        _suppressCtx = true;
        CtxSlotBox.ItemsSource = slots.Select(s => $"{s.SlotKey} → {s.Effect}").ToList();
        CtxSlotBox.IsVisible = shoots && slots.Count > 1;
        var slot = slots.FirstOrDefault(s => _ctxSlotPick is not null && s.SlotKey.Equals(_ctxSlotPick, StringComparison.OrdinalIgnoreCase)) ?? slots.FirstOrDefault();
        CtxSlotBox.SelectedIndex = slot is null ? -1 : slots.IndexOf(slot);
        _suppressCtx = false;
        if (shoots && slot is not null) cues.Add(slot);
        vfx.Context = new HostVfxContext(h => ContextResolver.OwnedSource(owned, h.Source), resolve, scope, cues)
        {
          Overrides = resolver.Overrides(plan.characters[0], weapon?.Class),
        };
        vfx.Map = map;
        HostVfxFilter.Text = "";
        var status = await Task.Run(() => vfx.SetEvents(mesh, mesh.Clip is not null));
        if (vfx.Events is { } ev)
          cueText = ev.Cues.Count == 0 ? "no effect cues on this clip"
            : string.Join(", ", ev.Cues.Take(6).Select(c => $"{c.Event}@{c.Time:0.00}s→{c.Effect}{(c.Track.Equals("Shoot", StringComparison.OrdinalIgnoreCase) && c.Event == c.Track ? " (HEURISTIC Shoot→muzzle)" : "")}"));
        else if (status.Length > 0) cueText = status;
      }
      else
      {
        vfx.Context = null;
        CtxSlotBox.IsVisible = false;
        await Task.Run(() => vfx.SetEvents(mesh, false));
      }

      _hostVfxClock.Restart();
    }

    var who = plan.characters.Count == 0 ? $"no class uses {tplStem}" : $"{plan.characters[0]}{(plan.charsProven ? "" : " (HEURISTIC: bundle from clip name)")}";
    var held = weapon is null ? "no weapon" : $"{weapon.Class} on {holdBone ?? weapon.Hold}{(weapon.Proven ? "" : " (HEURISTIC)")}{(showProps ? "" : " [hidden]")}";
    CtxStatus.Text = $"{who} · {held} · {(showFx ? cueText : "effects off")}{(notes.Count > 0 ? " · " + string.Join("; ", notes) : "")}"
                     + (forward is null ? "" : $" · {forward.Effect} via {forward.Owner}: {(forward.Events.Count > 0 ? string.Join("/", forward.Events) : "no event link")}{(forward.Notes.Count > 0 ? " — " + string.Join(" ", forward.Notes) : "")}");
    ToolTip.SetTip(CtxStatus, CtxStatus.Text);
    DrawMesh();
  }

  /// <summary>The hold locator on the model, else a hand / gun bone (HEURISTIC).</summary>
  private static string? FindHold(ExportedMesh mesh, string hold, out string note)
  {
    note = "";
    if (MeshAttach.FindNode(mesh, hold) is var i and >= 0) return mesh.Bones[i].Name;
    var right = !hold.EndsWith("_L", StringComparison.OrdinalIgnoreCase);
    string[] keys = right ? ["gun_r", "weapon_r", "r_weapon", "hand_r", "r_hand", "rhand"] : ["gun_l", "weapon_l", "l_weapon", "hand_l", "l_hand", "lhand"];
    foreach (var key in keys)
      foreach (var b in mesh.Bones)
        if (b.Name.Contains(key, StringComparison.OrdinalIgnoreCase))
        {
          note = $"HEURISTIC: no {hold} locator, weapon on {b.Name}";
          return b.Name;
        }

    note = $"no {hold} locator on this model";
    return null;
  }

  private async Task<(ExportedMesh Mesh, PreviewTextures Textures)?> LoadWeaponMeshAsync(string tpl)
  {
    if (_ctxWeaponMeshes.TryGetValue(tpl, out var cached)) return cached;
    if (_catalog is not { } catalog || _store is not { } store) return null;
    var loaded = await Task.Run(() =>
    {
      if (catalog.FindByFileName(tpl + ".tpl") is not { Count: > 0 } hits) return ((ExportedMesh, PreviewTextures)?)null;
      try
      {
        var mesh = TemplateMeshReader.Load(catalog, store, hits[0]);
        return (mesh, ResolvePreview(catalog, store, mesh, hits[0]));
      }
      catch { return null; }
    });
    _ctxWeaponMeshes[tpl] = loaded;
    return loaded;
  }

  private void MergeTextures(PreviewTextures textures)
  {
    static void Into(Dictionary<string, PreviewImage>? target, Dictionary<string, PreviewImage> source)
    {
      if (target is null) return;
      foreach (var (k, v) in source) target.TryAdd(k, v);
    }

    _albedo ??= new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    _normals ??= new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    _specular ??= new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    Into(_albedo, textures.Albedo);
    Into(_normals, textures.Normals);
    Into(_specular, textures.Specular);
    if (_baseAlbedo is not null && !ReferenceEquals(_baseAlbedo, _albedo)) Into(_baseAlbedo, textures.Albedo);
  }
}
