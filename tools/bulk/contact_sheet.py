VIEW_SIDE = {"imp_bur_door_03"}  # door face points sideways
# Contact sheet: /workspace/bulk/caps_re|caps/<asset>/front.png (+ status colour from rows.json) -> contact_sheet.jpg
import json, os, sys
from PIL import Image, ImageDraw, ImageFont
B = "/workspace/bulk"; rows = {r["asset"]: r for r in json.load(open(f"{B}/rows.json"))}
font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", 15)
W, H, cols = 320, 360, 7
assets = list(rows)
sheet = Image.new("RGB", (W * cols, H * ((len(assets) + cols - 1) // cols)), (24, 24, 24))
col = {"PASS": (60, 170, 80), "WARN": (215, 170, 40), "FAIL": (210, 60, 60)}
for i, a in enumerate(assets):
    x, y = (i % cols) * W, (i // cols) * H
    # caps_re: recaptures after texture compilation finished (first pass caught some assets with grey placeholder textures)
    views = ("side", "front") if a in VIEW_SIDE else ("front", "side")
    p = next((f"{B}/{d}/{a}/{v}.png" for d in ("caps_re", "caps") for v in views if os.path.exists(f"{B}/{d}/{a}/{v}.png")), None)
    if p:
        im = Image.open(p).convert("RGB"); im.thumbnail((W - 8, H - 44)); sheet.paste(im, (x + (W - im.width) // 2, y + 4))
    else:
        ImageDraw.Draw(sheet).text((x + 20, y + 140), "no capture", fill=(150, 150, 150), font=font)
    d = ImageDraw.Draw(sheet); st = rows[a]["st"]
    d.rectangle([x + 2, y + H - 40, x + W - 3, y + H - 3], fill=(36, 36, 36), outline=col[st], width=2)
    d.text((x + 8, y + H - 37), a[:34], fill=(235, 235, 235), font=font)
    d.text((x + 8, y + H - 20), "%s  %s" % (st, rows[a]["cat"]), fill=col[st], font=font)
sheet.save(f"{B}/contact_sheet.jpg", quality=88); print(sheet.size)
