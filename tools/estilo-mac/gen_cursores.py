# McBar — Creado por Andrés Londero
# (1.8.0) Para regenerar: python3 gen_cursores.py ../../src/McBar/Assets/Cursores  (borrar después el _vista.png)
# Genera los cursores "estilo Mac" de McBar (1.8.0). Son dibujos PROPIOS hechos por código (no copian los de
# Apple): flecha negra con borde blanco, manito blanca con borde negro, cursor de texto, flechas para cambiar
# el tamaño, prohibido, cruz, ayuda y una rueda de colores girando (anillo, no el "molinete" de macOS).
# Cada .cur trae los tamaños 32, 48 y 64 (Windows elige según "Tamaño del puntero"); las esperas son .ani.
# Uso: python3 gen_cursores.py <carpeta-de-salida>
import math, os, struct, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZES = [32, 48, 64]
SS = 4  # supermuestreo
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def canvas(S):
    return Image.new("L", (S * SS, S * SS), 0)


def pts(p, k):
    return [(x * k, y * k) for x, y in p]


def dilate(mask, r_px):
    r = max(1, int(round(r_px)))
    a = np.array(mask) > 127
    from scipy import ndimage
    yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
    disk = (xx * xx + yy * yy) <= r * r
    return Image.fromarray((ndimage.binary_dilation(a, structure=disk) * 255).astype(np.uint8))


def compose(S, fill_mask, outline_r, fill_rgb, outline_rgb, extra=None):
    """fill_mask en supermuestreo; devuelve RGBA de tamaño S."""
    k = S * SS / 32
    outer = dilate(fill_mask, outline_r * k)
    W = S * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    # sombra suave
    sh = outer.filter(ImageFilter.GaussianBlur(1.1 * k))
    sh = Image.fromarray((np.array(sh) * 0.38).astype(np.uint8))
    shadow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    shadow.putalpha(sh)
    img.alpha_composite(shadow, (int(0.35 * k), int(0.9 * k)))
    o = Image.new("RGBA", (W, W), outline_rgb + (255,)); o.putalpha(outer)
    img.alpha_composite(o)
    f = Image.new("RGBA", (W, W), fill_rgb + (255,)); f.putalpha(fill_mask)
    img.alpha_composite(f)
    if extra: extra(img, k)
    return img.resize((S, S), Image.LANCZOS)


# ---------- formas (en una grilla de 32) ----------
ARROW = [(2, 2), (2, 24.5), (7.2, 19.6), (10.6, 27.4), (13.8, 26.0), (10.5, 18.6), (17.4, 18.6)]


def m_arrow(S, ox=0, oy=0, scale=1.0):
    m = canvas(S); k = S * SS / 32
    d = ImageDraw.Draw(m)
    d.polygon(pts([(ox + x * scale, oy + y * scale) for x, y in ARROW], k), fill=255)
    return m


def m_ibeam(S):
    m = canvas(S); k = S * SS / 32; d = ImageDraw.Draw(m)
    d.rectangle([15.2 * k, 7 * k, 16.8 * k, 25 * k], fill=255)
    for y in (5.6, 25.0):
        d.rounded_rectangle([12 * k, y * k, 20 * k, (y + 1.5) * k], radius=0.7 * k, fill=255)
    return m


def m_double_arrow(S, angle):
    m = canvas(S); k = S * SS / 32; d = ImageDraw.Draw(m)
    d.rectangle([15 * k, 9 * k, 17 * k, 23 * k], fill=255)
    d.polygon(pts([(16, 3.5), (21.5, 10), (10.5, 10)], k), fill=255)
    d.polygon(pts([(16, 28.5), (21.5, 22), (10.5, 22)], k), fill=255)
    return m.rotate(angle, resample=Image.BICUBIC, center=(16 * k, 16 * k)) if angle else m


def m_move(S):
    """Cuatro flechas con puntas más chicas (para que no se toquen y no queden huecos)."""
    m = canvas(S); k = S * SS / 32; d = ImageDraw.Draw(m)
    d.rectangle([15 * k, 7 * k, 17 * k, 25 * k], fill=255)
    d.rectangle([7 * k, 15 * k, 25 * k, 17 * k], fill=255)
    for poly in ([(16, 3), (20, 8), (12, 8)], [(16, 29), (20, 24), (12, 24)],
                 [(3, 16), (8, 12), (8, 20)], [(29, 16), (24, 12), (24, 20)]):
        d.polygon(pts(poly, k), fill=255)
    return m


