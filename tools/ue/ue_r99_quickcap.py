# ue_r99_quickcap.py — single LDR capture from a fixed camera (fast; hub/floor/coat)
# usage: outdir tag [yaw] [x,y,z optional "keep"]
import sys, unreal, os
out = sys.argv[1].split(",")[0]
tag = sys.argv[2] if len(sys.argv) > 2 else "view"
yaw = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0
os.makedirs(out, exist_ok=True)
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
print("R99Q world", w.get_path_name())
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
acts = E.get_all_level_actors()
ps = [a for a in acts if isinstance(a, unreal.PlayerStart)]
if ps:
    c0 = ps[0].get_actor_location()
else:
    sm = [a.get_actor_location() for a in acts if a.get_component_by_class(unreal.StaticMeshComponent)]
    if sm:
        xs = sorted(v.x for v in sm); ys = sorted(v.y for v in sm); zs = sorted(v.z for v in sm)
        k = len(sm)//2; c0 = unreal.Vector(xs[k], ys[k], zs[max(0,len(zs)//5)])
    else:
        c0 = unreal.Vector(0,0,100)
print("R99Q cam_base", c0)
rt = unreal.RenderingLibrary.create_render_target2d(w, 960, 540, unreal.TextureRenderTargetFormat.RTF_RGBA8)
cp = c0 + unreal.Vector(0, 0, 160)
cap = E.spawn_actor_from_class(unreal.SceneCapture2D, cp, unreal.Rotator(0, -8, yaw))
cc = cap.capture_component2d
cc.set_editor_property("texture_target", rt)
cc.set_editor_property("capture_every_frame", False)
cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
cc.set_editor_property("fov_angle", 75)
# skip EyeAdaptation show-flag spam for speed
for _ in range(2):
    cc.capture_scene()
fn = "%s.png" % tag
unreal.RenderingLibrary.export_render_target(w, rt, out, fn)
print("R99Q wrote", os.path.join(out, fn))
E.destroy_actor(cap)
