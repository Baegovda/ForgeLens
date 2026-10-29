using Avalonia.Controls;
using Avalonia.Interactivity;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Context;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

/// <summary>
///   propanim: a template's own embedded sequences (<see cref="TplAnimation" />). Standalone: one ANIM row per sequence
///   and the playback-condition box (재생 조건). Context view: the held weapon's sequences composed over the character
///   clip (<see cref="ExportedMesh.Overlay" />), Auto = follow the character's action (<see cref="PropAnimStates.AutoPlays" />),
///   toggled by "Prop anim / 프롭 애님" (persisted).
/// </summary>
public partial class MainWindow
{
  private string _propCond = PropAnimStates.AutoName;
  private bool _suppressPropCond;
  private List<PropCondition> _propConds = [];
  private List<PropCondition> _ctxPropConds = [];
  private int _propGeneration;

  private void InitPropAnim()
  {
    CtxPropAnimToggle.IsChecked = _settings.PropAnim ?? true;
    if (_settings.PropCondition is { Length: > 0 } c) _propCond = c;
    if (Environment.GetEnvironmentVariable("SM2_PROP_ANIM") is { Length: > 0 } on) CtxPropAnimToggle.IsChecked = on != "0";
    if (Environment.GetEnvironmentVariable("SM2_PROP_COND") is { Length: > 0 } cond) _propCond = cond;
  }

  private void SavePropSettings()
  {
    _settings.PropAnim = CtxPropAnimToggle.IsChecked == true;
    _settings.PropCondition = _propCond;
    try { _settings.Save(); } catch { /* best effort */ }
  }

  /// <summary>ANIM rows for the model's embedded sequences (state order), first in the list.</summary>
  private static List<AnimChoice> PropSequenceRows(TplAnimation? anim) =>
    PropAnimStates.Conditions(anim).Where(c => c.Seq is not null).Select(c => new AnimChoice
    {
      Label = $"▶ {c.Display}  ({c.Seq!.Duration:0.00}s, tpl)", Index = -2, Path = $"tpl seq {c.Name} {c.Label}", Seq = c.Seq, Rank = ClipRank.Main, SameModel = true,
    }).ToList();

  private static void FillConditionBox(ComboBox box, List<PropCondition> conds, string pick)
  {
    box.ItemsSource = conds.Select(c => c.Display).ToList();
    var i = conds.FindIndex(c => c.Name.Equals(pick, StringComparison.OrdinalIgnoreCase));
    box.SelectedIndex = i < 0 ? 0 : i;
    box.IsVisible = conds.Count > 1;
  }

  /// <summary>Standalone: the condition box for the loaded model (hidden when it has no embedded sequence).</summary>
  private void SetupPropConditions()
  {
    _propConds = _mesh?.TplAnim is { Sequences.Count: > 0 } anim ? PropAnimStates.Conditions(anim) : [];
    _suppressPropCond = true;
    FillConditionBox(PropCondBox, _propConds, _propCond);
    _suppressPropCond = false;
  }

  private void SyncConditionBoxes()
  {
    _suppressPropCond = true;
    if (_propConds.Count > 1) FillConditionBox(PropCondBox, _propConds, _propCond);
    if (_ctxPropConds.Count > 1) FillConditionBox(CtxPropCondBox, _ctxPropConds, _propCond);
    _suppressPropCond = false;
  }

