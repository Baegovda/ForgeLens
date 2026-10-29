import unreal, sys
EAL = unreal.EditorAssetLibrary
for f in sys.argv[1:]:
    assets = EAL.list_assets(f, True, False)
    kinds = {}
    for a in assets:
        d = EAL.find_asset_data(a); c = str(d.asset_class_path.asset_name)
        kinds[c] = kinds.get(c, 0) + 1
    print("FOLDER", f, len(assets), kinds)
    for a in assets:
        d = EAL.find_asset_data(a)
        if str(d.asset_class_path.asset_name) == "SkeletalMesh" and "/Anims" not in a:
            m = unreal.load_asset(a.split(".")[0])
            pa = m.get_editor_property("physics_asset"); pp = m.get_editor_property("post_process_anim_blueprint")
            print("  MESH", m.get_path_name(), "skel", m.skeleton.get_name(), "physics_asset", pa.get_path_name() if pa else None, "ppabp", pp.get_path_name() if pp else None, "materials", len(m.materials))
        if str(d.asset_class_path.asset_name) == "AnimSequence":
            s = unreal.load_asset(a.split(".")[0]); print("  ANIM", s.get_path_name(), "len %.2fs" % s.get_play_length(), "skel", s.get_editor_property("skeleton").get_name())
print("pie", unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).is_in_play_in_editor())
