namespace LibSaber.SM2Extractor;

public static class ArchiveOutputPath
{

  public static bool IsDirectory(string relativePath)
  {
    if (string.IsNullOrWhiteSpace(relativePath))
      return true;

    return relativePath.EndsWith('/') || relativePath.EndsWith('\\');
  }

  public static string FileType(string relativePath)
  {
    if (IsDirectory(relativePath))
      return "directory";

    var normalized = ArchivePathFilter.Normalize(relativePath);
    var slash = normalized.LastIndexOf('/');
    var name = slash < 0 ? normalized : normalized[(slash + 1)..];

    // .pct.resource and similar pairs carry the real asset type in both suffixes.
    const string resourceSuffix = ".resource";
    if (name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase) && name.Length > resourceSuffix.Length)
    {
      var stem = name[..^resourceSuffix.Length];
      var dot = stem.LastIndexOf('.');
      if (dot >= 0)
        return name[dot..];
    }

    var extension = Path.GetExtension(name);
    return string.IsNullOrEmpty(extension) ? "(none)" : extension;
  }

  public static bool TryMap(
    string outputRoot,
    string archiveRelativePath,
    out string outputPath,
    out string error)
  {
    outputPath = "";
    error = "";

    var normalized = ArchivePathFilter.Normalize(archiveRelativePath).Trim();
    while (normalized.StartsWith('/'))
      normalized = normalized[1..];

    if (normalized.Length == 0 || normalized.EndsWith('/'))
    {
      error = "Entry path is empty or is a directory.";
      return false;
    }

    var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
    {
      error = "Entry path is empty.";
      return false;
    }

    foreach (var part in parts)
    {
      if (part is "." or "..")
      {
        error = "Entry path escapes the archive root.";
        return false;
      }

      if (HasInvalidNameChar(part))
      {
        error = "Entry path contains invalid file name characters.";
        return false;
      }
    }

    var combined = Path.Combine(parts.Prepend(outputRoot).ToArray());
    var fullRoot = Path.GetFullPath(outputRoot);
    var fullOutput = Path.GetFullPath(combined);
    var rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
      + Path.DirectorySeparatorChar;
    var comparison = OperatingSystem.IsWindows()
      ? StringComparison.OrdinalIgnoreCase
      : StringComparison.Ordinal;

    if (!fullOutput.StartsWith(rootPrefix, comparison))
    {
      error = "Entry path escapes the output directory.";
      return false;
    }

    outputPath = fullOutput;
    return true;
  }

  public static string PrefixFor(string inputPath, string archivePath)
  {
    var fullArchive = Path.GetFullPath(archivePath);
    var fullInput = Path.GetFullPath(inputPath);
    string relative;
    if (File.Exists(fullInput))
    {
      relative = Path.GetFileName(fullArchive);
    }
    else
    {
      relative = Path.GetRelativePath(fullInput, fullArchive);
    }

    var withoutExtension = Path.ChangeExtension(relative, null) ?? relative;
    return withoutExtension
      .Replace(Path.DirectorySeparatorChar, '_')
      .Replace(Path.AltDirectorySeparatorChar, '_')
      .Replace(':', '_');
  }

  private static bool HasInvalidNameChar(string part)
  {
    if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
      return true;

    // Reject characters Windows cannot store even when this build is running elsewhere.
    foreach (var ch in part)
    {
      if (ch is '<' or '>' or ':' or '"' or '|' or '?' or '*' || char.IsControl(ch))
        return true;
    }

    return false;
  }

}
