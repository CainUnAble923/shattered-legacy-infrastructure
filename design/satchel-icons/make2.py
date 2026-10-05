exec(open('make.py').read().split('# ORE:')[0])
def grayify(im, mask):
    a=np.array(im).astype(int); g=(a[...,0]*299+a[...,1]*587+a[...,2]*114)//1000
    for c in range(3): a[...,c]=np.where(mask,g,a[...,c])
    return Image.fromarray(a.astype(np.uint8))
def partial(im,h):
    if not h: return im.copy()
    cs=hue_colors(h); a=np.array(im).astype(int)
    m=(a[...,0]==a[...,1])&(a[...,1]==a[...,2])&(a[...,3]>0)
    g=a[...,0]*31//255; out=a.copy()
    for k in range(32):
        mm=m&(g==k); out[mm,0]=cs[k][0]; out[mm,1]=cs[k][1]; out[mm,2]=cs[k][2]
    return Image.fromarray(out.astype(np.uint8))
def nudge(im,mask):  # make sure non-gray content never equals r=g=b by accident
    a=np.array(im).astype(int); eq=(a[...,0]==a[...,1])&(a[...,1]==a[...,2])&mask
    a[eq,0]=np.minimum(a[eq,0]+3,255); return Image.fromarray(a.astype(np.uint8))
def alpha(im): return np.array(im)[...,3]>0
H,W=sat.size[1],sat.size[0]
def layer(part,x,y):
    L_=Image.new('RGBA',(W,H),(0,0,0,0)); L_.alpha_composite(part,(x,y)); return L_
# ORE: leather keeps its brown, ore chunks go pure gray and take the tier hue
a=np.array(sat).astype(int); r,g,b=a[...,0],a[...,1],a[...,2]
oremask=(abs(r-g)<30)&(abs(g-b)<30)&(r>55)&(a[...,3]>0)
chunk=layer(shrink(L('ore2'),0.55),19,0)
ore=sat.copy(); ore.alpha_composite(chunk)
om=oremask|alpha(chunk)
ore=nudge(ore,~om); ore=grayify(ore,om)
# LUMBER: leather gray (hued by tier), logs keep wood color
base=cover_ore(sat)
l1=layer(shrink(L('logs2'),0.8).rotate(0),16,0); l2=layer(shrink(L('logs2'),0.8),21,2)
lum=base.copy(); lum.alpha_composite(l1); lum.alpha_composite(l2)
cm=alpha(l1)|alpha(l2)
lum=grayify(lum,alpha(base)&~cm); lum=nudge(lum,cm)
# HUNTER
p=layer(shrink(L('hides'),0.38),15,1); f_=layer(shrink(L('feather'),0.9),25,0)
hun=base.copy(); hun.alpha_composite(p); hun.alpha_composite(f_)
cm2=alpha(p)|alpha(f_)
hun=grayify(hun,alpha(base)&~cm2); hun=nudge(hun,cm2)
rows=[('Ore satchel',ore,[('T1 Iron',0),('T2 Gold',0x8A5),('T3 Verite',0x89F),('T4 Valorite',0x8AB),('T5 (Platinum)',0x481)]),
      ('Lumber satchel',lum,[('T1 0x84C',0x84C),('T2 0x60',0x60),('T3 0x26C',0x26C),('alt: green 0x3F',0x3F),('alt: brown 0x5E9',0x5E9)]),
      ('Hunter satchel',hun,[('T1 0x5B5',0x5B5),('T2 0x5B2',0x5B2),('T3 0x4B5',0x4B5),('alt: tan 0x45E',0x45E)])]
S=4; cw=44*S+14; Wd=cw*6+170; Hd=len(rows)*(37*S+50)+60
sheet=Image.new('RGBA',(Wd,Hd),(48,44,40,255)); d=ImageDraw.Draw(sheet)
f=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',16); fs=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',13)
d.text((10,10),'Satchel icons with PartialHue: only the gray parts take the tier hue (4x).',fill=(235,225,200,255),font=f)
y=45
for name,im,tiers in rows:
    d.text((10,y+60),name,fill=(235,225,200,255),font=f); x=170
    for lab,src,h in [('stock 0xA272',sat,0)]+[(t,im,h) for t,h in tiers]:
        t=(partial(src,h) if src is not sat else src).resize((44*S,37*S),Image.NEAREST)
        sheet.alpha_composite(t,(x,y)); d.text((x+4,y+37*S+4),lab,fill=(200,190,170,255),font=fs); x+=cw
    y+=37*S+50
sheet.save(O+'satchel_mockups_v2.png')
# backpack scale, 1x on a backpack-ish background, tiers mixed
bp=Image.new('RGBA',(300,60),(92,62,38,255))
xs=0
for src,h in [(ore,0x8A5),(lum,0x60),(hun,0x5B5),(sat,0x8A5),(ore,0x8AB),(lum,0x26C)]:
    t=partial(src,h) if src is not sat else apply_hue(sat,h)
    bp.alpha_composite(t,(xs,10)); xs+=48
bp=bp.resize((900,180),Image.NEAREST); bp.save(O+'satchel_backpack.png')
for k,v in {'ore':ore,'lumber':lum,'hunter':hun}.items(): v.save(O+f'satchel_{k}_partial.png')
