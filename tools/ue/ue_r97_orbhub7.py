# ue_r97_orbhub7.py (R97x): hub Map_Floor - orb actor -> NS_vortex_orb_v7, pink orb point light on DISK (game orb lights the body), unbound PPV bloom (tagged R97X). Saves Map_Floor only.
import unreal, sys
LI = float(sys.argv[1]) if len(sys.argv) > 1 else 8000.0; BI = float(sys.argv[2]) if len(sys.argv) > 2 else 3.0
LV = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); LV.load_level("/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor")
A = unreal.get_editor_subsystem(unreal.EditorActorSubsystem); acts = A.get_all_level_actors()
for a in acts:
    if "R97X" in [str(t) for t in a.tags]: A.destroy_actor(a)
na = [x for x in acts if x.get_actor_label() == "R97_vortex_orb_ns"][0]; na.niagara_component.set_asset(unreal.load_asset("/Game/ForgeLens/SM2/VFX/vortex_orb/NS_vortex_orb_v7"))
vx = na.get_attach_parent_actor(); loc = na.get_actor_location()
pl = A.spawn_actor_from_class(unreal.PointLight, loc); pl.tags = ["R97X"]; pl.set_actor_label("R97X_orb_light")
c = pl.point_light_component; c.set_intensity(LI); c.set_light_color(unreal.LinearColor(1.0, 0.45, 0.9, 1)); c.set_attenuation_radius(220.0); c.set_editor_property("cast_shadows", False)
pl.attach_to_actor(vx, "DISK", unreal.AttachmentRule.KEEP_WORLD, unreal.AttachmentRule.KEEP_WORLD, unreal.AttachmentRule.KEEP_WORLD, False)
pp = A.spawn_actor_from_class(unreal.PostProcessVolume, loc); pp.tags = ["R97X"]; pp.unbound = True; s = pp.settings
s.override_bloom_intensity = True; s.bloom_intensity = BI; s.override_bloom_threshold = True; s.bloom_threshold = 1.0; pp.settings = s
LV.save_current_level(); print("R97X hub7", LI, BI, vx.get_actor_label())

