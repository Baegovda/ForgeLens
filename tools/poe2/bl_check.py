import bpy, sys, os
args = sys.argv[sys.argv.index('--')+1:]
out = []
for f in args:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        if f.lower().endswith('.fbx'): bpy.ops.import_scene.fbx(filepath=f)
        else: bpy.ops.import_scene.gltf(filepath=f)
    except Exception as e:
        out.append(f"{os.path.basename(f)}\tIMPORT FAILED {e}"); continue
    meshes=[o for o in bpy.data.objects if o.type=='MESH']; arms=[o for o in bpy.data.objects if o.type=='ARMATURE']
    v=sum(len(o.data.vertices) for o in meshes); t=sum(len(o.data.polygons) for o in meshes)
    bones=sum(len(a.data.bones) for a in arms); acts=[(a.name, tuple(round(x,1) for x in a.frame_range)) for a in bpy.data.actions]
    dims=[tuple(round(d,3) for d in o.dimensions) for o in meshes][:2]
    imgs=len(bpy.data.images)
    out.append(f"{os.path.basename(f)}\tmeshes {len(meshes)} verts {v} faces {t} bones {bones} actions {acts} dims {dims} images {imgs}")
open(os.environ.get('BL_OUT','bl_check.txt'),'w').write("\n".join(out)+"\n")
