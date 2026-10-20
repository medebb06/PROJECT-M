"""Ev (stilt house): gri render -> palet bake. Kullanım: python house_gen.py <render.png> <out_dir>
Maske koordinatları alfa bbox'a kırpılmış görüntüye göre (bbox sol-üst = 218,59)."""
import sys, os
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
src, out = sys.argv[1:3]; os.makedirs(out, exist_ok=True)
im = Image.open(src).convert("RGBA")
bb = im.getchannel("A").point(lambda x: 255 if x > 8 else 0).getbbox()
im = im.crop(bb); W, H = im.size; print(W, H)
def poly(p, b=1.2):
    m = Image.new("L", (W, H), 0); ImageDraw.Draw(m).polygon(p, fill=255)
    return np.array(m.filter(ImageFilter.GaussianBlur(b))).astype(np.float32) / 255
def rect(a, b, c, d, bl=1.2): return poly([(a, b), (c, b), (c, d), (a, d)], bl)
def clip(*m): return np.clip(sum(m), 0, 1)
P = {"wood": ((22, 13, 9), (96, 64, 38), (166, 118, 72)),
     "wood_l": ((26, 16, 10), (112, 78, 46), (184, 136, 84)),
     "roof": ((34, 20, 22), (112, 58, 42), (190, 112, 76)),
     "stone": ((20, 24, 30), (88, 98, 104), (166, 176, 178)),
     "cloth": ((50, 38, 22), (150, 122, 76), (214, 190, 132))}
roof = clip(poly([(325, 280), (520, 150), (600, 22), (1150, 38), (1130, 90), (990, 160), (800, 302), (335, 302)]),
            poly([(1090, 85), (1135, 85), (1445, 292), (1400, 312)]),
            poly([(0, 455), (180, 390), (330, 345), (640, 308), (652, 330), (480, 420), (300, 490), (20, 492)]))
posts = clip(rect(50, 700, 76, 905), rect(182, 700, 200, 905), rect(312, 700, 330, 905), rect(458, 700, 478, 905), rect(782, 700, 806, 905),
             poly([(520, 915), (650, 730), (770, 720), (745, 785), (625, 915)]))
stone = np.clip(clip(poly([(50, 706), (1390, 706), (1388, 890), (1000, 906), (500, 922), (50, 902)])) - posts, 0, 1)
cloth = clip(poly([(655, 420), (690, 420), (690, 560), (670, 640), (655, 600)]), poly([(745, 420), (792, 420), (802, 690), (745, 690)]))
louver = rect(48, 500, 318, 682)
a = np.array(im).astype(np.float32); al = a[..., 3] / 255; v = a[..., 0]
def ramp(n, t):
    d, m, l = [np.array(c, np.float32) for c in P[n]]; t = t[..., None]
    return np.where(t < .5, d + (m - d) * np.clip(t * 2, 0, 1), m + (l - m) * np.clip(t * 2 - 1, 0, 1))
t = np.clip((v - 20) / (255 - 20), 0, 1) ** 1.05
t = t * .72 + np.round(t * 5) / 5 * .28
o = ramp("wood", t)
for n, m in [("wood_l", louver), ("stone", stone), ("roof", roof), ("cloth", cloth)]:
    m = m[..., None]; o = o * (1 - m) + ramp(n, t) * m
res = Image.fromarray(np.dstack([np.clip(o, 0, 255), al * 255]).astype(np.uint8), "RGBA")
pr = np.array(res).astype(np.float32); aa = pr[..., 3:4] / 255; prem = pr[..., :3] * aa
gb = lambda c, s: np.array(Image.fromarray(np.clip(c, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s))).astype(np.float32)
pb = np.stack([gb(prem[..., i], 1.4) for i in range(3)], -1); ab = gb(aa[..., 0] * 255, 1.0) / 255; ac = gb(aa[..., 0] * 255, 1.4) / 255
soft = Image.fromarray(np.dstack([np.clip(pb / np.maximum(ac[..., None], .02), 0, 255), ab * 255]).astype(np.uint8), "RGBA")
soft.save(os.path.join(out, "House_Soft.png"))
bg = Image.new("RGBA", soft.size, (40, 70, 60, 255)); bg.alpha_composite(soft); bg.convert("RGB").save(os.path.join(out, "house_preview.png"))
