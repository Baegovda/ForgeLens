# ue_fix_split_skeletons.py [scan|fix] [root=/Game/SM2_Work] [only=<folder substring>]
# Split skeleton (ledger R35) — 애니메이션과 메시가 서로 다른 스켈레톤을 쓰는 문제.
# Rule: an AnimSequence must use the Skeleton of the SkeletalMesh it sits next to (same folder, or the mesh named
# like the clip prefix "{mesh}__{clip}"). A second "{mesh}_Skeleton" made by an import/move that did not pass the
# mesh's skeleton is a split; UE then refuses to play the anim on the mesh (no compatible skeleton).
# scan: print SPLIT rows only. fix: for each split whose bone-name list equals the mesh skeleton's (same names,
# same order = same tree for our FBX writer), move the anim aside to "<anim>_bakN" (N = next free, R03), then
# import its source FBX onto the mesh skeleton at the same path (legacy FBX, same options as ue_import_anim);
# if the source FBX is gone, export the clip with UE's FBX exporter and re-import that (yaw 0); if that fails, fall back to registering the mesh skeleton as compatible on the anim skeleton
# (non-destructive). Never touches /Game/SM2_Work/Original/* or /Game/Work/* (R05), never deletes (R03).
import os, sys

PROTECTED = ("/game/sm2_work/original/", "/game/work/")


def is_protected(path):
    p = path.lower().rstrip("/") + "/"
    return any(p.startswith(x) or p == x for x in PROTECTED) or "lightning_01" in p


def folder_of(path):
    return path.rsplit("/", 1)[0]


def owner_mesh(anim_path, meshes):
    """Sibling mesh for an anim: the SkeletalMesh in the same folder named like the clip prefix, else the only one."""
    folder, name = anim_path.rsplit("/", 1)
    here = [m for m in meshes if folder_of(m) == folder]
    prefix = name.split("__", 1)[0]
    named = [m for m in here if m.rsplit("/", 1)[1] == prefix]
    if named:
        return named[0]
    return here[0] if len(here) == 1 else None


def find_splits(anims, meshes):
    """anims: {anim_path: skeleton_path}; meshes: {mesh_path: skeleton_path}. Returns [(anim, anim_skel, mesh, mesh_skel)]."""
    out = []
    for a, askel in sorted(anims.items()):
        if is_protected(a):
            continue
        m = owner_mesh(a, list(meshes))
        if m and meshes[m] and askel != meshes[m]:
            out.append((a, askel, m, meshes[m]))
    return out


def next_backup(exists, path):
    n = 1
    while exists("%s_bak%d" % (path, n)):
        n += 1
    return "%s_bak%d" % (path, n)


def same_tree(a, b):
    return list(a) == list(b)


def load_importer():
    # The legacy-FBX anim importer block is shared (copied) between ue_import_anim.py / ue_sani_fix.py; reuse it.
    src = open(os.path.join(os.path.dirname(os.path.abspath(sys.argv[0])), "ue_import_anim.py"), encoding="utf-8").read()
    a = src.index("# --- sm2_anim_import"); b = src.index("# --- end sm2_anim_import")
    g = {}; exec("import sys, unreal\n" + src[a:b], g)
    return g["sm2_anim_import"]


