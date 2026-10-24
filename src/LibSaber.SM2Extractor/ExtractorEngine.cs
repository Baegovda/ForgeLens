using LibSaber.SpaceMarine2.Files;

namespace LibSaber.SM2Extractor;

public sealed class ExtractorEngine
{

  private readonly TextWriter _stdout;
  private readonly TextWriter _stderr;

  public ExtractorEngine(TextWriter stdout, TextWriter stderr)
  {
    _stdout = stdout;
    _stderr = stderr;
  }

  public int Execute(ExtractorOptions options)
  {
    var archives = ArchiveLocator.Find(options.InputPath, options.Recursive);
    if (archives.Count == 0)
    {
      _stderr.WriteLine($"No .pak or .pck archives found at '{options.InputPath}'.");
      return 1;
    }

    var opened = new List<OpenedArchive>();
    var archivesSkipped = 0;
    foreach (var archive in archives)
    {
      SM2PckFileDevice? device = null;
      try
      {
        device = new SM2PckFileDevice(archive);
        if (!device.Initialize())
          throw new InvalidDataException("Archive failed to initialize.");

        opened.Add(new OpenedArchive(archive, device));
        device = null;
      }
      catch (Exception ex)
      {
        archivesSkipped++;
        _stderr.WriteLine($"SKIP archive {archive}: {ex.Message}");
        device?.Dispose();
      }
    }

    try
    {
      return options.Verb switch
      {
        ExtractorVerb.List => List(opened, options, archivesSkipped),
        ExtractorVerb.Extract => Extract(opened, options, archivesSkipped),
        _ => 1
      };
    }
    finally
    {
      foreach (var archive in opened)
        archive.Device.Dispose();
    }
  }

  private int List(List<OpenedArchive> archives, ExtractorOptions options, int archivesSkipped)
  {
    var matched = 0;
    var filesSkipped = 0;
    if (!options.Quiet)
      _stdout.WriteLine("Size\tCompressed\tMethod\tType\tArchive\tPath");

    foreach (var archive in archives)
    {
      foreach (var file in Enumerate(archive))
      {
        try
        {
          if (!ArchivePathFilter.Matches(file.RelativePath, options.Filters))
            continue;

          matched++;
          if (!options.Quiet)
          {
            _stdout.WriteLine(
              $"{file.Size}\t{file.CompressedSize}\t{file.Method}\t{file.Type}\t{file.ArchiveName}\t{file.RelativePath}");
          }
        }
        catch (Exception ex)
        {
          filesSkipped++;
          _stderr.WriteLine($"SKIP {file.ArchiveName}:{file.RelativePath}: {ex.Message}");
        }
      }
    }

    _stdout.WriteLine(
      $"Summary: archivesOpened={archives.Count} archivesSkipped={archivesSkipped} filesListed={matched} filesSkipped={filesSkipped}");

    return archivesSkipped == 0 && filesSkipped == 0 ? 0 : 2;
  }

  private int Extract(List<OpenedArchive> archives, ExtractorOptions options, int archivesSkipped)
  {
    var outputRoot = options.OutputPath ?? throw new InvalidOperationException("Output path is required.");
    Directory.CreateDirectory(outputRoot);

    var extracted = 0;
    var filesSkipped = 0;
    var existing = 0;
    long bytesWritten = 0;

    foreach (var archive in archives)
    {
      var destinationRoot = outputRoot;
      if (options.PrefixArchive)
      {
        destinationRoot = Path.Combine(outputRoot, ArchiveOutputPath.PrefixFor(options.InputPath, archive.Path));
        Directory.CreateDirectory(destinationRoot);
      }

      foreach (var file in Enumerate(archive))
      {
        if (file.IsDirectory)
          continue;

        if (!ArchivePathFilter.Matches(file.RelativePath, options.Filters))
          continue;

        if (!ArchiveOutputPath.TryMap(destinationRoot, file.RelativePath, out var destination, out var pathError))
        {
          filesSkipped++;
          _stderr.WriteLine($"SKIP {file.ArchiveName}:{file.RelativePath}: {pathError}");
          continue;
        }

        try
        {
          if (!options.Overwrite && File.Exists(destination))
          {
            existing++;
            if (!options.Quiet)
              _stdout.WriteLine($"Exists {RelativeToOutput(outputRoot, destination)}");
            continue;
          }

          var written = WriteFile(file.Node, destination);
          extracted++;
          bytesWritten += written;
          if (!options.Quiet)
            _stdout.WriteLine($"Wrote {RelativeToOutput(outputRoot, destination)}");
        }
        catch (Exception ex)
        {
          filesSkipped++;
          _stderr.WriteLine($"SKIP {file.ArchiveName}:{file.RelativePath}: {ex.Message}");
        }
      }
    }

    _stdout.WriteLine(
      $"Summary: archivesOpened={archives.Count} archivesSkipped={archivesSkipped} filesExtracted={extracted} filesSkipped={filesSkipped} filesExisting={existing} bytesWritten={bytesWritten}");

    return archivesSkipped == 0 && filesSkipped == 0 ? 0 : 2;
  }

  private static long WriteFile(SM2FileSystemNode node, string destination)
  {
    var directory = Path.GetDirectoryName(destination);
    if (!string.IsNullOrEmpty(directory))
      Directory.CreateDirectory(directory);

    var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
    try
    {
      using (var source = node.Open())
      using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
      {
        source.CopyTo(output);
      }

      File.Move(temporary, destination, overwrite: true);
      return new FileInfo(destination).Length;
    }
    catch
    {
      if (File.Exists(temporary))
        File.Delete(temporary);
      throw;
    }
  }

  private static string RelativeToOutput(string outputRoot, string destination)
  {
    var relative = Path.GetRelativePath(outputRoot, destination);
    return ArchivePathFilter.Normalize(relative);
  }

  private static IEnumerable<ListedFile> Enumerate(OpenedArchive archive)
  {
    foreach (var node in archive.Device.EnumerateFiles())
    {
      if (node is not SM2FileSystemNode smNode)
        continue;

      var relative = smNode.RelativePath;
      yield return new ListedFile(
        archive.Path,
        Path.GetFileName(archive.Path),
        relative,
        ArchiveOutputPath.IsDirectory(relative),
        smNode.SizeInBytes,
        smNode.Entry.CompressedSize,
        FormatMethod(smNode.Entry.CompressMethod),
        ArchiveOutputPath.FileType(relative),
        smNode);
    }
  }

  private static string FormatMethod(LibSaber.SpaceMarine2.Structures.Resources.fioZIP_CACHE_FILE.COMPRESS_METHOD method)
  {
    return method switch
    {
      LibSaber.SpaceMarine2.Structures.Resources.fioZIP_CACHE_FILE.COMPRESS_METHOD.STORE => "Store",
      LibSaber.SpaceMarine2.Structures.Resources.fioZIP_CACHE_FILE.COMPRESS_METHOD.DEFLATE => "Deflate",
      LibSaber.SpaceMarine2.Structures.Resources.fioZIP_CACHE_FILE.COMPRESS_METHOD.DEFLATE64 => "Deflate64",
      _ => method.ToString()
    };
  }

  private sealed class OpenedArchive
  {
    public OpenedArchive(string path, SM2PckFileDevice device)
    {
      Path = path;
      Device = device;
    }

    public string Path { get; }
    public SM2PckFileDevice Device { get; }
  }

  private readonly record struct ListedFile(
    string ArchivePath,
    string ArchiveName,
    string RelativePath,
    bool IsDirectory,
    long Size,
    long CompressedSize,
    string Method,
    string Type,
    SM2FileSystemNode Node);

}
