# Switch the editor to /Game/SM2_Work/Tests/PhysTestMap (create it once). Never saves the previous map (Lightning_01 stays unsaved).
import unreal
P = "/Game/SM2_Work/Tests/PhysTestMap"
U = unreal.EditorLoadingAndSavingUtils
ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
cur = ues.get_editor_world()
if cur and cur.get_path_name().startswith(P):
    print("PHYSMAP already", P)
elif unreal.EditorAssetLibrary.does_asset_exist(P):
    U.load_map(P); print("PHYSMAP loaded", P)
else:
    w = U.new_blank_map(False)
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    l = eas.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 500), unreal.Rotator(-45, 0, 30))
    sky = eas.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 300))
    sky.light_component.set_editor_property("real_time_capture", False)
    ok = U.save_map(w, P); print("PHYSMAP created", P, ok)
print("PHYSMAP world", ues.get_editor_world().get_path_name())
