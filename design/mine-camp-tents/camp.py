import sys, json; sys.path.insert(0,'.'); import uorender as u
m=u.Map(1)
def avgz(x,y):
    t=m.land(x,y)[1]; l=m.land(x,y+1)[1]; r=m.land(x+1,y)[1]; b=m.land(x+1,y+1)[1]
    return (l+r)//2 if abs(t-b)>abs(l-r) else (t+b)//2
TENT=[(0x36f,0,1,0),(0x36e,1,0,0),(0x36e,2,0,0),(0x36d,3,0,0),(0x369,1,2,0),(0x369,2,2,0),(0x368,3,2,0),(0x603,1,1,14),(0x603,2,1,14),(0x633,3,1,11)]
def tent(x,y,z): return [(s,x+dx,y+dy,z+dz) for s,dx,dy,dz in TENT]
def place(lst, X, Y):
    out=[]
    for sid,dx,dy,dz in lst:
        x,y=X+dx,Y+dy; out.append((sid,x,y,avgz(x,y)+dz))
    return out
# Liaison tent (Miners' Compact): door/rep at (X+4,Y+1)
LX,LY,LZ=3494,2743,5
liaison=[
 (0xa58,1,-1,0),            # rolled bedroll along the north wall
 (0x1bf0,2,-1,0),(0x1bef,2,-1,1),  # stacked iron ingots
 (0xe77,3,-1,0),            # barrel
 (0xe31,5,-1,0),            # brazier by the front corner
 (0xe3f,4,0,0),(0xe86,4,0,3),   # crate with a pickaxe on it
 (0x19b8,5,0,0),(0x19ba,6,0,0), # ore piles
 (0xfb4,6,1,0),(0xf39,6,-1,0),  # sledge hammer, shovel
]
# Archivist tent (Survey): door/rep at (X+4,Y+1)
AX,AY,AZ=3492,2753,5
archivist=[
 (0xe31,4,3,0),              # brazier south-front corner
 (0xb4a,5,0,0),(0x14eb,5,0,5),(0xa0f,5,0,5),  # writing table, map, candle
 (0xb5f,4,0,0),              # bench at the table
 (0x1047,4,2,0),             # globe
 (0xe42,5,2,0),(0x2270,5,2,4),(0x2272,5,2,4),  # chest with scrolls on top
 (0x9aa,1,-1,0),(0x1057,1,-1,3), # wooden box with sextant
 (0xa59,2,3,0),              # rolled bedroll south side
]
tents=tent(LX,LY,LZ)+tent(AX,AY,AZ)
deco=place(liaison,LX,LY)+place(archivist,AX,AY)
t=u.Tiles()
def rows(lst,group): return [{"group":group,"id":"0x%04X"%sid,"name":t.sname[sid],"x":x,"y":y,"z":z} for sid,x,y,z in lst]
layout={"map":"Trammel","source":"design/mine-camp-tents/camp.py; z values are client-side estimates, verify on the server",
 "addons":[{"name":"MinersCompactTent","components":rows(tent(LX,LY,LZ),"tent")+rows(place(liaison,LX,LY),"deco")},
           {"name":"SurveyArchivistTent","components":rows(tent(AX,AY,AZ),"tent")+rows(place(archivist,AX,AY),"deco")}],
 "npcs":[{"type":"MinersCompactLiaison","from":[3510,2748,0],"to":[LX+4,LY+1,avgz(LX+4,LY+1)],"facing":"East"},
         {"type":"SurveyArchivist","from":[3516,2747,1],"to":[AX+4,AY+1,avgz(AX+4,AY+1)],"facing":"East"}]}
json.dump(layout,open('mnt/ShatteredLegacy/design/mine-camp-tents/layout.json','w'),indent=1)
if __name__=='__main__':
    D='mnt/ShatteredLegacy/design/mine-camp-tents/'
    marks=[('Miners\' Compact Liaison',LX+4,LY+1,avgz(LX+4,LY+1),(0,255,255,255)),('Survey Archivist',AX+4,AY+1,avgz(AX+4,AY+1),(0,255,0,255))]
    R=(1,3486,2736,3512,2762)
    print(u.render(*R,extra=tents+deco,grid=0,out=D+'v3_clean.png'))
    print(u.render(*R,extra=tents+deco,grid=0,marks=marks,out=D+'v3_labeled.png'))
    for d in deco: print(d)
