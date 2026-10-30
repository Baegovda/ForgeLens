// R71 (t146u): the 2-line model summary drawn as coloured Runs: each item has its own hue, labels dim, numbers bright,
// separators muted. Text content is identical to ExportedMesh.Summary (only colour changes); Details / tooltip unchanged.
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  /// <summary>Item key (matched on the item text) → bright colour for its numbers. Order = first match wins.</summary>
  internal static readonly (string Key, string Hex)[] SummaryColours =
  {
    ("LOD 0", "#7FB8FF"),      // blue
    ("verts", "#6FD3C6"),      // teal
    ("tris", "#9BD77A"),       // green
    ("parts", "#E8C86A"),      // amber
    ("bones", "#F0A070"),      // orange
    ("Textures", "#D59CF0"),   // violet
    ("submeshes", "#F08FB0"),  // pink
    ("LODs", "#8FA8FF"),       // periwinkle
    ("unskinned", "#B8B8B8"),  // grey
  };

  private static readonly IBrush SummaryLabel = new SolidColorBrush(Color.Parse("#9AA3AD"));
  private static readonly IBrush SummarySep = new SolidColorBrush(Color.Parse("#5A6068"));
  private static readonly Regex SummaryNumber = new(@"\d[\d,./]*", RegexOptions.CultureInvariant);

  /// <summary>Builds the Inlines for <paramref name="summary"/>; the concatenated Run texts equal the summary string.</summary>
  internal static InlineCollection SummaryInlines(string summary)
  {
    var inlines = new InlineCollection();
    var lines = summary.Split('\n');
    for (var l = 0; l < lines.Length; l++)
    {
      if (l > 0) inlines.Add(new LineBreak());
      var items = lines[l].Split("  ·  ");
      for (var i = 0; i < items.Length; i++)
      {
        if (i > 0) inlines.Add(new Run("  ·  ") { Foreground = SummarySep });
        var item = items[i];
        var hex = SummaryColours.FirstOrDefault(c => item.Contains(c.Key, StringComparison.Ordinal)).Hex;
        var bright = hex is null ? SummaryLabel : new SolidColorBrush(Color.Parse(hex));
        var pos = 0;
        foreach (Match m in SummaryNumber.Matches(item))
        {
          if (m.Index > pos) inlines.Add(new Run(item[pos..m.Index]) { Foreground = SummaryLabel });
          inlines.Add(new Run(m.Value) { Foreground = bright, FontWeight = FontWeight.SemiBold });
          pos = m.Index + m.Length;
        }
        if (pos < item.Length) inlines.Add(new Run(item[pos..]) { Foreground = hex is not null && pos == 0 ? bright : SummaryLabel });
      }
    }
    return inlines;
  }

  private void ShowSummaryText(string summary, bool coloured)
  {
    if (coloured)
    {
      PreviewNote.Text = null;
      PreviewNote.Inlines = SummaryInlines(summary);
    }
    else
    {
      PreviewNote.Inlines = null;
      PreviewNote.Text = summary;
    }
  }
}
