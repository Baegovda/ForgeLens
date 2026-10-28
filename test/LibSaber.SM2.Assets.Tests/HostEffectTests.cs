using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

public partial class HostEffectTests
{
  private const string Part = """
    timeLife   =   1
    emitters2   =   {
       flash   =   {
          isVisible   =   True
          emission   =   {
             speedNorm   =   {
                constExpr   =   [
                   3,
                   3
                ]
                __type   =   "local__constExpr"
             }
             type   =   {
                explosion   =   {
                   numPartsToEmit   =   [
                      8,
                      8
                   ]
                }
                __type   =   "local__explosion"
             }
          }
          particleParams   =   {
             timeLife   =   [
                1,
                1
             ]
          }
          __type   =   "CPU_EMITTER"
       }
    }
    __type   =   "sfx_part"
    """;

  private const string Group = """
    list   =   {
       fx_flash_part   =   {
          nameParent   =   "tip"
       }
    }
    __type   =   "sfx_group"
    """;

  private static EffectDefinition Muzzle()
  {
    var library = new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_flash_part"] = Part, ["fx_muzzle"] = Group }));
    return EffectDefinition.Build("fx_muzzle", library.Resolve("fx_muzzle")!);
  }

  [Fact]
  public void SocketEmitterWithoutDirEmitsAlongBoneZ()
  {
    var def = Muzzle();
    Assert.True(def.Emitters[0].EmitByNormal);
    Assert.Equal("tip", def.Emitters[0].NameParent);

    var host = new EffectHost(["root", "tip"]);
    var tip = Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 1, 2);
    host.SetPose([Matrix4x4.Identity, tip]);
    Assert.True(host.Binds(def));
    Assert.Equal(1, host.IndexOf("ti*"));
    Assert.Equal(-1, host.IndexOf("sfx_source_pt"));

