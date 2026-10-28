"""CPU reference renderer for *.vfxframe.json (same formulas as CaptureRules / VfxFrameExport.Composite).
usage: vfxframe_ref.py <frame.json> <out.png> [res] [backdrop_exposed]"""
import json, sys, os, math, numpy as np
from PIL import Image
fr = json.load(open(sys.argv[1])); out = sys.argv[2]
res = int(sys.argv[3]) if len(sys.argv) > 3 else 512
bd = float(sys.argv[4]) if len(sys.argv) > 4 else -1
root = os.path.dirname(sys.argv[1]); E = fr["exposure"]
cam = fr["camera"]; eye = np.array(cam["eye"], float); tgt = np.array(cam["target"], float); up = np.array(cam.get("up", [0, 1, 0]), float)
fwd = tgt - eye; fwd /= np.linalg.norm(fwd); right = np.cross(fwd, up); right /= np.linalg.norm(right); upv = np.cross(right, fwd)
ortho = cam.get("orthoWidth", 0)
th = math.tan(cam["fovY"] / 2) if not ortho else 0
def project(p):
    d = p - eye; x, y, z = d @ right, d @ upv, d @ fwd
    if ortho: sx, sy, w = x / (ortho / 2), y / (ortho / 2), np.ones_like(z)
    else: sx, sy, w = x / (z * th), y / (z * th), z
    return (sx + 1) / 2 * res, (1 - sy) / 2 * res, w
cache = {}
def tex(name, linear=False):
    if name not in cache:
        p = os.path.join(root, "textures", name.lower() + ".png")
        a = np.asarray(Image.open(p).convert("RGBA")).astype(np.float32) / 255
        if not linear and not any(k in name for k in ("_lin", "sm2r_ramp", "sm2r_nrm")):
            a[..., :3] = np.where(a[..., :3] <= 0.04045, a[..., :3] / 12.92, ((a[..., :3] + 0.055) / 1.055) ** 2.4)
        cache[name] = a
    return cache[name]
def sample(t, u, v):  # bilinear, wrap
    h, w = t.shape[:2]; x = u * w - 0.5; y = v * h - 0.5; x0 = np.floor(x); y0 = np.floor(y); fx = (x - x0)[:, None]; fy = (y - y0)[:, None]
    x0 = x0.astype(int); y0 = y0.astype(int)
    g = lambda yy, xx: t[yy % h, xx % w]
    return (g(y0, x0) * (1 - fx) + g(y0, x0 + 1) * fx) * (1 - fy) + (g(y0 + 1, x0) * (1 - fx) + g(y0 + 1, x0 + 1) * fx) * fy
hdr = np.zeros((res, res, 3), np.float32)
if bd >= 0: hdr[:] = bd / E
shift = np.zeros((res, res, 2), np.float32)
for b in fr["batches"]:
    v = np.array(b["vertices"], np.float32).reshape(-1, 13); kind = b["ueBlend"]
    T = tex(b["texture"], b["distortion"]) if b["texture"] else None
    M = tex(b["alphaMask"]) if b.get("alphaMask") else None
    for q in range(len(v) // 4):
        Q = v[q * 4:q * 4 + 4]
        sx, sy, w = project(Q[:, :3])
        for tri in ((0, 1, 2), (0, 2, 3)):
            X, Y, W = sx[list(tri)], sy[list(tri)], w[list(tri)]
            if np.any(W <= 0): continue
            x0, x1 = max(int(np.floor(X.min())), 0), min(int(np.ceil(X.max())), res - 1)
            y0, y1 = max(int(np.floor(Y.min())), 0), min(int(np.ceil(Y.max())), res - 1)
            if x1 < x0 or y1 < y0: continue
            px, py = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            d = (Y[1] - Y[2]) * (X[0] - X[2]) + (X[2] - X[1]) * (Y[0] - Y[2])
            if abs(d) < 1e-9: continue
            l0 = ((Y[1] - Y[2]) * (px - X[2]) + (X[2] - X[1]) * (py - Y[2])) / d
            l1 = ((Y[2] - Y[0]) * (px - X[2]) + (X[0] - X[2]) * (py - Y[2])) / d
            l2 = 1 - l0 - l1
            m = (l0 >= 0) & (l1 >= 0) & (l2 >= 0)
            if not m.any(): continue
            L = np.stack([l0[m] / W[0], l1[m] / W[1], l2[m] / W[2]], 1); L /= L.sum(1, keepdims=True)
            A = L @ Q[list(tri)]
            yy, xx = (py[m] - 0.5).astype(int), (px[m] - 0.5).astype(int)
            t = sample(T, A[:, 3], A[:, 4]) if T is not None else np.ones((len(A), 4), np.float32)
            if kind == "Opaque": hdr[yy, xx] = t[:, :3] * A[:, 5:8]; continue
            if kind == "Distortion":
                o = (t[:, :2] * 2.007874 - 1.007874) * A[:, 5:6] * np.clip(A[:, 8:9], 0, 1)
                mk = np.clip(10 * np.abs(o).sum(1, keepdims=True), 0, 1)
                shift[yy, xx] += np.clip(0.08 * mk * o, -0.02, 0.02); continue
            a = t[:, 3] * (sample(M, A[:, 11], A[:, 12])[:, 3] if M is not None else 1)
            clip = A[:, 9]; a = np.where(clip > 0, np.clip((a - clip) / np.maximum(1 - clip, 1e-4), 0, 1), a)
            lum = (t[:, :3] @ np.array([0.299, 0.587, 0.114], np.float32))[:, None]
            tx = lum + (t[:, :3] - lum) * b["textureTint"]
            tx = tx * (1 + b["contrast"] * np.clip(1 - np.linalg.norm(np.array(b["contrastKey"]) - tx, axis=1, keepdims=True), 0, 1))
            c = A[:, 5:8] * tx; va = np.clip(A[:, 8], 0, 1); dst = hdr[yy, xx]
            if kind == "AlphaComposite":
                k = va * a if b["rgbTexAlpha"] else va; ao = va * a if b["outAlpha"] else 0 * va
                hdr[yy, xx] = c * k[:, None] + dst * (1 - ao)[:, None]
            elif kind == "Translucent": al = (va * a)[:, None]; hdr[yy, xx] = c * al + dst * (1 - al)
            elif kind == "Masked": keep = a >= 0.5; hdr[yy[keep], xx[keep]] = (c * va[:, None])[keep]
            elif kind == "Additive": hdr[yy, xx] = dst + c * (va * a)[:, None]
if np.abs(shift).max() > 0:
    gy, gx = np.mgrid[0:res, 0:res]; sx_ = np.clip(np.rint(gx + shift[..., 0] * res), 0, res - 1).astype(int); sy_ = np.clip(np.rint(gy + shift[..., 1] * res), 0, res - 1).astype(int)
    hdr = hdr[sy_, sx_]
x = np.maximum(hdr * E, 0); A_, B_, C_, D_, E_, F_ = .12, .05, .45, .2, .02, .08
h = (x * (A_ * x + C_ * B_) + D_ * E_) / (x * (A_ * x + B_) + D_ * F_) - E_ / F_
l = np.clip(h * 1.375385, 0, 1); s = np.where(l <= 0.0031308, 12.92 * l, 1.055 * np.power(l, 1 / 2.4) - 0.055)
Image.fromarray(np.clip(s * 255 + 0.5, 0, 255).astype(np.uint8)).save(out); print("wrote", out)
