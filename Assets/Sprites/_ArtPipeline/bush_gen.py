"""Çalı: gri render -> oyun yaprak paleti, 4 varyant. Kullanım: python bush_gen.py <render.png> <out_dir>
Boyama: parlaklık yumuşatılıp 5 kademeye posterize edilir (resim gibi yaprak öbekleri), alfa kenarı yumuşatılır."""
import sys, os
from PIL import Image, ImageFilter
import numpy as np
src, out = sys.argv[1:3]; os.makedirs(out, exist_ok=True)
im = Image.open(src).convert("RGBA")
bb = im.getchannel("A").point(lambda x: 255 if x > 6 else 0).getbbox(); print(bb)
im = im.crop(bb); W, H = im.size
a = np.array(im).astype(np.float32); alpha = a[..., 3] / 255; v = a[..., 0]
# CARTOON: ince yaprak/dal detayını sil, büyük yumuşak öbekler bırak
def blur(ch, s): return np.array(Image.fromarray(np.clip(ch, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s))).astype(np.float32)
den = np.maximum(blur(alpha * 255, 9) / 255, .02)
vp = blur(v * alpha, 9) / den                                   # büyük ölçekli ışık-gölge
vl = blur(v * alpha, 22) / np.maximum(blur(alpha * 255, 22) / 255, .02)
v4 = blur(v * alpha, 4.5) / np.maximum(blur(alpha * 255, 4.5) / 255, .02)   # orta ölçek: yaprak öbeği dokusu
t = np.clip((0.45 * v4 + 0.30 * vp + 0.25 * vl - 35) / (140 - 35), 0, 1)
yy = np.linspace(0, 1, H)[:, None]; xx = np.linspace(0, 1, W)[None, :]
t = t * .72 + (1.0 - yy) * .26 + (0.5 - np.abs(xx - .42)) * .12   # üst-sol aydınlık, alt koyu
t = np.clip(t, 0, 1)
steps = 5
tq = np.round(t * steps) / steps
t = t * .65 + tq * .35                                          # belirgin ama yumuşak kademeler
t = blur(t * 255, 6.0) / 255                                    # iç renkleri iyice dağıt/karıştır
t = 0.5 + (t - 0.5) * 0.8                                      # kontrastı düşür (daha az leke)                                    # kademe kenarlarını yumuşat
PAL = {
 "A_FarTeal":  ((10, 34, 38), (30, 96, 82), (88, 156, 128)),
 "B_Green":    ((8, 30, 18), (32, 106, 46), (112, 180, 72)),
 "C_Olive":    ((22, 28, 12), (86, 110, 34), (178, 174, 70)),
 "D_DeepGreen":((12, 34, 24), (30, 88, 52), (82, 144, 84)),
}
def ramp(p, t):
    d, m, l = [np.array(c, np.float32) for c in p]; t = t[..., None]
    return np.where(t < .5, d + (m - d) * np.clip(t * 2, 0, 1), m + (l - m) * np.clip(t * 2 - 1, 0, 1))
# alfa: kenar yumuşat + hafif şişir (ince yapraklar kaybolmasın)
sm = blur(alpha * 255, 7) / 255
al2 = np.clip((sm - 0.16) / 0.10, 0, 1)                         # yumuşak, yuvarlak dış hat (ince yaprak uçları yok)
al2 = blur(al2 * 255, 1.2) / 255
# tepe/yan çıkıntıları (tek tük dal-yaprak) temizle: opening (aşındır -> genişlet) + en büyük parça
m8 = Image.fromarray((al2 > .5).astype(np.uint8) * 255)
m8 = m8.filter(ImageFilter.MinFilter(25)).filter(ImageFilter.MaxFilter(25))
try:
    from scipy import ndimage
    lab, n = ndimage.label(np.array(m8) > 0)
    if n > 1:
        sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1)); m8 = Image.fromarray(((lab == 1 + int(np.argmax(sizes))) * 255).astype(np.uint8))
except Exception as e:
    print("scipy yok:", e)
keep = np.array(m8.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(2.5))).astype(np.float32) / 255
al2 = al2 * np.clip(keep * 1.6, 0, 1)
al2 = np.where(yy > 0.97, np.maximum(al2, np.clip(alpha * 3, 0, 1)), al2)
prev = Image.new("RGBA", (W * 2 + 30, H), (24, 70, 104, 255))
for i, (name, p) in enumerate(PAL.items()):
    rgb = ramp(p, t)
    rng = np.random.default_rng(7 + i)
    gn = blur(rng.normal(0, 1, (H, W)) * 40 + 128, 1.0).astype(np.float32); gn = (gn - 128) / 40
    rgb = rgb * (1 + gn[..., None] * 0.22)                      # ince tane/gürültü
    img = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), al2 * 255]).astype(np.uint8), "RGBA")
    img.save(os.path.join(out, f"Bush_{name}.png"))
    if i < 2: prev.alpha_composite(img, (i * (W + 30), 0))
prev.convert("RGB").resize((prev.width // 2, prev.height // 2)).save(os.path.join(out, "bush_preview.png"))
print(W, H)
