# R66: textured, full-coverage hub_battle_barge in the UE scratch map (only /Game/SM2_Work/Tests/r54_level).
# Input = ForgeLens --levelbench <game> <level> <dir> 1 (R66): templates/*.obj with usemtl slots, textures/*.png,
# materials_ue.tsv, instances_ue.tsv (local test outputs under Exports\level, never committed).
# Modes (EngineWire via uerun.ps1, one call each; batch the big ones):
#   reset                       -> delete R54/R66 level actors + old Meshes/ folder (scratch)
#   textures <dir> <start> <n>  -> PNG -> Textures/ (TN_* = normal maps)
#   masters                     -> M_R66_Opaque / M_R66_Masked (albedo*tint, normal, rough 0.7)
#   mis <dir>                   -> one MaterialInstanceConstant per slot in materials_ue.tsv -> Materials/
#   meshes <dir> <start> <n>    -> OBJ -> Meshes66/, slots assigned to the MIs by slot name
#   place <dir>                 -> one actor per template with an InstancedStaticMeshComponent (all instances)
#   sky <dir> [fog]             -> SkyAtmosphere + height fog (no black void), keeps R64 lights
#   stats                       -> counts
import unreal, sys, os, collections
ROOT = "/Game/SM2_Work/Tests/r54_level"; TEX = ROOT + "/Textures"; MAT = ROOT + "/Materials"; MESH = ROOT + "/Meshes66"
eal = unreal.EditorAssetLibrary; at = unreal.AssetToolsHelpers.get_asset_tools(); mel = unreal.MaterialEditingLibrary
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
assert ROOT + "/" in world.get_path_name(), world.get_path_name()
mode = sys.argv[1]
def safe(t): return "SM_" + "".join(ch if ch.isalnum() or ch == "_" else "_" for ch in t)
def rows(d, f): return [l.split("\t") for l in open(os.path.join(d, f), encoding="utf-8").read().splitlines()[1:] if l]
def task(f, dest, name):
    t = unreal.AssetImportTask(); t.filename = f; t.destination_path = dest; t.destination_name = name
    t.automated = True; t.replace_existing = True; t.save = True; return t
if mode == "reset":
    n = 0
    for a in eas.get_all_level_actors():
        if a.actor_has_tag("R54") or a.actor_has_tag("R66"): eas.destroy_actor(a); n += 1
    old = eal.does_directory_exist(ROOT + "/Meshes") and eal.delete_directory(ROOT + "/Meshes")
    unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
    print("R66 reset actors", n, "deleted Meshes", old)
elif mode == "textures":
    d, s, n = sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
    fs = sorted(f for f in os.listdir(os.path.join(d, "textures")) if f.endswith(".png"))[s:s + n]
    ts = [task(os.path.join(d, "textures", f), TEX, f[:-4]) for f in fs if not eal.does_asset_exist(TEX + "/" + f[:-4])]
    if ts: at.import_asset_tasks(ts)
    for f in fs:
        if not f.startswith("TN_"): continue
        t = unreal.load_asset(TEX + "/" + f[:-4])
        if t and t.get_editor_property("compression_settings") != unreal.TextureCompressionSettings.TC_NORMALMAP:
            t.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_NORMALMAP); t.set_editor_property("srgb", False); eal.save_loaded_asset(t)
    print("R66 textures", s, len(fs), "imported", len(ts), "present", sum(eal.does_asset_exist(TEX + "/" + f[:-4]) for f in fs))
elif mode == "masters":
    for name, masked in (("M_R66_Opaque", False), ("M_R66_Masked", True)):
        p = MAT + "/" + name
        if eal.does_asset_exist(p): eal.delete_asset(p)
        m = at.create_asset(name, MAT, unreal.Material, unreal.MaterialFactoryNew())
        if masked: m.set_editor_property("blend_mode", unreal.BlendMode.BLEND_MASKED); m.set_editor_property("two_sided", True)
        a = mel.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -600, 0); a.set_editor_property("parameter_name", "Albedo")
        tint = mel.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -600, 300); tint.set_editor_property("parameter_name", "Tint"); tint.set_editor_property("default_value", unreal.LinearColor(1, 1, 1, 1))
        mul = mel.create_material_expression(m, unreal.MaterialExpressionMultiply, -250, 0)
        mel.connect_material_expressions(a, "RGB", mul, "A"); mel.connect_material_expressions(tint, "", mul, "B")
        mel.connect_material_property(mul, "", unreal.MaterialProperty.MP_BASE_COLOR)
        if masked: mel.connect_material_property(a, "A", unreal.MaterialProperty.MP_OPACITY_MASK)
        nm = mel.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -600, 600); nm.set_editor_property("parameter_name", "Normal")
        nm.set_editor_property("sampler_type", unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL)
        nm.set_editor_property("texture", unreal.load_asset("/Engine/EngineMaterials/FlatNormal"))
        mel.connect_material_property(nm, "RGB", unreal.MaterialProperty.MP_NORMAL)
        r = mel.create_material_expression(m, unreal.MaterialExpressionConstant, -250, 400); r.set_editor_property("r", 0.7)
        mel.connect_material_property(r, "", unreal.MaterialProperty.MP_ROUGHNESS)
        m.set_editor_property("used_with_instanced_static_meshes", True)
        mel.recompile_material(m); eal.save_loaded_asset(m); print("R66 master", name)
