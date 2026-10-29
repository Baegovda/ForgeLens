# Forge Lens "Direct to Unreal" import (oneclick, 원클릭 내보내기 옵션).
# Forge Lens writes the normal export (FBX, textures, materials.json, import_sm2_materials.py, clips, physics.json), then
# a manifest and runs this script inside Unreal: through the open editor (EngineWire MCP :3000 or Python Remote
# Execution) or headless (UnrealEditor-Cmd.exe <project> -run=pythonscript -script=<run_import.py>).
# usage: sys.argv = ['x', <manifest.json>]   (or env SM2_FORGE_MANIFEST)
# Manifest: {"log": <txt>, "result": <json>, "scriptsDir": <dir with ue_import_anim.py / ue_import_manny.py>,
#            "models": [{"name", "folder" (/Game/...), "fbx", "skeletal", "materials", "materialsScript", "physics",
#                        "clips": [...], "manny": [...], "colourScheme"}]}
# Rules (HANDOFF R03/R05): the destination must be under /Game, never /Game/SM2_Work/Original/* or /Game/Work/*, and not a
# top-level folder. An existing destination folder with assets is first DUPLICATED to <folder>_bakN (N = next free; an
# existing _bakN is never reused), then reimported in place (same reason as ue_bulk.py: renaming loaded assets and
# importing into the old path left private refs into _bakN). Only the destination folder (and its backup) is saved, so
# unrelated dirty packages (e.g. Lightning_01) are never saved. Nothing is deleted.
# The import steps are the existing ones: legacy FBX mesh import (ue_bulk.py), import_sm2_materials.py, sm2_anim_import
# (ue_import_anim.py), ue_import_manny.run (SK_Mannequin), SM2PhysicsSetupLibrary.setup_from_json (+ tune_rigid_body).
import sys, os, json, time, runpy, traceback

PROTECTED = ("/game/sm2_work/original", "/game/work")
MANNY_SKEL = "/Game/Characters/Mannequins/Meshes/SK_Mannequin"


def normalize_dest(path):
    p = (path or "").strip().replace("\\", "/")
    while "//" in p:
        p = p.replace("//", "/")
    return p.rstrip("/")


def dest_error(path):
    """None when the content path is allowed, else the reason (same rule as UnrealContentPath.Validate in Forge Lens)."""
    p = normalize_dest(path)
    if not p.startswith("/Game/"):
        return "the destination must start with /Game/"
    low = p.lower()
    for bad in PROTECTED:
        if low == bad or low.startswith(bad + "/"):
            return "the destination is a protected folder (" + bad + ")"
    parts = [s for s in p.split("/") if s]
    if len(parts) < 3 or (len(parts) == 3 and parts[1].lower() == "sm2_work" and parts[2].lower() in ("characters", "props", "animations", "materials", "tests")):
        return "the destination is a top-level folder; pick a folder for this asset (e.g. /Game/SM2_Work/Props/<asset>)"
    for seg in parts:
        if seg in (".", "..") or any(c in seg for c in ':*?"<>| '):
            return "invalid folder name '" + seg + "'"
    return None


def next_backup(exists, folder):
    n = 1
    while exists(folder + "_bak%d" % n):
        n += 1
    return folder + "_bak%d" % n


class Run:
    def __init__(self, manifest):
        self.m = manifest
        self.log_path = manifest.get("log")
        self.result = {"ok": False, "models": [], "errors": [], "mode": manifest.get("mode", "")}
        self.t0 = time.time()

    def log(self, msg):
        line = "[%6.1fs] %s" % (time.time() - self.t0, msg)
        print("FORGE " + line)
        if self.log_path:
            with open(self.log_path, "a", encoding="utf-8") as f:
                f.write(line + "\n")

    def write_result(self):
        p = self.m.get("result")
        if p:
            with open(p, "w", encoding="utf-8") as f:
                json.dump(self.result, f, indent=1)
        print("FORGEJSON " + json.dumps(self.result))


def anim_import_fn(scripts):
    src = open(os.path.join(scripts, "ue_import_anim.py"), encoding="utf-8").read()
    a = src.index("# --- sm2_anim_import"); b = src.index("# --- end sm2_anim_import")
    g = {}; exec(src[a:b], g); return g["sm2_anim_import"]


