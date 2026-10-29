# Bulk validation (step 4): import one bulk export into /Game/SM2_Work/Tests/bulk/<asset> and check it.
# usage: ue_bulk.py import <asset>   (mesh + exported import_sm2_materials.py + {asset}__*.fbx clips, then check)
#        ue_bulk.py check <asset>    (checks only)
# Export root: Desktop/SM2_Work/Exports/bulk/<asset>/tpl/<asset>.tpl/. Writes Exports/bulk/<asset>.ue.json and prints BULKJSON.
# "Anim Only (Manny)" exports ({asset}__{clip}_Manny.fbx, already on SK_Mannequin's skeleton) are imported onto
# /Game/Characters/Mannequins/Meshes/SK_Mannequin into /Game/SM2_Work/Anims_Mannequin (ue_import_manny.py, yaw 0, _bakN
# backups) and kept out of the SM2 clip import; an export with only _Manny files stops there.
# A re-import first DUPLICATES an existing /Game/SM2_Work/Tests/<set>/<asset> to <asset>_bakN, then reimports in place (never deletes).
import sys, os, re, json, math, time, runpy, unreal

# --- sm2_skel_rule (R43): mesh imports reuse the one existing {mesh}_Skeleton; same-name collisions fail loudly ---
def sm2_mesh_skeleton(folder, mesh_name, root="/Game"):
    import importlib.util, os, unreal
    here = os.path.dirname(os.path.abspath(globals().get("__file__") or os.environ.get("SM2_TOOLS_UE", ".") + "/x"))
    p = os.path.join(here, "sm2_skeleton_rule.py")
    if not os.path.exists(p): p = os.path.join(os.environ.get("SM2_TOOLS_UE", here), "sm2_skeleton_rule.py")
    spec = importlib.util.spec_from_file_location("sm2_skeleton_rule", p); R = importlib.util.module_from_spec(spec); spec.loader.exec_module(R)
    ar = unreal.AssetRegistryHelpers.get_asset_registry()
    f = unreal.ARFilter(class_paths=[unreal.TopLevelAssetPath("/Script/Engine", "Skeleton")], package_paths=[root], recursive_paths=True)
    skels = [str(a.package_name) for a in ar.get_assets(f)]
    sk = R.pick_mesh_skeleton(folder, mesh_name, skels)  # raises SkeletonRuleError on SKELCOLLISION
    if sk: print("SKELREUSE %s/%s -> %s" % (folder, mesh_name, sk))
    return unreal.load_asset(sk) if sk else None
# --- end sm2_skel_rule ---
mode, name = sys.argv[1], sys.argv[2]
SET = sys.argv[3] if len(sys.argv) > 3 else "bulk"
CHAOS = "chaoscloth" in [a.lower() for a in sys.argv[4:]]  # opt-in: also build <asset>_ChaosCloth (bone method stays the default)  # optional set name: Exports/<set>/<asset> -> /Game/SM2_Work/Tests/<set>/<asset>
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"
root = W + "/Exports/" + SET + "/" + name + "/tpl/" + name + ".tpl/"
folder = "/Game/SM2_Work/Tests/" + SET + "/" + name
EAL = unreal.EditorAssetLibrary; mel = unreal.MaterialEditingLibrary
X = unreal.AnimPoseExtensions; WS = unreal.AnimPoseSpaces.WORLD
res = {"asset": name, "errors": [], "warnings": []}
t0 = time.time()
def nanv(v): return any(math.isnan(c) or math.isinf(c) for c in (v.x, v.y, v.z))

def anim_import_fn():
    src = open(W + "/Scripts/ue_import_anim.py", encoding="utf-8").read()
    a = src.index("# --- sm2_anim_import"); b = src.index("# --- end sm2_anim_import")
    g = {}; exec(src[a:b], g); return g["sm2_anim_import"]

