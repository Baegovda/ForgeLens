#!/usr/bin/env python3
"""In-game reference vs bench columns (vfx3u/v). Usage: side_by_side.py <ref dir> <ingame_cmp dir>.
Each row: reference screenshot (scaled) + the given sheet tile (512 px, 4x2 layout) for off/blend/tint/vat/all."""
import sys, glob, os
from PIL import Image, ImageDraw
ref, o = sys.argv[1], sys.argv[2]
cols = ["off", "blend", "tint", "vat", "all"]; T = 384
def refimg(tag):
    f = [p for p in glob.glob(f"{ref}/*") if tag in os.path.basename(p)]
    return Image.open(f[0]).convert("RGB") if f else None
def tile(col, sub, fx, i):
    p = f"{o}/{col}/{sub}/{fx}_sheet.png" if sub else f"{o}/{col}/{fx}_sheet.png"
    s = Image.open(p).convert("RGB"); w = s.width // 4; h = s.height // 2
    return s.crop(((i % 4) * w, (i // 4) * h, (i % 4 + 1) * w, (i // 4 + 1) * h)).resize((T, T))
SETS = {  # name: (ref tag, sheet subdir, effect, frames)
    "grenade_early": ("154731", "early", "part_mg_frag_grenade_expl", [1, 3]),
    "grenade_late": ("154743", "", "part_mg_frag_grenade_expl", [2, 3]),
    "bolter_muzzle": ("154807", "early", "fx_bolter_muzzle_client", [1, 2]),
    "bolter_trail": ("154843", "", "trail_stalker_bolter_smoke", [2, 5]),
    "chainsaw_blood": ("155018", "early", "part_mag_chainsaw_blood_splash_red_01", [2, 4]),
}
for name, (tag, sub, fx, frames) in SETS.items():
    r = refimg(tag)
    if r is None: continue
    rw = int(r.width * T / r.height)
    out = Image.new("RGB", (rw + T * len(cols), (T + 18) * len(frames)), (16, 16, 16)); d = ImageDraw.Draw(out)
    for j, fi in enumerate(frames):
        y = j * (T + 18) + 18
        out.paste(r.resize((rw, T)), (0, y)); d.text((4, y - 16), f"in-game {tag}", fill=(230, 230, 230))
        for c, k in enumerate(cols):
            out.paste(tile(k, sub, fx, fi), (rw + c * T, y)); d.text((rw + c * T + 4, y - 16), f"{fx} f{fi} {k}", fill=(230, 230, 230))
    out.save(f"{o}/side_by_side_{name}.png")
    print("wrote", name)
