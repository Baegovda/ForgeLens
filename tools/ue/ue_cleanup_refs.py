# ue_cleanup_refs.py (R57): delete unused SM2_Work assets after a 0-reference check. Owner rule 10/30 11:13 (t110u).
# Usage: uerun.ps1 <this> <dir1>[,<dir2>...][,dry]
# Each dir's assets are deleted only if every referencer is inside the deletion set. Protected prefixes are refused.
# Prints R57DEL <path>\t<class> lines (copied to docs/handoff/CLEANUP_DELETED.tsv) and R57KEEP <path>\t<referencers>.
import sys, unreal
EAL = unreal.EditorAssetLibrary
PROT = ("/Game/SM2_Work/Original", "/Game/Work", "/Game/SM2_Work/Levels/Lightning_01")
toks = [t for a in sys.argv[1:] if not a.startswith("-") for t in a.split(",") if t]
dry = "dry" in toks; dirs = [t for t in toks if t != "dry"]
assets = []
unreal.AssetRegistryHelpers.get_asset_registry().scan_paths_synchronous([d for d in dirs], True)
for d in dirs:
    if d.startswith(PROT) or "Lightning_01" in d: print("R57REFUSE", d); continue
    assets += [a.rsplit(".", 1)[0] for a in EAL.list_assets(d, True, False)]
pkgs = set(assets)
refs = {a: [str(r) for r in EAL.find_package_referencers_for_asset(a, False)] for a in pkgs}
ok = set(pkgs)
changed = True
while changed:  # fixpoint: an asset referenced by a kept asset is kept too
    changed = False
    for a in list(ok):
        if any(r not in ok for r in refs[a]): ok.discard(a); changed = True
dele = [(a, []) for a in sorted(ok)]
keep = [(a, [r for r in refs[a] if r not in ok]) for a in sorted(pkgs - ok)]
for a, ext in keep: print("R57KEEP", a, ext[:5])
for a, _ in dele:
    cls = str(EAL.find_asset_data(a).asset_class_path.asset_name)
    ok = True if dry else EAL.delete_asset(a)
    print("R57DEL\t%s\t%s\t%s" % (a, cls, ok))
if not dry:
    for d in dirs:
        if EAL.does_directory_exist(d) and not EAL.list_assets(d, True, False): EAL.delete_directory(d)
print("R57SUM dirs", len(dirs), "assets", len(pkgs), "deleted", len(dele), "kept", len(keep), "dry", dry)
