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
# (The LibSaber dev tools in tools/ue pass the folder explicitly: /Game/SM2_Work/Characters/<mesh>
# for skeletal characters, /Game/SM2_Work/Props/<mesh> for props; see HANDOFF.md "UE layout".)

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


_IMPORTED = [0]
# The whole build runs inside one editor tick, so every imported texture's RHI resource and its
# build buffers stay alive until the script returns. 900 2K maps in one tick (an unfiltered
# constructor tpl) aborted the D3D11 RHI thread. Collect garbage every GC_EVERY imports.
GC_EVERY = 32


def import_texture(disk, dest, name, srgb, normal):
    _IMPORTED[0] += 1
    if _IMPORTED[0] % GC_EVERY == 0:
        unreal.SystemLibrary.collect_garbage()
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


def op(material, cls, a, b, x, y):
    node = expr(material, cls, x, y)
    link(a, "", node, "A")
    link(b, "", node, "B")
    return node


def mul(material, a, b, x, y):
    return op(material, unreal.MaterialExpressionMultiply, a, b, x, y)


def add(material, a, b, x, y):
    return op(material, unreal.MaterialExpressionAdd, a, b, x, y)


def lerp(material, a, b, alpha, x, y):
    node = expr(material, unreal.MaterialExpressionLinearInterpolate, x, y)
    link(a, "", node, "A")
    link(b, "", node, "B")
    link(alpha, "", node, "Alpha")
    return node


def unary(material, cls, value, x, y):
    node = expr(material, cls, x, y)
    link(value, "", node, "")
    return node


def vec3(material, rgb, x, y):
    node = expr(material, unreal.MaterialExpressionConstant3Vector, x, y)
    node.set_editor_property("constant", unreal.LinearColor(rgb[0], rgb[1], rgb[2], 0.0))
    return node


def mask(material, value, channels, x, y, pin=""):
    node = expr(material, unreal.MaterialExpressionComponentMask, x, y)
    for ch in "rgba":
        node.set_editor_property(ch, ch in channels)
    link(value, pin, node, "")
    return node


def aniso_remap(material, rough, anisotropy, x, y):
    # SM2 CB_STATIC_GLT packer FUN_1423ba150 (0x1423ba35c-0x1423ba3ec): PS_REG_GLT_ANISO_PARAMS.zw =
    # k = 1 - clamp(anisotropy, 0, 0.95) (constants 0x143c844ac = 0.95, 0x143c844b4 = 1.0). The glt pixel
    # shader uses alpha_T = (roughness * k)^2 and alpha_B = roughness^2. Substrate uses
    # alpha_T = alpha * (1 + A), alpha_B = alpha * (1 - A). Both axes match with
    # A = (k^2 - 1) / (k^2 + 1) and roughness' = roughness * sqrt((k^2 + 1) / 2).
    clamp = expr(material, unreal.MaterialExpressionClamp, x, y)
    clamp.set_editor_property("min_default", 0.0)
    clamp.set_editor_property("max_default", 0.95)
    link(anisotropy, "", clamp, "")
    k = unary(material, unreal.MaterialExpressionOneMinus, clamp, x + 140, y)
    k2 = mul(material, k, k, x + 280, y)
    one = const(material, 1.0, x + 280, y + 80)
    num = op(material, unreal.MaterialExpressionSubtract, k2, one, x + 420, y)
    den = add(material, k2, one, x + 420, y + 80)
    amount = op(material, unreal.MaterialExpressionDivide, num, den, x + 560, y)
    half = const(material, 0.5, x + 420, y + 160)
    widen = unary(material, unreal.MaterialExpressionSquareRoot, mul(material, den, half, x + 560, y + 120), x + 700, y + 120)
    rough_out = unary(material, unreal.MaterialExpressionSaturate, mul(material, rough, widen, x + 840, y + 120), x + 980, y + 120)
    return rough_out, amount


