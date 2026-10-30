import unreal, sys
D="/Game/ForgeLens/SM2/VFX/vortex_orb"; AT=unreal.AssetToolsHelpers.get_asset_tools(); E=unreal.EditorAssetLibrary; L=unreal.MaterialEditingLibrary
for n,par,v in [("MI_orb_bloom","M_orb_glow",float(sys.argv[1]))]:
    p=D+"/"+n
    mi=unreal.load_asset(p) if E.does_asset_exist(p) else AT.create_asset(n,D,unreal.MaterialInstanceConstant,unreal.MaterialInstanceConstantFactoryNew())
    mi.set_editor_property("parent",unreal.load_asset(D+"/"+par)); L.set_material_instance_scalar_parameter_value(mi,"Intensity",v); L.set_material_instance_vector_parameter_value(mi,"Tint",unreal.LinearColor(1.0,0.55,0.95,1)); E.save_loaded_asset(mi); print("R97X mi",p,v)
