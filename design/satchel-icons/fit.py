import numpy as np, struct
from PIL import Image, ImageDraw, ImageFont
D='/mnt/user-data/uploads/satchel-icons/'
hm=open(D+'hues.mul','rb').read()
def hue_colors(h):
    i=h-1; off=(i//8)*708+4+(i%8)*88
    return [(((c>>10)&31)*255//31,((c>>5)&31)*255//31,(c&31)*255//31) for c in struct.unpack_from('<32H',hm,off)]
def q15(a):
    a=a.copy(); a[...,:3]=(a[...,:3]>>3)*255//31; return a
def partial(im,h):
    if not h: return im
    cs=hue_colors(h); a=np.array(im).astype(int)
    m=(a[...,0]==a[...,1])&(a[...,1]==a[...,2])&(a[...,3]>0); g=a[...,0]*31//255
    for k in range(32):
        mm=m&(g==k); a[mm,0],a[mm,1],a[mm,2]=cs[k]
    return Image.fromarray(a.astype(np.uint8))
def fit(path,H):
    im=Image.open(path).convert('RGBA'); im=im.crop(im.getbbox())
    w=round(im.width*H/im.height)
    # premultiply to avoid dark fringes
    r=im.resize((w,H),Image.LANCZOS); a=np.array(r).astype(int)
    a[...,3]=np.where(a[...,3]>120,255,0)
    spread=a[...,:3].max(2)-a[...,:3].min(2)
    lum=(a[...,0]*299+a[...,1]*587+a[...,2]*114)//1000
    gray=(spread<=18)&(a[...,3]>0)&(lum>14)   # leather -> exact gray (tier hue)
    for c in range(3): a[...,c]=np.where(gray,lum,a[...,c])
    a=q15(a)
    # nudge non-gray accidental equals
    eq=(a[...,0]==a[...,1])&(a[...,1]==a[...,2])&~gray&(a[...,3]>0); a[eq,0]=np.minimum(a[eq,0]+8,255)
    # never pure black (UO transparent); lift to 1 step
    blk=(a[...,3]>0)&(a[...,:3].sum(2)==0); a[blk,:3]=8
    out=Image.fromarray(a.astype(np.uint8)); c=Image.new('RGBA',(max(44,w+4),H+4),(0,0,0,0)); c.alpha_composite(out,((c.width-w)//2,2)); return c, gray.sum(), (a[...,3]>0).sum()