elif mode == "mis":
    d = sys.argv[2]; opq = unreal.load_asset(MAT + "/M_R66_Opaque"); msk = unreal.load_asset(MAT + "/M_R66_Masked"); n = nt = 0
    for slot, alb, nrm, blend, r, g, b, _ in rows(d, "materials_ue.tsv"):
        p = MAT + "/" + slot
        mi = unreal.load_asset(p) if eal.does_asset_exist(p) else at.create_asset(slot, MAT, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        mel.set_material_instance_parent(mi, msk if blend in ("masked", "translucent") else opq)
        if alb and eal.does_asset_exist(TEX + "/" + alb): mel.set_material_instance_texture_parameter_value(mi, "Albedo", unreal.load_asset(TEX + "/" + alb)); nt += 1
        if nrm and eal.does_asset_exist(TEX + "/" + nrm): mel.set_material_instance_texture_parameter_value(mi, "Normal", unreal.load_asset(TEX + "/" + nrm))
        if os.environ.get("R66_TINT", "0") != "1": r = g = b = 1.0  # the ForgeLens viewer ignores glt.albedo_tint (looked rust-brown in UE with it)
        mel.set_material_instance_vector_parameter_value(mi, "Tint", unreal.LinearColor(float(r), float(g), float(b), 1))
        eal.save_loaded_asset(mi); n += 1
    print("R66 mis", n, "with albedo", nt)
elif mode == "meshes":
    d, s, n = sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
    fs = sorted(f for f in os.listdir(os.path.join(d, "templates")) if f.endswith(".obj"))[s:s + n]
    ts = [task(os.path.join(d, "templates", f), MESH, safe(f[:-4])) for f in fs if not eal.does_asset_exist(MESH + "/" + safe(f[:-4]))]  # existing meshes: slots re-assigned only
    if ts: at.import_asset_tasks(ts)
    ok = slots = hit = zero = 0
    for f in fs:
        m = unreal.load_asset(MESH + "/" + safe(f[:-4]))
        if not isinstance(m, unreal.StaticMesh): continue
        ok += 1; bb = m.get_bounding_box(); zero += (bb.max - bb.min).length() == 0
        ch = False
        for i, x in enumerate(m.get_editor_property("static_materials")):
            slots += 1; nm = str(x.get_editor_property("material_slot_name")); p = MAT + "/" + nm
            if eal.does_asset_exist(p):
                mi = unreal.load_asset(p)
                if x.get_editor_property("material_interface") != mi: m.set_material(i, mi); ch = True
                hit += m.get_material(i) == mi
        if ch: eal.save_loaded_asset(m)
    print("R66 meshes", s, len(fs), "imported", len(ts), "present", ok, "zero_bounds", zero, "slots", slots, "assigned", hit)
elif mode == "place":
    d = sys.argv[2]; sds = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
    for a in eas.get_all_level_actors():
        if a.actor_has_tag("R66"): eas.destroy_actor(a)
    by = collections.defaultdict(list)
    for tpl, idx, mat in rows(d, "instances_ue.tsv"):
        f = [float(x) for x in mat.split()]
        by[tpl].append(unreal.Matrix(unreal.Plane(*f[0:4]), unreal.Plane(*f[4:8]), unreal.Plane(*f[8:12]), unreal.Plane(*f[12:16])).transform())
    na = ni = miss = 0
    for tpl, trs in by.items():
        sm = unreal.load_asset(MESH + "/" + safe(tpl)) if eal.does_asset_exist(MESH + "/" + safe(tpl)) else None
        if sm is None: miss += len(trs); continue
        a = eas.spawn_actor_from_class(unreal.Actor, unreal.Vector(0, 0, 0)); a.tags = [unreal.Name("R66")]; a.set_actor_label("r66_" + tpl)
        root = sds.k2_gather_subobject_data_for_instance(a)[0]
        h, fail = sds.add_new_subobject(unreal.AddNewSubobjectParams(parent_handle=root, new_class=unreal.InstancedStaticMeshComponent))
        c = a.get_components_by_class(unreal.InstancedStaticMeshComponent)[0]
        c.set_static_mesh(sm); c.set_editor_property("mobility", unreal.ComponentMobility.STATIC)
        c.add_instances(trs, False, True); na += 1; ni += c.get_instance_count()
    unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
    print("R66 place actors", na, "instances", ni, "missing_instances", miss, "templates", len(by))
elif mode == "sky":
    for a in eas.get_all_level_actors():
        if a.actor_has_tag("R66S"): eas.destroy_actor(a)
    T = [unreal.Name("R66S")]
    # the level sits ~700 m below UE z=0: put the planet top / fog base under the lowest 2 % of instances
    zs = sorted(float(m.split()[14]) for _, _, m in rows(sys.argv[2], "instances_ue.tsv")); base = zs[int(len(zs) * .02)] - 5000
    s = eas.spawn_actor_from_class(unreal.SkyAtmosphere, unreal.Vector(0, 0, base)); s.tags = T
    s.get_component_by_class(unreal.SkyAtmosphereComponent).set_editor_property("transform_mode", unreal.SkyAtmosphereTransformMode.PLANET_TOP_AT_COMPONENT_TRANSFORM)
    f = eas.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector(0, 0, base)); f.tags = T
    f.component.set_editor_property("fog_density", float(sys.argv[3]) if len(sys.argv) > 3 else 0.0003); f.component.set_editor_property("fog_height_falloff", 0.02)
    f.component.set_editor_property("fog_inscattering_luminance", unreal.LinearColor(0.05, 0.07, 0.12, 1))
    sun = [a for a in eas.get_all_level_actors() if a.actor_has_tag("R64L") and isinstance(a, unreal.DirectionalLight)]
    for a in sun: a.light_component.set_editor_property("atmosphere_sun_light", a.get_actor_rotation().pitch < 0)
    unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
    print("R66 sky atmosphere + fog base z", base, "sun", len(sun))
