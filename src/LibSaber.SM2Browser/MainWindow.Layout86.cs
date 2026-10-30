using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace LibSaber.SM2Browser;

// R87: at small window sizes (1600x900, 1280x720) the centre file column and the wrapped toolbars ate the 3D view
// (436x33 px). Narrow windows get a smaller centre star (the saved ratio is kept for wide windows and for saving);
// the toolbar stack is capped to 30% of the viewer height and scrolls instead of pushing the viewport out.
public partial class MainWindow
{
  private bool _layoutNarrow;
  private double _filesStarSaved = 1.35;
  private const double NarrowWidth = 1900, NarrowFilesStar = 0.8;

  private void Layout86Init()
  {
    var main = MainColumns.ColumnDefinitions;
    if (main[2].Width.IsStar) _filesStarSaved = main[2].Width.Value;
    main[2].MinWidth = 200;
    main[4].MinWidth = 480;
    MainColumns.SizeChanged += (_, _) => Layout86Apply();
    ViewerColumns.SizeChanged += (_, _) => Layout86Apply();
    Layout86Apply();
  }

  private void Layout86Apply()
  {
    var main = MainColumns.ColumnDefinitions;
    var w = MainColumns.Bounds.Width;
    if (w > 0)
    {
      var narrow = w < NarrowWidth;
      if (narrow != _layoutNarrow)
      {
        if (narrow && main[2].Width.IsStar) _filesStarSaved = main[2].Width.Value;
        _layoutNarrow = narrow;
        var star = narrow ? Math.Min(_filesStarSaved, NarrowFilesStar) : _filesStarSaved;
        main[2].Width = new GridLength(star, GridUnitType.Star);
      }
    }
    var h = ViewerColumns.Bounds.Height;
    if (h > 0) TopBars.MaxHeight = Math.Max(40, h * 0.30);
  }

  internal string Layout86Snapshot()
    => $"R87 layout win {Bounds.Width:0}x{Bounds.Height:0} cols {string.Join("/", MainColumns.ColumnDefinitions.Select(c => Math.Round(c.ActualWidth)))} viewer {string.Join("/", ViewerColumns.ColumnDefinitions.Select(c => Math.Round(c.ActualWidth)))} topbars {TopBars.Bounds.Height:0}/{TopBars.MaxHeight:0} view {ViewBorder.Bounds.Width:0}x{ViewBorder.Bounds.Height:0} gpu {GpuView.Bounds.Width:0}x{GpuView.Bounds.Height:0} narrow {_layoutNarrow} filesStarSaved {_filesStarSaved}";
}

