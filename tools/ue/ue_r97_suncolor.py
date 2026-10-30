# ue_r97_suncolor.py (R97u): set the hub level DirectionalLights to the game sun ColorCtrl (lighting__sun_light [255,165,207,226] -> A,R,G,B = 165,207,226), save level (ours, /Game/SM2_Work/Tests/r54_level).
import unreal
les=unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); les.load_level("/Game/SM2_Work/Tests/r54_level/L_hub_battle_barge")
E=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
for a in E.get_all_level_actors():
    if isinstance(a,unreal.DirectionalLight):
        a.light_component.set_light_color(unreal.Color(r=165,g=207,b=226,a=255).to_linear() if hasattr(unreal.Color,"to_linear") else unreal.LinearColor(0.376,0.624,0.757,1))
        print("R97U set",a.get_actor_label(),a.light_component.get_editor_property("light_color"))
les.save_current_level()