def spec_layers(material, albedo, metal, rough, normal, emissive, albedo_raw=None, metal_raw=None, rough_raw=None, ao=None):
    # Builds the Substrate front material from the glt pixel shader (glt.sdc, e.g. PS 03840_0 / 00004_1):
    #   base lobe:   F0 = lerp(dielectric, albedo, metalness), roughness, anisotropy (UseAnisoSpec)
    #   second lobe: F0_2 = lerp(0.04, SecondSpecTint.rgb * albedo, m2), m2 = saturate(metal * scale + bias),
    #                r2 = saturate(rough * scale + bias), F90_2 = saturate(50 * F0_2.g), added on top of the base
    #                (no weight in the shader; Substrate Add). With aniso on, the second lobe is the extra{} lobe:
    #                its own anisotropy, intensity, and tangent shifted toward N by extra.shift.
    # albedo/metal/rough are the base-lobe inputs (glt metalness/roughness scale/bias and metallicTint applied);
    # the second lobe reads the raw packed metal/rough and the untinted albedo (PS 00004_1 lines 405-427).
    albedo_raw = albedo if albedo_raw is None else albedo_raw
    metal_raw = metal if metal_raw is None else metal_raw
    rough_raw = rough if rough_raw is None else rough_raw
    mel = unreal.MaterialEditingLibrary
    x, y = 300, -900
    zero = const(material, 0.0, x, y - 300)
    one = const(material, 1.0, x, y - 220)

    # APPROXIMATION (DirectAo*): the glt light loop scales every local light, diffuse and both specular lobes, by
    # saturate(2 * AO' - 1 + NdotL) (PS 00004_1 line 4167, _7685), AO' = lerp(1, vertexAO, w1) * lerp(1, AO, w2)
    # (line 3301, _2735; w1/w2 are per-instance data, CPU writer not located). Substrate has no per-light hook, so
    # the factor is replaced by its NdotL-weighted mean over NdotL in [0, 1], f(AO) = 2 * int NdotL * factor:
    #   AO >= 0.5: 1 - u^3 / 3, u = 2 - 2 AO;   AO < 0.5: 2/3 - d + d^3 / 3, d = 1 - 2 AO   (f(1) = 1, f(0.5) = 2/3)
    # and baked into the slab albedo / F0 / F90. DirectAoStrength = 0 (master default) disables it; the importer
    # sets 1 on the hair slots (aniso + dirMap) only.
    occ = one
    if ao is not None:
        ao_w = lerp(material, one, ao, scalar(material, "DirectAoStrength", 0.0, x - 900, y - 600), x - 760, y - 600)
        two_a = mul(material, ao_w, const(material, 2.0, x - 760, y - 520), x - 620, y - 600)
        third = const(material, 1.0 / 3.0, x - 620, y - 680)
        u = op(material, unreal.MaterialExpressionSubtract, const(material, 2.0, x - 620, y - 760), two_a, x - 480, y - 760)
        hi = op(material, unreal.MaterialExpressionSubtract, one, mul(material, mul(material, mul(material, u, u, x - 340, y - 760), u, x - 200, y - 760), third, x - 60, y - 760), x + 80, y - 760)
        d = op(material, unreal.MaterialExpressionSubtract, one, two_a, x - 480, y - 600)
        lo = add(material, op(material, unreal.MaterialExpressionSubtract, const(material, 2.0 / 3.0, x - 340, y - 520), d, x - 200, y - 600), mul(material, mul(material, mul(material, d, d, x - 340, y - 440), d, x - 200, y - 440), third, x - 60, y - 440), x + 80, y - 600)
        sel = unary(material, unreal.MaterialExpressionSaturate, unary(material, unreal.MaterialExpressionFloor, add(material, ao_w, const(material, 0.5, x - 620, y - 360), x - 480, y - 360), x - 340, y - 360), x - 200, y - 360)
        occ = unary(material, unreal.MaterialExpressionSaturate, lerp(material, lo, hi, sel, x + 220, y - 600), x + 360, y - 600)

    # Aniso direction (tangent space): (cos, sin, 0) of the clamped angle, or the dir map.
    angle = scalar(material, "AnisoAngle", 90.0, x - 700, y + 1500)
    angle_c = expr(material, unreal.MaterialExpressionClamp, x - 560, y + 1500)
    angle_c.set_editor_property("min_default", 0.0)
    angle_c.set_editor_property("max_default", 180.0)  # 0x1423ba302-0x1423ba30c: clamp(angle, 0, 180)
    link(angle, "", angle_c, "")
    cos_node = expr(material, unreal.MaterialExpressionCosine, x - 420, y + 1460)
    cos_node.set_editor_property("period", 360.0)
    link(angle_c, "", cos_node, "")
    sin_node = expr(material, unreal.MaterialExpressionSine, x - 420, y + 1540)
    sin_node.set_editor_property("period", 360.0)
    link(angle_c, "", sin_node, "")
    xy = op(material, unreal.MaterialExpressionAppendVector, cos_node, sin_node, x - 280, y + 1500)
    from_angle = op(material, unreal.MaterialExpressionAppendVector, xy, zero, x - 140, y + 1500)
    dir_tex = param_tex(material, "AnisoDir", x - 900, y + 1700, unreal.MaterialSamplerType.SAMPLERTYPE_MASKS)
    rg = mask(material, dir_tex, "rg", x - 600, y + 1700, "RGBA")
    signed = expr(material, unreal.MaterialExpressionConstantBiasScale, x - 440, y + 1700)
    signed.set_editor_property("bias", -0.5)
    signed.set_editor_property("scale", 2.0)
    link(rg, "", signed, "")
    from_map = unary(material, unreal.MaterialExpressionNormalize, op(material, unreal.MaterialExpressionAppendVector, signed, zero, x - 280, y + 1700), x - 140, y + 1700)
    direction = switch(material, "UseAnisoDirMap", from_angle, from_map, x, y + 1600)
    plus_x = vec3(material, (1.0, 0.0, 0.0), x, y + 1450)
    tangent = switch(material, "UseAnisoSpec", plus_x, direction, x + 200, y + 1550)

    # Base lobe. Dielectric F0 0.04 (Default Lit Specular 0.5). anisotropic_spec.intensity multiplies the
    # base D term (PS_REG_GLT_ANISO_PARAMS.x = glt+0x124, raw), applied here to F0/F90.
    # Dielectric F0 = 0.16 * reflectance^2 (PS 00004_1 line 3646; glt.reflectance, default 0.5 -> 0.04).
    reflectance = scalar(material, "Reflectance", 0.5, x - 300, y)
    dielectric = mul(material, mul(material, reflectance, reflectance, x - 160, y), const(material, 0.16, x - 160, y + 60), x - 20, y)
    f0 = lerp(material, dielectric, albedo, metal, x + 200, y)
    diffuse = mul(material, mul(material, albedo, unary(material, unreal.MaterialExpressionOneMinus, metal, x + 200, y + 100), x + 400, y + 80), occ, x + 540, y + 80)
    # mtl1-16: the game multiplies D by the intensity for DIRECT lights only (HANDOFF 1b/aniso: D = min(..,128) *
    # intensity; second lobe direct lights only). In Substrate F0 also scales the sky/reflection-capture term, so
    # F0 * 4 (aniso intensity 4, SecondSpecGain 4) turned paint (hellbrute blue albedo) into a cyan mirror.
    # SpecGainToF0 (default 0) blends the gains out of F0/F90: gain_eff = lerp(1, gain, SpecGainToF0); 1 = old look.
    to_f0 = scalar(material, "SpecGainToF0", 0.0, x - 300, y + 380)
    intensity = lerp(material, one, scalar(material, "AnisoIntensity", 1.0, x, y + 300), to_f0, x + 200, y + 300)
    f0_i = unary(material, unreal.MaterialExpressionSaturate, mul(material, f0, intensity, x + 400, y + 260), x + 540, y + 260)
    f90_i = unary(material, unreal.MaterialExpressionSaturate, intensity, x + 540, y + 340)
    amount = scalar(material, "AnisoAnisotropy", 0.5, x, y + 450)
    rough_a, aniso_a = aniso_remap(material, rough, amount, x + 200, y + 450)
    base = expr(material, unreal.MaterialExpressionSubstrateSlabBSDF, x + 1400, y)
    link(diffuse, "", base, "Diffuse Albedo")
    base_f0 = mul(material, switch(material, "UseAnisoSpec", f0, f0_i, x + 1200, y + 200), occ, x + 1300, y + 200)
    base_f90 = mul(material, switch(material, "UseAnisoSpec", one, f90_i, x + 1200, y + 300), occ, x + 1300, y + 300)
    base_rough = switch(material, "UseAnisoSpec", rough, rough_a, x + 1200, y + 400)
    base_aniso = switch(material, "UseAnisoSpec", zero, aniso_a, x + 1200, y + 500)
    link(base_f0, "", base, "F0")
    link(base_f90, "", base, "F90")
    link(base_rough, "", base, "Roughness")
    link(base_aniso, "", base, "Anisotropy")
    link(normal, "", base, "Normal")
    link(tangent, "", base, "Tangent")
    link(emissive, "", base, "Emissive Color")

    # Second lobe (glt.second_spec, cb6 OBJ_MTLBLEND_LAYER_CONST rel rows 12/13 + row 1.w).
    y2 = y - 1400
    m2 = scale_bias(material, metal_raw, "SecondMetalnessScale", 1.0, "SecondMetalnessBias", 0.0, x + 220, y2)
    r2 = scale_bias(material, rough_raw, "SecondRoughnessScale", 0.0, "SecondRoughnessBias", 0.7, x + 220, y2 + 220)
    tint = expr(material, unreal.MaterialExpressionVectorParameter, x, y2 - 220)
    tint.set_editor_property("parameter_name", "SecondSpecTint")
    tint.set_editor_property("default_value", unreal.LinearColor(1.0, 1.0, 1.0, 0.0))
    tinted = mul(material, mask(material, tint, "rgb", x + 200, y2 - 220), albedo_raw, x + 400, y2 - 220)
    f0_2 = lerp(material, vec3(material, (0.04, 0.04, 0.04), x + 400, y2 - 320), tinted, m2, x + 600, y2 - 220)
    # The non-aniso second lobe has no visibility term (PS 03840_0: D * F * NdotL); the aniso one keeps the
    # 1/(4 pi) constant (PS 00004_1). SecondSpecGain = 4 restores that level against Substrate's
    # visibility (V = 1/4 at normal incidence). The instance sets 1 when the aniso (extra) lobe is used.
    gain = scalar(material, "SecondSpecGain", 4.0, x + 600, y2 - 60)
    extra_i = scalar(material, "AnisoExtraIntensity", 1.0, x + 600, y2 + 20)
    gain_all = lerp(material, one, mul(material, gain, switch(material, "UseAnisoSpec", one, extra_i, x + 760, y2 + 20), x + 900, y2 - 40), to_f0, x + 960, y2 - 40)
    f90_2 = unary(material, unreal.MaterialExpressionSaturate, mul(material, mask(material, f0_2, "g", x + 760, y2 - 140), const(material, 50.0, x + 760, y2 - 100), x + 900, y2 - 140), x + 1040, y2 - 140)
    f0_2g = unary(material, unreal.MaterialExpressionSaturate, mul(material, f0_2, gain_all, x + 1040, y2 - 220), x + 1180, y2 - 220)
    f90_2g = unary(material, unreal.MaterialExpressionSaturate, mul(material, f90_2, gain_all, x + 1180, y2 - 140), x + 1320, y2 - 140)
    extra_a = scalar(material, "AnisoExtraAnisotropy", 0.5, x, y2 + 450)
    r2_a, a2 = aniso_remap(material, r2, extra_a, x + 200, y2 + 450)
    # extra.shift (glt+0x130 -> CB_STATIC_GLT row 4.y, 0x1423ba3f4): T2 = normalize(T + shift * N).
    # Substrate re-orthogonalises the tangent, so the whole frame is rotated: N2 = normalize(N - shift * T).
    shift = scalar(material, "AnisoExtraShift", 0.2, x, y2 + 700)
    t2 = unary(material, unreal.MaterialExpressionNormalize, add(material, tangent, mul(material, normal, shift, x + 200, y2 + 700), x + 360, y2 + 700), x + 500, y2 + 700)
    n2 = unary(material, unreal.MaterialExpressionNormalize, op(material, unreal.MaterialExpressionSubtract, normal, mul(material, tangent, shift, x + 200, y2 + 800), x + 360, y2 + 800), x + 500, y2 + 800)
    second = expr(material, unreal.MaterialExpressionSubstrateSlabBSDF, x + 1400, y2)
    link(vec3(material, (0.0, 0.0, 0.0), x + 1200, y2 - 300), "", second, "Diffuse Albedo")
    link(mul(material, f0_2g, occ, x + 1300, y2 - 220), "", second, "F0")
    link(mul(material, f90_2g, occ, x + 1300, y2 - 140), "", second, "F90")
    link(switch(material, "UseAnisoSpec", r2, r2_a, x + 1200, y2 + 300), "", second, "Roughness")
    link(switch(material, "UseAnisoSpec", zero, a2, x + 1200, y2 + 400), "", second, "Anisotropy")
    link(switch(material, "UseAnisoSpec", normal, n2, x + 1200, y2 + 500), "", second, "Normal")
    link(switch(material, "UseAnisoSpec", tangent, t2, x + 1200, y2 + 600), "", second, "Tangent")
    both = expr(material, unreal.MaterialExpressionSubstrateAdd, x + 1700, y - 600)
    link(base, "", both, "A")
    link(second, "", both, "B")
    # R96: the two-slab Substrate Add rendered the whole surface ~0.55x darker than the base slab alone in
    # UE 5.7 (hormagaunt head: lit 43,28,28 vs 74,56,54 with the second lobe zeroed; base colour 76,54,44),
    # although the second slab has black diffuse and the game lobe is purely additive specular. Default now:
    # ONE slab, second lobe folded in: F0 = saturate(F0 + F0_2), F90 = max(F90, F90_2), haziness lobe
    # (Second Roughness = r2, weight = F0_2 share). Loses the second lobe's own anisotropy/tangent shift
    # (HEURISTIC). Static switch SecondSpecAdd = true restores the old two-slab Add.
    f0_2o = mul(material, f0_2g, occ, x + 1300, y2 + 900)
    f90_2o = mul(material, f90_2g, occ, x + 1300, y2 + 980)
    merged = expr(material, unreal.MaterialExpressionSubstrateSlabBSDF, x + 1600, y + 700)
    link(diffuse, "", merged, "Diffuse Albedo")
    link(unary(material, unreal.MaterialExpressionSaturate, add(material, base_f0, f0_2o, x + 1440, y + 700), x + 1520, y + 700), "", merged, "F0")
    link(op(material, unreal.MaterialExpressionMax, base_f90, f90_2o, x + 1440, y + 780), "", merged, "F90")
    link(base_rough, "", merged, "Roughness")
    link(base_aniso, "", merged, "Anisotropy")
    link(normal, "", merged, "Normal")
    link(tangent, "", merged, "Tangent")
    link(emissive, "", merged, "Emissive Color")
    link(r2, "", merged, "Second Roughness")
    l0 = mask(material, base_f0, "g", x + 1300, y + 1060); l2 = mask(material, f0_2o, "g", x + 1300, y + 1120)
    share = unary(material, unreal.MaterialExpressionSaturate, op(material, unreal.MaterialExpressionDivide, l2, add(material, add(material, l0, l2, x + 1400, y + 1100), const(material, 0.0001, x + 1400, y + 1160), x + 1460, y + 1100), x + 1520, y + 1100), x + 1580, y + 1100)
    link(share, "", merged, "Second Roughness Weight")
    on = switch(material, "SecondSpecAdd", merged, both, x + 1800, y - 400)
    return switch(material, "UseSecondSpec", base, on, x + 1900, y - 300)





