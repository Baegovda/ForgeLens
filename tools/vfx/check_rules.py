"""Measure a render of the rule scene (sm2_vfx_rules.vfxframe.json from --vfxue-rules) against its 'expect' entries.
usage: check_rules.py <render.png> <sm2_vfx_rules.vfxframe.json>"""
import json, sys, numpy as np
from PIL import Image
j = json.load(open(sys.argv[2]))
im = np.asarray(Image.open(sys.argv[1]).convert('RGB')).astype(float); H, W, _ = im.shape
px = W / 12.0
def ramp_shift(row_in, row_ref, x0, x1):
    a = row_in[x0:x1]; best = None
    for s in np.arange(-40, 40.01, 0.05):
        xs = np.arange(x0, x1) - s  # content at x came from x - s ... test both conventions by sign
        b = np.interp(np.arange(x0, x1) + s, np.arange(len(row_ref)), row_ref)
        e = np.mean((a - b) ** 2)
        if best is None or e < best[0]: best = (e, s)
    return best[1], best[0]
rows = []; worst = 0
for e in j['expect']:
    x, y = e['u'] * W, e['v'] * H
    if e['what'] == 'display':
        r = im[int(y) - 10:int(y) + 10, int(x) - 10:int(x) + 10, 0].mean()
        d = r - e['value']; worst = max(worst, abs(d)) if not e['id'].startswith('x') else worst
        rows.append((e['id'], round(e['value'], 1), round(r, 1), round(d, 1)))
    else:
        yi = int(y); ref = im[int(y - 0.8 * px), :, 0]  # ramp row above the distortion quad (inside ramp, outside quad)
        x0, x1 = int(x - 0.4 * px), int(x + 0.4 * px)
        s, err = ramp_shift(im[yi, :, 0], ref, x0, x1)
        rows.append((e['id'], round(e['value'], 2), s, round(s - e['value'], 2), 'mse', round(err, 1)))
for r in rows: print(*r)
print('max |display err|', round(worst, 2))
