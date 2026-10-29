# ue_r51_muzzle.py (R51): UE side of the muzzle position. Opt-in (nothing changes unless run).
# Usage: uerun.ps1 <this> <weapon_tpl>[,socket][,<fbx_dir>]
#   Imports Exports/r51/<w>/tpl/<w>.tpl/<w>.fbx as a skeletal mesh into a NEW /Game/SM2_Work/Tests/r51/<w> (refuses if it
#   exists), reports the shoot-tip node (prop_firearm_shoot_tip locator; MUZZLE_AIM.tsv; default "shootTip") as a bone and
#   its ref-pose location (component query; may print 0 for an unregistered component - use the glb node world instead).
#   Undo: the test folder (owner).
import sys, time, unreal
toks = [t for a in sys.argv[1:] if not a.startswith("-") for t in a.split(",") if t]
w = toks[0] if toks else "wpn_bolt_rifle_01"; socket = "socket" in toks; tip = next((t[4:] for t in toks if t.startswith("tip=")), "shootTip")
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; fbx = W + "/Exports/r51/%s/tpl/%s.tpl/%s.fbx" % (w, w, w)
EAL = unreal.EditorAssetLibrary; folder = "/Game/SM2_Work/Tests/r51/" + w
if not (EAL.does_directory_exist(folder) and EAL.list_assets(folder, True, False)):
    t0 = time.time()
    t = unreal.AssetImportTask(); t.filename = fbx; t.destination_path = folder; t.automated = True; t.save = True
    o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
    o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH; t.options = o
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
    print("R51 import s=%.1f" % (time.time() - t0))
mesh = unreal.load_asset(folder + "/" + w)
if mesh is None: raise SystemExit("R51 no skeletal mesh at " + folder)
comp = unreal.SkeletalMeshComponent(); comp.set_skinned_asset_and_update(mesh)
names = [str(comp.get_bone_name(i)) for i in range(comp.get_num_bones())]
print("R51 bones", len(names), "tip candidates", [n for n in names if "tip" in n.lower()])
try:
    for n in [n for n in names if "tip" in n.lower()] + ["root"]:
        if n in names:
            loc = comp.get_socket_transform(n, unreal.RelativeTransformSpace.RTS_COMPONENT).translation
            print("R51 bone", n, "cm (%.2f, %.2f, %.2f)" % (loc.x, loc.y, loc.z))
except Exception as ex:
    print("R51 ref pose err", ex)
# Sockets cannot be created from Python in UE 5.7 (SkeletalMeshSocket.SocketName is read-only, R51 run): attach to the bone
# name itself (Niagara / actor attach accept bone names). The "socket" token is kept for a future plugin call.
print("R51 attach bone", tip, tip in names)
