# ue_r90_cloth.py (R90): generic cloth check over several characters with fast clips (death/run/melee) for the plugin before/after A/B.
# Modes: prep,<subj>,<tag> | scene,<subj>,<tag>,<clipkey> | cap,<dir>,<name> | measure,<name> | sim | stop | open,<map>
import sys, os, math, json, unreal
W = "C:/Users/Revaptor_FX/Desktop/SM2_Work"; F = "/Game/SM2_Work/Tests/r90_cloth"
EAL = unreal.EditorAssetLibrary; L = unreal.SM2PhysicsSetupLibrary
A = "/Game/SM2_Work/Animations/"
SUB = {
 "sorcerer": ("cc_sorcerer_exalted", {"death": A+"cc_sorcerer_exalted/Bulk/cc_sorcerer_exalted__lesser_sorcerer_death_from_idle_sorcerer_exalted"}),
 "titus": ("cc_titus_assault", {"run": A+"cc_titus_assault/yaw180/cc_titus_assault__tacticus_unarmed_run_fwd_intercessor",
                                "melee": A+"cc_titus_assault/yaw180/cc_titus_assault__npc_assault_attack_melee_thunder_hammer_light_1_intercessor"}),
 "calgar": ("cc_calgar", {"run": "/Game/SM2_Work/Tests/r67_phys/anims/cc_calgar__calgar_run_fwd_calgar"}),
 "tech_priest": ("cc_tech_priest", {"death": "/Game/ForgeLens/SM2/Tests/chars_cloth2/cc_tech_priest/Anims/cc_tech_priest__tech_priest_hordemode_death_tech_priest", "walk": "/Game/ForgeLens/SM2/Tests/chars_cloth2/cc_tech_priest/Anims/cc_tech_priest__tech_priest_censer_walk_loop_tech_priest"}),
 "chaplain": ("cc_chaplain", {"turn": "/Game/ForgeLens/SM2/Tests/chars_cloth2/cc_chaplain/Anims/cc_chaplain__chaplain_turn_180_lwd_chaplain"}),
 "commissar": ("cc_commissar", {"death": "/Game/ForgeLens/SM2/Tests/chars_cloth2/cc_commissar/Anims/cc_commissar__death_1_commissar", "run": "/Game/ForgeLens/SM2/Tests/chars_cloth2/cc_commissar/Anims/cc_commissar__trooper_run_start_fwd_v01_commissar", "melee": "/Game/ForgeLens/SM2/Tests/chars_cloth2/cc_commissar/Anims/cc_commissar__trooper_death_melee_front_v01_commissar"}),
 "vortex": ("cc_vortex_beast", {"melee": A+"cc_vortex_beast/cc_vortex_beast__vortex_beast_attack_melee_180_rwd_vortex_beast"}),
}
def pj(n):
    for d in ("chars_cloth2", "chars_cloth", "chars_phys52"):  # R91: chars_cloth2 = tech_priest/chaplain/commissar with _SIM
        p = W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (d, n, n, n)
        if os.path.exists(p): return p
def les(): return unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
def eas(): return unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
def world():
    u = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem); return u.get_game_world() or u.get_editor_world()
def tagged(cls, pre):
    out = {}
    for a in unreal.GameplayStatics.get_all_actors_of_class(world(), cls):
        t = [str(x) for x in a.tags if str(x).startswith(pre)]
        if t: out[t[0]] = a
    return out
def mesh_path(subj, tag): return "%s/%s_%s" % (F, SUB[subj][0], tag)
def prep(subj, tag):
    n = SUB[subj][0]; mp = mesh_path(subj, tag)
    if EAL.does_asset_exist(mp): EAL.delete_asset(mp)
    src = "/Game/SM2_Work/Characters/%s/%s" % (n, n)
    if not EAL.does_asset_exist(src): src = "/Game/ForgeLens/SM2/Tests/chars_cloth2/%s/%s" % (n, n)  # R91 imports under /Game/ForgeLens/SM2
    m = EAL.duplicate_asset(src, mp)
    r = L.create_chaos_cloth(m, pj(n)); conv, rep = (r if isinstance(r, tuple) else ([], ""))[-2:]
    print("R90 converted", subj, tag, len(conv))
    for l in str(rep).split(" | "):
        if "restFitted" in l or "deepIgnored" in l or "fitFloor=" in l: print("R90LOG", l[:300])
    print("R90 clothing", len(m.get_editor_property("mesh_clothing_assets"))); EAL.save_asset(mp, only_if_is_dirty=False)
def grey():
    p = F + "/M_r90_grey"
    if EAL.does_asset_exist(p): return unreal.load_asset(p)
    mat = unreal.AssetToolsHelpers.get_asset_tools().create_asset("M_r90_grey", F, unreal.Material, unreal.MaterialFactoryNew())
    MEL = unreal.MaterialEditingLibrary; c = MEL.create_material_expression(mat, unreal.MaterialExpressionConstant3Vector, -300, 0)
    c.set_editor_property("constant", unreal.LinearColor(0.18, 0.18, 0.18, 1)); MEL.connect_material_property(c, "", unreal.MaterialProperty.MP_BASE_COLOR)
    MEL.recompile_material(mat); EAL.save_loaded_asset(mat); return mat
