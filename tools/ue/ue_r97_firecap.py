# ue_r97_firecap.py (R97m): open LS_arifle_bolt_rifle, park at frames, capture the muzzle region with a SceneCapture2D
# (FINAL_COLOR_LDR into RTF_RGBA8 = non-sRGB target) and export PNGs. usage: uerun ue_r97_firecap.py <outdir>,<f1;f2;...>
import sys, unreal
a = sys.argv[1:]; a = a[0].split(",") if len(a) == 1 else a
out = a[0]; frames = [int(x) for x in a[1].split(";")]
LS = "/Game/ForgeLens/SM2/Sequences/BoltRifleFire_r97/LS_arifle_bolt_rifle"
ls = unreal.load_asset(LS); L = unreal.LevelSequenceEditorBlueprintLibrary
L.open_level_sequence(ls)
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
rt = unreal.RenderingLibrary.create_render_target2d(unreal.EditorLevelLibrary.get_editor_world(), 1280, 720, unreal.TextureRenderTargetFormat.RTF_RGBA8)
cap = None
for f in frames:
    L.set_current_time(f); L.refresh_current_level_sequence()
    acts = E.get_all_level_actors()
    mf = [x for x in acts if "Muzzle" in x.get_actor_label()]
    who = [x for x in acts if isinstance(x, unreal.SkeletalMeshActor) or "titus" in x.get_actor_label().lower()]
    print("R97M frame", f, "muzzle", [ (x.get_actor_label(), str(x.get_actor_location())) for x in mf][:3], "chars", [x.get_actor_label() for x in who][:4])
    tgt = mf[0].get_actor_location() if mf else (who[0].get_actor_location() + unreal.Vector(0, 0, 120) if who else unreal.Vector(0, 0, 120))
    cp = tgt + unreal.Vector(-60, 260, 30)
    if cap is None:
        cap = E.spawn_actor_from_class(unreal.SceneCapture2D, cp, unreal.MathLibrary.find_look_at_rotation(cp, tgt)); cap.set_actor_label("R97M_cap")
        cc = cap.capture_component2d; cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_every_frame", False)
        cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); cc.set_editor_property("fov_angle", 50)
    cap.set_actor_location_and_rotation(cp, unreal.MathLibrary.find_look_at_rotation(cp, tgt), False, False)
    cc = cap.capture_component2d; cc.capture_scene(); cc.capture_scene()
    unreal.RenderingLibrary.export_render_target(unreal.EditorLevelLibrary.get_editor_world(), rt, out, "fire_f%03d.png" % f)
    print("R97M wrote", out, "fire_f%03d.png" % f)
E.destroy_actor(cap)
L.close_level_sequence()
