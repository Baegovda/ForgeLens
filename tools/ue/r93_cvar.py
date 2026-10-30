# R93: set rest-fit CVars before prep. usage: r93_cvar.py <floor>,<deepfrac>,<shallowfrac>
import sys, unreal
a = sys.argv[1].split(",")
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
for k, v in zip(("sm2.RestFitFloor", "sm2.RestFitDeepFrac", "sm2.RestFitShallowFrac"), a):
    unreal.SystemLibrary.execute_console_command(w, "%s %s" % (k, v))
print("R93CVAR", *a)
