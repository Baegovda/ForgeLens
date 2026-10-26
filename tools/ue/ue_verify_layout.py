# Verify /Game/SM2_Work after the layout move: no redirectors, no missing dependencies, mesh MIs and parents, anim skeletons, master stats.
import unreal, collections
W = "/Game/SM2_Work"
reg = unreal.AssetRegistryHelpers.get_asset_registry()
mel = unreal.MaterialEditingLibrary
o = unreal.AssetRegistryDependencyOptions(include_soft_package_references=True, include_hard_package_references=True, include_searchable_names=False, include_soft_management_references=False, include_hard_management_references=False)
ads = reg.get_assets_by_path(W, recursive=True)
cnt = collections.Counter(); missing = []; reds = []
for a in ads:
    cls = str(a.asset_class_path.asset_name); cnt[(str(a.package_path), cls)] += 1
    if cls == "ObjectRedirector": reds.append(str(a.package_name))
    for d in reg.get_dependencies(a.package_name, o) or []:
        d = str(d)
        if d.startswith("/Game") and not unreal.EditorAssetLibrary.does_asset_exist(d): missing.append((str(a.package_name), d))
print("ASSETS", len(ads), "REDIRECTORS", reds, "MISSING_DEPS", missing[:20], len(missing))
for a in ads:
    cls = str(a.asset_class_path.asset_name); pk = str(a.package_name)
    if cls in ("SkeletalMesh", "StaticMesh"):
        m = unreal.load_asset(pk)
        mats = [s.material_interface for s in (m.materials if cls == "SkeletalMesh" else m.static_materials)]
        none = sum(1 for x in mats if x is None)
        outside = sorted({x.get_path_name().rsplit("/", 1)[0] for x in mats if x})
        parents = sorted({x.get_editor_property("parent").get_path_name() for x in mats if isinstance(x, unreal.MaterialInstanceConstant) and x.get_editor_property("parent")})
        sk = m.skeleton.get_path_name() if cls == "SkeletalMesh" else "-"
        print("MESH", pk, "slots", len(mats), "none", none, "mi_dirs", outside, "parents", parents, "skel", sk)
    elif cls == "AnimSequence":
        s = unreal.load_asset(pk).get_editor_property("skeleton")
        print("ANIM", pk, "skel", s.get_path_name() if s else None)
    elif cls == "Material":
        st = mel.get_statistics(unreal.load_asset(pk))
        print("MAT", pk, "ps", st.num_pixel_shader_instructions, "samplers", st.num_samplers)