def m_cross(S):
    m = canvas(S); k = S * SS / 32; d = ImageDraw.Draw(m)
    d.rectangle([15.3 * k, 4 * k, 16.7 * k, 28 * k], fill=255)
    d.rectangle([4 * k, 15.3 * k, 28 * k, 16.7 * k], fill=255)
    return m


def m_no(S):
    m = canvas(S); k = S * SS / 32; d = ImageDraw.Draw(m)
    d.ellipse([6 * k, 6 * k, 26 * k, 26 * k], fill=255)
    d.ellipse([8.8 * k, 8.8 * k, 23.2 * k, 23.2 * k], fill=0)
    line = canvas(S); dl = ImageDraw.Draw(line)
    dl.rectangle([7 * k, 14.6 * k, 25 * k, 17.4 * k], fill=255)
    line = line.rotate(-45, resample=Image.BICUBIC, center=(16 * k, 16 * k))
    return Image.fromarray(np.maximum(np.array(m), np.array(line)))


def m_hand(S):
    m = canvas(S); k = S * SS / 32; d = ImageDraw.Draw(m)
    rr = lambda x0, y0, x1, y1, r: d.rounded_rectangle([x0 * k, y0 * k, x1 * k, y1 * k], radius=r * k, fill=255)
    rr(10.6, 2.2, 15.2, 19, 2.3)       # índice
    rr(14.6, 11.2, 18.6, 20, 2.0)      # mayor
    rr(18.0, 12.2, 21.6, 20.5, 1.8)    # anular
    rr(21.0, 13.6, 24.2, 21, 1.6)      # meñique
    rr(9.6, 15.5, 24.2, 29.4, 4.2)     # palma
    d.polygon(pts([(10.2, 25.0), (4.2, 18.4), (6.2, 16.4), (11.4, 20.2)], k), fill=255)  # pulgar
    d.ellipse([3.4 * k, 15.6 * k, 7.0 * k, 19.2 * k], fill=255)
    return m


def hand_lines(img, k):
    d = ImageDraw.Draw(img)
    w = max(1, int(0.9 * k))
    for x, y0, y1 in ((15.0, 13.0, 17.6), (18.4, 13.8, 18.2), (21.3, 15.2, 18.8)):
        d.line([(x * k, y0 * k), (x * k, y1 * k)], fill=(20, 20, 20, 255), width=w)


def ring_frame(S, phase, cx=16, cy=16, ro=11, ri=6.2):
    """Anillo de colores (gira con phase 0..1). Devuelve (máscara, imagen de color) en supermuestreo."""
    W = S * SS; k = W / 32
    yy, xx = np.mgrid[0:W, 0:W].astype(np.float32)
    dx = xx / k - cx; dy = yy / k - cy
    r = np.sqrt(dx * dx + dy * dy)
    ang = (np.arctan2(dy, dx) / (2 * np.pi) + phase) % 1.0
    mask = ((r <= ro) & (r >= ri)).astype(np.float32)
    # bordes suaves
    mask = np.clip(np.minimum(ro - r, r - ri) * k * 0.6 + 0.5, 0, 1) * (mask > -1)
    h = ang * 6.0
    i = np.floor(h).astype(int) % 6; f = h - np.floor(h)
    v = np.ones_like(f); s = np.full_like(f, 0.72)
    p = v * (1 - s); q = v * (1 - s * f); t = v * (1 - s * (1 - f))
    rgb = np.zeros((W, W, 3), np.float32)
    for idx, (a, b, c) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        sel = i == idx
        rgb[sel, 0] = a[sel]; rgb[sel, 1] = b[sel]; rgb[sel, 2] = c[sel]
    # brillo arriba (volumen)
    shade = 0.82 + 0.18 * np.clip(-(dy) / ro, -1, 1)
    rgb *= shade[..., None]
    col = Image.fromarray(np.dstack([(rgb * 255).astype(np.uint8), (mask * 255).astype(np.uint8)]), "RGBA")
    return Image.fromarray((mask * 255).astype(np.uint8)), col


