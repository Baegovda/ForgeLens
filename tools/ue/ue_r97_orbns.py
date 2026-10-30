# ue_r97_orbns.py (R97s): attach NS_vortex_orb to bone DISK of R92_vortex in Map_Floor (beside the R97 orb mesh). mode: attach | cap <outdir>
import sys, unreal
a = sys.argv[1:]; a = a[0].split(",") if len(a) == 1 else a
MAP = "/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor"
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); U = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
w = U.get_editor_world()
if w.get_path_name().split(".")[0] != MAP:
    if w.get_path_name().startswith("/Game/ForgeLens/"): les.save_current_level()
    les.load_level(MAP); w = U.get_editor_world()
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem); acts = E.get_all_level_actors()
vx = [x for x in acts if x.get_actor_label() == "R92_vortex"][0]
ns = [x for x in acts if x.get_actor_label() == "R97_vortex_orb_ns"]
if a[0] == "attach":
    for x in ns: E.destroy_actor(x)
    sys_ = unreal.load_asset("/Game/ForgeLens/SM2/VFX/vortex_orb/NS_vortex_orb")
    n = E.spawn_actor_from_class(unreal.NiagaraActor, vx.get_actor_location()); n.set_actor_label("R97_vortex_orb_ns")
    nc = n.get_editor_property("niagara_component"); nc.set_asset(sys_)
    sk = vx.get_component_by_class(unreal.SkeletalMeshComponent)
    n.attach_to_component(sk, "DISK", unreal.AttachmentRule.SNAP_TO_TARGET, unreal.AttachmentRule.SNAP_TO_TARGET, unreal.AttachmentRule.KEEP_WORLD, False)
    nc.activate(True); les.save_current_level(); print("R97O attached", n.get_actor_location())
else:
    out = a[1]; n = ns[0]; tgt = n.get_actor_location()
    rt = unreal.RenderingLibrary.create_render_target2d(w, 900, 1000, unreal.TextureRenderTargetFormat.RTF_RGBA8)
    caps = [x for x in acts if isinstance(x, unreal.SceneCapture2D) and any(str(t) == "cap_vortex" for t in x.tags)]
    for c in caps:
        cc = c.capture_component2d; old = cc.get_editor_property("texture_target"); cc.set_editor_property("texture_target", rt)
        for _ in range(3): cc.capture_scene()
        unreal.RenderingLibrary.export_render_target(w, rt, out, "orb_ns_%s.png" % a[2] if len(a) > 2 else "orb_ns.png"); cc.set_editor_property("texture_target", old); print("R97O cap", c.get_actor_label())
    print("R97O caps", len(caps), "orb", tgt)
