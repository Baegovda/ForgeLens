import sys, unreal
a = [t for x in sys.argv[1:] for t in x.split(",") if t]
for p in a[1:]:
    names = sorted(set(str(d.asset_name) for d in unreal.AssetRegistryHelpers.get_asset_registry().get_assets_by_path(p, recursive=True) if str(d.asset_class_path.asset_name) == "Texture2D"))
    open(a[0] + "/" + p.strip("/").replace("/", "_") + ".txt", "w").write("\n".join(names)); print("R92 tex", p, len(names))
