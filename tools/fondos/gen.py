# McBar — Creado por Andrés Londero
# Genera los fondos "Ondas" de McBar (1.7.0), originales, 3840x2160. Uso: python3 gen.py  (o "gen.py 4" para vistas chicas).
# Los resultados se copian a src/McBar/Assets/Fondos con el nombre McBar-ondas-<momento>-4k.jpg.
# Fondos "Ondas doradas" para McBar — originales, generados por código.
# Cuatro momentos del día (mañana, tarde, atardecer, noche) en 3840x2160.
import numpy as np, sys
from PIL import Image, ImageFilter

W, H = 3840, 2160
def hexc(h): h=h.lstrip('#'); return np.array([int(h[i:i+2],16)/255 for i in (0,2,4)],dtype=np.float32)

def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)

def render(p, seed, out, scale=1):
    w,h=W//scale,H//scale
    rng=np.random.default_rng(seed)
    ys,xs=np.mgrid[0:h,0:w].astype(np.float32)
    X=xs/w; Y=ys/h
    # fondo: degradé diagonal con un brillo suave
    g=np.clip(0.65*Y+0.35*X,0,1)[...,None]
    img=hexc(p['bg0'])*(1-g)+hexc(p['bg1'])*g
    glow=np.exp(-(((X-p['glow'][0])/0.45)**2+((Y-p['glow'][1])/0.38)**2))[...,None]
    img=img+(hexc(p['glowc'])-img)*glow*p['glowk']
    th=np.deg2rad(p['tilt'])
    U=X*np.cos(th)+Y*np.sin(th)*(h/w)*1.0
    V=-X*np.sin(th)*(w/h)*0.18+Y  # coordenada "a lo ancho" de las cintas
    for i,rb in enumerate(p['ribbons']):
        base,amp1,f1,amp2,f2,thick,c0,c1,spec=rb
        ph1,ph2,ph3=rng.uniform(0,2*np.pi,3)
        c=base+amp1*np.sin(2*np.pi*f1*U+ph1)+amp2*np.sin(2*np.pi*f2*U+ph2)
        t=thick*(0.72+0.28*np.sin(2*np.pi*0.9*U+ph3))
        d=(V-c)/t
        # sombra suave debajo de la cinta
        below=np.clip(d-1,0,None)
        sh=np.exp(-below*1.6)*(d>1)*p['shadow']
        img=img*(1-sh[...,None]*0.55)
        inside=np.abs(d)<1
        # pliegue brillante: más claro al centro-arriba, más oscuro hacia el borde de abajo
        s=0.5+0.5*np.cos(np.pi*np.clip(d+0.25,-1,1)*0.95)
        col=hexc(c0)[None,None,:]*(1-s[...,None])+hexc(c1)[None,None,:]*s[...,None]
        hl=np.exp(-((d+0.62)/0.10)**2)*spec        # brillo fino arriba (canto de vidrio)
        rim=np.exp(-((d-0.93)/0.05)**2)*spec*0.35  # canto de abajo
        hlc=hexc(p.get('hlc','#FFFFFF'))
        col=col+(hlc-col)*np.clip(hl+rim,0,1)[...,None]
        edge=(1-np.abs(d))*t*h            # distancia al borde en píxeles
        a=np.clip(edge/1.6,0,1)*inside
        a=a*p['opacity']
        img=img*(1-a[...,None])+col*a[...,None]
    img=np.clip(img,0,1)
    # grano finísimo para que los degradés no hagan escalones
    img=img+rng.normal(0,0.6/255,img.shape).astype(np.float32)
    im=Image.fromarray((np.clip(img,0,1)*255+0.5).astype(np.uint8))
    im=im.filter(ImageFilter.GaussianBlur(0.6/scale))
    im.save(out,quality=95,subsampling=0,optimize=True)

P={
 'manana':dict(bg0='#FBF3E8',bg1='#EBCFA8',glow=(0.25,0.15),glowc='#FFFFFF',glowk=0.55,tilt=-14,shadow=0.18,opacity=0.97,
   ribbons=[(0.30,0.09,0.55,0.04,1.3,0.10,'#E4B77A','#FFF6E6',0.55),
            (0.52,0.10,0.45,0.05,1.1,0.13,'#D9A05A','#FFE9C4',0.6),
            (0.76,0.08,0.60,0.04,1.4,0.12,'#C98A45','#F8DDB0',0.5)]),
 'tarde':dict(bg0='#F2C987',bg1='#B87631',glow=(0.7,0.2),glowc='#FFF1CF',glowk=0.45,tilt=-12,shadow=0.25,opacity=0.97,
   ribbons=[(0.28,0.10,0.5,0.05,1.2,0.11,'#B8762C','#FFE1A3',0.6),
            (0.50,0.11,0.42,0.05,1.0,0.14,'#9C5A1E','#F7C66E',0.65),
            (0.74,0.09,0.58,0.04,1.3,0.13,'#7E4517','#E8A94F',0.55)]),
 'atardecer':dict(bg0='#E9855A',bg1='#3E1F2E',glow=(0.78,0.32),glowc='#FFC27A',glowk=0.55,tilt=-10,shadow=0.32,opacity=0.97,
   ribbons=[(0.30,0.10,0.5,0.05,1.25,0.11,'#A2432F','#FFB27A',0.6),
            (0.53,0.11,0.43,0.05,1.05,0.14,'#7A2E34','#F08A5D',0.6),
            (0.77,0.09,0.57,0.04,1.35,0.13,'#4C1F2E','#D9774E',0.5)]),
 'noche':dict(hlc='#E9C58C',bg0='#211826',bg1='#08070C',glow=(0.3,0.25),glowc='#3A2A2E',glowk=0.6,tilt=-13,shadow=0.4,opacity=0.98,
   ribbons=[(0.30,0.10,0.52,0.05,1.25,0.11,'#2A1A14','#8A6236',0.5),
            (0.52,0.11,0.44,0.05,1.05,0.14,'#1E120E','#A8783E',0.55),
            (0.76,0.09,0.58,0.04,1.35,0.13,'#150C0A','#7A552D',0.45)]),
}
if __name__=='__main__':
    scale=int(sys.argv[1]) if len(sys.argv)>1 else 1
    for i,(k,p) in enumerate(P.items()):
        render(p, 27, f'/home/claude/walls/{k}{"" if scale==1 else "_prev"}.jpg', scale)
        print('ok',k)
