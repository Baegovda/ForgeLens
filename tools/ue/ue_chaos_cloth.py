# Chaos Cloth option (mtl1-9): <asset> [set=chars_cloth]
# Duplicates /Game/SM2_Work/Tests/<set>/<asset>/<asset> to <asset>_ChaosCloth (never touches the original), turns every cloth
# whose game _SIM section is found into a Chaos Clothing Asset (CreateChaosCloth: MaxDistance from maxDistanceMeters,
# 0 = pinned), then rebuilds the bone-based PA/ABP for the rest (SetupFromJsonEx skip list: tentacles etc. stay RigidBody).
# The bone method (ue_bulk default) stays the default; this is opt-in.
import sys, json, unreal
name = sys.argv[1]; SET = sys.argv[2] if len(sys.argv) > 2 else "chars_cloth"
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; folder = "/Game/SM2_Work/Tests/%s/%s" % (SET, name)
pj = W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (SET, name, name, name)
EAL = unreal.EditorAssetLibrary; lib = unreal.SM2PhysicsSetupLibrary
src = folder + "/" + name; dst = folder + "/" + name + "_ChaosCloth"
def fix_redirector(p):
    # The redirector's referencers (e.g. a test level) keep the path; deleting the redirector and recreating the mesh at the
    # same path makes them load the NEW mesh (AssetTools fix-up is not exposed to Python in 5.7).
    if not EAL.does_asset_exist(p) or str(EAL.find_asset_data(p).asset_class_path.asset_name) != "ObjectRedirector": return
    print("CHAOS redirector deleted", p, EAL.find_package_referencers_for_asset(p, False), EAL.delete_asset(p))
# earlier run's redirector
fix_redirector(dst)
if EAL.does_asset_exist(dst):
    n = 1
    while EAL.does_asset_exist(dst + "_bak%d" % n): n += 1
    EAL.rename_asset(dst, dst + "_bak%d" % n); EAL.save_asset(dst + "_bak%d" % n, only_if_is_dirty=False)
# mtl1-22: a referenced mesh leaves an ObjectRedirector at dst after the rename; remove it so dst is free for the new mesh.
fix_redirector(dst)
mesh = EAL.duplicate_asset(src, dst)
if mesh is None:  # mtl1-33: the registry can still report the fresh redirector as the renamed mesh right after the rename (calgar, referenced by L_wind16) -> retry once
    fix_redirector(dst); mesh = EAL.duplicate_asset(src, dst)
if mesh is None: raise SystemExit("CHAOS duplicate failed: %s exists (%s)" % (dst, EAL.does_asset_exist(dst)))
# mtl1-28: exports without --include-cloth-sim have no _SIM section; CreateChaosCloth then falls back to render-as-sim.
try:
    for c in json.load(open(pj, encoding="utf-8")).get("cloths", []):
        if c.get("simSectionExported") is False: print("CHAOS note", c.get("SimMeshName"), "no _SIM section in export -> render-as-sim fallback")
except Exception as e: print("CHAOS note json", e)
r = lib.create_chaos_cloth(mesh, pj)
if isinstance(r, tuple) and len(r) == 3: ok, conv, rep = r
elif isinstance(r, tuple) and len(r) == 2: conv, rep = r; ok = len(conv) > 0  # UE drops the bool return when out params exist
else: ok, conv, rep = r, [], ""
print("CHAOS create", ok, list(conv)); [print("CHAOSLOG", l) for l in str(rep).split(" | ")]
sk = [str(c) for c in conv]
r2 = lib.setup_from_json_ex(mesh, pj, folder + "/ChaosCloth", sk)
ok2, rep2 = (r2 if isinstance(r2, tuple) else (r2, ""))
print("CHAOS bones(rest)", ok2, str(rep2)[:300])
EAL.save_asset(dst, only_if_is_dirty=False)
for a in EAL.list_assets(folder + "/ChaosCloth", False, False): EAL.save_asset(a, only_if_is_dirty=True)
print("CHAOSJSON " + json.dumps({"asset": name, "mesh": dst, "converted": sk, "createOk": bool(ok), "restOk": bool(ok2)}))
