# SM2_Work layout unification: move-only via AssetTools.rename_assets (references update), then fix redirectors, save.
# usage: ue_reorg.py plan|apply
import sys, unreal
W = "/Game/SM2_Work"
MOVES = [  # (src folder, dst folder) ; whole folder recursive
    ("step3/_ab_off/cc_calgar", "Tests/step3_ab_off/cc_calgar"),
    ("step3/_ab_off/cc_swarm_hormagaunt", "Tests/step3_ab_off/cc_swarm_hormagaunt"),
    ("step3/_ab_off/cc_titus_assault", "Tests/step3_ab_off/cc_titus_assault"),
    ("step3/cc_calgar", "Characters/cc_calgar"),
    ("step3/cc_swarm_hormagaunt", "Characters/cc_swarm_hormagaunt"),
    ("step3/cc_titus_assault", "Characters/cc_titus_assault"),
    ("cc_sorcerer_exalted", "Characters/cc_sorcerer_exalted"),
    ("sani_fix/cc_vortex_beast/before", "Tests/sani_before/cc_vortex_beast"),
    ("sani_fix/cc_vortex_beast/after", "Animations/cc_vortex_beast"),
    ("sani_fix/cc_vortex_beast", "Characters/cc_vortex_beast"),
    ("sani_fix/door_mushroom/after", "Animations/door_mushroom"),
    ("sani_fix/door_mushroom", "Props/door_mushroom"),
    ("refrt_imp_rhino_01_v2", "Props/refrt_imp_rhino_01"),
    ("refrt_imp_rhino_01", "Tests/rhino_stacked/refrt_imp_rhino_01"),
    ("refrt_tyr_arch_column_01", "Props/refrt_tyr_arch_column_01"),
    ("wpn_bolter_heavy_01", "Props/wpn_bolter_heavy_01"),
    ("Anim_Bulk/cc_titus_assault", "Animations/cc_titus_assault/Bulk"),
    ("Anim_GUI/cc_titus_assault", "Animations/cc_titus_assault/GUI"),
    ("Original/cc_titus_assault/Anim_yaw180", "Animations/cc_titus_assault/yaw180"),
    ("Anim_Bulk/cc_sorcerer_exalted", "Animations/cc_sorcerer_exalted/Bulk"),
    ("Anim_Bulk/cc_swarm_hormagaunt", "Animations/cc_swarm_hormagaunt/Bulk"),
    ("v4/cc_titus_assault", "Tests/v4/cc_titus_assault"),
    ("cc_titus_assault", "Tests/pre_step3/cc_titus_assault"),
    ("cc_swarm_hormagaunt", "Tests/pre_step3/cc_swarm_hormagaunt"),
    ("_Tools", "Materials"),
]
reg = unreal.AssetRegistryHelpers.get_asset_registry()
mode = sys.argv[1] if len(sys.argv) > 1 else "plan"
taken = set()
data = []
for src, dst in MOVES:
    s = W + "/" + src; d = W + "/" + dst
    for a in reg.get_assets_by_path(s, recursive=True):
        pk = str(a.package_name)
        if pk in taken: continue
        taken.add(pk)
        rel = str(a.package_path)[len(s):]
        newp = d + rel
        if unreal.EditorAssetLibrary.does_asset_exist(newp + "/" + str(a.asset_name)):
            print("COLLIDE", pk, newp); continue
        data.append((pk, newp, str(a.asset_name)))
print("PLAN", len(data))
if mode == "apply":
    rd = [unreal.AssetRenameData(unreal.load_asset(pk), newp, n) for pk, newp, n in data]
    ok = unreal.AssetToolsHelpers.get_asset_tools().rename_assets(rd)
    print("RENAMED", ok)
    reds = [a for a in reg.get_assets_by_path(W, recursive=True) if str(a.asset_class_path.asset_name) == "ObjectRedirector"]
    print("REDIRECTORS", len(reds))
    if reds:
        objs = [unreal.load_asset(str(a.package_name)) for a in reds]
        unreal.AssetToolsHelpers.get_asset_tools().fix_up_referencers(objs)
    unreal.EditorAssetLibrary.save_directory(W, only_if_is_dirty=True, recursive=True)
    reds = [str(a.package_name) for a in reg.get_assets_by_path(W, recursive=True) if str(a.asset_class_path.asset_name) == "ObjectRedirector"]
    print("REDIRECTORS_LEFT", reds)
else:
    for pk, newp, n in data[:5]: print(pk, "->", newp)
