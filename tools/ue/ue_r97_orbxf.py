# ue_r97_orbxf.py (R97h): relative transform of R97_vortex_orb on bone DISK. uerun.ps1 ue_r97_orbxf.py <roll> <pitch> <yaw> <scale>
import sys, unreal
r, p, y, s = (float(x) for x in sys.argv[1:5])
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
o = [a for a in E.get_all_level_actors() if a.get_actor_label() == "R97_vortex_orb"][0]
o.root_component.set_relative_rotation(unreal.Rotator(roll=r, pitch=p, yaw=y), False, False)
o.root_component.set_relative_scale3d(unreal.Vector(s, s, s))
unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level(); print("R97ORBXF", r, p, y, s)
