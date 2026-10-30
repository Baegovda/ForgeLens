# R54: build a UE scratch map from ForgeLens --levelbench output (templates/*.obj in UE axes/cm + instances_ue.tsv).
# Modes (run through uerun.ps1 / EngineWire, or headless):
#   import  <dir> <start> <count>  -> OBJ templates into /Game/ForgeLens/SM2/Tests/r54_level/Meshes (batched, saved)
#   place   <dir>                  -> new map /Game/SM2_Work/Tests/r54_level/L_hub_battle_barge, one StaticMeshActor per instance, saved
#   capture <out_dir>              -> top + oblique SceneCapture2D into render targets; then "export" in a later call writes PNGs
#   reset                          -> delete the r54 Meshes scratch folder (re-import)
#   probe   <asset>                -> print bounds of one imported mesh (axis/scale check)
# Only touches /Game/ForgeLens/SM2/Tests/r54_level. Never opens/saves Lightning_01, Original/*, /Game/Work/*.
import unreal, sys, os, builtins
ROOT = "/Game/ForgeLens/SM2/Tests/r54_level"; MESH = ROOT + "/Meshes"; MAP = ROOT + "/L_hub_battle_barge"
mode = sys.argv[1]
def safe_name(t): return "SM_" + "".join(ch if ch.isalnum() or ch == "_" else "_" for ch in t)
if mode == "import":
    d, start, count = sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
    files = sorted(f for f in os.listdir(os.path.join(d, "templates")) if f.endswith(".obj"))[start:start + count]
    tasks = []
    for f in files:
        if unreal.EditorAssetLibrary.does_asset_exist(MESH + "/" + safe_name(f[:-4])): continue
        t = unreal.AssetImportTask(); t.filename = os.path.join(d, "templates", f); t.destination_path = MESH
        t.destination_name = safe_name(f[:-4]); t.automated = True; t.replace_existing = True; t.save = True
        tasks.append(t)
    if tasks: unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks(tasks)
    ok = sum(1 for f in files if unreal.EditorAssetLibrary.does_asset_exist(MESH + "/" + safe_name(f[:-4])))
    print("import", start, len(files), "present", ok)
elif mode == "reset":  # scratch only: deletes /Game/ForgeLens/SM2/Tests/r54_level/Meshes (owner t110u: useless scratch may be deleted)
    print("deleted", unreal.EditorAssetLibrary.delete_directory(MESH))
elif mode == "list":
    for a in unreal.EditorAssetLibrary.list_assets(ROOT, True, False)[:int(sys.argv[2])]: print(a)
elif mode == "probe":
    m = unreal.load_asset(sys.argv[2]); b = m.get_bounding_box(); print("bounds", b.min, b.max)
elif mode == "place":
    d = sys.argv[2]
    les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
    cur = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world().get_path_name()
    print("current", cur)
    if "Lightning_01" in cur or "/Game/Work/" in cur or "/Original/" in cur:
        pass  # never saved: we only switch away (new_level does not save the old one with automated prompts off)
    if unreal.EditorAssetLibrary.does_asset_exist(MAP): les.load_level(MAP)
    else: les.new_level(MAP)
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for a in eas.get_all_level_actors():
        if a.actor_has_tag("R54"): eas.destroy_actor(a)
    n = miss = 0; cache = {}
    for line in open(os.path.join(d, "instances_ue.tsv"), encoding="utf-8").read().splitlines()[1:]:
        tpl, idx, mat = line.split("\t"); f = [float(x) for x in mat.split()]
        if tpl not in cache: cache[tpl] = unreal.load_asset(MESH + "/" + safe_name(tpl))
        sm = cache[tpl]
        if sm is None: miss += 1; continue
        m = unreal.Matrix(unreal.Plane(*f[0:4]), unreal.Plane(*f[4:8]), unreal.Plane(*f[8:12]), unreal.Plane(*f[12:16]))
        tr = m.transform()
        a = eas.spawn_actor_from_class(unreal.StaticMeshActor, tr.translation)
        a.set_actor_transform(tr, False, False); a.static_mesh_component.set_static_mesh(sm)
        a.tags = [unreal.Name("R54")]; a.set_actor_label("r54_" + idx + "_" + tpl); n += 1
    # simple light so the capture is readable
    if not any(x.actor_has_tag("R54L") for x in eas.get_all_level_actors()):
        s = eas.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 100000), unreal.Rotator(roll=0, pitch=-50, yaw=30)); s.tags = [unreal.Name("R54L")]
        k = eas.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 0)); k.tags = [unreal.Name("R54L")]
    les.save_current_level()
    print("placed", n, "missing", miss, "map", MAP)
