# ue_r97_firemrq.py (R97m): render frames of LS_arifle_bolt_rifle with Movie Render Queue (PIE executor) to PNG.
# usage: uerun ue_r97_firemrq.py <outdir>,<start>,<end>[,exposureOffset]   (async: poll <outdir> for PNGs)
import sys, unreal
a = sys.argv[1:]; a = a[0].split(",") if len(a) == 1 else a
out, s, e = a[0], int(a[1]), int(a[2]); ev = float(a[3]) if len(a) > 3 else 0.0
LS = a[5] if len(a) > 5 and a[5] else "/Game/ForgeLens/SM2/Sequences/BoltRifleFire_r97/LS_arifle_bolt_rifle"
sub = unreal.get_editor_subsystem(unreal.MoviePipelineQueueSubsystem)
q = sub.get_queue(); q.delete_all_jobs()
j = q.allocate_new_job(unreal.MoviePipelineExecutorJob)
j.sequence = unreal.SoftObjectPath(LS)
j.map = unreal.SoftObjectPath(unreal.EditorLevelLibrary.get_editor_world().get_path_name())
j.job_name = "R97M_fire"
c = j.get_configuration()
o = c.find_or_add_setting_by_class(unreal.MoviePipelineOutputSetting)
o.output_directory = unreal.DirectoryPath(out); o.file_name_format = "fire_{frame_number}"
o.use_custom_playback_range = True; o.custom_start_frame = s; o.custom_end_frame = e
o.output_resolution = unreal.IntPoint(1280, 720)
c.find_or_add_setting_by_class(unreal.MoviePipelineDeferredPassBase)
c.find_or_add_setting_by_class(unreal.MoviePipelineImageSequenceOutput_PNG)
aa = c.find_or_add_setting_by_class(unreal.MoviePipelineAntiAliasingSetting); aa.engine_warm_up_count = 32; aa.use_camera_cut_for_warm_up = False
cvs = c.find_or_add_setting_by_class(unreal.MoviePipelineConsoleVariableSetting)
for kv in (a[4].split(";") if len(a) > 4 else []):
    k, v = kv.split("="); cvs.add_or_update_console_variable(k, float(v))
if ev:
    cv = c.find_or_add_setting_by_class(unreal.MoviePipelineConsoleVariableSetting); cv.add_or_update_console_variable("r.ExposureOffset", ev)
ex = sub.render_queue_with_executor(unreal.MoviePipelinePIEExecutor)
print("R97M MRQ started", out, s, e, ex)



