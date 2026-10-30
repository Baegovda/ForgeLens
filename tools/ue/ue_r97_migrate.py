# ue_r97_migrate.py (R97b, owner t260u): move our SM2 assets /Game/SM2_Work/<x> -> /Game/ForgeLens/SM2/<x> with editor rename
# (redirectors left at the old paths). /Game/SM2_Work/Original (protected) is not moved. A second-level folder whose target
# already holds an asset of the same name goes to <name>_work. Then fixes up only redirectors whose referencers are all
# inside /Game/ForgeLens or /Game/SM2_Work (never re-saves other content); the rest stay and are listed.
# uerun.ps1 ue_r97_migrate.py <plan|move|fixup> [subfolder]   (log: Saved/r97_migrate.tsv)
import unreal, sys, os
print("R97MIG argv", sys.argv)
mode = sys.argv[1]; only = sys.argv[2] if len(sys.argv) > 2 else None
OLD, NEW, KEEP = "/Game/SM2_Work", "/Game/ForgeLens/SM2", ("Original",)
AR = unreal.AssetRegistryHelpers.get_asset_registry(); EAL = unreal.EditorAssetLibrary; AT = unreal.AssetToolsHelpers.get_asset_tools()
log = os.path.join(unreal.Paths.project_saved_dir(), "r97_migrate.tsv")
SKIP_CLASSES = ("ObjectRedirector", "World")  # maps: the rename asks a modal "Continue with rename?" (auto-Cancel) -> stay in SM2_Work
def assets(path):
    return [a for a in AR.get_assets_by_path(path, recursive=True) if str(a.asset_class_path.asset_name) not in SKIP_CLASSES]
def plan():
    out = []
    for top in sorted({str(a.package_path).split("/")[3] for a in assets(OLD) if str(a.package_path).count("/") >= 3}):
        if top in KEEP or (only and top != only): continue
        subs = {}
        for a in assets(OLD + "/" + top):
            rel = str(a.package_name)[len(OLD) + 1:]; parts = rel.split("/")
            subs.setdefault(parts[1] if len(parts) > 2 else "", []).append((a, rel))
        for sub, lst in subs.items():
            clash = sub and any(EAL.does_asset_exist(NEW + "/" + r) for _, r in lst)
            for a, rel in lst:
                nrel = rel if not clash else rel.replace(top + "/" + sub + "/", top + "/" + sub + "_work/", 1)
                out.append((a, NEW + "/" + nrel))
    return out
if mode in ("plan", "move"):
    p = plan(); print("R97MIG plan", len(p), "clash->_work", len({d.rsplit('/',1)[0] for a, d in p if "_work/" in d}))
    for d in sorted({d.rsplit('/',1)[0] for a, d in p if "_work/" in d}): print("R97MIG work", d)
    if mode == "move":
        with open(log, "a", encoding="utf-8") as f:
            for i in range(0, len(p), 100):
                batch = p[i:i+100]
                data = [unreal.AssetRenameData(a.get_asset(), d.rsplit("/", 1)[0], d.rsplit("/", 1)[1]) for a, d in batch]
                ok = AT.rename_assets(data)
                for a, d in batch: f.write("%s\t%s\t%s\n" % (a.package_name, d, ok))
                EAL.save_directory(NEW, only_if_is_dirty=True, recursive=True)
                print("R97MIG batch", i, ok)
                if not ok: print("R97MIG STOP on failed batch"); break
        EAL.save_directory(OLD, only_if_is_dirty=True, recursive=True)
        print("R97MIG moved", len(assets(OLD)), "left in old (incl Original)")
if mode == "fixup":
    reds = [a for a in AR.get_assets_by_path(OLD, recursive=True) if str(a.asset_class_path.asset_name) == "ObjectRedirector"]
    ours, ext = [], []
    for r in reds:
        refs = [str(x) for x in AR.get_referencers(r.package_name, unreal.AssetRegistryDependencyOptions())]
        (ours if all(x.startswith(OLD) or x.startswith(NEW) for x in refs) else ext).append((r, refs))
    pk = sorted({x for _, refs in ours for x in refs if not x.startswith(OLD) or EAL.does_asset_exist(x) and str(unreal.EditorAssetLibrary.find_asset_data(x).asset_class_path.asset_name) != "ObjectRedirector"})
    saved = 0
    for x in pk:  # loading resolves the redirector; re-saving writes the new path (our roots only)
        try:
            if EAL.load_asset(x) and EAL.save_asset(x, only_if_is_dirty=False): saved += 1
        except Exception as e: print("R97MIG resave fail", x, e)
    print("R97MIG resaved", saved, "of", len(pk))
    with open(log.replace(".tsv", "_external.tsv"), "w", encoding="utf-8") as f:
        for r, refs in ext: f.write("%s\t%s\n" % (r.package_name, ";".join(x for x in refs if not (x.startswith(OLD) or x.startswith(NEW)))))
    left = [a for a in AR.get_assets_by_path(OLD, recursive=True) if str(a.asset_class_path.asset_name) == "ObjectRedirector"]
    norefs = [a for a in left if not [x for x in AR.get_referencers(a.package_name, unreal.AssetRegistryDependencyOptions()) if not str(x).startswith(str(a.package_name))]]
    print("R97MIG redirectors", len(reds), "ours", len(ours), "external_kept", len(ext), "unreferenced_now", len(norefs))
    if len(sys.argv) > 2 and sys.argv[2] == "delete":  # only after verification; only unreferenced redirectors
        n = sum(1 for a in norefs if EAL.delete_asset(str(a.package_name)))
        print("R97MIG deleted_redirectors", n)
