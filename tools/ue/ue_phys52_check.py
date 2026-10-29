# phys52_check.py: read-only check of the Titus phys52 assets (mesh PA/ABP, jiggle bodies, body types / trace flags).
import json, unreal
from collections import Counter
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; name = "cc_titus_assault"; folder = "/Game/SM2_Work/Tests/chars_phys/" + name
pj = W + "/Exports/chars_phys52/%s/tpl/%s.tpl/%s.physics.json" % (name, name, name)
lib = unreal.SM2PhysicsSetupLibrary; mesh = unreal.load_asset(folder + "/" + name)
pa = mesh.get_editor_property("physics_asset"); abpc = mesh.get_editor_property("post_process_anim_blueprint")
print("MESH pa", pa.get_path_name(), "abp", abpc.get_path_name())
for P in [pa, unreal.load_asset(folder + "/PA_cc_titus_assault_SM2Damper_1")]:
    try:
        bs = P.get_editor_property("skeletal_body_setups")
        print("BODYTYPES", P.get_name(), len(bs), dict(Counter(str(b.get_editor_property("physics_type")) for b in bs)), dict(Counter(str(b.get_editor_property("collision_trace_flag")) for b in bs)))
    except Exception as ex: print("BODYTYPES err", P.get_name(), ex)
d = json.load(open(pj, encoding="utf-8")); jn = [j.get("Name") or j.get("bone") for j in d.get("jiggles", [])]
ok = 0; miss = []
for b in jn:
    s = lib.describe_body(pa, mesh, b)
    if " body=-1 " not in s and " type=1 " not in s.split("|")[0]: ok += 1
    else: miss.append(b + ":" + s[:60])
print("JIGGLE_BODIES %d/%d missing=%s" % (ok, len(jn), miss))
D = unreal.load_asset(folder + "/PA_cc_titus_assault_SM2Damper_1")
for b in ["box_jiggle_02", "box_jiggle_01", "cloth_cdt_2_a"]: print("DAMPER", lib.describe_body(D, mesh, b)[:260])
print("dirty", [p.get_name() for p in unreal.EditorLoadingAndSavingUtils.get_dirty_content_packages()])
