# ue_r97_mapinfo.py: list the open level's lights, PPV exposure and SkeletalMeshActors (+ material emissive values).
import unreal
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem); w = unreal.EditorLevelLibrary.get_editor_world()
print("R97MAP world", w.get_path_name(), len(eas.get_all_level_actors()))
from collections import Counter
print("R97MAP classes", Counter(type(a).__name__ for a in eas.get_all_level_actors()).most_common())
for a in eas.get_all_level_actors():
    if isinstance(a, unreal.Light):
        c = a.get_component_by_class(unreal.LightComponent)
        print("R97MAP light", a.get_actor_label(), type(a).__name__, round(c.get_editor_property("intensity"),1), c.get_editor_property("light_color"), a.get_actor_location(), c.is_visible())
    elif isinstance(a, unreal.PostProcessVolume):
        s = a.get_editor_property("settings"); print("R97MAP ppv", a.get_actor_label(), s.get_editor_property("auto_exposure_method"), s.get_editor_property("override_auto_exposure_bias"), s.get_editor_property("auto_exposure_bias"), s.get_editor_property("auto_exposure_min_brightness"), s.get_editor_property("auto_exposure_max_brightness"), a.get_editor_property("unbound"))
    elif isinstance(a, (unreal.SkeletalMeshActor, unreal.SkyLight)):
        if isinstance(a, unreal.SkyLight): print("R97MAP sky", a.light_component.get_editor_property("intensity")); continue
        sk = a.skeletal_mesh_component; m = sk.get_skinned_asset()
        es = []
        for i in range(sk.get_num_materials()):
            mi = sk.get_material(i)
            try: es.append(round(mi.get_scalar_parameter_value("EmissiveStrength"),1))
            except Exception: es.append("?")
        print("R97MAP char", a.get_actor_label(), m.get_path_name() if m else None, "mat0", sk.get_material(0).get_path_name() if sk.get_material(0) else None, "emis", Counter(es).most_common(4))
