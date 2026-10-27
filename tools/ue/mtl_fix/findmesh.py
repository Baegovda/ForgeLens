import unreal
for p in unreal.EditorAssetLibrary.list_assets("/Game/SM2_Work", recursive=True, include_folder=False):
    n = p.split(".")[0]
    if (n.endswith("/cc_hellbrute") or n.endswith("/cc_sorcerer_exalted") or n.endswith("/cc_sorcerer")): print("MESH", n)
