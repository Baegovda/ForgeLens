# R91: reflection captures for the r54 scratch level: one SphereReflectionCapture per busiest 4000 cm cell (max 8), tag R91Refl; "clear" removes them.
import unreal, sys, os, collections
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
assert "/Game/SM2_Work/Tests/r54_level/" in world.get_path_name(), world.get_path_name()
for a in eas.get_all_level_actors():
    if a.actor_has_tag("R91Refl"): eas.destroy_actor(a)
if sys.argv[1] == "add":
    d = sys.argv[2]
    rows = [l.split("\t") for l in open(os.path.join(d, "instances_ue.tsv"), encoding="utf-8").read().splitlines()[1:] if l]
    L = [[float(x) for x in m.split()[12:15]] for _, _, m in rows]
    cells = collections.Counter((int(x // 4000), int(y // 4000), int(z // 4000)) for x, y, z in L).most_common(8)
    for (cx, cy, cz), n in cells:
        a = eas.spawn_actor_from_class(unreal.SphereReflectionCapture, unreal.Vector((cx + .5) * 4000, (cy + .5) * 4000, (cz + .5) * 4000 + 300), unreal.Rotator())
        a.tags = [unreal.Name("R91Refl")]; a.capture_component.set_editor_property("influence_radius", 4500.0)
    unreal.SystemLibrary.execute_console_command(world, "BuildReflectionCaptures")
    print("R91REFL added", len(cells))
else:
    print("R91REFL cleared")