def busy(S, phase, small=False):
    W = S * SS; k = W / 32
    if small:
        cx, cy, ro, ri = 24.5, 24.5, 6.0, 3.2
    else:
        cx, cy, ro, ri = 16, 16, 11, 6.2
    mask, col = ring_frame(S, phase, cx, cy, ro, ri)
    out = dilate(mask, 1.1 * k)
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    if small:
        am = m_arrow(S)
        img.alpha_composite(compose_ss(S, am, 1.25, (0, 0, 0), (255, 255, 255)))
    sh = out.filter(ImageFilter.GaussianBlur(1.0 * k))
    shadow = Image.new("RGBA", (W, W), (0, 0, 0, 0)); shadow.putalpha(Image.fromarray((np.array(sh) * 0.35).astype(np.uint8)))
    img.alpha_composite(shadow, (0, int(0.8 * k)))
    o = Image.new("RGBA", (W, W), (255, 255, 255, 255)); o.putalpha(out)
    img.alpha_composite(o)
    # contorno fino oscuro para que se vea sobre blanco
    edge = Image.fromarray(np.clip(np.array(dilate(mask, 0.35 * k)).astype(int) - 0, 0, 255).astype(np.uint8))
    e = Image.new("RGBA", (W, W), (60, 60, 60, 255)); e.putalpha(edge)
    img.alpha_composite(e)
    img.alpha_composite(col)
    return img.resize((S, S), Image.LANCZOS)


def compose_ss(S, fill_mask, outline_r, fill_rgb, outline_rgb, extra=None):
    """Como compose pero devuelve en supermuestreo (para combinar)."""
    k = S * SS / 32; W = S * SS
    outer = dilate(fill_mask, outline_r * k)
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    sh = outer.filter(ImageFilter.GaussianBlur(1.1 * k))
    shadow = Image.new("RGBA", (W, W), (0, 0, 0, 0)); shadow.putalpha(Image.fromarray((np.array(sh) * 0.38).astype(np.uint8)))
    img.alpha_composite(shadow, (int(0.35 * k), int(0.9 * k)))
    o = Image.new("RGBA", (W, W), outline_rgb + (255,)); o.putalpha(outer); img.alpha_composite(o)
    f = Image.new("RGBA", (W, W), fill_rgb + (255,)); f.putalpha(fill_mask); img.alpha_composite(f)
    if extra: extra(img, k)
    return img


def help_extra(img, k):
    d = ImageDraw.Draw(img)
    cx, cy, r = 23.5, 23.5, 6.2
    d.ellipse([(cx - r - 1.1) * k, (cy - r - 1.1) * k, (cx + r + 1.1) * k, (cy + r + 1.1) * k], fill=(255, 255, 255, 255))
    d.ellipse([(cx - r) * k, (cy - r) * k, (cx + r) * k, (cy + r) * k], fill=(10, 132, 255, 255))
    f = ImageFont.truetype(FONT, int(9 * k))
    d.text((cx * k, (cy + 0.3) * k), "?", font=f, fill=(255, 255, 255, 255), anchor="mm")


# ---------- archivos .cur / .ani ----------

def dib(img):
    w, h = img.size
    a = np.array(img.convert("RGBA"))
    # premultiplicar no hace falta; BGRA de abajo hacia arriba
    bgra = a[::-1, :, [2, 1, 0, 3]].tobytes()
    and_row = ((w + 31) // 32) * 4
    andmask = b"\x00" * (and_row * h)
    hdr = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(bgra) + len(andmask), 0, 0, 0, 0)
    return hdr + bgra + andmask


def cur_bytes(images, hot):
    """images: lista de (tamaño, RGBA); hot: punto caliente en la grilla de 32."""
    n = len(images)
    out = struct.pack("<HHH", 0, 2, n)
    datas = [dib(im) for _, im in images]
    off = 6 + 16 * n
    for (S, im), data in zip(images, datas):
        hx = int(round(hot[0] * S / 32)); hy = int(round(hot[1] * S / 32))
        out += struct.pack("<BBBBHHII", S % 256, S % 256, 0, 0, hx, hy, len(data), off)
        off += len(data)
    return out + b"".join(datas)


