# ue_r97_floorchap.py (R97v): in our Map_Floor, swap any actor using the old chaplain test meshes (chars_cloth2/chars_phys, user skeleton) to
# /Game/ForgeLens/SM2/Characters/cc_chaplain/cc_chaplain (own skeleton); animation asset cleared if it is on the user skeleton. Saves Map_Floor only.
import unreal
LV = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); LV.load_level("/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor")
A = unreal.get_editor_subsystem(unreal.EditorActorSubsystem); new = unreal.load_asset("/Game/ForgeLens/SM2/Characters/cc_chaplain/cc_chaplain")
print("R97V new mesh skel", new.get_editor_property("skeleton").get_path_name(), "pa", new.get_editor_property("physics_asset").get_path_name() if new.get_editor_property("physics_asset") else None)
n = 0
for a in A.get_all_level_actors():
    for c in a.get_components_by_class(unreal.SkeletalMeshComponent):
        m = c.get_editor_property("skeletal_mesh_asset") if hasattr(c, "skeletal_mesh_asset") else c.skeletal_mesh
        if m and ("chars_cloth2/cc_chaplain" in m.get_path_name() or "chars_phys/cc_chaplain" in m.get_path_name()):
            mode = c.get_editor_property("animation_mode"); ad = c.get_editor_property("animation_data"); anim = ad.get_editor_property("anim_to_play")
            print("R97V found", a.get_actor_label(), m.get_path_name(), mode, anim.get_path_name() if anim else None)
            c.set_skeletal_mesh_asset(new) if hasattr(c, "set_skeletal_mesh_asset") else c.set_skeletal_mesh(new)
            if anim and not anim.get_editor_property("skeleton").get_path_name().startswith("/Game/ForgeLens/"):
                ad.set_editor_property("anim_to_play", None); c.set_editor_property("animation_data", ad)
            n += 1
if n: LV.save_current_level()
print("R97V swapped", n)
