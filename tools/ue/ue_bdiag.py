# bdiag.py <asset> <set> [grav 0|1]: which PA the ABP uses, DescribeBody for the first verify helpers; optional gravity toggle (saved).
import sys, json, unreal
name, SET = sys.argv[1], sys.argv[2]; G = sys.argv[3] if len(sys.argv) > 3 else ""
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; folder = "/Game/SM2_Work/Tests/%s/%s" % (SET, name)
pj = json.load(open(W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (SET, name, name, name), encoding="utf-8-sig"))
hs = []
for c in pj.get("cloths", []):
    md = c.get("maxDistanceMeters", [])
    for i, h in enumerate(c.get("helperBones", [])):
        if not (i < len(md) and md[i] == 0): hs.append(h["bone"])
hs = hs[::max(1, len(hs) // 12)][:3]
EAL = unreal.EditorAssetLibrary; L = unreal.SM2PhysicsSetupLibrary
m = unreal.load_asset(folder + "/" + name)
print("BD meshPA", m.get_editor_property("physics_asset").get_path_name() if m.get_editor_property("physics_asset") else None)
for a in EAL.list_assets(folder, False, False):
    if "/ABP_" in a and "_bak" not in a:
        deps = unreal.AssetRegistryHelpers.get_asset_registry().get_dependencies(a.split(".")[0], unreal.AssetRegistryDependencyOptions())
        print("BD abp", a.split("/")[-1], [str(x).split("/")[-1] for x in (deps or []) if "PA_" in str(x)])
pas = [a.split(".")[0] for a in EAL.list_assets(folder, False, False) if "/PA_" in a and "_bak" not in a and "ChaosCloth" not in a]
for p in pas:
    pa = unreal.load_asset(p)
    if G: print("BD grav", p.split("/")[-1], L.set_sim_gravity(pa, G == "1")); EAL.save_asset(p, only_if_is_dirty=False)
for b in hs: print("BD", L.describe_body(unreal.load_asset(pas[-1]), m, b)[:1500])