def chunk(cid, data):
    pad = b"\x00" if len(data) % 2 else b""
    return cid + struct.pack("<I", len(data)) + data + pad


def ani_bytes(frames, hot, rate_jiffies):
    anih = struct.pack("<9I", 36, len(frames), len(frames), 0, 0, 0, 0, rate_jiffies, 1)
    icons = b"".join(chunk(b"icon", cur_bytes(f, hot)) for f in frames)
    fram = b"LIST" + struct.pack("<I", len(icons) + 4) + b"fram" + icons
    body = b"ACON" + chunk(b"anih", anih) + fram
    return b"RIFF" + struct.pack("<I", len(body)) + body


def build(outdir):
    os.makedirs(outdir, exist_ok=True)
    black, white = (0, 0, 0), (255, 255, 255)

    def save_cur(name, make, hot):
        ims = [(S, make(S)) for S in SIZES]
        open(os.path.join(outdir, name), "wb").write(cur_bytes(ims, hot))
        return ims[-1][1]

    prev = {}
    prev["flecha"] = save_cur("flecha.cur", lambda S: compose(S, m_arrow(S), 1.25, black, white), (2, 2))
    prev["mano"] = save_cur("mano.cur", lambda S: compose(S, m_hand(S), 1.15, white, (15, 15, 15), hand_lines), (12.9, 2.4))
    prev["texto"] = save_cur("texto.cur", lambda S: compose(S, m_ibeam(S), 1.0, black, white), (16, 16))
    prev["vertical"] = save_cur("tam-vertical.cur", lambda S: compose(S, m_double_arrow(S, 0), 1.2, black, white), (16, 16))
    prev["horizontal"] = save_cur("tam-horizontal.cur", lambda S: compose(S, m_double_arrow(S, 90), 1.2, black, white), (16, 16))
    prev["diag1"] = save_cur("tam-diagonal1.cur", lambda S: compose(S, m_double_arrow(S, 45), 1.2, black, white), (16, 16))
    prev["diag2"] = save_cur("tam-diagonal2.cur", lambda S: compose(S, m_double_arrow(S, -45), 1.2, black, white), (16, 16))
    prev["mover"] = save_cur("mover.cur", lambda S: compose(S, m_move(S), 1.2, black, white), (16, 16))
    prev["cruz"] = save_cur("cruz.cur", lambda S: compose(S, m_cross(S), 1.0, black, white), (16, 16))
    prev["prohibido"] = save_cur("prohibido.cur", lambda S: compose(S, m_no(S), 1.1, black, white), (16, 16))
    prev["ayuda"] = save_cur("ayuda.cur", lambda S: compose(S, m_arrow(S), 1.25, black, white, help_extra), (2, 2))

    N = 12
    frames = [[(S, busy(S, i / N)) for S in SIZES] for i in range(N)]
    open(os.path.join(outdir, "espera.ani"), "wb").write(ani_bytes(frames, (16, 16), 4))
    prev["espera"] = frames[0][-1][1]
    frames2 = [[(S, busy(S, i / N, small=True)) for S in SIZES] for i in range(N)]
    open(os.path.join(outdir, "trabajando.ani"), "wb").write(ani_bytes(frames2, (2, 2), 4))
    prev["trabajando"] = frames2[0][-1][1]
    return prev


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "cursores"
    prev = build(out)
    # hoja de vista previa (sobre claro y oscuro) para revisar
    keys = list(prev)
    sheet = Image.new("RGBA", (len(keys) * 80, 160), (235, 235, 235, 255))
    dark = Image.new("RGBA", (len(keys) * 80, 80), (40, 40, 48, 255)); sheet.alpha_composite(dark, (0, 80))
    for i, kk in enumerate(keys):
        sheet.alpha_composite(prev[kk], (i * 80 + 8, 8))
        sheet.alpha_composite(prev[kk], (i * 80 + 8, 88))
    sheet.convert("RGB").save(os.path.join(out, "_vista.png"))
    print("ok", keys)
