import struct, os, sys, numpy as np
from PIL import Image, ImageDraw, ImageFont
CD = os.path.expanduser('~/mnt/ShatteredLegacy/client-data/classic-client')
M32 = 0xFFFFFFFF
def hashname(s):
    s = s.encode()
    L = len(s)
    eax=ecx=edx=0
    ebx=edi=esi=(L+0xDEADBEEF)&M32
    i=0
    while i+12 < L:
        edi=(struct.unpack_from('<I',s,i+4)[0]+edi)&M32
        esi=(struct.unpack_from('<I',s,i+8)[0]+esi)&M32
        edx=(struct.unpack_from('<I',s,i)[0]-esi)&M32
        edx=((edx+ebx)&M32) ^ (esi>>28) ^ ((esi<<4)&M32)
        esi=(esi+edi)&M32
        edi=((edi-edx)&M32) ^ (edx>>26) ^ ((edx<<6)&M32)
        edx=(edx+esi)&M32
        esi=((esi-edi)&M32) ^ (edi>>24) ^ ((edi<<8)&M32)
        edi=(edi+edx)&M32
        ebx=((edx-esi)&M32) ^ (esi>>16) ^ ((esi<<16)&M32)
        esi=(esi+edi)&M32
        edi=((edi-ebx)&M32) ^ (ebx>>13) ^ ((ebx<<19)&M32)
        ebx=(ebx+esi)&M32
        esi=((esi-edi)&M32) ^ (edi>>28) ^ ((edi<<4)&M32)
        edi=(edi+ebx)&M32
        i+=12
    r=L-i
    if r>0:
        b=s[i:]
        def g(k): return b[k] if k<len(b) else 0
        if r>=9: esi=(esi+(g(8)|g(9)<<8|g(10)<<16|g(11)<<24 if r>=12 else sum(g(k)<<(8*(k-8)) for k in range(8,r))))&M32
        if r>=5: edi=(edi+sum(g(k)<<(8*(k-4)) for k in range(4,min(r,8))))&M32
        ebx=(ebx+sum(g(k)<<(8*k) for k in range(0,min(r,4))))&M32
        esi=((esi^edi)-((edi>>18)^((edi<<14)&M32)))&M32
        ecx=((esi^ebx)-((esi>>21)^((esi<<11)&M32)))&M32
        edi=((edi^ecx)-((ecx>>7)^((ecx<<25)&M32)))&M32
        esi=((esi^edi)-((edi>>16)^((edi<<16)&M32)))&M32
        edx=((esi^ecx)-((esi>>28)^((esi<<4)&M32)))&M32
        edi=((edi^edx)-((edx>>18)^((edx<<14)&M32)))&M32
        eax=((esi^edi)-((edi>>8)^((edi<<24)&M32)))&M32
        return (edi<<32)|eax
    return (esi<<32)|eax

class UOP:
    def __init__(self, path):
        self.f=open(path,'rb'); f=self.f
        magic,ver,sig,nxt,cap,cnt=struct.unpack('<IIIqII',f.read(28))
        self.e={}
        while nxt:
            f.seek(nxt); n,nxt=struct.unpack('<Iq',f.read(12))
            for _ in range(n):
                off,hl,cl,dl,h,ad,fl=struct.unpack('<qIIIQIH',f.read(34))
                if off: self.e[h]=(off+hl,cl,dl,fl)
    def get(self,name):
        v=self.e.get(hashname(name))
        if not v: return None
        self.f.seek(v[0]); return self.f.read(v[1])

