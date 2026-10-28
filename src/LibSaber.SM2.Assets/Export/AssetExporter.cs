using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;
using LibSaber.SM2.Assets.Textures;

namespace LibSaber.SM2.Assets.Export;

public sealed class ExportSettings
{
  public bool WriteDds { get; init; } = true;
  public bool WritePng { get; init; } = true;
  public bool WriteTga { get; init; } = true;
  public bool WriteGltf { get; init; } = true;
  public bool WriteFbx { get; init; } = true;
  public bool WriteMaterials { get; init; } = true;
  public bool WriteModelTextures { get; init; } = true;
  public bool CopyOriginalBytes { get; init; }
  public ExportSpace Space { get; init; } = ExportSpace.Unreal;
  /// <summary>
  ///   0 exports the meshes Index leaves checked (LOD0 plus shells such as <c>_SIM</c>).
  ///   1 and up export that smaller vertex pool, which Index hides as a lower LOD or a collision shell.
  /// </summary>
  public int Lod { get; init; }
  /// <summary>Checked meshes for a previewed catalog index. Other files keep Index's defaults.</summary>
  public Dictionary<int, MeshVisibility>? PartVisibility { get; set; }
  /// <summary>Clips written beside the bind mesh as <c>{model}__{clip}.fbx</c> and <c>.glb</c>. The bind file itself stays unanimated.</summary>
  public IReadOnlyList<AnimationClip> Clips { get; set; } = Array.Empty<AnimationClip>();
  /// <summary>Adds the clip's 0xF4 track to the skeleton root translation. The extractor's <c>--no-root-motion</c> turns this off.</summary>
  public bool RootMotion { get; init; } = true;
  /// <summary>
  ///   mtl1-42: animation root mode. False (default, unchanged) = Root motion: ROOT keeps its travel and the 0xF4 track
  ///   (<see cref="RootMotion"/>), ready for UE EnableRootMotion. True = In place: the ROOT's horizontal travel is
  ///   removed like the Models preview's In place (<see cref="AnimationClip.InPlace"/>). Applies to the bind-skeleton
  ///   clips, the Anim Only (Manny) clips and the physics bake (which samples the same pose).
  /// </summary>
  public bool InPlace { get; init; }
  /// <summary>
  ///   Unreal FBX: make <c>ROOT</c> the hierarchy root (skeleton bone 0, carries root motion) and drop the
  ///   <c>{mesh}_Skeleton</c> Null. The extractor's <c>--fbx-armature-null</c> turns this off (old hierarchy).
  /// </summary>
  public bool UnrealRootBone { get; init; } = true;
  /// <summary>
  ///   Keep <c>*_SIM</c> cloth simulation meshes. SM2's cloth (<c>PropClothSim</c>, <c>simMeshName</c> /
  ///   <c>renderMeshes</c>) only skins the <c>*_REND</c> meshes from them and never draws them, so they are off by default.
  /// </summary>
  public bool IncludeClothSim { get; init; }
  /// <summary>
  ///   Move each clip's first frame back to the bind position and heading (see <see cref="AnimationClip.RebaseFirstFrame"/>).
  ///   On by default: cinematic shots are in level space, everything else is unchanged. The removed offset is written to
  ///   <c>{clip}.rebase.json</c> so a Sequencer shot can place the actor back. The extractor's <c>--anim-rebase none</c> keeps level space.
  /// </summary>
  public bool RebaseFirstFrame { get; init; } = true;
  /// <summary>Write the clip files only (no bind mesh, materials, or textures). For importing onto a skeleton already in the project.</summary>
  public bool AnimationOnly { get; init; }
  /// <summary>
  ///   Armor variant for customizable constructor tpls (<see cref="CustomizationLibrary.IsConstructorTemplate"/>), as a
  ///   <see cref="VariantRequest"/> spec. Null or <c>default</c> keeps PREDEFINED_PRESET_1 (what SetActivePresetToDefault
  ///   equips); <c>all</c> exports every armor piece. Other tpls are never filtered.
  /// </summary>
  public string? Variant { get; init; }
  /// <summary>
  ///   Secondary physics of the character class (cloth, jiggle bones; <see cref="PhysicsLocator"/>). Writes
  ///   <c>{model}.physics.json</c>, bakes the simulated jiggle bones into every clip, and adds cloth
  ///   helper bones (one per free sim particle, deterministic, also with <see cref="AnimationOnly"/>) that the cloth
  ///   render meshes are skinned to, and bakes them too.
  ///   The extractor's <c>--no-physics</c> turns this off.
  /// </summary>
  public bool Physics { get; init; } = true;
  /// <summary>
  ///   UE5 Mannequin reference pose JSON (tools/ue/ue_dump_skeleton.py on SK_Mannequin). When set, every clip is written as
  ///   {clip}_Manny.fbx on the Mannequin skeleton (<see cref="Retarget.MannequinRetarget"/>) instead of the SM2 skeleton.
  ///   The extractor's <c>--anim-mannequin &lt;json&gt;</c>.
  /// </summary>
  public string? MannequinReference { get; init; }
}

public sealed class ExportReport
{
  public int Written { get; set; }
  public int Skipped { get; set; }
  public List<string> Messages { get; } = new();
}

public static class AssetExporter
{

  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  public static ExportReport Export(
    GameCatalog catalog,
    ArchiveStore store,
    IReadOnlyList<int> files,
    string outputDirectory,
    ExportSettings settings,
    IProgress<string>? progress = null,
    CancellationToken cancellation = default)
  {
    Directory.CreateDirectory(outputDirectory);
    var report = new ExportReport();
    var notePath = Path.Combine(outputDirectory, "export-notes.txt");
    File.WriteAllText(notePath, Notes(settings));
    report.Written++;
    var batch = new string[files.Count];
    for (var i = 0; i < files.Count; i++)
      batch[i] = catalog.GetFile(files[i]).Path;

    foreach (var index in files)
    {
      cancellation.ThrowIfCancellationRequested();
      var file = catalog.GetFile(index);
      progress?.Report(file.Path);
      try
      {
        switch (file.Kind)
        {
          case AssetKind.Texture:
            ExportTexture(catalog, store, index, file, outputDirectory, settings, report, batch);
            break;
          case AssetKind.Model:
            ExportModel(catalog, store, index, file, outputDirectory, settings, report, batch);
            break;
          case AssetKind.Animation:
            ExportAnimation(catalog, store, index, file, outputDirectory, settings, report, batch);
            break;
          case AssetKind.Effect:
            ExportEffect(catalog, store, index, file, outputDirectory, settings, report, batch);
            break;
          default:
            WriteOriginal(store, index, file, outputDirectory, report, file.Kind == AssetKind.TextureMip
              ? "Loose mip payload. Preview and image export use the .pct.resource that names this file."
              : null);
            break;
        }

        if (settings.CopyOriginalBytes && file.Kind is AssetKind.Texture or AssetKind.Model or AssetKind.Animation)
          WriteOriginal(store, index, file, outputDirectory, report, null);
      }
      catch (Exception ex)
      {
        report.Skipped++;
        report.Messages.Add(file.Path + ": " + ex.Message);
      }
    }

    return report;
  }

