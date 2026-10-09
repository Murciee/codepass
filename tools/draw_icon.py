import struct
import zlib

def blend(dst, src):
    sr, sg, sb, sa = src
    dr, dg, db, da = dst
    a = sa / 255.0
    oa = a + da / 255.0 * (1.0 - a)
    if oa <= 0:
        return (0, 0, 0, 0)
    return (int((sr*a + dr*(da/255.0)*(1-a))/oa),
            int((sg*a + dg*(da/255.0)*(1-a))/oa),
            int((sb*a + db*(da/255.0)*(1-a))/oa), int(oa*255))

def put(px, w, x, y, c):
    if 0 <= x < w and 0 <= y < w:
        i = (y*w+x)*4
        px[i:i+4] = bytes(blend(tuple(px[i:i+4]), c))

def rounded(px, w, x0, y0, x1, y1, r, cfn):
    for y in range(max(0, y0), min(w, y1+1)):
        for x in range(max(0, x0), min(w, x1+1)):
            dx = max(x0+r-x, 0, x-(x1-r))
            dy = max(y0+r-y, 0, y-(y1-r))
            d = (dx*dx + dy*dy) ** 0.5
            if d <= r:
                a = min(255, max(0, int((r-d+1)*255)))
                put(px, w, x, y, cfn(x, y, a))

def polygon(px, w, pts, color):
    miny = max(0, min(y for x,y in pts)); maxy = min(w-1, max(y for x,y in pts))
    for y in range(miny, maxy+1):
        xs = []
        for (x1,y1),(x2,y2) in zip(pts, pts[1:]+pts[:1]):
            if (y1 <= y < y2) or (y2 <= y < y1):
                xs.append(int(x1 + (y-y1)*(x2-x1)/(y2-y1)))
        xs.sort()
        for i in range(0, len(xs)-1, 2):
            for x in range(max(0,xs[i]), min(w, xs[i+1]+1)):
                put(px,w,x,y,color)

def line(px,w,x0,y0,x1,y1,color,width):
    steps=max(abs(x1-x0),abs(y1-y0),1)
    for i in range(steps+1):
        x=int(x0+(x1-x0)*i/steps); y=int(y0+(y1-y0)*i/steps)
        rr=width//2
        for yy in range(y-rr,y+rr+1):
            for xx in range(x-rr,x+rr+1): put(px,w,xx,yy,color)

def png(px, w):
    raw=b''.join(b'\x00'+bytes(px[y*w*4:(y+1)*w*4]) for y in range(w))
    def chunk(t,d): return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,w,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b'')

def render(n):
    s=4; w=n*s; px=bytearray([0,0,0,0]*(w*w))
    def sc(v): return int(v*s)
    # soft shadow
    rounded(px,w,sc(29),sc(44),sc(227),sc(205),sc(37),lambda x,y,a:(0,20,45,int(a*.28)))
    polygon(px,w,[(sc(72),sc(175)),(sc(61),sc(224)),(sc(113),sc(185))],(2,42,82,255))
    rounded(px,w,sc(44),sc(55),sc(220),sc(190),sc(35),lambda x,y,a:(4,55,103,a))
    polygon(px,w,[(sc(73),sc(183)),(sc(61),sc(215)),(sc(111),sc(182))],(8,91,158,255))
    def blue(x,y,a):
        t=max(0,min(1,(y/s-22)/168)); return (int(61*(1-t)+0*t),int(183*(1-t)+99*t),int(249*(1-t)+183*t),a)
    rounded(px,w,sc(38),sc(38),sc(214),sc(174),sc(35),blue)
    line(px,w,sc(68),sc(57),sc(105),sc(45),(190,236,255,130),sc(7))
    rounded(px,w,sc(68),sc(68),sc(188),sc(144),sc(17),lambda x,y,a:(255,255,255,a))
    rounded(px,w,sc(73),sc(73),sc(183),sc(139),sc(13),lambda x,y,a:(231,246,255,a))
    for yy in range(sc(75),sc(137)):
        col=(255-int((yy/s-75)*.25),255-int((yy/s-75)*.12),255,a:=255)
        for xx in range(sc(75),sc(181)): put(px,w,xx,yy,col)
    # message glyph
    for cx,cy,r,col in [(88,88,7,(8,123,212,255))]:
        for yy in range(sc(cy-r),sc(cy+r)+1):
            for xx in range(sc(cx-r),sc(cx+r)+1):
                if (xx/s-cx)**2+(yy/s-cy)**2 <= r*r: put(px,w,xx,yy,col)
    line(px,w,sc(105),sc(88),sc(159),sc(88),(8,123,212,255),sc(7))
    line(px,w,sc(84),sc(112),sc(160),sc(112),(120,194,237,255),sc(6))
    line(px,w,sc(84),sc(128),sc(132),sc(128),(120,194,237,255),sc(6))
    for cx in (151,165,179):
        for yy in range(sc(160),sc(169)):
            for xx in range(sc(cx-5),sc(cx+6)):
                if (xx/s-cx)**2+(yy/s-164)**2 <= 25: put(px,w,xx,yy,(255,255,255,255))
    if s != 1:
        out=bytearray([0,0,0,0]*(n*n))
        for y in range(n):
            for x in range(n):
                vals=[]
                for yy in range(y*s,(y+1)*s):
                    for xx in range(x*s,(x+1)*s): vals.append(px[(yy*w+xx)*4:(yy*w+xx+1)*4])
                out[(y*n+x)*4:(y*n+x+1)*4]=bytes(sum(v[i] for v in vals)//len(vals) for i in range(4))
        return out
    return px

def make_ico(path):
    images=[]
    for n in (16,32,48,64,128,256): images.append((n,png(render(n),n)))
    head=struct.pack('<HHH',0,1,len(images)); directory=bytearray(); offset=6+16*len(images)
    for n,data in images:
        directory += struct.pack('<BBBBHHII',0 if n==256 else n,0 if n==256 else n,0,0,1,32,len(data),offset)
        offset += len(data)
    with open(path,'wb') as f: f.write(head+directory+b''.join(data for n,data in images))

if __name__ == '__main__':
    import os
    base=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    assets=os.path.join(base,'assets'); os.makedirs(assets,exist_ok=True)
    for n in (256,):
        with open(os.path.join(assets,'codepass-sms.png'),'wb') as f: f.write(png(render(n),n))
    make_ico(os.path.join(assets,'codepass-sms.ico'))
