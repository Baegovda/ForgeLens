using System.Diagnostics;
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
    Assert.Equal(1, catalog.Archives.Count);
    Assert.Equal(0, catalog.Root.FileCount);

    var folderA = catalog.Root.Children.Single(child => child.Name == "a");
    Assert.Equal(1, folderA.FileCount);
    Assert.Equal("d.txt", catalog.GetFile(folderA.FirstFile).Name);
    var folderB = folderA.Children.Single();
    Assert.Equal(2, folderB.FileCount);
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
    CacheFile.Create(dir.Path, "a.pak", new[] { new CacheFile.Entry("same/file.txt", new byte[] { 1 }) });
    CacheFile.Create(dir.Path, "b.pak", new[] { new CacheFile.Entry("same/file.txt", new byte[] { 2 }) });
    var catalog = GameCatalog.Load(dir.Path);
    Assert.Equal(2, catalog.FindByPath("same/file.txt").Count);
    Assert.Equal(2, catalog.FileCount);
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
  public void Axis_GltfTurnsZUpCentimetersIntoYUpMeters()
  {
    var point = AxisConvert.Point(new Vector3(0, 2, 0), ExportSpace.Unreal, gltf: true);
    Assert.True(MathF.Abs(point.X) < 0.0001f);
    Assert.True(MathF.Abs(point.Y) < 0.0001f);
    Assert.True(MathF.Abs(point.Z + 0.02f) < 0.0001f);
    var unreal = AxisConvert.Point(new Vector3(0, 2, 0), ExportSpace.Unreal, gltf: false);
    Assert.True(MathF.Abs(unreal.Y - 2f) < 0.0001f);
    var blender = AxisConvert.Point(new Vector3(0, 2, 0), ExportSpace.Blender, gltf: false);
    Assert.True(MathF.Abs(blender.Y - 0.02f) < 0.0001f);
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
    var fbxText = File.ReadAllText(fbx);
    Assert.Contains("Vertices:", fbxText);
    Assert.Contains("LimbNode", fbxText);

    var image = MeshPreview.Render(mesh, 128, 128, new OrbitCamera { Yaw = 0, Pitch = 0.3f, Distance = 4 }, true, 0, out _);
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
  public void EffectScan_FindsTextureNamesAndRefusesParentPaths()
  {
    var bytes = Encoding.ASCII.GetBytes("textures/body.pct.resource\0models/marine.tpl");
    var names = AssetExporter.ScanReferences(bytes);
    Assert.Contains("textures/body.pct.resource", names);
    Assert.Contains("models/marine.tpl", names);
    Assert.Throws<InvalidDataException>(() => OutputPaths.Map(Path.GetTempPath(), "../outside.txt", ".dds"));
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

}
