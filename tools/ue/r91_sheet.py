# R91: before(legacy)/after(cur) cloth sheet from Shots/r91_cloth/run1/<subj_clip>/<tag>/t2_side.png
import os, sys
from PIL import Image, ImageDraw
R = sys.argv[1]; d = os.path.join(R, "run1"); rows = sorted(os.listdir(d)); W = 360
sheet = Image.new("RGB", (W * 2 + 160, W * len(rows)), "white"); g = ImageDraw.Draw(sheet)
for i, r in enumerate(rows):
    g.text((4, i * W + 8), r, fill="black")
    for j, t in enumerate(("legacy", "cur")):
        f = os.path.join(d, r, t, "t2_side.png")
        if os.path.exists(f):
            im = Image.open(f).convert("RGB"); im.thumbnail((W, W)); sheet.paste(im, (160 + j * W, i * W))
        g.text((164 + j * W, i * W + 4), t, fill="red")
o = os.path.join(R, "r91_cloth_sheet.png"); sheet.save(o); print(o, sheet.size)