    var instance = host.Attach(def);
    instance.Seek(0.05f);
    var particles = instance.Emitters[0].Particles;
    Assert.Equal(8, particles.Count);
    var axis = Vector3.TransformNormal(Vector3.UnitZ, tip);
    var mean = Vector3.Zero;
    foreach (var p in particles) { mean += p.Velocity; Assert.True(Vector3.Distance(p.Position, new Vector3(0, 1, 2)) < 0.5f); }
    Assert.True(Vector3.Dot(Vector3.Normalize(mean), axis) > 0.9f, $"mean {mean} axis {axis}");
  }

  [Fact]
  public void ParsesMorphemeEventBlock()
  {
    var ms = new MemoryStream();
    var w = new BinaryWriter(ms);
    void Str(string s) { w.Write(s.Length); w.Write(Encoding.ASCII.GetBytes(s)); }
    w.Write(1); // tracks
    Str("vfx_events"); w.Write(new byte[16]); w.Write(0); w.Write((byte)0);
    w.Write(2); // events
    w.Write(0.25f); w.Write(0f); w.Write(7u); w.Write(1); w.Write(0);
    w.Write(0.5f); w.Write(0.25f); w.Write(0u); w.Write(2); w.Write(0); w.Write(1);
    w.Write(2); // tag table
    Str("Sound"); w.Write(3); w.Write(0);
    Str("Sound"); w.Write(3); w.Write(5);
    var blob = Encoding.ASCII.GetBytes("fire\0blink\0");
    w.Write(blob.Length); w.Write(blob);

    var tracks = AnimEvents.Parse(ms.ToArray());
    var track = Assert.Single(tracks);
    Assert.Equal("vfx_events", track.Name);
    Assert.Equal(2, track.Events.Count);
    Assert.Equal(0.25f, track.Events[0].Start);
    Assert.Equal(7u, track.Events[0].UserData);
    Assert.Equal(["fire"], track.Events[0].Tags);
    Assert.Equal(["fire", "blink"], track.Events[1].Tags);
    Assert.Empty(AnimEvents.Parse(new byte[] { 1, 2 }));
  }

  private const string Blueprint = """
    nodes   =   {
       bulk   =   {
          __type   =   "BlueprintNodeBulkVfxOnMorphemeEvents"
          eventInfos   =   {
             a   =   {
                mrEvent   =   {
                   name   =   "swing"
                }
                vfxList   =   [
                   "fx_swing"
                ]
             }
          }
       }
       ev   =   {
          __type   =   "BlueprintNodeOnMorphemeEvent"
          mrEvent   =   {
             name   =   "fire"
          }
          OnEnter   =   [
             "relay/Input"
          ]
       }
       relay   =   {
          __type   =   "BlueprintNodeSequence"
          Out   =   [
             "start/Input"
          ]
       }
       start   =   {
          __type   =   "BlueprintSfxLibraryStartsfx"
          sfx   =   {
             def   =   "fx_muzzle"
          }
          locatorRef   =   {
             def   =   "tip"
          }
       }
       pipe   =   {
          __type   =   "BlueprintNodeStringConst"
          value   =   {
             def   =   "fx_piped"
          }
          Out   =   [
             "start/sfxName/cb"
          ]
       }
    }
    """;

  [Fact]
  public void MapsEventsThroughBlueprintGraphs()
  {
    var map = new AnimEventVfxMap();
    map.AddBlueprint("bp_test", Blueprint);
    Assert.Equal("fx_swing", Assert.Single(map.For("swing")).Effect);
    var fire = map.For("fire");
    Assert.Contains(fire, v => v.Effect == "fx_muzzle" && v.Locator == "tip" && v.Source == "bp_test");
    Assert.Contains(fire, v => v.Effect == "fx_piped");
    Assert.Empty(map.For("nothing"));
  }

  private const string StopBlueprint = """
    nodes   =   {
       glow   =   {
          __type   =   "BlueprintNodeEquipmentUserMorphemeDurationEvent"
          mrDurationEvent   =   {
             name   =   "vfx_glow"
          }
          OnActivate   =   [
             "start/Input"
          ]
          OnDeactivate   =   [
             "stop/Input"
          ]
       }
       start   =   {
          __type   =   "BlueprintNodeVfxRestart"
          vfxUidRef   =   {
             def   =   "fx_muzzle"
          }
       }
       stop   =   {
          __type   =   "BlueprintNodeVfxStop"
          vfxUidRef   =   {
             def   =   "fx_muzzle"
          }
       }
       trailStop   =   {
          __type   =   "BlueprintNodeEquipmentUserMorphemeEvent"
          mrEvent   =   {
             name   =   "hit_trail_stop_effect"
          }
          OnEmitEvent   =   [
             "pack/Input"
          ]
       }
       pack   =   {
          __type   =   "BlueprintNodeStopPackOfVfx"
          vfxList   =   [
             "trail_a",
             "trail_b"
          ]
       }
    }
    """;

  [Fact]
  public void StopEventsEndEmission()
  {
    var map = new AnimEventVfxMap();
    map.AddBlueprint("bp_stop", StopBlueprint);
    Assert.False(Assert.Single(map.For("vfx_glow")).AtEnd);
    var end = Assert.Single(map.StopsFor("vfx_glow"));
    Assert.True(end.AtEnd);
    Assert.Equal(["trail_a", "trail_b"], Assert.Single(map.StopsFor("hit_trail_stop_effect")).Names);

    // Duration event 0.1..0.3 of a 1 s clip: the muzzle (8-particle burst at 0) starts at 0.1 and is stopped at 0.3.
    var clip = new AnimationClip { Duration = 1f };
    clip.EventTracks.Add(new AnimEventTrack("vfx", [new AnimEvent(0.1f, 0.2f, 0, ["vfx_glow"])]));
    var lib = new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_flash_part"] = Part, ["fx_muzzle"] = Group }));
    var host = new EffectHost(["root", "tip"]);
    host.SetPose([Matrix4x4.Identity, Matrix4x4.Identity]);
    var events = new AnimEventEffects(clip, map, n => lib.Resolve(n) is { } t ? EffectDefinition.Build(n, t) : null, host);
    var cue = Assert.Single(events.Cues);
    Assert.Equal(0.1f, cue.Time, 0.001f);
    Assert.Equal(0.3f, cue.Stop, 0.001f);
    Assert.Equal(1, events.StopCount);
    var inst = Assert.Single(events.At(0.5f));
    Assert.True(inst.Stopped);
    Assert.Empty(events.At(0.3f + 3f));
  }
}
