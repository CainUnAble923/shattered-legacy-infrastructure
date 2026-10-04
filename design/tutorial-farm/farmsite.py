import uorender as u
m=u.Map(1); t=u.Tiles()
cache={}
def blocked(x,y):
    k=(x,y)
    if k in cache: return cache[k]
    b=False
    for (sid,sx,sy,sz,h) in m.statics(x>>3,y>>3):
        if (x>>3)*8+sx==x and (y>>3)*8+sy==y and (t.sflags[sid]&0x40): b=True
    cache[k]=b; return b
def perim_ok(x0,y0,x1,y1):
    pts=[(x,y0) for x in range(x0,x1+1)]+[(x,y1) for x in range(x0,x1+1)]+[(x0,y) for y in range(y0,y1+1)]+[(x1,y) for y in range(y0,y1+1)]
    return not any(blocked(*p) for p in pts)
def area(x0,y0,w,h,zr):
    zs=[]
    for x in range(x0,x0+w):
        for y in range(y0,y0+h):
            if blocked(x,y): return None
            zs.append(m.land(x,y)[1])
    return None if max(zs)-min(zs)>zr else max(zs)-min(zs)
def interior_blocked(x0,y0,x1,y1):
    return sum(blocked(x,y) for x in range(x0+1,x1) for y in range(y0+1,y1))
def search(ax,ay,R,W=19,H=17):
    out=[]
    for dx in range(-R,R+1):
        for dy in range(-R,R+1):
            fx,fy=ax+dx,ay+dy; x1,y1=fx+W-1,fy+H-1
            if not perim_ok(fx,fy,x1,y1): continue
            ib=interior_blocked(fx,fy,x1,y1)
            for cx,cy,side in [(fx-9,fy+2,'W'),(fx+W+1,fy+2,'E'),(fx+2,fy-9,'N'),(fx+2,fy+H+1,'S'),(fx-9,fy+H-9,'W2'),(fx+W+1,fy+H-9,'E2'),(fx+W-9,fy-9,'N2'),(fx+W-9,fy+H+1,'S2')]:
                rc=area(cx,cy,8,8,2)
                if rc is not None: out.append((abs(dx)+abs(dy)+ib*2,ib,rc,fx,fy,cx,cy,side))
    out.sort(); return out
