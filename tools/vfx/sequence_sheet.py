#!/usr/bin/env python3
"""mtl1-40 UE frame sequences: contact sheet + GIF + per-frame comparison of a rendered sequence against the CPU reference.

usage: sequence_sheet.py <out_prefix> <effect> <ue_dir> [ref_dir] [--ref-prefix P]
  frames: <ue_dir>/<effect>_<ms>ms.png (render+export per *.vfxframe.json, tools/ue/vfx_sequence.ps1), sorted by time;
  reference: <ref_dir>/<P><effect>_<ms>ms.png (tools/vfx/vfxframe_ref.py). Writes <out_prefix>_sheet.png (row 1 UE,
  row 2 CPU reference, row 3 |diff| x4), <out_prefix>.gif (UE | ref side by side) and <out_prefix>.json (per frame mean
  |diff| and PSNR in 8-bit sRGB)."""
import glob, json, os, re, sys
import numpy as np
from PIL import Image, ImageDraw


def frames(d, prefix, effect):
    out = []
    for p in glob.glob(os.path.join(d, f"{prefix}{effect}_*ms.png")):
        m = re.fullmatch(re.escape(prefix + effect) + r"_(\d+)ms\.png", os.path.basename(p))
        if m: out.append((int(m.group(1)), p))
    return sorted(out)


def load(p, size=None):
    im = Image.open(p).convert("RGB")
    if size and im.size != size: im = im.resize(size, Image.BILINEAR)
    return np.asarray(im).astype(np.float32)


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    rp = argv[argv.index("--ref-prefix") + 1] if "--ref-prefix" in argv else ""
    if "--ref-prefix" in argv: args.remove(rp)
    out, effect, ue_dir = args[:3]; ref_dir = args[3] if len(args) > 3 else None
    ue = frames(ue_dir, "", effect)
    if not ue: print("no frames for", effect, "in", ue_dir); return 1
    ref = dict(frames(ref_dir, rp, effect)) if ref_dir else {}
    size = Image.open(ue[0][1]).size; th = 256
    rows = 3 if ref else 1
    sheet = Image.new("RGB", (th * len(ue), th * rows + 18 * rows), (16, 16, 16)); dr = ImageDraw.Draw(sheet)
    stats, gif = [], []
    for i, (ms, p) in enumerate(ue):
        a = load(p)
        sheet.paste(Image.fromarray(a.astype(np.uint8)).resize((th, th)), (i * th, 18)); dr.text((i * th + 4, 2), f"UE {ms} ms", fill=(255, 255, 255))
        g = [Image.fromarray(a.astype(np.uint8))]
        if ms in ref:
            b = load(ref[ms], size); d = np.abs(a - b); mse = float((d ** 2).mean())
            st = {"ms": ms, "meanAbs": round(float(d.mean()), 3), "psnr": round(float(10 * np.log10(255 ** 2 / max(mse, 1e-9))), 2)}
            stats.append(st)
            sheet.paste(Image.fromarray(b.astype(np.uint8)).resize((th, th)), (i * th, th + 36)); dr.text((i * th + 4, th + 20), f"ref {ms} ms", fill=(255, 255, 255))
            sheet.paste(Image.fromarray(np.clip(d * 4, 0, 255).astype(np.uint8)).resize((th, th)), (i * th, 2 * th + 54))
            dr.text((i * th + 4, 2 * th + 38), f"|d|x4 mean {st['meanAbs']} psnr {st['psnr']}", fill=(255, 255, 255))
            g.append(Image.fromarray(b.astype(np.uint8)))
        frame = Image.new("RGB", (size[0] * len(g), size[1])); [frame.paste(x, (k * size[0], 0)) for k, x in enumerate(g)]
        gif.append(frame)
    sheet.save(out + "_sheet.png")
    gif[0].save(out + ".gif", save_all=True, append_images=gif[1:], duration=200, loop=0)
    with open(out + ".json", "w") as f: json.dump({"effect": effect, "frames": [m for m, _ in ue], "compare": stats}, f, indent=1)
    print(effect, "frames", len(ue), "compare", stats)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
