# list SIM/REND material slots and clothing asset count of skeletal meshes: simslots.py <mesh path>...
import sys, unreal
for p in sys.argv[1:]:
    m = unreal.EditorAssetLibrary.load_asset(p)
    if not m: print("SIMS missing", p); continue
    s = [str(x.material_slot_name) for x in m.materials]
    print("SIMS", p, len(s), [x for x in s if "SIM" in x.upper() or "REND" in x.upper()], "clothAssets", len(m.get_editor_property("mesh_clothing_assets")))
