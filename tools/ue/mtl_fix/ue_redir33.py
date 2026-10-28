# delete the ObjectRedirector left at <path> by ue_chaos_cloth's rename (same as its fix_redirector); retries after saving it.
import sys, unreal
EAL = unreal.EditorAssetLibrary; p = sys.argv[1]
cls = str(EAL.find_asset_data(p).asset_class_path.asset_name) if EAL.does_asset_exist(p) else "-"
print("REDIR class", cls)
if cls == "ObjectRedirector":
    r = EAL.delete_asset(p); print("REDIR delete1", r)
    if not r:
        print("REDIR save", EAL.save_asset(p, only_if_is_dirty=False)); r = EAL.delete_asset(p); print("REDIR delete2", r)
print("REDIR exists", EAL.does_asset_exist(p))
