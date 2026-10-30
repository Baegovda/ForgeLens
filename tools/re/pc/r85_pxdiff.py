# R85: pixel diff of two frames (PNG, or the viewer's GPU readback .raw = "w h\n" + RGBA bottom-up): % pixels differing,
# mean abs per channel, max summed abs, % pixels with summed abs > 24, bbox of changed pixels. Optional 3rd arg: save a.raw as PNG.
import sys, numpy as np
from PIL import Image
def load(p):
    if p.endswith('.raw'):
        d=open(p,'rb').read(); i=d.index(b'\n'); w,h=map(int,d[:i].split()); return np.frombuffer(d[i+1:],np.uint8).reshape(h,w,4)[::-1,:,:3].astype(int)
    return np.asarray(Image.open(p).convert('RGB')).astype(int)
a=load(sys.argv[1]); b=load(sys.argv[2])
if len(sys.argv)>3: Image.fromarray(a.astype(np.uint8)).save(sys.argv[3])
if a.shape!=b.shape: print('size differs',a.shape,b.shape); sys.exit()
d=np.abs(a-b).sum(2); m=d>0; ys,xs=np.nonzero(m)
bb=f"{xs.min()},{ys.min()}-{xs.max()},{ys.max()}" if m.any() else "-"
print(f"size {a.shape[1]}x{a.shape[0]} diff_px {100*m.mean():.3f}% mean_abs {np.abs(a-b).mean():.4f} max {d.max()} gt24 {100*(d>24).mean():.3f}% bbox {bb}")