def main():
    import unreal
    sys.path.insert(0, os.path.dirname(os.path.abspath(sys.argv[0])))
    mode = sys.argv[1] if len(sys.argv) > 1 else "scan"
    root = sys.argv[2] if len(sys.argv) > 2 else "/Game/SM2_Work"
    only = sys.argv[3] if len(sys.argv) > 3 else ""
    EAL = unreal.EditorAssetLibrary; X = unreal.AnimPoseExtensions
    reg = unreal.AssetRegistryHelpers.get_asset_registry()
    anims, meshes = {}, {}
    for ad in reg.get_assets_by_path(root, recursive=True):
        c = str(ad.asset_class_path.asset_name); p = str(ad.package_name)
        if c not in ("AnimSequence", "SkeletalMesh") or "_bak" in p.rsplit("/", 1)[1]:
            continue
        o = ad.get_asset(); s = o.get_editor_property("skeleton") if o else None
        sp = s.get_path_name().split(".")[0] if s else ""
        (anims if c == "AnimSequence" else meshes)[p] = sp
    splits = [s for s in find_splits(anims, meshes) if only in s[0]]
    print("SCAN anims %d meshes %d splits %d" % (len(anims), len(meshes), len(splits)))
    for a, ask, m, msk in splits:
        print("SPLIT", a, ask, "| mesh", m, msk)
    if mode != "fix":
        return
    def bones(sk):
        return [str(b) for b in X.get_bone_names(unreal.AnimPoseExtensions.get_reference_pose(sk))]
    for a, ask, m, msk in splits:
        anim = unreal.load_asset(a); mskel = unreal.load_asset(msk); askel = unreal.load_asset(ask)
        if not same_tree(bones(askel), bones(mskel)):
            print("SKIP bones differ", a); continue
        src = ""
        try:
            fs = anim.get_editor_property("asset_import_data").extract_filenames()
            src = fs[0] if fs else ""
        except Exception:
            pass
        yaw = 0.0 if "split_skel" in src.replace("\\", "/") else "auto"  # our own UE round-trip FBX is in UE axes
        if not (src and os.path.exists(src)):
            # Source FBX gone (export folders get cleaned): round-trip the clip through UE's own FBX exporter. That FBX
            # is already in Unreal axes, so it re-imports with yaw 0 onto the mesh skeleton (same bone names).
            src = os.path.join(os.environ.get("USERPROFILE", "."), "Desktop", "SM2_Work", "Backup", "split_skel", a.rsplit("/", 1)[1] + ".fbx")
            os.makedirs(os.path.dirname(src), exist_ok=True)
            # The FBX anim exporter needs a preview mesh ("Cannot export animation sequence ... preview mesh is not
            # set") and drops one whose skeleton is not compatible, so mark it compatible in memory only (not saved).
            keep = list(askel.get_editor_property("compatible_skeletons") or [])
            askel.set_editor_property("compatible_skeletons", keep + [mskel])
            try: anim.set_preview_skeletal_mesh(unreal.load_asset(m))
            except Exception as e: print("WARN preview mesh", e)
            t = unreal.AssetExportTask(); t.object = anim; t.filename = src; t.automated = True; t.replace_identical = True
            t.prompt = False; t.exporter = unreal.AnimSequenceExporterFBX()
            print("EXPORT", src, unreal.Exporter.run_asset_export_task(t), os.path.exists(src)); yaw = 0.0
            askel.set_editor_property("compatible_skeletons", keep)
        if src and os.path.exists(src):
            # A re-import over the existing asset keeps its old skeleton (seen 10/29: SPLITSKEL after replace), so the
            # old clip is moved aside as the backup "<anim>_bakN" (rename, nothing deleted) and imported fresh here.
            bak = next_backup(EAL.does_asset_exist, a)
            if not EAL.rename_asset(a, bak):
                print("FAIL backup rename", a); continue
            print("BACKUP", a, "->", bak)
            sm2_anim_import = load_importer()
            paths, y = sm2_anim_import(src, mskel, folder_of(a), yaw)
            now = unreal.load_asset(a) if EAL.does_asset_exist(a) else None
            ok = now is not None and now.get_editor_property("skeleton") == mskel
            if ok:
                old = unreal.load_asset(bak)
                for prop in ("enable_root_motion", "root_motion_root_lock", "rate_scale"):
                    try: now.set_editor_property(prop, old.get_editor_property(prop))
                    except Exception: pass
                EAL.save_loaded_asset(now)
                print("REIMPORT", a, "on", msk, "from", src, "yaw", y); continue
            print("FAIL reimport", a, src)
            if now is None: EAL.rename_asset(bak, a)
        # Fallback: mark compatible (USkeleton.CompatibleSkeletons) both ways, keep the anim untouched.
        for s1, s2 in ((askel, mskel), (mskel, askel)):
            lst = list(s1.get_editor_property("compatible_skeletons") or [])
            if s2 not in lst:
                sp = s1.get_path_name().split(".")[0]; sb = next_backup(EAL.does_asset_exist, sp)
                if not EAL.duplicate_asset(sp, sb):
                    print("FAIL backup", sp); continue
                EAL.save_asset(sb, only_if_is_dirty=False); print("BACKUP", sp, "->", sb)
                lst.append(s2); s1.set_editor_property("compatible_skeletons", lst)
                EAL.save_loaded_asset(s1)
        print("COMPAT", ask, "<->", msk)


if __name__ == "__main__":
    main()