elif mode in ("capture", "export"):
    import builtins
    def pp(st, ev):
        st.set_editor_property("override_auto_exposure_method", True); st.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
        st.set_editor_property("override_auto_exposure_bias", True); st.set_editor_property("auto_exposure_bias", ev)
        st.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); st.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
    if mode == "capture":
        d, out, ev = sys.argv[2], sys.argv[3], float(sys.argv[4]); views = sys.argv[5] if len(sys.argv) > 5 else ""
        L = [unreal.Vector(*[float(x) for x in m.split()[12:15]]) for _, _, m in rows(d, "instances_ue.tsv")]
        def q(v, f): v = sorted(v); return v[int((len(v) - 1) * f)]
        cell = collections.Counter((int(v.x // 4000), int(v.y // 4000), int(v.z // 4000)) for v in L).most_common(1)[0][0]
        seed = unreal.Vector((cell[0] + .5) * 4000, (cell[1] + .5) * 4000, (cell[2] + .5) * 4000)
        near = [v for v in L if (v - seed).length() < 20000] or L
        c = unreal.Vector(q([v.x for v in near], .5), q([v.y for v in near], .5), q([v.z for v in near], .5))
        size = max(q([v.x for v in near], .9) - q([v.x for v in near], .1), q([v.y for v in near], .9) - q([v.y for v in near], .1), 2000.0)
        V = [("overview", c + unreal.Vector(-size * .55, -size * .55, size * .45), unreal.Rotator(roll=0, pitch=-30, yaw=45), 70)]
        cl = collections.Counter((int(v.x // 1500), int(v.y // 1500), int(v.z // 1500)) for v in near).most_common(12)
        pick = [cl[0][0]]
        for k, _ in cl[1:]:
            if all(abs(k[0] - p0[0]) + abs(k[1] - p0[1]) > 3 for p0 in pick): pick.append(k)
            if len(pick) == 3: break
        for i, k in enumerate(pick):
            cc = unreal.Vector((k[0] + .5) * 1500, (k[1] + .5) * 1500, (k[2] + .5) * 1500)
            V.append(("close%d" % (i + 1), cc + unreal.Vector(-1100, -1100, 700), unreal.Rotator(roll=0, pitch=-22, yaw=45), 75))
        # the viewer's own persp/close cameras (LevelScene.PickCameras: orbit pitch 0.45/0.3, vertical fov 45 deg) from levelbench.txt
        import re, math
        lb = open(os.path.join(d, "levelbench.txt"), encoding="utf-8").read()
        cm = re.search(r"cameras persp yaw ([-\d.]+) d ([-\d.]+) close yaw ([-\d.]+) d ([-\d.]+)", lb); ce = re.search(r"camera centre <([-\d.]+), ([-\d.]+), ([-\d.]+)>", lb)
        if cm and ce:
            g = [float(x) for x in ce.groups()]; tgt = unreal.Vector(g[0] * 100, g[2] * 100, g[1] * 100)
            hfov = math.degrees(2 * math.atan(math.tan(math.pi / 8) * 1.6))
            for tag, yaw, dist, pit in (("vpersp", float(cm.group(1)), float(cm.group(2)), .45), ("vclose", float(cm.group(3)), float(cm.group(4)), .3)):
                e = (math.cos(pit) * math.sin(yaw) * dist, math.sin(pit) * dist, math.cos(pit) * math.cos(yaw) * dist)
                eye = tgt + unreal.Vector(e[0] * 100, e[2] * 100, e[1] * 100)
                V.append((tag, eye, unreal.MathLibrary.find_look_at_rotation(eye, tgt), hfov, dist))
        if views: V = [v for v in V if v[0] in views.split(",")]
        pend = []
        for v in V:
            tag, loc, rot, fov = v[:4]
            rt = unreal.RenderingLibrary.create_render_target2d(world, 1600, 1000, unreal.TextureRenderTargetFormat.RTF_RGBA8)
            cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, loc, rot); cam.tags = [unreal.Name("R66Cap")]
            k = cam.capture_component2d; k.set_editor_property("texture_target", rt)
            k.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); k.set_editor_property("fov_angle", fov)
            if len(v) > 4: k.set_editor_property("override_custom_near_clipping_plane", True); k.set_editor_property("custom_near_clipping_plane", v[4])  # viewer near = distance*0.01 m
            st = k.get_editor_property("post_process_settings"); pp(st, ev); k.set_editor_property("post_process_settings", st); k.set_editor_property("post_process_blend_weight", 1.0)
            k.capture_scene(); pend.append((rt, out, "ue_r66_" + tag + ".png", cam)); print("R66 view", tag, loc)
        builtins._r66_pending = pend; print("R66 captured", len(pend), "centre", c, "size", size)
    else:
        for rt, out, name, cam in builtins._r66_pending:
            f = os.path.join(out, name)
            if os.path.exists(f): os.remove(f)
            unreal.RenderingLibrary.export_render_target(world, rt, out, name); eas.destroy_actor(cam)
        print("R66 exported", len(builtins._r66_pending)); builtins._r66_pending = []
elif mode == "probe":  # probe <mesh template>: slots -> MI -> albedo texture size
    m = unreal.load_asset(MESH + "/" + safe(sys.argv[2]))
    for x in m.get_editor_property("static_materials"):
        mi = x.get_editor_property("material_interface"); t = mel.get_material_instance_texture_parameter_value(mi, "Albedo") if isinstance(mi, unreal.MaterialInstanceConstant) else None
        print("R66 probe", x.get_editor_property("material_slot_name"), mi.get_name() if mi else None, t.get_name() if t else None, t.blueprint_get_size_x() if t else 0, t.get_editor_property("lod_group") if t else "")
elif mode == "stats":
    acts = [a for a in eas.get_all_level_actors() if a.actor_has_tag("R66")]
    inst = sum(c.get_instance_count() for a in acts for c in a.get_components_by_class(unreal.InstancedStaticMeshComponent))
    cnt = lambda p, cls: sum(1 for x in eal.list_assets(p, True, False) if x.split(".")[-1].startswith(cls)) if eal.does_directory_exist(p) else 0
    print("R66 stats actors", len(acts), "instances", inst, "meshes", len(eal.list_assets(MESH, True, False)) if eal.does_directory_exist(MESH) else 0,
          "textures", len(eal.list_assets(TEX, True, False)) if eal.does_directory_exist(TEX) else 0, "materials", len(eal.list_assets(MAT, True, False)) if eal.does_directory_exist(MAT) else 0)