W,H=7168,4096
class Map:
    def __init__(s,idx):
        s.u=UOP(f'{CD}/map{idx}LegacyMUL.uop'); s.idx=idx; s.chunks={}
        s.si=open(f'{CD}/staidx{idx}.mul','rb'); s.st=open(f'{CD}/statics{idx}.mul','rb')
        s.name=f'build/map{idx}legacymul/%08d.dat'
    def block(s,bx,by):
        bi=bx*(H//8)+by; off=bi*196; ci=off//0xC4000
        if ci not in s.chunks: s.chunks[ci]=s.u.get(s.name%ci)
        d=s.chunks[ci]; o=off-ci*0xC4000+4
        return [struct.unpack_from('<Hb',d,o+k*3) for k in range(64)]
    def land(s,x,y):
        return s.block(x>>3,y>>3)[(y&7)*8+(x&7)]
    def statics(s,bx,by):
        bi=bx*(H//8)+by; s.si.seek(bi*12); lk,ln,_=struct.unpack('<iii',s.si.read(12))
        if lk<0 or ln<=0: return []
        s.st.seek(lk); d=s.st.read(ln)
        return [struct.unpack_from('<HBBbH',d,k*7) for k in range(ln//7)]

class Tiles:
    def __init__(s):
        d=open(f'{CD}/tiledata.mul','rb').read()
        s.lflags={}; o=0
        for g in range(512):
            o+=4
            for k in range(32):
                s.lflags[g*32+k]=struct.unpack_from('<Q',d,o)[0]; o+=30
        s.sflags=[];s.sheight=[];s.sname=[]
        while o+4<=len(d):
            o+=4
            for k in range(32):
                if o+41>len(d): break
                fl=struct.unpack_from('<Q',d,o)[0]; hgt=d[o+20]; nm=d[o+21:o+41].split(b'\0')[0].decode('latin1')
                s.sflags.append(fl); s.sheight.append(hgt); s.sname.append(nm); o+=41

class Art:
    def __init__(s):
        s.u=UOP(f'{CD}/artLegacyMUL.uop'); s.c={}
    def raw(s,i): return s.u.get('build/artlegacymul/%08d.tga'%i)
    @staticmethod
    def px(v):
        if v==0: return (0,0,0,0)
        r=(v>>10)&31;g=(v>>5)&31;b=v&31
        return (r*255//31,g*255//31,b*255//31,255)
    def land(s,i):
        k=('L',i)
        if k in s.c: return s.c[k]
        d=s.raw(i); im=None
        if d:
            a=np.zeros((44,44,4),np.uint8); vals=np.frombuffer(d[:2*1012],'<u2'); p=0
            for y in range(44):
                w=2+2*y if y<22 else 2+2*(43-y)
                x0=22-w//2
                for x in range(w):
                    a[y,x0+x]=s.px(int(vals[p])); p+=1
            im=Image.fromarray(a,'RGBA')
        s.c[k]=im; return im
    def static(s,i):
        k=('S',i)
        if k in s.c: return s.c[k]
        d=s.raw(0x4000+i); im=None
        if d and len(d)>8:
            w,h=struct.unpack_from('<HH',d,4)
            if 0<w<1024 and 0<h<1024:
                look=struct.unpack_from('<%dH'%h,d,8); base=8+h*2
                a=np.zeros((h,w,4),np.uint8)
                for y in range(h):
                    o=base+look[y]*2; x=0
                    while True:
                        xo,rl=struct.unpack_from('<HH',d,o); o+=4
                        if xo+rl==0: break
                        x+=xo
                        v=np.frombuffer(d,'<u2',rl,o); o+=rl*2
                        r=((v>>10)&31)*255//31; g=((v>>5)&31)*255//31; b=(v&31)*255//31
                        a[y,x:x+rl,0]=r;a[y,x:x+rl,1]=g;a[y,x:x+rl,2]=b;a[y,x:x+rl,3]=255
                        x+=rl
                im=Image.fromarray(a,'RGBA')
        s.c[k]=im; return im

def persp_coeffs(src,dst):
    # coefficients mapping dst(output) -> src(input) for PIL PERSPECTIVE
    A=[];B=[]
    for (x,y),(u,v) in zip(dst,src):
        A.append([x,y,1,0,0,0,-u*x,-u*y]);B.append(u)
        A.append([0,0,0,x,y,1,-v*x,-v*y]);B.append(v)
    return np.linalg.solve(np.array(A,float),np.array(B,float)).tolist()

def render(mapidx,x0,y0,x1,y1,extra=None,marks=None,grid=10,out='out.png',scale=1,hide=None):
    m=Map(mapidx); t=Tiles(); art=Art()
    extra=extra or []; hide=hide or set()
    items=[]; seq=0
    def lz(x,y): return m.land(x,y)[1]
    for x in range(x0,x1+1):
        for y in range(y0,y1+1):
            tid,z=m.land(x,y)
            zr=lz(x+1,y); zb=lz(x+1,y+1); zl=lz(x,y+1)
            avg=(z+zr+zb+zl)//4 if abs(z-zb)<=abs(zr-zl) else (zr+zl)//4*1+(z+zb)//4*0
            avg=(z+zr+zb+zl)//4
            items.append((x+y,avg-1,0,seq,('L',x,y,tid,z,zr,zb,zl))); seq+=1
    for bx in range(x0>>3,(x1>>3)+1):
        for by in range(y0>>3,(y1>>3)+1):
            for (sid,sx,sy,sz,hue) in m.statics(bx,by):
                x=bx*8+sx;y=by*8+sy
                if x0<=x<=x1 and y0<=y<=y1 and (x,y) not in hide:
                    items.append(sitem(t,sid,x,y,sz,seq)); seq+=1
    for (sid,x,y,z) in extra:
        items.append(sitem(t,sid,x,y,z,seq,True)); seq+=1
    items.sort(key=lambda a:(a[0],a[1],a[2],a[3]))
    # canvas
    def scr(x,y,z): return ((x-y)*22,(x+y)*22-z*4)
    xs=[scr(x,y,0)[0] for x,y in [(x0,y1),(x1,y0)]]
    ys=[scr(x0,y0,0)[1],scr(x1,y1,0)[1]]
    ox=-xs[0]+60; oy=-ys[0]+260
    Wc=xs[1]-xs[0]+120; Hc=ys[1]-ys[0]+360
    can=Image.new('RGBA',(Wc,Hc),(0,0,0,255))
    for it in items:
        k=it[4]
        if k[0]=='L':
            _,x,y,tid,z,zr,zb,zl=k
            im=art.land(tid)
            if im is None: continue
            sx,sy=scr(x,y,0); sx+=ox; sy+=oy
            pts=[(sx,sy-z*4),(sx+22,sy+22-zr*4),(sx,sy+44-zb*4),(sx-22,sy+22-zl*4)]
            if z==zr==zb==zl:
                can.alpha_composite(im,(sx-22,sy-z*4))
            else:
                minx=min(p[0] for p in pts);miny=min(p[1] for p in pts)
                maxx=max(p[0] for p in pts);maxy=max(p[1] for p in pts)
                w=maxx-minx+1;h=maxy-miny+1
                if w<=0 or h<=0 or h>600: continue
                dst=[(p[0]-minx,p[1]-miny) for p in pts]
                src=[(22,0),(44,22),(22,44),(0,22)]
                try:
                    c=persp_coeffs(src,dst)
                    tim=im.transform((w,h),Image.PERSPECTIVE,c,Image.BILINEAR)
                    mask=Image.new('L',(w,h),0); ImageDraw.Draw(mask).polygon(dst,fill=255)
                    a=np.array(tim); a[...,3]=np.minimum(a[...,3],np.array(mask)); 
                    # fill holes where src outside diamond
                    can.alpha_composite(Image.fromarray(a),(minx,miny))
                except Exception as e: pass
        else:
            _,sid,x,y,z,new=k
            im=art.static(sid)
            if im is None: continue
            sx,sy=scr(x,y,z); sx+=ox; sy+=oy
            if new:
                a=np.array(im).astype(np.int16); 
                im=Image.fromarray(a.astype(np.uint8))
            px,py=sx-im.width//2, sy+44-im.height
            if px<-im.width or py<-im.height or px>Wc or py>Hc: continue
            can.alpha_composite(im,(max(px,0),max(py,0)),(max(-px,0),max(-py,0)))
    d=ImageDraw.Draw(can)
    try: f=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',13)
    except: f=ImageFont.load_default()
    if grid:
        for x in range(x0 - x0%grid, x1+1, grid):
            for y in range(y0 - y0%grid, y1+1, grid):
                if x<x0 or y<y0: continue
                z=m.land(x,y)[1]; sx,sy=scr(x,y,z); sx+=ox; sy+=oy+22
                d.ellipse((sx-3,sy-3,sx+3,sy+3),fill=(255,255,0,255))
                d.text((sx+5,sy-7),f'{x},{y}',fill=(255,255,0,255),font=f,stroke_width=2,stroke_fill=(0,0,0,255))
    for (label,x,y,z,col) in (marks or []):
        sx,sy=scr(x,y,z); sx+=ox; sy+=oy+22
        d.ellipse((sx-6,sy-6,sx+6,sy+6),outline=col,width=3)
        d.text((sx+8,sy-20),label,fill=col,font=f,stroke_width=2,stroke_fill=(0,0,0,255))
    can=can.convert('RGB')
    if scale!=1: can=can.resize((int(can.width*scale),int(can.height*scale)),Image.LANCZOS)
    can.save(out); return can.size

def sitem(t,sid,x,y,z,seq,new=False):
    fl=t.sflags[sid] if sid<len(t.sflags) else 0; hg=t.sheight[sid] if sid<len(t.sheight) else 0
    pz=z
    if fl&1: pz-=1
    if hg>0: pz+=1
    return (x+y,pz,1,seq,('S',sid,x,y,z,new))