  private async void PropCondChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressPropCond || PropCondBox.SelectedIndex < 0 || PropCondBox.SelectedIndex >= _propConds.Count) return;
    _propCond = _propConds[PropCondBox.SelectedIndex].Name;
    SavePropSettings();
    SyncConditionBoxes();
    await Guard(PlayPropStandaloneAsync(_propConds[PropCondBox.SelectedIndex], autoPlay: true));
  }

  private async void CtxPropCondChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressPropCond || CtxPropCondBox.SelectedIndex < 0 || CtxPropCondBox.SelectedIndex >= _ctxPropConds.Count) return;
    _propCond = _ctxPropConds[CtxPropCondBox.SelectedIndex].Name;
    SavePropSettings();
    SyncConditionBoxes();
    await Guard(UpdateModelContextAsync());
  }

  private async void CtxPropAnimChanged(object? sender, RoutedEventArgs e)
  {
    SavePropSettings();
    await Guard(UpdateModelContextAsync());
  }

  /// <summary>Owner class data for a template: blueprint anim triggers (owned, PROVEN filter), layer masks, shootAnimation.</summary>
  private async Task<(List<EventAnim> Triggers, IReadOnlyList<PropAnimLayer> Layers, string? Shoot, string Note, IReadOnlyList<FirearmFireRate> Rates)> PropDataAsync(string? cls, TplAnimation anim)
  {
    var resolver = await EnsureContextAsync();
    var map = await LoadHostVfxMapAsync();
    if (resolver is null) return (FallbackTriggers(map, anim), [], null, "no class index: blueprint whose anims all exist here (HEURISTIC)", (IReadOnlyList<FirearmFireRate>)[]);
    return await Task.Run(() =>
    {
      if (cls is null) return (FallbackTriggers(map, anim), (IReadOnlyList<PropAnimLayer>)[], (string?)null, "no owner class: blueprint whose anims all exist here (HEURISTIC)", (IReadOnlyList<FirearmFireRate>)[]);
      resolver.Index.AddInlineBlueprints(cls, map);
      var bps = resolver.Index.BlueprintsOf(cls);
      var owned = map.Anims.Where(a => ContextResolver.OwnedSource(bps, a.Source)).ToList();
      var note = $"{cls}: {owned.Count} blueprint anim trigger(s) (owned blueprints, DATA)";
      if (owned.Count == 0)
      {
        owned = FallbackTriggers(map, anim);
        if (owned.Count > 0) note = $"{cls}: blueprint picked by its anim names (HEURISTIC)";
      }

      return (owned, resolver.Index.AnimLayersOf(cls), resolver.Index.ShootAnimationOf(cls), note, resolver.Index.FireRatesOf(cls));
    });
  }

  /// <summary>HEURISTIC: triggers of the blueprints whose every Startanim names a sequence of this template.</summary>
  private static List<EventAnim> FallbackTriggers(AnimEventVfxMap map, TplAnimation anim) =>
    map.Anims.GroupBy(a => a.Source, StringComparer.OrdinalIgnoreCase)
      .Where(g => g.All(a => anim.Find(a.Anim) is not null) && g.Any(a => !a.Stop))
      .OrderByDescending(g => g.Count()).Take(1).SelectMany(g => g).ToList();

  /// <summary>Standalone: the chosen condition as the model's clip (Auto = holding state of the owner class).</summary>
  private async Task PlayPropStandaloneAsync(PropCondition cond, bool autoPlay)
  {
    if (_mesh?.TplAnim is not { } anim) return;
    var mesh = _mesh;
    var generation = ++_propGeneration;
    AnimationClip clip;
    string why;
    string? cls = null;
    if (_catalog is not null && _previewFileIndex >= 0 && await EnsureContextAsync() is { } resolver)
      cls = resolver.ClassesForTpl(ContextResolver.Stem(_catalog.GetFile(_previewFileIndex).Path)).FirstOrDefault();
    var data = await PropDataAsync(cls, anim);
    if (generation != _propGeneration || !ReferenceEquals(mesh, _mesh)) return;
    var masks = PropAnimStates.Masks(anim, mesh.Bones, data.Layers);
    if (cond.Seq is { } seq)
    {
      clip = anim.ToClip(seq, mesh.Bones, 0, masks.TryGetValue(seq.Name, out var m) ? m : null);
      why = $"{cond.Display}{(masks.ContainsKey(seq.Name) ? " · objects under its anim.layerList roots (DATA)" : "")}";
    }
    else
    {
      var plan = PropAnimStates.AutoPlays(anim, null, data.Triggers, masks, data.Shoot, data.Rates);
      if (plan.Plays.Count == 0)
      {
        ResetClip();
        PreviewNote.Text = "Auto: no holding-state sequence for this prop (bind pose). Pick a condition to play one.";
        DrawMesh();
        return;
      }

      var duration = Math.Max(0.5f, plan.Plays.Max(p => p.Seq.Duration));
      clip = anim.Compose(plan.Plays, duration, mesh.Bones, 0, "Auto");
      why = "Auto: " + string.Join("; ", plan.Plays.Where(p => p.Why.Length > 0).Select(p => p.Why));
    }

    _playTimer.Stop();
    PlayButton.Content = "Play";
    _clip = clip;
    _clipIndex = -1;
    ApplyClipToMesh(clip);
    PreviewNote.Text = $"{why} · {clip.Tracks.Count} object track(s), {clip.Duration:0.###}s · {data.Note}. Keys [frame, values] DATA-PROVEN; visibility < 0.5 = hidden (HEURISTIC).";
    if (autoPlay && clip.HasCurves && PlayButton.IsVisible)
      PlayClicked(PlayButton, new RoutedEventArgs());
    else
      DrawMesh();
  }

  /// <summary>
  ///   Context view: the held weapon's sequences over the character clip on the merged mesh (bones from
  ///   <paramref name="first" />). Returns a short status for the context line.
  /// </summary>
  private async Task<string> ApplyPropOverlayAsync(ExportedMesh mesh, ExportedMesh baseMesh, WeaponChoice? weapon, AnimationClip? clip, int generation)
  {
    baseMesh.Overlay = null;
    TplAnimation? anim = null;
    (ExportedMesh Mesh, PreviewTextures Textures)? loaded = null;
    if (weapon is not null && !ReferenceEquals(mesh, baseMesh))
    {
      loaded = await LoadWeaponMeshAsync(weapon.Tpl);
      anim = loaded?.Mesh.TplAnim is { Sequences.Count: > 0 } a ? a : null;
    }

    _ctxPropConds = anim is null ? [] : PropAnimStates.Conditions(anim);
    _suppressPropCond = true;
    FillConditionBox(CtxPropCondBox, _ctxPropConds, _propCond);
    CtxPropCondBox.IsVisible = _ctxPropConds.Count > 1 && CtxPropAnimToggle.IsChecked == true;
    _suppressPropCond = false;
    CtxPropAnimToggle.IsVisible = true;
    if (anim is null || loaded is null || weapon is null)
    {
      mesh.Overlay = null;
      return "";
    }

    if (CtxPropAnimToggle.IsChecked != true)
    {
      mesh.Overlay = null;
      return "prop anim off";
    }

    var data = await PropDataAsync(weapon.Class, anim);
    if (generation != _ctxGeneration) return "";
    var bones = loaded.Value.Mesh.Bones;
    var first = baseMesh.Bones.Count;
    var masks = PropAnimStates.Masks(anim, bones, data.Layers);
    var cond = _ctxPropConds.FirstOrDefault(c => c.Name.Equals(_propCond, StringComparison.OrdinalIgnoreCase)) ?? PropAnimStates.Auto;
    AnimationClip overlay;
    string text;
    if (cond.Seq is { } seq)
    {
      overlay = anim.Compose([PropAnimStates.Single(seq, masks)], seq.Duration, bones, first, seq.Name);
      text = $"prop anim: {cond.Display} (loop)";
    }
    else
    {
      var plan = PropAnimStates.AutoPlays(anim, clip, data.Triggers, masks, data.Shoot, data.Rates);
      var duration = clip is { Duration: > 0 } ? clip.Duration : Math.Max(0.5f, plan.Plays.Select(p => p.Seq.Duration).DefaultIfEmpty(1f).Max());
      overlay = anim.Compose(plan.Plays, duration, bones, first, "prop auto");
      var counts = plan.Plays.GroupBy(p => p.Seq.Name).Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key + (g.First().Loop ? " (loop)" : ""));
      text = plan.Plays.Count == 0 ? "prop anim: Auto, nothing maps" : "prop anim Auto: " + string.Join(", ", counts);
      ToolTip.SetTip(CtxPropAnimToggle, "Auto plays:\n" + string.Join("\n", plan.Plays.Select(p => $"{p.Start:0.00}s {p.Seq.Name}: {p.Why}").Take(30))
        + (plan.Notes.Count > 0 ? "\n" + string.Join("\n", plan.Notes) : "") + "\n" + data.Note);
    }

    // The weapon root sits on the hand locator in the merged mesh: its own key would put it back at the template origin.
    var roots = Enumerable.Range(0, bones.Count).Where(i => bones[i].Parent < 0).Select(i => first + i).ToHashSet();
    overlay.Tracks.RemoveAll(t => roots.Contains(t.Bone));
    mesh.Overlay = overlay.HasCurves ? overlay : null;
    return text;
  }
}
