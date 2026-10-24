using LibSaber.SpaceMarine2.Structures.Resources;

namespace LibSaber.SM2Extractor;

public static class ArchiveLocator
{

  public static IReadOnlyList<string> Find(string inputPath, bool recursive)
  {
    if (string.IsNullOrWhiteSpace(inputPath))
      throw new ArgumentException("Archive path is empty.", nameof(inputPath));

    if (File.Exists(inputPath))
    {
      if (!fioZIP_FILE.IsSupportedArchive(inputPath))
        throw new InvalidDataException(
          $"File is not a .pak or .pck archive: {inputPath}");

      return new[] { Path.GetFullPath(inputPath) };
    }

    if (!Directory.Exists(inputPath))
      throw new FileNotFoundException($"Path not found: {inputPath}", inputPath);

    var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    var comparison = OperatingSystem.IsWindows()
      ? StringComparer.OrdinalIgnoreCase
      : StringComparer.Ordinal;

    return Directory.EnumerateFiles(inputPath, "*", option)
      .Where(fioZIP_FILE.IsSupportedArchive)
      .Select(Path.GetFullPath)
      .Distinct(comparison)
      .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
      .ToArray();
  }

}
