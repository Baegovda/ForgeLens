"""Generic SM2 VFX frozen-frame import + render for UE (mtl1-34).

Input: *.vfxframe.json + textures/*.png written by ForgeLens --vfxue / --vfxue-rules
(LibSaber.SM2.Assets/Effects/VfxFrameExport.cs). Capture-derived rules (CaptureRules.cs):
  AlphaComposite  premultiplied CPU particles  One/InvSrcAlpha, rgb = c*va*(0x400 ? 1 : ta), a = 0x20 ? 0 : va*ta
  Translucent     trails                        SrcAlpha/InvSrcAlpha
  Masked          GPU sparks (HEURISTIC class)  blending off, alpha test 0.5
  Additive        sfx meshes                    One/One
  Distortion      scene colour offset           clamp(0.08*mask*o, +-0.02) screen UV, o = (tex.rg*2.007874-1.007874)*power*alpha
  Solid           lit mesh particles (combine_material): opaque, browser shading baked per vertex (mtl1-35)
  Soft depth      AlphaComposite/Translucent/Additive: va *= sat((SceneDepth - PixelDepth) / softDepth) (PS 101818)
  Tonemap         PP material replacing the tonemapper: 32^3 TONEMAP_LUT strip (CaptureLut / CS s1996: Hable*whitepoint,
                  sRGB, grading LUT) sampled at the HEURISTIC inverse-bake log coordinate; exact Hable when no strip
  Lights          frame["lights"]: lit ground (vfxframe_scene.ground_batch, browser ground shading) + UE PointLights
No Niagara, no per-effect names: one ProceduralMeshComponent per batch, sort priority = batch order (far -> near).

usage (via uerun.ps1):
  import <frame dir> [dest=/Game/SM2_Work/VfxUe] [fresh]   textures + masters + one MI per unique parameter set
  render <frame json> [dest] [res=1024] [backdrop_exposed=-1] [exposure_scale=1] [ground_exposed=-1] [lut=1]
  export <out png>                                          write the capture, destroy SM2Cap actors
"""
import builtins, glob, hashlib, json, os, struct, sys, zlib, math
import unreal
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "vfx"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib
import vfxframe_scene as S
S = importlib.reload(S)  # mtl1-36: the editor keeps modules loaded between uerun calls

EAL = unreal.EditorAssetLibrary
MEL = unreal.MaterialEditingLibrary
AT = unreal.AssetToolsHelpers.get_asset_tools()
DEST = "/Game/SM2_Work/VfxUe"
TAG = "SM2Cap"
BASE = unreal.Vector(300000, -300000, 50000)
STRIDE = 13  # pos3 uv2 rgb3 a clip softDepth mu mv
LINEAR = ("_lin", "sm2r_ramp", "sm2r_nrm")

