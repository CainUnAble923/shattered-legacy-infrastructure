import sys, json; sys.path.insert(0,'.'); import uorender as u, multi, farmhouse
m=u.Map(1); t=u.Tiles()
def z(x,y): return m.land(x,y)[1]
FN,FW,FC=0x836,0x837,0x835   # picket: run along x (north/south sides), run along y (west/east sides), NW corner
def rect(ex,x0,y0,x1,y1,gaps=()):
    for x in range(x0+1,x1+1):
        for yy in (y0,y1):
            if (x,yy) not in gaps: ex.append((FN,x,yy,z(x,yy)))
    for y in range(y0+1,y1+1):
        for xx in (x0,x1):
            if (xx,y) not in gaps: ex.append((FW,xx,y,z(xx,y)))
    ex.append((FC,x0,y0,z(x0,y0)))
def farm(x0,y0,x1,y1,gate):
    ex=[]; g=set()
    if gate=='W':
        gy=(y0+y1)//2; g={(x0,gy),(x0,gy+1)}; sign=(x0-2,gy-1)
    else:
        gy=(y0+y1)//2+3; g={(x1,gy),(x1,gy+1)}; sign=(x1+2,gy-1)
    rect(ex,x0,y0,x1,y1,g)
    # pen in the NE corner
    px0,py0,px1,py1=x1-7,y0,x1,y0+6
    rect(ex,px0,py0,px1,py1,{(px0,py0+3),(px0,py0+4)})
    ex+= [(0xb41,px0+2,py0+2,z(px0+2,py0+2)),(0xb42,px0+3,py0+2,z(px0+3,py0+2)),
          (0xf36,px1-2,py1-2,z(px1-2,py1-2)),(0xf36,px1-1,py1-2,z(px1-1,py1-2)),(0x100c,px1-2,py1-1,z(px1-2,py1-1))]
    # crop rows in the south-west part
    crops=[0xc7b,0xc55,0xc7d,0xc76,0xc6a]
    r=0
    for y in range(py1+2,y1-1,2):
        cid=crops[r%len(crops)]; r+=1
        for x in range(x0+2,x1-1):
            ex.append((cid,x,y,z(x,y)))
    for y in range(y0+2,py1,2):
        cid=crops[r%len(crops)]; r+=1
        for x in range(x0+2,px0-1):
            ex.append((cid,x,y,z(x,y)))
    ex.append((0x1e34,(x0+x1)//2-2,py1+3,z((x0+x1)//2-2,py1+3)))
    ex+= [(0xb97,sign[0],sign[1],z(*sign)),(0xbcf,sign[0],sign[1],z(*sign)),
          (0xe77,sign[0]+(1 if gate=='W' else -1),sign[1]+3,z(sign[0],sign[1]+3))]
    pen=((px0+px1)//2,(py0+py1)//2); field=((x0+x1)//2,(py1+y1)//2)
    return ex,pen,field,sign
def cottage(cx,cy):
    # thatched roof cottage, multi 0x6E (ThatchedRoofCottageDeed), footprint x -3..4, y -3..4 around its center
    zs=[z(x,y) for x in range(cx,cx+8) for y in range(cy,cy+8)]; bz=max(zs)
    ox,oy=cx+3,cy+3
    out=[]
    for sid,x,y,zz,fl in multi.load(0x6E):
        if fl==0 and t.sname[sid]!='wooden door': continue
        if t.sname[sid] in ('nodraw','wooden signpost','brass sign'): continue
        out.append((sid,ox+x,oy+y,bz+zz))
    return out,(ox,oy+4,bz)
def custom_house(cx,cy):
    core=[(x,y) for x in range(-4,6) for y in range(-4,5)]
    bz=max(z(cx+x,cy+y) for x,y in core)
    return [(sid,cx+x,cy+y,bz+zz) for sid,x,y,zz in farmhouse.build(STEP=0x725)],(cx+5,cy,bz)
def inside(x0,y0,x1,y1):
    return lambda t,sid,x,y,zz: x0<=x<=x1 and y0<=y<=y1
C=(0,255,255,255); R=(255,80,80,255); G=(120,255,120,255)
def go(name,F,view,gate,extra_marks,C0):
    ex,pen,field,sign=farm(*F,gate)
    cex,door=(custom_house(*C0[1:]) if C0[0]=='custom' else cottage(*C0)); ex+=cex
    mk=[('Pen: goat, sheep, cow, chicken (Taming)',pen[0],pen[1],z(*pen),G),
        ('Field: rats, giant rats, a snake (Combat)',field[0],field[1],z(*field),G),
        ('Farmhand (lesson NPC) + farm sign',sign[0],sign[1],z(*sign),C),('Farmhouse door and porch (faces town)' if C0[0]=='custom' else 'Farmhand cottage (door)',door[0],door[1],door[2],C)]+[(l,x,y,z(x,y),c) for l,x,y,c in extra_marks]
    print(name,u.render(1,*view,extra=ex,marks=mk,grid=0,out=name+'_mockup.png',scale=0.6))
    return [{"id":"0x%04X"%s,"name":t.sname[s],"x":x,"y":y,"z":zz} for s,x,y,zz in ex]
if __name__=='__main__':
    out={}
    out['north']=go('north',(3509,2452,3527,2468),(3478,2425,3540,2492),'W',
        [('Road to town (Magery school)',3497,2480,C),('Dojo',3500,2433,C),
         ('Stock wildlife spawner (bears, panthers) r25',3524,2459,R),('Stock wildlife spawner r25',3482,2459,R)],(3506,2440))
    out['west']=go('west',(3381,2603,3399,2619),(3368,2580,3434,2648),'E',
        [('Bardic Guild',3414,2604,C),('Road into town',3428,2598,C),
         ('Stock wildlife spawner r30',3403,2572,R)],('custom',3406,2631))
    json.dump(out,open('layouts.json','w'),indent=0)