def srgb_encode(material, lin, x, y):
    # tex_srgb = 1.055 * lin^(1/2.4) - 0.055 (clamped at 0; the linear toe below 0.0031308 is ignored).
    enc = expr(material, unreal.MaterialExpressionPower, x, y)
    link(unary(material, unreal.MaterialExpressionSaturate, lin, x - 120, y), "", enc, "Base")
    link(const(material, 1.0 / 2.4, x - 120, y + 60), "", enc, "Exponent")
    out = expr(material, unreal.MaterialExpressionMax, x + 360, y)
    link(op(material, unreal.MaterialExpressionSubtract, mul(material, enc, const(material, 1.055, x + 120, y + 80), x + 240, y), const(material, 0.055, x + 240, y + 80), x + 300, y + 40), "", out, "A")
    link(const(material, 0.0, x + 300, y + 120), "", out, "B")
    return out


def srgb_poly(material, v, x, y):
    # Game polynomial sRGB -> linear: x * (x * (0.305306 x + 0.682171) + 0.0125229).
    poly = add(material, mul(material, v, const(material, 0.305306, x, y + 80), x + 100, y), const(material, 0.682171, x + 100, y + 80), x + 200, y)
    poly = add(material, mul(material, poly, v, x + 300, y), const(material, 0.0125229, x + 300, y + 80), x + 400, y)
    return mul(material, poly, v, x + 500, y)


