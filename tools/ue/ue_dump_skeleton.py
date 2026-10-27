# Dump a skeleton's reference pose to JSON (UE space: cm, Z-up). Read-only.
# usage: sys.argv = ['x', <skeletal mesh or skeleton path>, <out.json>]
# Feeds the extractor's --anim-mannequin <json>: bone names, parents and ref locals/worlds of SK_Mannequin.
import sys, json, unreal
X = unreal.AnimPoseExtensions
a = unreal.load_asset(sys.argv[1])
sk = a if isinstance(a, unreal.Skeleton) else a.skeleton
mesh = a if isinstance(a, unreal.SkeletalMesh) else sk.get_preview_mesh()
comp = unreal.SkeletalMeshComponent(); comp.set_skeletal_mesh_asset(mesh)   # GetParentBone reads the mesh ref skeleton
pose = X.get_reference_pose(sk)
names = [str(n) for n in X.get_bone_names(pose)]
def tr(t):
    q = t.rotation; p = t.translation; s = t.scale3d
    return {"t": [p.x, p.y, p.z], "q": [q.x, q.y, q.z, q.w], "s": [s.x, s.y, s.z]}
bones = []
for n in names:
    par = str(comp.get_parent_bone(n))
    bones.append({"name": n, "parent": par if par in names else "",
                  "local": tr(X.get_ref_bone_pose(pose, n, unreal.AnimPoseSpaces.LOCAL)),
                  "world": tr(X.get_ref_bone_pose(pose, n, unreal.AnimPoseSpaces.WORLD))})
json.dump({"skeleton": sk.get_path_name(), "units": "cm", "up": "Z", "bones": bones}, open(sys.argv[2], "w"), indent=1)
print("DUMPSK", len(bones), sys.argv[2])
