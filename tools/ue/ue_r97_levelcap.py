# ue_r97_levelcap.py (R97r): load the R54 hub level, capture 4 views from the actor-centroid at 180 cm into RTF_RGBA8
# (non-sRGB target, FINAL_COLOR_LDR, eye adaptation on) + print sky/fog settings. usage: uerun ue_r97_levelcap.py <outdir>
import sys, unreal
out = sys.argv[1].split(",")[0]
LV = "/Game/SM2_Work/Tests/r54_level/L_hub_battle_barge"
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
if w.get_path_name().split(".")[0] != LV:
    if w.get_path_name().startswith("/Game/ForgeLens/"): les.save_current_level()
    les.load_level(LV)
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world(); print("R97R world", w.get_path_name())
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem); acts = E.get_all_level_actors()
for a in acts:
    if isinstance(a, unreal.SkyLight):
        c = a.light_component; print("R97R sky", a.get_actor_label(), c.get_editor_property("source_type"), c.get_editor_property("cubemap").get_name() if c.get_editor_property("cubemap") else None, c.intensity, c.get_editor_property("source_cubemap_angle"))
    if isinstance(a, unreal.ExponentialHeightFog):
        c = a.component; print("R97R fog", c.get_editor_property("fog_inscattering_luminance") if hasattr(c, "fog_inscattering_luminance") else c.get_editor_property("fog_inscattering_color"), c.get_editor_property("fog_density"))
    if isinstance(a, (unreal.DirectionalLight, unreal.PostProcessVolume)): print("R97R", type(a).__name__, a.get_actor_label())
ps = [a for a in acts if isinstance(a, unreal.PlayerStart)]
if ps: c0 = ps[0].get_actor_location()
else:
    sm = sorted([a.get_actor_location() for a in acts if a.get_component_by_class(unreal.StaticMeshComponent)], key=lambda v: v.x)
    xs = sorted(v.x for v in sm); ys = sorted(v.y for v in sm); zs = sorted(v.z for v in sm); k = len(sm) // 2
    c0 = unreal.Vector(xs[k], ys[k], zs[len(zs) // 5]); print("R97R n", len(sm))
print("R97R viewpoint", c0, "playerstart", bool(ps))
rt = unreal.RenderingLibrary.create_render_target2d(w, 1280, 720, unreal.TextureRenderTargetFormat.RTF_RGBA8)
cp = c0 + unreal.Vector(0, 0, 180)
cap = E.spawn_actor_from_class(unreal.SceneCapture2D, cp, unreal.Rotator(0, 0, 0)); cc = cap.capture_component2d
cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_every_frame", False)
cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); cc.set_editor_property("fov_angle", 80)
sf = cc.get_editor_property("show_flag_settings"); s = unreal.EngineShowFlagsSetting(); s.show_flag_name = "EyeAdaptation"; s.enabled = True; cc.set_editor_property("show_flag_settings", list(sf) + [s])
for yaw in (0, 90, 180, 270):
    cap.set_actor_rotation(unreal.Rotator(0, -5, yaw), False)
    for _ in range(4): cc.capture_scene()
    unreal.RenderingLibrary.export_render_target(w, rt, out, "hub_y%03d.png" % yaw); print("R97R wrote", yaw)
E.destroy_actor(cap)