def import_model(run, unreal, mdl, res):
    EAL = unreal.EditorAssetLibrary
    folder = normalize_dest(mdl["folder"])
    err = dest_error(folder)
    if err:
        raise RuntimeError(err + ": " + folder)
    res["folder"] = folder
    name = mdl["name"]
    scripts = run.m.get("scriptsDir") or ""
    # backup first (never overwrite: the old import stays complete in <folder>_bakN)
    if EAL.does_directory_exist(folder) and EAL.list_assets(folder, True, False):
        bak = next_backup(EAL.does_directory_exist, folder)
        if not EAL.duplicate_directory(folder, bak):
            raise RuntimeError("backup duplicate to " + bak + " failed; nothing imported")
        EAL.save_directory(bak, only_if_is_dirty=False, recursive=True)
        res["backup"] = bak
        run.log("%s: existing %s backed up to %s" % (name, folder, bak))
    EAL.make_directory(folder)
    mesh = None
    fbx = mdl.get("fbx")
    if fbx:
        if not os.path.exists(fbx):
            raise RuntimeError("missing FBX " + fbx)
        run.log("%s: importing %s mesh" % (name, "skeletal" if mdl.get("skeletal") else "static"))
        t = unreal.AssetImportTask(); t.filename = fbx; t.destination_path = folder
        t.automated = True; t.replace_existing = True; t.save = True
        o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
        if mdl.get("skeletal"):
            o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
        else:
            o.import_as_skeletal = False; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_STATIC_MESH
        t.options = o
        # Headless (UnrealEditor-Cmd -run=pythonscript) has no Slate application, and the Interchange FBX path asserts
        # CurrentApplication.IsValid() after its save (oneclick QA 10/29); the legacy FbxFactory imports fine there.
        legacy = os.environ.get("SM2_FORGE_HEADLESS") == "1"
        if legacy:
            unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 0")
        try:
            unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
        finally:
            if legacy:
                unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 1")
        base = os.path.splitext(os.path.basename(fbx))[0]
        mesh = unreal.load_asset(folder + "/" + base) if EAL.does_asset_exist(folder + "/" + base) else None
        if mesh is None:
            raise RuntimeError("the FBX import made no mesh at " + folder + "/" + base + " (see the Output Log)")
        res["mesh"] = mesh.get_path_name().split(".")[0]
        res["meshClass"] = mesh.get_class().get_name()
        run.log("%s: mesh %s (%s)" % (name, res["mesh"], res["meshClass"]))
        mj, ms = mdl.get("materials"), mdl.get("materialsScript")
        if mj and ms and os.path.exists(mj) and os.path.exists(ms):
            run.log("%s: materials (import_sm2_materials.py)" % name)
            old = sys.argv
            sys.argv = [ms, mj, folder]
            try:
                runpy.run_path(ms, run_name="__main__")
            except Exception as e:
                res["errors"].append("materials: %r" % e)
            finally:
                sys.argv = old
            res["materialInstances"] = len([a for a in EAL.list_assets(folder, False, False) if a.split("/")[-1].startswith("MI_")])
            run.log("%s: %d material instances" % (name, res["materialInstances"]))
        elif mj:
            res["warnings"].append("materials sidecar or import_sm2_materials.py missing; slots keep the default material")
    clips = [c for c in mdl.get("clips") or [] if os.path.exists(c)]
    if clips:
        if isinstance(mesh, unreal.SkeletalMesh):
            run.log("%s: %d clip(s) onto %s" % (name, len(clips), mesh.skeleton.get_name()))
            try:
                out = anim_import_fn(scripts)(clips, mesh.skeleton, folder + "/Anims")
                paths = out[0] if isinstance(out, tuple) else out
                res["clips"] = [str(p) for p in (paths or [])]
                if len(res["clips"]) < len(clips):
                    res["errors"].append("anim import: %d of %d clip(s) failed on %s (wrong skeleton? see the Output Log)"
                                         % (len(clips) - len(res["clips"]), len(clips), mesh.skeleton.get_name()))
            except Exception as e:
                res["errors"].append("anim import: %r" % e)
        else:
            res["warnings"].append("%d clip(s) skipped: no skeletal mesh to put them on" % len(clips))
    manny = [c for c in mdl.get("manny") or [] if os.path.exists(c)]
    if manny:
        if not EAL.does_asset_exist(MANNY_SKEL):
            res["errors"].append("Anim Only (Manny): %s is not in this project (add the UE5 Mannequin / Third Person content)" % MANNY_SKEL)
        else:
            run.log("%s: %d Manny clip(s) onto SK_Mannequin" % (name, len(manny)))
            src = open(os.path.join(scripts, "ue_import_manny.py"), encoding="utf-8").read()
            g = {"__name__": "ue_import_manny"}; exec(src, g)
            try:
                paths = g["run"](manny, folder + "/Anims_Manny", MANNY_SKEL, "0", anim_import_fn(scripts))
                res["mannyClips"] = [str(p) for p in (paths or [])]
                if len(res["mannyClips"]) < len(manny):
                    res["errors"].append("manny import: %d of %d clip(s) failed (see the Output Log)" % (len(manny) - len(res["mannyClips"]), len(manny)))
            except Exception as e:
                res["errors"].append("manny import: %r" % e)
    pj = mdl.get("physics")
    if pj and os.path.exists(pj) and isinstance(mesh, unreal.SkeletalMesh):
        lib = getattr(unreal, "SM2PhysicsSetupLibrary", None)
        if lib is None:
            res["warnings"].append("SM2PhysicsSetup plugin not loaded: physics stays baked in the clips only")
        else:
            run.log("%s: physics (SM2PhysicsSetup)" % name)
            try:
                r = lib.setup_from_json(mesh, pj, folder)
                ok, rep = (r if isinstance(r, tuple) else (bool(r), ""))
                X = unreal.AnimPoseExtensions
                abpc = mesh.get_editor_property("post_process_anim_blueprint")
                has_centre = "CENTRE" in [str(b) for b in X.get_bone_names(X.get_reference_pose(mesh.skeleton))]
                if ok and abpc and has_centre and hasattr(lib, "tune_rigid_body"):
                    # mtl1-9 RigidBody sim-space defaults, same as ue_bulk.py (HEURISTIC, HANDOFF mtl1-9)
                    abp = unreal.load_asset(abpc.get_path_name().rsplit(".", 1)[0])
                    lib.tune_rigid_body(abp, 2, "CENTRE", 0.0, 0.0, 600.0, 2000.0, 10.0, 100.0)
                res["physics"] = str(rep)
                if not ok:
                    res["errors"].append("physics: " + str(rep))
            except Exception as e:
                res["errors"].append("physics: %r" % e)
    cs = mdl.get("colourScheme")
    if cs and os.path.exists(cs) and mesh is not None:
        # The scheme is baked into the exported albedo (mtl1-52); the layer params json has no UE consumer yet, so it is
        # only recorded on the mesh (metadata) for a later material pass.
        try:
            scheme = json.load(open(cs, encoding="utf-8-sig"))
            EAL.set_metadata_tag(mesh, "SM2.ColourScheme", str(scheme.get("scheme", "")))
            EAL.set_metadata_tag(mesh, "SM2.ColourSchemeJson", cs)
            res["colourScheme"] = scheme.get("scheme")
        except Exception as e:
            res["warnings"].append("colour scheme tag: %r" % e)
    # save only this folder (never Lightning_01 / other dirty packages)
    EAL.save_directory(folder, only_if_is_dirty=True, recursive=True)
    res["assets"] = len(EAL.list_assets(folder, True, False))
    run.log("%s: saved %d asset(s) in %s" % (name, res["assets"], folder))


