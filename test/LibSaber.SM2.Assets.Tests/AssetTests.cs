using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SpaceMarine2.Enumerations;

namespace LibSaber.SM2.Assets.Tests;

public class AssetTests
{

  [Theory]
  [InlineData("marine.pct.resource", ".pct.resource")]
  [InlineData("xscenes/a.sani.resource", ".sani.resource")]
  [InlineData("foo.pct_mip", ".pct_mip")]
  [InlineData("foo.tpl_data", ".tpl_data")]
  [InlineData("foo.lods_base", ".lods_base")]
  [InlineData("foo.bar", ".bar")]
  [InlineData("noext", "")]
  public void CompoundExtension_KeepsResourceSuffix(string name, string expected)
  {
    Assert.Equal(expected, AssetClassification.CompoundExtension(name));
  }

  [Fact]
  public void Catalog_IndexesDirectoriesWithoutFileNodes()
  {
    using var dir = new TempDirectory();
    CacheFile.Create(dir.Path, "a.pak", new[]
    {
      new CacheFile.Entry("a/d.txt", Encoding.ASCII.GetBytes("d")),
      new CacheFile.Entry("a/b/c.txt", Encoding.ASCII.GetBytes("c")),
      new CacheFile.Entry("a/b/e.txt", Encoding.ASCII.GetBytes("e")),
      new CacheFile.Entry("textures/hero.pct.resource", Encoding.ASCII.GetBytes("yaml")),
      new CacheFile.Entry("textures/hero.pct_mip", new byte[] { 1, 2, 3, 4 }),
    });

    var catalog = GameCatalog.Load(dir.Path);
    Assert.Equal(5, catalog.FileCount);
    Assert.Equal(5, catalog.Root.SubtreeFileCount);
    Assert.Equal(1, catalog.Archives.Count);
    Assert.Equal(0, catalog.Root.FileCount);

    var folderA = catalog.Root.Children.Single(child => child.Name == "a");
    Assert.Equal(1, folderA.FileCount);
    Assert.Equal(3, folderA.SubtreeFileCount);
    Assert.Equal("d.txt", catalog.GetFile(folderA.FirstFile).Name);
    var folderB = folderA.Children.Single();
    Assert.Equal(2, folderB.FileCount);
    Assert.Equal(3, catalog.Query(folderA, null, AssetFilter.All, includeSubfolders: true).Length);
    Assert.DoesNotContain(folderA.Children, child => child.Name.EndsWith(".txt"));

    var textures = catalog.Query(null, "hero", AssetFilter.Textures);
    Assert.Equal(2, textures.Length);
    Assert.Contains(Enumerable.Range(0, textures.Length).Select(row => catalog.GetFile(textures.IndexAt(row)).Extension), ext => ext == ".pct.resource");
  }

  [Fact]
  public void Catalog_ResolvesMipByFileNameAndReadsDeflate()
  {
    using var dir = new TempDirectory();
    var payload = Encoding.ASCII.GetBytes("mip-bytes");
    CacheFile.Create(dir.Path, "resources.pak", new[]
    {
      new CacheFile.Entry(@"textures\hero.pct_mip", payload, Deflate: true),
    });

    var catalog = GameCatalog.Load(dir.Path);
    var resource = catalog.FindByFileName("hero.pct_mip");
    Assert.Single(resource);
    using var store = new ArchiveStore(catalog);
    Assert.Equal(payload, store.Read(resource[0]));
  }

  [Fact]
  public void Catalog_KeepsDuplicatePathsFromLaterArchives()
  {
    using var dir = new TempDirectory();
    CacheFile.Create(dir.Path, "a.pak", new[]
    {
      new CacheFile.Entry("same/file.txt", new byte[] { 1 }),
      new CacheFile.Entry("foo/bar.txt", new byte[] { 8 }),
      new CacheFile.Entry("foo\\bar.txt", new byte[] { 9 }),
    });
    CacheFile.Create(dir.Path, "b.pak", new[] { new CacheFile.Entry("same/file.txt", new byte[] { 2 }) });
    var catalog = GameCatalog.Load(dir.Path);
    Assert.Equal(2, catalog.DuplicatesCollapsed);
    Assert.Single(catalog.FindByPath("same/file.txt"));
    Assert.Equal(2, catalog.FileCount);
    var kept = catalog.GetFile(catalog.FindByPath("same/file.txt")[0]);
    Assert.Equal("b.pak", kept.ArchiveName);
    using var store = new ArchiveStore(catalog);
    Assert.Equal(new byte[] { 2 }, store.Read(kept.Index));
    var slash = catalog.GetFile(catalog.FindByPath("foo/bar.txt")[0]);
    Assert.Equal(new byte[] { 9 }, store.Read(slash.Index));
  }

