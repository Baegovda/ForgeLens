# ue_pie_phys.py (R60): Simulate-in-editor physics check on a scratch map (never Lightning_01 / protected maps).
# Steps (one uerun call each, screenshots between them with ew_shot.ps1):
#   setup[,map]        new scratch map (default /Game/SM2_Work/Tests/r60_pie/Map_PhysCheck): floor, light, one actor per mesh in a row; saves map
#   anims              give each test actor a looping clip (ANIMS; actors without one keep the ref pose); saves map
#   addcap[,x,y,z,pitch,yaw]  add a SceneCapture2D 'R60cap' (+ render target under the map folder) to the scratch map; saves
#   cap,<png path>     capture the sim world through R60cap and write a PNG (viewport-independent screenshot)
#   sim                start Simulate (LevelEditorSubsystem.editor_play_simulate)
#   snap,<tag>         dump every bone (component space) of each test actor to Saved/r60_<tag>.json
#   move,<dx>,<dz>     move all test actors by dx/dz cm in the sim world (non-teleport: drives rigid-body / cloth)
#   cmp,<a>,<b>        per actor: max / mean bone delta between snaps, NaN count, verdict
#   osc,<sec>,<amp>[,hz] per-frame tick callback: oscillates the actors (x and z) for <sec>, then holds still 3 s;
#                      tracks per-actor max bone delta vs the first frame + NaN, writes Saved/r60_osc.json
#   result             print r60_osc.json with verdicts
#   stop               end Simulate/PIE
# Verdict (HEURISTIC thresholds): NaN or max>150cm -> EXPLODE; after settle max>40cm -> NO_SETTLE; moved but max<0.05cm -> NO_MOTION; else OK.
import sys, json, os, math, unreal
MESHES = {"calgar": "/Game/SM2_Work/Characters/cc_calgar/cc_calgar",
          "sorcerer": "/Game/SM2_Work/Characters/cc_sorcerer_exalted/cc_sorcerer_exalted",
          "vortex": "/Game/SM2_Work/Characters/cc_vortex_beast/cc_vortex_beast",
          "sorc_cloth": "/Game/SM2_Work/Tests/r53_cloth/cc_sorcerer_exalted/cc_sorcerer_exalted"}
ANIMS = {"calgar": "/Game/SM2_Work/Tests/r60_pie/anims/cc_calgar__tacticus_wpn1_run_fwd_intercessor",
         "sorcerer": "/Game/SM2_Work/Animations/cc_sorcerer_exalted/Bulk/cc_sorcerer_exalted__sorcerer_exa_vs_tacticus_grab_loop_sorcerer_exalted",
         "vortex": "/Game/SM2_Work/Animations/cc_vortex_beast/cc_vortex_beast__vortex_beast_attack_melee_180_rwd_vortex_beast"}
PROTECTED = ("/Game/SM2_Work/Original", "/Game/Work", "Lightning_01")
SAVED = unreal.Paths.project_saved_dir()

def les(): return unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
def world():
    ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
    return ues.get_game_world() or ues.get_editor_world()
def actors():
    return [a for a in unreal.GameplayStatics.get_all_actors_of_class(world(), unreal.SkeletalMeshActor)
            if a.get_actor_label().startswith("R60_") or str(a.get_name()).startswith("R60_") or "R60_" in str(a.tags)]

def setup(mp="/Game/SM2_Work/Tests/r60_pie/Map_PhysCheck"):
    if any(p in mp for p in PROTECTED) or not mp.startswith("/Game/SM2_Work/Tests/"): raise SystemExit("R60 refuse map " + mp)
    les().new_level(mp)
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    fl = eas.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), unreal.Vector(0, 0, 0))
    fl.set_actor_scale3d(unreal.Vector(60, 30, 1)); fl.set_actor_label("R60floor")
    eas.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 800), unreal.Rotator(0, -50, -30))
    eas.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 500))
    for i, (k, p) in enumerate(MESHES.items()):
        pos = {"calgar": (-450, 0), "sorcerer": (-150, 0), "sorc_cloth": (150, 0), "vortex": (650, 900)}[k]
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(pos[0], pos[1], 0), unreal.Rotator(0, 0, 90))
        c = a.skeletal_mesh_component; c.set_skinned_asset_and_update(unreal.load_asset(p))
        a.set_actor_label("R60_" + k); a.tags = ["R60_" + k]
    les().save_current_level()
    print("R60 setup", mp, [a.get_actor_label() for a in actors()])