def instance_tint_param(material, x, y):
    p = expr(material, unreal.MaterialExpressionVectorParameter, x, y)
    p.set_editor_property("parameter_name", "InstanceTint")
    p.set_editor_property("default_value", unreal.LinearColor(1.0, 1.0, 1.0, 1.0))
    return mask(material, p, "rgb", x + 150, y)


def instance_tint(material, albedo, x, y):
    # mtl1-27: DYNAMIC_GLT per-instance tint (record +0, inst.rgb). Exe writer FUN_1423bb1e0 (glt vtable 0x1437edb20):
    # rgb = bytes of the runtime object's packed colour (obj+0x74: R = bits 16-23, G = 8-15, B = 0-7) / 255, default
    # (1,1,1); optionally * byte(obj+0x7c) / 255, and * 10 when object flag 0x200 is set. The PS (22961, 29540)
    # does albedo_lin = poly(inst.rgb * tex_srgb [* chameleon]). The colour is per spawned object at runtime (no
    # static source in the .tpl/.td; 1 on every captured draw), so the sidecar may carry instanceTint only as an
    # override. UseInstanceTint (default off) keeps the default path bit-exact with UE's own sRGB decode.
    tinted = srgb_poly(material, mul(material, srgb_encode(material, albedo, x - 600, y), instance_tint_param(material, x - 600, y + 200), x - 160, y), x - 100, y)
    return switch(material, "UseInstanceTint", albedo, tinted, x + 500, y)


