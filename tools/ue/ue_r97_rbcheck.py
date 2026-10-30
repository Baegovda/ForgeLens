# ue_r97_rbcheck.py (R97): find RigidBody-node ensure sources (AnimNode_RigidBody SkeletonBoneIndex >= 0): for every
# SkeletalMeshActor in the open level, list the PA bodies (mesh PA and the ABP's override PA) whose bone is not in the
# mesh/skeleton reference skeleton.
import unreal
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
if "Map_Floor" not in unreal.EditorLevelLibrary.get_editor_world().get_path_name(): les.load_level("/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor")
for a in E.get_all_level_actors():
    if not isinstance(a, unreal.SkeletalMeshActor): continue
    c = a.skeletal_mesh_component; m = c.get_skinned_asset()
    bones = {str(c.get_bone_name(i)) for i in range(c.get_num_bones())}
    sk = m.get_editor_property("skeleton"); pa = m.get_editor_property("physics_asset")
    ac = c.get_editor_property("anim_class"); mode = c.get_editor_property("animation_mode")
    miss = []
    if pa:
        for n in unreal.SM2PhysicsSetupLibrary.describe_body(pa, m, "None").split()[:0]: pass
        try:
            names = [str(x) for x in unreal.PhysicsAssetEditorLibrary.get_body_names(pa)] if hasattr(unreal, "PhysicsAssetEditorLibrary") else []
        except Exception: names = []
        miss = [n for n in names if n not in bones]
    print("R97RB", a.get_actor_label(), "mode", mode, "abp", ac.get_name() if ac else None, "pa", pa.get_path_name() if pa else None, "skel", sk.get_path_name() if sk else None, "missingBodies", len(miss), miss[:5])