  private static void ExportTexture(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var document = TextureLoader.Load(catalog, store, index);
    if (document.Payload.Length == 0)
    {
      report.Skipped++;
      report.Messages.Add(file.Path + ": " + (document.Message.Length == 0 ? "No texture payload." : document.Message));
      return;
    }

    if (document.DxgiFormat == 0)
    {
      WriteOriginal(store, index, file, outputDirectory, report, "No DDS mapping for this texture format. Original bytes were copied.");
      return;
    }

    if (settings.WriteDds)
    {
      var path = Destination(outputDirectory, file.Path, ".dds", batch);
      DdsWriter.Write(
        path,
        document.Payload,
        document.Width,
        document.Height,
        document.MipCount,
        document.FaceCount,
        document.DxgiFormat,
        TextureInfo.IsBlock(TextureInfo.PreviewOf(document.Format)));
      report.Written++;
    }

    if (!settings.WritePng && !settings.WriteTga)
      return;

    try
    {
      var pixels = document.Decode(0, 0, out var width, out var height, out var note);
      if (document.Format == SpaceMarine2.Enumerations.SM2TextureFormat.DXN && !TextureInfo.IsNormalName(file.Path))
      {
        FixBc5MaskPixels(pixels);
      }
      if (note.Length > 0)
        report.Messages.Add(file.Path + ": " + note);
      if (settings.WritePng)
      {
        ImageWriters.WritePng(Destination(outputDirectory, file.Path, ".png", batch), pixels, width, height);
        report.Written++;
      }

      if (settings.WriteTga)
      {
        ImageWriters.WriteTga(Destination(outputDirectory, file.Path, ".tga", batch), pixels, width, height);
        report.Written++;
      }
    }
    catch (Exception ex)
    {
      report.Messages.Add(file.Path + ": image preview export skipped (" + ex.Message + "). DDS was still requested separately.");
    }
  }

  private static void ExportModel(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var mesh = TemplateMeshReader.Load(catalog, store, index);
    if (mesh.Message.Length > 0)
      report.Messages.Add(file.Path + ": " + mesh.Message);
    if (settings.PartVisibility is not null && settings.PartVisibility.TryGetValue(index, out var visibility))
      visibility.Apply(mesh);
    if (!settings.IncludeClothSim)
      HideClothSim(mesh);
    else
      ShowClothSim(mesh);
    var variant = settings.AnimationOnly ? null : ApplyVariant(catalog, store, file, mesh, settings, report);
    var exportMesh = SelectExportGeometry(mesh, settings.Lod);
    if (!settings.AnimationOnly)
      AttachResolvedNormals(catalog, index, exportMesh);

    if (!exportMesh.HasTriangles && exportMesh.Bones.Count == 0)
    {
      report.Skipped++;
      if (settings.CopyOriginalBytes || file.Extension is ".tpl_data" or ".lods_base" or ".geom_dbg" or ".tpl_markup")
        WriteOriginal(store, index, file, outputDirectory, report, null);
      return;
    }

    exportMesh.Clip = null;
    PhysicsSetup? physics = null;
    ClothBoneRig? clothRig = null;
    var physicsBaked = new Dictionary<string, PhysicsBakeReport>(StringComparer.OrdinalIgnoreCase);
    if (settings.Physics && exportMesh.Bones.Count > 0)
    {
      try
      {
        physics = PhysicsLocator.Find(catalog, store, file.Path);
        if (physics is { IsEmpty: false })
        {
          // mtl1-37: CDT shape bones are link_tpl_obj names; tpl_markup locators are part of that object list.
          var resolvedCdt = CdtObjectResolver.Apply(physics, exportMesh.Bones, TplMarkup.Find(catalog, store, index));
          if (resolvedCdt > 0)
            report.Messages.Add($"{file.Path}: {resolvedCdt} CDT shape bone(s) resolved via tpl_markup locators.");
          // Helper bones are deterministic (same tpl + cls -> same names and order), so --anim-only clips key the
          // same bones the model export added; a skeleton imported without them just ignores those tracks.
          if (physics.Cloths.Count > 0)
            clothRig = PhysicsBake.AddClothBones(exportMesh, physics);
          report.Messages.Add($"{file.Path}: physics from {physics.SourceClass}: {physics.Cloths.Count} cloths, {physics.Jiggles.Count} jiggle bones" +
            (clothRig is null ? "" : $", {clothRig.Clusters.Count} cloth helper bones skinning {clothRig.SkinnedVertices} vertices") + ".");
          if (!settings.IncludeClothSim && physics.Cloths.Count > 0)
            report.Messages.Add($"{file.Path}: _SIM cloth shells not exported (--include-cloth-sim off): Chaos Cloth conversion uses the render-as-sim fallback.");
        }
        else
          physics = null;
      }
      catch (Exception ex)
      {
        physics = null;
        report.Messages.Add($"{file.Path}: physics skipped ({ex.Message}).");
      }
    }

    IReadOnlyDictionary<string, string>? textureFiles = null;
    if (settings.WriteGltf && !settings.AnimationOnly)
    {
      GltfWriter.Write(Destination(outputDirectory, file.Path, ".glb", batch), exportMesh, settings.Space);
      report.Written++;
    }

    if (settings.WriteFbx && exportMesh.HasTriangles && !settings.AnimationOnly)
    {
      var fbxPath = Destination(outputDirectory, file.Path, ".fbx", batch);
      textureFiles = TextureFiles(catalog, store, index, exportMesh, fbxPath, outputDirectory, settings, report, batch);
      FbxWriter.Write(fbxPath, exportMesh, settings.Space, textureFiles, unrealRoot: settings.UnrealRootBone);
      report.Written++;
      if (settings.WriteMaterials)
      {
        WriteMaterialSidecar(fbxPath, exportMesh, textureFiles, report);
        WriteUnrealScript(Path.Combine(Path.GetDirectoryName(fbxPath)!, "import_sm2_materials.py"), report);
      }
    }

    var usedClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var mannequin = settings.MannequinReference is { } mannyRef ? Retarget.MannequinReference.Load(mannyRef) : null;
    foreach (var clip in settings.Clips)
    {
      clip.RootMotion = settings.RootMotion;
      clip.RebaseFirstFrame = settings.RebaseFirstFrame;
      clip.InPlace = settings.InPlace;
      clip.InPlaceKey = null;
      if (!clip.HasCurves || exportMesh.Bones.Count == 0)
        continue;
      // __ADD marks a clip with the 0xF3/0xF7 additive slot (clip+0x68). ue_import_anim.py makes it a
      // local-space additive against its own frame 0, which is what that slot stores.
      var token = ClipToken(clip.Name) + (clip.HasAdditive ? AdditiveSuffix : "");
      var unique = token;
      var suffix = 2;
      while (!usedClips.Add(unique))
        unique = token + "_" + suffix++;
      exportMesh.Clip = clip;
      if (mannequin is not null)
      {
        var manny = Retarget.MannequinRetarget.Build(exportMesh, clip, mannequin, report.Messages);
        if (manny is not null && settings.WriteFbx)
        {
          var smPath = Destination(outputDirectory, AnimationRelative(file.Path, unique), ".fbx", batch);
          var mannyPath = Path.Combine(Path.GetDirectoryName(smPath)!, Path.GetFileNameWithoutExtension(smPath) + Retarget.MannequinRetarget.Suffix + ".fbx");
          FbxWriter.Write(mannyPath, manny, settings.Space, textureFiles, animationOnly: true, unrealRoot: true);
          report.Written++;
          var animScript = Path.Combine(Path.GetDirectoryName(mannyPath)!, "ue_import_anim.py");
          if (!File.Exists(animScript) || usedClips.Count == 1)
            WriteUnrealScript(animScript, report);
        }
        continue;
      }
      if (physics is not null)
      {
        try
        {
          physicsBaked[unique] = PhysicsBake.Bake(exportMesh, clip, physics, clothRig);
          exportMesh.Clip = clip;
        }
        catch (Exception ex)
        {
          clip.Baked = null;
          report.Messages.Add($"{clip.Name}: physics bake skipped ({ex.Message}).");
        }
      }

      var relative = AnimationRelative(file.Path, unique);
      if (settings.WriteGltf)
      {
        GltfWriter.Write(Destination(outputDirectory, relative, ".glb", batch), exportMesh, settings.Space);
        report.Written++;
      }

      if (settings.WriteFbx && exportMesh.Bones.Count > 0)
      {
        var fbxPath = Destination(outputDirectory, relative, ".fbx", batch);
        // Skeleton and curves only. A mesh in this file makes Interchange import nothing
        // when mesh import is off, and the legacy importer builds a second SkeletalMesh.
        FbxWriter.Write(fbxPath, exportMesh, settings.Space, textureFiles, animationOnly: true, unrealRoot: settings.UnrealRootBone);
        report.Written++;
        var animScript = Path.Combine(Path.GetDirectoryName(fbxPath)!, "ue_import_anim.py");
        if (!File.Exists(animScript) || usedClips.Count == 1)
          WriteUnrealScript(animScript, report);
        if (clip.RebaseFirstFrame && (clip.RebaseOffset != System.Numerics.Vector3.Zero || clip.RebaseYawDegrees != 0))
          WriteRebaseSidecar(Path.ChangeExtension(fbxPath, ".rebase.json"), clip, report);
      }

      if (clip.RebaseFirstFrame && (clip.RebaseOffset != System.Numerics.Vector3.Zero || clip.RebaseYawDegrees != 0))
        report.Messages.Add($"{clip.Name}: rebased to the bind pose: removed {clip.RebaseOffset.X:0.###}, {clip.RebaseOffset.Y:0.###}, {clip.RebaseOffset.Z:0.###} m (source Y-up) and {clip.RebaseYawDegrees:0.#} deg yaw. --anim-rebase none keeps level space.");
      if (clip.HasAdditive)
        report.Messages.Add($"{clip.Name}: additive slot (0xF3/0xF7) present, written as {unique}. Import as a local-space additive with base = its frame 0.");

      var binding = ClipBinding.Resolve(exportMesh.Bones, clip);
      var bound = binding.Count(index => index >= 0);
      var by = ClipBinding.UsesAnimIndex(exportMesh.Bones, clip) ? " (by template animNmb)" : "";
      var unbound = clip.Tracks.Where((_, i) => i < binding.Length && binding[i] < 0).Select(track => track.BoneName).ToList();
      var unboundNote = unbound.Count == 0 ? "" : $" Unmatched: {string.Join(", ", unbound.Take(8))}{(unbound.Count > 8 ? $" (+{unbound.Count - 8})" : "")}.";
      report.Messages.Add($"{clip.Name}: {bound} of {clip.Tracks.Count} tracks match {exportMesh.Name}{by}, {clip.Duration:0.###}s at {clip.SampleRate:0.###} fps.{unboundNote}");
      if (clip.InPlace)
        report.Messages.Add($"{clip.Name}: In place: ROOT horizontal travel removed (height and turns kept), like the Models preview. Use Root motion for UE EnableRootMotion.");
      if (clip.RootMotion && clip.HasRootRotation)
      {
        var turn = clip.RootRotation.Max(key => 2 * MathF.Acos(Math.Min(1f, MathF.Abs(key.Value.W)))) * 180 / MathF.PI;
        report.Messages.Add($"{clip.Name}: root rotation applied (up to {turn:0.#} deg). Turn clips need it; the two cinematic shots that carry one (pve_relay boss_intro / outro) end on the ground only without it, so export those with --no-root-motion.");
      }
    }

    exportMesh.Clip = null;
    foreach (var clip in settings.Clips)
    {
      clip.Baked = null;
      // The browser hands its preview clips in; the preview applies its own In place, so do not leave the export's on.
      clip.InPlace = false;
      clip.InPlaceKey = null;
    }
    if (physics is not null)
    {
      var physicsPath = Destination(outputDirectory, file.Path, ".physics.json", batch);
      Directory.CreateDirectory(Path.GetDirectoryName(physicsPath)!);
      File.WriteAllText(physicsPath, PhysicsSidecar.Serialize(exportMesh, physics, clothRig, physicsBaked));
      report.Written++;
      WriteUnrealScript(Path.Combine(Path.GetDirectoryName(physicsPath)!, "ue_sm2_physics.py"), report);
      foreach (var (name, baked) in physicsBaked)
        report.Messages.Add($"{name}: physics baked at {PhysicsBake.Rate:0} Hz, {baked.JiggleBones} jiggle and {baked.ClothBones} cloth helper bones, {baked.Frames} frames, {baked.MeanStepMs:0.00} ms/step.");
    }

    if (settings.AnimationOnly)
      return;
    WriteMaterial(outputDirectory, file.Path, exportMesh.Material, settings, report, batch);
    if (variant is not null)
      WriteVariantSidecar(Destination(outputDirectory, file.Path, ".variant.json", batch), variant, report);
    if (settings.WriteModelTextures)
      ExportModelTextures(catalog, store, index, exportMesh, outputDirectory, settings, report, batch, partsOnly: variant is { HiddenParts: > 0 });
  }

  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameCatalog, CustomizationLibrary?[]> Libraries = new();

