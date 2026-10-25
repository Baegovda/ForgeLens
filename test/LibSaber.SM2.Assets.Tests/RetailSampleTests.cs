using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SpaceMarine2.Enumerations;

namespace LibSaber.SM2.Assets.Tests;

public class RetailSampleTests
{

  [Fact]
  public void RetailSample_DecodesTexturesModelsAndAnimations()
  {
    var root = Environment.GetEnvironmentVariable("LIBSABER_SM2_SAMPLE");
    // Retail bytes stay out of the repo. This runner treats Xunit.Sdk.SkipException as a failure,
    // so an unset path returns and the test is reported as passed without checking files.
    if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
      return;

    using var dir = new TempDirectory();
    var entries = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
      .Select(path => new CacheFile.Entry(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes(path)))
      .ToArray();
    CacheFile.Create(dir.Path, "sample.pak", entries);
    var catalog = GameCatalog.Load(dir.Path);
    using var store = new ArchiveStore(catalog);

    Assert.Contains(catalog.Root.Children, child => child.Name == "tpl");
    Assert.Contains(catalog.Root.Children, child => child.Name == "pct");
    Assert.Contains(catalog.Root.Children, child => child.Name == "animation");

    var helmetTex = LoadTexture(catalog, store, "pct/ch_chaos_berserk_helmet_01_nm.pct.resource");
    Assert.Equal(SM2TextureFormat.DXN, helmetTex.Format);
    Assert.Equal(2048, helmetTex.Width);
    Assert.Equal(5592400, helmetTex.Payload.Length);
    var helmetPixels = helmetTex.Decode(0, 0, out var width, out var height, out _);
    Assert.Equal(2048, width);
    Assert.Equal(2048, height);
    // Decode returns BGRA. Channel 0 is the reconstructed Z, stored in blue.
    var blue = Average(helmetPixels, 0);
    var green = Average(helmetPixels, 1);
    var red = Average(helmetPixels, 2);
    Assert.True(blue > red + 15 && blue > green + 15,
      $"BC5 normal preview should come out blue (BGRA B={blue:0.0} G={green:0.0} R={red:0.0}).");

    var spec = LoadTexture(catalog, store, "pct/imp_bench_02_old_spec.pct.resource");
    Assert.Equal(SM2TextureFormat.DXN, spec.Format);
    Assert.Equal(1024, spec.Width);
    var specPixels = spec.Decode(0, 0, out width, out height, out _);
    Assert.Equal(1024, width);
    Assert.Equal(1024, height);
    // Same block format as a normal, but this map is a specular mask: red stays near 0.
    Assert.True(Average(specPixels, 2) < 40, "Specular DXN should stay dark in red, not be treated as a unit normal.");

    var font = LoadTexture(catalog, store, "font_main/font_main_00.pct.resource");
    Assert.Equal(SM2TextureFormat.XT5, font.Format);
    var fontPixels = font.Decode(0, 0, out width, out height, out _);
    Assert.Equal(512, width);
    var transparent = 0;
    var opaque = 0;
    for (var i = 3; i < fontPixels.Length; i += 4)
    {
      if (fontPixels[i] < 16)
        transparent++;
      else if (fontPixels[i] > 200)
        opaque++;
    }

    Assert.True(transparent > 1000 && opaque > 1000);

    var black = LoadTexture(catalog, store, "pct/__black__.pct.resource");
    var blackPixels = black.Decode(0, 0, out _, out _, out _);
    Assert.Equal(new byte[] { 0, 0, 0, 255 }, blackPixels);

    var shield = LoadMesh(catalog, store, "tpl/marine_shield.tpl/marine_shield.tpl");
    Assert.Equal(1120, shield.Positions.Count);
    Assert.Equal(2000, shield.Indices.Count / 3);
    Assert.Equal(shield.Positions.Count, shield.Uvs.Count);
    Assert.Equal(shield.Positions.Count, shield.Normals.Count);
    Assert.Empty(shield.Skin);
    var (shieldMin, shieldMax) = Bounds(shield);
    Assert.InRange(shieldMax.X - shieldMin.X, 7.0f, 8.0f);

    var helmet = LoadMesh(catalog, store, "tpl/refrt_asn_spacemarine_armor_helmet_01.tpl/refrt_asn_spacemarine_armor_helmet_01.tpl.resource");
    Assert.Equal(12416, helmet.Positions.Count);
    Assert.Equal(16300, helmet.Indices.Count / 3);
    Assert.Equal(helmet.Positions.Count, helmet.Uvs.Count);
    // Stored V is Index's on-screen coordinate (negated, then wrapped at sample time).
    Assert.True(helmet.Uvs.Exists(uv =>
    {
      var u = uv.X - MathF.Floor(uv.X);
      var v = uv.Y - MathF.Floor(uv.Y);
      return u > 0.05f && u < 0.95f && v > 0.05f && v < 0.95f;
    }));
    var (min, max) = Bounds(helmet);
    Assert.InRange(max.Y - min.Y, 0.25f, 0.40f);
    Assert.True(helmet.Normals.TrueForAll(normal => MathF.Abs(normal.Length() - 1f) < 0.02f));

    var clip = LoadClip(catalog, store, "animation/archive/npc/zombie/hit/mocap_static_hit_v5_swarm_hormagaunt.sani.resource");
    Assert.InRange(clip.Duration, 3.5f, 3.6f);
    Assert.Equal(30f, clip.SampleRate);
    Assert.Equal(73, clip.Bones.Count);
    Assert.Contains("CENTRE", clip.Bones);
    Assert.Contains("TAIL6", clip.Bones);
    Assert.Contains("Footsteps", clip.Sequences);
    Assert.Contains("snd_bodyfall", clip.Sequences);
    AssertCurves(clip, rotations: 73, translations: 73, frames: 107);

    var jump = LoadClip(catalog, store, "animation/archive/npc/zombie/jumping/jump_down_new/npc_zombie_jump_down_new_death_landing_v1_swarm_hormagaunt.sani.resource");
    Assert.InRange(jump.Duration, 1.9f, 2.1f);
    Assert.Equal(30f, jump.SampleRate);
    Assert.Equal(73, jump.Bones.Count);
    AssertCurves(jump, rotations: 73, translations: 73, frames: 60);
    var centre = jump.Tracks.Single(track => track.BoneName == "CENTRE");
    var first = centre.Rotation[0].Value;
    Assert.InRange(first.X, 0.47f, 0.50f);
    Assert.InRange(first.Y, 0.40f, 0.44f);
    Assert.InRange(first.Z, 0.56f, 0.60f);
    Assert.InRange(first.W, 0.49f, 0.52f);

    var output = Path.Combine(dir.Path, "out");
    var index = catalog.FindByPath("tpl/marine_shield.tpl/marine_shield.tpl").First();
    var report = AssetExporter.Export(catalog, store, new[] { index }, output, new ExportSettings());
    Assert.True(report.Written >= 2);
    var glb = File.ReadAllBytes(Directory.GetFiles(output, "*.glb", SearchOption.AllDirectories).Single());
    Assert.Equal(new byte[] { (byte)'g', (byte)'l', (byte)'T', (byte)'F' }, glb[..4]);
    var json = System.Text.Encoding.UTF8.GetString(glb);
    Assert.Contains("TEXCOORD_0", json);
    var fbx = File.ReadAllBytes(Directory.GetFiles(output, "*.fbx", SearchOption.AllDirectories).Single());
    Assert.Equal("Kaydara FBX Binary  "u8.ToArray(), fbx[..20]);
    Assert.Equal(7400, BitConverter.ToInt32(fbx, 23));
    Assert.True(fbx.AsSpan().IndexOf("LayerElementUV"u8) >= 0);
    Assert.True(fbx.AsSpan().IndexOf("LayerElementNormal"u8) >= 0);
  }

