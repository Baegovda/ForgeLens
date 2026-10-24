namespace LibSaber.SM2Extractor;

public sealed class ExtractorOptions
{

  public const string Usage =
"""
LibSaber.SM2Extractor — list and extract Warhammer 40,000: Space Marine 2 archives

Usage:
  LibSaber.SM2Extractor list <archive-or-folder> [options]
  LibSaber.SM2Extractor extract <archive-or-folder> --output <folder> [options]

The game ships .pak archives with a sibling cache of the same name plus ".cache"
(client_pc\something.pak and client_pc\something.pak.cache). .pck files with a
sibling .pck.cache are accepted too.

Options:
  -o, --output <folder>   Destination directory. Required for extract.
  -f, --filter <pattern>  Include filter, matched against the archive-relative path.
                          Repeat to combine with OR. A leading dot is an extension
                          (".pct" matches "*.pct"). Examples: *.pct, .tpl, characters/**/*.lg
  --no-recursive          When the input is a folder, do not search subfolders.
  --prefix-archive        Write each archive under <output>\<archive name>\.
  --no-overwrite          Leave files that are already in the output folder.
  -q, --quiet             Print the summary only. Failures still go to stderr.
  -h, --help              Show this help.

Exit codes:
  0  Finished with no skipped archives or files.
  1  Bad arguments, missing input, or no archives found.
  2  Finished, but at least one archive or file was skipped.

Examples:
  LibSaber.SM2Extractor list "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc" --filter *.pct
  LibSaber.SM2Extractor extract "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc" --output C:\sm2\extract --filter *.pct --filter *.tpl
""";

  public ExtractorVerb Verb { get; private init; }
  public string InputPath { get; private init; } = "";
  public string? OutputPath { get; private init; }
  public IReadOnlyList<string> Filters { get; private init; } = Array.Empty<string>();
  public bool Recursive { get; private init; } = true;
  public bool PrefixArchive { get; private init; }
  public bool Overwrite { get; private init; } = true;
  public bool Quiet { get; private init; }

  public static ExtractorOptions Help()
    => new() { Verb = ExtractorVerb.Help };

  public static bool TryParse(string[] args, out ExtractorOptions options, out string error)
  {
    try
    {
      return TryParseCore(args, out options, out error);
    }
    catch (ExtractorUsageException ex)
    {
      options = Help();
      error = ex.Message;
      return false;
    }
  }

  private static bool TryParseCore(string[] args, out ExtractorOptions options, out string error)
  {
    options = Help();
    error = "";

    if (args.Length == 0)
    {
      options = Help();
      return true;
    }

    var filters = new List<string>();
    string? output = null;
    var recursive = true;
    var prefixArchive = false;
    var overwrite = true;
    var quiet = false;
    var positionals = new List<string>();

    for (var i = 0; i < args.Length; i++)
    {
      var arg = args[i];
      if (arg is "-h" or "--help" or "-?" or "/?")
      {
        options = Help();
        return true;
      }

      if (arg is "-q" or "--quiet")
      {
        quiet = true;
        continue;
      }

      if (arg == "--no-recursive")
      {
        recursive = false;
        continue;
      }

      if (arg == "--prefix-archive")
      {
        prefixArchive = true;
        continue;
      }

      if (arg == "--no-overwrite")
      {
        overwrite = false;
        continue;
      }

      if (TryTakeValue(args, ref i, arg, "-o", "--output", out var outputValue))
      {
        if (output is not null)
        {
          error = "Output directory specified more than once.";
          return false;
        }

        if (string.IsNullOrWhiteSpace(outputValue))
        {
          error = "Output directory is empty.";
          return false;
        }

        output = outputValue;
        continue;
      }

      if (TryTakeValue(args, ref i, arg, "-f", "--filter", out var filterValue))
      {
        if (string.IsNullOrWhiteSpace(filterValue))
        {
          error = "Filter pattern is empty.";
          return false;
        }

        filters.Add(filterValue);
        continue;
      }

      if (arg.StartsWith('-'))
      {
        error = $"Unknown option '{arg}'.";
        return false;
      }

      positionals.Add(arg);
    }

    if (positionals.Count == 0)
    {
      error = "Missing command. Expected 'list' or 'extract'.";
      return false;
    }

    if (positionals.Count < 2)
    {
      error = "Missing archive or folder path.";
      return false;
    }

    if (positionals.Count > 2)
    {
      error = $"Unexpected argument '{positionals[2]}'.";
      return false;
    }

    var verb = positionals[0].ToLowerInvariant() switch
    {
      "list" => ExtractorVerb.List,
      "extract" => ExtractorVerb.Extract,
      _ => (ExtractorVerb?)null
    };

    if (verb is null)
    {
      error = $"Unknown command '{positionals[0]}'. Expected 'list' or 'extract'.";
      return false;
    }

    if (verb == ExtractorVerb.Extract && string.IsNullOrWhiteSpace(output))
    {
      error = "extract requires --output <folder>.";
      return false;
    }

    if (verb == ExtractorVerb.List && output is not null)
    {
      error = "--output is only valid with extract.";
      return false;
    }

    options = new ExtractorOptions
    {
      Verb = verb.Value,
      InputPath = positionals[1],
      OutputPath = output,
      Filters = filters,
      Recursive = recursive,
      PrefixArchive = prefixArchive,
      Overwrite = overwrite,
      Quiet = quiet
    };
    return true;
  }

  private static bool TryTakeValue(
    string[] args,
    ref int index,
    string arg,
    string shortName,
    string longName,
    out string value)
  {
    value = "";
    if (arg == shortName || arg == longName)
    {
      if (index + 1 >= args.Length)
        throw new ExtractorUsageException($"Option '{arg}' requires a value.");

      index++;
      value = args[index];
      return true;
    }

    var longEquals = longName + "=";
    if (arg.StartsWith(longEquals, StringComparison.Ordinal))
    {
      value = arg[longEquals.Length..];
      return true;
    }

    var shortEquals = shortName + "=";
    if (arg.StartsWith(shortEquals, StringComparison.Ordinal))
    {
      value = arg[shortEquals.Length..];
      return true;
    }

    return false;
  }

}

public sealed class ExtractorUsageException : Exception
{

  public ExtractorUsageException(string message)
    : base(message)
  {
  }

}
