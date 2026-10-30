# ue_r97_floor.py (R97d, owner t263u): Map_Floor lighting -> clean minimal chapel setup. Removes the 24 R95c per-character
# point lights (key 800 / green rim 2500 / fill 150 + PPV manual EV bias 8 blew the Vortex out white in the viewport),
# keeps ONE warm key DirectionalLight + DaylightAmbient SkyLight, and sets the same manual exposure on the PPV and the captures.
# uerun.ps1 ue_r97_floor.py <dir_lux> <sky> <ev>      then  ue_r92_floor.py cap,<dir>,<tag>
import sys, unreal
F = "/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor"
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
dl, sky, ev = (float(x) for x in sys.argv[1:4])
if "Map_Floor" not in unreal.EditorLevelLibrary.get_editor_world().get_path_name(): les.load_level(F)
def pp(s):
    s.set_editor_property("override_auto_exposure_method", True); s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True); s.set_editor_property("auto_exposure_bias", ev)
    s.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); s.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
n = 0
for a in E.get_all_level_actors():
    if isinstance(a, unreal.PointLight): E.destroy_actor(a); n += 1
    elif isinstance(a, unreal.DirectionalLight):
        a.set_actor_rotation(unreal.Rotator(roll=0, pitch=-40, yaw=-60), False); c = a.light_component
        c.set_intensity(dl); c.set_light_color(unreal.LinearColor(1.0, 0.85, 0.7, 1))
    elif isinstance(a, unreal.SkyLight): a.light_component.set_intensity(sky); a.light_component.recapture_sky()
    elif isinstance(a, unreal.PostProcessVolume): s = a.get_editor_property("settings"); pp(s); a.set_editor_property("settings", s)
    elif isinstance(a, unreal.SceneCapture2D):
        cc = a.capture_component2d; s = cc.get_editor_property("post_process_settings"); pp(s); cc.set_editor_property("post_process_settings", s)
les.save_current_level(); print("R97FLOOR removed_points", n, "dir", dl, "sky", sky, "ev", ev)
