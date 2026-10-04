# West site, v3 (Chase 2026-10-04): farmhouse WEST of the field, attached to the fence, door and porch facing
# east (toward town) and opening straight into the farm. Trees inside the field cleared by a map edit.
import json, uorender as u, farm, farmhouse, farmsite
t=farm.t; m=farm.m; z=farm.z
F=(3381,2603,3399,2619); CX,CY=3377,2611
ex,pen,field,sign=farm.farm(*F,'E')
# open the west fence where the porch meets it
ex=[e for e in ex if not (e[1]==F[0] and CY-2<=e[2]<=CY+2 and e[0] in (0x835,0x836,0x837))]
# crops off the porch/steps tiles
ex=[e for e in ex if not (e[1] in (F[0]+1,) and CY-2<=e[2]<=CY+2)]
core=[(x,y) for x in range(-4,6) for y in range(-4,5)]
bz=max(z(CX+x,CY+y) for x,y in core)
house=[]
for sid,x,y,zz in farmhouse.build(STEP=0x725):
    if x>=5 and y>=3: continue            # drop the herb garden: the field is right there
    if sid==0xa300: x,y=-5,4              # well to the west side
    house.append((sid,CX+x,CY+y,bz+zz))
ex+=house
# map edit: statics to delete (every static on the trunk tiles inside the fence; foliage shares the tile)
clear=set((x,y) for x in range(F[0]+1,F[2]) for y in range(F[1]+1,F[3]) if farmsite.blocked(x,y))
removed=[]
for (x,y) in clear:
    for (sid,sx,sy,sz,h) in m.statics(x>>3,y>>3):
        if (x>>3)*8+sx==x and (y>>3)*8+sy==y: removed.append({"id":"0x%04X"%sid,"name":t.sname[sid],"x":x,"y":y,"z":sz})
C=farm.C; G=farm.G
mk=[('Pen: goat, sheep, cow, chicken (Taming)',pen[0],pen[1],z(*pen),G),
    ('Field: rats, giant rats, a snake (Combat)',field[0],field[1],z(*field),G),
    ('Farm gate + sign (from town)',sign[0],sign[1],z(*sign),C),
    ('Farmhouse: porch opens into the field, faces town',CX+4,CY,bz+5,C),
    ('Bardic Guild',3414,2604,z(3414,2604),C),('Road into town',3428,2598,z(3428,2598),C)]
print(u.render(1,3362,2588,3432,2640,extra=ex,marks=mk,grid=0,hide=clear,out='west_v3_mockup.png',scale=0.6))
print(u.render(1,3366,2598,3402,2624,extra=ex,grid=0,hide=clear,out='west_v3_closeup.png'))
json.dump({"farmhouse_center":[CX,CY],"base_z":bz,"map_edit_remove_statics":removed,
  "items":[{"id":"0x%04X"%s,"name":t.sname[s],"x":x,"y":y,"z":zz} for s,x,y,zz in ex]},open('west_v3_layout.json','w'),indent=0)
print('cleared tiles',sorted(clear),'statics',len(removed))
