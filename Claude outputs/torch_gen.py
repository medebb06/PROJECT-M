"""Meşale: gri render -> palet bake (tent_colorize mantığı) + el boyası tarzı 8 karelik alev döngüsü.
Kullanım: python torch_gen.py <render.png> <out_dir>"""
import sys, os, math
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

src_path, out_dir = sys.argv[1:3]
os.makedirs(out_dir, exist_ok=True)
im = Image.open(src_path).convert("RGBA")
FW, FH = im.size
bb = im.getchannel("A").point(lambda x: 255 if x > 8 else 0).getbbox()
print("bbox", bb)


def poly(pts, blur=1.2):
    m = Image.new("L", (FW, FH), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return np.array(m.filter(ImageFilter.GaussianBlur(blur))).astype(np.float32) / 255.0


def rect(x0, y0, x1, y1, blur=1.2):
    return poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], blur)


def clip(*ms):
    return np.clip(sum(ms), 0, 1)


P = {
    "wood": ((24, 14, 9), (100, 66, 38), (170, 120, 72)),
    "iron": ((10, 10, 12), (46, 44, 44), (120, 104, 80)),
    "steel": ((26, 32, 38), (86, 98, 108), (170, 182, 190)),
    "rope": ((50, 38, 22), (150, 122, 76), (214, 190, 132)),
}
layers = [
    # kafes: dişler + iki bant + alt konik kısım (demir)
    ("iron", clip(poly([(870, 108), (1130, 108), (1122, 350), (1092, 420), (1052, 448), (958, 448), (928, 420), (896, 350)], 1.0))),
    # boyun halkası (çelik)
    ("steel", rect(962, 418, 1052, 468, 2)),
    # ip bağları: üst sarım + alt bağ
    ("rope", clip(poly([(953, 655), (1066, 648), (1072, 690), (958, 702)], 1.0), rect(985, 800, 1045, 832, 1.0))),
]

a = np.array(im).astype(np.float32)
alpha = a[..., 3] / 255.0
v = a[..., 0]


def ramp(pn, t):
    d, m, l = [np.array(c, np.float32) for c in P[pn]]
    t = t[..., None]
    lo = d + (m - d) * np.clip(t * 2, 0, 1)
    hi = m + (l - m) * np.clip(t * 2 - 1, 0, 1)
    return np.where(t < 0.5, lo, hi)


# bu render gri tonu geniş (gölgeli): 40..215 aralığı
t = np.clip((v - 35) / (330 - 35), 0, 1) ** 1.1
tp = np.round(t * 5) / 5.0
t = t * 0.72 + tp * 0.28
out = ramp("wood", t)
for pn, m in layers:
    m = m[..., None]
    out = out * (1 - m) + ramp(pn, t) * m
res = Image.fromarray(np.dstack([np.clip(out, 0, 255), alpha * 255]).astype(np.uint8), "RGBA").crop(bb)

pr = np.array(res).astype(np.float32)
aa = pr[..., 3:4] / 255
prem = pr[..., :3] * aa


def gb(ch, s):
    return np.array(Image.fromarray(np.clip(ch, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s))).astype(np.float32)


pb = np.stack([gb(prem[..., i], 1.4) for i in range(3)], -1)
ab = gb(aa[..., 0] * 255, 1.0) / 255
ab_c = gb(aa[..., 0] * 255, 1.4) / 255
rgb = pb / np.maximum(ab_c[..., None], 0.02)
soft = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), ab * 255]).astype(np.uint8), "RGBA")
soft.save(os.path.join(out_dir, "Torch_Soft.png"))
print("torch", soft.size)

# ---------------- alev kareleri ----------------
W, H, SS, N = 96, 160, 4, 8
cols = [((205, 52, 14), 1.00), ((255, 128, 24), 0.74), ((255, 196, 70), 0.50), ((255, 244, 180), 0.26)]
frames = []
for f in range(N):
    ph = 2 * math.pi * f / N
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    for ci, (col, wk) in enumerate(cols):
        layer = Image.new("L", (W * SS, H * SS), 0)
        d = ImageDraw.Draw(layer)

        def tongue(cx0, wmax, height, phase, amp, base_y=0.97):
            pts_l, pts_r = [], []
            steps = 40
            for i in range(steps + 1):
                t = i / steps
                y = (base_y - t * height) * H * SS
                sway = amp * math.sin(ph + phase + t * 3.2) * (t ** 1.3)
                wob = 1 + 0.10 * math.sin(ph * 2 + phase + t * 7)
                prof = (math.sin(math.pi * min(1, t * 1.0) ** 0.65) if t < 1 else 0) ** 0.85
                w = wmax * prof * wob
                x = (cx0 + sway) * W * SS
                pts_l.append((x - w * W * SS / 2, y))
                pts_r.append((x + w * W * SS / 2, y))
            d.polygon(pts_l + pts_r[::-1], fill=255)

        k = wk
        tongue(0.50, 0.78 * k, 0.80 * (0.92 + 0.08 * math.sin(ph)), 0.0, 0.05)
        tongue(0.33, 0.40 * k, 0.50 * (0.9 + 0.1 * math.sin(ph + 2)), 1.7, 0.06)
        tongue(0.68, 0.38 * k, 0.56 * (0.9 + 0.1 * math.sin(ph + 4)), 3.4, 0.06)
        layer = layer.filter(ImageFilter.GaussianBlur(SS * (1.2 + 0.5 * ci)))
        col_img = Image.new("RGBA", layer.size, col + (255,))
        col_img.putalpha(layer.point(lambda x: min(255, int(x * (1.0 if ci else 0.92)))))
        img.alpha_composite(col_img)
    frames.append(img.resize((W, H), Image.LANCZOS))
for i, fr in enumerate(frames):
    fr.save(os.path.join(out_dir, f"Flame_{i}.png"))
sheet = Image.new("RGB", (W * N, H), (28, 34, 46))
for i, fr in enumerate(frames):
    sheet.paste(fr, (i * W, 0), fr)
sheet = sheet.resize((sheet.width * 2 // 2, sheet.height))
tp_ = Image.new("RGB", (soft.width // 2 + W * N, max(soft.height // 2, H)), (28, 34, 46))
ts = soft.resize((soft.width // 2, soft.height // 2))
tp_.paste(ts, (0, 0), ts)
tp_.paste(sheet, (ts.width, 0))
tp_.save(os.path.join(out_dir, "torch_preview.png"))
