# cc-P81: gump IDs the Siege of Sosaria kit overrides, against Fiddle-Me-This's and ours. Read only.
import os, re, sys, json
K=r"D:/ShatteredLegacy/design/paperdoll-sos/kit/UO Paperdoll Project (Siege of Sosaria) Builder's Kit/PNGs"
FMT=r"D:/ShatteredLegacy/player-package/vendor/fiddle-me-this"
SL=r"D:/ShatteredLegacy/player-package/vendor/shattered-legacy-art/gumps"
def gid(name):
    b=os.path.splitext(name)[0]
    if b.lower().startswith('0x'):
        try: return int(b[2:],16)
        except: return None
    return int(b) if b.isdigit() else None
def scan(root, skip=()):
    ids={}; bad=[]
    for dp,dn,fn in os.walk(root):
        if any(s in dp for s in skip): continue
        for f in fn:
            if not f.lower().endswith(('.png','.bmp')): continue
            i=gid(f)
            if i is None: bad.append(os.path.join(dp,f)); continue
            ids.setdefault(i,[]).append(os.path.relpath(os.path.join(dp,f),root))
    return ids,bad
sets={}
for name,sub,skip in [('default','#01 Default Gumps',()),('bodies','#02 Player Mobile Swaps/#01 Default Versions',()),('spells','#03 Spell Icon Upgrades',()),('dlc','DLC',())]:
    sets[name]=scan(os.path.join(K,sub),skip)
fmt,_=scan(os.path.join(FMT,'[Original]')); fmtc,_=scan(os.path.join(FMT,'[Custom]'))
sl,_=scan(SL)
fmtall=set(fmt)|set(fmtc)
allkit={}
for n,(ids,bad) in sets.items():
    print(n, len(ids), 'ids', sum(len(v) for v in ids.values()), 'files', 'non-numeric', len(bad), bad[:5])
    dup=[i for i,v in ids.items() if len(v)>1]
    print('   duplicate ids inside', n, len(dup), dup[:10])
    for i in ids: allkit.setdefault(i,[]).append(n)
cross=[i for i,v in allkit.items() if len(v)>1]
print('kit ids total', len(allkit), 'in more than one kit folder', len(cross), sorted(cross)[:20])
print('fmt ids', len(fmtall), 'ours', sorted(sl))
col=sorted(set(allkit)&fmtall)
print('kit vs fiddle-me-this collisions', len(col))
print('  ', col[:200])
print('kit vs ours', sorted(set(allkit)&set(sl)))
json.dump({'kit':{str(k):v for k,v in allkit.items()},'fmt_collisions':col,'fmt_orig':sorted(fmt),'fmt_custom':sorted(fmtc)},open(sys.argv[1],'w'))
