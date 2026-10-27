# MRQ warm-up for Chaos Cloth / RigidBody: cloth starts from the bind pose on the first rendered frame, so give the sim
# frames to settle before the first output frame. usage: <MoviePipelinePrimaryConfig asset> [engine_warmup=32] [render_warmup=8]
# Edits the config's Anti-Aliasing setting (adds it if missing): engine_warm_up_count, render_warm_up_count,
# use_camera_cut_for_warm_up. Saves a _bakN copy of the config first.
import sys, unreal
cfgp = sys.argv[1]; ew = int(sys.argv[2]) if len(sys.argv) > 2 else 32; rw = int(sys.argv[3]) if len(sys.argv) > 3 else 8
EAL = unreal.EditorAssetLibrary
n = 1
while EAL.does_asset_exist(cfgp + "_bak%d" % n): n += 1
EAL.duplicate_asset(cfgp, cfgp + "_bak%d" % n); EAL.save_asset(cfgp + "_bak%d" % n, only_if_is_dirty=False)
cfg = unreal.load_asset(cfgp)
aa = cfg.find_or_add_setting_by_class(unreal.MoviePipelineAntiAliasingSetting)
aa.set_editor_property("engine_warm_up_count", ew)
aa.set_editor_property("render_warm_up_count", rw)
aa.set_editor_property("use_camera_cut_for_warm_up", False)
EAL.save_asset(cfgp, only_if_is_dirty=False)
print("MRQWARM", cfgp, "engine", ew, "render", rw)
