# ue_r97_orbseq.py (R97s): LS_orb_cam in Map_Floor - camera at the cap_vortex SceneCapture pose, 0-90 frames, for MRQ (PIE ticks Niagara).
import unreal
P = "/Game/ForgeLens/SM2/VFX/vortex_orb/LS_orb_cam"; E = unreal.EditorAssetLibrary
if E.does_asset_exist(P): E.delete_asset(P)
ls = unreal.AssetToolsHelpers.get_asset_tools().create_asset("LS_orb_cam", "/Game/ForgeLens/SM2/VFX/vortex_orb", unreal.LevelSequence, unreal.LevelSequenceFactoryNew())
ls.set_display_rate(unreal.FrameRate(30, 1)); ls.set_playback_start(0); ls.set_playback_end(90)
A = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
cap = [x for x in A.get_all_level_actors() if isinstance(x, unreal.SceneCapture2D) and any(str(t) == "cap_vortex" for t in x.tags)][0]
cam = A.spawn_actor_from_class(unreal.CineCameraActor, cap.get_actor_location(), cap.get_actor_rotation())
cc = cam.get_cine_camera_component(); fb = cc.get_editor_property("filmback"); fb.sensor_width = 24.0; fb.sensor_height = 26.7; cc.set_editor_property("filmback", fb)
cc.set_editor_property("current_focal_length", 12.0 / __import__("math").tan(__import__("math").radians(cap.capture_component2d.get_editor_property("fov_angle") / 2)))
fs = cc.get_editor_property("focus_settings"); fs.focus_method = unreal.CameraFocusMethod.DISABLE; cc.set_editor_property("focus_settings", fs)
b = ls.add_spawnable_from_instance(cam); A.destroy_actor(cam)
ct = ls.add_track(unreal.MovieSceneCameraCutTrack); s = ct.add_section(); s.set_range(0, 90)
bid = unreal.MovieSceneObjectBindingID(); bid.set_editor_property("guid", b.get_id()); s.set_camera_binding_id(bid)
E.save_loaded_asset(ls); print("R97Q seq", P)
