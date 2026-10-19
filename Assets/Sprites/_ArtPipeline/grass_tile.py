from PIL import Image, ImageFilter
import numpy as np
im=Image.open('grass_src.png').convert('RGBA')
a=np.array(im).astype(np.float32)
x0,x1,y0,y1=400,1620,136,830
c=a[y0:y1,x0:x1]
H,W=c.shape[:2]
al=c[...,3]/255.0
lum=c[...,0]/255.0
# premultiplied lum
P=np.dstack([lum*al,al])
B=np.roll(P,W//2,axis=1)
x=np.arange(W)
d=np.abs(x-W/2)/(0.05*W)
M=np.clip(1-d,0,1); M=M*M*(3-2*M)
M=M[None,:,None]
out=P*M+B*(1-P[...,1:2]*M)
alpha=out[...,1]
lumv=np.where(alpha>1e-3,out[...,0]/np.maximum(alpha,1e-3),0)
# normalise luminance of visible area
v=lumv[alpha>0.5]
lo,hi=np.percentile(v,[3,97])
n=np.clip((lumv-lo)/(hi-lo),0,1)
yy=np.linspace(0,1,H)[:,None]  # 0 top,1 bottom
# value: base darker, tips lighter, render shading on top
val=0.60+0.34*n
val=val*(0.82+0.18*(1-yy))  # tips (top) 1.0, base 0.82
# slight warm tip tint
r=val*(1.0+0.05*(1-yy)); g=val; b=val*(0.94+0.0*yy)
rgb=np.dstack([r,g,b]).clip(0,1)
# grain
rng=np.random.default_rng(3)
rgb=(rgb+rng.normal(0,0.012,rgb.shape)).clip(0,1)
res=np.dstack([rgb*255,alpha*255]).astype(np.uint8)
img=Image.fromarray(res,'RGBA')
tw=1024; th=int(round(H*tw/W))
# resize premultiplied
arr=np.array(img).astype(np.float32);pa=arr[...,3:4]/255
pm=np.dstack([arr[...,:3]*pa,arr[...,3:4]])
t=Image.fromarray(pm.astype(np.float32)[...,0],'F')
chs=[np.array(Image.fromarray(pm[...,i]).resize((tw,th),Image.LANCZOS)) for i in range(4)]
pm2=np.dstack(chs);pa2=np.clip(pm2[...,3:4]/255,0,1)
rgb2=np.where(pa2>1e-3,pm2[...,:3]/np.maximum(pa2,1e-3),0)
fin=np.dstack([rgb2,pm2[...,3:4]]).clip(0,255).astype(np.uint8)
Image.fromarray(fin,'RGBA').save('Grass_Tile.png')
print(W,H,tw,th)
# preview: tile 3x on tinted bg
tile=Image.fromarray(fin,'RGBA')
tint=np.array([0.22,0.37,0.26])
tt=np.array(tile).astype(np.float32)
col=np.dstack([tt[...,0]*tint[0],tt[...,1]*tint[1],tt[...,2]*tint[2],tt[...,3]]).astype(np.uint8)
ct=Image.fromarray(col,'RGBA')
sheet=Image.new('RGBA',(tw*3,th),(110,150,200,255))
for i in range(3): sheet.alpha_composite(ct,(i*tw,0))
sheet.convert('RGB').save('grass_prev.png')
