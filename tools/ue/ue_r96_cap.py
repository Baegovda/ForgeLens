# ue_r96_cap.py (R96): neutral "training room" capture to compare with the user's Steam F12 shots (grey grid room, daylight).
# uerun.ps1 ue_r96_cap.py <meshpath> <tag> <ev> [yaw] [lit|base]   -> Shots/r96/<tag>_<mode>.png  (RTF_RGBA8, not sRGB: see R95c)
import unreal, sys
mp, tag, ev = sys.argv[1], sys.argv[2], float(sys.argv[3]); yaw = float(sys.argv[4]) if len(sys.argv) > 4 else 0.0
DI = float(sys.argv[6]) if len(sys.argv) > 6 else 20.0; SI = float(sys.argv[7]) if len(sys.argv) > 7 else 6.0
modes = sys.argv[5].split(",") if len(sys.argv) > 5 else ["lit", "base"]
out = "C:/Users/Revaptor_FX/Desktop/SM2_Work/Shots/r96/"
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem); W = unreal.EditorLevelLibrary.get_editor_world()
for a in E.get_all_level_actors():
    if "R96Cap" in [str(t) for t in a.tags]: E.destroy_actor(a)
def tagit(a): a.tags = ["R96Cap"]; return a
mesh = unreal.load_asset(mp); unreal.AutomationLibrary.finish_loading_before_screenshot() if hasattr(unreal.AutomationLibrary,"finish_loading_before_screenshot") else None; L = unreal.Vector(0, 40000, 0)
sk = tagit(E.spawn_actor_from_class(unreal.SkeletalMeshActor, L, unreal.Rotator(0, 0, yaw))); sk.skeletal_mesh_component.set_skinned_asset_and_update(mesh)
sk.skeletal_mesh_component.set_editor_property("disable_cloth_simulation", True)
MF = sys.argv[9] if len(sys.argv) > 9 else ""   # material folder: override each slot with <MF>/<same MI name> if it exists
if MF:
    smc = sk.skeletal_mesh_component
    for i, sm in enumerate(mesh.get_editor_property("materials")):
        mi = sm.get_editor_property("material_interface"); alt = MF + "/" + mi.get_name() if mi else ""
        if alt and unreal.EditorAssetLibrary.does_asset_exist(alt): smc.set_material(i, unreal.load_asset(alt))
OV = sys.argv[8] if len(sys.argv) > 8 else ""   # debug overrides: "Name=val;Normal=flat" applied as MIDs on every slot
if OV:
    smc = sk.skeletal_mesh_component
    for i in range(len(mesh.get_editor_property("materials"))):
        mid = smc.create_dynamic_material_instance(i)
        if not mid: continue
        sname = str(mesh.get_editor_property("materials")[i].get_editor_property("material_slot_name"))
        for kv in OV.split(";"):
            k, v = kv.split("=")
            if "@" in v:  # Name=val@slot -> only that slot
                v, only = v.split("@")
                if only != sname: continue
            if v == "flat": mid.set_texture_parameter_value(k, unreal.load_asset("/Engine/EngineMaterials/FlatNormal"))
            else: mid.set_scalar_parameter_value(k, float(v))
b = mesh.get_bounds(); h = b.box_extent.z * 2; w = max(b.box_extent.x, b.box_extent.y); c = L + b.origin
grey = unreal.load_asset("/Game/ForgeLens/SM2/Tests/r72_mats/M_r72_grey")
fl = tagit(E.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), L + unreal.Vector(0, 0, b.origin.z - b.box_extent.z)))
fl.set_actor_scale3d(unreal.Vector(60, 60, 1)); fl.static_mesh_component.set_material(0, grey)
wl = tagit(E.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), L + unreal.Vector(0, -w * 3, b.origin.z), unreal.Rotator(roll=90, pitch=0, yaw=0)))
wl.set_actor_scale3d(unreal.Vector(60, 60, 1)); wl.static_mesh_component.set_material(0, grey)
d = tagit(E.spawn_actor_from_class(unreal.DirectionalLight, L + unreal.Vector(0, 0, 2000), unreal.Rotator(roll=0, pitch=-45, yaw=-120))); d.light_component.set_intensity(DI)
s = tagit(E.spawn_actor_from_class(unreal.SkyLight, L + unreal.Vector(0, 0, 1500))); s.light_component.set_intensity(SI)
s.light_component.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
s.light_component.set_editor_property("cubemap", unreal.load_asset("/Engine/MapTemplates/Sky/DaylightAmbientCubemap")); s.light_component.recapture_sky()
dist = max(h, w * 1.6) * 1.5
cp = c + unreal.Vector(dist * 0.25, dist, h * 0.1)
cap = tagit(E.spawn_actor_from_class(unreal.SceneCapture2D, cp, unreal.MathLibrary.find_look_at_rotation(cp, c)))
cc = cap.capture_component2d; cc.set_editor_property("fov_angle", 45.0); cc.set_editor_property("capture_every_frame", False)
f = unreal.EngineShowFlagsSetting(); f.set_editor_property("show_flag_name", "EyeAdaptation"); f.set_editor_property("enabled", True); cc.set_editor_property("show_flag_settings", [f])
ps = cc.get_editor_property("post_process_settings")
ps.set_editor_property("override_auto_exposure_method", True); ps.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
ps.set_editor_property("override_auto_exposure_bias", True); ps.set_editor_property("auto_exposure_bias", ev); ps.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); ps.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
cc.set_editor_property("post_process_settings", ps); cc.set_editor_property("post_process_blend_weight", 1.0)
rt = unreal.RenderingLibrary.create_render_target2d(W, 1200, 1000, unreal.TextureRenderTargetFormat.RTF_RGBA8)
cc.set_editor_property("texture_target", rt)
for m in modes:
    cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR if m == "lit" else unreal.SceneCaptureSource.SCS_BASE_COLOR)
    cc.capture_scene(); cc.capture_scene()
    unreal.RenderingLibrary.export_render_target(W, rt, out, "%s_%s.png" % (tag, m))
for a in E.get_all_level_actors():
    if "R96Cap" in [str(t) for t in a.tags]: E.destroy_actor(a)
print("R96CAP", tag, round(h), modes)
