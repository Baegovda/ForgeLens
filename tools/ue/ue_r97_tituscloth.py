import unreal
E=unreal.EditorAssetLibrary; F="/Game/ForgeLens/SM2/Characters/cc_titus_cloth"
m=unreal.load_asset(F+"/cc_titus_assault")
pj=r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Exports/r97/titus_cloth/tpl/cc_titus_assault.tpl/cc_titus_assault.physics.json"
lib=unreal.SM2PhysicsSetupLibrary
print("R97Q", lib.setup_from_json(m, pj, F))
for a in E.list_assets(F, False, False): E.save_asset(a, only_if_is_dirty=True)
E.save_loaded_asset(m)
pa=m.get_editor_property("physics_asset"); print("R97Q pa", pa.get_path_name() if pa else None)
