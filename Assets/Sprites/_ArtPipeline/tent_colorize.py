from PIL import Image, ImageDraw, ImageFilter
import numpy as np
src=Image.open("tent_crop.png").convert("RGBA")
W,H=src.size
a=np.array(src).astype(np.float32)
alpha=a[...,3]/255.0
v=a[...,0]  # gray render
# --- masks ---
def poly(pts):
    m=Image.new("L",(W,H),0); ImageDraw.Draw(m).polygon(pts,fill=255)
    return np.array(m.filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32)/255.0
def rect(x0,y0,x1,y1): return poly([(x0,y0),(x1,y0),(x1,y1),(x0,y1)])
M={}
M["steel_bucket"]=rect(0,266,97,347)
M["wood_crateL"]=rect(106,252,262,352)
M["metal_small"]=np.clip(rect(110,230,137,254)+rect(230,233,252,254)+rect(700,283,724,306),0,1)
M["wood_axe"]=rect(185,196,213,347)
M["steel_axe"]=poly([(145,225),(150,203),(166,190),(206,189),(208,240),(172,238)])
M["iron_lantern"]=np.clip(rect(374,4,433,42)+rect(377,118,432,150),0,1)
M["glass_lantern"]=rect(391,48,417,117)
M["leather"]=rect(431,38,470,166)
M["rope"]=rect(432,2,482,60)
M["wood_pole"]=rect(447,58,471,357)
M["glass_bottle"]=poly([(746,222),(769,222),(772,240),(779,256),(779,284),(734,284),(734,256),(743,240)])
M["cork"]=rect(746,208,770,224)
M["wood_chest"]=rect(654,281,799,355)
M["wood_pegs"]=np.clip(rect(84,349,102,379)+rect(378,349,396,379)+rect(852,330,872,379)+rect(1148,355,1163,379),0,1)
M["wood_base"]=rect(100,338,1078,352)
order=["wood_base","steel_bucket","wood_crateL","metal_small","wood_axe","steel_axe","iron_lantern","glass_lantern","leather","rope","wood_pole","glass_bottle","cork","wood_chest","wood_pegs"]
# --- palettes: (dark, mid, light) as RGB 0-255 ---
P={
 "tarp":((34,10,12),(128,34,30),(204,92,66)),          # muted red canvas
 "steel_bucket":((26,32,38),(86,98,108),(170,182,190)),
 "wood_crateL":((26,16,10),(96,64,38),(168,118,72)),
 "metal_small":((20,22,24),(80,86,92),(170,176,182)),
 "wood_axe":((28,17,10),(108,72,42),(180,128,80)),
 "steel_axe":((22,28,34),(100,112,122),(200,212,220)),
 "iron_lantern":((10,10,12),(46,44,44),(120,104,80)),
 "glass_lantern":((70,36,8),(206,128,34),(255,214,120)),
 "leather":((22,12,8),(78,48,28),(130,88,52)),
 "rope":((50,38,22),(150,122,76),(214,190,132)),
 "wood_pole":((24,14,8),(92,60,34),(158,108,62)),
 "glass_bottle":((24,34,20),(98,120,52),(190,200,110)),
 "cork":((40,26,12),(140,98,52),(196,150,88)),
 "wood_chest":((24,14,9),(100,66,38),(170,120,72)),
 "wood_pegs":((20,12,8),(90,60,34),(150,104,60)),
 "wood_base":((16,10,7),(70,46,28),(120,84,50)),
}
def ramp(name,t):
    d,m,l=[np.array(c,np.float32) for c in P[name]]
    t=t[...,None]
    lo=d+(m-d)*np.clip(t*2,0,1)
    hi=m+(l-m)*np.clip(t*2-1,0,1)
    return np.where(t<0.5,lo,hi)
t=np.clip((v-22)/(150-22),0,1)
# soft posterize for painted feel
tp=np.round(t*5)/5.0
t=t*0.72+tp*0.28
out=ramp("tarp",t)
for n in order:
    m=M[n][...,None]
    out=out*(1-m)+ramp(n,t)*m
# tarp subtle fold tint: warmer on lit side
# outline from alpha: darken silhouette edge
al=Image.fromarray((alpha*255).astype(np.uint8))
er=np.array(al.filter(ImageFilter.MinFilter(5))).astype(np.float32)/255.0
edge=np.clip(alpha-er,0,1)[...,None]
out=out*(1-0.55*edge)
img=np.dstack([np.clip(out,0,255),alpha*255]).astype(np.uint8)
res=Image.fromarray(img,"RGBA")
res.save("/mnt/user-data/outputs/tent/Tent_Sharp.png")
# blurred (mild) variant, premultiplied
pr=np.array(res).astype(np.float32); aa=pr[...,3:4]/255
prem=pr[...,:3]*aa
def gb(ch,s): return np.array(Image.fromarray(np.clip(ch,0,255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s))).astype(np.float32)
pb=np.stack([gb(prem[...,i],2.2) for i in range(3)],-1)
ab=gb(aa[...,0]*255,1.6)/255
ab_c=gb(aa[...,0]*255,2.2)/255
rgb=pb/np.maximum(ab_c[...,None],0.02)
Image.fromarray(np.dstack([np.clip(rgb,0,255),ab*255]).astype(np.uint8),"RGBA").save("/mnt/user-data/outputs/tent/Tent_Soft.png")
# rim mask (moon from upper-right)
a1=alpha
def shift(x,dx,dy):
    h,w=x.shape; o=np.zeros_like(x)
    ys=slice(max(0,-dy),min(h,h-dy)); xs=slice(max(0,-dx),min(w,w-dx)); yd=slice(max(0,dy),min(h,h+dy)); xd=slice(max(0,dx),min(w,w+dx))
    o[ys,xs]=x[yd,xd]; return o
d=5
r=np.maximum(a1*(1-shift(a1,1,-d)),0.55*a1*(1-shift(a1,d,-1)))
r=np.array(Image.fromarray((np.clip(r,0,1)*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.9))).astype(np.float32)/255*1.5
r=np.clip(r,0,1)*a1
o=np.zeros((H,W,4),np.uint8); o[...,0]=190;o[...,1]=225;o[...,2]=255;o[...,3]=(r*255).astype(np.uint8)
Image.fromarray(o,"RGBA").save("/mnt/user-data/outputs/tent/Tent_Rim.png")
# preview
bg=Image.new("RGBA",(W,H),(40,70,60,255)); bg.alpha_composite(res); bg.convert("RGB").save("tent_preview.png")
