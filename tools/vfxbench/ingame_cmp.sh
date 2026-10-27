#!/usr/bin/env bash
# Re-render the in-game comparison bench sheets (vfx3r/vfx3s) and the contact sheet.
# Usage: tools/vfxbench/ingame_cmp.sh <raw root> <out dir>
set -euo pipefail
R=${1:-/workspace/vfx3_raw}; O=${2:-/workspace/browser_run_shots/ingame_cmp}
FX="part_mg_frag_grenade_expl fx_bolter_muzzle_client part_mag_chainsaw_blood_splash_red_01 fx_mgs_hierophant_bomb_ee trail_stalker_bolter_smoke part_mg_candle_smoke_hub"
cd "$(dirname "$0")/../.."
dotnet build src/LibSaber.SM2Browser -c Release -v q -nologo >/dev/null
run() { local name=$1; shift; mkdir -p "$O/$name"
  env SM2_VFX_MESHES="$R/mesh1" "$@" xvfb-run -a -s "-screen 0 1920x1080x24" dotnet run --no-build --project src/LibSaber.SM2Browser -c Release -- \
    --vfxbench "$R/sfx" "$R/aliases/aliases/part_aliases/part_aliases.ps" "$R" "$O/$name" $FX > "$O/$name.log" 2>&1; }
run off
run blend SM2_VFX_BLEND_REA=1
run tint SM2_VFX_TINT_EXACT=1
run vat SM2_VFX_VAT=1
run all SM2_VFX_BLEND_REA=1 SM2_VFX_TINT_EXACT=1 SM2_VFX_VAT=1 SM2_VFX_CONTRAST=1
python3 - "$O" $FX <<'P'
import sys, numpy as np
from PIL import Image, ImageDraw
o, fx = sys.argv[1], sys.argv[2:]
cols = ["off", "blend", "tint", "vat", "all"]; W, H = 512, 256
sheet = Image.new("RGB", (W * len(cols), (H + 20) * len(fx)), (16, 16, 16)); d = ImageDraw.Draw(sheet)
for r, n in enumerate(fx):
    base = np.asarray(Image.open(f"{o}/off/{n}_sheet.png").convert("RGB"), dtype=np.float32)
    for c, k in enumerate(cols):
        im = Image.open(f"{o}/{k}/{n}_sheet.png").convert("RGB")
        delta = np.abs(np.asarray(im, dtype=np.float32) - base).mean()
        sheet.paste(im.resize((W, H)), (c * W, r * (H + 20) + 20))
        d.text((c * W + 4, r * (H + 20) + 4), f"{n} | {k} | d={delta:.2f}", fill=(230, 230, 230))
sheet.save(f"{o}/ingame_cmp_contact.png")
P