def chameleon(material, albedo, plain, metal, normal_ts, x, y):
    # glt.chameleon (UseChameleon). mtl1-26, capture frame 68141 (PS 19112 GBUFFER / 22961 forward, FORMULA.md):
    #   t = pow(saturate(N.V), fresnelPwr), N = normal-mapped tangent-space normal, V in (T, B, N)
    #   c = lerp(C0, C1, sat((t - r14.x) * r14.y)); c = lerp(c, C2, sat((t - r14.z) * r14.w)); c = lerp(c, C3, sat((t - r15.x) * r15.y))
    #   c = c + (1 - c) * metal
    #   albedo_lin = sRGBtoLin_poly(inst.rgb * tex_srgb * c), poly(x) = x * (x * (0.305306 x + 0.682171) + 0.0125229)
    # The ramp colours are the raw .td bytes / 255 (NOT linearised) and the product with the texture happens in sRGB.
    # UE samples the sRGB texture linear, so the texture is re-encoded to sRGB here, multiplied, then linearised with
    # the game's polynomial. inst.rgb (DYNAMIC_GLT[+0]) is the InstanceTint parameter (see instance_tint). plain is the
    # UseChameleon=False result.
    # Thresholds/scales packed like the CPU packer FUN_1423ba150, see chameleon_ramp().
    mel = unreal.MaterialEditingLibrary
    # N.V in tangent space like the PS (normal map normal . tangent-space view vector). PixelNormalWS cannot be
    # used here: it feeds the Substrate front material, which owns the normal (circular -> default grey material).
    nv = expr(material, unreal.MaterialExpressionDotProduct, x - 700, y)
    view_ts = expr(material, unreal.MaterialExpressionTransform, x - 850, y + 80)
    view_ts.set_editor_property("transform_source_type", unreal.MaterialVectorCoordTransformSource.TRANSFORMSOURCE_WORLD)
    view_ts.set_editor_property("transform_type", unreal.MaterialVectorCoordTransform.TRANSFORM_TANGENT)
    link(expr(material, unreal.MaterialExpressionCameraVectorWS, x - 1000, y + 80), "", view_ts, "")
    link(normal_ts, "", nv, "A")
    link(view_ts, "", nv, "B")
    # Built from plain nodes (the first version used a Custom node whose pins likely did not bind from Python: the paint went grey).
    par = {}
    for i, n in enumerate(["C0", "C1", "C2", "C3", "R0", "R1"]):
        p = expr(material, unreal.MaterialExpressionVectorParameter, x - 1400, y + 120 + i * 90)
        p.set_editor_property("parameter_name", "Chameleon" + n)
        default = CHAMELEON_CAPTURE_DEFAULT_PARAMS["Chameleon" + n]
        p.set_editor_property("default_value", unreal.LinearColor(default[0], default[1], default[2], default[3]))
        par[n] = p
    t = expr(material, unreal.MaterialExpressionPower, x - 1000, y)
    link(unary(material, unreal.MaterialExpressionSaturate, nv, x - 1100, y), "", t, "Base")
    link(mask(material, par["R1"], "b", x - 1100, y + 60), "", t, "Exponent")

    def step(c, cn, thr, scl, yy):
        a_ = unary(material, unreal.MaterialExpressionSaturate,
                   mul(material, op(material, unreal.MaterialExpressionSubtract, t, thr, x - 900, yy), scl, x - 800, yy), x - 700, yy)
        return lerp(material, c, mask(material, par[cn], "rgb", x - 700, yy + 60), a_, x - 600, yy)

    c = step(mask(material, par["C0"], "rgb", x - 700, y - 60), "C1", mask(material, par["R0"], "r", x - 1000, y + 200), mask(material, par["R0"], "g", x - 1000, y + 260), y + 200)
    c = step(c, "C2", mask(material, par["R0"], "b", x - 1000, y + 400), mask(material, par["R0"], "r", x - 1000, y + 460, "A"), y + 400)
    c = step(c, "C3", mask(material, par["R1"], "r", x - 1000, y + 600), mask(material, par["R1"], "g", x - 1000, y + 660), y + 600)
    node = lerp(material, c, vec3(material, (1.0, 1.0, 1.0), x - 400, y + 700), unary(material, unreal.MaterialExpressionSaturate, metal, x - 400, y + 780), x - 300, y)
    tex_s = srgb_encode(material, albedo, x - 420, y + 160)
    prod = mul(material, mul(material, tex_s, node, x + 40, y + 60), instance_tint_param(material, x - 100, y + 340), x + 100, y + 60)
    tinted = srgb_poly(material, prod, x + 140, y + 100)
    return switch(material, "UseChameleon", plain, tinted, x + 700, y)


# mtl1-26: capture defaults (frame 68141, CB_STATIC_GLT rows 10..15 of the Hellbrute chameleon draws, identical to its
# .td glt.chameleon) used only when a sidecar chameleon block lacks a field. The exporter always writes every field
# (the exe parser FUN_142576f30 defaults missing text values to 0), so these only cover old or partial sidecars.
CHAMELEON_CAPTURE_DEFAULTS = {
    "colors": [[0.0, 0.0, 0.0, 0.0], [0.0, 1.0, 1.0, 1.0], [0.137255, 0.631373, 0.745098, 1.0], [0.070588, 0.909804, 0.768627, 1.0]],
    "offset1": 0.5,
    "offset2": 0.9,
    "fresnelPwr": 0.7,
}


def chameleon_params(cham):
    """Material-instance vectors for a sidecar chameleon block: raw sRGB colours (rgb, alpha 1) and the packed ramp rows."""
    cham = dict(cham or {})
    colors = list(cham.get("colors") or [])
    full = {}
    for i in range(4):
        c = colors[i] if i < len(colors) and colors[i] is not None else CHAMELEON_CAPTURE_DEFAULTS["colors"][i]
        rgb = (list(c) + [1.0, 1.0, 1.0])[:3]
        full["ChameleonC%d" % i] = (float(rgb[0]), float(rgb[1]), float(rgb[2]), 1.0)
    for key in ("offset1", "offset2", "fresnelPwr"):
        if cham.get(key) is None:
            cham[key] = CHAMELEON_CAPTURE_DEFAULTS[key]
    r0, r1 = chameleon_ramp(cham)
    full["ChameleonR0"] = tuple(float(v) for v in r0)
    full["ChameleonR1"] = tuple(float(v) for v in r1)
    return full


def srgb_to_linear_poly(x):
    # The glt PS polynomial (PS 22961 lines 627-641).
    return x * (x * (0.305306 * x + 0.682171) + 0.0125229)


def chameleon_albedo_reference(tex_srgb, ndotv, cham, metal=0.0, inst=(1.0, 1.0, 1.0)):
    """CPU reference of the game's chameleon albedo (linear RGB) for one pixel; mirrors chameleon() above."""
    p = chameleon_params(cham)
    r0, r1 = p["ChameleonR0"], p["ChameleonR1"]
    col = [p["ChameleonC%d" % i][:3] for i in range(4)]
    t = max(0.0, min(1.0, ndotv)) ** r1[2]
    sat = lambda v: max(0.0, min(1.0, v))
    out = []
    for k in range(3):
        c = col[0][k]
        c = c + (col[1][k] - c) * sat((t - r0[0]) * r0[1])
        c = c + (col[2][k] - c) * sat((t - r0[2]) * r0[3])
        c = c + (col[3][k] - c) * sat((t - r1[0]) * r1[1])
        c = c + (1.0 - c) * sat(metal)
        out.append(srgb_to_linear_poly(inst[k] * tex_srgb[k] * c))
    return out


def chameleon_ramp(cham):
    # Exact CPU packer FUN_1423ba150 (eps 1e-6); parser FUN_142576f30 defaults every missing value to 0.
    eps = 1e-6
    o1 = float(cham.get("offset1", 0.0)); o2 = float(cham.get("offset2", 0.0))
    if o2 < o1:
        o2 = o1 + eps
    o1 = min(max(o1, 0.0), 1.0); o2 = min(max(o2, 0.0), 1.0)
    return (0.0, 1.0 / (o1 + eps), o1, 1.0 / (o2 - o1 + eps)), (o2, 1.0 / (1.0 - o2 + eps), float(cham.get("fresnelPwr", 0.0)), 0.0)


CHAMELEON_CAPTURE_DEFAULT_PARAMS = chameleon_params({})