PARTICLE = """float a = T.a * (HasMask > 0.5 ? M.a : 1.0);
a = Clip > 0.0 ? saturate((a - Clip) / max(1.0 - Clip, 1e-4)) : a;
// PROVEN particles PS 281 (mtl1-38): boost = 1 + Contrast*sat(1 - |Key - tex|); hdr = lerp(tex*C*boost, Tint*C*tex.a, Tint)
float3 c0 = T.rgb * C.rgb * (1.0 + Contrast * saturate(1.0 - length(Key.rgb - T.rgb)));
float3 hdr = lerp(c0, Tint * C.rgb * T.a, Tint);
float va = saturate(C.a);
"""
CODE = {
    # One/InvSrcAlpha: UE AlphaComposite = Emissive + Dst*(1-Opacity)
    "AlphaComposite": PARTICLE + "float k = RgbTexAlpha > 0.5 ? va * a : va;\nreturn float4(hdr * k, OutAlpha > 0.5 ? va * a : 0.0);",
    # SrcAlpha/InvSrcAlpha
    "Translucent": PARTICLE + "return float4(hdr, va * a);",
    # blending off, alpha test 0.5 on texture alpha
    "Masked": PARTICLE + "return float4(hdr * va, a);",
    # One/One
    "Additive": PARTICLE + "return float4(hdr * va * a, 1.0);",
    "Opaque": "return float4(T.rgb * C.rgb, 1.0);",
    "Solid": "return float4(T.rgb * C.rgb, 1.0);",
}
SOFT = ("AlphaComposite", "Translucent", "Additive")  # translucent masters can read SceneDepth
SOFT_CODE = "va *= SoftDepth > 0.0 ? saturate((SD - PD) / (SoftDepth * 100.0)) : 1.0;\n"
DISTORT = """float2 o = (T.rg * 2.007874 - 1.007874) * C.r * saturate(C.a) * (HasMask > 0.5 ? M.a : 1.0);
float m = saturate(10.0 * (abs(o.x) + abs(o.y)));
return float3(clamp(0.08 * m * o, -0.02, 0.02), 1.0);"""  # b = footprint (game: DUDV alpha 0; here additive count, RT cleared to 0)
# mtl1-40 PROVEN post PS 5160 (capture 5160_ps.txt l.30-64): the shift is applied only when the source DUDV is non-neutral
# and the DUDV alpha at the shifted uv is 0, i.e. inside the distortion footprint (all bilinear taps drawn). Resample of
# the scene with the same sampler (PS_SAMPLERS[4], bilinear: INFERRED) -> SceneTexture filtered.
DISTORT_UV = """float2 s = float2(0.0, 0.0);
if (On > 0.5)
{
  float4 d = Texture2DSampleLevel(D, DSampler, UV, 0.0);
  if (dot(d.rg, d.rg) > 0.0)
  {
    float2 p = (UV + d.rg) * Size - 0.5; float2 f = frac(p); float2 b = (floor(p) + 0.5) / Size;
    float cov = 1.0;
    for (int j = 0; j < 2; j++)
      for (int i = 0; i < 2; i++)
      {
        float w = (i == 1 ? f.x : 1.0 - f.x) * (j == 1 ? f.y : 1.0 - f.y);
        if (w > 0.0 && Texture2DSampleLevel(D, DSampler, b + float2(i, j) / Size, 0.0).b < 0.5) cov = 0.0;
      }
    s = d.rg * cov;
  }
}
return UV + s;"""
TONEMAP = """float3 x = max(S.rgb * Exposure, 0.0);
if (LutOn > 0.5)
{
  // CaptureLut strip (1024x32: x = r + 32*b, y = g), coordinate = inverse of the s1996 bake input (PROVEN hdr PS 44/47)
  const float c0 = 0.18 * exp2(-0.43 * 14.0);
  float3 t = saturate(0.43 + log2((x + c0) / 0.18) / 14.0) * 31.0;
  float b0 = min(floor(t.b), 30.0); float fb = t.b - b0;
  float2 uv0 = float2((b0 * 32.0 + t.r + 0.5) / 1024.0, (t.g + 0.5) / 32.0);
  float3 s0 = Texture2DSampleLevel(L, LSampler, uv0, 0).rgb;
  float3 s1 = Texture2DSampleLevel(L, LSampler, uv0 + float2(32.0 / 1024.0, 0.0), 0).rgb;
  return lerp(s0, s1, fb);
}
const float A = 0.12, B = 0.05, Cc = 0.45, D = 0.2, E = 0.02, F = 0.08;
float3 h = (x * (A * x + Cc * B) + D * E) / (x * (A * x + B) + D * F) - E / F;
float3 l = saturate(h * WhitePoint);
float3 lo = 12.92 * l;
float3 hi = 1.055 * pow(max(l, 1e-6), 1.0 / 2.4) - 0.055;
return lerp(hi, lo, step(l, 0.0031308));"""
MASTER = {"AlphaComposite": "M_SM2Vfx_Premul", "Translucent": "M_SM2Vfx_Trail", "Masked": "M_SM2Vfx_Spark",
          "Additive": "M_SM2Vfx_Add", "Distortion": "M_SM2Vfx_Distort", "Opaque": "M_SM2Vfx_Backdrop", "Solid": "M_SM2Vfx_Solid"}
BLEND = {"AlphaComposite": unreal.BlendMode.BLEND_ALPHA_COMPOSITE, "Translucent": unreal.BlendMode.BLEND_TRANSLUCENT,
         "Masked": unreal.BlendMode.BLEND_MASKED, "Additive": unreal.BlendMode.BLEND_ADDITIVE,
         "Distortion": unreal.BlendMode.BLEND_TRANSLUCENT, "Opaque": unreal.BlendMode.BLEND_OPAQUE, "Solid": unreal.BlendMode.BLEND_OPAQUE}


def png(path, rgba, w=4, h=4):
    raw = b"".join(b"\x00" + bytes(rgba) * w for _ in range(h))
    def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


def is_linear(name, distortion_names):
    return name in distortion_names or any(k in name for k in LINEAR)


