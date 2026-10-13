# cc-P81: the SFW set to install, by gump ID, from the Siege of Sosaria kit. Read only unless an out dir is given.
# Install: #01 Default Gumps; #02 Player Mobile Swaps\#01 Default Versions (bodies; identical to #01's, checked);
# #03 Spell Icon Upgrades; DLC top level minus 9001 (the author's login splash, 0x2329) and minus 62000-62026 (taken by
# Fiddle-Me-This's own gumps); DLC Paperdoll Templates\#01 Default Versions\Grayscale (hueable). Never a Topless or
# Nude folder.
import os, sys, hashlib, shutil
K=r"D:/ShatteredLegacy/design/paperdoll-sos/kit/UO Paperdoll Project (Siege of Sosaria) Builder's Kit/PNGs"
FMT_TAKEN=set(range(62000,62027))
EXCLUDE={9001}
def ids_in(folder, recurse=False):
    out={}
    for f in os.listdir(folder):
        p=os.path.join(folder,f)
        if os.path.isfile(p) and f.lower().endswith('.png') and os.path.splitext(f)[0].isdigit():
            out[int(os.path.splitext(f)[0])]=p
    return out
sel={}; src={}
for name,folder in [('default','#01 Default Gumps'),('bodies','#02 Player Mobile Swaps/#01 Default Versions'),('spells','#03 Spell Icon Upgrades'),('dlc','DLC'),('templates','DLC/Paperdoll Templates/#01 Default Versions/Grayscale')]:
    for i,p in ids_in(os.path.join(K,folder)).items():
        if name=='dlc' and (i in EXCLUDE or i in FMT_TAKEN): continue
        if i in sel:
            if open(sel[i],'rb').read()!=open(p,'rb').read(): print('DIFFERENT duplicate', i, src[i], name)
            continue
        sel[i]=p; src[i]=name
assert not any(('Topless' in p or 'Nude' in p) for p in sel.values())
nsfw=set()
for d in ['#02 Player Mobile Swaps/#02 Topless Versions','#02 Player Mobile Swaps/#03 Nude Versions']:
    for root,_,fs in os.walk(os.path.join(K,d)):
        for f in fs: nsfw.add(hashlib.sha256(open(os.path.join(root,f),'rb').read()).hexdigest())
for d in ['DLC/Paperdoll Templates/#02 Topless Versions','DLC/Paperdoll Templates/#03 Nude Versions']:
    for root,_,fs in os.walk(os.path.join(K,d)):
        for f in fs: nsfw.add(hashlib.sha256(open(os.path.join(root,f),'rb').read()).hexdigest())
# A hash that is also an SFW file is not NSFW: the male templates' "topless" are the default (a man in a loincloth).
sfw=set(hashlib.sha256(open(p,'rb').read()).hexdigest() for p in sel.values())
for root,_,fs in os.walk(os.path.join(K,'#02 Player Mobile Swaps/#01 Default Versions')):
    for f in fs: sfw.add(hashlib.sha256(open(os.path.join(root,f),'rb').read()).hexdigest())
nsfw-=sfw
hits=[i for i,p in sel.items() if hashlib.sha256(open(p,'rb').read()).hexdigest() in nsfw]
from collections import Counter
print('selected', len(sel), Counter(src.values()), 'bytes', sum(os.path.getsize(p) for p in sel.values()), 'largest', max(os.path.getsize(p) for p in sel.values()), 'nsfw hash hits', hits)
if len(sys.argv)>1 and sys.argv[1]=='nsfw-hashes':
    open(sys.argv[2],'w',newline='\n').write(''.join(h+'\n' for h in sorted(nsfw))); print('wrote', len(nsfw), 'hashes')
if len(sys.argv)>1 and sys.argv[1]=='copy':
    out=sys.argv[2]; os.makedirs(out, exist_ok=True)
    for i,p in sorted(sel.items()): shutil.copyfile(p, os.path.join(out, f'{i}.png'))
    print('copied', len(sel), 'to', out)
if len(sys.argv)>1 and sys.argv[1]=='bodies':
    out=sys.argv[2]; os.makedirs(out, exist_ok=True)
    for i in (12,13,14,15,665,666): shutil.copyfile(os.path.join(K,'#02 Player Mobile Swaps/#01 Default Versions',f'{i}.png'), os.path.join(out,f'{i}.png'))
    print('copied 6 SFW bodies to', out)
if len(sys.argv)>1 and sys.argv[1]=='addons':
    out=sys.argv[2]
    for mode,d in (('topless','#02 Topless Versions'),('nude','#03 Nude Versions')):
        os.makedirs(os.path.join(out,mode), exist_ok=True)
        for i in (12,13,14,15,665,666): shutil.copyfile(os.path.join(K,'#02 Player Mobile Swaps',d,f'{i}.png'), os.path.join(out,mode,f'{i}.png'))
    print('copied the local add-on bodies to', out)