  [Fact]
  public void RetailTitus_DecodesEveryPartAndPairsSkin()
  {
    var root = Environment.GetEnvironmentVariable("LIBSABER_SM2_TITUS");
    // Same runner limitation as the other retail test: an unset path returns and is reported as passed.
    if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
      return;

    using var dir = new TempDirectory();
    var entries = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
      .Select(path => new CacheFile.Entry(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes(path)))
      .ToArray();
    CacheFile.Create(dir.Path, "titus.pak", entries);
    var catalog = GameCatalog.Load(dir.Path);
    using var store = new ArchiveStore(catalog);
    var mesh = LoadMesh(catalog, store, "tpl/cc_titus_assault.tpl/cc_titus_assault.tpl");

    Assert.Equal(413472, mesh.Positions.Count);
    Assert.Equal(309390, mesh.Indices.Count / 3);
    var arms = mesh.Parts.First(part => part.Texture == "ch_titus_arms_02");
    float armMinV = 1, armMaxU = 0;
    var armEnd = Math.Min(mesh.Indices.Count, arms.IndexStart + arms.IndexCount);
    for (var i = Math.Max(0, arms.IndexStart); i < armEnd; i++)
    {
      var uv = mesh.Uvs[mesh.Indices[i]];
      armMinV = MathF.Min(armMinV, uv.Y);
      armMaxU = MathF.Max(armMaxU, uv.X);
    }

    // Index multiplies the serializer UV by UvScaling (2 on the first ch_titus_arms_02 island)
    // and the viewer negates V. The stored coordinate is therefore about U in [0, 2] and
    // V in [-2, -1]. MeshPreview wraps that into the atlas the same way Helix does.
    Assert.InRange(armMinV, -2.05f, -0.9f);
    Assert.InRange(armMaxU, 1.5f, 2.05f);
    Assert.Contains(mesh.Parts, part => part.Name == "head_01" && part.Visible);
    Assert.Contains(mesh.Parts, part => part.Name == "titus_torso_01" && part.Visible);
    Assert.DoesNotContain(mesh.Parts, part => part.Name.Contains("_LOD", StringComparison.OrdinalIgnoreCase));
    var lower = mesh.Levels.SelectMany(level => level.Parts).ToList();
    Assert.Contains(lower, part => part.Name.Contains("_LOD", StringComparison.OrdinalIgnoreCase) && !part.Visible && part.HideReason == "LOD");
    Assert.Contains(lower, part => part.Name.Contains("cdt", StringComparison.OrdinalIgnoreCase) && !part.Visible && part.HideReason == "volume");
    Assert.Contains(lower, part => part.Name == "cloth_stamp_leg_SIM" && part.Visible);
    Assert.Equal(mesh.Positions.Count, mesh.Uvs.Count);
    Assert.Equal(mesh.Positions.Count, mesh.Normals.Count);
    Assert.Equal(mesh.Positions.Count, mesh.Skin.Count);
    Assert.True(mesh.HasSkin);
    Assert.InRange(mesh.Parts.Count, 30, 60);
    Assert.InRange(mesh.Levels.Count, 4, 12);
    Assert.Contains("Paired all 199", mesh.Message);
    Assert.Contains(mesh.Bones, bone => bone.Name == "CENTRE" || bone.Name.StartsWith("CENTRE"));
    var inverse = mesh.Bones[0].InverseBind;
    Assert.True(MathF.Abs(inverse.M41) + MathF.Abs(inverse.M42) + MathF.Abs(inverse.M43) > 0.5f);

    var (min, max) = Bounds(mesh);
    Assert.InRange(max.Y - min.Y, 2.4f, 2.8f);
    Assert.InRange(max.X - min.X, 1.6f, 2.2f);
    var unit = 0;
    var dropped = 0;
    var maxJoint = 0;
    foreach (var skin in mesh.Skin)
    {
      var sum = skin.W0 + skin.W1 + skin.W2 + skin.W3;
      if (MathF.Abs(sum - 1f) < 0.05f)
        unit++;
      if (skin.W0 > 0)
        maxJoint = Math.Max(maxJoint, skin.J0);
      if (skin.W1 > 0)
        maxJoint = Math.Max(maxJoint, skin.J1);
      if (skin.W2 > 0)
        maxJoint = Math.Max(maxJoint, skin.J2);
      if (skin.W3 > 0)
        maxJoint = Math.Max(maxJoint, skin.J3);
      if (skin.J0 >= mesh.Bones.Count || skin.J1 >= mesh.Bones.Count || skin.J2 >= mesh.Bones.Count || skin.J3 >= mesh.Bones.Count)
        dropped++;
    }

    Assert.True(unit > mesh.Skin.Count * 9 / 10, $"weights should sum to 1, {unit} of {mesh.Skin.Count}");
    Assert.Equal(0, dropped);
    Assert.InRange(maxJoint, 100, mesh.Bones.Count - 1);

    var image = MeshPreview.Render(
      mesh, 480, 720,
      new OrbitCamera { Yaw = 0.65f, Pitch = 0.16f, Distance = 0 },
      false, 0, out _);
    var minY = int.MaxValue;
    var maxY = int.MinValue;
    var lit = 0;
    for (var y = 0; y < 720; y++)
    {
      for (var x = 0; x < 480; x++)
      {
        var pixel = (y * 480 + x) * 4;
        if (image[pixel] == 22 && image[pixel + 1] == 24 && image[pixel + 2] == 28)
          continue;
        lit++;
        minY = Math.Min(minY, y);
        maxY = Math.Max(maxY, y);
      }
    }

    Assert.True(lit > 20000, $"Titus should fill the preview, lit {lit}");
    Assert.True(maxY - minY > 500, $"Titus should stand in frame, span {maxY - minY}");
  }

