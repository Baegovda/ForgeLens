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


def scalar(material, name, value, x, y):
    node = expr(material, unreal.MaterialExpressionScalarParameter, x, y)
    node.set_editor_property("parameter_name", name)
    node.set_editor_property("default_value", value)
    return node


def const(material, value, x, y):
    node = expr(material, unreal.MaterialExpressionConstant, x, y)
    node.set_editor_property("r", value)
    return node


def switch(material, name, off, on, x, y):
    # Every node that shares a static switch name shares its value on the instance.
    node = expr(material, unreal.MaterialExpressionStaticSwitchParameter, x, y)
    node.set_editor_property("parameter_name", name)
    node.set_editor_property("default_value", False)
    link(off, "", node, "False")
    link(on, "", node, "True")
    return node


def scale_bias(material, value, scale_name, scale, bias_name, bias, x, y):
    # saturate(value * scale + bias): the scale/bias pair of glt.second_spec.metalness/roughness.
    k = scalar(material, scale_name, scale, x - 220, y)
    b = scalar(material, bias_name, bias, x - 220, y + 80)
    mul = expr(material, unreal.MaterialExpressionMultiply, x, y)
    link(value, "", mul, "A")
    link(k, "", mul, "B")
    add = expr(material, unreal.MaterialExpressionAdd, x + 140, y)
    link(mul, "", add, "A")
    link(b, "", add, "B")
    sat = expr(material, unreal.MaterialExpressionSaturate, x + 280, y)
    link(add, "", sat, "")
    return sat


def second_spec_layer(material, base, metal, rough):
    # glt.second_spec (sm2_retail.exe: glt+0x94, permutation GLT_USE_SECOND_SPEC). The game adds a second
    # specular lobe with its own F0 color (metallicTint), metalness and roughness. Default Lit has one lobe,
    # so UseSecondSpec blends the single lobe toward the second one by SecondSpecWeight * second metalness.
    # UseSecondSpec off (the default) passes base, metal, and rough through unchanged.
    x, y = 200, -900
    m2 = scale_bias(material, metal, "SecondMetalnessScale", 1.0, "SecondMetalnessBias", 0.0, x, y)
    r2 = scale_bias(material, rough, "SecondRoughnessScale", 0.0, "SecondRoughnessBias", 0.7, x, y + 220)
    tint = expr(material, unreal.MaterialExpressionVectorParameter, x, y - 220)
    tint.set_editor_property("parameter_name", "SecondSpecTint")
    tint.set_editor_property("default_value", unreal.LinearColor(1.0, 1.0, 1.0, 0.0))
    weight = scalar(material, "SecondSpecWeight", 0.5, x + 280, y - 120)
    w = expr(material, unreal.MaterialExpressionMultiply, x + 500, y - 60)
    link(m2, "", w, "A")
    link(weight, "", w, "B")
    # Tint alpha (the .td A byte) gates recoloring: A=0 is a white lobe that only moves metal/rough.
    w_tint = expr(material, unreal.MaterialExpressionMultiply, x + 560, y - 200)
    link(w, "", w_tint, "A")
    link(tint, "A", w_tint, "B")
    base2 = expr(material, unreal.MaterialExpressionLinearInterpolate, x + 700, y - 220)
    link(base, "", base2, "A")
    link(tint, "", base2, "B")
    link(w_tint, "", base2, "Alpha")
    one = const(material, 1.0, x + 500, y + 60)
    metal2 = expr(material, unreal.MaterialExpressionLinearInterpolate, x + 700, y)
    link(metal, "", metal2, "A")
    link(one, "", metal2, "B")
    link(w, "", metal2, "Alpha")
    rough2 = expr(material, unreal.MaterialExpressionLinearInterpolate, x + 700, y + 220)
    link(rough, "", rough2, "A")
    link(r2, "", rough2, "B")
    link(w, "", rough2, "Alpha")
    return (
        switch(material, "UseSecondSpec", base, base2, x + 900, y - 220),
        switch(material, "UseSecondSpec", metal, metal2, x + 900, y),
        switch(material, "UseSecondSpec", rough, rough2, x + 900, y + 220),
    )


