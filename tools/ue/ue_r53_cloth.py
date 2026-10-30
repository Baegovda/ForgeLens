# ue_r53_cloth.py (R53): verify the installed SM2PhysicsSetup cloth path (CreateChaosCloth) on one character.
# Imports Exports/chars_cloth/<n> FBX into a NEW folder /Game/ForgeLens/SM2/Tests/r53_cloth/<n> (refuses if it exists; nothing
# renamed or deleted), runs create_chaos_cloth with the export's physics.json, prints the plugin report, saves.
# Usage: uerun.ps1 <this> cc_sorcerer_exalted
import sys, json, time, unreal
args = [t for a in sys.argv[1:] if not a.startswith("-") for t in a.split(",") if t]
n = args[0] if args else "cc_sorcerer_exalted"
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; base = W + "/Exports/chars_cloth/%s/tpl/%s.tpl/%s" % (n, n, n)
EAL = unreal.EditorAssetLibrary; lib = unreal.SM2PhysicsSetupLibrary
folder = "/Game/ForgeLens/SM2/Tests/r53_cloth/" + n
if EAL.does_directory_exist(folder) and EAL.list_assets(folder, True, False):
    raise SystemExit("R53 folder exists, refusing: " + folder)
t0 = time.time()
t = unreal.AssetImportTask(); t.filename = base + ".fbx"; t.destination_path = folder; t.automated = True; t.save = True
o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH; t.options = o
unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
mesh = unreal.load_asset(folder + "/" + n)
print("R53 import", mesh is not None, "s=%.1f" % (time.time() - t0))
if mesh is None: raise SystemExit("R53 import failed")
secs = [str(m.get_editor_property("material_slot_name")) for m in mesh.get_editor_property("materials")]
print("R53 slots", len(secs), "SIM slots", [s for s in secs if "_SIM" in s.upper()])
pj = base + ".physics.json"
cl = json.load(open(pj, encoding="utf-8")).get("cloths", [])
print("R53 json cloths", [c.get("SimMeshName") for c in cl])
t1 = time.time(); r = lib.create_chaos_cloth(mesh, pj)
if isinstance(r, tuple) and len(r) == 3: ok, conv, rep = r
elif isinstance(r, tuple) and len(r) == 2: conv, rep = r; ok = len(conv) > 0
else: ok, conv, rep = r, [], ""
print("R53 create", ok, [str(c) for c in conv], "s=%.1f" % (time.time() - t1))
for l in str(rep).split(" | "): print("R53LOG", l[:300])
try: print("R53 clothing assets", len(mesh.get_editor_property("mesh_clothing_assets")))
except Exception as ex: print("R53 clothing assets err", ex)
EAL.save_asset(folder + "/" + n, only_if_is_dirty=False)
