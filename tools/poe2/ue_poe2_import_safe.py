"""PoE2 M3: same as ue_poe2_import.py main() but saves only its own assets (no save_dirty_packages, which would also save
other workers' dirty maps) and never deletes (no cleanup()). usage: ue_poe2_import_safe.py <result.json> <dest root> <export dir>..."""
import json, os, sys
import unreal
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ue_poe2_import as imp

def main(argv):
    result, root, dirs = argv[0], argv[1].rstrip("/"), argv[2:]
    if not root.startswith("/Game/ForgeLens/POE"):
        raise SystemExit("dest must be under /Game/ForgeLens/POE")
    report = {}
    for d in dirs:
        name = os.path.splitext([f for f in os.listdir(d) if f.lower().endswith(".fbx")][0])[0]
        dest = f"{root}/{name}"
        tex = imp.textures(d, dest)
        assets = imp.fbx(d, dest)
        mesh = next((str(x).split(".")[0] for x in assets if str(x).split("/")[-1].split(".")[0] == name), f"{dest}/{name}")
        mats = imp.materials(d, dest, mesh)
        n_anims = imp.anims(d, dest, mesh)
        for a in unreal.EditorAssetLibrary.list_assets(dest, recursive=True):
            o = unreal.EditorAssetLibrary.load_asset(a)
            if o:
                unreal.EditorAssetLibrary.save_loaded_asset(o, True)  # only_if_is_dirty
        classes = {}
        for a in unreal.EditorAssetLibrary.list_assets(dest, recursive=True):
            c = str(unreal.EditorAssetLibrary.find_asset_data(a).asset_class_path.asset_name)
            classes[c] = classes.get(c, 0) + 1
        sk = unreal.load_asset(mesh)
        bones = []
        if isinstance(sk, unreal.SkeletalMesh):
            X = unreal.AnimPoseExtensions
            bones = [str(n) for n in X.get_bone_names(X.get_reference_pose(sk.get_editor_property("skeleton")))]
        report[name] = {"dest": dest, "mesh": mesh, "textures": len(tex), "material_slots": mats, "anims_imported": n_anims, "classes": classes,
                        "bones": len(bones), "phys_bones": [b for b in bones if b.lower().startswith("phys_")]}
    json.dump(report, open(result, "w"), indent=1)

if __name__ == "__main__":
    main(sys.argv[1:])
