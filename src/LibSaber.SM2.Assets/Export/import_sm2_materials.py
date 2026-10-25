# Build Unreal materials for an SM2 FBX export.
#
# The FBX already has diffuse, normal, and specular links. This script reads the
# sidecar written next to that FBX (cc_titus_assault.materials.json) and creates
# a master material plus one material instance per slot. Normals are imported as
# normal maps. Packed masks (spec, AO, detail, height) are linear.
#
# In the Unreal Editor Python console, from the folder that contains this file
# and the sidecar:
#
#   py "D:/export/tpl/cc_titus_assault.tpl/import_sm2_materials.py" "D:/export/tpl/cc_titus_assault.tpl/cc_titus_assault.materials.json" "/Game/SM2/cc_titus_assault"
#
# Or, with the editor closed:
#
#   UnrealEditor-Cmd.exe Project.uproject -ExecutePythonScript="D:/export/tpl/cc_titus_assault.tpl/import_sm2_materials.py D:/export/tpl/cc_titus_assault.tpl/cc_titus_assault.materials.json /Game/SM2/cc_titus_assault"
#
# Import the FBX as a skeletal mesh first (default settings, materials and
# textures on). The script then replaces those slot materials. The third
# argument is the content folder. It defaults to /Game/SM2/<mesh name>.

import json
import os
import sys

try:
    import unreal
except ImportError:
    sys.stderr.write("Run this inside Unreal Editor. unreal could not be imported.\n")
    raise


def content_path(mesh_name):
    return "/Game/SM2/" + mesh_name


def load_sidecar(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def texture_file(sidecar_path, entry):
    relative = entry.get("file")
    if not relative:
        return None
    return os.path.normpath(os.path.join(os.path.dirname(sidecar_path), relative))


def import_texture(disk, dest, name, srgb, normal):
    if not disk or not os.path.isfile(disk):
        unreal.log_warning("SM2 missing texture " + str(disk))
        return None
    task = unreal.AssetImportTask()
    task.filename = disk
    task.destination_path = dest
    task.destination_name = name
    task.automated = True
    task.replace_existing = True
    task.save = True
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    asset = unreal.EditorAssetLibrary.load_asset(dest + "/" + name)
    if asset is None:
        return None
    asset.set_editor_property("srgb", srgb and not normal)
    if normal:
        asset.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_NORMALMAP)
        asset.set_editor_property("lod_group", unreal.TextureGroup.TEXTUREGROUP_CHARACTER_NORMAL_MAP)
    elif not srgb:
        asset.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_MASKS)
        asset.set_editor_property("lod_group", unreal.TextureGroup.TEXTUREGROUP_CHARACTER_SPECULAR)
    else:
        asset.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_DEFAULT)
        asset.set_editor_property("lod_group", unreal.TextureGroup.TEXTUREGROUP_CHARACTER)
    unreal.EditorAssetLibrary.save_loaded_asset(asset)
    return asset


def expr(material, cls, x, y):
    return unreal.MaterialEditingLibrary.create_material_expression(material, cls, x, y)


def link(src, src_pin, dst, dst_pin):
    unreal.MaterialEditingLibrary.connect_material_expressions(src, src_pin, dst, dst_pin)


def param_tex(material, name, x, y, sampler):
    node = expr(material, unreal.MaterialExpressionTextureSampleParameter2D, x, y)
    node.set_editor_property("parameter_name", name)
    node.set_editor_property("sampler_type", sampler)
    _d = {unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL: "/Engine/EngineMaterials/FlatNormal", unreal.MaterialSamplerType.SAMPLERTYPE_MASKS: "/Engine/EngineMaterials/DefaultDiffuse_TC_Masks"}.get(sampler, "/Engine/EngineResources/WhiteSquareTexture")
    _t = unreal.load_asset(_d)
    if _t: node.set_editor_property("texture", _t)
    return node