# R49: colour scheme ({tpl}.colour_scheme.{id}.json, ArmorColourSchemes.UeParams) on the master material.
# Static switch UseColourScheme (default off = old path, bit-identical). Per layer L (mask r,g,b,a = layer0..3):
#   col = lerp(Tint, MetalTint, max(metal, Metallic));  col = lerp(col, base * col, MultiplyBase)
#   albedo = lerp(albedo, col, SchemeMask[L] * Tint.a * Enabled)
# Rule from the glt PS layer loop (RE_NOTES "Preview (code, ArmorSchemeBake)"); Metallic as max() is HEURISTIC (isMetallic).
SCHEME_CH = ("r", "g", "b", "a")


def colour_scheme(material, base, metal, x, y):
    linear = unreal.MaterialSamplerType.SAMPLERTYPE_MASKS
    smask = param_tex(material, "SchemeMask", x - 900, y, linear)
    out = base
    for i, ch in enumerate(SCHEME_CH):
        yy = y + 260 * i
        t = expr(material, unreal.MaterialExpressionVectorParameter, x - 700, yy)
        t.set_editor_property("parameter_name", "Layer%dTint" % i)
        t.set_editor_property("default_value", unreal.LinearColor(1.0, 1.0, 1.0, 0.0))
        mt = expr(material, unreal.MaterialExpressionVectorParameter, x - 700, yy + 120)
        mt.set_editor_property("parameter_name", "Layer%dMetalTint" % i)
        mt.set_editor_property("default_value", unreal.LinearColor(1.0, 1.0, 1.0, 1.0))
        mflag = scalar(material, "Layer%dMetallic" % i, 0.0, x - 700, yy + 200)
        mb = scalar(material, "Layer%dMultiplyBase" % i, 0.0, x - 500, yy + 200)
        en = scalar(material, "Layer%dEnabled" % i, 0.0, x - 300, yy + 200)
        m = op(material, unreal.MaterialExpressionMax, metal, mflag, x - 500, yy + 120)
        col = lerp(material, mask(material, t, "rgb", x - 500, yy), mask(material, mt, "rgb", x - 500, yy + 60), m, x - 350, yy)
        col = lerp(material, col, mul(material, out, col, x - 250, yy + 60), mb, x - 150, yy)
        w = mul(material, mul(material, mask(material, smask, ch, x - 350, yy + 140, "RGBA"), mask(material, t, "r", x - 350, yy + 180, "A"), x - 200, yy + 150), en, x - 100, yy + 150)
        out = lerp(material, out, col, w, x, yy)
    return switch(material, "UseColourScheme", base, out, x + 200, y)


def td_scalar_tags(slot):
    """R50 / F43: sidecar tdScalars -> asset metadata tags (SM2.td.<key>). Kept as data, not wired to the graph: the game's
    emissive intensity units (adaptive exposure, log2 curve with an unknown CB constant) have no proven UE mapping yet."""
    return {"SM2.td." + k: "%g" % float(v) for k, v in sorted((slot.get("tdScalars") or {}).items())}


def apply_td_scalars(instance, slot):
    for k, v in td_scalar_tags(slot).items():
        unreal.EditorAssetLibrary.set_metadata_tag(instance, k, v)


def find_scheme(sidecar_path):
    """Explicit --scheme=<json> / SM2_COLOUR_SCHEME wins; else the one *.colour_scheme.*.json beside the sidecar (the browser
    writes it only when a scheme is picked); none or several -> None (old behaviour)."""
    for arg in sys.argv[1:]:
        if arg.startswith("--scheme="):
            return arg.split("=", 1)[1]
    if os.environ.get("SM2_COLOUR_SCHEME"):
        return os.environ["SM2_COLOUR_SCHEME"]
    here = os.path.dirname(os.path.abspath(sidecar_path))
    found = [n for n in os.listdir(here) if ".colour_scheme." in n and n.endswith(".json")]
    return os.path.join(here, found[0]) if len(found) == 1 else None


def scheme_part_for(scheme, slot_name, diffuse_name):
    """Scheme part for a material slot: same part name, else same texture / albedo as the slot diffuse (case-insensitive)."""
    keys = {k.lower() for k in (slot_name, diffuse_name) if k}
    for part in (scheme or {}).get("parts", []):
        if {str(part.get(k, "")).lower() for k in ("part", "texture", "albedo")} & keys:
            return part
    return None