def snap(tag):
    out = {}
    for a in actors():
        c = a.skeletal_mesh_component; d = {}
        for i in range(c.get_num_bones()):
            n = c.get_bone_name(i); v = c.get_socket_transform(n, unreal.RelativeTransformSpace.RTS_COMPONENT).translation
            d[str(n)] = [v.x, v.y, v.z]
        out[str(a.tags[0]) if a.tags else a.get_actor_label()] = d
    json.dump(out, open(os.path.join(SAVED, f"r60_{tag}.json"), "w"))
    print("R60 snap", tag, {k: len(v) for k, v in out.items()})

def cmp(a, b, moved=True):
    A = json.load(open(os.path.join(SAVED, f"r60_{a}.json"))); B = json.load(open(os.path.join(SAVED, f"r60_{b}.json")))
    for k in A:
        ds = []; nan = 0; worst = ""
        for n, p in A[k].items():
            q = B.get(k, {}).get(n)
            if q is None: continue
            if any(math.isnan(x) or math.isinf(x) for x in q): nan += 1; continue
            d = math.dist(p, q); ds.append(d)
            if d == max(ds): worst = n
        mx = max(ds) if ds else 0; mean = sum(ds) / len(ds) if ds else 0
        moving = sum(1 for d in ds if d > 0.5)
        v = "EXPLODE" if nan or mx > 150 else ("NO_MOTION" if moved and mx < 0.05 else "OK")
        print(f"R60 cmp {a}->{b} {k} bones={len(ds)} max={mx:.2f}cm({worst}) mean={mean:.3f} moving>0.5cm={moving} nan={nan} {v}")

def bones(c):
    return {str(c.get_bone_name(i)): c.get_socket_transform(c.get_bone_name(i), unreal.RelativeTransformSpace.RTS_COMPONENT).translation for i in range(c.get_num_bones())}

def osc(sec, amp, hz=1.0, tag="phys"):
    # metrics per actor (all bones, component space):
    #   stretch = | |bone-parent| - rest length |  (rigid-body / cloth constraints keep lengths; explosion breaks them)
    #   jump    = frame-to-frame change of (bone - parent)                      (jitter / popping)
    st = {"t": 0.0, "acts": [(a, a.get_actor_location()) for a in actors()], "res": {}, "par": {}}
    for a, _ in st["acts"]:  # tag nophys = baseline with the post-process (rigid body) ABP off: same clip, no physics
        c = a.skeletal_mesh_component
        if bool(c.get_editor_property("disable_post_process_blueprint")) != (tag == "nophys"): c.toggle_disable_post_process_blueprint()
    for a, _ in st["acts"]:
        c = a.skeletal_mesh_component; B = bones(c)
        sim = sim_bones_c(c, list(B))
        par = {k: str(c.get_parent_bone(k)) for k in B if (not sim or k in sim)}; par = {k: v for k, v in par.items() if v in B}
        st["par"][a.get_actor_label()] = par
        print("R60 simbones", a.get_actor_label(), len(sim))
        st["res"][a.get_actor_label()] = {"rest": {k: math.dist(_v(B[k]), _v(B[p])) for k, p in par.items()}, "prev": None,
            "bs": {}, "bj": {}, "max_stretch": 0, "w_stretch": "", "max_jump": 0, "w_jump": "", "settle_jump": 0, "nan": 0, "frames": 0}
    def tick(dt):
        st["t"] += dt; t = st["t"]
        for a, p0 in st["acts"]:
            off = math.sin(2 * math.pi * hz * t) * amp if t < sec else 0.0
            a.set_actor_location(p0 + unreal.Vector(off, 0, abs(off) * 0.3), False, False)
            r = st["res"][a.get_actor_label()]; par = st["par"][a.get_actor_label()]; r["frames"] += 1
            B = bones(a.skeletal_mesh_component); loc = {}
            for k, p in par.items():
                v, q = _v(B[k]), _v(B[p])
                if any(math.isnan(x) or math.isinf(x) for x in v): r["nan"] += 1; continue
                d = [v[i] - q[i] for i in range(3)]; loc[k] = d
                sv = abs(math.hypot(*d) - r["rest"][k]); r["bs"][k] = max(r["bs"].get(k, 0), sv)
                if sv > r["max_stretch"]: r["max_stretch"], r["w_stretch"] = sv, k
                if r["prev"] and k in r["prev"]:
                    j = math.dist(d, r["prev"][k])
                    if t > 0.5: r["bj"][k] = max(r["bj"].get(k, 0), j)
                    if t > 0.5 and j > r["max_jump"]: r["max_jump"], r["w_jump"] = j, k
                    if t > sec + 2.0: r["settle_jump"] = max(r["settle_jump"], j)
            r["prev"] = loc
        if t > sec + 3.0:
            unreal.unregister_slate_post_tick_callback(st["h"])
            out = {k: {kk: vv for kk, vv in v.items() if kk not in ("rest", "prev")} for k, v in st["res"].items()}
            json.dump(out, open(os.path.join(SAVED, f"r60_osc_{tag}.json"), "w")); unreal.log("R60 osc done")
    if os.path.exists(os.path.join(SAVED, f"r60_osc_{tag}.json")): os.remove(os.path.join(SAVED, f"r60_osc_{tag}.json"))
    st["h"] = unreal.register_slate_post_tick_callback(tick)
    print("R60 osc started", sec, amp, hz, [a.get_actor_label() for a, _ in st["acts"]])

