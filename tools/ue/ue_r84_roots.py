# R84 (owner t200u): verify per-game roots /Game/ForgeLens/<GAMECODE>/... in the open editor (EngineWire via uerun.ps1).
# usage: ue_r84_roots.py count <out.json> <folder>...      -> asset counts per folder (recursive), to compare before/after
#        ue_r84_roots.py check <out.json> <mesh asset>...  -> load each mesh, resolve every material slot + its textures
# Read-only: loads assets, saves nothing.
import unreal, sys, json
mode, out = sys.argv[1], sys.argv[2]
EAL = unreal.EditorAssetLibrary
res = {"mode": mode, "items": []}
if mode == "count":
    for f in sys.argv[3:]:
        a = EAL.list_assets(f, recursive=True, include_folder=False) if EAL.does_directory_exist(f) else []
        res["items"].append({"folder": f, "exists": EAL.does_directory_exist(f), "assets": len(a)})
else:
    for p in sys.argv[3:]:
        it = {"asset": p, "exists": EAL.does_asset_exist(p)}
        m = unreal.load_asset(p) if it["exists"] else None
        it["loaded"] = m is not None
        if m is not None:
            it["class"] = m.get_class().get_name()
            slots = m.materials if isinstance(m, unreal.SkeletalMesh) else m.static_materials
            mats, unresolved, tex = [], 0, set()
            for s in slots:
                mi = s.material_interface
                if mi is None:
                    unresolved += 1; continue
                path = mi.get_path_name(); mats.append(path)
                if path.startswith("/Engine/"): unresolved += 1
                if isinstance(mi, unreal.MaterialInstanceConstant):
                    for tp in mi.texture_parameter_values:
                        if tp.parameter_value: tex.add(tp.parameter_value.get_path_name())
            it.update(slots=len(slots), unresolved=unresolved, materials_outside=[x for x in mats if not x.startswith(p.rsplit("/", 1)[0])][:5],
                      textures=len(tex), textures_in_folder=sum(1 for t in tex if t.startswith(p.rsplit("/", 1)[0])), sample_texture=sorted(tex)[:2])
        res["items"].append(it)
open(out, "w").write(json.dumps(res, indent=1))
print("R84JSON", json.dumps(res))