def aniso_layer(material):
    # glt.anisotropic_spec (glt+0x108, permutations GLT_USE_ANISO_SPEC / GLT_USE_ANISO_SPEC_DIR_MAP, texture slot
    # GLT_TEX_ANISO_DIR). Mapped to Default Lit Anisotropy + Tangent. Off: anisotropy 0, tangent +X, specular 0.5,
    # which is the same shading as leaving the pins unconnected. extra{} (the shifted second hair lobe) has no
    # Default Lit equivalent; it stays in the sidecar.
    anisotropy_pin = getattr(unreal.MaterialProperty, "MP_ANISOTROPY", None)
    tangent_pin = getattr(unreal.MaterialProperty, "MP_TANGENT", None)
    if anisotropy_pin is None or tangent_pin is None:
        unreal.log_warning("SM2 this Unreal has no Anisotropy/Tangent material pins; anisotropic_spec is skipped")
        return
    mel = unreal.MaterialEditingLibrary
    x, y = 200, 1600
    amount = scalar(material, "AnisoAnisotropy", 0.5, x, y)
    zero = const(material, 0.0, x, y + 80)
    mel.connect_material_property(switch(material, "UseAnisoSpec", zero, amount, x + 300, y), "", anisotropy_pin)

    # Tangent-space direction from angle (degrees): (cos, sin, 0). 90, the engine default, runs along V.
    angle = scalar(material, "AnisoAngle", 90.0, x - 600, y + 220)
    cos_node = expr(material, unreal.MaterialExpressionCosine, x - 400, y + 200)
    cos_node.set_editor_property("period", 360.0)
    link(angle, "", cos_node, "")
    sin_node = expr(material, unreal.MaterialExpressionSine, x - 400, y + 280)
    sin_node.set_editor_property("period", 360.0)
    link(angle, "", sin_node, "")
    xy = expr(material, unreal.MaterialExpressionAppendVector, x - 240, y + 240)
    link(cos_node, "", xy, "A")
    link(sin_node, "", xy, "B")
    from_angle = expr(material, unreal.MaterialExpressionAppendVector, x - 100, y + 240)
    link(xy, "", from_angle, "A")
    link(zero, "", from_angle, "B")

    # dirMap (_dm, usage "MD", neutral 127): RG direction encoded as 0..1.
    dir_tex = param_tex(material, "AnisoDir", x - 900, y + 420, unreal.MaterialSamplerType.SAMPLERTYPE_MASKS)
    rg = expr(material, unreal.MaterialExpressionComponentMask, x - 600, y + 420)
    rg.set_editor_property("r", True)
    rg.set_editor_property("g", True)
    rg.set_editor_property("b", False)
    rg.set_editor_property("a", False)
    link(dir_tex, "RGBA", rg, "")
    signed = expr(material, unreal.MaterialExpressionConstantBiasScale, x - 440, y + 420)
    signed.set_editor_property("bias", -0.5)
    signed.set_editor_property("scale", 2.0)
    link(rg, "", signed, "")
    from_map3 = expr(material, unreal.MaterialExpressionAppendVector, x - 280, y + 420)
    link(signed, "", from_map3, "A")
    link(zero, "", from_map3, "B")
    from_map = expr(material, unreal.MaterialExpressionNormalize, x - 120, y + 420)
    link(from_map3, "", from_map, "")
    direction = switch(material, "UseAnisoDirMap", from_angle, from_map, x + 100, y + 300)
    plus_x = expr(material, unreal.MaterialExpressionConstant3Vector, x + 100, y + 180)
    plus_x.set_editor_property("constant", unreal.LinearColor(1.0, 0.0, 0.0, 0.0))
    mel.connect_material_property(switch(material, "UseAnisoSpec", plus_x, direction, x + 300, y + 240), "", tangent_pin)

    # intensity scales the highlight. Default Lit's Specular is 0.5 at intensity 1.
    intensity = scalar(material, "AnisoIntensity", 1.0, x, y + 600)
    half = const(material, 0.5, x, y + 680)
    spec = expr(material, unreal.MaterialExpressionMultiply, x + 160, y + 600)
    link(intensity, "", spec, "A")
    link(half, "", spec, "B")
    spec_sat = expr(material, unreal.MaterialExpressionSaturate, x + 300, y + 600)
    link(spec, "", spec_sat, "")
    mel.connect_material_property(switch(material, "UseAnisoSpec", half, spec_sat, x + 460, y + 640), "", unreal.MaterialProperty.MP_SPECULAR)


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

    # glt.albedo_tint from the .td variant (sidecar albedoTint). White when unset.
    tint = expr(material, unreal.MaterialExpressionVectorParameter, -500, -320)
    tint.set_editor_property("parameter_name", "AlbedoTint")
    tint.set_editor_property("default_value", unreal.LinearColor(1.0, 1.0, 1.0, 1.0))
    tinted = expr(material, unreal.MaterialExpressionMultiply, -200, -260)
    link(diffuse, "RGB", tinted, "A")
    link(tint, "", tinted, "B")
    base_out, metal_out, rough_out = second_spec_layer(material, tinted, metal, rough_switch)
    mel.connect_material_property(base_out, "", unreal.MaterialProperty.MP_BASE_COLOR)
    mel.connect_material_property(opacity, "", unreal.MaterialProperty.MP_OPACITY if translucent else unreal.MaterialProperty.MP_OPACITY_MASK)
    mel.connect_material_property(detail_switch, "", unreal.MaterialProperty.MP_NORMAL)
    mel.connect_material_property(metal_out, "", unreal.MaterialProperty.MP_METALLIC)
    mel.connect_material_property(rough_out, "", unreal.MaterialProperty.MP_ROUGHNESS)
    aniso_layer(material)
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


