# Shared hub/level-light → Unreal rules (viewer + UE). Keeps one-off ue_r97_/ue_r98_ scripts from drifting.
# Mirrors LibSaber.SM2.Assets.Effects.LevelLightUe.
#
# Evidence:
#   ColorCtrl order [A,R,G,B] — PROVEN (R97y)
#   Colour decode (c/255)^2.2 — PROVEN (level-lights DATA / CaptureLightUnits.LightColorToLinear)
#   Position SM2 m (x,y,z) → UE cm (x*100, z*100, y*100) — PROVEN (ue_vfx_frame / ue_r66_level)
#   Intensity candela = K * Intensity — HEURISTIC FIT (R98b K=10); not from RE
#   spot→point when direction missing — INFERRED from TSV schema (lights_tsv2 has no direction column)

INTENSITY_FIT_K = 10.0
INTENSITY_EVIDENCE = "HEURISTIC FIT K=10 (R98b); ARGB colour order PROVEN; spot→point when direction missing INFERRED"
COLOR_ORDER_EVIDENCE = "PROVEN R97y: ColorCtrl = [A,R,G,B]"


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


def ue_candela(intensity, k=INTENSITY_FIT_K):
    return float(k) * float(intensity)


def attenuation_radius_cm(radius_ctrl_metres, fallback_metres=10.0):
    r = float(radius_ctrl_metres or 0.0) or float(fallback_metres)
    return r * 100.0


def light_row_to_ue(row, k=INTENSITY_FIT_K, spot_scale=1.0):
    """Map a lights TSV/dict row to UE spawn params. Spots without direction become points (INFERRED)."""
    lt = (row.get("lightType") or "point").strip().lower()
    x, y, z = float(row["x"]), float(row["y"]), float(row["z"])
    I = float(row.get("Intensity") or 0)
    if I <= 0:
        return None
    loc = metres_to_ue_cm(x, y, z)
    rgb = color_ctrl_to_linear_rgb(row["ColorCtrl"])
    scale = float(spot_scale) if lt == "spot" else 1.0
    return {
        "lightType": "point",  # spot→point when no direction in dump
        "authoredType": lt,
        "location": loc,
        "intensity": ue_candela(I, k) * scale,
        "color_linear": rgb,
        "attenuation_radius": attenuation_radius_cm(row.get("RadiusCtrl")),
        "cast_shadows": False,
        "evidence": INTENSITY_EVIDENCE,
    }
