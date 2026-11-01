# Shared hub/level-light → Unreal rules (viewer + UE). Keeps one-off ue_r97_/ue_r98_ scripts from drifting.
# Mirrors LibSaber.SM2.Assets.Effects.LevelLightUe.
#
# Evidence:
#   ColorCtrl order [A,R,G,B] — PROVEN (R97y)
#   Colour decode (c/255)^2.2 — PROVEN (level-lights DATA / CaptureLightUnits.LightColorToLinear)
#   Position SM2 m (x,y,z) → UE cm (x*100, z*100, y*100) — PROVEN (ue_vfx_frame / ue_r66_level)
#   P0.w + colour×400 invSq — PROVEN FUN_14224f010 / FUN_14224ecf0 (REA_FINDINGS)
#   UE Intensity = I * colorScale (no FIT K=10) — INFERRED unit bridge
#   Spot stays SpotLight with cones — PROVEN cone math; direction missing in TSV — INFERRED default forward

INV_SQUARED_COLOR_SCALE = 400.0
INV_SQUARED_SOFT = 0.0025
CONST_ATTEN_SOFT = 8e22

INTENSITY_EVIDENCE = (
    "PROVEN FUN_14224ecf0/FUN_14224f010: colour=(c/255)^2.2*I [*400 if invSq]; "
    "P0.w directional=0 / constAtten=8e22 / invSq=0.0025 / else 299*a^4+1 or 256^a. "
    "INFERRED UE: Intensity=I*colorScale (no FIT K=10); radius m→cm; soft exponent from attenCurveParam approx."
)
COLOR_ORDER_EVIDENCE = "PROVEN R97y: ColorCtrl = [A,R,G,B]"
SPOT_EVIDENCE = (
    "PROVEN FUN_14224ecf0 type1 spot cones; FUN_141384090 spotAngle/spotInnerAngleFactor. "
    "INFERRED UE outer=spotAngle, inner=spotAngle*factor; direction not in TSV."
)


def metres_to_ue_cm(x, y, z):
    return (float(x) * 100.0, float(z) * 100.0, float(y) * 100.0)


def parse_color_ctrl(color_ctrl):
    """Return (a,r,g,b) floats from '[A,R,G,B]' or list."""
    if isinstance(color_ctrl, (list, tuple)):
        vals = [float(v) for v in color_ctrl]
    else:
        vals = [float(v) for v in str(color_ctrl).strip().strip("[]").split(",")]
    if len(vals) < 4:
        raise ValueError("ColorCtrl needs 4 ARGB components: %r" % (color_ctrl,))
    return vals[0], vals[1], vals[2], vals[3]


def argb_to_linear_rgb(a, r, g, b):
    # pure 2.2 power (PROVEN level-lights), ignore A for colour
    return ((r / 255.0) ** 2.2, (g / 255.0) ** 2.2, (b / 255.0) ** 2.2)


def color_ctrl_to_linear_rgb(color_ctrl):
    a, r, g, b = parse_color_ctrl(color_ctrl)
    return argb_to_linear_rgb(a, r, g, b)


def color_scale(use_inverse_squared_falloff):
    return INV_SQUARED_COLOR_SCALE if use_inverse_squared_falloff else 1.0


def p0w(atten_curve_param, use_inverse_squared_falloff=False, is_const_atten=False, directional=False):
    """PROVEN FUN_14224f010."""
    if directional:
        return 0.0
    if use_inverse_squared_falloff:
        return INV_SQUARED_SOFT
    if is_const_atten:
        return CONST_ATTEN_SOFT
    a = float(atten_curve_param or 0.0)
    if a > 0.0:
        return 299.0 * (a ** 4) + 1.0
    return 256.0 ** a


def ue_intensity(intensity, use_inverse_squared_falloff=False):
    """INFERRED UE Intensity = authored I * engine colour scale (replaces FIT K=10)."""
    return float(intensity) * color_scale(use_inverse_squared_falloff)


def attenuation_radius_cm(radius_ctrl_metres, fallback_metres=10.0):
    r = float(radius_ctrl_metres or 0.0) or float(fallback_metres)
    return r * 100.0


def spot_cone_degrees(spot_angle, spot_inner_angle_factor):
    outer = float(spot_angle) if float(spot_angle or 0.0) > 0 else 45.0
    factor = max(0.0, min(1.0, float(spot_inner_angle_factor or 0.0)))
    inner = outer * factor
    if inner > outer:
        inner = outer
    return outer, inner


def ue_falloff_exponent(atten_curve_param, use_inverse_squared_falloff, is_const_atten):
    if use_inverse_squared_falloff:
        return 2.0
    if is_const_atten:
        return 0.0
    a = float(atten_curve_param or 0.0)
    if a <= 0.0:
        return max(0.1, min(16.0, 2.0 + a * 2.0))
    return max(0.1, min(16.0, 2.0 + a * 6.0))


def _truthy(v):
    if isinstance(v, bool):
        return v
    s = str(v or "").strip().lower()
    return s in ("1", "true", "yes", "y")


def light_row_to_ue(row, spot_scale=1.0):
    """Map a lights TSV/dict row to UE spawn params. Spots stay spots with cones (PROVEN)."""
    lt = (row.get("lightType") or "point").strip().lower()
    if lt not in ("point", "spot", "directional"):
        return None
    x, y, z = float(row["x"]), float(row["y"]), float(row["z"])
    I = float(row.get("Intensity") or 0)
    if I <= 0:
        return None
    inv_sq = _truthy(row.get("useInverseSquaredFalloff"))
    const_atten = _truthy(row.get("isConstAtten"))
    acp = float(row.get("attenCurveParam") or 0.0)
    spot_angle = float(row.get("spotAngle") or 0.0)
    spot_inner = float(row.get("spotInnerAngleFactor") or 0.0)
    loc = metres_to_ue_cm(x, y, z)
    rgb = color_ctrl_to_linear_rgb(row["ColorCtrl"])
    outer, inner = spot_cone_degrees(spot_angle, spot_inner)
    scale = float(spot_scale) if lt == "spot" else 1.0
    ue_type = "spot" if lt == "spot" else ("directional" if lt == "directional" else "point")
    return {
        "lightType": ue_type,
        "authoredType": lt,
        "location": loc,
        "intensity": ue_intensity(I, inv_sq) * scale,
        "color_linear": rgb,
        "attenuation_radius": attenuation_radius_cm(row.get("RadiusCtrl")),
        "use_inverse_squared": inv_sq,
        "falloff_exponent": ue_falloff_exponent(acp, inv_sq, const_atten),
        "p0w": p0w(acp, inv_sq, const_atten, lt == "directional"),
        "outer_cone_degrees": outer,
        "inner_cone_degrees": inner,
        "cast_shadows": False,
        "evidence": INTENSITY_EVIDENCE,
        "spot_evidence": SPOT_EVIDENCE,
    }