  /// <summary>
  ///   Picks one armor variant on a constructor tpl. Team and appearance come from the dummy .cls that names the tpl
  ///   (<c>geom.nameTpl</c> + <c>characterInfo</c>), else from the tpl name. Returns null when nothing was filtered.
  /// </summary>
  internal static VariantResult? ApplyVariant(GameCatalog catalog, ArchiveStore store, CatalogFileInfo file, ExportedMesh mesh, ExportSettings settings, ExportReport report)
  {
    var template = AssetClassification.BaseName(file.Name);
    if (!string.IsNullOrWhiteSpace(settings.Variant))
    {
      var npc = VariantRequest.Parse(settings.Variant);
      if (npc.Npc is not null || npc.NpcPreset is not null)
        return ApplyNpcVariant(catalog, store, template, mesh, npc, report);
    }

    if (!CustomizationLibrary.IsConstructorTemplate(template))
      return ApplyWeaponVariant(catalog, store, template, mesh, settings, report);
    var request = VariantRequest.Parse(settings.Variant);
    if (request.PowerFist is not null && MeleeLibrary(catalog, store)?.AddPowerFist(request, template) is { } fistNote)
      report.Messages.Add(template + ": " + fistNote);
    if (request.All)
    {
      report.Messages.Add($"{template}: --variant all, every customization region exported.");
      return null;
    }

    var holder = Libraries.GetValue(catalog, _ => new CustomizationLibrary?[1]);
    CustomizationLibrary? library;
    lock (holder)
      library = holder[0] ??= CustomizationLibrary.Load(catalog, store);
    if (library is null)
    {
      report.Messages.Add($"{template}: {CustomizationLibrary.LibraryPath} is not in the catalog; every armor piece exported.");
      return null;
    }

    string? team = request.Team;
    string? appearance = request.Appearance;
    var source = "--variant";
    if (appearance is null)
    {
      var character = CustomizationLibrary.CharacterFromDummyClass(catalog, store, template);
      if (character is { } fromClass)
      {
        team ??= fromClass.Team;
        appearance = library.AppearanceForMastery(team, fromClass.Mastery);
        source = fromClass.Source + " characterInfo " + fromClass.Team + "/" + fromClass.Mastery;
      }

      if (appearance is null && CustomizationLibrary.CharacterFromName(template) is { } fromName)
      {
        team ??= fromName.Team;
        appearance = library.AppearanceForMastery(team, fromName.Mastery) ?? "PVE_PVP_" + fromName.Mastery[4..];
        source = "tpl name (" + fromName.Team + "/" + fromName.Mastery + ")";
      }
    }

    team ??= "LOYALIST";
    if (appearance is null)
    {
      report.Messages.Add($"{template}: no customization appearance found; every armor piece exported.");
      return null;
    }

    var result = ArmorVariant.Apply(mesh, library, team, appearance, request);
    result.Template = template;
    result.AppearanceSource = source;
    report.Messages.Add($"{template}: variant {team}/{appearance} {result.Preset}: hid {result.HiddenParts} armor parts of {result.CustomizableObjects} customizable objects, {result.VisibleParts} visible parts left. --variant all keeps every piece.");
    foreach (var note in result.Notes.Take(8))
      report.Messages.Add(template + ": " + note);
    return result;
  }

