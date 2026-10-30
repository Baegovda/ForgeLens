# ue_r97_sun2.py (R97y): hub sun from game data. lighting__sun_light matrix row z (game Y-up) = (0.652,-0.709,0.268) -> UE (x,z,y) = (0.652,0.268,-0.709)
# -> pitch -45.2 yaw 22.3. Colour [A,R,G,B] order PROVEN by armory_pairing (bot_3: [255,165,255,247] x35 -> rgb 13.4,35,32.6) => sun 165,207,226.
# usage: uerun ue_r97_sun2.py <fillmode: none|preset> [fillInt]
import unreal, sys
fm = sys.argv[1] if len(sys.argv) > 1 else "preset"; fi = float(sys.argv[2]) if len(sys.argv) > 2 else 4.0
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); les.load_level("/Game/SM2_Work/Tests/r54_level/L_hub_battle_barge")
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
for a in E.get_all_level_actors():
    if not isinstance(a, unreal.DirectionalLight): continue
    c = a.light_component
    if a.get_actor_label() == "DirectionalLight":
        a.set_actor_rotation(unreal.Rotator(pitch=-45.2, yaw=22.3, roll=0), False); c.set_light_color(unreal.LinearColor(0.376, 0.624, 0.757, 1))
    else:  # fill: game env preset sun [255,236,196] (sky_env.py, PROVEN read) or off
        c.set_light_color(unreal.LinearColor(1.0, 0.838, 0.552, 1)); c.set_intensity(fi if fm == "preset" else 0.0)
    print("R97Y", a.get_actor_label(), a.get_actor_rotation(), c.intensity)
les.save_current_level()