def manny_import():
    files = sorted(root + f for f in os.listdir(root) if f.lower().endswith("_manny.fbx")) if os.path.isdir(root) else []
    if not files: return 0
    src = open(W + "/Scripts/ue_import_manny.py", encoding="utf-8").read()
    g = {"__name__": "ue_import_manny"}; exec(src, g)
    try:
        paths = g["run"](files, g["DEST"], g["SKEL"], "0", anim_import_fn())
        res["mannyClips"] = len(paths); res["mannyDest"] = g["DEST"]
    except Exception as e: res["errors"].append("manny import: %r" % e)
    return len(files)

if mode == "import":
    if manny_import() and not os.path.exists(root + name + ".fbx"):
        res["importS"] = round(time.time() - t0, 1); print("BULKJSON " + json.dumps(res)); raise SystemExit
    if EAL.does_directory_exist(folder):
        n = 1
        while EAL.does_directory_exist(folder + "_bak%d" % n): n += 1
        # Back up by DUPLICATING (not renaming): rename_directory on loaded assets + import into the same path left the new
        # mesh with private refs into _bakN (InterchangeAssetImportData/SkeletalMeshDescriptionBulkData) -> unsaveable.
        # The original objects are then reimported in place (replace_existing), a normal reimport.
        okd = EAL.duplicate_directory(folder, folder + "_bak%d" % n)
        if not okd: res["errors"].append("backup duplicate to _bak%d failed; aborting import" % n); print("BULKJSON " + json.dumps(res)); raise SystemExit
        EAL.save_directory(folder + "_bak%d" % n, only_if_is_dirty=False, recursive=True)
        res["warnings"].append("previous import copied to _bak%d" % n)
    t = unreal.AssetImportTask(); t.filename = root + name + ".fbx"; t.destination_path = folder
    t.automated = True; t.replace_existing = True; t.save = True
    o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
    o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    _sk = sm2_mesh_skeleton(folder, name)
    if _sk: o.skeleton = _sk
    t.options = o
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
    res["meshImportS"] = round(time.time() - t0, 1)
    t1 = time.time()
    sys.argv = [root + "import_sm2_materials.py", root + name + ".materials.json", folder]
    try: runpy.run_path(sys.argv[0], run_name="__main__")
    except Exception as e: res["errors"].append("materials: %r" % e)
    res["matImportS"] = round(time.time() - t1, 1)
    clips = sorted(root + f for f in os.listdir(root) if f.lower().endswith(".fbx") and "__" in f and not f.lower().endswith("_manny.fbx"))
    mesh = unreal.load_asset(folder + "/" + name)
    if clips and isinstance(mesh, unreal.SkeletalMesh):
        t2 = time.time()
        try: anim_import_fn()(clips, mesh.skeleton, folder + "/Anims")
        except Exception as e: res["errors"].append("anim import: %r" % e)
        res["animImportS"] = round(time.time() - t2, 1)
    res["clipFiles"] = len(clips)
    if isinstance(mesh, unreal.SkeletalMesh) and not EAL.save_asset(folder + "/" + name, only_if_is_dirty=False):
        res["errors"].append("mesh failed to save (see Saved/Logs, e.g. illegal private reference)")
    # Live physics (SM2PhysicsSetup editor plugin, tools/ue/SM2PhysicsSetup): PhysicsAsset + post-process RigidBody AnimBP
    # from {model}.physics.json. Skipped (warning) when the plugin is not loaded or the json has no cloth/jiggle.
    pj = root + name + ".physics.json"
    if os.path.exists(pj) and isinstance(mesh, unreal.SkeletalMesh):
        lib = getattr(unreal, "SM2PhysicsSetupLibrary", None)
        if lib is None:
            res["warnings"].append("SM2PhysicsSetup plugin not loaded: no live physics")
        else:
            try:
                r = lib.setup_from_json(mesh, pj, folder)
                ok, rep = (r if isinstance(r, tuple) else (bool(r), ""))
                # mtl1-9 RigidBody sim-space defaults (CENTRE base bone, no world feedback); same as the plugin cpp, applied here
                # too so an older installed plugin build gets them. HEURISTIC (see HANDOFF mtl1-9).
                abpc = mesh.get_editor_property("post_process_anim_blueprint")
                has_centre = "CENTRE" in [str(b) for b in X.get_bone_names(X.get_reference_pose(mesh.skeleton))]
                if ok and abpc and has_centre and hasattr(lib, "tune_rigid_body"):
                    abp = unreal.load_asset(abpc.get_path_name().rsplit(".", 1)[0])
                    tr = lib.tune_rigid_body(abp, 2, "CENTRE", 0.0, 0.0, 600.0, 2000.0, 10.0, 100.0)
                    res["rigidBodyTune"] = str(tr[1] if isinstance(tr, tuple) else tr)
                    unreal.EditorAssetLibrary.save_loaded_asset(abp)
                res["physics"] = rep
                if not ok: res["errors"].append("physics: " + str(rep))
                for a in unreal.EditorAssetLibrary.list_assets(folder, False, False):
                    if "_SM2" in a.split("/")[-1]: unreal.EditorAssetLibrary.save_asset(a, only_if_is_dirty=True)
                unreal.EditorAssetLibrary.save_loaded_asset(mesh)
            except Exception as e: res["errors"].append("physics: %r" % e)

