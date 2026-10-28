"""Shared scene helpers for *.vfxframe.json renders (pure Python: used by tools/ue/ue_vfx_frame.py inside UE and by
tools/vfx/vfxframe_ref.py), mtl1-35.

ground_batch: opaque ground plane at frame["groundY"] lit by frame["lights"] with the browser ground shading
(EffectRenderer GroundFs), mtl1-36 capture-calibrated units (CaptureLightUnits): per channel
  exposed = grey * (1 + ALBEDO / pi * sum_lights(colour * att * max(0, l.y))),  att = sat(1 - (d / radius)^4)^2
colour = relative light (1 = particle ambient = capture avgLum), att from GPU particle CS s4462; ALBEDO 0.18 HEURISTIC
(the browser's ambient 0.6 maps to grey), written per vertex as HDR (exposed / exposure) so the UE capture and the CPU reference interpolate the same values.
Triangles with a vertex closer than NEAR metres to the eye plane are dropped (identical clipping in both renderers).
"""
import math

STRIDE = 13
NEAR = 0.5


def _sub(a, b): return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def _norm(a):
    l = math.sqrt(_dot(a, a)) or 1.0
    return [a[0] / l, a[1] / l, a[2] / l]


ALBEDO = 0.18
MAX_EXPOSED = 64.0
SURFACE = ALBEDO / math.pi


def attenuation(dist, radius):
    """s4462 point light attenuation sat(1 - d^4 / r^4)^2 (CaptureLightUnits.Attenuation)."""
    q = (dist / max(radius, 1e-4)) ** 2
    a = min(max(1.0 - q * q, 0.0), 1.0)
    return a * a


def ground_light(p, lights, exposure=None):
    """Browser GroundFs light factor at world point p (rgb), relative to the ambient: 1 + albedo/pi * sum(...)."""
    out = [1.0, 1.0, 1.0]
    for l in lights:
        d = _sub(l["position"], p); dist = math.sqrt(_dot(d, d))
        f = attenuation(dist, max(0.01, l["radius"]))
        if f <= 0: continue
        ny = max(0.0, d[1] / dist) if dist > 1e-6 else 1.0
        k = SURFACE * f * ny
        for c in range(3): out[c] += l["color"][c] * k
    return out


def ground_batch(fr, grey, cells=64):
    """Opaque lit ground batch (ueBlend Opaque, untextured) or None. grey = exposed ambient level."""
    cam = fr["camera"]; eye = cam["eye"]; tgt = cam["target"]; E = fr["exposure"]; y = fr.get("groundY", 0.0)
    fwd = _norm(_sub(tgt, eye)); dist = math.sqrt(_dot(_sub(tgt, eye), _sub(tgt, eye)))
    half = dist * 2.0; cx, cz = tgt[0], tgt[2]
    lights = fr.get("lights", [])
    verts, keep = [], []
    n = cells + 1
    for j in range(n):
        for i in range(n):
            p = [cx - half + 2 * half * i / cells, y, cz - half + 2 * half * j / cells]
            L = ground_light(p, lights, E)
            L = [min(c, MAX_EXPOSED / max(grey, 1e-6)) for c in L]  # VfxFrameExport.MaxExposed (UE half-float safety)
            verts += [p[0], p[1], p[2], i / cells, j / cells, grey / E * L[0], grey / E * L[1], grey / E * L[2], 1.0, 0.0, 0.0, 0.0, 0.0]
            keep.append(_dot(_sub(p, eye), fwd) > NEAR)
    idx = []
    for j in range(cells):
        for i in range(cells):
            a = j * n + i; b = a + 1; c = a + n + 1; d = a + n
            if keep[a] and keep[b] and keep[c] and keep[d]: idx += [a, b, c, a, c, d]
    if not idx: return None
    return {"ueBlend": "Opaque", "kind": "Ground", "texture": "", "alphaMask": "", "textureTint": 1, "contrast": 0, "contrastKey": [0, 0, 0],
            "rgbTexAlpha": 1, "outAlpha": 1, "softDepth": 0, "quads": 0, "vertices": verts, "indices": idx, "emitter": "ground", "distortion": False}


