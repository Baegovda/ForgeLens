# mtl1-16 MRQ keyed-wind test scene: mrqwind.py build <mesh path> | info
# build: new map /Game/ForgeLens/SM2/Tests/mrq16/L_wind16 (sky + sun, SkeletalMeshActor with the ChaosCloth mesh, CineCamera, a
# WindDirectionalSource whose Speed/Strength are KEYED in LS_wind16: calm 0-40, ramp 40-60, strong 60-150 @30 fps), and an
# MRQ primary config CFG_wind16 (PNG 960x540, engine warm-up 32). Restores the previously open map afterwards (not saved).
# Render with mrqrender.ps1 (full object paths; a bare package path leaves {sequence_name} empty).
import sys, unreal
F = "/Game/ForgeLens/SM2/Tests/mrq16"; EAL = unreal.EditorAssetLibrary; AT = unreal.AssetToolsHelpers.get_asset_tools()
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
mode = sys.argv[1]
if mode == "info":
    w = ues.get_editor_world(); print("MRQW world", w.get_path_name(), "dirty", [p.get_name() for p in unreal.EditorLoadingAndSavingUtils.get_dirty_map_packages()])
    raise SystemExit
mesh = sys.argv[2]
prev = ues.get_editor_world().get_path_name().split(".")[0]
reuse = prev == F + "/L_wind16"
if reuse:
    for x in eas.get_all_level_actors(): eas.destroy_actor(x)
    prev = "/Game/SM2_Work/Tests/PhysTestMap"
for a in (("LS_wind16", "CFG_wind16") if reuse else ("L_wind16", "LS_wind16", "CFG_wind16")):
    if EAL.does_asset_exist(F + "/" + a):
        n = 1
        while EAL.does_asset_exist(F + "/%s_bak%d" % (a, n)): n += 1
        EAL.rename_asset(F + "/" + a, F + "/%s_bak%d" % (a, n))
ok = reuse or les.new_level(F + "/L_wind16"); print("MRQW new_level", ok)
sp = eas.spawn_actor_from_class
sun = sp(unreal.DirectionalLight, unreal.Vector(0, 0, 500), unreal.Rotator(0, -40, 30))
sun.light_component.set_mobility(unreal.ComponentMobility.MOVABLE); sun.light_component.set_editor_property("atmosphere_sun_light", True)
sp(unreal.SkyAtmosphere, unreal.Vector(0, 0, 0))
sky = sp(unreal.SkyLight, unreal.Vector(0, 0, 300)); sky.light_component.set_mobility(unreal.ComponentMobility.MOVABLE); sky.light_component.set_editor_property("real_time_capture", True)
floor = sp(unreal.StaticMeshActor, unreal.Vector(0, 0, 0)); floor.static_mesh_component.set_static_mesh(unreal.load_asset("/Engine/BasicShapes/Plane")); floor.set_actor_scale3d(unreal.Vector(20, 20, 1))
ch = sp(unreal.SkeletalMeshActor, unreal.Vector(0, 0, 0)); ch.skeletal_mesh_component.set_skeletal_mesh_asset(unreal.load_asset(mesh))
cam = sp(unreal.CineCameraActor, unreal.Vector(-520, -260, 170), unreal.MathLibrary.find_look_at_rotation(unreal.Vector(-520, -260, 170), unreal.Vector(0, 0, 130)))
wind = sp(unreal.WindDirectionalSource, unreal.Vector(0, 0, 200), unreal.MathLibrary.make_rot_from_x(unreal.Vector(0, 1, 0)))
wc = wind.get_component_by_class(unreal.WindDirectionalSourceComponent); wc.set_speed(0.0); wc.set_strength(0.0); wind.tags = [unreal.Name("SM2Wind")]
seq = AT.create_asset("LS_wind16", F, unreal.LevelSequence, unreal.LevelSequenceFactoryNew())
seq.set_display_rate(unreal.FrameRate(30, 1)); seq.set_playback_start(0); seq.set_playback_end(150)
MSE = unreal.MovieSceneSequenceExtensions
cb = MSE.add_possessable(seq, cam)
cut = MSE.add_track(seq, unreal.MovieSceneCameraCutTrack) if hasattr(MSE, "add_track") else seq.add_master_track(unreal.MovieSceneCameraCutTrack)
cs = cut.add_section(); cs.set_range(0, 150)
try: bid = MSE.get_portable_binding_id(seq, seq, cb)
except Exception: bid = MSE.make_binding_id(seq, cb, unreal.MovieSceneObjectBindingSpace.LOCAL)
cs.set_camera_binding_id(bid)
wb = MSE.add_possessable(seq, wc)
print("MRQW wind binding", wb.get_display_name(), "parent", wb.get_parent().get_display_name() if hasattr(wb, "get_parent") else "?")
keys = {"Speed": [(0, 0.0), (40, 0.0), (60, 8.0), (150, 8.0)], "Strength": [(0, 0.0), (40, 0.0), (60, 1.0), (150, 1.0)]}
for prop, ks in keys.items():
    t = wb.add_track(unreal.MovieSceneFloatTrack); t.set_property_name_and_path(prop, prop)
    s = t.add_section(); s.set_range(0, 150); ch0 = s.get_all_channels()[0]
    for f, v in ks: ch0.add_key(unreal.FrameNumber(f), v)
    print("MRQW keyed", prop, ks)
cfg = AT.create_asset("CFG_wind16", F, unreal.MoviePipelinePrimaryConfig, None)
if cfg is None: print("MRQW cfg factory missing")
else:
    out = cfg.find_or_add_setting_by_class(unreal.MoviePipelineOutputSetting)
    out.set_editor_property("output_resolution", unreal.IntPoint(960, 540))
    out.set_editor_property("output_directory", unreal.DirectoryPath(r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Captures/mrq16"))
    out.set_editor_property("file_name_format", "{sequence_name}.{frame_number}")
    cfg.find_or_add_setting_by_class(unreal.MoviePipelineDeferredPassBase)
    cfg.find_or_add_setting_by_class(unreal.MoviePipelineImageSequenceOutput_PNG)
    aa = cfg.find_or_add_setting_by_class(unreal.MoviePipelineAntiAliasingSetting)
    aa.set_editor_property("engine_warm_up_count", 32); aa.set_editor_property("render_warm_up_count", 8); aa.set_editor_property("use_camera_cut_for_warm_up", False)
    EAL.save_asset(F + "/CFG_wind16", only_if_is_dirty=False)
EAL.save_asset(F + "/LS_wind16", only_if_is_dirty=False)
les.save_current_level()
print("MRQW saved", F, "prev", prev)
if prev and not prev.startswith(F): print("MRQW reload prev", les.load_level(prev))
