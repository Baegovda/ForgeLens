using System.ComponentModel;
using System.Runtime.CompilerServices;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public sealed class MeshToggle : INotifyPropertyChanged
{
  private readonly MeshPart? _part;
  private bool _suppress;

  /// <summary>
  ///   R71: the one place that decides whether a part is a non-render entry (collision / occluder / shadow-caster
  ///   volume, cloth simulation shell, placeholder sfx emitter/trail source). Data-driven: only the reader's own
  ///   HideReason flags count (IndexMeshVisibility "volume", "clothSim", MaterialMaps "sfxSource"); "LOD", "sfx"
  ///   (shown while an effect drives it) and "variant" (other armor pieces) are real geometry and stay on top.
  ///   Conservative: an unflagged part is always treated as visible geometry.
  /// </summary>
  public static bool IsNonRender(MeshPart part)
    => part.HideReason is "volume" or "clothSim" or "sfxSource"
       // R75 HEURISTIC (name only, R71 inventory): navigation helpers nav_silhouette / navmesh_floor (still drawn by default)
       || part.Name.StartsWith("nav_", StringComparison.OrdinalIgnoreCase) || part.Name.StartsWith("navmesh", StringComparison.OrdinalIgnoreCase);

  public const string HelperGroupTitle = "Collision / Helpers";

  public bool IsGroup => _part is null;
  public MeshPart? Part => _part;
  public bool NonRender => _part is not null && IsNonRender(_part);

  private readonly string? _label;

  /// <summary>R71: group row ("Collision / Helpers (N)"); its checkbox shows / hides every part under it.</summary>
  private MeshToggle(string label, List<MeshToggle> children)
  {
    _label = label;
    Children.AddRange(children);
    Tip = "Not drawn by the game: collision / occluder / shadow-caster volumes, cloth simulation shells, sfx emitter helpers. Check to show them.";
    foreach (var child in AllParts())
      child.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Visible)) OnPropertyChanged(nameof(Visible)); };
  }

  public MeshToggle(MeshPart part)
  {
    _part = part;
    var tris = Math.Max(0, part.IndexCount / 3);
    var texture = string.IsNullOrWhiteSpace(part.Texture) ? "no texture" : part.Texture;
    Tip = part.HideReason switch
    {
      "LOD" => $"{texture} · {tris:N0} tris · Index hides this LOD",
      "volume" => $"{texture} · {tris:N0} tris · Index hides this collision volume",
      "sfx" => $"{texture} · {tris:N0} tris · sfx shader card (td preset \"sfx\"): only shown while its effect drives it; drawn with the sfx blend",
      "sfxSource" => $"{texture} · {tris:N0} tris · sfx helper with the placeholder material (emitter / trail source), not drawn by the game",
      "clothSim" => $"{texture} · {tris:N0} tris · cloth simulation shell (the game never draws it)",
      _ => $"{texture} · {tris:N0} tris",
    };
  }

  public string Label => _part?.Name ?? _label ?? "";

  /// <summary>LOD variants of this mesh (<c>_LOD1</c>.. <c>_LODn</c>), shown collapsed under it.</summary>
  public List<MeshToggle> Children { get; } = new();

  /// <summary>Base name with a trailing LOD suffix removed (<c>body_LOD2</c> becomes <c>body</c>).</summary>
  public static string BaseName(string name)
    => LodSuffix.Replace(name, "");

  public static bool IsLod(string name)
    => LodSuffix.IsMatch(name);

  private static readonly System.Text.RegularExpressions.Regex LodSuffix =
    new(@"[_\.\- ]?lod_?\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

  /// <summary>Nests LOD variants under their base mesh. A group without a base row promotes its lowest LOD.</summary>
  public static List<MeshToggle> Group(IEnumerable<MeshToggle> toggles)
  {
    var roots = new List<MeshToggle>();
    foreach (var group in toggles.GroupBy(t => BaseName(t.Label), StringComparer.OrdinalIgnoreCase))
    {
      var items = group.OrderBy(t => IsLod(t.Label) ? 1 : 0).ThenBy(t => t.Label, StringComparer.OrdinalIgnoreCase).ToList();
      var root = items[0];
      root.Children.Clear();
      root.Children.AddRange(items.Skip(1));
      roots.Add(root);
    }

    roots.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
    // R71: non-render roots (a base row decides for its LOD children) go into one collapsed group at the end.
    var helpers = roots.Where(r => r.NonRender).ToList();
    if (helpers.Count == 0)
      return roots;
    var visible = roots.Where(r => !r.NonRender).ToList();
    var helperGroup = new MeshToggle($"{HelperGroupTitle} ({helpers.Sum(h => 1 + h.Children.Count)})", helpers);
    visible.Add(helperGroup);
    return visible;
  }

  public string Tip { get; }

  private IEnumerable<MeshToggle> AllParts()
    => Children.SelectMany(c => c.IsGroup ? c.AllParts() : new[] { c }.Concat(c.Children));

  public bool Visible
  {
    get => _part?.Visible ?? AllParts().Any(c => c.Visible);
    set
    {
      if (_part is null)
      {
        var parts = AllParts().ToList();
        foreach (var c in parts)
          c.SetVisible(value);
        OnPropertyChanged();
        if (!_suppress && parts.Count > 0)
          parts[0].Toggled?.Invoke(parts[0], EventArgs.Empty);
        return;
      }

      if (_part.Visible == value)
        return;
      _part.Visible = value;
      OnPropertyChanged();
      if (!_suppress)
        Toggled?.Invoke(this, EventArgs.Empty);
    }
  }

  public event EventHandler? Toggled;

  public event PropertyChangedEventHandler? PropertyChanged;

  public void SetVisible(bool value)
  {
    _suppress = true;
    Visible = value;
    _suppress = false;
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null)
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