  /// <summary>
  ///   Slots for a part picker: armor (constructor tpl) = default preset regions + every region per body section;
  ///   weapon tpl = skins (Presets), first skin's regions (Regions) and every region present in the tpl (AvailableRegions).
  ///   Loads its own mesh copy; null when the file has no customization.
  /// </summary>
  public static VariantResult? DescribeVariant(GameCatalog catalog, ArchiveStore store, int index)
  {
    var file = catalog.GetFile(index);
    var template = AssetClassification.BaseName(file.Name);
    var report = new ExportReport();
    var mesh = TemplateMeshReader.Load(catalog, store, index);
    if (CustomizationLibrary.IsConstructorTemplate(template))
      return ApplyVariant(catalog, store, file, mesh, new ExportSettings(), report);
    if (!template.StartsWith("wpn_", StringComparison.OrdinalIgnoreCase))
      return null;
    var lib = WeaponLibrary(catalog, store);
    var uid = lib?.UidFor(template);
    if (lib is null || uid is null)
    {
      var melee = MeleeLibrary(catalog, store);
      if (melee is null || melee.ActorsFor(template).Count == 0)
        return null;
      var m = MeleeVariant.Apply(mesh, melee, template, VariantRequest.Default);
      m.Template = template;
      return m;
    }

    var result = WeaponVariant.Apply(mesh, lib, uid, VariantRequest.Default);
    result.Template = template;
    return result;
  }

  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameCatalog, MeleeCustomization?[]> MeleeLibraries = new();

  /// <summary>ui-45: melee_skins_library + geom_customizer_preset_melee + melee_actor_factory (cached per catalog).</summary>
  public static MeleeCustomization? MeleeLibrary(GameCatalog catalog, ArchiveStore store)
  {
    var holder = MeleeLibraries.GetValue(catalog, _ => new MeleeCustomization?[1]);
    lock (holder)
      return holder[0] ??= MeleeCustomization.Load(catalog, store);
  }

  /// <summary>ui-45: <c>npc=TEMPLATE,layer=MeshTemplate|none</c> / <c>npcpreset=CLS</c> on any tpl (enemy / NPC Kitbash).</summary>
  private static VariantResult? ApplyNpcVariant(GameCatalog catalog, ArchiveStore store, string template, ExportedMesh mesh, VariantRequest request, ExportReport report)
  {
    var lib = InstConstructorLibrary.Load(catalog, store);
    if (lib is null)
    {
      report.Messages.Add($"{template}: {InstConstructorLibrary.TemplatesPath} is not in the catalog; every part exported.");
      return null;
    }

    var result = NpcVariantFilter.Apply(mesh, lib, request);
    result.Template = template;
    result.AppearanceSource = InstConstructorLibrary.TemplatesPath;
    report.Messages.Add($"{template}: NPC {request.Npc}{(request.NpcPreset is null ? "" : " preset " + request.NpcPreset)}: hid {result.HiddenParts} parts, {result.VisibleParts} visible ({result.ShownObjects.Count} picked objects).");
    foreach (var note in result.Notes.Take(8))
      report.Messages.Add(template + ": " + note);
    return result;
  }

  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameCatalog, WeaponCustomization?[]> WeaponLibraries = new();

  public static WeaponCustomization? WeaponLibrary(GameCatalog catalog, ArchiveStore store)
  {
    var holder = WeaponLibraries.GetValue(catalog, _ => new WeaponCustomization?[1]);
    lock (holder)
      return holder[0] ??= WeaponCustomization.Load(catalog, store);
  }

  /// <summary>Weapon tpls only when a --variant is given (default export keeps every part, as before).</summary>
  private static VariantResult? ApplyWeaponVariant(GameCatalog catalog, ArchiveStore store, string template, ExportedMesh mesh, ExportSettings settings, ExportReport report)
  {
    if (string.IsNullOrWhiteSpace(settings.Variant) || !template.StartsWith("wpn_", StringComparison.OrdinalIgnoreCase))
      return null;
    var request = VariantRequest.Parse(settings.Variant);
    if (request.All || (request.Skin is null && request.WeaponRegions is null && request.Show.Count == 0 && request.Hide.Count == 0))
      return null;
    var lib = WeaponLibrary(catalog, store);
    var uid = lib?.UidFor(template);
    if ((lib is null || uid is null) && MeleeLibrary(catalog, store) is { } melee && melee.ActorsFor(template).Count > 0)
    {
      var m = MeleeVariant.Apply(mesh, melee, template, request);
      m.AppearanceSource = MeleeCustomization.SkinsPath + "/" + m.Appearance;
      report.Messages.Add($"{template}: melee skin {m.Preset}: hid {m.HiddenParts} parts of {m.CustomizableObjects} customizable objects, {m.VisibleParts} visible.");
      foreach (var note in m.Notes.Take(8))
        report.Messages.Add(template + ": " + note);
      return m;
    }

    if (lib is null || uid is null)
    {
      report.Messages.Add($"{template}: no firearmUid (firearm mgs *_client.cls nameTpl) found; every part exported.");
      return null;
    }

    var result = WeaponVariant.Apply(mesh, lib, uid, request);
    result.AppearanceSource = "firearm mgs/" + uid;
    report.Messages.Add($"{template}: weapon skin {uid}/{result.Preset}: hid {result.HiddenParts} parts of {result.CustomizableObjects} customizable objects, {result.VisibleParts} visible.");
    foreach (var note in result.Notes.Take(8))
      report.Messages.Add(template + ": " + note);
    return result;
  }

