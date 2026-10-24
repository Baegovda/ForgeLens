using System.Text.RegularExpressions;

namespace LibSaber.SM2Extractor;

public static class ArchivePathFilter
{

  public static bool Matches(string relativePath, IReadOnlyList<string> filters)
  {
    if (filters.Count == 0)
      return true;

    var path = Normalize(relativePath).TrimStart('/');
    var slash = path.LastIndexOf('/');
    var fileName = slash < 0 ? path : path[(slash + 1)..];

    foreach (var raw in filters)
    {
      var filter = Normalize(raw).TrimStart('/');
      if (filter.Length == 0)
        continue;

      if (filter.StartsWith('.') && filter.IndexOfAny(new[] { '*', '?', '/' }) < 0)
        filter = "*" + filter;

      var candidate = filter.Contains('/') ? path : fileName;
      if (IsGlobMatch(candidate, filter))
        return true;
    }

    return false;
  }

  public static string Normalize(string path)
    => path.Replace('\\', '/');

  private static bool IsGlobMatch(string text, string glob)
  {
    var pattern = "^" + GlobToRegex(glob) + "$";
    return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
  }

  private static string GlobToRegex(string glob)
  {
    var builder = new System.Text.StringBuilder();
    for (var i = 0; i < glob.Length; i++)
    {
      var current = glob[i];
      if (current == '*')
      {
        var isDouble = i + 1 < glob.Length && glob[i + 1] == '*';
        if (isDouble)
        {
          i++;
          if (i + 1 < glob.Length && glob[i + 1] == '/')
            i++;

          builder.Append("(?:.*/)?");
        }
        else
        {
          builder.Append("[^/]*");
        }
      }
      else if (current == '?')
      {
        builder.Append("[^/]");
      }
      else
      {
        builder.Append(Regex.Escape(current.ToString()));
      }
    }

    return builder.ToString();
  }

}
