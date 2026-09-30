import sys, json; sys.path.insert(0,'.'); import uorender as u
m=u.Map(1); t=u.Tiles()
def avgz(x,y):
    a=m.land(x,y)[1]; l=m.land(x,y+1)[1]; r=m.land(x+1,y)[1]; b=m.land(x+1,y+1)[1]
    return (l+r)//2 if abs(a-b)>abs(l-r) else (a+b)//2
F=18  # floor z
TBL=F+6
items=[
 # --- outside, at the north door (3459,2596) ---
 ('outside',0x11c9,3458,2595,None),          # potted tree left of the door
 ('outside',0xb97,3463,2597,None),           # wooden signpost off the east wall, seen from the road and the square
 ('outside',0xbcf,3463,2597,None),           # wooden sign on it (named "League of Extraordinary Citizens" server-side)
 ('outside',0xb98,3460,2595,None),           # second signpost at the door
 ('outside',0xbd0,3460,2595,None),           # and its sign
 # --- office (east room) ---
 ('office',0x1e25,3458,2600,TBL),            # stack of books on the desk
 ('office',0x2d61,3458,2601,TBL),            # inkwell
 ('office',0x2270,3458,2601,TBL),            # scroll
 ('office',0x1810,3458,2600,TBL+3),          # hourglass on the books
 ('office',0xa99,3461,2597,F),               # bookcase against the north wall
 ('office',0xa9a,3458,2602,F),               # bookcase against the partition
 ('office',0x1e5f,3458,2598,F),              # bulletin board on the partition wall
 ('office',0xb26,3461,2600,F),               # standing candelabra
 ('office',0x11ca,3460,2597,F),              # flowerpot under the north window
 ('office',0x1047,3461,2598,F),              # globe beside the bookcase
 # --- back room (west room, the registrar's quarters) ---
 ('quarters',0xe42,3456,2597,F),             # chest at the foot of the bed
 ('quarters',0xa9a,3454,2602,F),             # bookcase
 ('quarters',0x2272,3454,2598,TBL),          # scroll on the side table
]
extra=[(sid,x,y,(avgz(x,y) if z is None else z)) for g,sid,x,y,z in items]
if __name__=='__main__':
    D='mnt/ShatteredLegacy/design/registrar-office/'
    roof=lambda t,sid,x,y,z: ('roof' in t.sname[sid]) or z>=35
    mk=[('Registrar',3459,2601,F,(0,255,255,255))]
    R=(1,3448,2588,3470,2610)
    print(u.render(*R,extra=extra,grid=0,out=D+'after_exterior.png'))
    print(u.render(*R,extra=extra,grid=0,skip=roof,out=D+'after_interior.png'))
    print(u.render(*R,grid=0,skip=roof,out=D+'before_interior.png'))
    json.dump({"map":"Trammel","building":"League of Extraordinary Citizens Field Office (New Haven), walls x3453-3462 y2596-2603, floor z18",
      "source":"design/registrar-office/registrar.py; z values are client-side estimates, verify on the server",
      "components":[{"area":g,"id":"0x%04X"%sid,"name":t.sname[sid],"x":x,"y":y,"z":(avgz(x,y) if z is None else z)} for g,sid,x,y,z in items]},
      open(D+'layout.json','w'),indent=1)
