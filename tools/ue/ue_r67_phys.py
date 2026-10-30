# ue_r67_phys.py (R67): readable ON/OFF physics checks for calgar (jiggle bodies, run clip) and vortex (tentacle Chaos cloth).
# Map per subject /Game/SM2_Work/Tests/r67_phys/Map_<subj>: grey floor, dir + sky + 2 point + spot, manual exposure,
# actor <subj>_on (physics) and <subj>_off (calgar: post-process ABP off; vortex: cloth off), one SceneCapture each.
# Modes: prep_vortex | scene,<subj> | cap,<subj>,<dir>,<tag> | sim | stop | osc,<sec>,<amp> | measure,<subj>,<tag> | open,<map>
import sys, math, json, re, unreal
W = "C:/Users/Revaptor_FX/Desktop/SM2_Work"; F = "/Game/SM2_Work/Tests/r67_phys"
EAL = unreal.EditorAssetLibrary; L = unreal.SM2PhysicsSetupLibrary
VSRC = "/Game/SM2_Work/Characters/cc_vortex_beast/cc_vortex_beast"; VMESH = F + "/cc_vortex_beast_cloth"
VPJ = W + "/Exports/chars_phys52/cc_vortex_beast/tpl/cc_vortex_beast.tpl/cc_vortex_beast.physics.json"
CLIP = F + "/anims/cc_calgar__calgar_run_fwd_calgar"
SUB = {"calgar": dict(mesh="/Game/SM2_Work/Characters/cc_calgar/cc_calgar", clip=CLIP, off="ppabp",
                      bones=["order_jinggle_01", "tassel_01_jiggle_01", "tassel_02_jiggle_02", "BACKup"]),
       "vortex": dict(mesh=VMESH, clip="/Game/SM2_Work/Animations/cc_vortex_beast/cc_vortex_beast__vortex_beast_attack_melee_180_rwd_vortex_beast", off="cloth", bones=[])}
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
def pp(s, ev):
    s.set_editor_property("override_auto_exposure_method", True); s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True); s.set_editor_property("auto_exposure_bias", ev)
    s.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True)
    s.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
    s.set_editor_property("override_motion_blur_amount", True); s.set_editor_property("motion_blur_amount", 0.0)
def prep_vortex():
    if EAL.does_asset_exist(VMESH): EAL.delete_asset(VMESH)
    m = EAL.duplicate_asset(VSRC, VMESH); r = L.create_chaos_cloth(m, VPJ)
    conv, rep = (r if isinstance(r, tuple) else ([], ""))[-2:]
    print("R67 converted", len(conv), [str(c) for c in conv])
    for l in str(rep).split(" | ")[:40]: print("R67LOG", l[:240])
    print("R67 clothing assets", len(m.get_editor_property("mesh_clothing_assets"))); EAL.save_asset(VMESH, only_if_is_dirty=False)
def grey_mat():
    p = F + "/M_r67_grey"
    if EAL.does_asset_exist(p): return unreal.load_asset(p)
    mat = unreal.AssetToolsHelpers.get_asset_tools().create_asset("M_r67_grey", F, unreal.Material, unreal.MaterialFactoryNew())
    MEL = unreal.MaterialEditingLibrary
    c = MEL.create_material_expression(mat, unreal.MaterialExpressionConstant3Vector, -300, 0); c.set_editor_property("constant", unreal.LinearColor(0.18, 0.18, 0.18, 1))
    MEL.connect_material_property(c, "", unreal.MaterialProperty.MP_BASE_COLOR)
    MEL.recompile_material(mat); EAL.save_loaded_asset(mat); return mat