HEADLESS_LEGACY = ("PNG", "TGA", "DDS", "JPG", "EXR", "BMP", "TIFF", "PSD", "HDR")


def main(argv=None):
    import unreal
    argv = argv if argv is not None else sys.argv
    mp = argv[1] if len(argv) > 1 else os.environ.get("SM2_FORGE_MANIFEST")
    manifest = json.load(open(mp, encoding="utf-8-sig"))
    run = Run(manifest)
    run.log("Forge Lens direct import: %d model(s)" % len(manifest.get("models") or []))
    if os.environ.get("SM2_FORGE_HEADLESS") == "1":
        # No Slate in the commandlet: Interchange texture imports assert CurrentApplication.IsValid() too, so the
        # whole headless run uses the legacy factories (the process exits afterwards; the cvars are not saved).
        for fmt in HEADLESS_LEGACY:
            unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.%s 0" % fmt)
        run.log("headless: legacy importers (Interchange off for %s)" % ", ".join(HEADLESS_LEGACY))
    for mdl in manifest.get("models") or []:
        res = {"name": mdl.get("name"), "errors": [], "warnings": []}
        run.result["models"].append(res)
        try:
            import_model(run, unreal, mdl, res)
        except Exception as e:
            res["errors"].append(str(e))
            run.log("%s: FAILED %s" % (mdl.get("name"), e))
            traceback.print_exc()
        for w in res["warnings"]:
            run.log("%s: warning: %s" % (mdl.get("name"), w))
        for er in res["errors"]:
            run.log("%s: error: %s" % (mdl.get("name"), er))
    run.result["ok"] = bool(run.result["models"]) and all(not r["errors"] for r in run.result["models"])
    run.result["seconds"] = round(time.time() - run.t0, 1)
    run.log("done: " + ("OK" if run.result["ok"] else "FAILED"))
    run.write_result()
    return run.result


if __name__ == "__main__":
    main()
