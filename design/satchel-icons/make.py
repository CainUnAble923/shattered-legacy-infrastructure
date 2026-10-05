from PIL import Image, ImageDraw, ImageFont
import struct, numpy as np
D='/mnt/user-data/uploads/satchel-icons/'
O='/tmp/claude-0/w/'
def L(n): return Image.open(D+f'src_{n}.png').convert('RGBA')
sat=L('satchel')
# hues
hm=open(D+'hues.mul','rb').read()
def hue_colors(h):
    i=h-1; blk=i//8; ent=i%8; off=blk*708+4+ent*88
    cs=struct.unpack_from('<32H',hm,off)
    return [(((c>>10)&31)*255//31,((c>>5)&31)*255//31,(c&31)*255//31) for c in cs]
def apply_hue(im,h):
    if not h: return im.copy()
    cs=hue_colors(h); a=np.array(im).astype(int)
    g=((a[...,0]*299+a[...,1]*587+a[...,2]*114)//1000)*31//255
    out=a.copy()
    for k in range(32):
        m=g==k; out[m,0]=cs[k][0]; out[m,1]=cs[k][1]; out[m,2]=cs[k][2]
    return Image.fromarray(out.astype(np.uint8),'RGBA')
def shrink(im,f):
    bb=im.getbbox(); im=im.crop(bb)
    w,h=max(1,round(im.width*f)),max(1,round(im.height*f))
    r=im.resize((w,h),Image.LANCZOS); a=np.array(r); a[...,3]=np.where(a[...,3]>110,255,0); return Image.fromarray(a)
def cover_ore(im):
    # repaint gray/white ore pixels in the opening with dark leather
    a=np.array(im).astype(int)
    r,g,b,al=a[...,0],a[...,1],a[...,2],a[...,3]
    gray=(abs(r-g)<28)&(abs(g-b)<28)&(r>60)&(al>0)
    ys,xs=np.mgrid[0:a.shape[0],0:a.shape[1]]
    zone=(ys<=21)&(xs>=16)
    m=gray&zone
    a[m,0]=60;a[m,1]=18;a[m,2]=14
    return Image.fromarray(a.astype(np.uint8))
def paste(base,part,x,y):
    c=base.copy(); c.alpha_composite(part,(x,y)); return c

# ORE: existing art plus a chunk spilling over the top and one at the flap
ore=sat.copy()
o1=shrink(L('ore2'),0.55); ore=paste(ore,o1,19,0)
# LUMBER: cover ore, two log ends poking out of the top, a board strapped
lum=cover_ore(sat)
lg=shrink(L('logs'),0.8); lg2=shrink(L('logs2'),0.8)
lum=paste(lum,lg2,17,0); lum=paste(lum,lg,23,1)
# HUNTER: cover ore, a folded pelt over the opening, a feather
hun=cover_ore(sat)
pe=shrink(L('hides'),0.38); hun=paste(hun,pe,15,1)
fe=shrink(L('feather'),0.9); hun=paste(hun,fe,25,0)
variants={'Ore':ore,'Lumber':lum,'Hunter':hun}
for k,v in variants.items(): v.save(O+f'satchel_{k.lower()}.png')
rows=[('Ore satchel',ore,[('stock',0),('T1 Iron',0),('T2 Gold',0x8A5),('T3 Verite',0x89F),('T4 Valorite',0x8AB)]),
      ('Lumber satchel',lum,[('stock',0),('T1',0x84C),('T2',0x60),('T3',0x26C)]),
      ('Hunter satchel',hun,[('stock',0),('T1',0x5B5),('T2',0x5B2),('T3',0x4B5)])]
S=4; cw=44*S+20; W=cw*6+170; H=len(rows)*(37*S+50)+60
sheet=Image.new('RGBA',(W,H),(48,44,40,255)); d=ImageDraw.Draw(sheet)
try: f=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',16); fs=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',13)
except: f=fs=ImageFont.load_default()
d.text((10,10),'Satchel icon mockups (4x, nearest). Original 0xA272 at left, then each variant hued per tier.',fill=(235,225,200,255),font=f)
y=45
for name,im,tiers in rows:
    d.text((10,y+60),name,fill=(235,225,200,255),font=f)
    x=170
    cells=[('original',sat,0)]+[(t,im,h) for t,h in tiers] if name=='Ore satchel' else [(t,im,h) for t,h in tiers]
    if name!='Ore satchel': cells=[('original',sat,0)]+cells
    for lab,src,h in cells[:6]:
        t=apply_hue(src,h).resize((44*S,37*S),Image.NEAREST)
        sheet.alpha_composite(t,(x,y)); d.text((x+4,y+37*S+4),lab,fill=(200,190,170,255),font=fs); x+=cw
    y+=37*S+50
sheet.save(O+'satchel_mockups.png'); print(sheet.size)
# also 1x backpack-scale strip
strip=Image.new('RGBA',(44*4*2,37*2),(30,24,18,255))
for i,(k,v) in enumerate([('o',sat),('a',ore),('b',lum),('c',hun)]):
    strip.alpha_composite(v.resize((88,74),Image.NEAREST),(i*88,0))
strip.save(O+'satchel_2x.png')
