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
      _ => $"{texture} · {tris:N0} tris",
    };
  }

  public string Label => _part.Name;

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
