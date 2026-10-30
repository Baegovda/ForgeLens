# Capture: "capture" renders into kept render targets, "export" (a later call) writes PNGs; exporting in the same call
# returned stale frames. For an animated pose run "pose" (same args) first in its own call, then "capture", then "export".
import unreal, sys, builtins
mode = sys.argv[1]
posed = getattr(builtins, "_sm2_posed", None) if mode == "capture" else None
if mode in ("capture", "pose"):
    asset, out, src = sys.argv[2], sys.argv[3], sys.argv[4]
    head = len(sys.argv) > 5 and sys.argv[5] == "head"
    w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    m = unreal.load_asset(asset)
    base = unreal.Vector(300000, 300000, 50000)
    # Clear what earlier runs left behind (a failed capture never reaches export, which is what destroys them):
    # anything tagged SM2Cap, plus untagged actors near the capture spot from older versions of this script.
    stale = 0
    for old in ([] if posed else eas.get_all_level_actors()):
        if old.actor_has_tag("SM2Cap") or (old.get_actor_location() - base).length() < 20000:
            eas.destroy_actor(old); stale += 1
    if stale: print("cleared", stale)
    if posed:
        a = posed; builtins._sm2_posed = None
    elif isinstance(m, unreal.SkeletalMesh):
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, base); a.tags = [unreal.Name("SM2Cap")]; a.skeletal_mesh_component.set_skeletal_mesh_asset(m)
        anim = next((x[5:] for x in sys.argv if x.startswith("anim=")), None)
        if anim:
            path, at = anim.rsplit("@", 1)
            seq = unreal.load_asset(path); comp = a.skeletal_mesh_component
            # Editor-world components do not tick, so a SingleNode animation stays on the bind pose unless the
            # component is told to update in the editor and gets a frame first. With "pose" (run first, in its own
            # call) the actor is posed and kept; the following "capture" reuses it.
            comp.set_update_animation_in_editor(True)
            comp.override_animation_data(seq, False, False, float(at) * seq.get_play_length(), 0.0)
            for fn in ("tick_animation", "refresh_bone_transforms"):
                try: getattr(comp, fn)(0.0, False) if fn == "tick_animation" else getattr(comp, fn)()
                except Exception: pass
    else:
        a = eas.spawn_actor_from_class(unreal.StaticMeshActor, base); a.tags = [unreal.Name("SM2Cap")]; a.static_mesh_component.set_static_mesh(m)
    if mode == "pose":
        builtins._sm2_posed = a
        print("posed")
    else:
        o, e = a.get_actor_bounds(False); r = max(e.x, e.y, e.z) * 2.2
        if head and isinstance(m, unreal.SkeletalMesh):
            comp = a.skeletal_mesh_component
            names = [str(b) for b in comp.get_all_socket_names()]
            hb = next((b for b in names if b.lower() == "head"), None) or next((b for b in names if "head" in b.lower()), None)
            o = comp.get_socket_location(hb); r = 70.0
            hidden = unreal.load_asset("/Game/ForgeLens/SM2/Materials/M_Hide")
            if hidden is None:
                # Masked, opacity mask 0: the section draws nothing (the ClothSimHidden one still ghosts).
                hidden = unreal.AssetToolsHelpers.get_asset_tools().create_asset("M_Hide", "/Game/ForgeLens/SM2/Materials", unreal.Material, unreal.MaterialFactoryNew())
                hidden.set_editor_property("blend_mode", unreal.BlendMode.BLEND_MASKED)
                hidden.set_editor_property("two_sided", True)
                mel = unreal.MaterialEditingLibrary
                k = mel.create_material_expression(hidden, unreal.MaterialExpressionConstant, -200, 0)
                k.set_editor_property("r", 0.0)
                mel.connect_material_property(k, "", unreal.MaterialProperty.MP_OPACITY_MASK)
                mel.recompile_material(hidden)
                unreal.EditorAssetLibrary.save_loaded_asset(hidden)
            keep = sys.argv[6].split(",") if len(sys.argv) > 6 else ["helmet"]
            for i, slot in enumerate(m.materials):
                sn = str(slot.material_slot_name).lower()
                if hidden and (any(k in sn for k in keep) if keep[0] != "only" else not any(k in sn for k in keep[1:])):
                    comp.set_material(i, hidden)
        pending = []
        for tag, d, yaw in (("front", unreal.Vector(-1, 0, 0), 0), ("side", unreal.Vector(0, -1, 0), 90), ("back", unreal.Vector(1, 0, 0), 180)):
            rt = unreal.RenderingLibrary.create_render_target2d(w, 1024, 1024, unreal.TextureRenderTargetFormat.RTF_RGBA8)
            cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, o + d * r, unreal.Rotator(roll=0, pitch=0, yaw=yaw)); cam.tags = [unreal.Name("SM2Cap")]
            c = cam.capture_component2d
            c.set_editor_property("texture_target", rt)
            c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_BASE_COLOR if src == "base" else unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
            c.set_editor_property("fov_angle", 45)
            c.capture_scene()
            pending.append((rt, out, tag + ".png", cam))
        builtins._sm2_pending = (pending, a)
        print("captured", len(pending))
else:
    w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    pending, a = builtins._sm2_pending
    for rt, out, name, cam in pending:
        unreal.RenderingLibrary.export_render_target(w, rt, out, name)
        eas.destroy_actor(cam)
    eas.destroy_actor(a)
    print("exported", len(pending))
