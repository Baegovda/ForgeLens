# ue_skel_audit.py [root=/Game] [only=<substring>] [out=<file>] — read-only skeleton audit inside the editor (R43).
# AssetRegistry only (asset tags, nothing loaded, nothing saved): every AnimSequence / SkeletalMesh with its
# "Skeleton" tag, every Skeleton asset, then sm2_skeleton_rule.audit():
#   SKELAUDIT SPLIT      anim not on its sibling mesh's skeleton (rule violation -> ue_fix_split_skeletons.py fix)
#   SKELAUDIT COLLISION  two live skeletons with the same name (the trap that makes splits on the next import)
#   SKELAUDIT DANGLING   asset tag points at a skeleton that does not exist
#   SKELAUDIT ORPHAN     skeleton used by anims but by no SkeletalMesh
# Run: uerun.ps1 tools/ue/ue_skel_audit.py /Game  (editor open -> EngineWire; closed -> headless commandlet).
import os, sys, unreal
sys.path.insert(0, os.path.dirname(os.path.abspath(globals().get("__file__") or os.environ.get("SM2_TOOLS_UE", ".") + "/x")))
if os.environ.get("SM2_TOOLS_UE"): sys.path.insert(0, os.environ["SM2_TOOLS_UE"])
import sm2_skeleton_rule as R

root = sys.argv[1] if len(sys.argv) > 1 else "/Game"
only = sys.argv[2] if len(sys.argv) > 2 else ""
OUT = open(sys.argv[3], "w", encoding="utf-8") if len(sys.argv) > 3 else None  # commandlet stdout drops print()
_print = print
def print(*a):
    _print(*a)
    if OUT: OUT.write(" ".join(str(x) for x in a) + "\n"); OUT.flush()
ar = unreal.AssetRegistryHelpers.get_asset_registry()


def assets(cls):
    f = unreal.ARFilter(class_paths=[unreal.TopLevelAssetPath("/Script/Engine", cls)], package_paths=[root], recursive_paths=True)
    return ar.get_assets(f)


def skel_tag(a):
    v = a.get_tag_value("Skeleton")
    if not v or v == "None":
        return None
    v = str(v).split("'")[1] if "'" in str(v) else str(v)
    return v.split(".", 1)[0]


anims = {str(a.package_name): skel_tag(a) for a in assets("AnimSequence")}
meshes = {str(a.package_name): skel_tag(a) for a in assets("SkeletalMesh")}
skels = [str(a.package_name) for a in assets("Skeleton")]
res = R.audit(anims, meshes, skels)
print("SKELAUDIT COUNT anims %d meshes %d skeletons %d root %s" % (len(anims), len(meshes), len(skels), root))
keep = lambda *xs: (not only) or any(only.lower() in (x or "").lower() for x in xs)
for x in res["splits"]:
    if keep(*x): print("SKELAUDIT SPLIT %s uses %s ; mesh %s uses %s" % x)
for n, ps in sorted(res["collisions"].items()):
    if keep(*ps): print("SKELAUDIT COLLISION %s : %s" % (n, " | ".join(ps)))
for p, s in res["dangling"]:
    if keep(p, s): print("SKELAUDIT DANGLING %s -> %s" % (p, s))
for s in res["orphan_skeletons"]:
    if keep(s): print("SKELAUDIT ORPHAN %s (%d anims)" % (s, sum(1 for v in anims.values() if v == s)))
print("SKELAUDIT END splits %d collisions %d dangling %d orphan %d" % (len(res["splits"]), len(res["collisions"]), len(res["dangling"]), len(res["orphan_skeletons"])))
