from PIL import Image, ImageFilter
import numpy as np
c=Image.open('tree_crop.png').convert('RGBA'); W,H=c.size
a=np.array(c).astype(np.float32); alpha=a[...,3]/255; L=a[...,0]
def gb(x,s): return np.array(Image.fromarray(np.clip(x,0,255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s))).astype(np.float32)
yy,xx=np.mgrid[0:H,0:W]
trunk=((yy>=592)&(xx>=322)&(xx<=458)).astype(np.float32)
trunk=np.array(Image.fromarray((trunk*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32)/255
# canopy shading: local contrast + vertical light
loc=(L-gb(L*alpha,18)/np.maximum(gb(alpha*255,18)/255,0.05))*alpha
t=0.5+loc*0.085+(L-124)*0.012-0.03
t=t+ (1-yy/ H)*0.16 - 0.06      # lighter at top (sky light)
t=np.clip(gb(t*255,3.5)/255,0,1)
t=np.clip((t-0.5)*1.5+0.5,0,1)
tp=np.round(t*3.0)/3.0; t=gb(tp*255,1.1)/255
def ramp(P,t):
    d,m,l=[np.array(x,np.float32) for x in P]; t=t[...,None]
    lo=d+(m-d)*np.clip(t*2,0,1); hi=m+(l-m)*np.clip(t*2-1,0,1)
    return np.where(t<0.5,lo,hi)
leaf=ramp(((14,44,30),(30,88,46),(78,134,62)),t)
# subtle warm/cool variation
n=gb(np.random.RandomState(3).rand(H,W)*255,40)/255-0.5
gx=(xx/ W)*0.45+(1-yy/ H)*0.55
ls=gb(np.clip(loc*0.07*255+128,0,255),22)/255-0.5
f=np.clip(0.45+(gx-0.45)*1.0+ls*1.6,0,1)
f=gb(f*255,16)/255
k=np.clip(np.floor(f*3.4),0,2).astype(np.float32)        # 3 big overlapping layers, hard edges
# smooth gradient inside each layer (lighter toward top-right)
g=np.clip(0.15+0.9*gx,0,1)
g=gb(g*255,10)/255
tl=np.clip(0.10+k*0.20+g*0.46,0,1)
tl=gb(tl*255,6.5)/255
# thin lighter bevel along layer boundaries (upper edge)
kk=gb(k*255,1.0)/255
dy=np.roll(kk,2,axis=0)-kk
dx=np.roll(kk,2,axis=1)-kk
bev=np.clip(-(dy*1.0+dx*0.6)*14,0,1)
bev=gb(bev*255,1.0)/255*(np.abs(np.roll(k,2,0)-k)+np.abs(np.roll(k,2,1)-k)>0)
inner=gb(np.array(Image.fromarray((alpha*255).astype(np.uint8)).filter(ImageFilter.MinFilter(7))).astype(np.float32),2.5)/255
pool=np.clip(1-inner,0,1)*0.0
leaf=ramp(((6,34,18),(16,76,26),(44,128,44)),np.clip(tl-pool+bev*0.08,0,1))
# trunk: brown, shade by x (light from right) + luminance
tx=np.clip(0.35+(L-118)*0.02+((xx-322)/136.0)*0.3,0,1)
wood=ramp(((24,18,13),(86,64,42),(142,110,74)),tx)
gr=np.random.RandomState(9).randn(H,W)*3.2
gr2=(gb(np.random.RandomState(4).rand(H,W)*255,2.2)/255-0.5)*14
leaf=leaf+(gr+gr2)[...,None]*np.array([0.7,1.0,0.6])
out=leaf*(1-trunk[...,None])+wood*trunk[...,None]
# edge darkening
al=Image.fromarray((alpha*255).astype(np.uint8))
er=np.array(al.filter(ImageFilter.MinFilter(5))).astype(np.float32)/255
edge=np.clip(alpha-er,0,1)[...,None]
pass
res=Image.fromarray(np.dstack([np.clip(out,0,255),alpha*255]).astype(np.uint8),"RGBA")
res.save('/mnt/user-data/outputs/tree/Tree_Sharp.png')
pr=np.array(res).astype(np.float32); aa=pr[...,3:4]/255; prem=pr[...,:3]*aa
pb=np.stack([gb(prem[...,i],2.0) for i in range(3)],-1); ab=gb(aa[...,0]*255,1.4)/255; abc=gb(aa[...,0]*255,2.0)/255
rgb=pb/np.maximum(abc[...,None],0.02)
Image.fromarray(np.dstack([np.clip(rgb,0,255),ab*255]).astype(np.uint8),"RGBA").save('/mnt/user-data/outputs/tree/Tree_Soft.png')
def shift(x,dx,dy):
    h,w=x.shape; o=np.zeros_like(x)
    ys=slice(max(0,-dy),min(h,h-dy)); xs=slice(max(0,-dx),min(w,w-dx)); yd=slice(max(0,dy),min(h,h+dy)); xd=slice(max(0,dx),min(w,w+dx))
    o[ys,xs]=x[yd,xd]; return o
a1=alpha; d=5
r=np.maximum(a1*(1-shift(a1,1,-d)),0.55*a1*(1-shift(a1,d,-1)))
r=gb(np.clip(r,0,1)*255,0.9)/255*1.5; r=np.clip(r,0,1)*a1*(1-trunk)
o=np.zeros((H,W,4),np.uint8); o[...,0]=190;o[...,1]=225;o[...,2]=255;o[...,3]=(r*255).astype(np.uint8)
Image.fromarray(o,"RGBA").save('/mnt/user-data/outputs/tree/Tree_Rim.png')
bg=Image.new("RGBA",(W,H),(30,60,90,255)); bg.alpha_composite(res); bg.convert("RGB").save('tree_preview.png')