  [Fact]
  public void RetailClip_WritesFbxCurvesOnTheNamedBones()
  {
    var root = Environment.GetEnvironmentVariable("LIBSABER_SM2_SAMPLE");
    if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
      return;

    using var dir = new TempDirectory();
    var entries = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
      .Select(path => new CacheFile.Entry(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes(path)))
      .ToArray();
    CacheFile.Create(dir.Path, "sample.pak", entries);
    var catalog = GameCatalog.Load(dir.Path);
    using var store = new ArchiveStore(catalog);
    var jump = LoadClip(catalog, store, "animation/archive/npc/zombie/jumping/jump_down_new/npc_zombie_jump_down_new_death_landing_v1_swarm_hormagaunt.sani");
    var mesh = new ExportedMesh { Name = "stick", Clip = jump };
    mesh.Bones.Add(new Bone { Name = "CharacterWorldSpaceTM", Parent = -1 });
    mesh.Bones.Add(new Bone { Name = "CENTRE", Parent = 0, Local = Matrix4x4.CreateTranslation(0, 1, 0), InverseBind = Matrix4x4.CreateTranslation(0, -1, 0) });
    mesh.Bones.Add(new Bone { Name = "HEAD", Parent = 1, Local = Matrix4x4.CreateTranslation(0, 0.3f, 0), InverseBind = Matrix4x4.CreateTranslation(0, -1.3f, 0) });
    mesh.Positions.AddRange(new[] { new Vector3(0, 1, 0), new Vector3(0.1f, 1, 0), new Vector3(0, 1.2f, 0.05f) });
    mesh.Normals.AddRange(new[] { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ });
    mesh.Uvs.AddRange(new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY });
    mesh.Indices.AddRange(new[] { 0, 1, 2 });
    mesh.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });
    mesh.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });
    mesh.Skin.Add(new SkinWeights { J0 = 2, W0 = 1 });
    mesh.Parts.Add(new MeshPart { Name = "stick", IndexStart = 0, IndexCount = 3, Visible = true });

    var fbxPath = Path.Combine(dir.Path, "stick.fbx");
    var glbPath = Path.Combine(dir.Path, "stick.glb");
    FbxWriter.Write(fbxPath, mesh, ExportSpace.Unreal);
    GltfWriter.Write(glbPath, mesh, ExportSpace.Unreal);
    var fbx = File.ReadAllBytes(fbxPath);
    Assert.Contains("AnimationStack", System.Text.Encoding.ASCII.GetString(fbx));
    Assert.Contains("AnimationCurve", System.Text.Encoding.ASCII.GetString(fbx));
    Assert.Contains("CENTRE", System.Text.Encoding.ASCII.GetString(fbx));
    Assert.Contains("Lcl Rotation", System.Text.Encoding.ASCII.GetString(fbx));
    var glb = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(glbPath));
    Assert.Contains("\"animations\"", glb);
    Assert.Contains("CENTRE", glb);
    Assert.Contains("rotation", glb);
  }

  [Fact]
  public void RetailMarine_DecodesIdleAndAttackOntoTitusNames()
  {
    var root = Environment.GetEnvironmentVariable("LIBSABER_SM2_PICK");
    if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
      return;

    using var dir = new TempDirectory();
    var entries = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
      .Select(path => new CacheFile.Entry(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes(path)))
      .ToArray();
    CacheFile.Create(dir.Path, "pick.pak", entries);
    var catalog = GameCatalog.Load(dir.Path);
    using var store = new ArchiveStore(catalog);

    var idle = LoadClip(catalog, store, "animation/interplay/battle_barge/idle/marine_idle_v01_intercessor.sani.resource");
    Assert.Equal(286, idle.Bones.Count);
    Assert.InRange(idle.Duration, 40.4f, 40.5f);
    Assert.Equal(30f, idle.SampleRate);
    Assert.Contains("Footsteps", idle.Sequences);
    AssertCurves(idle, rotations: 282, translations: 282, frames: 1214);
    var idleCentre = idle.Tracks.Single(track => track.BoneName == "CENTRE").Rotation[0].Value;
    Assert.InRange(idleCentre.X, 0.45f, 0.48f);
    Assert.InRange(idleCentre.Y, 0.45f, 0.48f);
    Assert.InRange(idleCentre.Z, 0.53f, 0.56f);
    Assert.InRange(idleCentre.W, 0.51f, 0.54f);
    Assert.InRange(Arc(idle.Tracks.Single(track => track.BoneName == "BACKup")), 1f, 6f);
    AssertMatchesReference(root, idle, "marine_idle_v01_intercessor.sani");

    var attack = LoadClip(catalog, store, "animation/primaris/assault/melee/npc_assault_attack_melee_thunder_hammer_light_1_intercessor.sani");
    Assert.InRange(attack.Duration, 5.1f, 5.3f);
    Assert.Equal(30f, attack.SampleRate);
    Assert.Contains("vfx_hammer", attack.Sequences);
    AssertCurves(attack, rotations: 282, translations: 282, frames: 157);
    Assert.InRange(Arc(attack.Tracks.Single(track => track.BoneName == "CENTRE")), 110f, 135f);
    Assert.InRange(Arc(attack.Tracks.Single(track => track.BoneName == "ARM1R")), 140f, 160f);
    Assert.InRange(Arc(attack.Tracks.Single(track => track.BoneName == "shoulder_R")), 70f, 90f);
    Assert.InRange(Arc(attack.Tracks.Single(track => track.BoneName == "LEG1L")), 70f, 90f);
    AssertMatchesReference(root, attack, "npc_assault_attack_melee_thunder_hammer_light_1_intercessor.sani");

    var run = LoadClip(catalog, store, "animation/primaris/tacticus/loco/run/tacticus_unarmed_run_fwd_intercessor.sani");
    Assert.True(run.HasCurves);
    Assert.Equal(286, run.Bones.Count);
    Assert.InRange(run.Duration, 1.0f, 1.1f);
    AssertCurves(run, rotations: 282, translations: 282, frames: 33);
    Assert.InRange(Arc(run.Tracks.Single(track => track.BoneName == "LEG1L")), 45f, 60f);
    Assert.InRange(Arc(run.Tracks.Single(track => track.BoneName == "LEG1R")), 60f, 75f);
    Assert.InRange(Arc(run.Tracks.Single(track => track.BoneName == "LEG2L")), 70f, 85f);
    Assert.Equal(33, run.RootTranslation.Count);
    Assert.InRange(run.RootTranslation[0].Value.Length(), 0f, 0.001f);
    Assert.InRange(run.RootTranslation[^1].Value.X, -0.02f, 0.02f);
    Assert.InRange(run.RootTranslation[^1].Value.Y, -0.02f, 0.02f);
    Assert.InRange(run.RootTranslation[^1].Value.Z, 4.8f, 4.9f);
    Assert.InRange(run.RootRotation[0].Value.W, 0.99f, 1.01f);
    AssertMatchesReference(root, run, "tacticus_unarmed_run_fwd_intercessor.sani");

    var titus = Environment.GetEnvironmentVariable("LIBSABER_SM2_TITUS");
    if (string.IsNullOrWhiteSpace(titus) || !Directory.Exists(titus))
      return;

    using var titusDir = new TempDirectory();
    var titusEntries = Directory.EnumerateFiles(titus, "*", SearchOption.AllDirectories)
      .Select(path => new CacheFile.Entry(Path.GetRelativePath(titus, path).Replace('\\', '/'), File.ReadAllBytes(path)))
      .ToArray();
    CacheFile.Create(titusDir.Path, "titus.pak", titusEntries);
    var titusCatalog = GameCatalog.Load(titusDir.Path);
    using var titusStore = new ArchiveStore(titusCatalog);
    var bones = LoadMesh(titusCatalog, titusStore, "tpl/cc_titus_assault.tpl/cc_titus_assault.tpl").Bones;
    var names = bones.Select(bone => bone.Name).ToHashSet(StringComparer.Ordinal);
    Assert.Equal(281, idle.Bones.Count(name => names.Contains(name)));
    Assert.Equal(533, bones.Count);
    Assert.Contains(bones, bone => bone.Name == "Object00C9" && bone.Parent < 0);
  }

  private static void AssertMatchesReference(string root, AnimationClip clip, string fileName)
  {
    var sani = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).Single();
    var data = File.Exists(sani + "_data") ? sani + "_data" : null;
    var script = Path.Combine(AppContext.BaseDirectory, "decode_sani.py");
    Assert.True(File.Exists(script), script);
    var jsonPath = Path.Combine(Path.GetTempPath(), "sani-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
      var start = new ProcessStartInfo
      {
        FileName = "python3",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
      };
      start.ArgumentList.Add(script);
      start.ArgumentList.Add(sani);
      if (data is not null)
        start.ArgumentList.Add(data);
      start.ArgumentList.Add("--json");
      start.ArgumentList.Add(jsonPath);
      using var process = Process.Start(start);
      Assert.NotNull(process);
      var stdout = process.StandardOutput.ReadToEnd();
      var stderr = process.StandardError.ReadToEnd();
      process.WaitForExit();
      Assert.True(process.ExitCode == 0, stdout + stderr);
      using var document = JsonDocument.Parse(File.OpenRead(jsonPath));
      var body = document.RootElement;
      Assert.Equal(body.GetProperty("frames").GetInt32(), clip.Tracks.Max(track => Math.Max(track.Rotation.Count, track.Translation.Count)));
      foreach (var bone in body.GetProperty("bones").EnumerateObject())
      {
        var track = clip.Tracks.SingleOrDefault(item => item.BoneName == bone.Name);
        CompareChannel(bone.Name, bone.Value.GetProperty("rot"), track?.Rotation, true);
        CompareChannel(bone.Name, bone.Value.GetProperty("trans"), track?.Translation, false);
      }

      var rootMotion = body.GetProperty("root");
      if (rootMotion.ValueKind == JsonValueKind.Null)
      {
        Assert.Empty(clip.RootTranslation);
        return;
      }

      var frame = 0;
      foreach (var sample in rootMotion.EnumerateArray())
      {
        var translation = sample[0];
        var rotation = sample[1];
        Near(clip.RootTranslation[frame].Value.X, translation[0].GetDouble(), "root t " + frame);
        Near(clip.RootTranslation[frame].Value.Y, translation[1].GetDouble(), "root t " + frame);
        Near(clip.RootTranslation[frame].Value.Z, translation[2].GetDouble(), "root t " + frame);
        Near(clip.RootRotation[frame].Value.W, rotation[0].GetDouble(), "root q " + frame);
        Near(clip.RootRotation[frame].Value.X, rotation[1].GetDouble(), "root q " + frame);
        Near(clip.RootRotation[frame].Value.Y, rotation[2].GetDouble(), "root q " + frame);
        Near(clip.RootRotation[frame].Value.Z, rotation[3].GetDouble(), "root q " + frame);
        frame++;
      }

      Assert.Equal(frame, clip.RootTranslation.Count);
      Assert.Equal(frame, clip.RootRotation.Count);
    }
    finally
    {
      if (File.Exists(jsonPath))
        File.Delete(jsonPath);
    }
  }

  private static void CompareChannel(string bone, JsonElement frames, object? keys, bool rotation)
  {
    var index = 0;
    var present = 0;
    foreach (var frame in frames.EnumerateArray())
    {
      if (frame.ValueKind != JsonValueKind.Null)
        present++;
      index++;
    }

    if (rotation)
    {
      var rotationKeys = keys as List<QuatKey>;
      if (present == 0)
      {
        Assert.True(rotationKeys is null || rotationKeys.Count == 0, bone + " rotation");
        return;
      }

      Assert.NotNull(rotationKeys);
      Assert.Equal(index, rotationKeys.Count);
      index = 0;
      foreach (var frame in frames.EnumerateArray())
      {
        var value = rotationKeys[index].Value;
        Near(value.W, frame[0].GetDouble(), bone + " rot " + index);
        Near(value.X, frame[1].GetDouble(), bone + " rot " + index);
        Near(value.Y, frame[2].GetDouble(), bone + " rot " + index);
        Near(value.Z, frame[3].GetDouble(), bone + " rot " + index);
        index++;
      }

      return;
    }

    var translationKeys = keys as List<VectorKey>;
    if (present == 0)
    {
      Assert.True(translationKeys is null || translationKeys.Count == 0, bone + " translation");
      return;
    }

    Assert.NotNull(translationKeys);
    Assert.Equal(index, translationKeys.Count);
    index = 0;
    foreach (var frame in frames.EnumerateArray())
    {
      var value = translationKeys[index].Value;
      Near(value.X, frame[0].GetDouble(), bone + " tr " + index);
      Near(value.Y, frame[1].GetDouble(), bone + " tr " + index);
      Near(value.Z, frame[2].GetDouble(), bone + " tr " + index);
      index++;
    }
  }

  private static void Near(double actual, double expected, string label)
  {
    var delta = Math.Abs(actual - expected);
    Assert.True(delta <= 2e-5, label + " delta " + delta.ToString("G6") + " actual " + actual + " expected " + expected);
  }

  private static float Arc(BoneTrack track)
  {
    var origin = track.Rotation[0].Value;
    var arc = 0f;
    foreach (var key in track.Rotation)
    {
      var dot = MathF.Abs(Quaternion.Dot(origin, key.Value));
      arc = MathF.Max(arc, MathF.Acos(Math.Clamp(dot, -1f, 1f)) * 2f * (180f / MathF.PI));
    }

    return arc;
  }

  private static void AssertCurves(AnimationClip clip, int rotations, int translations, int frames)
  {
    Assert.True(clip.HasCurves);
    Assert.Equal(rotations, clip.Tracks.Count(track => track.Rotation.Count > 0));
    Assert.Equal(translations, clip.Tracks.Count(track => track.Translation.Count > 0));
    var steps = new List<float>();
    foreach (var track in clip.Tracks)
    {
      foreach (var key in track.Rotation)
      {
        var length = key.Value.Length();
        Assert.InRange(length, 0.98f, 1.02f);
      }

      Assert.Equal(track.Rotation.Count == 0 ? 0 : frames, track.Rotation.Count);
      Assert.Equal(track.Translation.Count == 0 ? 0 : frames, track.Translation.Count);
      for (var i = 1; i < track.Rotation.Count; i++)
      {
        var dot = MathF.Abs(Quaternion.Dot(track.Rotation[i - 1].Value, track.Rotation[i].Value));
        steps.Add(MathF.Acos(Math.Clamp(dot, -1f, 1f)) * 2f * (180f / MathF.PI));
      }

      for (var i = 1; i < track.Translation.Count; i++)
        Assert.True(Vector3.Distance(track.Translation[i - 1].Value, track.Translation[i].Value) < 1.5f);
    }

    steps.Sort();
    Assert.True(steps.Count > 0);
    Assert.InRange(steps[steps.Count / 2], 0f, 8f);
  }

  private static TextureDocument LoadTexture(GameCatalog catalog, ArchiveStore store, string path)
  {
    var index = catalog.FindByPath(path).Single();
    return TextureLoader.Load(catalog, store, index);
  }

  private static ExportedMesh LoadMesh(GameCatalog catalog, ArchiveStore store, string path)
  {
    var index = catalog.FindByPath(path).Single();
    return TemplateMeshReader.Load(catalog, store, index);
  }

  private static AnimationClip LoadClip(GameCatalog catalog, ArchiveStore store, string path)
  {
    var index = catalog.FindByPath(path).Single();
    return SaniReader.Load(catalog, store, index);
  }

  private static float Average(byte[] pixels, int channel)
  {
    long sum = 0;
    var count = 0;
    for (var i = channel; i < pixels.Length; i += 4)
    {
      sum += pixels[i];
      count++;
    }

    return count == 0 ? 0 : sum / (float)count;
  }

  private static (Vector3 Min, Vector3 Max) Bounds(ExportedMesh mesh)
  {
    var min = mesh.Positions[0];
    var max = mesh.Positions[0];
    foreach (var position in mesh.Positions)
    {
      min = Vector3.Min(min, position);
      max = Vector3.Max(max, position);
    }

    return (min, max);
  }

}
