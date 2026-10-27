# AirDensity is kg/cm^3 in Chaos (1.225e-6); 1.225 explodes cloth.
# mtl1-13b cloth wind at runtime. modes (run while Simulate/PIE is active unless noted):
#   wind <name> <set> [scale=1]  : per cloth ChaosClothingInteractor.set_wind(drag, lift, airDensity, velocity) from the sidecar
#                                  (ue.chaosCloth.localWindCm/drag/lift; X-mirrored unless collision.space == "ueMesh").
#   src <x> <y> <z> [strength]   : editor world: spawn/update a WindDirectionalSource (tag SM2Wind) - Sequencer can key it.
#   srcclear                     : editor world: remove SM2Wind sources.
import sys, json, unreal
mode = sys.argv[1]
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"
ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem); eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
if mode == "wind":
    name, SET = sys.argv[2], sys.argv[3]; sc = float(sys.argv[4]) if len(sys.argv) > 4 else 0.05  # sidecar localWindCm x1 explodes cloth; 0.05 verified stable (mtl1-14c)
    pj = json.load(open(W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (SET, name, name, name), encoding="utf-8-sig"))
    w = ues.get_game_world(); n = 0
    for a in unreal.GameplayStatics.get_all_actors_with_tag(w, "SM2Phys"):
        if not a.actor_has_tag("on"): continue
        comp = a.skeletal_mesh_component; si = comp.get_clothing_simulation_interactor()
        print("WIND interactor", type(si).__name__ if si else None)
        if not si: continue
        try: names = [str(ca.get_name()) for ca in comp.get_skinned_asset().get_editor_property("mesh_clothing_assets")]
        except Exception as e: names = []; print("WIND names err", e)
        print("WIND clothing assets", names)
        for c in pj.get("cloths", []):
            cc = c.get("ue", {}).get("chaosCloth");
            if not cc: continue
            v = cc.get("localWindCm", [0, 0, 0]); mir = not (cc.get("windSpace") == "ueMesh" or cc.get("collision", {}).get("space") == "ueMesh")
            vel = unreal.Vector((-v[0] if mir else v[0]) * sc, v[1] * sc, v[2] * sc)
            ci = None
            for nm in [c["SimMeshName"]] + names:
                if not (c["SimMeshName"].lower() in nm.lower() or nm == c["SimMeshName"]): continue
                x = si.get_clothing_interactor(nm)
                if x is not None and hasattr(x, "set_wind"): ci = x; break
            if ci is not None and not hasattr(ci, "set_wind"):
                try: ci = unreal.ChaosClothingInteractor.cast(ci)
                except Exception as e: print("WIND cast err", e)
            if ci is None or not hasattr(ci, "set_wind"):
                print("WIND no interactor for", c["SimMeshName"], type(ci).__name__ if ci else None); continue
            drag = float(cc.get("drag", 0.035)); lift = float(cc.get("lift", 0.035))
            try: ci.set_wind(unreal.Vector2D(drag, drag), unreal.Vector2D(lift, lift), 1.225e-6, vel)
            except Exception as e:
                print("WIND v2 sig err", e); ci.set_wind(drag, lift, 1.225e-6, vel)
            n += 1
            print("WIND set %s drag=%.3f lift=%.3f vel=(%.0f %.0f %.0f)" % (c["SimMeshName"], drag, lift, vel.x, vel.y, vel.z))
    print("WIND applied", n)
elif mode == "probe":
    w = ues.get_game_world()
    for a in unreal.GameplayStatics.get_all_actors_with_tag(w, "SM2Phys"):
        comp = a.skeletal_mesh_component; si = comp.get_clothing_simulation_interactor()
        print("PROBE actor", a.get_name(), a.tags, type(si).__name__ if si else None)
        if not si: continue
        print("PROBE num", si.get_num_cloths(), si.get_num_dynamic_particles(), si.get_num_kinematic_particles())
        cas = comp.get_skinned_asset().get_editor_property("mesh_clothing_assets")
        for i, ca in enumerate(cas):
            nm = str(ca.get_name())
            for k in [nm, str(ca.get_fname()), nm.replace("_0", ""), str(i)]:
                x = si.get_clothing_interactor(k); print("PROBE", i, repr(k), type(x).__name__ if x else None)
elif mode == "src":
    x, y, z = [float(v) for v in sys.argv[2:5]]; st = float(sys.argv[5]) if len(sys.argv) > 5 else 1.0
    v = unreal.Vector(x, y, z); a = next((a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2Wind")), None)
    if not a: a = eas.spawn_actor_from_class(unreal.WindDirectionalSource, unreal.Vector(0, 0, 300)); a.tags = [unreal.Name("SM2Wind")]
    a.set_actor_rotation(v.rotation(), False); c = a.get_component_by_class(unreal.WindDirectionalSourceComponent)
    c.set_strength(st); c.set_speed(v.length() / 100.0); print("WIND source", a.get_name(), v, st)
elif mode == "srcclear":
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2Wind")]: eas.destroy_actor(a)
    print("WIND sources cleared")
