import sys, unreal
mel = unreal.MaterialEditingLibrary
m = unreal.load_asset(sys.argv[1]); keys = sys.argv[2].split(",") if len(sys.argv) > 2 else []
print("MCHK compiling", unreal.ShaderCompilingManager if hasattr(unreal, "ShaderCompilingManager") else "")
for s in m.materials:
    sn = str(s.material_slot_name); mi = s.material_interface
    if keys and not any(k in sn for k in keys): continue
    if not isinstance(mi, unreal.MaterialInstanceConstant): print("MCHK", sn, mi.get_path_name() if mi else None); continue
    tex = {}
    for p in mi.get_editor_property("texture_parameter_values"):
        tex[str(p.parameter_info.name)] = p.parameter_value.get_name() if p.parameter_value else None
    sw = {k: mel.get_material_instance_static_switch_parameter_value(mi, k) for k in ("UseChameleon", "UseSecondSpec", "UseOpacityMask")}
    print("MCHK", sn, mi.get_path_name().split(".")[0], mi.parent.get_name() if mi.parent else None, sw, tex)
