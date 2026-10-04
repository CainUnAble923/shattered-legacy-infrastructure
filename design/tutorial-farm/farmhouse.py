# Custom farmhouse for the F-35 tutorial farm: the thatched cottage shell, door moved to the EAST wall,
# a covered porch under the east eave, steps, chimney, lean-to hay shed, well, coop, herb garden.
import uorender as u, multi
t=u.Tiles()
def build(DOOR_FRAME=(0xab,0xad), STEP=0x753):
    P=[]  # (id, dx, dy, dz) relative to the house center, dz relative to base z
    base=[c for c in multi.load(0x6E) if c[4]!=0 and t.sname[c[0]] not in ('nodraw','wooden signpost','brass sign','stone stairs')]
    for sid,x,y,z,f in base:
        if z==7 and (x,y) in ((-1,3),(0,3),(1,3)) and sid in (0xad,0xab): continue   # old south door frame
        if z==7 and x==3 and y in (-1,0,1) and sid in (0xa7,0xb9): continue           # east wall middle, rebuilt below
        if (x,y)==(-3,-3) and sid==0x66: sid=0x64
        P.append((sid,x,y,z))
    # south wall: close the old doorway, with a window in the middle
    P+= [(0xa8,-1,3,7),(0xba,0,3,7),(0xa8,1,3,7)]
    # east wall: door in the middle (SouthCW wooden door 0x6AD), frame pieces either side
    P+= [(DOOR_FRAME[0],3,-1,7),(0x6ad,3,0,7),(DOOR_FRAME[1],3,1,7)]
    # covered porch under the east eave: board deck, bench, water barrel, steps down
    for y in (-2,-1,0,1,2): P.append((0x4af if y else 0x4a9,4,y,5))
    P+= [(0xb2c,4,-2,7),(0x154d,4,2,7),(STEP,5,0,2),(STEP,5,-1,2),(STEP,5,1,2)]
    # stone chimney on the west wall
    P+= [(0x8d5,-4,0,0)]
    # open lean-to on the north side: hay and tools
    P+= [(0xf36,-2,-4,0),(0x100c,-1,-4,0),(0xf36,0,-4,0),(0xe87,1,-4,0),(0x1e34,2,-5,0)]
    # well, chicken coop
    P+= [(0xa300,6,-4,0),(0x4514,-2,-6,0)]
    # fenced herb garden south-east of the porch
    gx0,gy0,gx1,gy1=5,3,9,6
    for x in range(gx0+1,gx1+1):
        P+= [(0x836,x,gy0,0),(0x836,x,gy1,0)]
    for y in range(gy0+1,gy1+1):
        P+= [(0x837,gx0,y,0),(0x837,gx1,y,0)]
    P.append((0x835,gx0,gy0,0))
    for x in range(gx0+1,gx1):
        for y in range(gy0+1,gy1): P.append(((0xc7b,0xc76,0xc70)[(x+y)%3],x,y,0))
    return P
