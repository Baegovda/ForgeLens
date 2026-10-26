namespace LibSaber.SM2Extractor;

public sealed class ExtractorOptions
{

  public const string Usage =
"""
LibSaber.SM2Extractor — list and extract Warhammer 40,000: Space Marine 2 archives

Usage:
  LibSaber.SM2Extractor list <archive-or-folder> [options]
  LibSaber.SM2Extractor extract <archive-or-folder> --output <folder> [options]
  LibSaber.SM2Extractor export <game-folder> --asset <path> --format <fbx-unreal|gltf|both> --output <folder> [--lod <n>] [--anim <name|all>]... [--anim-only] [--anim-rebase <first-frame|none>] [--no-root-motion] [--include-cloth-sim] [--no-physics] [--variant <spec>] [--tex-formats <list>]

The game ships .pak archives with a sibling cache of the same name plus ".cache"
(client_pc\something.pak and client_pc\something.pak.cache). .pck files with a
sibling .pck.cache are accepted too.

Options:
  -o, --output <folder>   Destination directory. Required for extract and export.
  -f, --filter <pattern>  Include filter, matched against the archive-relative path.
                          Repeat to combine with OR. A leading dot is an extension
                          (".pct" matches "*.pct"). Examples: *.pct, .tpl, characters/**/*.lg
                          list and extract only.
  --asset <path>          Model (.tpl) or effect (.sfx) to export, as the browser shows it. export only.
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
                          The clip's 0xF4 track is added to the skeleton root translation.
                          Repeat --anim, or separate tokens with ';' or ',', to export many
                          clips in one run. A token with * or ? is a glob on the whole .sani
                          path (animation/primaris/assault/*_run_*). @list.txt reads tokens
                          from a file, one per line. Clips with the additive slot (0xF3/0xF7)
                          end in __ADD.
  --anim-only             Write the clip files only: no bind mesh, materials, or textures.
                          Use with a skeleton that is already in the Unreal project.
  --anim-rebase <mode>    first-frame (default): move each clip's first frame to the bind
                          position and heading, so level-space cinematics start at the origin.
                          The removed offset goes to {clip}.rebase.json. Clips that already
                          start at the bind are unchanged. none keeps level space.
  --no-root-motion        Leave that root translation off. The bone curves are unchanged.
                          export only.
  --fbx-armature-null     Old FBX hierarchy: a {model}_Skeleton Null above the tpl root object.
                          Default for fbx-unreal: ROOT is the hierarchy root (Unreal bone 0)
                          and carries the root motion. export only.
  --include-cloth-sim     Keep *_SIM cloth simulation meshes. The game only uses them to
                          drive the *_REND cloth (InitSimToRenderSkinning) and never draws
                          them, so they are skipped by default. export only.
  --no-physics            Skip the class physics (cloth, jiggle bones). By default the export
                          writes {model}.physics.json, bakes the simulated jiggle bones into
                          every clip and adds cloth helper bones (skinned to the cloth render
                          meshes, baked per clip) so Unreal plays the game's secondary motion
                          with no runtime setup. export only.
  --variant <spec>        Armor variant for customizable constructor tpls (cc_sm_constructor_*,
                          cc_ch_constructor_*), which carry every armor piece of every region.
                          default: PREDEFINED_PRESET_1 of the tpl's appearance (the preset
                          SetActivePresetToDefault equips). all: no filter (old behavior).
                          Tokens, separated by ',' or ';': preset=N, appearance=TEAM/UID,
                          SECTION=regionUid (HELMET=helmet_tank_common_01), +object, -object.
                          Other tpls are not filtered. Writes {model}.variant.json. export only.
  --tex-formats <list>    Texture files to write: any of dds, png, tga (',' separated).
                          Default dds,png,tga. The FBX links png, then tga, then dds.
                          export only.
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
  public bool RootMotion { get; private init; } = true;
  public bool UnrealRootBone { get; private init; } = true;
  public bool AnimationOnly { get; private init; }
  public bool RebaseFirstFrame { get; private init; } = true;
  public bool IncludeClothSim { get; private init; }
  public bool Physics { get; private init; } = true;
  /// <summary><c>--variant</c> spec, or null for the default (PREDEFINED_PRESET_1 on constructor tpls).</summary>
  public string? Variant { get; private init; }
  public bool WriteDds { get; private init; } = true;
  public bool WritePng { get; private init; } = true;
  public bool WriteTga { get; private init; } = true;

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
    var rootMotion = true;
    var unrealRootBone = true;
    var animationOnly = false;
    var rebase = true;
    var rebaseSet = false;
    var rootMotionSet = false;
    var includeClothSim = false;
    var physics = true;
    string? variant = null;
    string? texFormats = null;
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

      if (arg == "--no-physics")
      {
        physics = false;
        continue;
      }

      if (arg == "--include-cloth-sim")
      {
        includeClothSim = true;
        continue;
      }

      if (TryTakeValue(args, ref i, arg, "--variant", "--variant", out var variantValue))
      {
        if (variant is not null)
        {
          error = "Variant specified more than once.";
          return false;
        }

        if (string.IsNullOrWhiteSpace(variantValue))
        {
          error = "Variant is empty.";
          return false;
        }

        try
        {
          LibSaber.SM2.Assets.Customization.VariantRequest.Parse(variantValue);
        }
        catch (ArgumentException ex)
        {
          error = ex.Message;
          return false;
        }

        variant = variantValue.Trim();
        continue;
      }

      if (TryTakeValue(args, ref i, arg, "--tex-formats", "--tex-formats", out var texValue))
      {
        if (texFormats is not null)
        {
          error = "Texture formats specified more than once.";
          return false;
        }

        texFormats = texValue ?? "";
        continue;
      }

      if (arg == "--anim-only")
      {
        animationOnly = true;
        continue;
      }

      if (TryTakeValue(args, ref i, arg, "--anim-rebase", "--anim-rebase", out var rebaseValue))
      {
        if (rebaseValue is not ("first-frame" or "none"))
        {
          error = "--anim-rebase takes first-frame or none.";
          return false;
        }

        rebase = rebaseValue == "first-frame";
        rebaseSet = true;
        continue;
      }

      if (arg == "--fbx-armature-null")
      {
        unrealRootBone = false;
        continue;
      }

      if (arg == "--no-root-motion")
      {
        if (rootMotionSet)
        {
          error = "Root motion specified more than once.";
          return false;
        }

        rootMotion = false;
        rootMotionSet = true;
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
        if (string.IsNullOrWhiteSpace(animValue))
        {
          error = "Animation name is empty.";
          return false;
        }

        animation = animation is null ? animValue : animation + ";" + animValue;
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

    if (animationOnly && animation is null)
    {
      error = "--anim-only needs --anim.";
      return false;
    }

    if (verb != ExtractorVerb.Export && (asset is not null || format is not null || lodSet || animation is not null || rootMotionSet || includeClothSim || !physics || !unrealRootBone || animationOnly || rebaseSet || variant is not null || texFormats is not null))
    {
      error = "--asset, --format, --lod, --anim, --anim-only, --anim-rebase, --no-root-motion, --fbx-armature-null, --include-cloth-sim, --no-physics, --variant, and --tex-formats are only valid with export.";
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

    bool dds = true, png = true, tga = true;
    if (texFormats is not null && !TryParseTextureFormats(texFormats, out dds, out png, out tga, out error))
      return false;

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
      RootMotion = rootMotion,
      UnrealRootBone = unrealRootBone,
      AnimationOnly = animationOnly,
      RebaseFirstFrame = rebase,
      IncludeClothSim = includeClothSim,
      Physics = physics,
      Variant = variant,
      WriteDds = dds,
      WritePng = png,
      WriteTga = tga,
    };
    return true;
  }

  /// <summary><c>--tex-formats</c>: a ',' / ';' / '+' list of dds, png, tga. At least one is required.</summary>
  public static bool TryParseTextureFormats(string value, out bool dds, out bool png, out bool tga, out string error)
  {
    dds = png = tga = false;
    error = "";
    foreach (var token in value.Split(new[] { ',', ';', '+' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      switch (token.TrimStart('.').ToLowerInvariant())
      {
        case "dds":
          dds = true;
          break;
        case "png":
          png = true;
          break;
        case "tga":
          tga = true;
          break;
        case "all":
          dds = png = tga = true;
          break;
        default:
          error = $"Unknown texture format '{token}'. --tex-formats takes dds, png, tga (comma separated).";
          return false;
      }
    }

    if (!dds && !png && !tga)
    {
      error = "--tex-formats needs at least one of dds, png, tga.";
      return false;
    }

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
