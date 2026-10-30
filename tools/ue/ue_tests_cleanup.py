# Dry run (default) or delete scratch folders under /Game/SM2_Work/Tests. Never touches anything outside Tests.
import sys, unreal
spec = sys.argv[1]  # "dry:a,b,c" or "delete:a,b,c" (one arg; uerun -File passes one argv)
mode, _, flist = spec.partition(":")
folders = flist.split(",")
ar = unreal.AssetRegistryHelpers.get_asset_registry()
eal = unreal.EditorAssetLibrary
root = "/Game/SM2_Work/Tests/"
targets = [root + f for f in folders if f and ".." not in f and not f.startswith("/")]
dele = tuple(t + "/" for t in targets)
opts = unreal.AssetRegistryDependencyOptions(include_soft_package_references=True, include_hard_package_references=True, include_searchable_names=False, include_soft_management_references=False, include_hard_management_references=False)
for t in targets:
    if not eal.does_directory_exist(t):
        print("MISSING", t); continue
    pkgs = sorted({p.split(".")[0] for p in eal.list_assets(t, recursive=True, include_folder=False)})
    ext = set()
    for p in pkgs:
        for r in (ar.get_referencers(p, opts) or []):
            r = str(r)
            if not r.startswith(dele):
                ext.add(r)
    print(f"{t}: {len(pkgs)} packages, external referencers: {len(ext)} {sorted(ext)[:5]}")
    if mode == "delete":
        if ext:
            print("  SKIP (referenced from outside)")
            continue
        try:
            eal.delete_directory(t)
        except Exception as e:
            print("  (delete raised: %s)" % str(e).splitlines()[0][:160])
        unreal.SystemLibrary.collect_garbage()
        print("  deleted" if not eal.does_directory_exist(t) or not eal.list_assets(t, recursive=True) else "  DELETE INCOMPLETE")
