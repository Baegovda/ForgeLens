# ue_r92_robe.py (R92): sorcerer robe colour check in Tests/r72_mats/Map_Mats, no asset changes.
# uerun.ps1 <this> set,<ev>,<yaw>   then (separate call, so the scene updates)   cap,<dir>,<name>   ; reset
import sys, unreal
F = "/Game/SM2_Work/Tests/r72_mats"
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
a = [t for x in sys.argv[1:] for t in x.split(",") if t]
w = unreal.EditorLevelLibrary.get_editor_world()
if "Map_Mats" not in str(w.get_path_name()): les.load_level(F + "/Map_Mats"); w = unreal.EditorLevelLibrary.get_editor_world()
acts = {str(t): x for x in E.get_all_level_actors() for t in x.tags}
s = acts["R72_sorcerer"]; cc = acts["cap_sorcerer"].capture_component2d
def ev(v):
    ps = cc.get_editor_property("post_process_settings"); ps.set_editor_property("auto_exposure_bias", v); cc.set_editor_property("post_process_settings", ps)
if a[0] == "set": ev(float(a[1])); s.set_actor_rotation(unreal.Rotator(0, 0, float(a[2])), False); print("R92 set", a[1], a[2])
elif a[0] == "cap":
    cc.capture_scene(); cc.capture_scene(); unreal.RenderingLibrary.export_render_target(w, cc.get_editor_property("texture_target"), a[1], a[2] + ".png"); print("R92 cap", a[2])
elif a[0] == "reset": ev(0.0); s.set_actor_rotation(unreal.Rotator(0, 0, 90), False); print("R92 reset")

elif a[0] == "src":  # src,<base|ldr>: capture source base colour / final LDR
    cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_BASE_COLOR if a[1] == "base" else unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); print("R92 src", a[1])
