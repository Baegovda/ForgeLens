import unreal, json, sys
out = {}
for a in unreal.EditorAssetLibrary.list_assets("/Game/ForgeLens/POE", recursive=True):
    o = unreal.load_asset(a)
    if isinstance(o, unreal.MaterialInstanceConstant):
        tv = [str(p.parameter_value.get_name()) if p.parameter_value else None for p in o.get_editor_property("texture_parameter_values")]
        out[a] = {"parent": o.get_editor_property("parent").get_name() if o.get_editor_property("parent") else None, "tex": tv}
    elif isinstance(o, unreal.AnimSequence):
        out[a] = {"len": round(o.get_editor_property("sequence_length"), 3), "skel": o.get_editor_property("skeleton").get_name()}
    elif isinstance(o, unreal.SkeletalMesh):
        out[a] = {"bones": o.get_editor_property("skeleton").get_editor_property("bone_tree").__len__(), "mats": len(o.get_editor_property("materials")), "bounds": str(o.get_bounds().box_extent)}
open(sys.argv[1], "w").write(json.dumps(out, indent=1))