def make_master(folder, asset_name="M_SM2", translucent=False):
    # translucent=True is the Transp(a) parent (glass, gibs caps): diffuse alpha drives Opacity.
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    existing = folder + "/" + asset_name
    if unreal.EditorAssetLibrary.does_asset_exist(existing):
        unreal.EditorAssetLibrary.delete_asset(existing)
    material = tools.create_asset(asset_name, folder, unreal.Material, unreal.MaterialFactoryNew())
    material.set_editor_property("blend_mode", unreal.BlendMode.BLEND_TRANSLUCENT if translucent else unreal.BlendMode.BLEND_MASKED)
    if translucent:
        material.set_editor_property("translucency_lighting_mode", unreal.TranslucencyLightingMode.TLM_SURFACE)
    material.set_editor_property("two_sided", True)
    mel = unreal.MaterialEditingLibrary

    color = unreal.MaterialSamplerType.SAMPLERTYPE_COLOR
    linear = unreal.MaterialSamplerType.SAMPLERTYPE_MASKS
    normal_sampler = unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL
    diffuse = param_tex(material, "Diffuse", -900, -200, color)
    normal = param_tex(material, "Normal", -900, 80, normal_sampler)
    packed = param_tex(material, "Packed", -900, 360, linear)
    emissive = param_tex(material, "Emissive", -900, 640, color)
    detail = param_tex(material, "Detail", -900, 920, normal_sampler)
    detail_mask = param_tex(material, "DetailMask", -1100, 1100, linear)
    height = param_tex(material, "Height", -900, 1280, linear)

    strength = expr(material, unreal.MaterialExpressionScalarParameter, -500, 700)
    strength.set_editor_property("parameter_name", "EmissiveStrength")
    strength.set_editor_property("default_value", 0.0)
    emit_mul = expr(material, unreal.MaterialExpressionMultiply, -200, 680)
    link(emissive, "RGB", emit_mul, "A")
    link(strength, "", emit_mul, "B")

    rough_g = expr(material, unreal.MaterialExpressionComponentMask, -560, 360)
    rough_g.set_editor_property("r", False)
    rough_g.set_editor_property("g", True)
    rough_g.set_editor_property("b", False)
    rough_g.set_editor_property("a", False)
    link(packed, "RGBA", rough_g, "")
    rough_a = expr(material, unreal.MaterialExpressionComponentMask, -560, 460)
    rough_a.set_editor_property("r", False)
    rough_a.set_editor_property("g", False)
    rough_a.set_editor_property("b", False)
    rough_a.set_editor_property("a", True)
    link(packed, "RGBA", rough_a, "")
    rough_switch = expr(material, unreal.MaterialExpressionStaticSwitchParameter, -280, 400)
    rough_switch.set_editor_property("parameter_name", "RoughnessInAlpha")
    rough_switch.set_editor_property("default_value", False)
    link(rough_g, "", rough_switch, "False")
    link(rough_a, "", rough_switch, "True")

    ao_b = expr(material, unreal.MaterialExpressionComponentMask, -560, 560)
    ao_b.set_editor_property("r", False)
    ao_b.set_editor_property("g", False)
    ao_b.set_editor_property("b", True)
    ao_b.set_editor_property("a", False)
    link(packed, "RGBA", ao_b, "")
    one = expr(material, unreal.MaterialExpressionConstant, -560, 640)
    one.set_editor_property("r", 1.0)
    ao_switch = expr(material, unreal.MaterialExpressionStaticSwitchParameter, -280, 560)
    ao_switch.set_editor_property("parameter_name", "AoInBlue")
    ao_switch.set_editor_property("default_value", True)
    link(one, "", ao_switch, "False")
    link(ao_b, "", ao_switch, "True")

    # Some _em files are AO(a) or AO(r), not an emissive color. They are a second texture.
    ao_tex = param_tex(material, "Ao", -1100, 560, linear)
    ao_from_r = expr(material, unreal.MaterialExpressionComponentMask, -760, 700)
    ao_from_r.set_editor_property("r", True)
    ao_from_r.set_editor_property("g", False)
    ao_from_r.set_editor_property("b", False)
    ao_from_r.set_editor_property("a", False)
    link(ao_tex, "RGBA", ao_from_r, "")
    ao_from_a = expr(material, unreal.MaterialExpressionComponentMask, -760, 800)
    ao_from_a.set_editor_property("r", False)
    ao_from_a.set_editor_property("g", False)
    ao_from_a.set_editor_property("b", False)
    ao_from_a.set_editor_property("a", True)
    link(ao_tex, "RGBA", ao_from_a, "")
    ao_channel = expr(material, unreal.MaterialExpressionStaticSwitchParameter, -480, 740)
    ao_channel.set_editor_property("parameter_name", "AoInAlpha")
    ao_channel.set_editor_property("default_value", True)
    link(ao_from_r, "", ao_channel, "False")
    link(ao_from_a, "", ao_channel, "True")
    ao_source = expr(material, unreal.MaterialExpressionStaticSwitchParameter, -80, 560)
    ao_source.set_editor_property("parameter_name", "UseSeparateAo")
    ao_source.set_editor_property("default_value", False)
    link(ao_switch, "", ao_source, "False")
    link(ao_channel, "", ao_source, "True")

    metal = expr(material, unreal.MaterialExpressionComponentMask, -280, 280)
    metal.set_editor_property("r", True)
    metal.set_editor_property("g", False)
    metal.set_editor_property("b", False)
    metal.set_editor_property("a", False)
    link(packed, "", metal, "")

    mask_g = expr(material, unreal.MaterialExpressionComponentMask, -560, 980)
    mask_g.set_editor_property("r", False)
    mask_g.set_editor_property("g", True)
    mask_g.set_editor_property("b", False)
    mask_g.set_editor_property("a", False)
    link(detail_mask, "", mask_g, "")
    detail_lerp = expr(material, unreal.MaterialExpressionLinearInterpolate, -280, 900)
    link(normal, "RGB", detail_lerp, "A")
    link(detail, "RGB", detail_lerp, "B")
    link(mask_g, "", detail_lerp, "Alpha")
    detail_switch = expr(material, unreal.MaterialExpressionStaticSwitchParameter, -40, 120)
    detail_switch.set_editor_property("parameter_name", "HasDetail")
    detail_switch.set_editor_property("default_value", False)
    link(normal, "RGB", detail_switch, "False")
    link(detail_lerp, "", detail_switch, "True")

    height_scale = expr(material, unreal.MaterialExpressionScalarParameter, -500, 1280)
    height_scale.set_editor_property("parameter_name", "HeightScale")
    height_scale.set_editor_property("default_value", 0.0)
    height_bias = expr(material, unreal.MaterialExpressionSubtract, -200, 1240)
    link(height, "R", height_bias, "A")
    half = expr(material, unreal.MaterialExpressionConstant, -400, 1360)
    half.set_editor_property("r", 0.5)
    link(half, "", height_bias, "B")
    height_mul = expr(material, unreal.MaterialExpressionMultiply, 0, 1240)
    link(height_bias, "", height_mul, "A")
    link(height_scale, "", height_mul, "B")
    vertex_normal = expr(material, unreal.MaterialExpressionVertexNormalWS, 0, 1400)
    height_vec = expr(material, unreal.MaterialExpressionMultiply, 220, 1240)
    link(vertex_normal, "", height_vec, "A")
    link(height_mul, "", height_vec, "B")

    opaque = expr(material, unreal.MaterialExpressionConstant, -400, -40)
    opaque.set_editor_property("r", 1.0)
    opacity = expr(material, unreal.MaterialExpressionStaticSwitchParameter, -80, -40)
    opacity.set_editor_property("parameter_name", "UseOpacityMask")
    opacity.set_editor_property("default_value", False)
    link(opaque, "", opacity, "False")
    link(diffuse, "A", opacity, "True")

    mel.connect_material_property(diffuse, "RGB", unreal.MaterialProperty.MP_BASE_COLOR)
    mel.connect_material_property(opacity, "", unreal.MaterialProperty.MP_OPACITY if translucent else unreal.MaterialProperty.MP_OPACITY_MASK)
    mel.connect_material_property(detail_switch, "", unreal.MaterialProperty.MP_NORMAL)
    mel.connect_material_property(metal, "", unreal.MaterialProperty.MP_METALLIC)
    mel.connect_material_property(rough_switch, "", unreal.MaterialProperty.MP_ROUGHNESS)
    mel.connect_material_property(ao_source, "", unreal.MaterialProperty.MP_AMBIENT_OCCLUSION)
    mel.connect_material_property(emit_mul, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    mel.connect_material_property(height_vec, "", unreal.MaterialProperty.MP_WORLD_POSITION_OFFSET)
    mel.recompile_material(material)
    unreal.EditorAssetLibrary.save_loaded_asset(material)
    return material


def packing_flags(channels):
    text = channels or ""
    rough_alpha = "Roughness(a)" in text
    ao_blue = "AO(b)" in text or "AO(B)" in text
    return rough_alpha, ao_blue


def dds_dxgi(png_path):
    # The game ships these maps as BC5 (DXN, DXGI 83) or BC4 (DXT5A, DXGI 80).
    # Their .td format_descr names logical channels (r, a), but BC5 only stores
    # two physical channels (R, G) and BC4 one (R); the engine swizzles in the
    # shader (swizzleTexInShader). The exported PNG therefore has the "a" data in
    # G (BC5) or R (BC4) and a constant alpha of 255. Read the DDS beside the PNG.
    if not png_path:
        return None
    dds = os.path.splitext(png_path)[0] + ".dds"
    try:
        with open(dds, "rb") as handle:
            head = handle.read(132)
    except OSError:
        return None
    if len(head) < 132 or head[84:88] != b"DX10":
        return None
    return int.from_bytes(head[128:132], "little")


def stored_channel(sidecar_path, entry, meaning):
    # Exporters from this commit on write packing[].stored: where the logical
    # channel physically is in the PNG (BC5 Roughness(a) -> g, BC4 AO(a) -> r).
    for pack in (entry or {}).get("packing") or []:
        if pack.get("meaning") == meaning and pack.get("stored"):
            return pack["stored"]
    # Older sidecars: fall back to the DDS written beside the PNG.
    return logical_alpha_lives_in(texture_file(sidecar_path, entry) if entry else None)


def logical_alpha_lives_in(png_path):
    fmt = dds_dxgi(png_path)
    if fmt == 83:
        return "g"
    if fmt == 80:
        return "r"
    return "a"


def first_map(slot, role):
    for item in slot.get("maps", []):
        if item.get("role") == role and item.get("file") and not item.get("missing"):
            return item
    return None


def is_sim_proxy(slot):
    # SM2 cloth (nvcloth PropClothSim) has a low-poly simulation mesh (simMeshName)
    # that only drives the visible *_REND meshes via InitSimToRenderSkinning.
    # The game never draws it; the export gives it __default_material__.
    name = slot.get("slot", "")
    diff = first_map(slot, "diffuse") or {}
    return "_SIM" in name.upper() or diff.get("name") == "__default_material__"


def make_hidden(folder):
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    asset_name = "M_SM2_ClothSimHidden"
    existing = folder + "/" + asset_name
    if unreal.EditorAssetLibrary.does_asset_exist(existing):
        unreal.EditorAssetLibrary.delete_asset(existing)
    material = tools.create_asset(asset_name, folder, unreal.Material, unreal.MaterialFactoryNew())
    material.set_editor_property("blend_mode", unreal.BlendMode.BLEND_MASKED)
    zero = expr(material, unreal.MaterialExpressionConstant, -300, 0)
    zero.set_editor_property("r", 0.0)
    unreal.MaterialEditingLibrary.connect_material_property(zero, "", unreal.MaterialProperty.MP_OPACITY_MASK)
    unreal.MaterialEditingLibrary.recompile_material(material)
    unreal.EditorAssetLibrary.save_loaded_asset(material)
    return material


def build(sidecar_path, folder):
    data = load_sidecar(sidecar_path)
    tools = unreal.AssetToolsHelpers.get_asset_tools()
    unreal.EditorAssetLibrary.make_directory(folder)
    master = make_master(folder)
    translucent_master = make_master(folder, "M_SM2_Translucent", translucent=True)
    hidden = make_hidden(folder)
    cache = {}
    instances = {}

    def texture_for(entry, normal):
        if entry is None:
            return None
        disk = texture_file(sidecar_path, entry)
        key = (disk, bool(entry.get("srgb")), normal)
        if key not in cache:
            cache[key] = import_texture(disk, folder + "/Textures", entry["name"], bool(entry.get("srgb")), normal)
        return cache[key]

    for slot in data.get("slots", []):
        name = slot["slot"]
        if is_sim_proxy(slot):
            stale = folder + "/MI_" + "".join(ch if ch.isalnum() or ch == "_" else "_" for ch in name)
            if unreal.EditorAssetLibrary.does_asset_exist(stale):
                unreal.EditorAssetLibrary.delete_asset(stale)
            instances[name] = hidden
            unreal.log("SM2 hidden cloth sim proxy " + name)
            continue
        safe = "".join(ch if ch.isalnum() or ch == "_" else "_" for ch in name)
        asset_name = "MI_" + safe
        full = folder + "/" + asset_name
        if unreal.EditorAssetLibrary.does_asset_exist(full):
            unreal.EditorAssetLibrary.delete_asset(full)
        instance = tools.create_asset(asset_name, folder, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        instance.set_editor_property("parent", translucent_master if slot.get("blend") == "translucent" else master)
        diffuse = first_map(slot, "diffuse")
        normal = first_map(slot, "normal")
        packed = first_map(slot, "specular")
        emissive = first_map(slot, "emissive")
        detail = first_map(slot, "detail") or first_map(slot, "wrinkleNormal")
        detail_mask = first_map(slot, "detailMask")
        height = first_map(slot, "height")
        ao = first_map(slot, "ao")
        if detail_mask and "Heightmap" in (detail_mask.get("channels") or ""):
            height = height or detail_mask
            detail_mask = None
        assignments = (
            ("Diffuse", texture_for(diffuse, False)),
            ("Normal", texture_for(normal, True)),
            ("Packed", texture_for(packed, False)),
            ("Emissive", texture_for(emissive, False)),
            ("Detail", texture_for(detail, True)),
            ("DetailMask", texture_for(detail_mask, False)),
            ("Height", texture_for(height, False)),
            ("Ao", texture_for(ao, False)),
        )
        for parameter, asset in assignments:
            if asset is not None:
                unreal.MaterialEditingLibrary.set_material_instance_texture_parameter_value(instance, parameter, asset)
        rough_alpha, ao_blue = packing_flags((packed or {}).get("channels"))
        ao_channels = (ao or {}).get("channels") or ""
        if rough_alpha and packed and stored_channel(sidecar_path, packed, "roughness") != "a":
            rough_alpha = False  # BC5: Roughness(a) is physically in G
        unreal.MaterialEditingLibrary.set_material_instance_static_switch_parameter_value(instance, "RoughnessInAlpha", rough_alpha)
        unreal.MaterialEditingLibrary.set_material_instance_static_switch_parameter_value(instance, "AoInBlue", ao_blue if packed else False)
        unreal.MaterialEditingLibrary.set_material_instance_static_switch_parameter_value(instance, "UseSeparateAo", ao is not None)
        ao_in_alpha = "AO(a)" in ao_channels or "AO(A)" in ao_channels
        if ao_in_alpha and ao and stored_channel(sidecar_path, ao, "ao") == "r":
            ao_in_alpha = False  # BC4: AO(a) is physically in R
        unreal.MaterialEditingLibrary.set_material_instance_static_switch_parameter_value(instance, "AoInAlpha", ao_in_alpha)
        unreal.MaterialEditingLibrary.set_material_instance_static_switch_parameter_value(instance, "HasDetail", detail is not None and detail_mask is not None)
        unreal.MaterialEditingLibrary.set_material_instance_static_switch_parameter_value(instance, "UseOpacityMask", slot.get("blend") in ("masked", "translucent"))
        if emissive is not None:
            unreal.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(instance, "EmissiveStrength", 1.0)
        unreal.EditorAssetLibrary.save_loaded_asset(instance)
        instances[name] = instance
        unreal.log("SM2 instance " + name)

    mesh_name = data.get("mesh") or "mesh"
    candidates = [
        folder + "/" + mesh_name,
        folder + "/" + mesh_name + "_Skeleton",
    ]
    # FBX imports as a SkeletalMesh for characters and a StaticMesh for props,
    # weapons, and vehicles (no skin weights). Prefer the asset named after the mesh.
    found = None
    registry = unreal.AssetRegistryHelpers.get_asset_registry()
    for asset_data in registry.get_assets_by_path(folder, recursive=True):
        if str(asset_data.asset_class_path.asset_name) in ("SkeletalMesh", "StaticMesh"):
            loaded = asset_data.get_asset()
            if loaded is not None and (found is None or loaded.get_name() == mesh_name):
                found = loaded
                if loaded.get_name() == mesh_name:
                    break
    if found is None:
        unreal.log_warning("SM2 imported the instances. No skeletal or static mesh was found under " + folder + ". Assign them on the mesh after the FBX import.")
        return
    assigned = 0
    if isinstance(found, unreal.StaticMesh):
        for index, material_slot in enumerate(list(found.get_editor_property("static_materials"))):
            slot_name = str(material_slot.get_editor_property("material_slot_name"))
            if slot_name in instances:
                found.set_material(index, instances[slot_name])
                assigned += 1
    else:
        slots = list(found.get_editor_property("materials"))
        for index, material_slot in enumerate(slots):
            slot_name = str(material_slot.get_editor_property("material_slot_name"))
            if slot_name in instances:
                material_slot.set_editor_property("material_interface", instances[slot_name])
                slots[index] = material_slot
                assigned += 1
        found.set_editor_property("materials", slots)
    unreal.EditorAssetLibrary.save_loaded_asset(found)
    unreal.log("SM2 assigned " + str(assigned) + " of " + str(len(instances)) + " instances to " + found.get_name())

def main():
    args = [arg for arg in sys.argv[1:] if not arg.startswith("-")]
    # Unreal inserts the project path and this script path before user arguments.
    json_path = None
    folder = None
    for arg in args:
        if arg.endswith(".json"):
            json_path = arg
        elif arg.startswith("/Game"):
            folder = arg
    if json_path is None:
        here = os.path.dirname(os.path.abspath(__file__))
        found = [name for name in os.listdir(here) if name.endswith(".materials.json")]
        if len(found) == 1:
            json_path = os.path.join(here, found[0])
    if json_path is None:
        sys.stderr.write("Pass the .materials.json path and an optional /Game folder.\n")
        return
    data = load_sidecar(json_path)
    if folder is None:
        folder = content_path(data.get("mesh") or "SM2")
    build(json_path, folder)


if __name__ == "__main__":
    main()
