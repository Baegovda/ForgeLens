# drop the unsaved /Game/ForgeLens/SM2/Tests/diag_unsaved/* duplicates (in-memory only) so quit does not prompt
import unreal, os
EAL = unreal.EditorAssetLibrary
root = "/Game/ForgeLens/SM2/Tests/diag_unsaved"
disk = unreal.Paths.project_content_dir() + "SM2_Work/Tests/diag_unsaved"
print("ONDISK", os.path.isdir(disk) and os.listdir(disk))
for a in EAL.list_assets(root, True, False):
    print("DROP", a, EAL.delete_asset(a))
