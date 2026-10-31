# ue_r98_floorev.py: set Map_Floor main PPV manual exposure bias (R98a found it at -5)
import unreal, sys
ev=float(sys.argv[1]); A=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
for a in A.get_all_level_actors():
    if isinstance(a,unreal.PostProcessVolume) and "R97X" not in [str(t) for t in a.tags]:
        s=a.settings; s.override_auto_exposure_bias=True; s.auto_exposure_bias=ev; a.settings=s; print("R98EV",a.get_actor_label(),ev)
unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