# ---- checks ----
mesh = unreal.load_asset(folder + "/" + name)
side = json.load(open(root + name + ".materials.json", encoding="utf-8-sig"))
want = {s["slot"]: s for s in side["slots"]}
res["sidecarSlots"] = len(want)
if not isinstance(mesh, unreal.SkeletalMesh):
    res["errors"].append("no skeletal mesh at " + folder + "/" + name)
else:
    skel = mesh.skeleton
    ref = X.get_reference_pose(skel); bones = [str(b) for b in X.get_bone_names(ref)]
    res["bones"] = len(bones); res["bone0"] = bones[0] if bones else None
    res["refNaN"] = sum(1 for b in bones if nanv(X.get_bone_pose(ref, b, WS).translation))
    bb = mesh.get_bounds(); e = bb.box_extent; o = bb.origin
    res["boundsCm"] = [round(2 * e.x, 1), round(2 * e.y, 1), round(2 * e.z, 1)]; res["boundsNaN"] = nanv(e) or nanv(o)
    mats = mesh.materials; res["slots"] = len(mats)
    bad = []; mis = 0; noslot = []; texbad = []; hidden = 0
    pmap = {"diffuse": "Diffuse", "normal": "Normal", "specular": "Packed"}
    def sc(mi, k): return round(mel.get_material_instance_scalar_parameter_value(mi, k), 4)
    for slot in mats:
        sn = str(slot.material_slot_name); mi = slot.material_interface
        s = want.get(sn)
        if s is None: noslot.append(sn); continue
        if not isinstance(mi, unreal.MaterialInstanceConstant):
            if mi is not None and ("Hide" in mi.get_name() or "Hidden" in mi.get_name()): hidden += 1; continue  # M_SM2_Hide / M_SM2_ClothSimHidden on collision and nav slots
            bad.append((sn, "not an MI", mi.get_name() if mi else None)); continue
        mis += 1
        a = s.get("anisotropicSpec") or {}; ss = s.get("secondSpec") or {}
        got = tuple(mel.get_material_instance_static_switch_parameter_value(mi, k) for k in ("UseSecondSpec", "UseAnisoSpec", "UseAnisoDirMap"))
        exp = (bool(ss), bool(a), bool(a.get("dirMap")))
        if got != exp: bad.append((sn, "switch", got, exp))
        bl = s.get("baseLayer")
        if bl:
            for k, v in {"MetalnessScale": bl["metalness"]["scale"], "MetalnessBias": bl["metalness"]["bias"], "RoughnessScale": bl["roughness"]["scale"], "RoughnessBias": bl["roughness"]["bias"], "Reflectance": bl["reflectance"]}.items():
                if abs(sc(mi, k) - v) > 1e-3: bad.append((sn, k, sc(mi, k), v))
        if ss:
            for k, v in {"SecondMetalnessBias": ss["metalness"]["bias"], "SecondRoughnessBias": ss["roughness"]["bias"]}.items():
                if abs(sc(mi, k) - v) > 1e-3: bad.append((sn, k, sc(mi, k), v))
        if a:
            for k, v in {"AnisoAnisotropy": a["anisotropy"], "AnisoAngle": a["angle"], "AnisoIntensity": a["intensity"]}.items():
                if abs(sc(mi, k) - v) > 1e-3: bad.append((sn, k, sc(mi, k), v))
        for m in s.get("maps", []):
            p = pmap.get(m["role"])
            if not p or m.get("missing"): continue
            if any(x["role"] == m["role"] for x in s["maps"][:s["maps"].index(m)]): continue  # first map of the role only
            tex = mel.get_material_instance_texture_parameter_value(mi, p)
            if tex is None or tex.get_name().lower() != m["name"].lower(): texbad.append((sn, p, m["name"], tex.get_name() if tex else None))
    res["mis"] = mis; res["hiddenSlots"] = hidden; res["matMismatch"] = bad[:20]; res["matMismatchN"] = len(bad)
    res["slotsNotInSidecar"] = noslot; res["texParamBad"] = texbad[:20]; res["texParamBadN"] = len(texbad)