def srgb_to_linear(c):
    return pow(max(0.0, min(1.0, float(c))), 2.2)


def apply_spec_layers(instance, slot, texture_for):
    mel = unreal.MaterialEditingLibrary
    second = slot.get("secondSpec")
    mel.set_material_instance_static_switch_parameter_value(instance, "UseSecondSpec", bool(second))
    if second:
        # metallicTint is R, G, B, A in 0..1 (engine default A=0, white). RGB is sRGB like albedo_tint; A stays linear.
        r, g, b, a = (list(second.get("metallicTint") or [1, 1, 1, 0]) + [1, 1, 1, 0])[:4]
        lin = [srgb_to_linear(c) for c in (r, g, b)]
        mel.set_material_instance_vector_parameter_value(instance, "SecondSpecTint", unreal.LinearColor(lin[0], lin[1], lin[2], float(a)))
        metal = second.get("metalness") or {}
        rough = second.get("roughness") or {}
        mel.set_material_instance_scalar_parameter_value(instance, "SecondMetalnessScale", float(metal.get("scale", 1.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "SecondMetalnessBias", float(metal.get("bias", 0.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "SecondRoughnessScale", float(rough.get("scale", 0.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "SecondRoughnessBias", float(rough.get("bias", 0.7)))
    aniso = slot.get("anisotropicSpec")
    mel.set_material_instance_static_switch_parameter_value(instance, "UseAnisoSpec", bool(aniso))
    dir_asset = None
    if aniso:
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoAnisotropy", max(-1.0, min(1.0, float(aniso.get("anisotropy", 0.5)))))
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoAngle", float(aniso.get("angle", 90.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoIntensity", float(aniso.get("intensity", 1.0)))
        if aniso.get("dirMap"):
            dir_asset = texture_for(first_map(slot, "anisoDir"), False)
            if dir_asset is not None:
                mel.set_material_instance_texture_parameter_value(instance, "AnisoDir", dir_asset)
    mel.set_material_instance_static_switch_parameter_value(instance, "UseAnisoDirMap", dir_asset is not None)


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
        albedo_tint = slot.get("albedoTint")
        if albedo_tint:
            # The tint bytes are sRGB like the albedo they multiply; Unreal multiplies in linear.
            lin = [pow(float(c), 2.2) for c in albedo_tint[:3]]
            unreal.MaterialEditingLibrary.set_material_instance_vector_parameter_value(instance, "AlbedoTint", unreal.LinearColor(lin[0], lin[1], lin[2], 1.0))
        apply_spec_layers(instance, slot, texture_for)
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
