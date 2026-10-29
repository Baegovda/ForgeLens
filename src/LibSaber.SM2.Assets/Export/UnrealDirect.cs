using System.Text.Json;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Export;

/// <summary>
///   oneclick (원클릭 내보내기 옵션, "Direct to Unreal"): content-path rules for the automatic import. Same rule as
///   <c>dest_error</c> in tools/ue/ue_forge_direct.py: under /Game, never /Game/SM2_Work/Original/* or /Game/Work/*
///   (HANDOFF R05), and not a top-level folder (the importer backs a whole folder up as _bakN before reimporting).
/// </summary>
public static class UnrealContentPath
{
  public const string DefaultProject = @"K:\Unreal Projects\UE_VFX_57\UE_VFX_57.uproject";

  private static readonly string[] Protected = { "/Game/SM2_Work/Original", "/Game/Work" };
  private static readonly string[] TopLevel = { "Characters", "Props", "Animations", "Materials", "Tests" };

  public static string Normalize(string? path)
  {
    var p = (path ?? "").Trim().Replace('\\', '/');
    while (p.Contains("//"))
      p = p.Replace("//", "/");
    return p.TrimEnd('/');
  }

  /// <summary>Null when <paramref name="path" /> is an allowed destination, else the reason.</summary>
  public static string? Validate(string? path)
  {
    var p = Normalize(path);
    if (!p.StartsWith("/Game/", StringComparison.Ordinal))
      return "The destination must start with /Game/.";
    foreach (var bad in Protected)
      if (p.Equals(bad, StringComparison.OrdinalIgnoreCase) || p.StartsWith(bad + "/", StringComparison.OrdinalIgnoreCase))
        return $"{bad} is protected. Pick another folder.";
    var parts = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 3 || parts.Length == 3 && parts[1].Equals("SM2_Work", StringComparison.OrdinalIgnoreCase) && TopLevel.Contains(parts[2], StringComparer.OrdinalIgnoreCase))
      return "That is a top-level folder. Pick a folder for this asset, e.g. /Game/SM2_Work/Props/<asset>.";
    foreach (var seg in parts)
      if (seg is "." or ".." || seg.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|', ' ' }) >= 0)
        return $"Invalid folder name '{seg}' (no spaces or :*?\"<>|).";
    return null;
  }

  /// <summary>
  ///   Default folder category: anim-only exports → Animations, characters (cc_/ch_/chr_ templates, the existing
  ///   /Game/SM2_Work layout, HANDOFF "UE layout") → Characters, everything else → Props. A naming convention for the
  ///   default only; the user can type any allowed folder.
  /// </summary>
  public static string Category(string asset, bool animationOnly)
  {
    if (animationOnly)
      return "Animations";
    var a = asset.ToLowerInvariant();
    return a.StartsWith("cc_") || a.StartsWith("ch_") || a.StartsWith("chr_") ? "Characters" : "Props";
  }

  public static string Default(string asset, bool animationOnly) => $"/Game/SM2_Work/{Category(asset, animationOnly)}/{Safe(asset)}";

  /// <summary>Unreal-safe folder/asset name.</summary>
  public static string Safe(string name)
  {
    var chars = name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray();
    var s = new string(chars);
    return s.Length == 0 ? "asset" : s;
  }

  /// <summary>
  ///   Destination of each model: the typed folder for a single model, <c>{folder}/{model}</c> for several; when the
  ///   user kept the default, each model gets its own default folder.
  /// </summary>
  public static string ForModel(string chosen, bool isDefault, string model, int modelCount, bool animationOnly)
  {
    if (isDefault && modelCount > 1)
      return Default(model, animationOnly);
    var p = Normalize(chosen);
    return modelCount > 1 ? p + "/" + Safe(model) : p;
  }
}

/// <summary>oneclick: what the user picked in the Export window for the automatic Unreal import.</summary>
public sealed class UnrealDirectChoice
{
  public required string Project { get; init; }
  public required string ContentPath { get; init; }
  /// <summary>True when the user did not edit the suggested folder.</summary>
  public bool ContentPathIsDefault { get; init; }
}

/// <summary>oneclick: writes the importer scripts + manifest next to an export.</summary>
public static class UnrealDirectManifest
{
  public const string FolderName = "_forge_direct";
  public static readonly string[] Scripts = { "ue_forge_direct.py", "ue_import_anim.py", "ue_import_manny.py" };

  public sealed record Paths(string Folder, string Manifest, string Runner, string Log, string Result);

  /// <summary>Builds the manifest json (pure; tested).</summary>
  public static JsonObject Build(IReadOnlyList<ExportedModelFiles> models, UnrealDirectChoice choice, bool animationOnly, Paths paths, string mode = "")
  {
    var list = new JsonArray();
    foreach (var m in models)
    {
      var o = new JsonObject
      {
        ["name"] = m.Name,
        ["source"] = m.Source,
        ["folder"] = UnrealContentPath.ForModel(choice.ContentPath, choice.ContentPathIsDefault, m.Name, models.Count, animationOnly),
        ["skeletal"] = m.Skeletal,
        ["fbx"] = Fwd(m.Fbx),
        ["materials"] = Fwd(m.Materials),
        ["materialsScript"] = Fwd(m.MaterialsScript),
        ["physics"] = Fwd(m.Physics),
        ["clips"] = new JsonArray(m.Clips.Select(c => (JsonNode?)JsonValue.Create(Fwd(c))).ToArray()),
        ["manny"] = new JsonArray(m.Manny.Select(c => (JsonNode?)JsonValue.Create(Fwd(c))).ToArray()),
        ["colourScheme"] = Fwd(m.ColourScheme),
      };
      list.Add(o);
    }

    return new JsonObject
    {
      ["project"] = Fwd(choice.Project),
      ["mode"] = mode,
      ["log"] = Fwd(paths.Log),
      ["result"] = Fwd(paths.Result),
      ["scriptsDir"] = Fwd(paths.Folder),
      ["models"] = list,
    };
  }