def sim_bones_c(c, names):
    mesh = c.get_skinned_asset() if hasattr(c, "get_skinned_asset") else c.skeletal_mesh
    return sim_bones(mesh, names)

def sim_bones(mesh, names=None):
    """Bones with a non-kinematic body in the mesh PA or its phys52 damper PA (what the RigidBody nodes simulate).
    PhysicsAsset.SkeletalBodySetups is not exposed to Python -> SM2PhysicsSetupLibrary.describe_body ("body=<i> type=<t>", 1 = kinematic)."""
    import re
    out = set(); mp = mesh.get_path_name().split(".")[0]; folder = mp.rsplit("/", 1)[0]
    pas = [mesh.get_editor_property("physics_asset")]
    for x in unreal.EditorAssetLibrary.list_assets(folder, recursive=False):
        if x.split(".")[0].endswith("_SM2Damper"): pas.append(unreal.load_asset(x))
    names = names or []  # caller passes the component's bone names
    for pa in pas:
        if not pa: continue
        for n in names:
            m = re.match(r"\S+ body=(-?\d+) type=(-?\d+)", unreal.SM2PhysicsSetupLibrary.describe_body(pa, mesh, n))
            if m and int(m.group(1)) >= 0 and int(m.group(2)) != 1: out.add(n)
    return out

def _v(x): return [x.x, x.y, x.z]

def result():
    # physics-only effect = per bone (phys run) - (nophys baseline, same clip/motion). HEURISTIC thresholds:
    # NaN or added stretch>30cm -> EXPLODE; added jump>15cm/frame -> POP; settle jump>2cm/frame -> JITTER; no bone with added motion>0.2cm -> NO_MOTION
    P = json.load(open(os.path.join(SAVED, "r60_osc_phys.json")))
    N = json.load(open(os.path.join(SAVED, "r60_osc_nophys.json"))) if os.path.exists(os.path.join(SAVED, "r60_osc_nophys.json")) else {}
    for k, r in P.items():
        n = N.get(k, {"bs": {}, "bj": {}, "settle_jump": 0})
        ds = {b: v - n["bs"].get(b, 0) for b, v in r["bs"].items()}; dj = {b: v - n["bj"].get(b, 0) for b, v in r["bj"].items()}
        bs = max(ds, key=ds.get) if ds else ""; bj = max(dj, key=dj.get) if dj else ""
        ms, mj = ds.get(bs, 0), dj.get(bj, 0); moving = sum(1 for v in dj.values() if v > 0.2)
        sj = r["settle_jump"] - n.get("settle_jump", 0)
        v = "EXPLODE" if r["nan"] or ms > 30 else ("POP" if mj > 15 else ("JITTER" if sj > 2 else ("NO_MOTION" if moving == 0 else "OK")))
        print(f"R60 result {k} frames={r['frames']} added_stretch={ms:.2f}cm({bs}) added_jump={mj:.2f}cm/f({bj}) bones_moved_by_phys={moving} settle_jump={sj:.2f} nan={r['nan']} {v}")