def scheme_values(part):
    """(vectors, scalars) material-instance values for one scheme part (pure; unit-tested)."""
    vec, sca = {}, {}
    p = part.get("params", {})
    for i in range(4):
        on = str(p.get("Layer%dEnabled" % i, "0")) == "1" or p.get("Layer%dEnabled" % i) == 1
        sca["Layer%dEnabled" % i] = 1.0 if on else 0.0
        if not on:
            continue
        vec["Layer%dTint" % i] = tuple(float(v) for v in p["Layer%dTint" % i])
        vec["Layer%dMetalTint" % i] = tuple(float(v) for v in p["Layer%dMetalTint" % i])
        sca["Layer%dMetallic" % i] = float(p.get("Layer%dMetallic" % i, 0))
        sca["Layer%dMultiplyBase" % i] = float(p.get("Layer%dMultiplyBase" % i, 0))
    return vec, sca


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
    # glt.detail {density, scale} (HEURISTIC): detail UV = UV0 * DetailDensity, blend = saturate(mask.g * DetailScale).
    detail_uv0 = expr(material, unreal.MaterialExpressionTextureCoordinate, -1300, 900)
    detail_uv = expr(material, unreal.MaterialExpressionMultiply, -1100, 900)
    link(detail_uv0, "", detail_uv, "A")
    link(scalar(material, "DetailDensity", 1.0, -1300, 980), "", detail_uv, "B")
    link(detail_uv, "", detail, "UVs")
    detail_amount = expr(material, unreal.MaterialExpressionMultiply, -420, 1000)
    link(mask_g, "", detail_amount, "A")
    link(scalar(material, "DetailScale", 1.0, -560, 1060), "", detail_amount, "B")
    detail_alpha = expr(material, unreal.MaterialExpressionSaturate, -340, 1000)
    link(detail_amount, "", detail_alpha, "")
    link(detail_alpha, "", detail_lerp, "Alpha")
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
    # glt.metalness / glt.roughness {scale, bias} (sidecar baseLayer): base metal/rough = saturate(packed * scale + bias)
    # (PS 00004_1 cb6 rows 8.xy / 9.xy). glt.metallicTint: albedo *= lerp(albedo_tint, metallicTint, metal) (lines 455-457).
    # UseSpecMap (default on). mtl1-26: on the captured chameleon draws (PS 19112/22961) the spec-map channel flags
    # OBJ_MTLBLEND_LAYER_CONST[15].x are 0, so the PS replaces the packed metal with 0 and the packed roughness with 1
    # (PS 22961 lines 615-625): metal = saturate(bias), roughness = saturate(scale + bias). The importer turns it off
    # for chameleon slots (HEURISTIC generalisation: 7/7 chameleon draws; the flag writer was not located).
    use_spec = expr(material, unreal.MaterialExpressionStaticSwitchParameter, -160, 300)
    use_spec.set_editor_property("parameter_name", "UseSpecMap")
    use_spec.set_editor_property("default_value", True)
    link(const(material, 0.0, -280, 240), "", use_spec, "False")
    link(metal, "", use_spec, "True")
    use_spec_r = expr(material, unreal.MaterialExpressionStaticSwitchParameter, -160, 460)
    use_spec_r.set_editor_property("parameter_name", "UseSpecMap")
    use_spec_r.set_editor_property("default_value", True)
    link(const(material, 1.0, -280, 520), "", use_spec_r, "False")
    link(rough_switch, "", use_spec_r, "True")
    metal = use_spec
    rough_switch = use_spec_r
    metal_b = scale_bias(material, metal, "MetalnessScale", 1.0, "MetalnessBias", 0.0, -40, -620)
    rough_b = scale_bias(material, rough_switch, "RoughnessScale", 1.0, "RoughnessBias", 0.0, -40, -440)
    metal_tint = expr(material, unreal.MaterialExpressionVectorParameter, -500, -500)
    metal_tint.set_editor_property("parameter_name", "MetallicTint")
    metal_tint.set_editor_property("default_value", unreal.LinearColor(1.0, 1.0, 1.0, 1.0))
    tint_mix = lerp(material, mask(material, tint, "rgb", -360, -320), mask(material, metal_tint, "rgb", -360, -500), metal_b, -200, -400)
    albedo_rgb = mask(material, diffuse, "rgb", -360, -200, "RGB")
    # Chameleon first (it works on the raw texture in sRGB, then linearises), then the albedo / metallic tint.
    plain_rgb = instance_tint(material, albedo_rgb, -200, -1500)
    cham_rgb = chameleon(material, albedo_rgb, plain_rgb, metal_b, detail_switch, -200, -900)
    tinted = mul(material, cham_rgb, tint_mix, -200, -260)
    tinted = colour_scheme(material, tinted, metal_b, -200, -2600)
    front = spec_layers(material, tinted, metal_b, rough_b, detail_switch, emit_mul, albedo_rgb, metal, rough_switch, ao_source)
    if translucent:
        # Substrate translucency: coverage = diffuse alpha.
        cover = expr(material, unreal.MaterialExpressionSubstrateWeight, 2500, -200)
        link(front, "", cover, "A")
        link(opacity, "", cover, "Weight")
        front = cover
    mel.connect_material_property(front, "", unreal.MaterialProperty.MP_FRONT_MATERIAL)
    if not translucent:
        mel.connect_material_property(opacity, "", unreal.MaterialProperty.MP_OPACITY_MASK)
    mel.connect_material_property(ao_source, "", unreal.MaterialProperty.MP_AMBIENT_OCCLUSION)
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


def decode_instance_color(packed, scale_byte=None, boost=False):
    # FUN_1423bb1e0: rgb = ((packed >> 16) & 255, (packed >> 8) & 255, packed & 255) / 255; * scale_byte / 255 when the
    # glt mode at +0x12 is 2; * 10 when object flag 0x200 is set. For a runtime value read from the game.
    rgb = [((packed >> 16) & 255) / 255.0, ((packed >> 8) & 255) / 255.0, (packed & 255) / 255.0]
    if scale_byte is not None:
        rgb = [c * (scale_byte & 255) / 255.0 for c in rgb]
    if boost:
        rgb = [c * 10.0 for c in rgb]
    return rgb


def instance_tint_value(slot):
    # Optional sidecar override instanceTint [r, g, b] (0..1, sRGB space like the game's obj+0x74 bytes / 255).
    # None (no override, or white) leaves the exact default path.
    v = slot.get("instanceTint")
    if not v or len(v) < 3:
        return None
    rgb = [max(0.0, float(c)) for c in v[:3]]
    if all(abs(c - 1.0) < 1e-6 for c in rgb):
        return None
    return (rgb[0], rgb[1], rgb[2], 1.0)


def srgb_to_linear(c):
    return pow(max(0.0, min(1.0, float(c))), 2.2)


