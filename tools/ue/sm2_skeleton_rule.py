# sm2_skeleton_rule.py — one skeleton per mesh and its anims (ledger R35 + R43).
# Rule: an AnimSequence and the SkeletalMesh it belongs to must reference ONE Skeleton asset (same object path).
# Skeletons are identified by object path, never by name: UE's FBX importer names every new skeleton
# "{mesh}_Skeleton", so importing the same mesh (or a mesh+anim FBX) into a second folder without passing the
# existing skeleton silently makes a second, same-named, incompatible skeleton (the hologram_planet localbake case).
# Pure Python (no `unreal`): used by the UE import scripts, tools/ue/ue_skel_audit.py and the unit tests.
import re

BAK = re.compile(r"_bak\d+$", re.I)
PROTECTED = ("/game/sm2_work/original/", "/game/work/")


class SkeletonRuleError(RuntimeError):
    """Raised (loudly) when an import would split a mesh and its anims across skeletons."""


def leaf(path):
    return path.rstrip("/").rsplit("/", 1)[-1].split(".", 1)[0]


def folder_of(path):
    return path.rstrip("/").rsplit("/", 1)[0]


def is_backup(path):
    return bool(BAK.search(leaf(path)))


def is_protected(path):
    p = path.lower().rstrip("/") + "/"
    return any(p.startswith(x) for x in PROTECTED) or "lightning_01" in p


def name_collisions(skeleton_paths):
    """{name: [paths]} for skeleton names used by more than one live (non-_bakN) skeleton asset."""
    by = {}
    for p in sorted(set(skeleton_paths)):
        if not is_backup(p):
            by.setdefault(leaf(p).lower(), []).append(p)
    return {k: v for k, v in by.items() if len(v) > 1}


def pick_mesh_skeleton(folder, mesh_name, skeleton_paths):
    """Skeleton to pass to a skeletal-mesh import of `mesh_name` into `folder`.
    Same folder `{mesh}_Skeleton` -> reuse it. Else exactly one live `{mesh}_Skeleton` elsewhere -> reuse it (unify,
    so a second copy of the mesh never gets its own same-named skeleton). None -> UE may create a fresh one.
    Two or more live same-named skeletons elsewhere -> SkeletonRuleError (ambiguous; fix the project first)."""
    want = (mesh_name + "_Skeleton").lower()
    live = sorted(p for p in set(skeleton_paths) if leaf(p).lower() == want and not is_backup(p))
    here = [p for p in live if folder_of(p).lower() == folder.rstrip("/").lower()]
    if here:
        return here[0]
    if len(live) == 1:
        return live[0]
    if len(live) > 1:
        raise SkeletonRuleError("SKELCOLLISION %s: %d skeletons named %s_Skeleton (%s); unify them before importing"
                                % (folder, len(live), mesh_name, ", ".join(live)))
    return None


def check_anims(results, expected):
    """results: [(anim_path, skeleton_path_or_None)] after an anim import onto `expected`. Raise on any mismatch."""
    bad = [(a, s) for a, s in results if s != expected]
    if bad:
        raise SkeletonRuleError("SPLITSKEL %d clip(s) not on %s: %s (run ue_fix_split_skeletons.py)"
                                % (len(bad), expected, "; ".join("%s uses %s" % b for b in bad)))


def owner_mesh(anim_path, meshes):
    """Sibling mesh of an anim: SkeletalMesh in the same folder (or the parent folder for `.../Anims/`) named like the
    clip prefix `{mesh}__{clip}`, else the only one there."""
    name = leaf(anim_path); prefix = name.split("__", 1)[0]
    folder = folder_of(anim_path)
    for f in (folder, folder_of(folder) if leaf(folder).lower() == "anims" else None):
        if not f:
            continue
        here = [m for m in meshes if folder_of(m) == f and not is_backup(m)]
        named = [m for m in here if leaf(m) == prefix]
        if named:
            return named[0]
        if len(here) == 1:
            return here[0]
    return None


def audit(anims, meshes, skeletons):
    """anims {path: skel}, meshes {path: skel}, skeletons [existing skeleton paths].
    Returns dict: splits [(anim, askel, mesh, mskel)], collisions {name: [paths]},
    dangling [(asset, skel)] (skeleton not found), orphan_skeletons [skel with anims but no mesh]."""
    exist = set(skeletons)
    splits = []
    for a, s in sorted(anims.items()):
        if is_protected(a) or is_backup(a):
            continue
        m = owner_mesh(a, list(meshes))
        if m and meshes[m] and s != meshes[m]:
            splits.append((a, s, m, meshes[m]))
    dangling = sorted((p, s) for d in (anims, meshes) for p, s in d.items() if s and s not in exist and not is_backup(p))
    mesh_skels = set(meshes.values())
    orphan = sorted({s for p, s in anims.items() if s and s not in mesh_skels and not is_backup(p)})
    return {"splits": splits, "collisions": name_collisions(skeletons), "dangling": dangling, "orphan_skeletons": orphan}