elif mode == "capture":
    out = sys.argv[2]
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    acts = [a for a in eas.get_all_level_actors() if a.actor_has_tag("R54")]
    # Robust framing: median actor location and the 10..90 % spread (a few actors report infinite bounds).
    L = [a.get_actor_location() for a in acts]
    def q(vals, f): v = sorted(vals); return v[int((len(v) - 1) * f)]
    xs = [v.x for v in L]; ys = [v.y for v in L]; zs = [v.z for v in L]
    c0 = unreal.Vector(q(xs, .5), q(ys, .5), q(zs, .5)); size = max(q(xs, .9) - q(xs, .1), q(ys, .9) - q(ys, .1), 2000.0)
    # densest 40 m cell (same rule as ForgeLens LevelScene), parts within 200 m of it
    import collections
    cell = collections.Counter((int(v.x // 4000), int(v.y // 4000), int(v.z // 4000)) for v in L).most_common(1)[0][0]
    seed = unreal.Vector((cell[0] + .5) * 4000, (cell[1] + .5) * 4000, (cell[2] + .5) * 4000)
    near = [v for v in L if (v - seed).length() < 20000] or L
    xs = [v.x for v in near]; ys = [v.y for v in near]; zs = [v.z for v in near]
    c0 = unreal.Vector(q(xs, .5), q(ys, .5), q(zs, .5)); size = max(q(xs, .9) - q(xs, .1), q(ys, .9) - q(ys, .1), 2000.0)
    lo = unreal.Vector(q(xs, .1), q(ys, .1), q(zs, .1)); hi = unreal.Vector(q(xs, .9), q(ys, .9), q(zs, .9))
    print("actors", len(acts), "bounds", lo, hi)
    pend = []
    for tag, loc, rot, ortho in (("top", c0 + unreal.Vector(0, 0, size * 1.5), unreal.Rotator(roll=0, pitch=-90, yaw=0), True),
                                 ("oblique", c0 + unreal.Vector(-size * 0.6, -size * 0.6, size * 0.5), unreal.Rotator(roll=0, pitch=-30, yaw=45), False)):
        rt = unreal.RenderingLibrary.create_render_target2d(unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world(), 1600, 1000, unreal.TextureRenderTargetFormat.RTF_RGBA8)
        cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, loc, rot); cam.tags = [unreal.Name("R54Cap")]
        c = cam.capture_component2d; c.set_editor_property("texture_target", rt)
        c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
        if ortho: c.set_editor_property("projection_type", unreal.CameraProjectionMode.ORTHOGRAPHIC); c.set_editor_property("ortho_width", size * 1.05)
        else: c.set_editor_property("fov_angle", 60)
        c.capture_scene(); pend.append((rt, out, "ue_r54_" + tag + ".png", cam))
    builtins._r54_pending = pend; print("captured", len(pend))
elif mode == "export":
    w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world(); eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for rt, out, name, cam in builtins._r54_pending:
        unreal.RenderingLibrary.export_render_target(w, rt, out, name); eas.destroy_actor(cam)
    print("exported", len(builtins._r54_pending))
