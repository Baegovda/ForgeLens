# mtl1-31 HEURISTIC visual penetration metric for ue_c28 captures (left half = skinned base, right half = cloth sim).
# Rows 230-500: inside each row's red-cloth span, % of grey (low-saturation, mid-bright) pixels = body/armour showing
# through the robe. Reports sim - base (delta); the base shows the robe skinned, so delta ~0 means no visible poke-through.
# Tuned for the red tech_priest robe only. Usage: python3 vis_intrusion.py c28_*.png
import sys, numpy as np
from PIL import Image
def intr(a, y0=230, y1=500):
    a = a.astype(int); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    red = (r > g + 12) & (r > b + 12); s = r + g + b
    grey = (~red) & (s > 60) & (s < 330) & (np.abs(r - g) < 20) & (np.abs(g - b) < 25)
    tot = bad = 0
    for y in range(y0, y1):
        xs = np.where(red[y])[0]
        if len(xs) < 10: continue
        x0, x1 = xs.min(), xs.max(); tot += x1 - x0 + 1; bad += int(grey[y, x0:x1 + 1].sum())
    return 100 * bad / max(tot, 1), tot
for p in sys.argv[1:]:
    a = np.asarray(Image.open(p).convert("RGB")); h = a.shape[1] // 2
    L, R = intr(a[:, :h]), intr(a[:, h:])
    print(f"{p} base {L[0]:.1f} sim {R[0]:.1f} delta {R[0]-L[0]:+.1f} spanRatio {R[1]/max(L[1],1):.2f}")
