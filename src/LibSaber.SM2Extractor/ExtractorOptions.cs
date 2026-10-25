namespace LibSaber.SM2Extractor;

public sealed class ExtractorOptions
{

  public const string Usage =
"""
LibSaber.SM2Extractor — list and extract Warhammer 40,000: Space Marine 2 archives

Usage:
  LibSaber.SM2Extractor list <archive-or-folder> [options]
  LibSaber.SM2Extractor extract <archive-or-folder> --output <folder> [options]
  LibSaber.SM2Extractor export <game-folder> --asset <path> --format <fbx-unreal|gltf|both> --output <folder> [--lod <n>] [--anim <name|all>]

The game ships .pak archives with a sibling cache of the same name plus ".cache"
(client_pc\something.pak and client_pc\something.pak.cache). .pck files with a
sibling .pck.cache are accepted too.

Options:
  -o, --output <folder>   Destination directory. Required for extract and export.
  -f, --filter <pattern>  Include filter, matched against the archive-relative path.
                          Repeat to combine with OR. A leading dot is an extension
                          (".pct" matches "*.pct"). Examples: *.pct, .tpl, characters/**/*.lg
                          list and extract only.
  --asset <path>          Model to export, as the browser shows it. export only.
                          tpl/cc_titus_assault.tpl finds tpl/cc_titus_assault.tpl/cc_titus_assault.tpl.
  --format <name>         fbx-unreal, gltf, or both. export only. FBX is Z-up centimeters.
                          glTF is Y-up meters. Textures are written next to the model.
  --lod <n>               0 (default) exports the meshes Index leaves checked.
                          1 and up export that smaller vertex pool. export only.
  --anim <name|all>       Also write {model}__{clip}.fbx / .glb with that clip on this
                          skeleton. The FBX is the skeleton and the curves only (no mesh),
                          so Unreal imports an AnimSequence onto the existing skeleton.
                          The glb keeps the mesh. <name> matches the .sani path (one token,
                          or several clips when it is a substring). all writes every .sani
                          that shares at least 8 bone names with the model. export only.
                          Bones the clip does not name stay on the bind pose.
  --no-recursive          When the input is a folder, do not search subfolders.
  --prefix-archive        Write each archive under <output>\<archive name>\. extract only.
  --no-overwrite          Leave files that are already in the output folder. extract only.
  -q, --quiet             Print the summary only. Failures still go to stderr.
  -h, --help              Show this help.

Exit codes:
  0  Finished with no skipped archives or files.
  1  Bad arguments, missing input, or no archives found.
  2  Finished, but at least one archive or file was skipped.

Examples:
  LibSaber.SM2Extractor list "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc" --filter *.pct
  LibSaber.SM2Extractor extract "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc" --output C:\sm2\extract --filter *.pct --filter *.tpl
  LibSaber.SM2Extractor export "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc" --asset tpl/cc_titus_assault.tpl --format fbx-unreal --lod 0 -o C:\sm2\titus
  LibSaber.SM2Extractor export "D:\Steam\steamapps\common\Space Marine 2\client_pc" --asset tpl/cc_titus_assault.tpl --anim all --format both -o D:\export\tpl
""";

  public ExtractorVerb Verb { get; private init; }
  public string InputPath { get; private init; } = "";
  public string? OutputPath { get; private init; }
  public IReadOnlyList<string> Filters { get; private init; } = Array.Empty<string>();
  public bool Recursive { get; private init; } = true;
  public bool PrefixArchive { get; private init; }
  public bool Overwrite { get; private init; } = true;
  public bool Quiet { get; private init; }
  public string? Asset { get; private init; }
  public string? Format { get; private init; }
  public int Lod { get; private init; }
  public string? Animation { get; private init; }

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
    string? asset = null;
    string? format = null;
    string? animation = null;
    var lod = 0;
    var lodSet = false;
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

      if (TryTakeValue(args, ref i, arg, "--asset", "--asset", out var assetValue))
      {
        if (asset is not null)
        {
          error = "Asset specified more than once.";
          return false;
        }

        if (string.IsNullOrWhiteSpace(assetValue))
        {
          error = "Asset path is empty.";
          return false;
        }

        asset = assetValue;
        continue;
      }

      if (TryTakeValue(args, ref i, arg, "--format", "--format", out var formatValue))
      {
        if (format is not null)
        {
          error = "Format specified more than once.";
          return false;
        }

        if (string.IsNullOrWhiteSpace(formatValue))
        {
          error = "Format is empty.";
          return false;
        }

        format = formatValue;
        continue;
      }

      if (TryTakeValue(args, ref i, arg, "--anim", "--anim", out var animValue))
      {
        if (animation is not null)
        {
          error = "Animation specified more than once.";
          return false;
        }

        if (string.IsNullOrWhiteSpace(animValue))
        {
          error = "Animation name is empty.";
          return false;
        }

        animation = animValue;
        continue;
      }

      if (TryTakeValue(args, ref i, arg, "--lod", "--lod", out var lodValue))
      {
        if (lodSet)
        {
          error = "LOD specified more than once.";
          return false;
        }

        if (!int.TryParse(lodValue, out lod) || lod < 0)
        {
          error = "LOD must be 0 or a positive integer.";
          return false;
        }

        lodSet = true;
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
      error = "Missing command. Expected 'list', 'extract', or 'export'.";
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
      "export" => ExtractorVerb.Export,
      _ => (ExtractorVerb?)null
    };

    if (verb is null)
    {
      error = $"Unknown command '{positionals[0]}'. Expected 'list', 'extract', or 'export'.";
      return false;
    }

    if (verb == ExtractorVerb.Extract && string.IsNullOrWhiteSpace(output))
    {
      error = "extract requires --output <folder>.";
      return false;
    }

    if (verb == ExtractorVerb.List && output is not null)
    {
      error = "--output is only valid with extract and export.";
      return false;
    }

    if (verb != ExtractorVerb.Export && (asset is not null || format is not null || lodSet || animation is not null))
    {
      error = "--asset, --format, --lod, and --anim are only valid with export.";
      return false;
    }

    if (verb == ExtractorVerb.Export)
    {
      if (string.IsNullOrWhiteSpace(output))
      {
        error = "export requires --output <folder>.";
        return false;
      }

      if (string.IsNullOrWhiteSpace(asset))
      {
        error = "export requires --asset <path>.";
        return false;
      }

      if (string.IsNullOrWhiteSpace(format))
      {
        error = "export requires --format fbx-unreal|gltf|both.";
        return false;
      }

      if (format.ToLowerInvariant() is not ("fbx-unreal" or "gltf" or "both"))
      {
        error = $"Unknown format '{format}'. Expected fbx-unreal, gltf, or both.";
        return false;
      }

      if (filters.Count > 0 || prefixArchive || !overwrite)
      {
        error = "export does not use --filter, --prefix-archive, or --no-overwrite.";
        return false;
      }
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
      Quiet = quiet,
      Asset = asset,
      Format = format?.ToLowerInvariant(),
      Lod = lod,
      Animation = animation,
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
