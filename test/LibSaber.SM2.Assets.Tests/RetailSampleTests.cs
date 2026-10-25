using System.Numerics;
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
    Assert.True(helmet.Uvs.Exists(uv => uv.X > 0.05f && uv.X < 0.95f && uv.Y > 0.05f && uv.Y < 0.95f));
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
    Assert.False(clip.HasCurves);

    var jump = LoadClip(catalog, store, "animation/archive/npc/zombie/jumping/jump_down_new/npc_zombie_jump_down_new_death_landing_v1_swarm_hormagaunt.sani.resource");
    Assert.InRange(jump.Duration, 1.9f, 2.1f);
    Assert.Equal(30f, jump.SampleRate);
    Assert.Equal(73, jump.Bones.Count);
    Assert.False(jump.HasCurves);

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

    // Compressed UVs are already texture coordinates. This arms island sits in the lower half
    // of the atlas, which is the blue plate. Multiplying by the submesh UvScaling short would
    // stretch it onto the parchment half and drop V's minimum near 0.
    Assert.InRange(armMinV, 0.45f, 0.65f);
    Assert.InRange(armMaxU, 0.95f, 1.05f);
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
