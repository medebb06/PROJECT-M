"""Bileği taşı (grindstone) — gri Blender render'ı -> palet bake. tent_colorize.py mantığı.
Kullanım: python grindstone_colorize.py <render.png> <out_dir>
Koordinatlar alfa bbox'a kırpılmış (1404x951) görüntüye göredir.
Kenar ışığı / outline BAKE EDİLMEZ (M_Prop_Coloured / SilhouetteRim verir)."""
import sys, os
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

src_path, out_dir = sys.argv[1], sys.argv[2]
os.makedirs(out_dir, exist_ok=True)
im = Image.open(src_path).convert("RGBA")
bb = im.getchannel("A").point(lambda x: 255 if x > 8 else 0).getbbox()
src = im.crop(bb)
W, H = src.size
a = np.array(src).astype(np.float32)
alpha = a[..., 3] / 255.0
v = a[..., 0]


def poly(pts, blur=1.2):
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return np.array(m.filter(ImageFilter.GaussianBlur(blur))).astype(np.float32) / 255.0


def rect(x0, y0, x1, y1):
    return poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])


def ellipse(cx, cy, rx, ry, blur=1.2):
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    return np.array(m.filter(ImageFilter.GaussianBlur(blur))).astype(np.float32) / 255.0


M = {}
beam = poly([(100, 303), (1016, 303), (1016, 420), (400, 420), (100, 403)], 1.0)
M["stone"] = np.clip(ellipse(608, 268, 270, 270) - beam, 0, 1)
M["iron"] = np.clip(
    ellipse(642, 284, 76, 42, 2)         # mil yatağı / kelepçe
    + ellipse(272, 772, 28, 20, 3)        # pedal kolu başı
    + ellipse(672, 322, 18, 14, 2),
    0, 1)
M["rope"] = poly([(657, 335), (680, 335), (703, 885), (689, 885)], 0.8)
M["treadle"] = poly([(262, 788), (310, 790), (935, 943), (935, 948), (700, 900), (300, 805)], 0.8)
order = ["stone", "treadle", "iron", "rope"]

P = {
    "wood":    ((24, 14, 9), (100, 66, 38), (170, 120, 72)),       # çadır sandık/direk ahşabı
    "stone":   ((20, 24, 30), (88, 98, 104), (166, 176, 178)),     # soğuk gri-mavi taş
    "iron":    ((10, 10, 12), (46, 44, 44), (120, 104, 80)),
    "rope":    ((50, 38, 22), (150, 122, 76), (214, 190, 132)),
    "treadle": ((16, 10, 7), (70, 46, 28), (120, 84, 50)),
}


def ramp(name, t):
    d, m, l = [np.array(c, np.float32) for c in P[name]]
    t = t[..., None]
    lo = d + (m - d) * np.clip(t * 2, 0, 1)
    hi = m + (l - m) * np.clip(t * 2 - 1, 0, 1)
    return np.where(t < 0.5, lo, hi)


# render çok parlak (medyan ~205): aydınlık -> açık, gölge -> koyu olacak şekilde normalize
t = np.clip((v - 45) / (345 - 45), 0, 1)
t = t**1.1                                   # düz yüzey (~205) orta tonda (~0.5)
tp = np.round(t * 5) / 5.0
t = t * 0.72 + tp * 0.28
out = ramp("wood", t)
for n in order:
    m = M[n][..., None]
    out = out * (1 - m) + ramp(n, t) * m

img = np.dstack([np.clip(out, 0, 255), alpha * 255]).astype(np.uint8)
res = Image.fromarray(img, "RGBA")
res.save(os.path.join(out_dir, "Grindstone_Sharp.png"))

# yumuşak (premultiplied blur) varyant — çadırdaki gibi
pr = np.array(res).astype(np.float32)
aa = pr[..., 3:4] / 255
prem = pr[..., :3] * aa


def gb(ch, s):
    return np.array(Image.fromarray(np.clip(ch, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s))).astype(np.float32)


pb = np.stack([gb(prem[..., i], 1.6) for i in range(3)], -1)
ab = gb(aa[..., 0] * 255, 1.2) / 255
ab_c = gb(aa[..., 0] * 255, 1.6) / 255
rgb = pb / np.maximum(ab_c[..., None], 0.02)
soft = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), ab * 255]).astype(np.uint8), "RGBA")
soft.save(os.path.join(out_dir, "Grindstone_Soft.png"))

bg = Image.new("RGBA", (W, H), (40, 70, 60, 255))
bg.alpha_composite(soft)
bg.convert("RGB").save(os.path.join(out_dir, "grindstone_preview.png"))
print("ok", W, H)