def import_textures(files, folder, linear_names):
    tasks = []
    for f in files:
        name = os.path.splitext(os.path.basename(f))[0]
        if EAL.does_asset_exist(f"{folder}/{name}"): continue
        t = unreal.AssetImportTask(); t.filename = f; t.destination_path = folder
        t.automated = True; t.save = False; t.replace_existing = False; tasks.append(t)
    if tasks: AT.import_asset_tasks(tasks)
    for f in files:
        name = os.path.splitext(os.path.basename(f))[0]
        tex = unreal.load_asset(f"{folder}/{name}")
        if not tex: print("MISSING texture", name); continue
        lin = is_linear(name, linear_names)
        tex.set_editor_property("srgb", not lin)
        if name.startswith("sm2lut_"):  # TONEMAP_LUT strip: raw 8-bit, bilinear, clamp, no mips (CaptureLut.Strip)
            tex.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_VECTOR_DISPLACEMENTMAP)
            tex.set_editor_property("mip_gen_settings", unreal.TextureMipGenSettings.TMGS_NO_MIPMAPS)
            tex.set_editor_property("address_x", unreal.TextureAddress.TA_CLAMP); tex.set_editor_property("address_y", unreal.TextureAddress.TA_CLAMP)
            tex.set_editor_property("filter", unreal.TextureFilter.TF_BILINEAR)
        elif lin or name.startswith("sm2r_"):
            tex.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_VECTOR_DISPLACEMENTMAP)
            tex.set_editor_property("mip_gen_settings", unreal.TextureMipGenSettings.TMGS_NO_MIPMAPS)
        elif os.environ.get("SM2_UE_TEX_BC") != "1":
            # mtl1-39: the PNGs are already decoded game BCn data; re-encoding to BC1/3 adds a second block error that
            # additionalContrast (boost up to 1+41x near the key colour) amplifies (cine_bomb fireball blue +11/255).
            # Keep them uncompressed RGBA8 (sRGB kept, mips kept). SM2_UE_TEX_BC=1 restores the default compression.
            tex.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_EDITOR_ICON)
        tex.set_editor_property("lod_group", unreal.TextureGroup.TEXTUREGROUP_EFFECTS)
        tex.set_editor_property("never_stream", True)
        EAL.save_loaded_asset(tex)


def node(m, cls, x, y): return MEL.create_material_expression(m, cls, x, y)


def scalar(m, name, val, y):
    s = node(m, unreal.MaterialExpressionScalarParameter, -900, y)
    s.set_editor_property("parameter_name", name); s.set_editor_property("default_value", val); return s


def custom(m, code, names, otype, x, y):
    c = node(m, unreal.MaterialExpressionCustom, x, y)
    c.set_editor_property("code", code); c.set_editor_property("output_type", otype)
    ins = []
    for n in names:
        ci = unreal.CustomInput(); ci.set_editor_property("input_name", n); ins.append(ci)
    c.set_editor_property("inputs", ins); return c


def mask(m, src, rgba, x, y):
    k = node(m, unreal.MaterialExpressionComponentMask, x, y)
    for ch, on in zip("rgba", rgba): k.set_editor_property(ch, on)
    MEL.connect_material_expressions(src, "", k, ""); return k


def get_or_create(folder, name, cls, factory):
    path = f"{folder}/{name}"
    a = unreal.load_asset(path) if EAL.does_asset_exist(path) else None
    return a or AT.create_asset(name, folder, cls, factory)