def backdrop_batch(fr, level):
    """Opaque grey card perpendicular to the view at 2x the target distance (mtl1-34), HDR = level / exposure."""
    cam = fr["camera"]; eye = cam["eye"]; tgt = cam["target"]
    f = _sub(tgt, eye); dist = math.sqrt(_dot(f, f)); fwd = _norm(f)
    right = _norm([-fwd[2], 0.0, fwd[0]])  # fwd x (0,1,0)
    up = [right[1] * fwd[2] - right[2] * fwd[1], right[2] * fwd[0] - right[0] * fwd[2], right[0] * fwd[1] - right[1] * fwd[0]]
    ctr = [tgt[k] + fwd[k] * dist * 2.0 for k in range(3)]; s = dist * 4.0
    hdr = level / fr["exposure"]; v = []
    for (a, b), (u, t) in zip([(-1, -1), (1, -1), (1, 1), (-1, 1)], [(0, 1), (1, 1), (1, 0), (0, 0)]):
        p = [ctr[k] + right[k] * s * a + up[k] * s * b for k in range(3)]
        v += [p[0], p[1], p[2], u, t, hdr, hdr, hdr, 1, 0, 0, u, t]
    return {"ueBlend": "Opaque", "kind": "Backdrop", "texture": "", "alphaMask": "", "textureTint": 1, "contrast": 0, "contrastKey": [0, 0, 0],
            "rgbTexAlpha": 1, "outAlpha": 1, "softDepth": 0, "quads": 1, "vertices": v, "emitter": "backdrop", "distortion": False}


def triangles(b):
    """Triangle index list of a batch: explicit indices, else two per quad."""
    if b.get("indices"): return b["indices"]
    t = []
    for q in range(len(b["vertices"]) // STRIDE // 4):
        a = q * 4; t += [a, a + 1, a + 2, a, a + 2, a + 3]
    return t


def lut_coord(x):
    """PROVEN apply mapping (hdr PS 44/47, mtl1-38; inverse of the s1996 bake input): exposed HDR -> LUT coordinate 0..1."""
    grey, rng = 0.43, 14.0
    c0 = 0.18 * 2.0 ** (-grey * rng)
    return min(max(grey + math.log2((max(x, 0.0) + c0) / 0.18) / rng, 0.0), 1.0)


def distort_resample(hdr, shift, cover):
    """mtl1-40 PS 5160 (PROVEN gate): shift only where the shift is non-zero and every bilinear tap of the shifted uv lies
    in the DUDV footprint; the scene is resampled bilinearly, clamped (sampler PS_SAMPLERS[4]: bilinear INFERRED).
    numpy is imported here so the module stays numpy-free inside UE."""
    import numpy as np
    n = hdr.shape[0]; gy, gx = np.mgrid[0:n, 0:n].astype(np.float32)
    px = gx + shift[..., 0] * n; py = gy + shift[..., 1] * n  # texel-space sample position (pixel centre -> index)
    x0 = np.floor(px).astype(int); y0 = np.floor(py).astype(int); fx = px - x0; fy = py - y0
    cl = lambda a: np.clip(a, 0, n - 1)
    ok = (shift != 0).any(-1)
    for dy, wy in ((0, 1 - fy), (1, fy)):
        for dx, wx in ((0, 1 - fx), (1, fx)):
            ok &= ~((wx * wy > 0) & ~cover[cl(y0 + dy), cl(x0 + dx)])
    out = sum(hdr[cl(y0 + dy), cl(x0 + dx)] * (wx * wy)[..., None] for dy, wy in ((0, 1 - fy), (1, fy)) for dx, wx in ((0, 1 - fx), (1, fx)))
    return np.where(ok[..., None], out, hdr), int(ok.sum())
