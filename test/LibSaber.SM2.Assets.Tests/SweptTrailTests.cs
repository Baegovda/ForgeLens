using System.Numerics;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-53: weapon swing trails (sfx_trail_triangle) — data chain, windows and the ribbon.</summary>
public class SweptTrailTests
{
  // trail_melee_01_yellow (trimmed retail): colour (t,A,R,G,B) fades to black by 0.68, additive, segments live 0.25 s.
  private const string Yellow = """
    isAutoStart   =   False
    timeLife   =   1
    visuals   =   {
       isDistortion   =   False
       color   =   [
          0, 207, 252, 246, 163,
          0.1928965, 44, 55, 55, 55,
          0.6832885, 0, 0, 0, 0
       ]
       colorIntensityMultiplier   =   1
       colorIntensity   =   {
          constExpr   =   {
             x   =   4
             y   =   4
          }
          __type   =   "local__constExpr"
       }
       blendType   =   "add"
       texture   =   {
          nameTex   =   "gradient_02"
       }
    }
    trailParams   =   {
       maxSegLength   =   0.1
       angleMin   =   1
       timeLifeSeg   =   0.25
       typeParams   =   {
          billboard   =   {
             segSubdivs   =   8
          }
          __type   =   "local__triangle"
       }
    }
    __type   =   "sfx_trail_triangle"
    """;

  private const string Blueprint = """
    nodes   =   {
       hit   =   {
          __type   =   "BlueprintNodeEquipmentUserMorphemeEvent"
          mrEvent   =   {
             name   =   "hit1_start_anim"
          }
          OnEmitEvent   =   [
             "start/Input"
          ]
       }
       start   =   {
          __type   =   "BlueprintSfxLibraryStartsfxtrailtriangle"
          nameSFX   =   {
             def   =   "trail_melee_01_yellow"
          }
          locator1   =   {
             def   =   "sfx_trail"
          }
          locator2   =   {
             def   =   "sfx_trail1"
          }
          locator2Offset   =   {
             def   =   {
                y   =   0.25
             }
          }
       }
       stopEv   =   {
          __type   =   "BlueprintNodeEquipmentUserMorphemeEvent"
          mrEvent   =   {
             name   =   "mele_1hit_trail_stop_effect"
          }
          OnEmitEvent   =   [
             "stop/Input"
          ]
       }
       stop   =   {
          __type   =   "BlueprintNodeStopPackOfVfx"
          vfxList   =   [
             "trail_melee_01_yellow"
          ]
       }
    }
    """;

  private static EffectLibrary Library() => new(new MemoryEffectSource(new Dictionary<string, string> { ["trail_melee_01_yellow"] = Yellow }));

  [Fact]
  public void ParsesTriangleTrail()
  {
    var def = EffectDefinition.Build("trail_melee_01_yellow", Library().Resolve("trail_melee_01_yellow")!);
    var trail = Assert.Single(def.SweptTrails);
    Assert.Empty(def.Trails);
    Assert.Equal(TrailShape.Triangle, trail.Shape);
    Assert.Equal(0.25f, trail.TimeLifeSeg, 0.0001f);
    Assert.Equal(1f, trail.TimeLife, 0.0001f);
    Assert.Equal(EffectBlend.Add, trail.Render.Blend);
    Assert.Contains("gradient_02", def.Textures);
  }

  [Fact]
  public void ClassSfxListOverridesTrail()
  {
    // wpn_melee_chainsword.cls sfx.list: trail_mele_01_yellow = trail_melee_01_yellow with timeLifeSeg 0.15, intensity 5 x 0.4.
    var entry = SslText.Parse("""
      visuals   =   {
         colorIntensityMultiplier   =   0.4
         colorIntensity   =   {
            constExpr   =   {
               x   =   5
               y   =   5
            }
            __type   =   "local__constExpr"
         }
      }
      trailParams   =   {
         timeLifeSeg   =   0.15
      }
      __type   =   "trail_melee_01_yellow"
      """);
    var def = EffectDefinition.Build("trail_melee_01_yellow", Library().ResolveNode(entry));
    var trail = Assert.Single(def.SweptTrails);
    Assert.Equal(0.15f, trail.TimeLifeSeg, 0.0001f);
    Assert.Equal(2f, trail.Render.ColorIntensity.Sample(0f, 0.5f), 0.01f);
    Assert.Equal(1f, trail.TimeLife, 0.0001f); // inherited
  }

  [Fact]
  public void MapsTriangleStarterEdge()
  {
    var map = new AnimEventVfxMap();
    map.AddBlueprint("bp", Blueprint);
    var hit = Assert.Single(map.For("hit1_start_anim"));
    Assert.Equal("trail_melee_01_yellow", hit.Effect);
    var edge = Assert.IsType<TrailEdge>(hit.Edge);
    Assert.Equal("sfx_trail", edge.Locator1);
    Assert.Equal("sfx_trail1", edge.Locator2);
    Assert.Equal(Vector3.Zero, edge.Offset1);
    Assert.Equal(new Vector3(0, 0.25f, 0), edge.Offset2);
  }