def build_master(folder, kind, defaults):
    m = get_or_create(folder, MASTER[kind], unreal.Material, unreal.MaterialFactoryNew())
    MEL.delete_all_material_expressions(m)
    m.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_UNLIT)
    m.set_editor_property("blend_mode", BLEND[kind]); m.set_editor_property("two_sided", True)
    for p, v in (("use_translucency_vertex_fog", False), ("opacity_mask_clip_value", 0.5)):
        try: m.set_editor_property(p, v)
        except Exception as e: print("note", p, e)
    lin = kind in ("Distortion", "Opaque")
    st = unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR if lin else unreal.MaterialSamplerType.SAMPLERTYPE_COLOR
    tex = node(m, unreal.MaterialExpressionTextureSampleParameter2D, -900, -400)
    tex.set_editor_property("parameter_name", "Tex"); tex.set_editor_property("sampler_type", st)
    tex.set_editor_property("texture", defaults["lin" if lin else "srgb"])
    msk = node(m, unreal.MaterialExpressionTextureSampleParameter2D, -900, -150)
    msk.set_editor_property("parameter_name", "Mask"); msk.set_editor_property("texture", defaults["srgb"])
    uv3 = node(m, unreal.MaterialExpressionTextureCoordinate, -1150, -150); uv3.set_editor_property("coordinate_index", 3)
    MEL.connect_material_expressions(uv3, "", msk, "UVs")
    uv1 = node(m, unreal.MaterialExpressionTextureCoordinate, -1150, 100); uv1.set_editor_property("coordinate_index", 1)
    uv2 = node(m, unreal.MaterialExpressionTextureCoordinate, -1150, 200); uv2.set_editor_property("coordinate_index", 2)
    col = node(m, unreal.MaterialExpressionAppendVector, -900, 150)
    MEL.connect_material_expressions(uv1, "", col, "A"); MEL.connect_material_expressions(uv2, "", col, "B")
    vc = node(m, unreal.MaterialExpressionVertexColor, -1150, 300)
    key = node(m, unreal.MaterialExpressionVectorParameter, -900, 700); key.set_editor_property("parameter_name", "Key")
    params = {"HasMask": scalar(m, "HasMask", 0.0, 350), "Tint": scalar(m, "Tint", 1.0, 420), "Contrast": scalar(m, "Contrast", 0.0, 490),
              "RgbTexAlpha": scalar(m, "RgbTexAlpha", 1.0, 560), "OutAlpha": scalar(m, "OutAlpha", 1.0, 630)}
    soft = kind in SOFT
    if soft:
        params["SoftDepth"] = scalar(m, "SoftDepth", 0.0, 770)
        params["SD"] = node(m, unreal.MaterialExpressionSceneDepth, -900, 840)
        params["PD"] = node(m, unreal.MaterialExpressionPixelDepth, -900, 910)
    if kind == "Distortion":
        # Game: distortion particles accumulate a screen-UV shift, then the scene is resampled (CaptureRules distortion).
        # UE: same two-step structure. This master writes the shift (additive, float RT) in a show-only capture;
        # PP_SM2Tonemap resamples PostProcessInput0 at ViewportUV + shift. (Translucent SceneColor / RM_2D_OFFSET
        # refraction read a constant colour inside SceneCapture2D in 5.7, measured on the rule scene.)
        m.set_editor_property("blend_mode", unreal.BlendMode.BLEND_ADDITIVE)
        c = custom(m, DISTORT, ["T", "M", "C", "HasMask"], unreal.CustomMaterialOutputType.CMOT_FLOAT3, -500, 0)
        MEL.connect_material_property(c, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    else:
        code = CODE[kind].replace("float va = saturate(C.a);\n", "float va = saturate(C.a);\n" + SOFT_CODE) if soft else CODE[kind]
        c = custom(m, code, ["T", "M", "C", "Clip", "Key"] + list(params), unreal.CustomMaterialOutputType.CMOT_FLOAT4, -500, 0)
        rgb = mask(m, c, (True, True, True, False), -250, 0)
        MEL.connect_material_property(rgb, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
        if kind not in ("Opaque", "Solid"):
            a = mask(m, c, (False, False, False, True), -250, 150)
            MEL.connect_material_property(a, "", unreal.MaterialProperty.MP_OPACITY_MASK if kind == "Masked" else unreal.MaterialProperty.MP_OPACITY)
    MEL.connect_material_expressions(tex, "RGBA", c, "T"); MEL.connect_material_expressions(msk, "RGBA", c, "M")
    MEL.connect_material_expressions(col, "", c, "C")
    if kind != "Distortion":
        MEL.connect_material_expressions(vc, "R", c, "Clip"); MEL.connect_material_expressions(key, "", c, "Key")
        for n, p in params.items(): MEL.connect_material_expressions(p, "", c, n)
    else:
        MEL.connect_material_expressions(params["HasMask"], "", c, "HasMask")
    MEL.recompile_material(m); EAL.save_loaded_asset(m); return m


def build_tonemap(folder, lin_default):
    m = get_or_create(folder, "PP_SM2Tonemap", unreal.Material, unreal.MaterialFactoryNew())
    MEL.delete_all_material_expressions(m)
    m.set_editor_property("material_domain", unreal.MaterialDomain.MD_POST_PROCESS)
    m.set_editor_property("blendable_location", unreal.BlendableLocation.BL_REPLACING_TONEMAPPER)
    sp = node(m, unreal.MaterialExpressionScreenPosition, -1300, 0)
    dt = node(m, unreal.MaterialExpressionTextureObjectParameter, -1300, 200)
    dt.set_editor_property("parameter_name", "Distort"); dt.set_editor_property("sampler_type", unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR)
    dt.set_editor_property("texture", lin_default)
    don = scalar(m, "DistortOn", 0.0, 400); dsz = scalar(m, "DistortSize", 512.0, 480)
    uv = custom(m, DISTORT_UV, ["UV", "D", "On", "Size"], unreal.CustomMaterialOutputType.CMOT_FLOAT2, -1000, 0)
    MEL.connect_material_expressions(sp, "ViewportUV", uv, "UV"); MEL.connect_material_expressions(dt, "", uv, "D")
    MEL.connect_material_expressions(don, "", uv, "On"); MEL.connect_material_expressions(dsz, "", uv, "Size")
    st = node(m, unreal.MaterialExpressionSceneTexture, -700, 0)
    st.set_editor_property("scene_texture_id", unreal.SceneTextureId.PPI_POST_PROCESS_INPUT0)
    try: st.set_editor_property("filtered", True)  # bilinear resample (game PS_SAMPLERS[4], INFERRED)
    except Exception as e: print("note SceneTexture filtered", e)
    MEL.connect_material_expressions(uv, "", st, "UVs")
    ex = scalar(m, "Exposure", 1.0 / 250.0, 200)
    lon = scalar(m, "LutOn", 0.0, 500); wpt = scalar(m, "WhitePoint", 1.375385, 600)
    lt = node(m, unreal.MaterialExpressionTextureObjectParameter, -900, 700)
    lt.set_editor_property("parameter_name", "Lut"); lt.set_editor_property("sampler_type", unreal.MaterialSamplerType.SAMPLERTYPE_LINEAR_COLOR)
    lt.set_editor_property("texture", lin_default)
    c = custom(m, TONEMAP, ["S", "Exposure", "LutOn", "WhitePoint", "L"], unreal.CustomMaterialOutputType.CMOT_FLOAT3, -400, 0)
    MEL.connect_material_expressions(st, "Color", c, "S"); MEL.connect_material_expressions(ex, "", c, "Exposure")
    MEL.connect_material_expressions(lon, "", c, "LutOn"); MEL.connect_material_expressions(wpt, "", c, "WhitePoint")
    MEL.connect_material_expressions(lt, "", c, "L")
    MEL.connect_material_property(c, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    MEL.recompile_material(m); EAL.save_loaded_asset(m); return m


def mi_key(b):
    k = [b["ueBlend"], b["texture"], b.get("alphaMask") or "", round(b["textureTint"], 4), round(b["contrast"], 4),
         [round(v, 4) for v in b["contrastKey"]], b["rgbTexAlpha"], b["outAlpha"]]
    if b.get("softDepth") and b["ueBlend"] in SOFT: k.append(round(b["softDepth"], 4))
    return f"MI_{b['ueBlend']}_{b['texture'] or 'none'}_" + hashlib.md5(json.dumps(k).encode()).hexdigest()[:6]


def ensure_mi(dest, b, masters):
    name = mi_key(b)
    mi = get_or_create(f"{dest}/MI", name, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    MEL.set_material_instance_parent(mi, masters[b["ueBlend"]])
    tex = unreal.load_asset(f"{dest}/Textures/{b['texture']}") if b["texture"] else None
    if tex: MEL.set_material_instance_texture_parameter_value(mi, "Tex", tex)
    if b.get("alphaMask"):
        mt = unreal.load_asset(f"{dest}/Textures/{b['alphaMask']}")
        if mt:
            MEL.set_material_instance_texture_parameter_value(mi, "Mask", mt)
            MEL.set_material_instance_scalar_parameter_value(mi, "HasMask", 1.0)
    for p, v in (("Tint", b["textureTint"]), ("Contrast", b["contrast"]), ("RgbTexAlpha", b["rgbTexAlpha"]), ("OutAlpha", b["outAlpha"])):
        MEL.set_material_instance_scalar_parameter_value(mi, p, float(v))
    if b["ueBlend"] in SOFT: MEL.set_material_instance_scalar_parameter_value(mi, "SoftDepth", float(b.get("softDepth") or 0.0))
    k = b["contrastKey"]; MEL.set_material_instance_vector_parameter_value(mi, "Key", unreal.LinearColor(k[0], k[1], k[2], 1))
    EAL.save_loaded_asset(mi); return mi


def frames_in(path):
    return sorted(glob.glob(os.path.join(path, "*.vfxframe.json"))) if os.path.isdir(path) else [path]


def ue(p, o=None):  # SM2 metres (x, y, z) -> UE cm (x, z, y)
    v = unreal.Vector(p[0] * 100, p[2] * 100, p[1] * 100)
    return v if o is None else v + o


mode = sys.argv[1]
if mode == "import":
    src = sys.argv[2]; dest = sys.argv[3] if len(sys.argv) > 3 else DEST
    assert dest.startswith("/Game/SM2_Work/") and not dest.startswith("/Game/SM2_Work/Original"), dest
    if len(sys.argv) > 4 and sys.argv[4] == "fresh" and EAL.does_directory_exist(dest):
        n = 1
        while EAL.does_directory_exist(f"{dest}_bak{n}"): n += 1
        EAL.rename_directory(dest, f"{dest}_bak{n}"); print("backup", f"{dest}_bak{n}")
    frames = [json.load(open(f, encoding="utf-8")) for f in frames_in(src)]
    root = src if os.path.isdir(src) else os.path.dirname(src)
    need = {b[k] for fr in frames for b in fr["batches"] for k in ("texture", "alphaMask") if b.get(k)}
    dist = {b["texture"] for fr in frames for b in fr["batches"] if b["distortion"]}
    files = [os.path.join(root, "textures", n.lower() + ".png") for n in sorted(need)]
    strips = sorted({fr["grading"]["strip"] for fr in frames if fr.get("grading", {}).get("strip")})
    files += [os.path.join(root, "textures", n + ".png") for n in strips]
    dist |= set(strips)
    files = [f for f in files if os.path.exists(f) or print("MISSING png", f)]
    tmp = os.path.join(unreal.Paths.project_saved_dir(), "sm2vfx_defaults"); os.makedirs(tmp, exist_ok=True)
    png(os.path.join(tmp, "sm2d_white.png"), (255, 255, 255, 255)); png(os.path.join(tmp, "sm2d_white_lin.png"), (255, 255, 255, 255))
    import_textures([os.path.join(tmp, "sm2d_white.png"), os.path.join(tmp, "sm2d_white_lin.png")], f"{dest}/Materials", set())
    import_textures(files, f"{dest}/Textures", dist)
    defaults = {"srgb": unreal.load_asset(f"{dest}/Materials/sm2d_white"), "lin": unreal.load_asset(f"{dest}/Materials/sm2d_white_lin")}
    kinds = {b["ueBlend"] for fr in frames for b in fr["batches"]} | {"Opaque"}
    masters = {k: build_master(f"{dest}/Materials", k, defaults) for k in sorted(kinds)}
    build_tonemap(f"{dest}/Materials", defaults["lin"])
    mis = {}
    for fr in frames:
        for b in fr["batches"]: mis.setdefault(mi_key(b), ensure_mi(dest, b, masters))
    print("import done", dest, "frames", len(frames), "textures", len(files), "masters", sorted(kinds), "MIs", len(mis))

elif mode == "render":
    fr = json.load(open(sys.argv[2], encoding="utf-8"))
    dest = sys.argv[3] if len(sys.argv) > 3 else DEST
    res = int(sys.argv[4]) if len(sys.argv) > 4 else int(fr.get("pixels", 1024))
    backdrop = float(sys.argv[5]) if len(sys.argv) > 5 else -1.0
    escale = float(sys.argv[6]) if len(sys.argv) > 6 else 1.0
    ground = float(sys.argv[7]) if len(sys.argv) > 7 else -1.0
    use_lut = (sys.argv[8] != "0") if len(sys.argv) > 8 else True
    w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    sds = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
    SDL = unreal.SubobjectDataBlueprintFunctionLibrary
    for old in eas.get_all_level_actors():
        if old.actor_has_tag(TAG): eas.destroy_actor(old)
    tag = [unreal.Name(TAG)]
    host = eas.spawn_actor_from_class(unreal.StaticMeshActor, BASE); host.tags = tag
    host.set_actor_label("SM2Cap_" + fr["effect"])
    host.root_component.set_editor_property("mobility", unreal.ComponentMobility.MOVABLE)
    root = sds.k2_gather_subobject_data_for_instance(host)[0]

    def add_pmc():
        h, fail = sds.add_new_subobject(unreal.AddNewSubobjectParams(parent_handle=root, new_class=unreal.ProceduralMeshComponent))
        return SDL.get_associated_object(SDL.get_data(h)) if hasattr(SDL, "get_associated_object") else SDL.get_object(SDL.get_data(h))

    cam = fr["camera"]; eye = ue(cam["eye"]); tgt = ue(cam["target"]); fwd = (tgt - eye); dist_cm = fwd.length(); fwd = fwd.normal()
    batches = list(fr["batches"])
    pre = []
    if backdrop >= 0: pre.append(S.backdrop_batch(fr, backdrop))  # opaque grey card behind the effect (HDR = exposed / exposure)
    if ground >= 0:  # opaque ground at groundY lit by the frame's lights (same vertices as the CPU reference)
        g = S.ground_batch(fr, ground)
        if g: pre.append(g)
    batches = pre + batches
    for li, l in enumerate(fr.get("lights", [])):  # UE point lights for scene use (unlit capture masters ignore them)
        pl = eas.spawn_actor_from_class(unreal.PointLight, ue(l["position"], BASE)); pl.tags = tag
        pl.set_actor_label(f"SM2Cap_light_{li}_{l.get('name', '')}")
        lc = pl.point_light_component; mx = max(max(l["color"]), 1e-6)
        lc.set_editor_property("intensity_units", unreal.LightUnits.UNITLESS)
        # mtl1-36: colour is relative to the ambient (CaptureLightUnits); unitless intensity = albedo/pi-scaled peak (HEURISTIC)
        lc.set_editor_property("intensity", mx * S.SURFACE)
        lc.set_light_color(unreal.LinearColor(l["color"][0] / mx, l["color"][1] / mx, l["color"][2] / mx, 1.0))
        lc.set_editor_property("attenuation_radius", l["radius"] * 100.0)
        # level-lights: useInverseSquaredFalloff lights are physical 1/d^2 in game (P0.w 1/400, colour x400, FUN_14224ecf0)
        inv = bool(l.get("useInverseSquaredFalloff", False))
        lc.set_editor_property("use_inverse_squared_falloff", inv)
        if not inv: lc.set_editor_property("light_falloff_exponent", 1.0)
        # SM2 window sat(1-(d/r)^4)^2 is PROVEN (particles PS 214); UE non-inverse-square falloff is (1-(d/r)^2)^n
        # (Engine DynamicLightingCommon.ush RadialAttenuation), which cannot express it. n = 1 is the best fit
        # (max error 0.13 vs 0.59 for the previous n = 4): APPROXIMATION.
    drawn = 0
    for i, b in enumerate(batches):
        mi = unreal.load_asset(f"{dest}/MI/{mi_key(b)}") if b["texture"] else None
        if b["ueBlend"] == "Opaque" and not b["texture"]:  # backdrop / ground
            mi = unreal.load_asset(f"{dest}/Materials/M_SM2Vfx_Backdrop")
            if mi:
                mi = unreal.MaterialLibrary.create_dynamic_material_instance(w, mi)
                mi.set_texture_parameter_value("Tex", unreal.load_asset(f"{dest}/Materials/sm2d_white_lin"))
        if not mi: print("skip batch (no MI)", i, b.get("emitter"), b.get("texture")); continue
        vs = b["vertices"]; n = len(vs) // STRIDE
        pos, uv0, uv1, uv2, uv3, col, nrm, tri = [], [], [], [], [], [], [], []
        for k in range(n):
            o = k * STRIDE
            pos.append(ue(vs[o:o + 3])); uv0.append(unreal.Vector2D(vs[o + 3], vs[o + 4]))
            uv1.append(unreal.Vector2D(vs[o + 5], vs[o + 6])); uv2.append(unreal.Vector2D(vs[o + 7], vs[o + 8]))
            uv3.append(unreal.Vector2D(vs[o + 11], vs[o + 12])); col.append(unreal.LinearColor(vs[o + 9], 0, 0, 1))
            nrm.append(fwd * -1.0)
        tri = list(S.triangles(b))
        pmc = add_pmc()
        pmc.create_mesh_section_linear_color(0, pos, tri, nrm, uv0, uv1, uv2, uv3, col, [], False)
        pmc.set_material(0, mi)
        pmc.set_editor_property("translucency_sort_priority", i)
        pmc.set_editor_property("cast_shadow", False)
        drawn += 1
    rot = unreal.MathLibrary.find_look_at_rotation(eye, tgt)

    def capture(fmt, source, show_flags_off):
        ca = eas.spawn_actor_from_class(unreal.SceneCapture2D, BASE + eye, rot); ca.tags = tag
        cc = ca.capture_component2d
        r = unreal.RenderingLibrary.create_render_target2d(w, res, res, fmt)
        cc.set_editor_property("texture_target", r); cc.set_editor_property("capture_source", source)
        # capture_every_frame stays on: with it off, editor capture_scene() skipped the PP tonemap material (measured, rules_v3)
        cc.set_editor_property("capture_every_frame", True)
        if cam.get("orthoWidth", 0) > 0:
            cc.set_editor_property("projection_type", unreal.CameraProjectionMode.ORTHOGRAPHIC)
            cc.set_editor_property("ortho_width", cam["orthoWidth"] * 100.0)
        else:
            # fovY is vertical, radians; the target is square, so UE's horizontal fov_angle equals it
            cc.set_editor_property("fov_angle", math.degrees(cam["fovY"]))
        fl = []
        for f in show_flags_off:
            sf = unreal.EngineShowFlagsSetting(); sf.set_editor_property("show_flag_name", f); sf.set_editor_property("enabled", False); fl.append(sf)
        cc.set_editor_property("show_flag_settings", fl)
        pps = cc.get_editor_property("post_process_settings")
        for p, v in (("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL), ("auto_exposure_apply_physical_camera_exposure", False),
                     ("auto_exposure_bias", 0.0), ("bloom_intensity", 0.0), ("vignette_intensity", 0.0),
                     ("local_exposure_highlight_contrast_scale", 1.0), ("local_exposure_shadow_contrast_scale", 1.0)):
            try: pps.set_editor_property("override_" + p, True); pps.set_editor_property(p, v)
            except Exception as e: print("note pps", p, e)
        cc.set_editor_property("post_process_settings", pps)
        return cc, r

    OFF = ("Bloom", "Fog", "AtmosphericFog", "Atmosphere", "VolumetricFog", "MotionBlur", "TemporalAA", "AntiAliasing", "EyeAdaptation",
           "LensFlares", "Vignette", "Grain", "DepthOfField", "AmbientOcclusion", "ScreenSpaceReflections", "LocalExposure", "Cloud")
    dist_comps = [pc for pc in host.get_components_by_class(unreal.ProceduralMeshComponent)
                  if pc.get_material(0) and pc.get_material(0).get_name().startswith("MI_Distortion")]
    # Pass 1 (only if the frame has distortion): accumulate the screen-UV shift in a float target.
    rtd = None
    if dist_comps:
        cd, rtd = capture(unreal.TextureRenderTargetFormat.RTF_RGBA16F, unreal.SceneCaptureSource.SCS_SCENE_COLOR_HDR, OFF)
        cd.set_editor_property("primitive_render_mode", unreal.SceneCapturePrimitiveRenderMode.PRM_USE_SHOW_ONLY_LIST)
        for pc in dist_comps: cd.show_only_component(pc)
        cd.capture_scene()
    # Pass 2: everything else, tonemapped by the capture tonemap, resampled by the shift.
    c, rt = capture(unreal.TextureRenderTargetFormat.RTF_RGBA8, unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR, OFF)
    for pc in dist_comps: c.hide_component(pc)
    pps = c.get_editor_property("post_process_settings")
    pp = unreal.MaterialLibrary.create_dynamic_material_instance(w, unreal.load_asset(f"{dest}/Materials/PP_SM2Tonemap"))
    pp.set_scalar_parameter_value("Exposure", fr["exposure"] * escale)
    gr = fr.get("grading") or {}
    pp.set_scalar_parameter_value("WhitePoint", float(gr.get("whitePointScale", 1.375385)))
    lut = unreal.load_asset(f"{dest}/Textures/{gr['strip']}") if use_lut and gr.get("strip") else None
    pp.set_scalar_parameter_value("LutOn", 1.0 if lut else 0.0)
    if lut: pp.set_texture_parameter_value("Lut", lut)
    elif use_lut and gr.get("strip"): print("MISSING lut strip", gr["strip"])
    if rtd:
        pp.set_texture_parameter_value("Distort", rtd); pp.set_scalar_parameter_value("DistortOn", 1.0)
        pp.set_scalar_parameter_value("DistortSize", float(res))
    wb = unreal.WeightedBlendables(); wb.set_editor_property("array", [unreal.WeightedBlendable(1.0, pp)])
    pps.set_editor_property("weighted_blendables", wb)
    c.set_editor_property("post_process_settings", pps); c.set_editor_property("post_process_blend_weight", 1.0)
    c.capture_scene(); builtins._sm2_vfxframe = (rt, pp, c, rtd)
    print("captured", fr["effect"], fr["time"], "batches", drawn, "distortion", len(dist_comps), "res", res, "lights", len(fr.get("lights", [])),
          "lut", gr.get("strip") if lut else "off")

elif mode == "export":
    out = sys.argv[2]; os.makedirs(os.path.dirname(out), exist_ok=True)
    w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    rt, pp, c, rtd = builtins._sm2_vfxframe
    c.capture_scene()
    unreal.RenderingLibrary.export_render_target(w, rt, os.path.dirname(out), os.path.basename(out)); print("wrote", out)
    if len(sys.argv) <= 3 or sys.argv[3] != "keep":
        eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
        for old in eas.get_all_level_actors():
            if old.actor_has_tag(TAG): eas.destroy_actor(old)
