# R91 floor exposure: bottom 35 % of the viewer-matched views (floor band) -> mean luma, sd, % clipped (>=245), vs the viewer levelbench shots.
import sys, os
from PIL import Image, ImageStat
def band(f):
    im = Image.open(f).convert("L"); w, h = im.size; b = im.crop((0, int(h * .65), w, h)); s = ImageStat.Stat(b)
    hist = b.histogram(); n = sum(hist); clip = sum(hist[245:]) / n * 100
    return "mean %.1f sd %.1f clip %.2f%%" % (s.mean[0], s.stddev[0], clip)
lv = sys.argv[1]; sh = sys.argv[2]
print("viewer persp", band(os.path.join(lv, "level_persp.png"))); print("viewer close", band(os.path.join(lv, "level_close.png")))
for t in sys.argv[3:]:
    for v in ("vpersp", "vclose"):
        print(t, v, band(os.path.join(sh, t, "ue_r66_%s.png" % v)))