if __name__ == "__main__":
    a = sys.argv[1].split(",") if len(sys.argv) == 2 else sys.argv[1:]
    m = a[0]
    if m == "setup": setup(*a[1:2])
    elif m == "anims":
        for x in actors():
            k = str(x.tags[0])[4:] if x.tags else ""
            if k in ANIMS and unreal.EditorAssetLibrary.does_asset_exist(ANIMS[k]):
                c = x.skeletal_mesh_component; c.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE)
                d = unreal.SingleAnimationPlayData(); d.set_editor_property("anim_to_play", unreal.load_asset(ANIMS[k]))
                d.set_editor_property("saved_looping", True); d.set_editor_property("saved_playing", True)
                c.set_editor_property("animation_data", d); print("R60 anim", k, ANIMS[k].split("/")[-1])
        les().save_current_level()
    elif m == "addcap":
        v = [float(x) for x in a[1:6]] if len(a) >= 6 else [150, -1700, 450, -10, 90]
        rt = unreal.AssetToolsHelpers.get_asset_tools().create_asset("RT_R60cap", "/Game/SM2_Work/Tests/r60_pie", unreal.TextureRenderTarget2D, unreal.TextureRenderTargetFactoryNew()) \
            if not unreal.EditorAssetLibrary.does_asset_exist("/Game/SM2_Work/Tests/r60_pie/RT_R60cap") else unreal.load_asset("/Game/SM2_Work/Tests/r60_pie/RT_R60cap")
        rt.set_editor_property("size_x", 1600); rt.set_editor_property("size_y", 900)
        rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8)
        unreal.EditorAssetLibrary.save_loaded_asset(rt)
        eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
        for x in unreal.GameplayStatics.get_all_actors_of_class(world(), unreal.SceneCapture2D): eas.destroy_actor(x)
        cap = eas.spawn_actor_from_class(unreal.SceneCapture2D, unreal.Vector(*v[:3]), unreal.Rotator(roll=0, pitch=v[3], yaw=v[4]))
        cap.set_actor_label("R60cap"); c = cap.capture_component2d
        c.set_editor_property("texture_target", rt); c.set_editor_property("capture_every_frame", False)
        c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
        les().save_current_level(); print("R60 addcap", v)
    elif m == "cap":
        caps = unreal.GameplayStatics.get_all_actors_of_class(world(), unreal.SceneCapture2D)
        c = caps[0].capture_component2d; c.capture_scene()
        out = a[1].replace("\\", "/"); d, f = os.path.split(out)
        unreal.RenderingLibrary.export_render_target(world(), c.get_editor_property("texture_target"), d, f)
        print("R60 cap", out, os.path.exists(out))
    elif m == "sim": les().editor_play_simulate(); print("R60 sim started")
    elif m == "stop": les().editor_request_end_play(); print("R60 stopped")
    elif m == "snap": snap(a[1])
    elif m == "move":
        dx, dz = float(a[1]), float(a[2]) if len(a) > 2 else 0.0
        for x in actors(): x.add_actor_world_offset(unreal.Vector(dx, 0, dz), False, False)
        print("R60 moved", dx, dz, len(actors()))
    elif m == "cmp": cmp(a[1], a[2], a[3] != "0" if len(a) > 3 else True)
    elif m == "osc": osc(float(a[1]), float(a[2]), float(a[3]) if len(a) > 3 else 1.0, a[4] if len(a) > 4 else "phys")
    elif m == "result": result()
    elif m == "list": print("R60 world", world().get_path_name(), [x.get_actor_label() for x in actors()])
