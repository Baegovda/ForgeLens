"""PoE2 M2 F.4: live secondary physics like SM2 (SM2PhysicsSetup plugin: PhysicsAsset + post-process RigidBody AnimBP).
PoE2 marks simulated bones by name (rig .ast: phys_* / phys_skinned_*; BellCrow 20 = bell + chains, lich 7 = tail,
cart rhoa 17 = saddle cords/feathers). No per-bone spring data was found in .sm/.smd/.ast/.ao, so every phys_ bone becomes
an SM2 'jiggle' with the plugin's default rigid-body parameters (HEURISTIC).
usage: ue_poe2_physics.py <result.json> <mesh asset> [<mesh asset> ...]"""
import json, os, sys, tempfile
import unreal

def bones(mesh):
    X = unreal.AnimPoseExtensions
    ref = X.get_reference_pose(mesh.get_editor_property("skeleton"))
    return [str(n) for n in X.get_bone_names(ref)]

out = {}
for path in sys.argv[2:]:
    mesh = unreal.load_asset(path)
    phys = [b for b in bones(mesh) if b.lower().startswith("phys_")]
    rec = {"phys_bones": phys}
    if phys:
        js = {"jiggles": [{"Name": b, "source": {"Mass": 1.0}} for b in phys], "cloths": [], "springDampers": []}
        f = os.path.join(tempfile.gettempdir(), mesh.get_name() + ".poe2.physics.json")
        open(f, "w").write(json.dumps(js))
        dest = path.rsplit("/", 1)[0] + "/Physics"
        res = unreal.SM2PhysicsSetupLibrary.setup_from_json(mesh, f, dest)
        ok, report = (res[0], res[-1]) if isinstance(res, tuple) else (True, res)  # Python wraps (bool, out Report)
        rec.update({"ok": bool(ok), "report": str(report)[:3000], "physics_asset": str(mesh.get_editor_property("physics_asset").get_path_name()) if mesh.get_editor_property("physics_asset") else None})
    out[path] = rec
open(sys.argv[1], "w").write(json.dumps(out, indent=1))
