# phys52_apply.py: re-run SetupFromJson on the Titus mesh with the phys52 sidecar (new PA/ABP/_SM2Damper, old kept). Saves only those + mesh.
import sys, json, unreal
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; name = "cc_titus_assault"
folder = "/Game/SM2_Work/Tests/chars_phys/" + name
pj = W + "/Exports/chars_phys52/%s/tpl/%s.tpl/%s.physics.json" % (name, name, name)
lib = unreal.SM2PhysicsSetupLibrary; EAL = unreal.EditorAssetLibrary
mesh = unreal.load_asset(folder + "/" + name)
print("BEFORE pa", mesh.get_editor_property("physics_asset").get_path_name(), "abp", mesh.get_editor_property("post_process_anim_blueprint").get_path_name())
before = set(EAL.list_assets(folder, False, False))
r = lib.setup_from_json(mesh, pj, folder)
print("SETUP", r)
pa = mesh.get_editor_property("physics_asset")
abpc = mesh.get_editor_property("post_process_anim_blueprint")
abp = unreal.load_asset(abpc.get_path_name().rsplit(".", 1)[0])
print("AFTER pa", pa.get_path_name(), "abp", abp.get_path_name())
print("TUNE", lib.tune_rigid_body(abp, 2, "CENTRE", 0.0, 0.0, 600.0, 2000.0, 10.0, 100.0))
new = [a for a in EAL.list_assets(folder, False, False) if a not in before]
print("NEW", new)
EAL.save_loaded_asset(abp)
for a in new: EAL.save_asset(a, only_if_is_dirty=True)
EAL.save_loaded_asset(mesh)
from collections import Counter
for P in [pa] + [unreal.load_asset(a.rsplit(".", 1)[0]) for a in new if "SM2Damper" in a]:
  try:
    print("BODYTYPES", P.get_name(), dict(Counter(int(b.get_editor_property("physics_type").value) for b in P.get_editor_property("skeletal_body_setups"))),
          dict(Counter(str(b.get_editor_property("collision_trace_flag")) for b in P.get_editor_property("skeletal_body_setups"))))
  except Exception as ex: print("BODYTYPES err", ex)
d = json.load(open(pj, encoding="utf-8"))
jn = [j.get("Name") or j.get("bone") for j in d.get("jiggles", [])]
ok = 0; miss = []
for b in jn:
    s = lib.describe_body(pa, mesh, b)
    if " body=-1 " not in s and " type=1 " not in s.split("|")[0]: ok += 1  # phys52: simulated = PhysType_Default (0) + bUseDefaultAsSimulated
    else: miss.append(b + ":" + s[:60])
print("JIGGLE_BODIES %d/%d missing=%s" % (ok, len(jn), miss))
print("SAMPLE", lib.describe_body(pa, mesh, "box_jiggle_01"))
dpa = [a for a in new if "SM2Damper" in a]
if dpa:
    D = unreal.load_asset(dpa[0].rsplit(".", 1)[0])
    for b in ["box_jiggle_02", "box_jiggle_end", "box_jiggle_01", "cloth_cdt_2_a"]:
        print("DAMPER", b, lib.describe_body(D, mesh, b))
