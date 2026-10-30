# ue_r97_orbswap.py (R97t): load hub Map_Floor, point R97_vortex_orb_ns at NS_vortex_orb_v6 (compiled template-based build), save level.
import unreal
LV = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); LV.load_level("/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor")
A = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
na = [x for x in A.get_all_level_actors() if x.get_actor_label() == "R97_vortex_orb_ns"][0]
na.niagara_component.set_asset(unreal.load_asset("/Game/ForgeLens/SM2/VFX/vortex_orb/NS_vortex_orb_v6"))
LV.save_current_level(); print("R97T swap", na.get_attach_parent_actor().get_actor_label() if na.get_attach_parent_actor() else None)
