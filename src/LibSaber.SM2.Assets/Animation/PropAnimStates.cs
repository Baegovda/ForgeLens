using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>
///   propanim: one <c>anim.layerList</c> entry of a prop class (e.g. <c>MainMultiStitch</c>): the embedded sequences in
///   <see cref="Anims" /> drive only the object subtrees named in <see cref="Roots" /> (<c>RootObjectNameList</c>). DATA.
/// </summary>
public sealed record PropAnimLayer(string Name, string Type, IReadOnlyList<string> Anims, IReadOnlyList<string> Roots);

/// <summary>A playback condition for the condition selector: <see cref="Seq" /> null = Auto.</summary>
public sealed record PropCondition(string Name, string Label, TplSequence? Seq)
{
  public bool IsAuto => Seq is null;
  public string Display => Seq is null ? Label : Label.Length > 0 ? $"{Name} · {Label}" : Name;
}

/// <summary>Sequence plays for one character clip, with why each was chosen.</summary>
public sealed record PropAutoPlan(List<TplPlay> Plays, List<string> Notes);

/// <summary>
///   propanim: the weapon/prop states a tpl's embedded sequences stand for, their short English labels, and the "Auto"
///   mapping from a character clip to sequence plays.
///   <list type="bullet">
///     <item>Firearms (<c>prop_firearm_animation</c>, DATA): Idle (initial) ⇄ Shoot ⇄ Reload; IDLE=[IDLE,CHARGING,TAP,COOLING],
///       SHOOT=[SHOOT_1..3], RELOAD=[RELOAD_START,RELOAD_END,RELOAD_FULL]; <c>shootAnimation</c> default <c>SHOOT_1</c>.</item>
///     <item>Melee and other props (DATA): blueprint <c>Startanim</c>/<c>Stopanim</c> nodes and what triggers them
///       (<see cref="EventAnim" />), e.g. chainsword <c>OnHoldInArms → chain_anim (cycle)</c>, <c>hitN_start_anim → animN</c>.</item>
///     <item>Shot = an event on the character clip's <c>Shoot</c> track (HEURISTIC); player firing clips have none. A player fire
///       clip (<see cref="PlayerFireClips"/>: <c>tacticus_shtN_shoot_aim|blind</c>, selected by the network from the firearm's
///       <c>wpnType</c>, INFERRED) fires every <c>60 / shotsPerMin</c> s of the firearm's <c>firearm_library_pve</c> barrel (rate DATA).
///       Other clips named <c>*shoot*</c>/<c>*fire*</c> keep the old name rule (HEURISTIC).</item>
///     <item>Reload = the clip carries a <c>reload_sound_seq</c> event track (DATA: exactly the 18 of 23,227 retail clips that
///       are reloads); RELOAD_END starts at its <c>snd_reload_mag_in</c> event (INFERRED).</item>
///   </list>
/// </summary>
public static class PropAnimStates
{
  public const string AutoName = "Auto";
  public const string AutoLabel = "Auto (follow character action)";
  public static readonly PropCondition Auto = new(AutoName, AutoLabel, null);

