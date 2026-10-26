# Check / set up the SM2 secondary physics of an imported character in Unreal (UE 5.4+).
# usage (UEMCP ue_python): sys.argv = ['x', <{model}.physics.json>, <skeletal mesh asset>,
#                                      [dest=/Game/SM2_Work/Tests/physics], [verify|physasset]]
#
# The exporter (SM2Extractor export / SM2Browser export, physics on by default) already BAKES the game's secondary
# motion into the files, so nothing has to run in Unreal for it to play:
#   * jiggle bones (prop_phys_jiggle): exact port of the engine step (loop 0x14147e5b5) run at 60 Hz over each clip,
#     written as those bones' curves in every {model}__{clip}.fbx;
#   * cloth (prop_cloth_sim, purity seals / tabards / cloaks): the NvCloth-style sim of the SM2Browser preview, reduced
#     to rigid clusters of sim particles = helper bones "{SIM mesh}_physNN" in the bind FBX, with the *_REND vertices
#     re-skinned to them; their per-frame best fit is baked into every clip.
# verify    (default) checks that the skeleton has the jiggle and cloth helper bones the clips animate and lists the
#           baked clips. Read-only.
# physasset also creates a PhysicsAsset (FBX-import settings) under dest, without assigning it, as the base for a live
#           setup (RigidBody / Chaos Cloth collision). Existing assets are never replaced: a free _N name is used.
# A live (runtime) setup instead of the bake is documented per bone / cloth in the json "ue" blocks:
#   jiggles -> AnimDynamics (linear spring k/m, damping, gravity scale, angular limits atan(constraint)),
#   cloths  -> Chaos Cloth config + MaxDistance mask + PhysicsAsset collision capsules / spheres / boxes.
# AnimGraph nodes and cloth assets cannot be created from Python in UE 5.7 (no AnimGraph node API, no clothing factory
# exposed), so that part stays manual; see HANDOFF.md "Physics".
import sys, json, os, unreal

def _bone_names(mesh):
    X = unreal.AnimPoseExtensions
    skel = mesh.skeleton if isinstance(mesh, unreal.SkeletalMesh) else mesh
    ref = X.get_reference_pose(skel)
    return [str(n) for n in X.get_bone_names(ref)]

def _free_name(folder, name):
    eal = unreal.EditorAssetLibrary
    cand, n = name, 1
    while eal.does_asset_exist(folder + "/" + cand):
        cand = "%s_%d" % (name, n); n += 1
    return cand

def run(json_path, mesh_path, dest="/Game/SM2_Work/Tests/physics", mode="verify"):
    data = json.load(open(json_path, encoding="utf-8"))
    mesh = unreal.load_asset(mesh_path)
    if mesh is None:
        print("SM2PHYS ERROR mesh not found: %s" % mesh_path); return False
    names = set(n.lower() for n in _bone_names(mesh))
    # The sidecar mixes cases (System.Text.Json keeps C# member names: "Name", "Group"; anonymous ones: "found").
    jig = [j.get("Name") or j.get("name") for j in data.get("jiggles", []) if j.get("found")]
    helpers = [h["bone"] for c in data.get("cloths", []) for h in (c.get("helperBones") or [])]
    miss_j = [n for n in jig if n.lower() not in names]
    miss_h = [n for n in helpers if n.lower() not in names]
    print("SM2PHYS class %s: %d jiggle bones (%d missing), %d cloth helper bones (%d missing)" %
          (data.get("SourceClass"), len(jig), len(miss_j), len(helpers), len(miss_h)))
    if miss_h:
        print("SM2PHYS cloth helper bones missing: re-import the model FBX exported with physics on (this build): %s" % ", ".join(miss_h[:8]))
    if miss_j:
        print("SM2PHYS jiggle bones missing from the skeleton: %s" % ", ".join(miss_j[:8]))
    for b in data.get("baked", []):
        print("SM2PHYS baked %s: %d frames, %d jiggle + %d cloth bones" % (b["clip"], b["Frames"], b["JiggleBones"], b["ClothBones"]))
    for c in data.get("cloths", []):
        ue = c["ue"]["chaosCloth"]
        print("SM2PHYS cloth %s -> %s: %d sim verts (%d pinned), damping %.3f, gravity %s cm/s2, edge %.2f bend %.2f" %
              (c["SimMeshName"], ",".join(c["RenderMeshes"]), c["simVertices"], c["pinned"], ue["dampingCoefficient"],
               ue["gravityOverrideCm"], ue["edgeStiffness"], ue["bendingStiffness"]))
    if mode == "physasset":
        eal = unreal.EditorAssetLibrary
        if not eal.does_directory_exist(dest):
            eal.make_directory(dest)
        pa = unreal.SkeletalMeshEditorSubsystem.create_physics_asset(mesh, False)
        if pa is None:
            print("SM2PHYS ERROR create_physics_asset failed"); return False
        target = _free_name(dest, "PA_" + mesh.get_name() + "_sm2")
        src = pa.get_path_name().split(".")[0]
        if eal.rename_asset(src, dest + "/" + target):
            eal.save_asset(dest + "/" + target)
            print("SM2PHYS physics asset %s/%s (not assigned to the mesh)" % (dest, target))
        else:
            print("SM2PHYS physics asset left at %s" % src)
    return not miss_j and not miss_h

if __name__ == "__main__" or True:
    a = sys.argv[1:]
    if len(a) >= 2:
        ok = run(a[0], a[1], a[2] if len(a) > 2 else "/Game/SM2_Work/Tests/physics", a[3] if len(a) > 3 else "verify")
        print("SM2PHYS %s" % ("OK" if ok else "CHECK"))
