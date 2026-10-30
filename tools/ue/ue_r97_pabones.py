import unreal, re, os
AR = unreal.AssetRegistryHelpers.get_asset_registry()
content = unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_content_dir())
def fnames(pkg):
    f = content + pkg[len("/Game/"):] + ".uasset"
    if not os.path.exists(f): return set()
    t = open(f, "rb").read()
    return set(m.decode() for m in re.findall(rb"(?<=\x00)[A-Za-z][A-Za-z0-9_]{1,}(?=\x00)", t))
seen = set(); out = []
for ad in AR.get_assets_by_class(unreal.TopLevelAssetPath("/Script/Engine", "SkeletalMesh")):
    pkg = str(ad.package_name)
    if not (pkg.startswith("/Game/Characters") or pkg.startswith("/Game/ForgeLens") or pkg.startswith("/Game/SM2_Work")): continue
    m = ad.get_asset()
    try: pa = m.get_editor_property("physics_asset"); sk = m.get_editor_property("skeleton")
    except Exception: continue
    if not pa or not sk: continue
    key = (pa.get_path_name(), sk.get_path_name())
    if key in seen: continue
    seen.add(key)
    B = set(str(b) for b in unreal.AnimPoseExtensions.get_bone_names(unreal.AnimPoseExtensions.get_reference_pose(sk)))
    N = fnames(pa.get_outermost().get_name())
    cand = sorted(n for n in N if n not in B and re.search(r"(?i)(phys|bone|spine|arm|leg|head|neck|clav|hand|foot|pelvis|cdt|_SIM|cloth|_[LR]$|\d$)", n) and not re.search(r"(?i)(Property|Elem|Setup|Body|Profile|Physics|Constraint|Class|Script|Package|Default|Instance|Collision|Agg|Vector|Rotator|Float|Struct)", n))
    if cand: out.append((pa.get_path_name(), sk.get_path_name(), len(B), cand[:15]))
for o in out: print("R97B", o)
print("R97B checked", len(seen), "with suspects", len(out))
