# ue_r67_cloth.py (R67): sidecar-variant rebuilds of the R63 cloth sorcerer to cut cloth-in-body clipping.
#   build,<tag>,<rscale>,<backstopCm>  write a variant physics.json (every cloth collision radius x rscale, ue.chaosCloth.backstopCm)
#                                       -> duplicate Characters/cc_sorcerer_exalted to Tests/r67_cloth/cc_sorcerer_exalted_<tag> + create_chaos_cloth
#   use,<tag>                           put that mesh on R63_on / R63_off in the open Map_Cloth (editor world, saved)
import sys, json, unreal
W = "C:/Users/Revaptor_FX/Desktop/SM2_Work"; N = "cc_sorcerer_exalted"; F = "/Game/SM2_Work/Tests/r67_cloth"
PJ = W + "/Exports/chars_cloth/%s/tpl/%s.tpl/%s.physics.json" % (N, N, N); SRC = "/Game/SM2_Work/Characters/%s/%s" % (N, N)
EAL = unreal.EditorAssetLibrary; L = unreal.SM2PhysicsSetupLibrary
def build(tag, rs, bs, md=None):
    j = json.load(open(PJ, encoding="utf-8")); n = 0
    for c in j["cloths"]:
        cc = c.setdefault("ue", {}).setdefault("chaosCloth", {}); cc["backstopCm"] = bs
        if md is not None: cc["maxDistanceScaleCm"] = md  # 0 = every particle pinned = cloth stays on its skinned pose (baseline)
        col = cc.get("collision") or {}
        for k in col.get("capsules", []):
            k["radiusFirstCm"] *= rs; k["radiusSecondCm"] *= rs; n += 1
        for k in col.get("spheres", []): k["radiusCm"] *= rs; n += 1
        for k in col.get("boxes", []): k["sizeCm"] = [v * rs for v in k["sizeCm"]]; n += 1
    out = W + "/Scripts/r67/%s.%s.physics.json" % (N, tag); json.dump(j, open(out, "w", encoding="utf-8"), indent=1)
    m = F + "/%s_%s" % (N, tag)
    if EAL.does_asset_exist(m): EAL.delete_asset(m)
    mesh = EAL.duplicate_asset(SRC, m); r = L.create_chaos_cloth(mesh, out); conv, rep = (r if isinstance(r, tuple) else ([], ""))[-2:]
    print("R67C build", tag, "rscale", rs, "backstop", bs, "shapes scaled", n, "converted", len(conv))
    for l in str(rep).split(" | "):
        if "collision shapes" in l or "backstop" in l: print("R67LOG", l[:220])
    EAL.save_asset(m, only_if_is_dirty=False)
def use(tag):
    mesh = unreal.load_asset(F + "/%s_%s" % (N, tag)); w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    for a in unreal.GameplayStatics.get_all_actors_of_class(w, unreal.SkeletalMeshActor):
        if a.get_actor_label() in ("R63_on", "R63_off"):
            c = a.skeletal_mesh_component; ad = c.get_editor_property("animation_data"); c.set_skinned_asset_and_update(mesh); c.set_editor_property("animation_data", ad)
            c.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE)
    unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level(); print("R67C use", tag)
a = sys.argv[1].split(",")
if a[0] == "build": build(a[1], float(a[2]), float(a[3]), float(a[4]) if len(a) > 4 else None)
elif a[0] == "use": use(a[1])

