import sys,json; sys.path.insert(0,'.'); import uorender as u
L=json.load(open('/home/'+__import__('os').environ.get('USER','')+'/mnt/ShatteredLegacy/design/registrar-office/layout.json')) if False else json.load(open(__import__('os').path.expanduser('~/mnt/ShatteredLegacy/design/registrar-office/layout.json')))
cur=[(int(c['id'],16),c['x'],c['y'],c['z']) for c in L['components']]
roof=lambda t,sid,x,y,z: ('roof' in t.sname[sid]) or z>=35
R=(1,3450,2592,3466,2606)
# proposal
new=[]
for sid,x,y,z in cur:
    if sid==0x1E5F: continue                      # bulletin board: crowds the map's stretched hide
    if sid==0x0B26: x,y=3461,2602                 # candelabra: out of the walkway into SE corner
    if sid==0x1047: x,y=3456,2598                 # globe: to the quarters, was hidden behind bookcase
    new.append((sid,x,y,z))
mk=[('R',3459,2601,18,(0,255,255,255))]
print(u.render(*R,extra=cur,grid=0,skip=roof,marks=mk,out='now.png'))
print(u.render(*R,extra=new,grid=0,skip=roof,marks=mk,out='prop.png'))