def scene(subj, ev=0.0):
    S = SUB[subj]; mp = F + "/Map_" + subj; E = eas()
    if EAL.does_asset_exist(mp):  # scratch map of ours: reopen and clear it (new_level fails on an existing path)
        les().load_level(mp); [E.destroy_actor(x) for x in E.get_all_level_actors()]
    else: les().new_level(mp)
    print("R67 level", world().get_path_name(), "actors", len(E.get_all_level_actors()))
    fl = E.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), unreal.Vector(0, 0, 0))
    fl.set_actor_scale3d(unreal.Vector(60, 60, 1)); fl.static_mesh_component.set_material(0, grey_mat())
    d = E.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 800), unreal.Rotator(roll=0, pitch=-45, yaw=60)); d.light_component.set_intensity(2.0)
    s = E.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 500)); s.light_component.set_intensity(0.8)
    s.light_component.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
    s.light_component.set_editor_property("cubemap", unreal.load_asset("/Engine/MapTemplates/Sky/SunsetAmbientCubemap")); s.light_component.recapture_sky()
    for y in (600, -600):
        p = E.spawn_actor_from_class(unreal.PointLight, unreal.Vector(0, y, 300)); p.point_light_component.set_intensity(1500); p.point_light_component.set_attenuation_radius(3000)
    v = E.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0)); v.set_editor_property("unbound", True)
    st = v.get_editor_property("settings"); pp(st, ev); v.set_editor_property("settings", st)
    mesh = unreal.load_asset(S["mesh"]); clip = unreal.load_asset(S["clip"])
    o, ext = mesh.get_bounds().origin, mesh.get_bounds().box_extent; gap = max(ext.x, ext.y) * 2.4 + 100
    h = ext.z * 2; fov = 35.0; dist = max(h * 0.6, max(ext.x, ext.y) * 0.75) / math.tan(math.radians(fov / 2)) + 100
    T = unreal.AssetToolsHelpers.get_asset_tools()
    for i, k in enumerate(("on", "off")):
        x = (-0.5 + i) * gap; tag = subj + "_" + k
        a = E.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(x, 0, 0), unreal.Rotator(0, 0, 90))
        c = a.skeletal_mesh_component; c.set_skinned_asset_and_update(mesh); a.set_actor_label(tag); a.tags = [tag]
        if k == "off":
            if S["off"] == "ppabp": c.set_editor_property("disable_post_process_blueprint", True)
            else: c.set_editor_property("disable_cloth_simulation", True)
        c.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE)
        dd = unreal.SingleAnimationPlayData(); dd.set_editor_property("anim_to_play", clip); dd.set_editor_property("saved_looping", True); dd.set_editor_property("saved_playing", True)
        c.set_editor_property("animation_data", dd)
        rp = F + "/RT_" + tag
        rt = unreal.load_asset(rp) if EAL.does_asset_exist(rp) else T.create_asset("RT_" + tag, F, unreal.TextureRenderTarget2D, unreal.TextureRenderTargetFactoryNew())
        rt.set_editor_property("size_x", 900); rt.set_editor_property("size_y", 1100); rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8); EAL.save_loaded_asset(rt)
        cp = unreal.Vector(x + dist * 0.35, dist, ext.z * 1.3); tg = unreal.Vector(x, 0, ext.z)
        cap = E.spawn_actor_from_class(unreal.SceneCapture2D, cp, unreal.MathLibrary.find_look_at_rotation(cp, tg))
        cap.tags = ["cap_" + tag]; cc = cap.capture_component2d
        cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_every_frame", False)
        cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); cc.set_editor_property("fov_angle", fov)
        ps = cc.get_editor_property("post_process_settings"); pp(ps, ev); cc.set_editor_property("post_process_settings", ps); cc.set_editor_property("post_process_blend_weight", 1.0)
    les().save_current_level(); print("R67 scene", subj, "gap", round(gap), "dist", round(dist), "ext", [round(ext.x), round(ext.y), round(ext.z)])
def cap(subj, d, tag):
    for k, c in tagged(unreal.SceneCapture2D, "cap_" + subj).items():
        cc = c.capture_component2d; cc.capture_scene()
        unreal.RenderingLibrary.export_render_target(world(), cc.get_editor_property("texture_target"), d.replace("\\", "/"), "%s_%s.png" % (tag, k.split("_")[-1]))
    print("R67 cap", subj, tag)
def osc(sec, amp, hz=0.7):
    st = {"t": 0.0, "a": [(a, a.get_actor_location()) for a in tagged(unreal.SkeletalMeshActor, "").values()]}
    def tick(dt):
        st["t"] += dt; t = st["t"]
        for a, p0 in st["a"]: a.set_actor_location(p0 + unreal.Vector(math.sin(2 * math.pi * hz * t) * amp if t < sec else 0.0, 0, 0), False, False)
        if t > sec: unreal.unregister_slate_post_tick_callback(st["h"])
    st["h"] = unreal.register_slate_post_tick_callback(tick); print("R67 osc", sec, amp, len(st["a"]))
