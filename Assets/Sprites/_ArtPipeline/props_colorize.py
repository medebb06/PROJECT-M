"""Demirci props (ocak/körük, örs, kalkan+kılıç, mızrak rafı) — gri render -> palet bake.
tent_colorize.py / grindstone_colorize.py mantığı. Maskeler 1920x1080 render koordinatlarında.
Kullanım: python props_colorize.py <name> <render.png> <out_dir>"""
import sys, os
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

name, src_path, out_dir = sys.argv[1:4]
os.makedirs(out_dir, exist_ok=True)
im = Image.open(src_path).convert("RGBA")
FW, FH = im.size
bb = im.getchannel("A").point(lambda x: 255 if x > 8 else 0).getbbox()


def poly(pts, blur=1.2):
    m = Image.new("L", (FW, FH), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return np.array(m.filter(ImageFilter.GaussianBlur(blur))).astype(np.float32) / 255.0


def rect(x0, y0, x1, y1, blur=1.2):
    return poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], blur)


def ell(cx, cy, rx, ry, blur=1.5):
    m = Image.new("L", (FW, FH), 0)
    ImageDraw.Draw(m).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    return np.array(m.filter(ImageFilter.GaussianBlur(blur))).astype(np.float32) / 255.0


def clip(*ms):
    return np.clip(sum(ms), 0, 1)


P = {
    "wood":    ((24, 14, 9), (100, 66, 38), (170, 120, 72)),
    "wood_d":  ((16, 10, 7), (70, 46, 28), (120, 84, 50)),
    "stone":   ((20, 24, 30), (88, 98, 104), (166, 176, 178)),
    "soot":    ((14, 13, 14), (58, 54, 54), (122, 112, 104)),       # isli ocak taşı
    "steel":   ((26, 32, 38), (86, 98, 108), (170, 182, 190)),
    "iron":    ((10, 10, 12), (46, 44, 44), (120, 104, 80)),
    "leather": ((22, 12, 8), (78, 48, 28), (130, 88, 52)),
    "rope":    ((50, 38, 22), (150, 122, 76), (214, 190, 132)),
    "red":     ((34, 10, 12), (128, 34, 30), (204, 92, 66)),        # çadırdaki soluk kırmızı bez
}

# (taban paleti, [(maske, palet), ...] sırayla üstüne)
if name == "Forge":
    base = "soot"
    layers = [
        ("leather", poly([(435, 740), (700, 760), (870, 830), (890, 860), (860, 885), (700, 915), (560, 1000), (470, 1005), (440, 880)])),
        ("wood", clip(rect(605, 455, 720, 1052), rect(500, 1010, 890, 1052),
                      poly([(560, 1015), (700, 880), (745, 905), (625, 1018)]),
                      poly([(400, 600), (405, 578), (1170, 428), (1182, 440), (1172, 456), (420, 616)]))),
        ("wood_d", rect(985, 705, 1325, 1052)),                      # söndürme fıçısı
        ("iron", clip(rect(1000, 700, 1325, 735), rect(1015, 735, 1110, 1000),
                      poly([(960, 650), (1200, 650), (1195, 710), (1120, 725), (1030, 725), (965, 710)]),
                      ell(850, 830, 45, 35), rect(398, 600, 470, 1012, 1.0) * 0.0)),
    ]
elif name == "Anvil":
    base = "steel"
    layers = [("stone", rect(320, 765, 1600, 960))]
elif name == "ShieldSword":
    base = "red"
    layers = [
        ("iron", ell(1000, 645, 62, 78)),
        ("steel", clip(poly([(1048, 325), (1108, 318), (1222, 815), (1228, 862), (1200, 948), (1182, 930), (1058, 385)]),
                       poly([(985, 300), (1000, 285), (1150, 244), (1160, 266), (1142, 304), (1040, 340), (990, 322)]))),
        ("leather", poly([(995, 62), (1050, 48), (1068, 255), (1042, 268), (1012, 105)])),
    ]
elif name == "SpearRack":
    base = "wood"
    layers = [
        ("steel", clip(poly([(698, 152), (722, 20), (750, 152)]), poly([(1170, 152), (1195, 22), (1222, 152)]),
                       poly([(1322, 152), (1346, 28), (1366, 34), (1378, 150)]))),
        ("rope", clip(rect(700, 172, 748, 262), rect(1175, 172, 1222, 262),
                      poly([(1300, 160), (1358, 160), (1346, 285), (1296, 285)]))),
    ]
else:
    raise SystemExit("bilinmeyen: " + name)

a = np.array(im).astype(np.float32)
alpha = a[..., 3] / 255.0
v = a[..., 0]


def ramp(pn, t):
    d, m, l = [np.array(c, np.float32) for c in P[pn]]
    t = t[..., None]
    lo = d + (m - d) * np.clip(t * 2, 0, 1)
    hi = m + (l - m) * np.clip(t * 2 - 1, 0, 1)
    return np.where(t < 0.5, lo, hi)


t = np.clip((v - 45) / (345 - 45), 0, 1) ** 1.1
tp = np.round(t * 5) / 5.0
t = t * 0.72 + tp * 0.28
out = ramp(base, t)
for pn, m in layers:
    m = m[..., None]
    out = out * (1 - m) + ramp(pn, t) * m

res = Image.fromarray(np.dstack([np.clip(out, 0, 255), alpha * 255]).astype(np.uint8), "RGBA").crop(bb)
res.save(os.path.join(out_dir, name + "_Sharp.png"))

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
soft.save(os.path.join(out_dir, name + "_Soft.png"))
bg = Image.new("RGBA", soft.size, (40, 70, 60, 255))
bg.alpha_composite(soft)
bg.convert("RGB").save(os.path.join(out_dir, name + "_preview.png"))
print(name, "ok", soft.size)
