# Framed capture for .sani checks. Same three-call protocol as ue_cap.py: "pose" (own call), "capture", "export".
# usage: ue_cap2.py pose|capture <skeletal mesh> <out dir> anim=<AnimSequence>@<0..1> [hide=<a,b>] [ev=<bias>] [intensity=<k>]
#        ue_cap2.py export
# Framing uses the POSED world positions of the bones (centre) and max(bone spread, mesh bounds sphere) (radius), not the actor bounds: collision hulls (cdt, _glr, rb_) and
# LOD parts inflate the bounds, which put the camera inside the visible mesh. Those slots are also hidden (M_Hide).
# The camera sits at R / sin(fov / 2) from the centre, so it is always outside the framing sphere of radius R.
import unreal, sys, builtins, math
# Slot names only carry part names, so affix-hidden volumes (visibility_*, IndexMeshVisibility) need hide=<a,b>;
# nav_silhouette (navigation silhouette, never rendered) is hidden by default.
HIDE = ("cdt", "_glr", "rb_", "_lod", "nav_silhouette") + tuple(next((x[5:] for x in sys.argv if x.startswith("hide=")), "").lower().split(",") if any(x.startswith("hide=") and len(x) > 5 for x in sys.argv) else ())
mode = sys.argv[1]
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
if mode in ("pose", "capture"):
    asset, out = sys.argv[2], sys.argv[3]
    m = unreal.load_asset(asset)
    base = unreal.Vector(300000, 300000, 50000)
    posed = getattr(builtins, "_sm2_posed", None) if mode == "capture" else None
    if posed:
        a = posed; builtins._sm2_posed = None
    else:
        for old in eas.get_all_level_actors():
            if old.actor_has_tag("SM2Cap") or (old.get_actor_location() - base).length() < 20000:
                eas.destroy_actor(old)
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, base); a.tags = [unreal.Name("SM2Cap")]
        comp = a.skeletal_mesh_component; comp.set_skeletal_mesh_asset(m)
        anim = next((x[5:] for x in sys.argv if x.startswith("anim=")), None)
        if anim:
            path, at = anim.rsplit("@", 1)
            seq = unreal.load_asset(path)
            comp.set_update_animation_in_editor(True)
            comp.override_animation_data(seq, False, False, float(at) * seq.get_play_length(), 0.0)
            try: comp.tick_animation(0.0, False)
            except Exception: pass
            try: comp.refresh_bone_transforms()
            except Exception: pass
    if mode == "pose":
        builtins._sm2_posed = a; print("posed"); raise SystemExit
    comp = a.skeletal_mesh_component
    hidden = unreal.load_asset("/Game/ForgeLens/SM2/Materials/M_Hide")
    nh = 0
    for i, slot in enumerate(m.materials):
        if hidden and any(k in str(slot.material_slot_name).lower() for k in HIDE):
            comp.set_material(i, hidden); nh += 1
    pts = []
    for i in range(comp.get_num_bones()):
        bn = str(comp.get_bone_name(i))
        if any(k in bn.lower() for k in HIDE): continue
        pts.append(comp.get_socket_location(bn))
    lo = unreal.Vector(min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))
    hi = unreal.Vector(max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))
    o = (lo + hi) * 0.5
    # The bones alone under-frame parts that extend far from their node (hologram rings), so the radius also
    # covers the mesh's bind bounds sphere around the posed bone centre.
    mb = m.get_bounds()
    R = max((hi - lo).length() * 0.5 * 1.3, mb.sphere_radius * 1.05, 60.0)
    fov = 40.0
    d = R / math.sin(math.radians(fov / 2))
    print("FRAME bones %d hiddenSlots %d centre %.0f,%.0f,%.0f R %.0f dist %.0f" % (len(pts), nh, o.x - base.x, o.y - base.y, o.z - base.z, R, d))
    kv = float(next((x[10:] for x in sys.argv if x.startswith("intensity=")), 40))
    ev = float(next((x[3:] for x in sys.argv if x.startswith("ev=")), 0))
    lit = []
    for off, mul in ((unreal.Vector(-1.0, -0.6, 0.5), 1.0), (unreal.Vector(-0.6, 1.0, 0.1), 0.35), (unreal.Vector(1.0, 0.3, 0.6), 0.8)):
        L = eas.spawn_actor_from_class(unreal.PointLight, o + off * d); L.tags = [unreal.Name("SM2Cap")]
        lc = L.point_light_component
        lc.set_mobility(unreal.ComponentMobility.MOVABLE)
        lc.set_editor_property("intensity_units", unreal.LightUnits.CANDELAS)
        lc.set_intensity(kv * mul * (d / 100.0) ** 2); lc.set_attenuation_radius(d * 6)
        lit.append(L)
    pending = []
    for tag, yaw, pitch in (("front", 0, 0), ("side", 90, 0), ("three_quarter", 45, -30)):
        rot = unreal.Rotator(roll=0, pitch=pitch, yaw=yaw)
        fwd = rot.get_forward_vector()
        rt = unreal.RenderingLibrary.create_render_target2d(w, 1024, 1024, unreal.TextureRenderTargetFormat.RTF_RGBA8)
        cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, o - fwd * d, rot); cam.tags = [unreal.Name("SM2Cap")]
        c = cam.capture_component2d
        c.set_editor_property("texture_target", rt)
        c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
        c.set_editor_property("fov_angle", fov)
        pps = c.get_editor_property("post_process_settings")
        pps.set_editor_property("override_auto_exposure_method", True); pps.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
        pps.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); pps.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
        pps.set_editor_property("override_auto_exposure_bias", True); pps.set_editor_property("auto_exposure_bias", ev)
        c.set_editor_property("post_process_settings", pps)
        c.capture_scene()
        pending.append((rt, out, tag + ".png", cam))
    builtins._sm2_pending = (pending, a, lit)
    print("captured", len(pending))
else:
    pending, a, lit = builtins._sm2_pending
    for rt, out, name, cam in pending:
        unreal.RenderingLibrary.export_render_target(w, rt, out, name); eas.destroy_actor(cam)
    eas.destroy_actor(a)
    for L in lit: eas.destroy_actor(L)
    print("exported", len(pending))
