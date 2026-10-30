# ue_r97_orbdark.py (R97t): dark empty test map + LS_orb_dark (spawnable camera + spawnable NiagaraActor NS_vortex_orb) for MRQ isolation.
import unreal, sys
E = unreal.EditorAssetLibrary; A = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
LV = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
M = "/Game/ForgeLens/SM2/Tests/r97_orbdark/Map_OrbDark"; D = "/Game/ForgeLens/SM2/VFX/vortex_orb"
NS = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] else D + "/NS_vortex_orb"
if not E.does_asset_exist(M): LV.new_level(M)
else: LV.load_level(M)
P = D + "/LS_orb_dark"
if E.does_asset_exist(P): E.delete_asset(P)
ls = unreal.AssetToolsHelpers.get_asset_tools().create_asset("LS_orb_dark", D, unreal.LevelSequence, unreal.LevelSequenceFactoryNew())
ls.set_display_rate(unreal.FrameRate(30, 1)); ls.set_playback_start(0); ls.set_playback_end(90)
cam = A.spawn_actor_from_class(unreal.CineCameraActor, unreal.Vector(-250, 0, 0), unreal.Rotator(0, 0, 0))
cc = cam.get_cine_camera_component(); cc.set_editor_property("current_focal_length", 35.0)
fs = cc.get_editor_property("focus_settings"); fs.focus_method = unreal.CameraFocusMethod.DISABLE; cc.set_editor_property("focus_settings", fs)
b = ls.add_spawnable_from_instance(cam); A.destroy_actor(cam)
na = A.spawn_actor_from_class(unreal.NiagaraActor, unreal.Vector(0, 0, 0), unreal.Rotator(0, 0, 0))
na.niagara_component.set_asset(unreal.load_asset(NS)); na.niagara_component.set_auto_activate(True)
ls.add_spawnable_from_instance(na); A.destroy_actor(na)
ct = ls.add_track(unreal.MovieSceneCameraCutTrack); s = ct.add_section(); s.set_range(0, 90)
bid = unreal.MovieSceneObjectBindingID(); bid.set_editor_property("guid", b.get_id()); s.set_camera_binding_id(bid)
E.save_loaded_asset(ls); LV.save_current_level(); print("R97T dark", P, NS)
