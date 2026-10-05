import sys, os
sys.path.insert(0, r'D:\ShatteredLegacy\design\tutorial-farm')
import uorender as u
u.CD = r'D:\ShatteredLegacy\client-data\classic-client'
from PIL import Image
a = u.Art()
out = os.path.dirname(os.path.abspath(__file__))
ids = {'satchel':0xA272,'ore_pile':0x19B9,'ore_small':0x19B7,'ore2':0x19B8,'ore3':0x19BA,'logs':0x1BDD,'logs2':0x1BE0,'boards':0x1BD7,'hides':0x1079,'hides2':0x1078,'pelt':0x11F4,'ingots':0x1BF2,'pouch':0xE79,'bag':0xE76,'pickaxe':0xE86,'hatchet':0xF43,'feather':0x1BD1,'gem':0xF26}
sheet = Image.new('RGBA',(18*70,120),(40,40,40,255))
for n,(k,i) in enumerate(ids.items()):
    im = a.static(i)
    if im is None: print(k, hex(i), 'missing'); continue
    print(k, hex(i), im.size)
    im.save(os.path.join(out, f'src_{k}.png'))
    big = im.resize((im.width*2, im.height*2), Image.NEAREST)
    sheet.alpha_composite(big.crop((0,0,min(big.width,68),min(big.height,118))), (n*70+1,1))
sheet.save(os.path.join(out,'src_sheet.png'))
