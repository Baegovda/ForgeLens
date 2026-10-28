using Avalonia.Controls;
using Avalonia.Input;

namespace LibSaber.SM2Browser;

/// <summary>
///   ui-43: a ComboBox that steps its selection with the mouse wheel while the pointer is over the CLOSED box, focused or
///   not (Avalonia's own ComboBox only steps when it has keyboard focus, and PointerWheelChanged is a bubble-only event,
///   so the mtl1-42 tunnel handler never ran and the wheel looked blocked). Wheel up = previous row, wheel down = next
///   row of the current ItemsSource (so a filtered clip list stays filtered). An open dropdown scrolls as usual.
///   <see cref="WheelStepped"/> fires after the selection moved, so the owner can e.g. start playback.
/// </summary>
public class WheelComboBox : ComboBox
{
  protected override Type StyleKeyOverride => typeof(ComboBox);

  private double _accumulated;

  /// <summary>Raised after a wheel notch changed <see cref="SelectingItemsControl.SelectedIndex"/>.</summary>
  public event EventHandler? WheelStepped;

  /// <summary>Raised just before the wheel changes the selection (the SelectionChanged handlers run in between).</summary>
  public event EventHandler? WheelStepping;

  protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
  {
    if (IsDropDownOpen)
    {
      base.OnPointerWheelChanged(e);
      return;
    }

    e.Handled = true;
    if (!IsEnabled || ItemCount == 0 || e.Delta.Y == 0)
      return;
    // Mouse notches are ±1; precision touchpads send fractions, which add up to one step.
    _accumulated += e.Delta.Y;
    if (Math.Abs(_accumulated) < 0.99)
      return;
    var steps = (int)Math.Truncate(_accumulated + Math.Sign(_accumulated) * 0.01);
    _accumulated = 0;
    var next = Math.Clamp(SelectedIndex - steps, 0, ItemCount - 1);
    if (next == SelectedIndex)
      return;
    WheelStepping?.Invoke(this, EventArgs.Empty);
    SelectedIndex = next;
    WheelStepped?.Invoke(this, EventArgs.Empty);
  }

  /// <summary>The step a wheel delta makes from <paramref name="index"/> (pure, for tests and the handler).</summary>
  public static int Step(int index, int count, double deltaY)
  {
    if (count <= 0 || deltaY == 0)
      return index;
    return Math.Clamp(index + (deltaY > 0 ? -1 : 1), 0, count - 1);
  }
}