  private static readonly (Regex Pattern, Func<Match, string> Label, int Order)[] Labels =
  [
    (R("^idle$"), _ => "Idle", 0),
    (R(@"^idle_(\d+)$"), m => "Idle " + m.Groups[1].Value, 0),
    (R("^idle_no_stack$"), _ => "Idle (no stack)", 0),
    (R("^charging$"), _ => "Charging", 1),
    (R("^tap$"), _ => "Tap (cylinder spin)", 1),
    (R("^cooling$"), _ => "Cooling", 1),
    (R(@"^shoot_(\d+)$"), m => "Shoot " + m.Groups[1].Value, 2),
    (R("^last_shoot$"), _ => "Last shot", 3),
    (R("^shoot_aux$"), _ => "Aux shot (augmented aim)", 4),
    (R("^reload_start$"), _ => "Reload start", 5),
    (R("^reload_end$"), _ => "Reload end", 5),
    (R("^reload_full$"), _ => "Full reload", 5),
    (R(@"^anim(\d+)$"), m => "Melee hit " + m.Groups[1].Value, 6),
    (R("^anim_stop$"), _ => "Hit stop", 6),
    (R("^chain_anim$"), _ => "Teeth spin 1", 7),
    (R(@"^chain_anim(\d+)$"), m => $"Teeth spin {m.Groups[1].Value}", 7),
    (R("^chain_anim_stop$"), _ => "Teeth stop", 7),
    (R("^power_on$"), _ => "Power on", 8),
    (R("^power_off$"), _ => "Power off", 8),
    (R("^inc_start$"), _ => "Heat start", 8),
    (R("^inc_stop$"), _ => "Heat end", 8),
    (R("^small_inc_start$"), _ => "Light heat start", 8),
    (R("^inc_hit$"), _ => "Heat hit", 8),
    (R("^vfx_idle$"), _ => "FX idle", 8),
    (R("^vfx_charge_on$"), _ => "Charge on", 8),
    (R("^vfx_charge_off$"), _ => "Charge off", 8),
    (R("^vfx_charge_chill"), _ => "Charge hold", 8),
    (R("^hit_no_stack$"), _ => "Hit (no stack)", 8),
    (R(@"^hit_stack_(\d+)$"), m => "Stack hit " + m.Groups[1].Value, 8),
    (R("^init$"), _ => "Init", 9),
    (R(@"^finisher(\d+)$"), m => "Finisher " + m.Groups[1].Value, 9),
    (R("^start$"), _ => "Start", 9),
    (R("^stop$"), _ => "Stop", 9),
    (R("^open$"), _ => "Open", 9),
    (R("^close$"), _ => "Close", 9),
    (R("^used$"), _ => "Used", 9),
    (R("^active$"), _ => "Active", 9),
    (R("^screen_on$"), _ => "Screen on", 9),
  ];

