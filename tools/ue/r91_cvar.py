# R91: set the SM2 rest-fit CVars (sm2.RestFitFloor / sm2.RestFitDeepFrac) before prep. usage: r91_cvar.py <floor> <deepfrac>
import sys, unreal
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
unreal.SystemLibrary.execute_console_command(w, "sm2.RestFitFloor %s" % sys.argv[1])
unreal.SystemLibrary.execute_console_command(w, "sm2.RestFitDeepFrac %s" % sys.argv[2])
print("R91CVAR", sys.argv[1], sys.argv[2])