  [Fact]
  public void EventWindowGatesRibbon()
  {
    var map = new AnimEventVfxMap();
    map.AddBlueprint("bp", Blueprint);
    var clip = new AnimationClip { Duration = 1f }; // event times are clip fractions
    clip.EventTracks.Add(new AnimEventTrack("ev", [new AnimEvent(0.2f, 0f, 0, ["hit1_start_anim"]), new AnimEvent(0.5f, 0f, 0, ["mele_1hit_trail_stop_effect"])]));
    var lib = Library();
    var host = new EffectHost(["root", "sfx_trail", "sfx_trail1"]);
    host.SetPose([Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.CreateTranslation(0, 1, 0)]);
    var events = new AnimEventEffects(clip, map, n => lib.Resolve(n) is { } t ? EffectDefinition.Build(n, t) : null, host);
    var cue = Assert.Single(events.Swept);
    Assert.Equal(0.2f, cue.Start, 0.001f);
    Assert.Equal(0.5f, cue.End, 0.001f);
    Assert.Empty(events.SweptAt(0.1f));
    Assert.Single(events.SweptAt(0.3f));
    Assert.Single(events.SweptAt(0.7f)); // tail still fading (0.25 s)
    Assert.Empty(events.SweptAt(0.8f));

    // The blade swings along +x at 1 m/s: the ribbon spans the last 0.25 s.
    (Vector3, Vector3)? Edge(float s) => (new Vector3(s, 0, 0), new Vector3(s, 1.25f, 0));
    var batch = SweptTrails.Build(cue, 0.4f, Edge, new Vector3(0, 0, 10))!;
    Assert.Equal(VfxDrawKind.Trail, batch.Kind);
    var times = SweptTrails.SampleTimes(cue, 0.4f);
    Assert.Equal(0.4f, times[0], 0.0001f);
    Assert.Equal(0.2f, times[^1], 0.0001f); // clipped at the window start
    Assert.Equal(times.Count - 1, batch.QuadCount);
    Assert.Equal(batch.QuadCount * 4 * SpriteBatch.Stride, batch.Vertices.Length);
    // Head is bright yellow, the tail is black (authored gradient = age fade).
    var verts = Enumerable.Range(0, batch.Vertices.Length / SpriteBatch.Stride).Select(i => batch.Vertices.AsSpan(i * SpriteBatch.Stride, SpriteBatch.Stride).ToArray()).ToList();
    var head = verts.MaxBy(v => v[0])!;
    var tail = verts.MinBy(v => v[0])!;
    Assert.Equal(0.4f, head[0], 0.001f);
    Assert.Equal(0.2f, tail[0], 0.001f);
    Assert.True(head[5] > 1f);
    Assert.True(tail[5] < head[5] * 0.5f);
    // After the stop the head is frozen at 0.5 and the tail moves on.
    Assert.Equal(0.5f, SweptTrails.SampleTimes(cue, 0.6f)[0], 0.0001f);
  }

  [Fact]
  public void RealChainswordBlueprintsAndClip()
  {
    const string bps = "/workspace/mtl_fix/trail/raw/t53/ssl/weapons/controllers/melee_controller/combo";
    const string sani = "/workspace/mtl_fix/prop_anims/sani_all/animation/primaris/tacticus/melee/chainsword/lite/tacticus_attack_melee_chainsword_light_slash_1_intercessor.sani";
    if (!Directory.Exists(bps) || !File.Exists(sani)) return;
    var map = AnimEventVfxMap.FromDirectory(bps);
    var trails = map.For("hit1_start_anim").Where(v => v.Edge is not null).ToList();
    Assert.NotEmpty(trails);
    // Chainsword: trail_mele_01_yellow (+ distortion, dark) on the edge sfx_trail3 -> sfx_trail2; power sword t5 -> t1.
    Assert.Contains(trails, v => v.Source == "blueprint_wpn_melee_effects_chainsword" && v.Effect == "trail_mele_01_yellow"
      && v.Edge == new TrailEdge("sfx_trail3", Vector3.Zero, "sfx_trail2", Vector3.Zero));
    Assert.Contains(trails, v => v.Source == "blueprint_wpn_melee_effects_power_sword" && v.Edge!.Locator1 == "t5" && v.Edge.Locator2 == "t1");
    var events = SaniReader.ReadEvents(File.ReadAllBytes(sani));
    Assert.NotNull(events);
    var tags = events!.Value.Tracks.SelectMany(t => t.Events).SelectMany(e => e.Tags).ToHashSet();
    Assert.Contains("hit1_start_anim", tags);

    const string sfx = "/workspace/vfx3_raw/sfx";
    if (!Directory.Exists(sfx)) return;
    var clip = new AnimationClip { Duration = events.Value.Duration };
    clip.EventTracks.AddRange(events.Value.Tracks);
    var lib = new EffectLibrary(new DirectoryEffectSource(sfx));
    var host = new EffectHost(["root", "sfx_trail2", "sfx_trail3"]);
    host.SetPose([Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity]);
    var fx = new AnimEventEffects(clip, map, n => lib.Resolve(n) is { } t ? EffectDefinition.Build(n, t) : null, host,
      include: v => v.Source == "blueprint_wpn_melee_effects_chainsword", resolve: n => n == "trail_mele_01_yellow" ? "trail_melee_01_yellow" : n);
    var swing = Assert.Single(fx.Swept, c => c.Effect == "trail_melee_01_yellow");
    Assert.True(swing.End > swing.Start && swing.End <= clip.Duration);
    Assert.Equal(TrailShape.Triangle, swing.Def.Shape);
    // sfx track: hit1_start_anim @0.181 s .. mele_1hit_trail_stop_effect @0.813 s (2.167 s clip).
    Assert.Equal(0.181f, swing.Start, 0.002f);
    Assert.Equal(0.813f, swing.End, 0.002f);
  }
}