  [Fact]
  public void Catalog_IndexesThousandsOfPathsQuickly()
  {
    using var dir = new TempDirectory();
    var entries = new CacheFile.Entry[20000];
    for (var i = 0; i < entries.Length; i++)
      entries[i] = new CacheFile.Entry($"group{i % 40}/item{i}.pct_mip", new byte[] { 1 });

    CacheFile.Create(dir.Path, "big.pak", entries);
    var watch = Stopwatch.StartNew();
    var catalog = GameCatalog.Load(dir.Path);
    watch.Stop();
    Assert.Equal(20000, catalog.FileCount);
    Assert.Equal(40, catalog.Root.Children.Length);
    Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), watch.Elapsed.ToString());
    Assert.Equal(500, catalog.Root.Children[0].FileCount);
    Assert.Empty(catalog.Root.Children[0].Children);
  }

  [Fact]
  public void Bc1_DecodesOpaqueWhiteBlock()
  {
    var block = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 };
    var pixels = TextureInfo.Decode(SM2TextureFormat.OXT1, block, 4, 4);
    Assert.Equal(4 * 4 * 4, pixels.Length);
    Assert.Equal(255, pixels[0]);
    Assert.Equal(255, pixels[1]);
    Assert.Equal(255, pixels[2]);
    Assert.Equal(255, pixels[3]);
  }

  [Fact]
  public void TextureResource_ExportsDdsPngAndTgaFromExternalMip()
  {
    var yaml = """
      __type: res_desc_pct
      header:
        faceSize: 8
        format: 12
        mipLevel:
        - {offset: 0, size: 8}
        nFaces: 1
        nMipMap: 1
        sign: 0
        size: 8
        sx: 4
        sy: 4
        sz: 1
      mipMaps:
      - hero.pct_mip
      texName: hero
      texType: color
      """;
    using var dir = new TempDirectory();
    CacheFile.Create(dir.Path, "resources.pak", new[]
    {
      new CacheFile.Entry("textures/hero.pct.resource", Encoding.ASCII.GetBytes(yaml)),
      new CacheFile.Entry("textures/hero.pct_mip", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 }),
    });

    var catalog = GameCatalog.Load(dir.Path);
    Assert.True(catalog.TryGetByPath("textures/hero.pct.resource", out var index));
    using var store = new ArchiveStore(catalog);
    var document = TextureLoader.Load(catalog, store, index);
    Assert.Equal(SM2TextureFormat.OXT1, document.Format);
    Assert.Equal(8, document.Payload.Length);
    var pixels = document.Decode(0, 0, out var width, out var height, out _);
    Assert.Equal(4, width);
    Assert.Equal(4, height);
    Assert.Equal(255, pixels[2]);

    var output = Path.Combine(dir.Path, "out");
    var report = AssetExporter.Export(catalog, store, new[] { index }, output, new ExportSettings());
    Assert.Equal(0, report.Skipped);
    Assert.True(File.Exists(Path.Combine(output, "textures", "hero.dds")));
    Assert.True(File.Exists(Path.Combine(output, "textures", "hero.png")));
    Assert.True(File.Exists(Path.Combine(output, "textures", "hero.tga")));
    var dds = File.ReadAllBytes(Path.Combine(output, "textures", "hero.dds"));
    Assert.Equal((byte)'D', dds[0]);
    Assert.Equal((byte)'D', dds[1]);
    Assert.Equal((byte)'S', dds[2]);
    Assert.Contains(dds, b => b == 0xFF);
    var png = File.ReadAllBytes(Path.Combine(output, "textures", "hero.png"));
    Assert.Equal(new byte[] { 137, 80, 78, 71 }, png[..4]);
  }

  [Fact]
  public void TextureResource_UsesTheLargestMipThatIsActuallyPresent()
  {
    var yaml = """
      __type: res_desc_pct
      header:
        faceSize: 40
        format: 12
        mipLevel:
        - {offset: 0, size: 32}
        - {offset: 32, size: 8}
        nFaces: 1
        nMipMap: 2
        sign: 0
        size: 40
        sx: 8
        sy: 8
        sz: 1
      mipMaps:
      - hero_1.pct_mip
      - hero_2.pct_mip
      texName: hero
      texType: color
      """;
    using var dir = new TempDirectory();
    CacheFile.Create(dir.Path, "resources.pak", new[]
    {
      new CacheFile.Entry("textures/hero.pct.resource", Encoding.ASCII.GetBytes(yaml)),
      new CacheFile.Entry("textures/hero_2.pct_mip", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 }),
    });

    var catalog = GameCatalog.Load(dir.Path);
    Assert.True(catalog.TryGetByPath("textures/hero.pct.resource", out var index));
    using var store = new ArchiveStore(catalog);
    var document = TextureLoader.Load(catalog, store, index);
    Assert.Equal(4, document.Width);
    Assert.Equal(4, document.Height);
    Assert.Equal(1, document.MipCount);
    Assert.Equal(8, document.Payload.Length);
    var pixels = document.Decode(0, 0, out var width, out var height, out _);
    Assert.Equal(4, width);
    Assert.Equal(4, height);
    Assert.Equal(255, pixels[2]);
  }

  [Fact]
  public void ModelExport_WritesResolvedTexturesOnceAndSkipsMissingNames()
  {
    Assert.True(new ExportSettings().WriteModelTextures);
    var yaml = """
      __type: res_desc_pct
      header:
        faceSize: 8
        format: 12
        mipLevel:
        - {offset: 0, size: 8}
        nFaces: 1
        nMipMap: 1
        sign: 0
        size: 8
        sx: 4
        sy: 4
        sz: 1
      mipMaps:
      - hero.pct_mip
      texName: hero
      texType: color
      """;
    using var dir = new TempDirectory();
    CacheFile.Create(dir.Path, "resources.pak", new[]
    {
      new CacheFile.Entry("textures/hero.pct.resource", Encoding.ASCII.GetBytes(yaml)),
      new CacheFile.Entry("textures/hero.pct_mip", new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 }),
      new CacheFile.Entry("tpl/hero.tpl", new byte[] { 1 }),
    });

    var catalog = GameCatalog.Load(dir.Path);
    Assert.True(catalog.TryGetByPath("tpl/hero.tpl", out var model));
    using var store = new ArchiveStore(catalog);
    var mesh = new ExportedMesh { Name = "hero" };
    mesh.Parts.Add(new MeshPart { Name = "body", Texture = "hero" });
    mesh.Parts.Add(new MeshPart { Name = "cloth", Texture = "missing_cloth" });
    mesh.Levels.Add(new MeshLod
    {
      Name = "LOD1",
      Parts = { new MeshPart { Name = "body_lod", Texture = "textures/hero.pct.resource" } },
    });
    mesh.Material = new MaterialDocument { Name = "hero" };
    mesh.Material.Slots.Add(new MaterialSlot { Slot = "diffuse", Texture = "hero" });

    var output = Path.Combine(dir.Path, "out");
    var report = new ExportReport();
    AssetExporter.ExportModelTextures(
      catalog, store, model, mesh, output, new ExportSettings(), report, new[] { "tpl/hero.tpl" });

    Assert.Empty(report.Messages);
    Assert.Equal(3, report.Written);
    Assert.True(File.Exists(Path.Combine(output, "textures", "hero.dds")));
    Assert.True(File.Exists(Path.Combine(output, "textures", "hero.png")));
    Assert.True(File.Exists(Path.Combine(output, "textures", "hero.tga")));
    Assert.Equal(3, Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length);

    var skipped = Path.Combine(dir.Path, "skip");
    var skipReport = new ExportReport();
    AssetExporter.ExportModelTextures(
      catalog, store, model, mesh, skipped, new ExportSettings(), skipReport,
      new[] { "tpl/hero.tpl", "textures/hero.pct.resource" });
    Assert.Equal(0, skipReport.Written);
    Assert.False(Directory.Exists(skipped));
  }

  [Fact]
  public void Axis_GltfKeepsMetersAndUnrealScalesToCentimeters()
  {
    var point = AxisConvert.Point(new Vector3(0, 2, 0), ExportSpace.Unreal, gltf: true);
    Assert.True(MathF.Abs(point.X) < 0.0001f);
    Assert.True(MathF.Abs(point.Y - 2f) < 0.0001f);
    Assert.True(MathF.Abs(point.Z) < 0.0001f);
    var unreal = AxisConvert.Point(new Vector3(0, 2, 0), ExportSpace.Unreal, gltf: false);
    Assert.True(MathF.Abs(unreal.Z - 200f) < 0.0001f);
    var blender = AxisConvert.Point(new Vector3(0, 2, 0), ExportSpace.Blender, gltf: false);
    Assert.True(MathF.Abs(blender.Z - 2f) < 0.0001f);
    var front = AxisConvert.Point(new Vector3(0.2f, 1.6f, 0.5f), ExportSpace.Unreal, gltf: false);
    Assert.True(MathF.Abs(front.X - (-20f)) < 0.001f);
    Assert.True(MathF.Abs(front.Y - 50f) < 0.001f);
    Assert.True(MathF.Abs(front.Z - 160f) < 0.001f);
    var uv = AxisConvert.FbxUv(new Vector2(1.5f, -1.25f), ExportSpace.Unreal);
    Assert.True(MathF.Abs(uv.X - 1.5f) < 0.0001f);
    Assert.True(MathF.Abs(uv.Y - 2.25f) < 0.0001f);
    var kept = AxisConvert.FbxUv(new Vector2(1.5f, -1.25f), ExportSpace.Blender);
    Assert.True(MathF.Abs(kept.Y - (-1.25f)) < 0.0001f);
  }

  [Fact]
  public void Axis_BoneLocalsDoNotStackTheConversion()
  {
    var source = new[]
    {
      Matrix4x4.CreateTranslation(0, 1, 0),
      Matrix4x4.CreateTranslation(0, 0.25f, 0),
    };
    var parents = new[] { -1, 0 };
    var locals = new Matrix4x4[2];
    var worlds = new Matrix4x4[2];
    AxisConvert.ConvertHierarchy(source, parents, ExportSpace.Unreal, locals, worlds);

    Assert.Equal(0f, worlds[0].M41, 0.001f);
    Assert.Equal(0f, worlds[0].M42, 0.001f);
    Assert.Equal(100f, worlds[0].M43, 0.001f);
    Assert.Equal(125f, worlds[1].M43, 0.001f);
    Assert.Equal(0f, locals[1].M41, 0.001f);
    Assert.Equal(0f, locals[1].M42, 0.001f);
    Assert.Equal(25f, locals[1].M43, 0.001f);
    Assert.Equal(1f, locals[0].M11, 0.001f);
    Assert.Equal(1f, locals[0].M22, 0.001f);
    Assert.Equal(1f, locals[0].M33, 0.001f);
  }

  [Fact]
  public void Preview_FramesAStandingMeshAndSamplesDiffuse()
  {
    var mesh = new ExportedMesh { Name = "post" };
    mesh.Positions.Add(new Vector3(-0.15f, 0f, 0f));
    mesh.Positions.Add(new Vector3(0.15f, 0f, 0f));
    mesh.Positions.Add(new Vector3(0.15f, 2f, 0f));
    mesh.Positions.Add(new Vector3(-0.15f, 2f, 0f));
    for (var i = 0; i < 4; i++)
      mesh.Normals.Add(Vector3.UnitZ);
    mesh.Uvs.Add(new Vector2(0, 1));
    mesh.Uvs.Add(new Vector2(1, 1));
    mesh.Uvs.Add(new Vector2(1, 0));
    mesh.Uvs.Add(new Vector2(0, 0));
    mesh.Indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
    mesh.Parts.Add(new MeshPart { Name = "plate", IndexStart = 0, IndexCount = 6, Texture = "plate" });
    var albedo = new Dictionary<string, PreviewImage>
    {
      ["plate"] = new PreviewImage { Width = 1, Height = 1, Bgra = new byte[] { 20, 30, 220, 255 } },
    };

    var image = MeshPreview.Render(
      mesh, 160, 320,
      new OrbitCamera { Yaw = 0f, Pitch = 0.05f, Distance = 0f },
      false, 0, out _,
      new PreviewSettings { Albedo = albedo, Part = -1 });

    var minY = int.MaxValue;
    var maxY = int.MinValue;
    var red = 0;
    var lit = 0;
    for (var y = 0; y < 320; y++)
    {
      for (var x = 0; x < 160; x++)
      {
        var pixel = (y * 160 + x) * 4;
        if (image[pixel] == 22 && image[pixel + 1] == 24 && image[pixel + 2] == 28)
          continue;
        lit++;
        minY = Math.Min(minY, y);
        maxY = Math.Max(maxY, y);
        if (image[pixel + 2] > image[pixel] + 40 && image[pixel + 2] > image[pixel + 1] + 40)
          red++;
      }
    }

    Assert.True(lit > 400, $"standing quad should fill the frame, lit {lit}");
    Assert.True(maxY - minY > 200, $"auto-frame should use the height, span {maxY - minY}");
    Assert.True(red > 200, $"diffuse sample should read red, red pixels {red}");
  }

  [Fact]
  public void VisibleGeometry_OmitsUncheckedPartsAndKeepsACheckedSimShell()
  {
    var whole = new ExportedMesh { Name = "whole" };
    whole.Positions.Add(Vector3.Zero);
    whole.Positions.Add(Vector3.UnitX);
    whole.Positions.Add(Vector3.UnitY);
    whole.Indices.AddRange(new[] { 0, 1, 2 });
    whole.Parts.Add(new MeshPart { Name = "body", IndexStart = 0, IndexCount = 3 });
    Assert.Same(whole, whole.VisibleGeometry());

    var mesh = new ExportedMesh { Name = "split" };
    mesh.Positions.Add(new Vector3(0, 0, 0));
    mesh.Positions.Add(new Vector3(1, 0, 0));
    mesh.Positions.Add(new Vector3(0, 1, 0));
    mesh.Positions.Add(new Vector3(0, 0, 1));
    mesh.Positions.Add(new Vector3(1, 0, 1));
    mesh.Positions.Add(new Vector3(0, 1, 1));
    mesh.Indices.AddRange(new[] { 0, 1, 2, 3, 4, 5 });
    mesh.Parts.Add(new MeshPart { Name = "body", IndexStart = 0, IndexCount = 3, Visible = true });
    mesh.Parts.Add(new MeshPart { Name = "HEAD_cdt", IndexStart = 3, IndexCount = 3, Visible = false, HideReason = "volume" });
    var level = new MeshLod { Name = "LOD5" };
    level.Positions.Add(new Vector3(2, 0, 0));
    level.Positions.Add(new Vector3(3, 0, 0));
    level.Positions.Add(new Vector3(2, 1, 0));
    level.Indices.AddRange(new[] { 0, 1, 2 });
    level.Parts.Add(new MeshPart { Name = "cloth_stamp_leg_SIM", IndexStart = 0, IndexCount = 3, Visible = true });
    mesh.Levels.Add(level);

    var filtered = mesh.VisibleGeometry();
    Assert.Equal(2, filtered.Indices.Count / 3);
    Assert.Equal(9, filtered.Positions.Count);
    Assert.Equal(new[] { 0, 1, 2, 6, 7, 8 }, filtered.Indices);
    Assert.Equal("body", filtered.Parts[0].Name);
    Assert.Equal("cloth_stamp_leg_SIM", filtered.Parts[1].Name);
  }

  [Fact]
  public void Mesh_WritesGlbAndFbxAndRasterizes()
  {
    var mesh = new ExportedMesh { Name = "tri" };
    mesh.Positions.Add(new Vector3(-1, 0, -1));
    mesh.Positions.Add(new Vector3(1, 0, -1));
    mesh.Positions.Add(new Vector3(0, 0, 1));
    mesh.Normals.Add(Vector3.UnitY);
    mesh.Normals.Add(Vector3.UnitY);
    mesh.Normals.Add(Vector3.UnitY);
    mesh.Indices.Add(0);
    mesh.Indices.Add(1);
    mesh.Indices.Add(2);
    mesh.Bones.Add(new Bone { Name = "root", Local = Matrix4x4.CreateTranslation(0, 0, 0) });
    mesh.Bones.Add(new Bone { Name = "tip", Parent = 0, Local = Matrix4x4.CreateTranslation(0, 0, 1) });
    mesh.Clip = new AnimationClip { Name = "idle", Duration = 1 };
    mesh.Clip.Tracks.Add(new BoneTrack { Bone = 1, BoneName = "tip" });
    mesh.Clip.Tracks[0].Translation.Add(new VectorKey { Time = 0, Value = new Vector3(0, 0, 1) });
    mesh.Clip.Tracks[0].Translation.Add(new VectorKey { Time = 1, Value = new Vector3(0, 0, 2) });

    using var dir = new TempDirectory();
    var glb = Path.Combine(dir.Path, "tri.glb");
    var fbx = Path.Combine(dir.Path, "tri.fbx");
    GltfWriter.Write(glb, mesh, ExportSpace.Unreal);
    FbxWriter.Write(fbx, mesh, ExportSpace.Blender);
    var glbBytes = File.ReadAllBytes(glb);
    Assert.Equal(0x46546C67, BitConverter.ToInt32(glbBytes, 0));
    var jsonLength = BitConverter.ToInt32(glbBytes, 12);
    var json = Encoding.UTF8.GetString(glbBytes, 20, jsonLength).TrimEnd();
    Assert.Contains("\"meshes\"", json);
    Assert.Contains("\"animations\"", json);
    var fbxBytes = File.ReadAllBytes(fbx);
    Assert.Equal("Kaydara FBX Binary  "u8.ToArray(), fbxBytes[..20]);
    Assert.Equal(7400, BitConverter.ToInt32(fbxBytes, 23));
    Assert.True(fbxBytes.AsSpan().IndexOf("Vertices"u8) >= 0);
    Assert.True(fbxBytes.AsSpan().IndexOf("LimbNode"u8) >= 0);
    Assert.True(fbxBytes.AsSpan().IndexOf("DiffuseColor"u8) >= 0);
    Assert.True(fbxBytes.AsSpan().IndexOf("TransformLink"u8) < 0);

    var source = Matrix4x4.CreateRotationX(0.4f) * Matrix4x4.CreateRotationY(-0.3f) * Matrix4x4.CreateRotationZ(0.5f);
    var euler = FbxWriter.EulerXYZDegrees(source);
    var back = Matrix4x4.CreateRotationX(euler.X * MathF.PI / 180f)
      * Matrix4x4.CreateRotationY(euler.Y * MathF.PI / 180f)
      * Matrix4x4.CreateRotationZ(euler.Z * MathF.PI / 180f);
    Assert.Equal(source.M11, back.M11, 0.001f);
    Assert.Equal(source.M12, back.M12, 0.001f);
    Assert.Equal(source.M13, back.M13, 0.001f);
    Assert.Equal(source.M21, back.M21, 0.001f);
    Assert.Equal(source.M22, back.M22, 0.001f);
    Assert.Equal(source.M23, back.M23, 0.001f);
    Assert.Equal(source.M31, back.M31, 0.001f);
    Assert.Equal(source.M32, back.M32, 0.001f);
    Assert.Equal(source.M33, back.M33, 0.001f);

    var image = MeshPreview.Render(mesh, 128, 128, new OrbitCamera { Yaw = 0.4f, Pitch = 1.0f, Distance = 4 }, true, 0, out _);
    var lit = 0;
    for (var i = 0; i < image.Length; i += 4)
    {
      if (image[i + 2] > 40)
        lit++;
    }

    Assert.True(lit > 20, $"lit pixels {lit}");
    var sampled = AnimationClip.Sample(mesh.Clip.Tracks[0].Translation, Vector3.Zero, 0.5f);
    Assert.Equal(1.5f, sampled.Z, 3f);
  }

  [Fact]
  public void SkinnedFbx_RoundTripsThroughBlenderAndAssimp()
  {
    var mesh = new ExportedMesh { Name = "plate" };
    mesh.Positions.Add(new Vector3(-0.2f, 0, 0));
    mesh.Positions.Add(new Vector3(0.2f, 0, 0));
    mesh.Positions.Add(new Vector3(0.2f, 2, 0));
    mesh.Positions.Add(new Vector3(-0.2f, 2, 0));
    for (var i = 0; i < 4; i++)
      mesh.Normals.Add(Vector3.UnitZ);
    mesh.Uvs.Add(new Vector2(0, 0));
    mesh.Uvs.Add(new Vector2(1, 0));
    mesh.Uvs.Add(new Vector2(1, 1));
    mesh.Uvs.Add(new Vector2(0, 1));
    mesh.Indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
    mesh.Parts.Add(new MeshPart { Name = "plate", IndexStart = 0, IndexCount = 6, Texture = "plate_color" });
    var root = Matrix4x4.CreateTranslation(0, 0, 0);
    var tip = Matrix4x4.CreateRotationZ(0.4f) * Matrix4x4.CreateTranslation(0, 1, 0);
    Matrix4x4.Invert(tip, out var tipInverse);
    mesh.Bones.Add(new Bone { Name = "root", Local = root, InverseBind = Matrix4x4.Identity });
    mesh.Bones.Add(new Bone { Name = "tip", Parent = 0, Local = tip, InverseBind = tipInverse });
    mesh.Skin.Add(new SkinWeights { J0 = 0, W0 = 1 });
    mesh.Skin.Add(new SkinWeights { J0 = 0, W0 = 1 });
    mesh.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });
    mesh.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });

    using var dir = new TempDirectory();
    var fbx = Path.Combine(dir.Path, "plate.fbx");
    var glb = Path.Combine(dir.Path, "plate.glb");
    FbxWriter.Write(fbx, mesh, ExportSpace.Unreal);
    GltfWriter.Write(glb, mesh, ExportSpace.Unreal);
    var fbxBytes = File.ReadAllBytes(fbx);
    Assert.True(fbxBytes.AsSpan().IndexOf("TransformLink"u8) >= 0);
    Assert.True(fbxBytes.AsSpan().IndexOf("BindPose"u8) >= 0);
    Assert.True(fbxBytes.AsSpan().IndexOf("plate_color.png"u8) >= 0);
    Assert.True(fbxBytes.AsSpan().IndexOf("\0\u0001Geometry"u8) >= 0 || fbxBytes.AsSpan().IndexOf("Geometry"u8) >= 0);

    if (!File.Exists("/usr/bin/blender") || !File.Exists("/usr/bin/assimp"))
      return;

    var assimp = RunTool("/usr/bin/assimp", "info", fbx);
    Assert.Contains("Vertices:           4", assimp);
    Assert.Contains("Faces:              2", assimp);
    Assert.Contains("Bones:              2", assimp);
    Assert.Contains("plate_color.png", assimp);

    var script = Path.Combine(dir.Path, "check.py");
    File.WriteAllText(script, """
      import bpy, sys
      kind, path = sys.argv[sys.argv.index("--") + 1:]
      bpy.ops.wm.read_factory_settings(use_empty=True)
      if kind == "fbx":
          bpy.ops.import_scene.fbx(filepath=path)
      else:
          bpy.ops.import_scene.gltf(filepath=path)
      def display_only(obj):
          return any(col.name == "glTF_not_exported" for col in obj.users_collection)
      meshes = [o for o in bpy.data.objects if o.type == "MESH" and not display_only(o)]
      arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
      verts = sum(len(o.data.vertices) for o in meshes)
      polys = sum(len(o.data.polygons) for o in meshes)
      bones = sum(len(o.data.bones) for o in arms)
      groups = sum(len(o.vertex_groups) for o in meshes)
      uvs = len(meshes[0].data.uv_layers) if meshes else 0
      uvset = ""
      if uvs:
          layer = meshes[0].data.uv_layers[0].data
          uvset = "|".join(sorted({f"{loop.uv.x:.2f},{loop.uv.y:.2f}" for loop in layer}))
      names = ",".join(f"{o.name}:{len(o.data.vertices)}:{len(o.data.polygons)}" for o in meshes)
      print(f"RESULT verts={verts} polys={polys} bones={bones} groups={groups} uvs={uvs} uvset={uvset} meshes={len(meshes)} arms={len(arms)} detail={names}")
      """);
    var fbxImport = RunTool("/usr/bin/blender", "--background", "--python", script, "--", "fbx", fbx);
    Assert.Contains("RESULT verts=4 polys=2 bones=2", fbxImport);
    Assert.Contains("uvs=1 uvset=0.00,0.00|0.00,1.00|1.00,0.00|1.00,1.00", fbxImport);
    Assert.DoesNotContain("mapping type unsupported", fbxImport);
    var glbImport = RunTool("/usr/bin/blender", "--background", "--python", script, "--", "glb", glb);
    Assert.Contains("RESULT verts=4 polys=2 bones=2", glbImport);
    Assert.Contains("uvs=1 uvset=0.00,0.00|0.00,1.00|1.00,0.00|1.00,1.00", glbImport);

    var validator = "/tmp/gltfval/gltf_validator";
    if (!File.Exists(validator))
      return;
    RunTool(validator, glb);
    var report = File.ReadAllText(glb + ".report.json");
    Assert.Contains("\"numErrors\": 0", report);
  }

  private static string RunTool(string file, params string[] args)
  {
    var info = new ProcessStartInfo(file)
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
    };
    foreach (var arg in args)
      info.ArgumentList.Add(arg);
    var process = Process.Start(info)!;
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    Assert.True(process.ExitCode == 0, stdout + stderr);
    return stdout + stderr;
  }

  [Fact]
  public void Fbx_LinksEachPartToItsDiffuseAndNormalByRelativePath()
  {
    var mesh = new ExportedMesh { Name = "armor" };
    mesh.Positions.Add(new Vector3(0, 0, 0));
    mesh.Positions.Add(new Vector3(1, 0, 0));
    mesh.Positions.Add(new Vector3(0, 1, 0));
    mesh.Indices.AddRange(new[] { 0, 1, 2, 0, 2, 1, 0, 1, 2 });
    mesh.Parts.Add(new MeshPart { Name = "torso", IndexStart = 0, IndexCount = 3, Texture = "ch_titus_body", Normal = "ch_titus_body_nm" });
    mesh.Parts.Add(new MeshPart { Name = "head_01", IndexStart = 3, IndexCount = 3, Texture = "ch_titus_head", Normal = "ch_titus_head_nm" });
    mesh.Parts.Add(new MeshPart { Name = "head_01", IndexStart = 6, IndexCount = 3, Texture = "ch_titus_head" });
    mesh.Material = new MaterialDocument { Name = "armor" };
    mesh.Material.Slots.Add(new MaterialSlot { Slot = "torso.layer2", Texture = "ch_titus_body_spec" });

    using var dir = new TempDirectory();
    var fbx = Path.Combine(dir.Path, "armor.fbx");
    var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["ch_titus_body"] = "../../pct/ch_titus_body.png",
      ["ch_titus_body_nm"] = "../../pct/ch_titus_body_nm.png",
      ["ch_titus_head"] = "../../pct/ch_titus_head.png",
      ["ch_titus_head_nm"] = "../../pct/ch_titus_head_nm.png",
    };
    FbxWriter.Write(fbx, mesh, ExportSpace.Unreal, files);
    var bytes = File.ReadAllBytes(fbx);
    Assert.Contains("../../pct/ch_titus_body.png", Encoding.ASCII.GetString(bytes));
    Assert.Contains("../../pct/ch_titus_body_nm.png", Encoding.ASCII.GetString(bytes));
    Assert.Contains("NormalMap", Encoding.ASCII.GetString(bytes));
    Assert.Contains("head_01_2", Encoding.ASCII.GetString(bytes));
    Assert.DoesNotContain("ch_titus_body_spec", Encoding.ASCII.GetString(bytes));
  }

  [Fact]
  public void UnrealFbx_OmitsHiddenShellsFromTheSingleMesh()
  {
    var mesh = new ExportedMesh { Name = "hero" };
    mesh.Positions.Add(new Vector3(0, 1, 0.4f));
    mesh.Positions.Add(new Vector3(0.1f, 1, 0.4f));
    mesh.Positions.Add(new Vector3(0, 1.1f, 0.4f));
    mesh.Normals.Add(Vector3.UnitZ);
    mesh.Normals.Add(Vector3.UnitZ);
    mesh.Normals.Add(Vector3.UnitZ);
    mesh.Uvs.Add(new Vector2(0.25f, -1.5f));
    mesh.Uvs.Add(new Vector2(0.75f, -1.5f));
    mesh.Uvs.Add(new Vector2(0.25f, -1.1f));
    mesh.Indices.AddRange(new[] { 0, 1, 2 });
    mesh.Parts.Add(new MeshPart { Name = "body", IndexStart = 0, IndexCount = 3, Visible = true, Texture = "body" });
    var level = new MeshLod { Name = "LOD1" };
    level.Positions.Add(Vector3.Zero);
    level.Positions.Add(Vector3.UnitX);
    level.Positions.Add(Vector3.UnitY);
    level.Normals.Add(Vector3.UnitZ);
    level.Normals.Add(Vector3.UnitZ);
    level.Normals.Add(Vector3.UnitZ);
    level.Uvs.Add(Vector2.Zero);
    level.Uvs.Add(Vector2.UnitX);
    level.Uvs.Add(Vector2.UnitY);
    level.Indices.AddRange(new[] { 0, 1, 2 });
    level.Parts.Add(new MeshPart { Name = "shell_cdt", IndexStart = 0, IndexCount = 3, Visible = false, HideReason = "volume" });
    mesh.Levels.Add(level);

    using var dir = new TempDirectory();
    var fbx = Path.Combine(dir.Path, "hero.fbx");
    FbxWriter.Write(fbx, mesh, ExportSpace.Unreal);
    var text = Encoding.ASCII.GetString(File.ReadAllBytes(fbx));
    Assert.Contains("body", text);
    Assert.DoesNotContain("shell_cdt", text);
    Assert.Equal(1, Count(text, "\0\u0001Geometry"));
    Assert.Equal(1, Count(text, "\0\u0001Model"));
  }

  private static int Count(string text, string token)
  {
    var count = 0;
    var index = 0;
    while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
    {
      count++;
      index += token.Length;
    }
    return count;
  }

  [Fact]
  public void SelectExportGeometry_Lod0KeepsTheCheckedSetAndLod1TakesTheNextPool()
  {
    var mesh = new ExportedMesh { Name = "split" };
    mesh.Positions.Add(Vector3.Zero);
    mesh.Positions.Add(Vector3.UnitX);
    mesh.Positions.Add(Vector3.UnitY);
    mesh.Indices.AddRange(new[] { 0, 1, 2 });
    mesh.Parts.Add(new MeshPart { Name = "body", IndexStart = 0, IndexCount = 3, Texture = "body", Visible = true });
    var level = new MeshLod { Name = "LOD1" };
    level.Positions.Add(new Vector3(2, 0, 0));
    level.Positions.Add(new Vector3(3, 0, 0));
    level.Positions.Add(new Vector3(2, 1, 0));
    level.Indices.AddRange(new[] { 0, 1, 2 });
    level.Parts.Add(new MeshPart { Name = "body_LOD1", IndexStart = 0, IndexCount = 3, Texture = "body", Visible = false, HideReason = "LOD" });
    mesh.Levels.Add(level);

    var visible = AssetExporter.SelectExportGeometry(mesh, 0);
    Assert.Equal("body", Assert.Single(visible.Parts).Name);
    var lod1 = AssetExporter.SelectExportGeometry(mesh, 1);
    Assert.Equal("body_LOD1", Assert.Single(lod1.Parts).Name);
    Assert.True(lod1.Parts[0].Visible);
    Assert.Equal(3, lod1.Positions.Count);

    var model = Path.Combine(Path.GetTempPath(), "tpl", "hero.tpl", "hero.fbx");
    var texture = Path.Combine(Path.GetTempPath(), "pct", "body.png");
    Assert.Equal("../../pct/body.png", AssetExporter.RelativeTexturePath(model, texture));
    Assert.True(TemplateMeshReader.IsNormalMapName("ch_titus_body_nm"));
    Assert.False(TemplateMeshReader.IsNormalMapName("ch_titus_body"));
  }

  [Fact]
  public void SaniIndex_CountsSharedBoneNames()
  {
    var skeleton = new[] { "CENTRE", "HEAD", "ARM1L", "ARM1R", "LEG1L", "LEG1R", "TAIL1", "TAIL6" };
    var clip = new[] { "CENTRE", "HEAD", "ARM1L", "other" };
    Assert.Equal(3, SaniIndex.SharedBones(skeleton, clip));
    Assert.Empty(SaniIndex.Compatible(new[] { new SaniIndex.Header(0, "a.sani", 1, 30, clip) }, skeleton));
    Assert.Single(SaniIndex.Compatible(new[] { new SaniIndex.Header(0, "a.sani", 1, 30, skeleton) }, skeleton, minimum: 8));
    Assert.Single(SaniIndex.Match(new[] { new SaniIndex.Header(0, "animation/idle_marine.sani", 1, 30, clip) }, "idle_marine"));
  }

  [Fact]
  public void EffectScan_FindsTextureNamesAndRefusesParentPaths()
  {
    var bytes = Encoding.ASCII.GetBytes("textures/body.pct.resource\0models/marine.tpl");
    var names = AssetExporter.ScanReferences(bytes);
    Assert.Contains("textures/body.pct.resource", names);
    Assert.Contains("models/marine.tpl", names);
    Assert.Throws<InvalidDataException>(() => OutputPaths.Map(Path.GetTempPath(), "../outside.txt", ".dds"));
  }

  [Fact]
  public void OutputPaths_KeepFileName_SeparatesSiblingExports()
  {
    var root = Path.Combine(Path.GetTempPath(), "libsaber-out-" + Guid.NewGuid().ToString("N"));
    try
    {
      var sani = OutputPaths.Map(root, "animation/clip.sani", ".anim.json", keepFileName: true);
      var resource = OutputPaths.Map(root, "animation/clip.sani.resource", ".anim.json", keepFileName: true);
      var data = OutputPaths.Map(root, "animation/clip.sani_data", ".anim.json", keepFileName: true);
      Assert.NotEqual(sani, resource);
      Assert.NotEqual(sani, data);
      Assert.EndsWith(Path.Combine("animation", "clip.sani.anim.json"), sani);
      Assert.EndsWith(Path.Combine("animation", "clip.sani.resource.anim.json"), resource);
      Assert.EndsWith(Path.Combine("animation", "clip.sani_data.anim.json"), data);
    }
    finally
    {
      if (Directory.Exists(root))
        Directory.Delete(root, true);
    }
  }

  [Fact]
  public void Export_SiblingAnimationsDoNotOverwrite()
  {
    using var dir = new TempDirectory();
    CacheFile.Create(dir.Path, "clips.pak", new[]
    {
      new CacheFile.Entry("animation/clip.sani", Encoding.ASCII.GetBytes("not a clip")),
      new CacheFile.Entry("animation/clip.sani_data", Encoding.ASCII.GetBytes("payload")),
    });
    var catalog = GameCatalog.Load(dir.Path);
    using var store = new ArchiveStore(catalog);
    var output = Path.Combine(dir.Path, "out");
    var indices = Enumerable.Range(0, catalog.FileCount).ToArray();
    AssetExporter.Export(catalog, store, indices, output, new ExportSettings());
    var sani = File.ReadAllText(Path.Combine(output, "animation", "clip.sani.anim.json"));
    var data = File.ReadAllText(Path.Combine(output, "animation", "clip.sani_data.anim.json"));
    Assert.DoesNotContain("external animation payload", sani);
    Assert.Contains("external animation payload", data);
  }

  [Fact]
  public void LooseMip_DoesNotPretendToBeATexture()
  {
    using var dir = new TempDirectory();
    CacheFile.Create(dir.Path, "resources.pak", new[]
    {
      new CacheFile.Entry("textures/hero.pct_mip", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
    });
    var catalog = GameCatalog.Load(dir.Path);
    using var store = new ArchiveStore(catalog);
    var document = TextureLoader.Load(catalog, store, 0);
    Assert.Contains(".pct.resource", document.Message);
    Assert.Equal(0, document.Width);
  }

  [Fact]
  public void MaterialMaps_PacksChannelsFromTheTextureDefinition()
  {
    var armor = MaterialMaps.Pack("Metalness(r)+Roughness(g)+AO(b)");
    Assert.Equal(new[] { "r", "g", "b" }, armor.Select(pack => pack.Channel).ToArray());
    Assert.Equal(new[] { "metalness", "roughness", "ao" }, armor.Select(pack => pack.Meaning).ToArray());
    Assert.False(MaterialMaps.IsSrgb(armor));
    var skin = MaterialMaps.Pack("MetGray(r)+Roughness(a)");
    Assert.Equal("a", skin[1].Channel);
    Assert.Equal("roughness", skin[1].Meaning);
    var diffuse = MaterialMaps.Pack("Diff(rgb)+AKill(a)");
    Assert.True(MaterialMaps.IsSrgb(diffuse));
    Assert.Equal("alphaTest", diffuse[1].Meaning);
    Assert.False(MaterialMaps.IsSrgb(MaterialMaps.Pack("Normalmap(rgb)")));
    Assert.False(MaterialMaps.IsSrgb(MaterialMaps.Pack("AO(a)")));
    Assert.True(MaterialMaps.IsSrgb(MaterialMaps.Pack("RGBEm(rgb)+AO(a)")));
  }

  [Fact]
  public void MaterialMaps_ReadsTheDefaultVariantFromAnotherArchive()
  {
    const string definition = """
      convert_settings = {
         format_descr = "Diff(rgb)"
         format_name = "MD"
      }
      materials = {
         default = {
            shaders = {
               glt = {
                  covering = {
                     coveringMask = "ch_seal_hdetm"
                  }
               }
            }
         }
         damaged = {
            shaders = {
               glt = {
                  tex = {
                     NM = "ch_seal_damaged_nm"
                     spec = "ch_seal_damaged_spec"
                     diffOverride = "ch_seal_damaged"
                  }
               }
            }
         }
      }
      """;
    using var dir = new TempDirectory();
    CacheFile.Create(dir.Path, "model.pak", new[]
    {
      new CacheFile.Entry("tpl/hero.tpl", new byte[] { 1 }),
    });
    CacheFile.Create(dir.Path, "shared.pak", new[]
    {
      new CacheFile.Entry("pct/ch_seal.pct.resource", Encoding.ASCII.GetBytes("diffuse")),
      new CacheFile.Entry("pct/ch_seal_nm.pct.resource", Encoding.ASCII.GetBytes("normal")),
      new CacheFile.Entry("pct/ch_seal_spec.pct.resource", Encoding.ASCII.GetBytes("spec")),
      new CacheFile.Entry("td/ch_seal.td", Encoding.UTF8.GetBytes(definition)),
      new CacheFile.Entry("td/ch_seal_nm.td", Encoding.UTF8.GetBytes("format_descr = \"Normalmap(rgb)\"\n")),
      new CacheFile.Entry("td/ch_seal_spec.td", Encoding.UTF8.GetBytes("format_descr = \"Metalness(r)+Roughness(g)+AO(b)\"\n")),
      new CacheFile.Entry("td/ch_seal_hdetm.td", Encoding.UTF8.GetBytes("format_descr = \"DetMask(g)\"\n")),
      new CacheFile.Entry("pct/ch_seal_ao.pct.resource", Encoding.ASCII.GetBytes("not-ao")),
      new CacheFile.Entry("td/ch_seal_ao.td", Encoding.UTF8.GetBytes("format_descr = \"Diff(rgb)\"\n")),
      new CacheFile.Entry("pct/ch_seal_em.pct.resource", Encoding.ASCII.GetBytes("ao")),
      new CacheFile.Entry("td/ch_seal_em.td", Encoding.UTF8.GetBytes("format_descr = \"AO(a)\"\n")),
    });
    var catalog = GameCatalog.Load(dir.Path);
    using var store = new ArchiveStore(catalog);
    Assert.True(catalog.TryGetByPath("tpl/hero.tpl", out var hint));
    var mesh = new ExportedMesh { Name = "hero" };
    mesh.Parts.Add(new MeshPart { Name = "seal", Texture = "res://pct/ch_seal.pct.resource", Variant = "default" });
    mesh.Parts.Add(new MeshPart { Name = "grey", Texture = "sc_grey_90", Variant = "default" });
    mesh.Parts.Add(new MeshPart { Name = "gone", Texture = "ch_purity_seals_01", Variant = "default" });
    MaterialMaps.Attach(catalog, store, hint, mesh);

    var seal = mesh.Parts[0];
    Assert.Equal("ch_seal", seal.Texture);
    Assert.Equal("ch_seal_nm", seal.Normal);
    Assert.Equal("ch_seal_spec", seal.Specular);
    Assert.Contains("Metalness(r)", seal.SpecularChannels);
    Assert.DoesNotContain(seal.Maps, map => map.Name.Contains("damaged", StringComparison.Ordinal));
    var mask = seal.Maps.Single(map => map.Role == "detailMask");
    Assert.True(mask.Missing);
    Assert.Equal("DetMask(g)", mask.Channels);
    Assert.False(seal.Maps.Single(map => map.Role == "normal").Srgb);
    Assert.True(seal.Maps.Single(map => map.Role == "diffuse").Srgb);
    Assert.DoesNotContain(seal.Maps, map => map.Name == "ch_seal_ao");
    var ao = seal.Maps.Single(map => map.Role == "ao");
    Assert.Equal("ch_seal_em", ao.Name);
    Assert.Equal("AO(a)", ao.Channels);
    Assert.False(ao.Srgb);

    var grey = mesh.Parts[1].Maps.Single(map => map.Role == "diffuse");
    Assert.True(grey.Generated);
    Assert.Equal(230, grey.SolidR);
    Assert.True(mesh.Parts[2].Maps.Single(map => map.Role == "diffuse").Missing);
  }

  [Fact]
  public void Fbx_LinksSpecularAndWritesTheUnrealSidecar()
  {
    var mesh = new ExportedMesh { Name = "armor" };
    mesh.Positions.Add(new Vector3(0, 0, 0));
    mesh.Positions.Add(new Vector3(1, 0, 0));
    mesh.Positions.Add(new Vector3(0, 1, 0));
    mesh.Indices.AddRange(new[] { 0, 1, 2 });
    var part = new MeshPart
    {
      Name = "torso",
      IndexStart = 0,
      IndexCount = 3,
      Texture = "ch_titus_body",
      Normal = "ch_titus_body_nm",
      Specular = "ch_titus_body_spec",
      SpecularChannels = "Metalness(r)+Roughness(g)+AO(b)",
      Variant = "default",
      Blend = "opaque",
    };
    part.Maps.Add(new MaterialMap { Role = "diffuse", Name = "ch_titus_body", Channels = "Diff(rgb)", Srgb = true });
    part.Maps.Add(new MaterialMap { Role = "normal", Name = "ch_titus_body_nm", Channels = "Normalmap(rgb)", Srgb = false });
    var spec = new MaterialMap { Role = "specular", Name = "ch_titus_body_spec", Channels = part.SpecularChannels, Srgb = false };
    spec.Packing.Add(new ChannelPack("r", "metalness"));
    spec.Packing.Add(new ChannelPack("g", "roughness"));
    spec.Packing.Add(new ChannelPack("b", "ao"));
    part.Maps.Add(spec);
    mesh.Parts.Add(part);

    using var dir = new TempDirectory();
    var fbx = Path.Combine(dir.Path, "armor.fbx");
    var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["ch_titus_body"] = "../../pct/ch_titus_body.png",
      ["ch_titus_body_nm"] = "../../pct/ch_titus_body_nm.png",
      ["ch_titus_body_spec"] = "../../pct/ch_titus_body_spec.png",
    };
    FbxWriter.Write(fbx, mesh, ExportSpace.Unreal, files);
    var text = Encoding.ASCII.GetString(File.ReadAllBytes(fbx));
    Assert.Contains("SpecularColor", text);
    Assert.Contains("../../pct/ch_titus_body_spec.png", text);
    Assert.Contains("NormalMap", text);
  }

  [Fact]
  public void AnimFbx_IsSkeletonOnlyAndTheCurvesMove()
  {
    var mesh = new ExportedMesh { Name = "arm" };
    mesh.Bones.Add(new Bone { Name = "root", Parent = -1 });
    mesh.Bones.Add(new Bone { Name = "ARM1R", Parent = 0 });
    mesh.Bones.Add(new Bone { Name = "marker", Parent = -1 }); // two roots: the writer keeps the armature Null
    mesh.Positions.Add(Vector3.Zero);
    mesh.Positions.Add(Vector3.UnitX);
    mesh.Positions.Add(Vector3.UnitY);
    mesh.Indices.AddRange(new[] { 0, 1, 2 });
    mesh.Parts.Add(new MeshPart { Name = "tri", IndexStart = 0, IndexCount = 3 });
    var clip = new AnimationClip { Name = "swing", Duration = 2f, SampleRate = 30f };
    var track = new BoneTrack { BoneName = "ARM1R" };
    track.Rotation.Add(new QuatKey { Time = 0f, Value = Quaternion.Identity });
    track.Rotation.Add(new QuatKey { Time = 2f, Value = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f) });
    clip.Tracks.Add(track);
    mesh.Clip = clip;

    using var dir = new TempDirectory();
    var path = Path.Combine(dir.Path, "swing.fbx");
    FbxWriter.Write(path, mesh, ExportSpace.Unreal, animationOnly: true);
    var file = File.ReadAllBytes(path);
    var roots = FbxNodes(file);
    var objects = roots.Single(node => node.Name == "Objects");
    Assert.DoesNotContain(objects.Children, node => node.Name is "Geometry" or "Deformer" or "Material");
    Assert.DoesNotContain(objects.Children, node => node.Name == "Model" && node.Props.Count > 2 && node.Props[2] as string == "Mesh");

    var nullAttribute = objects.Children.Single(node => node.Name == "NodeAttribute" && node.Props.Count > 2 && node.Props[2] as string == "Null");
    Assert.Equal("Null", nullAttribute.Children.Single(node => node.Name == "TypeFlags").Props[0]);
    var limb = objects.Children.First(node => node.Name == "NodeAttribute" && node.Props.Count > 2 && node.Props[2] as string == "LimbNode");
    Assert.Equal("Skeleton", limb.Children.Single(node => node.Name == "TypeFlags").Props[0]);

    var layer = objects.Children.Single(node => node.Name == "AnimationLayer");
    Assert.True(layer.Sentinel, "AnimationLayer needs the null terminator the FBX SDK reader requires");
    Assert.Contains(layer.Children, node => node.Name == "Properties70");

    var curveNode = objects.Children.First(node => node.Name == "AnimationCurveNode");
    var props = curveNode.Children.Single(node => node.Name == "Properties70");
    Assert.Contains(props.Children, node => node.Name == "P" && node.Props.Count > 0 && node.Props[0] as string == "d");

    var moved = false;
    foreach (var curve in objects.Children.Where(node => node.Name == "AnimationCurve"))
    {
      var values = (float[])curve.Children.Single(node => node.Name == "KeyValueFloat").Props[0];
      if (values.Max() - values.Min() > 20f)
        moved = true;
      var flags = (int[])curve.Children.Single(node => node.Name == "KeyAttrFlags").Props[0];
      Assert.Equal((1 << 2) | (1 << 8) | (1 << 13) | (1 << 14), flags[0]);
    }

    Assert.True(moved, "a rotation curve should change by more than 20 degrees");

    var settings = roots.Single(node => node.Name == "GlobalSettings");
    var timeStop = settings.Children.Single(node => node.Name == "Properties70").Children
      .Single(node => node.Name == "P" && node.Props[0] as string == "TimeSpanStop");
    Assert.Equal(2L * 46186158000L, (long)timeStop.Props[^1]);

    var takes = roots.Single(node => node.Name == "Takes");
    var take = takes.Children.Single(node => node.Name == "Take");
    var local = take.Children.Single(node => node.Name == "LocalTime");
    Assert.Equal(0L, (long)local.Props[0]);
    Assert.Equal(2L * 46186158000L, (long)local.Props[1]);
  }

  private sealed class FbxNode
  {
    public string Name { get; init; } = "";
    public List<object?> Props { get; } = new();
    public List<FbxNode> Children { get; } = new();
    public bool Sentinel { get; set; }
  }

  private static List<FbxNode> FbxNodes(byte[] file)
  {
    var offset = 27;
    var roots = new List<FbxNode>();
    while (offset + 13 < file.Length)
    {
      var node = ReadFbx(file, ref offset);
      if (node is null)
        break;
      roots.Add(node);
    }

    return roots;
  }

  private static FbxNode? ReadFbx(byte[] file, ref int offset)
  {
    var end = BitConverter.ToInt32(file, offset);
    if (end == 0)
    {
      offset += 13;
      return null;
    }

    var count = BitConverter.ToInt32(file, offset + 4);
    var nameLength = file[offset + 12];
    var name = Encoding.UTF8.GetString(file, offset + 13, nameLength);
    var cursor = offset + 13 + nameLength;
    var node = new FbxNode { Name = name };
    for (var i = 0; i < count; i++)
      node.Props.Add(ReadFbxProp(file, ref cursor));
    if (cursor < end)
    {
      while (cursor < end - 13)
      {
        var child = ReadFbx(file, ref cursor);
        if (child is null)
          break;
        node.Children.Add(child);
      }

      node.Sentinel = end - cursor == 13 && file.AsSpan(cursor, 13).SequenceEqual(new byte[13]);
    }

    offset = end;
    return node;
  }

  private static object? ReadFbxProp(byte[] file, ref int cursor)
  {
    var type = (char)file[cursor++];
    switch (type)
    {
      case 'Y':
        var y = BitConverter.ToInt16(file, cursor);
        cursor += 2;
        return y;
      case 'C':
        return file[cursor++];
      case 'I':
        var i = BitConverter.ToInt32(file, cursor);
        cursor += 4;
        return i;
      case 'F':
        var f = BitConverter.ToSingle(file, cursor);
        cursor += 4;
        return f;
      case 'D':
        var d = BitConverter.ToDouble(file, cursor);
        cursor += 8;
        return d;
      case 'L':
        var l = BitConverter.ToInt64(file, cursor);
        cursor += 8;
        return l;
      case 'S' or 'R':
        var n = BitConverter.ToInt32(file, cursor);
        cursor += 4;
        var bytes = file.AsSpan(cursor, n);
        cursor += n;
        if (type == 'R')
          return bytes.ToArray();
        var zero = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(zero >= 0 ? bytes[..zero] : bytes);
      case 'i' or 'f' or 'd' or 'l':
        var elements = BitConverter.ToInt32(file, cursor);
        var encoding = BitConverter.ToInt32(file, cursor + 4);
        var compressed = BitConverter.ToInt32(file, cursor + 8);
        cursor += 12;
        var raw = file.AsSpan(cursor, compressed).ToArray();
        cursor += compressed;
        if (encoding == 1)
        {
          using var input = new MemoryStream(raw);
          using var zlib = new ZLibStream(input, CompressionMode.Decompress);
          using var output = new MemoryStream();
          zlib.CopyTo(output);
          raw = output.ToArray();
        }

        if (type == 'f')
        {
          var values = new float[elements];
          Buffer.BlockCopy(raw, 0, values, 0, elements * 4);
          return values;
        }

        if (type == 'i')
        {
          var values = new int[elements];
          Buffer.BlockCopy(raw, 0, values, 0, elements * 4);
          return values;
        }

        return raw;
      default:
        throw new InvalidDataException($"FBX property {type}");
    }
  }

}