# sidecar / disk textures
allmaps = {(m["name"], m.get("file")) for s in side["slots"] for m in s.get("maps", [])}
res["sidecarMissing"] = sorted({n for s in side["slots"] for m in s.get("maps", []) if m.get("missing") for n in [m["name"]]})
res["filesMissing"] = sorted(n for n, f in allmaps if f and not os.path.exists(os.path.normpath(root + f)))
res["texAssets"] = len([p for p in EAL.list_assets(folder + "/Textures", False, False)]) if EAL.does_directory_exist(folder + "/Textures") else 0
# anims
def frames(s):
    try: return int(unreal.AnimationLibrary.get_num_frames(s))
    except Exception: return None
an = []
if EAL.does_directory_exist(folder + "/Anims"):
    for p in EAL.list_assets(folder + "/Anims", False, False):
        s = unreal.load_asset(p)
        if not isinstance(s, unreal.AnimSequence): continue
        ln = s.get_play_length(); op = unreal.AnimPoseEvaluationOptions()
        p0 = X.get_anim_pose_at_time(s, 0, op); names = [str(b) for b in X.get_bone_names(p0)]
        nan = 0; worst = 0; rpos = []
        for k in range(13):
            ps = X.get_anim_pose_at_time(s, ln * k / 12, op)
            r = X.get_bone_pose(ps, names[0], WS).translation; rpos.append((r.x, r.y, r.z))
            for b in names[::max(1, len(names) // 40)]:
                v = X.get_bone_pose(ps, b, WS).translation
                if nanv(v): nan += 1
                else: worst = max(worst, math.dist((v.x, v.y, v.z), (r.x, r.y, r.z)))
        disp = math.dist(rpos[0], rpos[-1])
        an.append({"clip": s.get_name(), "len": round(ln, 3), "frames": frames(s),
                   "rootMotion": bool(s.get_editor_property("enable_root_motion")), "rootDispCm": round(disp, 1), "speedCmS": round(disp / ln, 1) if ln else 0,
                   "nan": nan, "maxBoneFromRootCm": round(worst, 1)})
res["anims"] = an
res["checkS"] = round(time.time() - t0, 1)
json.dump(res, open(W + "/Exports/" + SET + "/" + name + ".ue.json", "w"), indent=1)
if CHAOS and mode == "import":
    import os
    here = os.path.dirname(os.path.abspath(sys.argv[0] if not globals().get("__file__") else __file__))
    cands = [os.path.join(here, "ue_chaos_cloth.py"), os.path.join(here, "mtl_fix", "ue_chaos_cloth.py")]
    sc = next((c for c in cands if os.path.exists(c)), None)
    if sc:
        sys.argv = [sc, name, SET]
        try: runpy.run_path(sc, run_name="__main__"); res["chaosCloth"] = name + "_ChaosCloth"
        except Exception as e: res["warnings"].append("chaos cloth: %s" % e)
    else: res["warnings"].append("chaos cloth: ue_chaos_cloth.py not found")
print("BULKJSON " + json.dumps(res))