  private static void WriteVariantSidecar(string path, VariantResult variant, ExportReport report)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, JsonSerializer.Serialize(variant, new JsonSerializerOptions { WriteIndented = true }));
    report.Written++;
  }

  /// <summary>True for a cloth simulation shell such as <c>tbrd_01_frnt_SIM_02</c> or <c>cloth_stamp_leg_SIM</c>.</summary>
  public static bool IsClothSim(string? name)
    => !string.IsNullOrEmpty(name) && Regex.IsMatch(name, "_SIM(_|$)", RegexOptions.IgnoreCase);

  internal static void HideClothSim(ExportedMesh mesh)
  {
    void Hide(MeshPart part)
    {
      if (!IsClothSim(part.Name))
        return;
      part.Visible = false;
      part.HideReason = "clothSim";
    }

    foreach (var part in mesh.Parts)
      Hide(part);
    foreach (var level in mesh.Levels)
    {
      foreach (var part in level.Parts)
        Hide(part);
    }
  }

  /// <summary>--include-cloth-sim: the reader hides *_SIM shells by default (mtl1-42), so turn them back on.</summary>
  internal static void ShowClothSim(ExportedMesh mesh)
  {
    foreach (var part in mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)))
      if (IsClothSim(part.Name))
      {
        part.Visible = true;
        part.HideReason = null;
      }
  }

  internal static ExportedMesh SelectExportGeometry(ExportedMesh mesh, int lod)
  {
    if (lod < 0)
      throw new InvalidDataException("LOD is negative.");
    if (lod == 0)
      return mesh.VisibleGeometry();
    if (lod > mesh.Levels.Count)
      throw new InvalidDataException($"LOD {lod} is not in this model. Lod 0 is the Index-visible set, and this model has {mesh.Levels.Count} smaller vertex pools.");

    var level = mesh.Levels[lod - 1];
    var copy = new ExportedMesh
    {
      Name = mesh.Name,
      Material = mesh.Material,
      Clip = mesh.Clip,
      Message = mesh.Message,
    };
    copy.Bones.AddRange(mesh.Bones);
    copy.Positions.AddRange(level.Positions);
    copy.Normals.AddRange(level.Normals);
    copy.Uvs.AddRange(level.Uvs);
    copy.Indices.AddRange(level.Indices);
    copy.Skin.AddRange(level.Skin);
    foreach (var part in level.Parts)
    {
      if (part.IndexCount < 3 || part.HideReason == "clothSim")
        continue;
      var copied = part.Copy(part.IndexStart, part.IndexCount);
      copied.Visible = true;
      copy.Parts.Add(copied);
    }

    return copy;
  }

  private static void AttachResolvedNormals(GameCatalog catalog, int index, ExportedMesh mesh)
  {
    foreach (var part in mesh.Parts)
    {
      if (!string.IsNullOrWhiteSpace(part.Normal) || string.IsNullOrWhiteSpace(part.Texture))
        continue;
      var candidate = part.Texture + "_nm";
      if (TemplateMeshReader.ResolveTexture(catalog, index, candidate) >= 0)
        part.Normal = candidate;
    }
  }

  internal static string RelativeTexturePath(string modelFile, string textureFile)
  {
    var modelDir = Path.GetDirectoryName(Path.GetFullPath(modelFile))!;
    return Path.GetRelativePath(modelDir, Path.GetFullPath(textureFile)).Replace('\\', '/');
  }

  private static Dictionary<string, string> TextureFiles(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    ExportedMesh mesh,
    string modelFile,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var extension = settings.WritePng ? ".png" : settings.WriteTga ? ".tga" : settings.WriteDds ? ".dds" : null;
    if (extension is null)
      return map;

    foreach (var name in TextureNames(mesh))
    {
      var resolved = TemplateMeshReader.ResolveTexture(catalog, index, name);
      var linkedKind = resolved < 0 ? AssetKind.Other : catalog.GetFile(resolved).Kind;
      if (resolved < 0 || linkedKind != AssetKind.Texture || !PixelsPresent(catalog, store, resolved))
      {
        if (!IsGenerated(mesh, name))
          ForgetTexture(mesh, name);
        continue;
      }

      var linked = catalog.GetFile(resolved);
      var destination = Destination(outputDirectory, linked.Path, extension, batch);
      map[name] = RelativeTexturePath(modelFile, destination);
    }

    foreach (var solid in GeneratedSolids(mesh))
    {
      if (map.ContainsKey(solid.Name))
        continue;
      var destination = Destination(outputDirectory, "pct/" + solid.Name + ".pct.resource", extension, batch);
      WriteSolid(destination, solid, extension);
      map[solid.Name] = RelativeTexturePath(modelFile, destination);
      report.Written++;
    }

    return map;
  }

  private static bool PixelsPresent(GameCatalog catalog, ArchiveStore store, int index)
  {
    try
    {
      var document = TextureLoader.Load(catalog, store, index);
      return document.CanDecode && document.Payload.Length > 0;
    }
    catch (Exception)
    {
      return false;
    }
  }

  private static bool IsGenerated(ExportedMesh mesh, string name)
  {
    foreach (var part in mesh.Parts)
    {
      if (part.Maps.Any(map => map.Generated && string.Equals(map.Name, name, StringComparison.OrdinalIgnoreCase)))
        return true;
    }

    return false;
  }

  private static void ForgetTexture(ExportedMesh mesh, string name)
  {
    foreach (var part in mesh.Parts)
    {
      if (string.Equals(part.Texture, name, StringComparison.OrdinalIgnoreCase))
        part.Texture = null;
      if (string.Equals(part.Normal, name, StringComparison.OrdinalIgnoreCase))
        part.Normal = null;
      if (string.Equals(part.Specular, name, StringComparison.OrdinalIgnoreCase))
        part.Specular = null;
      if (string.Equals(part.Emissive, name, StringComparison.OrdinalIgnoreCase))
        part.Emissive = null;
      foreach (var map in part.Maps)
      {
        if (!map.Generated && string.Equals(map.Name, name, StringComparison.OrdinalIgnoreCase))
          map.Missing = true;
      }
    }
  }

  private static void WriteSolid(string path, MaterialMap solid, string extension)
  {
    var pixels = new byte[4 * 4 * 4];
    for (var i = 0; i < pixels.Length; i += 4)
    {
      pixels[i] = solid.SolidB;
      pixels[i + 1] = solid.SolidG;
      pixels[i + 2] = solid.SolidR;
      pixels[i + 3] = solid.SolidA;
    }

    if (extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
      ImageWriters.WriteTga(path, pixels, 4, 4);
    else
      ImageWriters.WritePng(path, pixels, 4, 4);
  }

  private static IEnumerable<MaterialMap> GeneratedSolids(ExportedMesh mesh)
  {
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var part in mesh.Parts)
    {
      foreach (var map in part.Maps)
      {
        if (map.Generated && seen.Add(map.Name))
          yield return map;
      }
    }
  }

  internal static void ExportModelTextures(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    ExportedMesh mesh,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch,
    bool partsOnly = false)
  {
    var names = TextureNames(mesh);
    // The tpl texture list (tex0..) and per-part material slots cover every armor piece. With a variant
    // filter on, only textures the visible parts reference are written.
    if (mesh.Material is not null && !partsOnly)
    {
      foreach (var slot in mesh.Material.Slots)
      {
        if (!string.IsNullOrWhiteSpace(slot.Texture))
          names.Add(slot.Texture);
      }
    }

    var selected = new HashSet<string>(batch, StringComparer.OrdinalIgnoreCase);
    var seen = new HashSet<int>();
    foreach (var name in names)
    {
      var resolved = TemplateMeshReader.ResolveTexture(catalog, index, name);
      if (resolved < 0 || !seen.Add(resolved))
        continue;
      var linked = catalog.GetFile(resolved);
      if (linked.Kind != AssetKind.Texture || selected.Contains(linked.Path))
        continue;
      try
      {
        ExportTexture(catalog, store, resolved, linked, outputDirectory, settings, report, batch);
      }
      catch (Exception ex)
      {
        report.Messages.Add(name + ": " + ex.Message);
      }
    }
  }

  private static void ExportAnimation(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var clip = SaniReader.Load(catalog, store, index);
    var jsonPath = Destination(outputDirectory, file.Path, ".anim.json", batch);
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
      clip.Name,
      clip.Duration,
      clip.SampleRate,
      bones = clip.Bones,
      note = clip.Notes.Count == 0
        ? "No curves were produced. Particle systems are unrelated and are not converted."
        : string.Join(" ", clip.Notes),
      sequences = clip.Sequences,
      notes = clip.Notes,
      tracks = clip.Tracks.Select(track => new
      {
        track.Bone,
        track.BoneName,
        translation = track.Translation.Select(key => new { key.Time, key.Value.X, key.Value.Y, key.Value.Z }),
        rotation = track.Rotation.Select(key => new { key.Time, key.Value.X, key.Value.Y, key.Value.Z, key.Value.W }),
        scale = track.Scale.Select(key => new { key.Time, key.Value.X, key.Value.Y, key.Value.Z }),
      }),
    }, JsonOptions));
    report.Written++;

    if (settings.WriteGltf && clip.HasCurves)
    {
      var mesh = SkeletonForClip(clip);
      GltfWriter.Write(Destination(outputDirectory, file.Path, ".glb", batch), mesh, settings.Space);
      report.Written++;
      if (settings.WriteFbx && mesh.Bones.Count > 0)
      {
        FbxWriter.Write(Destination(outputDirectory, file.Path, ".fbx", batch), mesh, settings.Space, unrealRoot: settings.UnrealRootBone);
        report.Written++;
      }
    }
    else if (clip.Notes.Count > 0)
    {
      report.Messages.Add(file.Path + ": " + clip.Notes[0]);
    }
  }

  private static ExportedMesh SkeletonForClip(AnimationClip clip)
  {
    var mesh = new ExportedMesh { Name = clip.Name, Clip = clip };
    if (clip.Bones.Count > 0)
    {
      foreach (var name in clip.Bones)
        mesh.Bones.Add(new Bone { Name = name, Parent = -1 });
      return mesh;
    }

    var boneIds = clip.Tracks.Select(track => track.Bone).Where(bone => bone >= 0).Distinct().OrderBy(bone => bone).ToList();
    if (boneIds.Count == 0)
      boneIds.Add(0);
    foreach (var bone in boneIds)
    {
      var track = clip.Tracks.Find(item => item.Bone == bone);
      mesh.Bones.Add(new Bone { Name = track is { BoneName.Length: > 0 } ? track.BoneName : "bone" + bone, Parent = -1 });
    }

    foreach (var track in clip.Tracks)
    {
      var mapped = boneIds.IndexOf(track.Bone);
      if (mapped >= 0)
        track.Bone = mapped;
    }

    return mesh;
  }

  /// <summary>File-name marker for clips with the additive slot (<see cref="AnimationClip.HasAdditive"/>).</summary>
  public const string AdditiveSuffix = "__ADD";

  private static void WriteRebaseSidecar(string path, AnimationClip clip, ExportReport report)
  {
    // Unreal level space of a ROOT-rooted import at yaw 0 (bone 0 ref yaw 180 cancels the (-x, -z) of
    // AxisConvert.Basis plus the importer's Y flip): (x, y, z) m -> (x, z, y) * 100 cm. Matches the survey's
    // c06 CENTRE (-470621, -166166, 42000) cm.
    var o = clip.RebaseOffset;
    var ue = new System.Numerics.Vector3(o.X * 100, o.Z * 100, o.Y * 100);
    var json = JsonSerializer.Serialize(new
    {
      clip = clip.Name,
      note = "Removed by --anim-rebase first-frame. Place the actor at this transform (then play the clip) to get the level-space shot back.",
      sourceMeters = new[] { o.X, o.Y, o.Z },
      sourceYawDegrees = clip.RebaseYawDegrees,
      unrealLevelCm = new[] { ue.X, ue.Y, ue.Z },
    }, JsonOptions);
    File.WriteAllText(path, json);
    report.Written++;
  }

  private static string AnimationRelative(string modelPath, string token)
  {
    var normalized = modelPath.Replace('\\', '/');
    var slash = normalized.LastIndexOf('/');
    var folder = slash >= 0 ? normalized[..(slash + 1)] : "";
    return folder + AssetClassification.BaseName(normalized) + "__" + token + ".tpl";
  }

  private static string ClipToken(string name)
  {
    var builder = new StringBuilder(name.Length);
    foreach (var c in name)
    {
      if (c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' or '\0' or ' ')
        builder.Append('_');
      else
        builder.Append(c);
    }

    var text = builder.ToString().Trim('_');
    return text.Length == 0 ? "clip" : text;
  }

  private static void ExportEffect(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    WriteOriginal(store, index, file, outputDirectory, report, null);
    var bytes = store.Read(index);
    if (Effects.SslText.LooksText(bytes))
    {
      ExportEffectText(catalog, store, index, file, bytes, outputDirectory, settings, report, batch);
      return;
    }

    var references = ScanReferences(bytes);
    var copied = new List<string>();
    foreach (var reference in references)
    {
      var resolved = catalog.ResolveReference(reference, index);
      if (resolved < 0)
        continue;
      var linked = catalog.GetFile(resolved);
      WriteOriginal(store, resolved, linked, outputDirectory, report, null);
      copied.Add(linked.Path);
    }

    var note = Destination(outputDirectory, file.Path, ".effects.txt", batch);
    var builder = new StringBuilder();
    builder.AppendLine("Particle systems and effects are not converted to Unreal or Blender.");
    builder.AppendLine("This folder contains the original effect bytes and any texture or model names found as plain text inside them.");
    builder.AppendLine("Those linked files are copied raw. They are not turned into meshes or materials.");
    builder.AppendLine();
    foreach (var path in copied.Distinct(StringComparer.OrdinalIgnoreCase))
      builder.AppendLine(path);
    if (copied.Count == 0)
      builder.AppendLine("No linked asset names were found in the effect bytes.");
    File.WriteAllText(note, builder.ToString());
    report.Written++;
  }

  /// <summary>
  ///   Retail .sfx are SSL text. Writes {name}.sfx.json (tree with tagged unions resolved),
  ///   {name}.niagara.json (one record per CPU/GPU emitter: renderer, blend, spawn, shape,
  ///   lifetime, size/color splines, gravity, textures, meshes), and exports every
  ///   referenced texture as PNG/TGA/DDS next to them.
  /// </summary>
  private static void ExportEffectText(
    GameCatalog catalog,
    ArchiveStore store,
    int index,
    CatalogFileInfo file,
    byte[] bytes,
    string outputDirectory,
    ExportSettings settings,
    ExportReport report,
    IReadOnlyList<string> batch)
  {
    var root = Effects.SslText.Parse(bytes);
    var name = Path.GetFileNameWithoutExtension(file.Path);
    var result = Effects.EffectSummary.Summarize(name, root);
    var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    var tree = Destination(outputDirectory, file.Path, ".sfx.json", batch);
    Directory.CreateDirectory(Path.GetDirectoryName(tree)!);
    File.WriteAllText(tree, Effects.EffectSummary.Resolve(root)!.ToJsonString(options));
    report.Written++;

    var resolvedTextures = new System.Text.Json.Nodes.JsonObject();
    foreach (var texture in result.Textures.OrderBy(x => x))
    {
      var hit = TemplateMeshReader.ResolveTexture(catalog, index, texture);
      if (hit < 0)
      {
        resolvedTextures[texture] = null;
        continue;
      }

      var linked = catalog.GetFile(hit);
      try
      {
        ExportTexture(catalog, store, hit, linked, outputDirectory, settings, report, batch);
        resolvedTextures[texture] = linked.Path;
      }
      catch (Exception ex)
      {
        report.Messages.Add(linked.Path + ": " + ex.Message);
      }
    }

    var meshes = new System.Text.Json.Nodes.JsonObject();
    foreach (var mesh in result.Meshes.OrderBy(x => x))
    {
      var path = mesh.Replace('\\', '/');
      var hit = -1;
      foreach (var candidate in new[] { path, path + ".tpl", "tpl/" + path + ".tpl/" + Path.GetFileName(path) + ".tpl", path + ".tpl.resource" })
      {
        hit = catalog.ResolveReference(candidate, index);
        if (hit >= 0) break;
      }
      meshes[mesh] = hit >= 0 ? catalog.GetFile(hit).Path : null;
    }

    result.Summary["textureFiles"] = resolvedTextures;
    result.Summary["meshFiles"] = meshes;
    var summary = Destination(outputDirectory, file.Path, ".niagara.json", batch);
    File.WriteAllText(summary, result.Summary.ToJsonString(options));
    report.Written++;
  }

  private static HashSet<string> TextureNames(ExportedMesh mesh)
  {
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void Take(string? name)
    {
      if (!string.IsNullOrWhiteSpace(name))
        names.Add(name);
    }

    void TakePart(MeshPart part)
    {
      Take(part.Texture);
      Take(part.Normal);
      Take(part.Specular);
      Take(part.Emissive);
      foreach (var map in part.Maps)
      {
        if (!map.Missing)
          Take(map.Name);
      }
    }

    foreach (var part in mesh.Parts)
      TakePart(part);
    foreach (var level in mesh.Levels)
    {
      foreach (var part in level.Parts)
        TakePart(part);
    }

    return names;
  }

  private static void WriteMaterialSidecar(string fbxPath, ExportedMesh mesh, IReadOnlyDictionary<string, string> files, ExportReport report)
  {
    var slots = MaterialMaps.Slots(mesh).Select(slot => new
    {
      slot = slot.Name,
      variant = slot.Part.Variant,
      blend = slot.Part.Blend,
      albedoTint = slot.Part.AlbedoTint,
      blendMode = slot.Part.BlendMode,
      // glt.second_spec / glt.anisotropic_spec (null when the variant leaves them disabled). Tints are R, G, B, A in 0..1.
      secondSpec = slot.Part.SecondSpec is { } second ? new
      {
        metallicTint = second.MetallicTint,
        metalness = new { scale = second.MetalnessScale, bias = second.MetalnessBias },
        roughness = new { scale = second.RoughnessScale, bias = second.RoughnessBias },
      } : null,
      // glt-level base lobe inputs (always written; engine defaults when the .td leaves them unset).
      baseLayer = new
      {
        metallicTint = slot.Part.BaseLayer.MetallicTint,
        reflectance = slot.Part.BaseLayer.Reflectance,
        metalness = new { scale = slot.Part.BaseLayer.MetalnessScale, bias = slot.Part.BaseLayer.MetalnessBias },
        roughness = new { scale = slot.Part.BaseLayer.RoughnessScale, bias = slot.Part.BaseLayer.RoughnessBias },
      },
      anisotropicSpec = slot.Part.AnisoSpec is { } aniso ? new
      {
        angle = aniso.Angle,
        anisotropy = aniso.Anisotropy,
        intensity = aniso.Intensity,
        dirMap = aniso.DirMap.Length == 0 ? null : aniso.DirMap,
        extra = new { anisotropy = aniso.ExtraAnisotropy, intensity = aniso.ExtraIntensity, shift = aniso.ExtraShift },
      } : null,
      // glt.detail {density, scale} (null when absent). HEURISTIC: Unreal tiles the detail normal by density and blends it by scale.
      detail = slot.Part.Detail is { } det ? new { density = det.Density, scale = det.Scale } : null,
      // glt.chameleon (null when disabled): colors R, G, B, A in 0..1; see ChameleonSpec for the shader ramp.
      chameleon = slot.Part.Chameleon is { } cham ? new
      {
        colors = cham.Colors,
        offset1 = cham.Offset1,
        offset2 = cham.Offset2,
        fresnelPwr = cham.FresnelPower,
      } : null,
      // Saber material layer0..3 (non-empty only). layers[0] is the base; later layers blend over it by
      // blending.method/weights (vertex colour set weightsColorSet, or layer alpha / heightmap). Not shaded yet.
      layers = slot.Part.Layers.Select(layer => new
      {
        index = layer.Index,
        texture = layer.Texture,
        subMaterial = layer.SubMaterial,
        tint = layer.Tint,
        vcSet = layer.VcSet,
        uvSet = layer.UvSet,
        tiling = new[] { layer.TilingU, layer.TilingV },
        blending = new
        {
          method = layer.BlendMethod,
          useLayerAlpha = layer.UseLayerAlpha,
          useHeightmap = layer.UseHeightMap,
          blendNormalAdd = layer.BlendNormalAdd,
          weightMultiplier = layer.WeightMultiplier,
          heightmapSoftness = layer.HeightMapSoftness,
          texChannelBlendMask = layer.TexChannelBlendMask,
          weightsColorSet = layer.WeightsColorSet,
          heightmapColorSet = layer.HeightMapColorSet,
          heightmapInvert = layer.HeightMapInvert,
          heightmapOverride = layer.HeightMapOverride,
        },
      }),
      maps = slot.Part.Maps.Select(map => new
      {
        role = map.Role,
        name = map.Name,
        file = files.TryGetValue(map.Name, out var file) ? file : null,
        channels = map.Channels,
        srgb = map.Srgb,
        generated = map.Generated,
        missing = map.Missing,
        format = map.Format,
        stored = map.StoredChannels,
        packing = map.Packing.Select(pack => new { channel = pack.Channel, meaning = pack.Meaning, stored = pack.Stored.Length == 0 ? pack.Channel : pack.Stored }),
      }),
    });
    var jsonPath = Path.Combine(Path.GetDirectoryName(fbxPath)!, Path.GetFileNameWithoutExtension(fbxPath) + ".materials.json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
      mesh = mesh.Name,
      fbx = Path.GetFileName(fbxPath),
      note = "Each map's channels are the .td format_descr (logical channels). stored/packing[].stored say where each one physically is in the exported PNG: BC5 (DXN) keeps two channels, so Roughness(a) is in g; BC4 (DXT5A) keeps one, so AO(a) is in r. The game swizzles these in its shader (swizzleTexInShader). srgb false is a normal or a packed mask, so Unreal imports it linear. The FBX links diffuse, normal, and specular. import_sm2_materials.py builds a master material and one instance per slot from this file. secondSpec and anisotropicSpec are the variant's glt.second_spec and glt.anisotropic_spec (second lobe metalness/roughness = saturate(packed * scale + bias)); absent means disabled. baseLayer is the glt-level metallicTint, reflectance and metalness/roughness {scale, bias} (base metal/rough = saturate(packed * scale + bias), albedo *= lerp(albedo_tint, metallicTint, metal), dielectric F0 = 0.16 * reflectance^2). detail is glt.detail {density, scale} (absent = no block): the script tiles the detail normal UVs by density and scales its blend by scale (HEURISTIC). With Substrate the script builds base + second as two added Slabs (SecondSpecGain 4 without aniso, 1 with aniso; aniso extra{} drives the second slab).",
      slots,
    }, JsonOptions));
    report.Written++;
  }

  private static void WriteUnrealScript(string path, ExportReport report)
  {
    var assembly = typeof(AssetExporter).Assembly;
    var file = Path.GetFileName(path);
    var name = assembly.GetManifestResourceNames().FirstOrDefault(item => item.EndsWith(file, StringComparison.Ordinal));
    if (name is null)
      return;
    using var stream = assembly.GetManifestResourceStream(name);
    if (stream is null)
      return;
    using var reader = new StreamReader(stream);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, reader.ReadToEnd().Replace("\r\n", "\n"));
    report.Written++;
  }

  private static void WriteMaterial(string outputDirectory, string relative, MaterialDocument? material, ExportSettings settings, ExportReport report, IReadOnlyList<string> batch)
  {
    if (!settings.WriteMaterials || material is null || (material.Slots.Count == 0 && material.Notes.Count == 0))
      return;

    var path = Destination(outputDirectory, relative, ".material.json", batch);
    File.WriteAllText(path, JsonSerializer.Serialize(new
    {
      material.Name,
      note = "Texture slots named by the Saber material or the .tpl.resource. The textures themselves are exported from their .pct.resource.",
      slots = material.Slots.Select(slot => new { slot.Slot, slot.Texture, slot.Tint }),
      notes = material.Notes,
    }, JsonOptions));
    report.Written++;
  }

  /// <summary>
  /// BC5 masks (MetGray(r)+Roughness(a)) store two channels. B is not a normal Z here.
  /// Decode returns BGRA, so blue is byte 0. (Byte 2 is red = metalness; zeroing it made every spec mask non-metal.)
  /// </summary>
  public static void FixBc5MaskPixels(byte[] pixels)
  {
    for (var i = 0; i < pixels.Length; i += 4)
      pixels[i] = 0;
  }

  private static string Destination(string outputDirectory, string relative, string extension, IReadOnlyList<string> batch)
  {
    var plain = OutputPaths.Map(outputDirectory, relative, extension);
    foreach (var other in batch)
    {
      if (string.Equals(other, relative, StringComparison.OrdinalIgnoreCase))
        continue;
      if (OutputPaths.Map(outputDirectory, other, extension) == plain)
        return OutputPaths.Map(outputDirectory, relative, extension, keepFileName: true);
    }

    return plain;
  }

  private static void WriteOriginal(ArchiveStore store, int index, CatalogFileInfo file, string outputDirectory, ExportReport report, string? note)
  {
    var path = OutputPaths.Map(outputDirectory, file.Path, file.Extension.Length == 0 ? ".bin" : file.Extension);
    File.WriteAllBytes(path, store.Read(index));
    report.Written++;
    if (note is not null)
    {
      File.WriteAllText(OutputPaths.Map(outputDirectory, file.Path, ".txt"), note);
      report.Written++;
    }
  }

  public static List<string> ScanReferences(ReadOnlySpan<byte> data)
  {
    var found = new List<string>();
    var start = -1;
    for (var i = 0; i <= data.Length && i < 4 * 1024 * 1024; i++)
    {
      var printable = i < data.Length && data[i] is >= 32 and < 127;
      if (printable)
      {
        if (start < 0)
          start = i;
        continue;
      }

      if (start >= 0 && i - start >= 5)
      {
        var text = Encoding.ASCII.GetString(data.Slice(start, i - start));
        if (IsAssetReference(text))
          found.Add(text);
      }

      start = -1;
    }

    return found;
  }

  private static bool IsAssetReference(string text)
  {
    var lower = text.ToLowerInvariant();
    return lower.EndsWith(".pct") || lower.EndsWith(".pct_mip") || lower.EndsWith(".pct.resource")
      || lower.EndsWith(".tpl") || lower.EndsWith(".tpl_data") || lower.EndsWith(".lods_base")
      || lower.EndsWith(".sani") || lower.EndsWith(".td") || lower.Contains(".tpl") || lower.Contains(".pct");
  }

  private static string Notes(ExportSettings settings)
  {
    var space = settings.Space == ExportSpace.Blender
      ? "Blender: FBX is Z-up meters. glTF is Y-up meters."
      : "Unreal: FBX is Z-up centimeters (source meters times 100). glTF is Y-up meters, which both Unreal and Blender import.";
    return AxisConvert.Assumption + Environment.NewLine
      + space + Environment.NewLine
      + "DDS keeps the original block compression. PNG and TGA are decoded previews." + Environment.NewLine
      + "Most textures are .pct.resource plus .pct_mip. A bare .pct is uncommon." + Environment.NewLine
      + "Models are .tpl plus .tpl_data. Lod 0 keeps the meshes Index leaves checked (the largest pool) and drops _LOD, collision, and *_SIM cloth simulation shells (the game never draws them; --include-cloth-sim keeps them). A higher lod writes that smaller vertex pool. FBX is binary 7.4, Z-up, with a skinned limb hierarchy. Each exported part gets a material whose diffuse (and _nm normal, when that texture is in the catalog) is a path relative to the FBX, pointing at the PNG or TGA written beside it. .lods_base, .geom_dbg, and .cdt are not mesh payloads." + Environment.NewLine
      + "Animations: a retail .sani is an ETNA version-2 clip. 0xF2 stores the codec and every frame. 0xF6 stores the codec plus a few preview frames, and the sibling .sani_data holds the rest. Codes are unsigned (value = code * scale + min). Translation is an 11/11/10-bit word. Rotation is three codes through a modified Rodrigues quaternion, and that quaternion replaces the bone's bind rotation. Constant tracks are written for every bone the clip lists. Bones the clip does not name stay on the model bind pose. Scalar channels are not bone scale. The 0xF4 track is added to the skeleton root translation unless export is run with --no-root-motion. export --anim writes {model}__{clip}.fbx and .glb beside the bind mesh." + Environment.NewLine
      + "Effects: a text .sfx (SSL) is written as {name}.sfx.json (local__X unions resolved to the live member) and {name}.niagara.json (per CPU/GPU emitter: renderer, blend, spawn, shape, lifetime, size and color splines, gravity, textures, meshes), and its textures are exported. Binary .ps/.sfx stay raw with any named files copied. Nothing is turned into a Niagara asset automatically." + Environment.NewLine;
  }

}

