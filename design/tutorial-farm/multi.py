import struct, uorender as u
def load(mid):
    CD=u.CD
    idx=open(f'{CD}/multi.idx','rb').read(); mul=open(f'{CD}/multi.mul','rb')
    off,ln,ex=struct.unpack_from('<iii',idx,mid*12)
    mul.seek(off); d=mul.read(ln)
    return [struct.unpack_from('<Hhhhi',d,i) for i in range(0,ln,16)]
