using System.ComponentModel;
using System.Runtime.CompilerServices;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public sealed class MeshToggle : INotifyPropertyChanged
{
  private readonly MeshPart _part;
  private bool _suppress;

  public MeshToggle(MeshPart part)
  {
    _part = part;
    var tris = Math.Max(0, part.IndexCount / 3);
    var texture = string.IsNullOrWhiteSpace(part.Texture) ? "no texture" : part.Texture;
    Tip = part.HideReason switch
    {
      "LOD" => $"{texture} · {tris:N0} tris · Index hides this LOD",
      "volume" => $"{texture} · {tris:N0} tris · Index hides this collision volume",
      "clothSim" => $"{texture} · {tris:N0} tris · cloth simulation shell (the game never draws it)",
      _ => $"{texture} · {tris:N0} tris",
    };
  }

  public string Label => _part.Name;

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
    return roots;
  }

  public string Tip { get; }

  public bool Visible
  {
    get => _part.Visible;
    set
    {
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
