# ue_view_actor.py (R59/R60): point the level viewport at an actor (label/class substring) for screenshots.
# Usage: uerun.ps1 ue_view_actor.py <substr>[,dist_cm=250[,yaw=-150[,pitch=-10[,zoff=120]]]]
import sys, math, unreal
a = sys.argv[1].split(",") if len(sys.argv) == 2 else sys.argv[1:]
sub = a[0].lower(); dist = float(a[1]) if len(a) > 1 else 250; yaw = float(a[2]) if len(a) > 2 else -150
pitch = float(a[3]) if len(a) > 3 else -10; zoff = float(a[4]) if len(a) > 4 else 120
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
acts = unreal.GameplayStatics.get_all_actors_of_class(w, unreal.Actor)  # includes Sequencer spawnables
hit = [x for x in acts if sub in x.get_actor_label().lower() or sub in x.get_class().get_name().lower()]
if not hit: raise SystemExit("VIEW no actor " + sub)
t = hit[0].get_actor_location() + unreal.Vector(0, 0, zoff)
r = math.radians(yaw); p = math.radians(pitch)
loc = t - unreal.Vector(math.cos(r) * math.cos(p), math.sin(r) * math.cos(p), math.sin(p)) * dist
unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).set_level_viewport_camera_info(loc, unreal.Rotator(roll=0, pitch=pitch, yaw=yaw))
print("VIEW", hit[0].get_actor_label(), t, loc)