  private static Regex R(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

  /// <summary>Short English label for a sequence name (empty when unknown: the raw data name is shown alone).</summary>
  public static string Label(string seq)
  {
    foreach (var (pattern, label, _) in Labels)
      if (pattern.Match(seq) is { Success: true } m)
        return label(m);
    return "";
  }

  private static int Order(string seq)
  {
    foreach (var (pattern, _, order) in Labels)
      if (pattern.IsMatch(seq))
        return order;
    return 10;
  }

  /// <summary>Auto first, then every embedded sequence in state order (idle, shoot, aux, reload, melee, teeth, charge, other).</summary>
  public static List<PropCondition> Conditions(TplAnimation? anim)
  {
    var list = new List<PropCondition> { Auto };
    if (anim is null) return list;
    list.AddRange(anim.Sequences.OrderBy(s => Order(s.Name)).ThenBy(s => s.Index).Select(s => new PropCondition(s.Name, Label(s.Name), s)));
    return list;
  }

  /// <summary>
  ///   Sequence name → the object indices it may drive: objects whose bone, or an ancestor bone, is named in a layer's
  ///   <c>RootObjectNameList</c> (DATA). Sequences in no layer are unrestricted.
  /// </summary>
  public static Dictionary<string, HashSet<int>> Masks(TplAnimation anim, IReadOnlyList<Bone>? bones, IEnumerable<PropAnimLayer>? layers)
  {
    var masks = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
    if (bones is null || layers is null) return masks;
    var map = anim.BoneMap(bones);
    foreach (var layer in layers)
    {
      if (layer.Roots.Count == 0 || layer.Anims.Count == 0) continue;
      var roots = new HashSet<string>(layer.Roots, StringComparer.OrdinalIgnoreCase);
      var objects = new HashSet<int>();
      foreach (var obj in anim.Objects)
      {
        for (var b = map[obj.Index]; b >= 0 && b < bones.Count; b = bones[b].Parent)
          if (roots.Contains(bones[b].Name) || roots.Contains(bones[b].SourceName))
          {
            objects.Add(obj.Index);
            break;
          }
      }

      foreach (var a in layer.Anims)
      {
        if (!masks.TryGetValue(a, out var set)) masks[a] = set = [];
        set.UnionWith(objects);
      }
    }

    return masks;
  }

  /// <summary>A selected condition: that sequence on a loop, restricted to its layer's objects.</summary>
  /// <summary>
  ///   Export: <c>seq:NAME[,NAME..]</c> or <c>seq:all</c> as clips on the template's own skeleton (tracks bind by animNmb
  ///   rank, like a .sani on that template), named by the sequence (the exporter writes <c>{model}__{sequence}</c>). Unknown names land in <paramref name="missing" />.
  /// </summary>
  public static List<AnimationClip> SequenceClips(TplAnimation anim, string spec, string model, List<string>? missing = null)
  {
    var list = spec.StartsWith("seq:", StringComparison.OrdinalIgnoreCase) ? spec[4..] : spec;
    var names = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var seqs = names.Any(n => n.Equals("all", StringComparison.OrdinalIgnoreCase))
      ? anim.Sequences.ToList()
      : names.Select(n => (Name: n, Seq: anim.Find(n))).Where(x => { if (x.Seq is null) missing?.Add(x.Name); return x.Seq is not null; }).Select(x => x.Seq!).ToList();
    var clips = new List<AnimationClip>();
    foreach (var seq in seqs)
    {
      var clip = anim.ToClip(seq);
      clip.Name = seq.Name;
      clip.Notes.Add($"tpl-embedded sequence {TplAnimation.Describe(seq)} (propanim; layout DATA-PROVEN, visibility as scale 0 HEURISTIC)");
      clips.Add(clip);
    }

    return clips;
  }

  public static TplPlay Single(TplSequence seq, IReadOnlyDictionary<string, HashSet<int>>? masks = null) =>
    new(seq, 0f, Loop: true, OnlyMoving: false, Why: "selected condition", Mask: MaskOf(seq, masks));

  private static HashSet<int>? MaskOf(TplSequence seq, IReadOnlyDictionary<string, HashSet<int>>? masks) =>
    masks is not null && masks.TryGetValue(seq.Name, out var m) ? m : null;

  /// <summary>The <c>reload_sound_seq</c> event track that marks a reload clip (DATA), or null.</summary>
  public static AnimEventTrack? ReloadTrack(AnimationClip? clip) =>
    clip?.EventTracks.FirstOrDefault(t => t.Name.Equals("reload_sound_seq", StringComparison.OrdinalIgnoreCase));

  private static readonly string[] HoldPins = [".OnHoldInArms", ".OnUserSet", ".OnEquip", ".OnAttached"];

  /// <summary>
  ///   Auto: the plays that follow <paramref name="character" />'s action. <paramref name="triggers" /> should already be
  ///   the blueprints the weapon class owns.
  /// </summary>
  public static PropAutoPlan AutoPlays(TplAnimation anim, AnimationClip? character, IEnumerable<EventAnim>? triggers,
    IReadOnlyDictionary<string, HashSet<int>>? masks = null, string? shootAnimation = null, IReadOnlyList<FirearmFireRate>? fireRates = null,
    double? wpnType = null)
  {
    var plays = new List<TplPlay>();
    var notes = new List<string>();
    var known = (triggers ?? []).Where(t => anim.Find(t.Anim) is not null).ToList();
    var duration = character?.Duration ?? 0f;

    // 1. Holding state: what the blueprint starts when the prop is held (DATA), else the firearm FSM's initial Idle.
    TplPlay? holding = null;
    foreach (var pin in HoldPins)
    {
      var starts = known.Where(t => !t.Stop && !t.IsEvent && t.Trigger.EndsWith(pin, StringComparison.OrdinalIgnoreCase)).ToList();
      if (starts.Count == 0) continue;
      var pick = starts.OrderByDescending(t => t.Loops).ThenBy(t => t.Anim.Contains("off", StringComparison.OrdinalIgnoreCase)).First();
      var seq = anim.Find(pick.Anim)!;
      var branch = starts.Select(t => t.Anim).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
      holding = new TplPlay(seq, 0f, Loop: pick.Loops, Hold: !pick.Loops, Why: $"{pick.Trigger[1..]} → {seq.Name} (blueprint, DATA{(branch ? "; branch pick HEURISTIC" : "")})",
        Mask: MaskOf(seq, masks));
      break;
    }

    if (holding is null && anim.Find("IDLE") is { } idle)
      holding = new TplPlay(idle, 0f, Loop: true, Why: "FSM initial state Idle (prop_firearm_animation, DATA)", Mask: MaskOf(idle, masks));
    if (holding is not null) plays.Add(holding);

    if (character is null)
      return new PropAutoPlan(plays, notes);

    // 2. Reload clip: it carries the reload_sound_seq track (DATA, see summary): RELOAD_FULL, else RELOAD_START then RELOAD_END.
    var reloadTrack = ReloadTrack(character);
    if (reloadTrack is not null)
    {
      var magIn = reloadTrack.Events.FirstOrDefault(e => e.Tags.Any(t => t.Equals("snd_reload_mag_in", StringComparison.OrdinalIgnoreCase)));
      if (anim.Find("RELOAD_FULL") is { } full)
        plays.Add(new TplPlay(full, 0f, Hold: false, Why: "reload clip (reload_sound_seq track, DATA) → Reload state RELOAD_FULL", Mask: MaskOf(full, masks)));
      else if (anim.Find("RELOAD_START") is { } rs)
      {
        plays.Add(new TplPlay(rs, 0f, Why: "reload clip (reload_sound_seq track, DATA) → RELOAD_START", Mask: MaskOf(rs, masks)));
        if (anim.Find("RELOAD_END") is { } re)
          plays.Add(magIn is not null
            ? new TplPlay(re, magIn.Start * duration, Why: $"→ RELOAD_END at snd_reload_mag_in {magIn.Start * duration:0.00}s (INFERRED)", Mask: MaskOf(re, masks))
            : new TplPlay(re, rs.Duration, Why: "→ RELOAD_END after START (HEURISTIC: no mag_in event)", Mask: MaskOf(re, masks)));
      }
    }

    // 3. Shots: each event on the character's Shoot track → shootAnimation (+ blueprints hooked on OnShoot, e.g. SHOOT_AUX).
    var shoot = (shootAnimation is { Length: > 0 } ? anim.Find(shootAnimation) : null) ?? anim.Find("SHOOT_1")
      ?? anim.Sequences.FirstOrDefault(s => s.Name.StartsWith("SHOOT_", StringComparison.OrdinalIgnoreCase) && !s.Name.Equals("SHOOT_AUX", StringComparison.OrdinalIgnoreCase));
    var onShoot = known.Where(t => !t.Stop && !t.IsEvent && t.Trigger.EndsWith(".OnShoot", StringComparison.OrdinalIgnoreCase)).ToList();
    foreach (var track in character.EventTracks.Where(t => t.Name.Equals("Shoot", StringComparison.OrdinalIgnoreCase)))
      foreach (var ev in track.Events)
      {
        var at = ev.Start * duration;
        foreach (var t in onShoot)
          plays.Add(new TplPlay(anim.Find(t.Anim)!, at, Loop: t.Loops, Why: $"shot → {t.Trigger[1..]} → {t.Anim} (blueprint DATA; shot = Shoot track HEURISTIC)", Mask: MaskOf(anim.Find(t.Anim)!, masks)));
        if (shoot is not null)
          plays.Add(new TplPlay(shoot, at, Why: $"shot → shootAnimation {shoot.Name} (FSM DATA; shot = Shoot track HEURISTIC)", Mask: MaskOf(shoot, masks)));
      }

    // 3b. Player firing clips carry no Shoot track (only 55 NPC trooper clips do). The player network (primaris.nmb) fires through
    // tacticus_shtN_shoot_aim|blind, picked by wpn_type (R38, INFERRED); the shot is the ho_shoot pulse, not a clip event.
    // Any other clip named *shoot*/*fire* (not reload) keeps the old name rule (HEURISTIC).
    var playerFire = PlayerFireClips.IsFireClip(character.Name);
    if (shoot is not null && !plays.Any(p => p.Seq == shoot) && character.Name is { } clipName && reloadTrack is null
        && (playerFire || (Regex.IsMatch(clipName, "shoot|fire", RegexOptions.IgnoreCase) && !clipName.Contains("reload", StringComparison.OrdinalIgnoreCase))))
    {
      var clipWhy = playerFire ? "player fire clip (primaris.nmb wpn_type switch, INFERRED)" : "firing clip (name HEURISTIC)";
      if (playerFire && wpnType is { } wt)
      {
        var pick = PlayerFireClips.For(wt);
        var sht = PlayerFireClips.ShtIndex(clipName);
        if (sht != PlayerFireClips.ShtIndex(pick.Aim) && sht != PlayerFireClips.ShtIndex(pick.Blind))
          notes.Add($"wpnType {wt}: the network picks {pick.Aim} / {pick.Blind} ({pick.Evidence}), not {Path.GetFileName(clipName)}.");
      }
      var rate = fireRates?.FirstOrDefault(r => r.Interval > 0);
      if (rate is not null && duration > 0)
      {
        // FixedFireRate (DATA): one shot every 60/shotsPerMin s; each shot restarts shootAnimation (cut at the next shot).
        var step = (float)rate.Interval;
        var count = Math.Min(512, Math.Max(1, (int)Math.Ceiling(duration / step - 1e-4f)));
        for (var k = 0; k < count; k++)
          plays.Add(new TplPlay(shoot, k * step, End: (k + 1) * step,
            Why: k == 0 ? $"{clipWhy} → {shoot.Name} every {step:0.000}s = 60/{rate.ShotsPerMin:0} shotsPerMin ({rate.Uid}.{rate.Barrel}, {rate.ShootLogic ?? "?"}; DATA)" : "", Mask: MaskOf(shoot, masks)));
        if (fireRates!.Select(r => r.ShotsPerMin).Distinct().Count() > 1)
          notes.Add($"{rate.Uid}: barrels differ in shotsPerMin ({string.Join(", ", fireRates!.Select(r => $"{r.Barrel} {r.ShotsPerMin:0}"))}); the first one is used.");
        if (rate.AffectsShootAnim == true)
          notes.Add("fireRateAffectShootAnim = True: the game also scales the shoot sequence to the fire rate (not applied here).");
      }
      else
        plays.Add(new TplPlay(shoot, 0f, Loop: true, Why: $"{clipWhy} → shootAnimation {shoot.Name} looped (HEURISTIC: no Shoot track or firearm_library rate; rate = sequence length)", Mask: MaskOf(shoot, masks)));
    }

        // 4. Morpheme events named by blueprint triggers (DATA): start at the event (or its end for OnDeactivate/OnExit).
    var events = character.EventTracks.SelectMany(t => t.Events).SelectMany(e => e.Tags.Select(tag => (Tag: tag, Ev: e)))
      .OrderBy(x => x.Ev.Start).ToList();
    foreach (var (tag, ev) in events)
      foreach (var t in known.Where(k => k.IsEvent && k.Trigger.Equals(tag, StringComparison.OrdinalIgnoreCase)))
      {
        var at = (t.AtEnd ? ev.Start + ev.Duration : ev.Start) * duration;
        var seq = anim.Find(t.Anim)!;
        if (t.Stop)
        {
          for (var i = plays.Count - 1; i >= 0; i--)
            if (plays[i].Seq.Name.Equals(seq.Name, StringComparison.OrdinalIgnoreCase) && plays[i].Start <= at)
            {
              plays[i] = plays[i] with { End = at };
              break;
            }

          continue;
        }

        plays.Add(new TplPlay(seq, at, Loop: t.Loops, Hold: !t.Loops && t.Playback.Equals("clamp", StringComparison.OrdinalIgnoreCase),
          Why: $"event {tag}{(t.AtEnd ? " (end)" : "")} → {seq.Name} (blueprint, DATA)", Mask: MaskOf(seq, masks)));
      }

    if (plays.Count == (holding is null ? 0 : 1) && anim.Sequences.Count > 0)
      notes.Add("No character event on this clip maps to a prop sequence; the prop stays in its holding state.");
    return new PropAutoPlan(plays, notes);
  }
}
