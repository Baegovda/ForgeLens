# R91 probe: mesh paths of new imports + bone-name overlap with titus clips' skeleton (for shared fast clips).
import unreal
EAL=unreal.EditorAssetLibrary
def bones(m):
    s=m.get_editor_property("skeleton"); return s, set(str(n) for n in [s.get_editor_property("reference_skeleton")] ) if False else s
ar=unreal.AssetRegistryHelpers.get_asset_registry()
for n in ["cc_tech_priest","cc_chaplain","cc_commissar"]:
    f="/Game/SM2_Work/Tests/chars_phys/"+n
    for a in EAL.list_assets(f, recursive=True, include_folder=False):
        d=EAL.find_asset_data(a); c=str(d.asset_class_path.asset_name)
        if c in("SkeletalMesh","Skeleton","AnimSequence","PhysicsAsset"): print("R91",n,c,a)
t=unreal.load_asset("/Game/SM2_Work/Animations/cc_titus_assault/yaw180/cc_titus_assault__tacticus_unarmed_run_fwd_intercessor")
print("R91 titus skel", t.get_editor_property("skeleton").get_path_name())