def pp(s, ev=0.0):
    s.set_editor_property("override_auto_exposure_method", True); s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True); s.set_editor_property("auto_exposure_bias", ev)
    s.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); s.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
    s.set_editor_property("override_motion_blur_amount", True); s.set_editor_property("motion_blur_amount", 0.0)
def scene(subj, tag, ck):
    mp = F + "/Map_r90"; E = eas()
    if EAL.does_asset_exist(mp):
        les().load_level(mp); [E.destroy_actor(x) for x in E.get_all_level_actors()]
    else: les().new_level(mp)
    fl = E.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), unreal.Vector(0, 0, 0))
    fl.set_actor_scale3d(unreal.Vector(60, 60, 1)); fl.static_mesh_component.set_material(0, grey())
    d = E.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 800), unreal.Rotator(roll=0, pitch=-45, yaw=60)); d.light_component.set_intensity(3.0)
    s = E.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 500)); s.light_component.set_intensity(1.0)
    s.light_component.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
    s.light_component.set_editor_property("cubemap", unreal.load_asset("/Engine/MapTemplates/Sky/SunsetAmbientCubemap")); s.light_component.recapture_sky()
    v = E.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0)); v.set_editor_property("unbound", True)
    st = v.get_editor_property("settings"); pp(st); v.set_editor_property("settings", st)
    les().save_current_level(); print("R90 env", subj)
def actor(subj, tag, ck):
    E = eas(); a = E.spawn_actor_from_object(unreal.load_asset(mesh_path(subj, tag)), unreal.Vector(0, 0, 0), unreal.Rotator(0, 0, 90)); print("R90 spawned", a.get_name())
    c = a.skeletal_mesh_component
    c.set_editor_property("disable_post_process_blueprint", True)
    c.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE)
    dd = unreal.SingleAnimationPlayData(); dd.set_editor_property("anim_to_play", unreal.load_asset(SUB[subj][1][ck]))
    dd.set_editor_property("saved_looping", True); dd.set_editor_property("saved_playing", True); c.set_editor_property("animation_data", dd)
    a.set_actor_label("R90_on"); a.tags = ["R90_on"]; les().save_current_level(); print("R90 actor ok")
def cams():
    E = eas()
    rtp = F + "/RT_r90"; T = unreal.AssetToolsHelpers.get_asset_tools()
    rt = unreal.load_asset(rtp) if EAL.does_asset_exist(rtp) else T.create_asset("RT_r90", F, unreal.TextureRenderTarget2D, unreal.TextureRenderTargetFactoryNew())
    rt.set_editor_property("size_x", 1000); rt.set_editor_property("size_y", 1200)
    rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB); EAL.save_loaded_asset(rt)
    for nm, loc, rot in (("back", (0, -420, 170), (-8, 90)), ("side", (420, 0, 170), (-8, 180))):
        cap = E.spawn_actor_from_class(unreal.SceneCapture2D, unreal.Vector(*loc), unreal.Rotator(roll=0, pitch=rot[0], yaw=rot[1]))
        cap.tags = ["R90cap_" + nm]; cc = cap.capture_component2d
        cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_every_frame", False)
        cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); cc.set_editor_property("fov_angle", 55)
        ps = cc.get_editor_property("post_process_settings"); pp(ps); cc.set_editor_property("post_process_settings", ps); cc.set_editor_property("post_process_blend_weight", 1.0)
    les().save_current_level(); print("R90 cams ok")
def cap(d, name):
    os.makedirs(d, exist_ok=True)
    for k, c in tagged(unreal.SceneCapture2D, "R90cap_").items():
        cc = c.capture_component2d; cc.capture_scene()
        unreal.RenderingLibrary.export_render_target(world(), cc.get_editor_property("texture_target"), d.replace("\\", "/"), "%s_%s.png" % (name, k[7:]))
    print("R90 cap", name)
def measure(name):
    for k, a in tagged(unreal.SkeletalMeshActor, "R90_").items():
        c = a.skeletal_mesh_component; out = {"t": name}
        o, ext = a.get_actor_bounds(False); out["nan"] = any(math.isnan(x) or math.isinf(x) for x in (o.x, o.y, o.z, ext.x, ext.y, ext.z))
        for f, args in (("measure_cloth_penetration", (1.0,)), ("measure_cloth_skin_penetration", (1.0, 15.0)), ("measure_cloth_wrong_side", (2.0, 30.0))):
            try:
                r = getattr(L, f)(c, *args); out[f] = [r[0], str(r[1])[:400]] if isinstance(r, tuple) else [r, ""]
            except Exception as ex: out[f] = ["ERR", str(ex)[:200]]
        print("R90M", json.dumps(out))
if __name__ == "__main__":
    a = sys.argv[1].split(",") if len(sys.argv) == 2 else sys.argv[1:]; m = a[0]
    if m == "prep": prep(a[1], a[2])
    elif m == "scene": scene(a[1], a[2], a[3])
    elif m == "actor": actor(a[1], a[2], a[3])
    elif m == "cams": cams()
    elif m == "cap": cap(a[1], a[2])
    elif m == "measure": measure(a[1])
    elif m == "sim": les().editor_play_simulate(); print("R90 sim")
    elif m == "stop": les().editor_request_end_play(); print("R90 stop")