def apply_spec_layers(instance, slot, texture_for):
    mel = unreal.MaterialEditingLibrary
    cham = slot.get("chameleon")
    mel.set_material_instance_static_switch_parameter_value(instance, "UseChameleon", bool(cham))
    inst = instance_tint_value(slot)
    mel.set_material_instance_static_switch_parameter_value(instance, "UseInstanceTint", inst is not None and not cham)
    if inst is not None:
        mel.set_material_instance_vector_parameter_value(instance, "InstanceTint", unreal.LinearColor(*inst))
    # mtl1-26: chameleon slots shade without the spec map (see UseSpecMap in make_master).
    # rea-batch 4 (DATA, armory 128197, 140 draws): the spec-map bits of OBJ_MTLBLEND_LAYER_CONST[15].x are set exactly
    # when a spec texture is bound: 0x2000 <-> packed MSCRGHAO td 68/68, 0x40 <-> legacy *_spec td 66/67. With no
    # spec texture the PS uses metal 0 / rough 1 raw, so a slot without a specular map also turns UseSpecMap off.
    has_spec = first_map(slot, "specular") is not None
    mel.set_material_instance_static_switch_parameter_value(instance, "UseSpecMap", has_spec and not cham)
    if cham:
        # Raw .td colours (bytes / 255, sRGB space; the PS multiplies them with the sRGB texture before linearising).
        for name, value in chameleon_params(cham).items():
            mel.set_material_instance_vector_parameter_value(instance, name, unreal.LinearColor(*value))
    second = slot.get("secondSpec")
    aniso = slot.get("anisotropicSpec")
    mel.set_material_instance_static_switch_parameter_value(instance, "UseSecondSpec", bool(second))
    base = slot.get("baseLayer")
    if base:
        b_metal = base.get("metalness") or {}
        b_rough = base.get("roughness") or {}
        mel.set_material_instance_scalar_parameter_value(instance, "MetalnessScale", float(b_metal.get("scale", 1.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "MetalnessBias", float(b_metal.get("bias", 0.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "RoughnessScale", float(b_rough.get("scale", 1.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "RoughnessBias", float(b_rough.get("bias", 0.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "Reflectance", float(base.get("reflectance", 0.5)))
        mr, mg, mb = (list(base.get("metallicTint") or [1, 1, 1, 0]) + [1, 1, 1])[:3]
        mlin = [srgb_to_linear(c) for c in (mr, mg, mb)]
        mel.set_material_instance_vector_parameter_value(instance, "MetallicTint", unreal.LinearColor(mlin[0], mlin[1], mlin[2], 1.0))
    if second:
        # metallicTint R, G, B in 0..1, sRGB like albedo_tint, stored linear. The shader reads only RGB
        # (cb6 rel row 12.xyz); row 12.w is the metalness scale, so the tint alpha is unused.
        r, g, b, a = (list(second.get("metallicTint") or [1, 1, 1, 0]) + [1, 1, 1, 0])[:4]
        lin = [srgb_to_linear(c) for c in (r, g, b)]
        mel.set_material_instance_vector_parameter_value(instance, "SecondSpecTint", unreal.LinearColor(lin[0], lin[1], lin[2], float(a)))
        metal = second.get("metalness") or {}
        rough = second.get("roughness") or {}
        mel.set_material_instance_scalar_parameter_value(instance, "SecondMetalnessScale", float(metal.get("scale", 1.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "SecondMetalnessBias", float(metal.get("bias", 0.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "SecondRoughnessScale", float(rough.get("scale", 0.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "SecondRoughnessBias", float(rough.get("bias", 0.7)))
        # 4 = the plain second lobe (no visibility term), 1 = the aniso extra lobe (keeps 1/(4 pi)).
        mel.set_material_instance_scalar_parameter_value(instance, "SecondSpecGain", 1.0 if aniso else 4.0)
    mel.set_material_instance_static_switch_parameter_value(instance, "UseAnisoSpec", bool(aniso))
    dir_asset = None
    if aniso:
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoAnisotropy", float(aniso.get("anisotropy", 0.5)))
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoAngle", float(aniso.get("angle", 90.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoIntensity", float(aniso.get("intensity", 1.0)))
        # extra{} only shades through the second lobe: the glt PS with aniso and no second spec has one D term.
        extra = aniso.get("extra") or {}
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoExtraAnisotropy", float(extra.get("anisotropy", 0.5)))
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoExtraIntensity", float(extra.get("intensity", 1.0)))
        mel.set_material_instance_scalar_parameter_value(instance, "AnisoExtraShift", float(extra.get("shift", 0.2)))
        # APPROXIMATION, see DirectAo* in spec_layers: on for the hair slots only (aniso with a dirMap flow map).
        mel.set_material_instance_scalar_parameter_value(instance, "DirectAoStrength", 1.0 if aniso.get("dirMap") else 0.0)
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
    scheme_path = find_scheme(sidecar_path)
    scheme = load_sidecar(scheme_path) if scheme_path else None
    scheme_hits = 0
    if scheme:
        unreal.log("SM2 colour scheme " + str(scheme.get("scheme")) + " from " + scheme_path)

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
        detail_spec = slot.get("detail") or {}
        unreal.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(instance, "DetailDensity", float(detail_spec.get("density", 1.0)))
        unreal.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(instance, "DetailScale", float(detail_spec.get("scale", 1.0)))
        albedo_tint = slot.get("albedoTint")
        if albedo_tint:
            # The tint bytes are sRGB like the albedo they multiply; Unreal multiplies in linear.
            lin = [pow(float(c), 2.2) for c in albedo_tint[:3]]
            unreal.MaterialEditingLibrary.set_material_instance_vector_parameter_value(instance, "AlbedoTint", unreal.LinearColor(lin[0], lin[1], lin[2], 1.0))
        apply_spec_layers(instance, slot, texture_for)
        apply_td_scalars(instance, slot)
        spart = scheme_part_for(scheme, name, (diffuse or {}).get("name")) if scheme else None
        if spart:
            sdir = os.path.dirname(os.path.abspath(scheme_path))
            mfile = next((os.path.join(sdir, spart["mask"] + e) for e in (".png", ".tga", ".dds") if os.path.exists(os.path.join(sdir, spart["mask"] + e))), None)
            if mfile is None:
                unreal.log_warning("SM2 scheme part " + spart["part"] + ": mask " + spart["mask"] + " not exported beside the scheme json; skipped")
            else:
                mel = unreal.MaterialEditingLibrary
                mel.set_material_instance_texture_parameter_value(instance, "SchemeMask", import_texture(mfile, folder + "/Textures", spart["mask"], False, False))
                afile = next((os.path.join(sdir, spart["albedo"] + e) for e in (".png", ".tga", ".dds") if spart.get("albedo") and os.path.exists(os.path.join(sdir, spart["albedo"] + e))), None)
                if afile:  # neutral _a albedo, so the scheme is not applied on top of a baked one
                    mel.set_material_instance_texture_parameter_value(instance, "Diffuse", import_texture(afile, folder + "/Textures", spart["albedo"], True, False))
                vec, sca = scheme_values(spart)
                for k, v in vec.items():
                    mel.set_material_instance_vector_parameter_value(instance, k, unreal.LinearColor(*v))
                for k, v in sca.items():
                    mel.set_material_instance_scalar_parameter_value(instance, k, v)
                mel.set_material_instance_static_switch_parameter_value(instance, "UseColourScheme", True)
                scheme_hits += 1
        unreal.EditorAssetLibrary.save_loaded_asset(instance)
        instances[name] = instance
        unreal.log("SM2 instance " + name)

    if scheme:
        unreal.log("SM2 colour scheme applied to " + str(scheme_hits) + " of " + str(len(scheme.get("parts", []))) + " scheme parts")
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
