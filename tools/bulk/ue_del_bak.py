# Delete only /Game/ForgeLens/SM2/Tests/bulk/*_bakN folders (bulk-validation backups).
import unreal, re
root = "/Game/ForgeLens/SM2/Tests/bulk"
eal = unreal.EditorAssetLibrary
subs = unreal.AssetRegistryHelpers.get_asset_registry().get_sub_paths(root, False)
done = []
for p in subs:
    name = p.rsplit("/", 1)[-1]
    if p.startswith(root + "/") and re.search(r"_bak\d+$", name):
        ok = eal.delete_directory(p)
        done.append((name, ok))
print("DELETED", len(done), [d for d in done if not d[1]])
