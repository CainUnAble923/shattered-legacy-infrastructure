# cc-P81: the kit's PaperDollGump.json against TazUO 3212623f's built-in PaperDollGump.Settings defaults. Read only.
import re, json, sys
src=open(r'D:/UO/cc-p40-work/tazuo-3212623f/src/ClassicUO.Client/Game/UI/Gumps/PaperdollGump.cs',encoding='utf-8').read()
body=src[src.index('public static class Settings'):]
defs={}
for m in re.finditer(r'public static \w+ (\w+) \{ get; set; \} = ([^;]+);', body):
    expr=m.group(2).strip()
    try: defs[m.group(1)]=eval(expr.replace('0x','0x'),{})
    except Exception: defs[m.group(1)]=expr
kit=json.load(open(sys.argv[1],encoding='utf-8-sig'))
same=[k for k in kit if k in defs and defs[k]==kit[k]]
diff=[(k,defs.get(k),kit[k]) for k in kit if k in defs and defs[k]!=kit[k]]
only_kit=[k for k in kit if k not in defs]
only_taz=[k for k in defs if k not in kit]
print(f'kit keys {len(kit)}, TazUO keys {len(defs)}, same {len(same)}, different {len(diff)}, kit only {len(only_kit)}, TazUO only {len(only_taz)}')
for k,a,b in diff: print(f'  DIFF {k}: TazUO {a} kit {b}')
for k in only_kit: print(f'  KIT ONLY {k} = {kit[k]}')
for k in only_taz: print(f'  TAZUO ONLY {k} = {defs[k]}')