  /// <summary>Writes <c>{export}/_forge_direct/</c>: the three scripts, manifest.json and run_import.py (no arguments, so
  ///   the same file runs through EngineWire, Remote Execution and -run=pythonscript).</summary>
  public static Paths Write(string exportFolder, IReadOnlyList<ExportedModelFiles> models, UnrealDirectChoice choice, bool animationOnly)
  {
    var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var dir = Path.Combine(exportFolder, FolderName);
    Directory.CreateDirectory(dir);
    var paths = new Paths(dir, Path.Combine(dir, $"manifest_{stamp}.json"), Path.Combine(dir, $"run_import_{stamp}.py"),
      Path.Combine(dir, $"import_{stamp}.log"), Path.Combine(dir, $"result_{stamp}.json"));
    foreach (var script in Scripts)
      File.WriteAllText(Path.Combine(dir, script), Resource(script).Replace("\r\n", "\n"));
    File.WriteAllText(paths.Manifest, Build(models, choice, animationOnly, paths).ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(paths.Runner, RunnerSource(Path.Combine(dir, "ue_forge_direct.py"), paths.Manifest));
    File.WriteAllText(paths.Log, "");
    return paths;
  }

  /// <summary>Python that runs the importer with the manifest (JSON string literals are valid Python literals).</summary>
  public static string RunnerSource(string script, string manifest)
  {
    var s = JsonSerializer.Serialize(Fwd(script));
    var m = JsonSerializer.Serialize(Fwd(manifest));
    return "# Forge Lens Direct to Unreal (generated)\nimport sys, runpy\n" +
           $"sys.argv = [{s}, {m}]\n" +
           $"runpy.run_path({s}, run_name=\"__main__\")\n";
  }

  public static string Resource(string name)
  {
    var asm = typeof(UnrealDirectManifest).Assembly;
    var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.Ordinal))
              ?? throw new InvalidOperationException("missing embedded " + name);
    using var stream = asm.GetManifestResourceStream(res)!;
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }

  private static string? Fwd(string? path) => path?.Replace('\\', '/');
}

/// <summary>oneclick: finds UnrealEditor-Cmd.exe for a .uproject (EngineAssociation → launcher install list → default path).</summary>
public static class UnrealEditorLocator
{
  public static string? EngineAssociation(string uproject)
  {
    try
    {
      using var doc = JsonDocument.Parse(File.ReadAllText(uproject));
      return doc.RootElement.TryGetProperty("EngineAssociation", out var e) ? e.GetString() : null;
    }
    catch
    {
      return null;
    }
  }

  /// <summary>Engine roots from %ProgramData%\Epic\UnrealEngineLauncher\LauncherInstalled.dat (AppName UE_5.7).</summary>
  public static string? FromLauncherList(string json, string version)
  {
    try
    {
      using var doc = JsonDocument.Parse(json);
      foreach (var item in doc.RootElement.GetProperty("InstallationList").EnumerateArray())
        if (item.TryGetProperty("AppName", out var app) && string.Equals(app.GetString(), "UE_" + version, StringComparison.OrdinalIgnoreCase)
                                                        && item.TryGetProperty("InstallLocation", out var loc))
          return loc.GetString();
    }
    catch
    {
      // fall through
    }

    return null;
  }

  public static string? FindEditorCmd(string uproject, Func<string, bool>? exists = null, Func<string, string?>? readText = null)
  {
    exists ??= File.Exists;
    readText ??= p => File.Exists(p) ? File.ReadAllText(p) : null;
    var version = EngineAssociation(uproject) ?? "5.7";
    var roots = new List<string>();
    var programData = Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData";
    if (readText(Path.Combine(programData, "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat")) is { } list && FromLauncherList(list, version) is { } root)
      roots.Add(root);
    roots.Add($@"C:\Program Files\Epic Games\UE_{version}");
    roots.Add($@"D:\Program Files\Epic Games\UE_{version}");
    roots.Add($@"D:\Epic Games\UE_{version}");
    foreach (var r in roots)
    {
      var cmd = Path.Combine(r, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe");
      if (exists(cmd))
        return cmd;
    }

    return null;
  }

  /// <summary>Headless command line: the commandlet runs run_import.py and exits.</summary>
  public static string HeadlessArguments(string uproject, string runner) =>
    $"\"{uproject}\" -run=pythonscript -script=\"{runner.Replace('\\', '/')}\" -unattended -nop4 -nosplash -NoSound -AutoDeclinePackageRecovery -stdout -FullStdOutLogOutput";
}
