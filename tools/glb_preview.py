# glb_preview.py <in.glb> <out.png> [regex]: minimal numpy/PIL flat-shaded z-buffer render (side/top/persp); triangles whose JOINTS_0[0] node name matches the regex are red. Used for the mtl1-46 purity-seal check.
import json, struct, sys, numpy as np, re
from PIL import Image
path, out = sys.argv[1], sys.argv[2]; HL = re.compile(sys.argv[3] if len(sys.argv) > 3 else 'seal|paper', re.I)
b = open(path, 'rb').read(); jl = struct.unpack_from('<I', b, 12)[0]; g = json.loads(b[20:20+jl]); bin0 = 20 + jl + 8
CT = {5126: ('f', 4), 5125: ('I', 4), 5123: ('H', 2), 5121: ('B', 1)}; NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}
def acc(i):
  a = g['accessors'][i]; bv = g['bufferViews'][a['bufferView']]; t, s = CT[a['componentType']]; n = NC[a['type']]
  off = bin0 + bv.get('byteOffset', 0) + a.get('byteOffset', 0)
  arr = np.frombuffer(b, dtype='<' + t, count=a['count'] * n, offset=off).reshape(a['count'], n) if bv.get('byteStride', s*n) == s*n else None
  return arr
tris = []; hl = []
for m in g['meshes']:
  for p in m['primitives']:
    if p.get('mode', 4) != 4 or 'indices' not in p: continue
    P = acc(p['attributes']['POSITION']).astype(np.float64); I = acc(p['indices']).reshape(-1, 3).astype(np.int64)
    name = m.get('name', '') + ' ' + (g['materials'][p['material']].get('name', '') if 'material' in p else '')
    tris.append(P[I])
    if 'JOINTS_0' in p['attributes'] and g.get('skins'):
      J = acc(p['attributes']['JOINTS_0'])[:, 0].astype(np.int64); jn = [g['nodes'][j].get('name', '') for j in g['skins'][0]['joints']]
      mark = np.array([bool(HL.search(x)) for x in jn]); hl.append(mark[J[I[:, 0]]])
    else: hl.append(np.full(len(I), bool(HL.search(name))))
T = np.concatenate(tris); H = np.concatenate(hl)
print('tris', len(T), 'highlight', int(H.sum()))
W = 900; views = {'side': (0, 0), 'top': (90, 0), 'persp': (25, 35)}
imgs = []
for vn, (el, az) in views.items():
  e, a = np.radians(el), np.radians(az)
  Ry = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]]); Rx = np.array([[1, 0, 0], [0, np.cos(e), -np.sin(e)], [0, np.sin(e), np.cos(e)]])
  # glTF Y-up; weapon long axis ~ Z: look along +X
  base = np.array([[0, 0, 1], [0, 1, 0], [-1, 0, 0]])
  R = Rx @ Ry @ base
  V = T @ R.T; mn = V.reshape(-1, 3).min(0); mx = V.reshape(-1, 3).max(0); sc = (W - 40) / max(mx[0]-mn[0], mx[1]-mn[1])
  X = (V[..., 0] - mn[0]) * sc + 20; Y = (mx[1] - V[..., 1]) * sc + 20; Z = V[..., 2]
  n = np.cross(V[:, 1] - V[:, 0], V[:, 2] - V[:, 0]); n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-12
  shade = 0.25 + 0.75 * np.abs(n[:, 2])
  hgt = int((mx[1]-mn[1]) * sc + 40)
  img = np.zeros((hgt, W, 3)); zb = np.full((hgt, W), -1e9)
  for k in np.argsort(Z.mean(1)):
    x, y, z = X[k], Y[k], Z[k]
    x0, x1 = int(max(0, np.floor(x.min()))), int(min(W-1, np.ceil(x.max()))); y0, y1 = int(max(0, np.floor(y.min()))), int(min(hgt-1, np.ceil(y.max())))
    if x1 < x0 or y1 < y0: continue
    xs, ys = np.meshgrid(np.arange(x0, x1+1) + .5, np.arange(y0, y1+1) + .5)
    d = (y[1]-y[2])*(x[0]-x[2]) + (x[2]-x[1])*(y[0]-y[2])
    if abs(d) < 1e-9: continue
    l0 = ((y[1]-y[2])*(xs-x[2]) + (x[2]-x[1])*(ys-y[2])) / d; l1 = ((y[2]-y[0])*(xs-x[2]) + (x[0]-x[2])*(ys-y[2])) / d; l2 = 1-l0-l1
    m = (l0 >= 0) & (l1 >= 0) & (l2 >= 0)
    if not m.any(): continue
    zz = l0*z[0] + l1*z[1] + l2*z[2]; sub = zb[y0:y1+1, x0:x1+1]; w = m & (zz > sub)
    sub[w] = zz[w]; c = np.array([1, .2, .15]) if H[k] else np.array([.8, .8, .8]); img[y0:y1+1, x0:x1+1][w] = c * shade[k]
  imgs.append(Image.fromarray((img*255).astype(np.uint8)))
hh = sum(i.height for i in imgs); o = Image.new('RGB', (W, hh)); yy = 0
for i in imgs: o.paste(i, (0, yy)); yy += i.height
o.save(out)
