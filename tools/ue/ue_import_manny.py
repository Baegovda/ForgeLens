# Import mannequin-ready anim FBX ({model}__{clip}_Manny.fbx from `export --anim-mannequin <json>`) onto SK_Mannequin.
# usage: sys.argv = ['x', <fbx | folder>, [dest=/Game/ForgeLens/SM2/Anims_Mannequin], [skeleton=SK_Mannequin path], [yaw=0]]
# Yaw 0: the clip is already in SK_Mannequin's frame (root identity, +Y front). The SM2 "auto" rule would give 180 here and
# turn the clip around. Re-imports reuse the yaw stored in the existing asset's import data, so it is reset first.
# No IK Retargeter: the FBX already has SK_Mannequin bone names, hierarchy and ref pose. Existing assets are kept as
# <name>_bakN before reimport.
import sys, os, unreal
SKEL = "/Game/Characters/Mannequins/Meshes/SK_Mannequin"
DEST = "/Game/ForgeLens/SM2/Anims_Mannequin"
def manny_files(p):
    if isinstance(p, (list, tuple)): return list(p)
    if os.path.isdir(p):
        return sorted(os.path.join(r, f) for r, _, fs in os.walk(p) for f in fs if f.lower().endswith("_manny.fbx"))
    return [p]
def backup(dest, files):
    EAL = unreal.EditorAssetLibrary
    for f in files:
        a = dest + "/" + os.path.splitext(os.path.basename(f))[0]
        if EAL.does_asset_exist(a):
            n = 1
            while EAL.does_asset_exist(a + "_bak%d" % n): n += 1
            EAL.duplicate_asset(a, a + "_bak%d" % n); EAL.save_asset(a + "_bak%d" % n, only_if_is_dirty=False)
def reset_yaw(dest, files, yaw):
    # replace_existing reimports keep the import_rotation stored on the asset; set the wanted yaw there first.
    for f in files:
        a = unreal.load_asset(dest + "/" + os.path.splitext(os.path.basename(f))[0]) if unreal.EditorAssetLibrary.does_asset_exist(dest + "/" + os.path.splitext(os.path.basename(f))[0]) else None
        d = a.get_editor_property("asset_import_data") if isinstance(a, unreal.AnimSequence) else None
        if d is not None and hasattr(d, "import_rotation"):
            d.set_editor_property("import_rotation", unreal.Rotator(roll=0, pitch=0, yaw=float(yaw)))
def run(src, dest=DEST, skel=SKEL, yaw="0", import_fn=None):
    files = manny_files(src)
    sk = unreal.load_asset(skel)
    sk = sk if isinstance(sk, unreal.Skeleton) else sk.skeleton
    backup(dest, files)
    if yaw != "auto": reset_yaw(dest, files, yaw)
    paths, y = import_fn(files, sk, dest, yaw, True)
    print("MANNY imported %d clips onto %s (yaw %g) -> %s" % (len(paths), sk.get_path_name(), y, dest))
    return paths
if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(sys.argv[0]))
    src = open(os.path.join(here, "ue_import_anim.py"), encoding="utf-8").read()
    a = src.index("def sm2_anim_import"); b = src.index("# --- end sm2_anim_import")
    g = {}; exec(src[a:b], g)
    args = sys.argv[1:] + [None] * 4
    run(args[0], args[1] or DEST, args[2] or SKEL, args[3] or "0", g["sm2_anim_import"])