def measure(subj, tag):
    A = tagged(unreal.SkeletalMeshActor, subj + "_"); out = {"tag": tag}
    for k, a in A.items():
        c = a.skeletal_mesh_component; o, ext = a.get_actor_bounds(False); b = [o.x, o.y, o.z, ext.x, ext.y, ext.z]
        r = {"nan": any(math.isnan(v) or math.isinf(v) for v in b), "ext": [round(v, 1) for v in b[3:]]}
        r["bones"] = {}
        for bn in SUB[subj]["bones"]:
            q = c.get_socket_transform(bn, unreal.RelativeTransformSpace.RTS_COMPONENT).rotation; r["bones"][bn] = [q.x, q.y, q.z, q.w]
        if subj == "vortex":
            for f, args in (("measure_cloth_penetration", (1.0,)), ("measure_cloth_skin_penetration", (1.0, 15.0))):
                try: x = getattr(L, f)(c, *args); r[f] = [x[0], str(x[1])[:700]] if isinstance(x, tuple) else [x, ""]
                except Exception as ex: r[f] = ["ERR", str(ex)[:200]]
        out[k] = r
    if subj == "calgar" and len(A) == 2:
        on, off = out[subj + "_on"]["bones"], out[subj + "_off"]["bones"]
        out["diff_deg"] = {b: round(math.degrees(2 * math.acos(min(1.0, abs(sum(x * y for x, y in zip(on[b], off[b])))))), 2) for b in on}
        for k in A: out[k]["bones"] = {}
    print("R67M", json.dumps(out))
def bodies(path):
    m = unreal.load_asset(path); pa = m.get_editor_property("physics_asset")
    c = unreal.new_object(unreal.SkeletalMeshComponent); c.set_skinned_asset_and_update(m); n = 0; sim = 0
    for i in range(c.get_num_bones()):
        mm = re.match(r"\S+ body=(-?\d+) type=(-?\d+)", L.describe_body(pa, m, str(c.get_bone_name(i))) if pa else "")
        if mm and int(mm.group(1)) >= 0: n += 1; sim += int(mm.group(2)) != 1
    print("R67 bodies", path, "total", n, "simulated", sim, "clothing_assets", len(m.get_editor_property("mesh_clothing_assets")))
if __name__ == "__main__":
    a = sys.argv[1].split(","); m = a[0]
    if m == "prep_vortex": prep_vortex()
    elif m == "rootzero":  # ROOT carries the run translation (bone 0 is cc_calgar_Skeleton, so force_root_lock cannot lock it): zero ROOT translation keys
        s = unreal.load_asset(CLIP); E = unreal.AnimPoseExtensions; po = unreal.AnimPoseEvaluationOptions(); n = unreal.AnimationLibrary.get_num_frames(s)
        rot = []; z = []
        for i in range(n + 1):
            t = unreal.AnimationLibrary.get_time_at_frame(s, i); x = E.get_bone_pose(E.get_anim_pose_at_time(s, t, po), "ROOT", unreal.AnimPoseSpaces.LOCAL)
            rot.append(x.rotation); z.append(unreal.Vector(0, 0, 0))
        ctl = s.get_editor_property("controller"); ctl.open_bracket(unreal.Text("r67 rootzero"))
        ok = ctl.set_bone_track_keys("ROOT", z, rot, [unreal.Vector(1, 1, 1)] * (n + 1)); ctl.close_bracket(); EAL.save_loaded_asset(s)
        p = E.get_bone_pose(E.get_anim_pose_at_time(s, 0.5, po), "ROOT", unreal.AnimPoseSpaces.WORLD).translation; print("R67 rootzero", ok, n, "ROOT@0.5", round(p.x, 2), round(p.y, 2), round(p.z, 2))
    elif m == "lock":
        s = unreal.load_asset(CLIP); s.set_editor_property("force_root_lock", True); EAL.save_loaded_asset(s); print("R67 lock ok")
    elif m == "scene": scene(a[1], float(a[2]) if len(a) > 2 else 0.0)
    elif m == "cap": cap(a[1], a[2], a[3])
    elif m == "sim": les().editor_play_simulate(); print("R67 sim")
    elif m == "stop": les().editor_request_end_play(); print("R67 stop")
    elif m == "osc": osc(float(a[1]), float(a[2]))
    elif m == "measure": measure(a[1], a[2])
    elif m == "bodies": bodies(a[1])
    elif m == "open": les().load_level(a[1]); print("R67 open", a[1])