public static class OutputPaths
{

  public static string Map(string root, string relative, string extensionWithDot, bool keepFileName = false)
  {
    if (!extensionWithDot.StartsWith('.'))
      extensionWithDot = "." + extensionWithDot;

    relative = relative.Replace('\\', '/').TrimStart('/');
    var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
      throw new InvalidDataException("Empty archive path.");

    var safe = new string[parts.Length];
    for (var i = 0; i < parts.Length; i++)
    {
      var part = parts[i];
      if (part is "." or "..")
        throw new InvalidDataException($"Refusing to export '{relative}'.");
      foreach (var c in part)
      {
        if (c < 32 || c is '<' or '>' or ':' or '"' or '|' or '?' or '*')
          throw new InvalidDataException($"Refusing to export '{relative}'.");
      }

      safe[i] = part;
    }

    var fileName = safe[^1];
    var baseName = keepFileName ? fileName : AssetClassification.BaseName(fileName);
    if (string.IsNullOrWhiteSpace(baseName))
      baseName = "file";

    var combined = new string[safe.Length + 1];
    combined[0] = root;
    for (var i = 0; i < safe.Length - 1; i++)
      combined[i + 1] = safe[i];
    combined[^1] = baseName + extensionWithDot;
    var full = Path.GetFullPath(Path.Combine(combined));
    var rootFull = Path.GetFullPath(root);
    var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException($"Export path left the output folder: {relative}");

    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    return full;
  }

}

public static class BytePreview
{

  public static string Hex(ReadOnlySpan<byte> data, int maxBytes = 4096)
  {
    var count = Math.Min(data.Length, maxBytes);
    var builder = new StringBuilder(count * 4);
    for (var i = 0; i < count; i += 16)
    {
      builder.Append(i.ToString("X8")).Append("  ");
      var line = Math.Min(16, count - i);
      for (var j = 0; j < line; j++)
        builder.Append(data[i + j].ToString("X2")).Append(' ');
      builder.AppendLine();
    }

    if (data.Length > count)
      builder.Append("… ").Append(data.Length - count).Append(" more bytes");
    return builder.ToString();
  }

  public static string Text(ReadOnlySpan<byte> data, int maxBytes = 200_000)
  {
    var count = Math.Min(data.Length, maxBytes);
    var text = Encoding.UTF8.GetString(data[..count]);
    if (data.Length > count)
      text += Environment.NewLine + $"… {data.Length - count} more bytes";
    return text;
  }

}
