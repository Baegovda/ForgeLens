using LibSaber.SM2.Assets.Catalog;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>
///   Lists retail <c>.sani</c> headers and picks the clips whose bone names overlap a skeleton.
///   Matching is by name. A clip does not have to use the same index order as the model.
/// </summary>
public static class SaniIndex
{

  /// <summary>Shared names required before a clip is offered for a model. A named <c>--anim</c> bypasses this.</summary>
  public const int MinimumSharedBones = 8;

  public readonly record struct Header(int Index, string Path, float Duration, float SampleRate, IReadOnlyList<string> Bones);

  public static int SharedBones(IEnumerable<string> skeleton, IReadOnlyList<string> clipBones)
  {
    var names = skeleton as HashSet<string> ?? new HashSet<string>(skeleton, StringComparer.Ordinal);
    var count = 0;
    foreach (var bone in clipBones)
    {
      if (names.Contains(bone))
        count++;
    }

    return count;
  }

  public static List<Header> ReadHeaders(GameCatalog catalog, ArchiveStore store)
  {
    var query = catalog.Query(null, ".sani", AssetFilter.Animations);
    var headers = new List<Header>();
    for (var row = 0; row < query.Length; row++)
    {
      var index = query.IndexAt(row);
      var file = catalog.GetFile(index);
      if (!file.Extension.Equals(".sani", StringComparison.OrdinalIgnoreCase))
        continue;
      byte[] bytes;
      try
      {
        bytes = store.Read(index);
      }
      catch (Exception)
      {
        continue;
      }

      if (!SaniReader.TryPeek(bytes, out var duration, out var rate, out var bones))
        continue;
      headers.Add(new Header(index, file.Path, duration, rate, bones));
    }

    return headers;
  }

  public static List<Header> Compatible(IReadOnlyList<Header> headers, IReadOnlyList<string> skeleton, int minimum = MinimumSharedBones)
  {
    var names = new HashSet<string>(skeleton, StringComparer.Ordinal);
    return headers
      .Select(header => (header, shared: SharedBones(names, header.Bones)))
      .Where(item => item.shared >= minimum)
      .OrderByDescending(item => item.shared)
      .ThenBy(item => item.header.Path, StringComparer.OrdinalIgnoreCase)
      .Select(item => item.header)
      .ToList();
  }

  /// <summary>
  ///   propanim: clips stored under the model's own folder (<c>tpl/x.tpl/animations/*.sani</c>), whatever their track names
  ///   (level props often have unnamed tracks, which <see cref="Compatible" /> cannot count; they bind by animNmb rank).
  /// </summary>
  public static List<Header> InFolder(IReadOnlyList<Header> headers, string modelPath)
  {
    var dir = System.IO.Path.GetDirectoryName(modelPath.Replace('\\', '/'))?.Replace('\\', '/');
    if (string.IsNullOrEmpty(dir)) return [];
    var prefix = dir.TrimEnd('/') + "/";
    return headers.Where(h => h.Path.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      .OrderBy(h => h.Path, StringComparer.OrdinalIgnoreCase).ToList();
  }

  public static List<Header> Match(IReadOnlyList<Header> headers, string token)
  {
    var needle = token.Replace('\\', '/').Trim();
    if (needle.IndexOfAny(new[] { '*', '?' }) >= 0)
    {
      // Glob on the whole .sani path: * = any run (also across /), ? = one character.
      var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(needle).Replace("\\*", ".*").Replace("\\?", ".") + "$";
      var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
      return headers
        .Where(header => regex.IsMatch(header.Path))
        .OrderBy(header => header.Path, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    return headers
      .Where(header => header.Path.Contains(needle, StringComparison.OrdinalIgnoreCase))
      .OrderBy(header => header.Path, StringComparer.OrdinalIgnoreCase)
      .ToList();
  }

}
