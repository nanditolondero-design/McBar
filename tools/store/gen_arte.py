# McBar — Creado por Andrés Londero
# Genera el logo de McBar en alta resolución (redibujado por código) y las imágenes de la ficha de la Store:
# ícono 300/150/71, arte de caja 2160×2160 y arte de póster 1440×2160 (sobre el fondo "Ondas" de McBar).
# Uso: python3 gen_arte.py <carpeta-de-salida> <fondo-ondas.jpg>
import sys, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

S = 2048  # tamaño de trabajo del logo
K = S / 512.0
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def r(v): return int(round(v * K))


def logo(size):
    W = S
    base = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    # degradé: celeste arriba → azul → violeta abajo
    grad = Image.new("RGBA", (W, W))
    gd = ImageDraw.Draw(grad)
    stops = [(0.0, (0x86, 0xBA, 0xFF)), (0.12, (0x7A, 0xB0, 0xFF)), (0.16, (0x55, 0x92, 0xF8)), (0.55, (0x56, 0x7C, 0xF0)), (1.0, (0x6E, 0x4F, 0xE0))]
    for y in range(W):
        t = y / (W - 1)
        for i in range(len(stops) - 1):
            a, ca = stops[i]; b, cb = stops[i + 1]
            if a <= t <= b:
                u = (t - a) / (b - a) if b > a else 0
                c = tuple(int(ca[j] + (cb[j] - ca[j]) * u) for j in range(3))
                break
        gd.line([(0, y), (W, y)], fill=c + (255,))
    mask = Image.new("L", (W, W), 0)
    ImageDraw.Draw(mask).rounded_rectangle([r(24), r(24), r(488), r(488)], radius=r(112), fill=255)
    base.paste(grad, (0, 0), mask)
    d = ImageDraw.Draw(base)
    # barra de arriba
    for x0, x1 in ((70, 108), (133, 167), (185, 219), (237, 271)):
        d.rounded_rectangle([r(x0), r(47), r(x1), r(61)], radius=r(7), fill=(255, 255, 255, 235))
    # sombra del Dock
    sh = Image.new("L", (W, W), 0)
    ImageDraw.Draw(sh).rounded_rectangle([r(78), r(354), r(438), r(446)], radius=r(24), fill=110)
    sh = sh.filter(ImageFilter.GaussianBlur(r(10)))
    shadow = Image.new("RGBA", (W, W), (30, 20, 90, 0)); shadow.putalpha(Image.fromarray(__import__("numpy").minimum(__import__("numpy").array(sh), __import__("numpy").array(mask))))
    base.alpha_composite(shadow)
    d = ImageDraw.Draw(base)
    d.rounded_rectangle([r(76), r(346), r(436), r(436)], radius=r(22), fill=(255, 255, 255, 255))
    for (x0, y0, x1, y1, rad, col) in ((96, 344, 166, 414, 12, (0xFF, 0x5A, 0x5A)), (263, 344, 333, 414, 12, (0x2D, 0xC8, 0x4A)),
                                       (346, 344, 416, 414, 12, (0x0A, 0x84, 0xFF)), (172, 298, 256, 382, 14, (0xFF, 0xB8, 0x30))):
        d.rounded_rectangle([r(x0), r(y0), r(x1), r(y1)], radius=r(rad), fill=col + (255,))
    for cx in (130, 214, 381):
        d.ellipse([r(cx - 4), r(422), r(cx + 4), r(430)], fill=(40, 40, 50, 255))
    return base.resize((size, size), Image.LANCZOS)


def cover(bg, w, h):
    k = max(w / bg.width, h / bg.height)
    im = bg.resize((int(bg.width * k) + 1, int(bg.height * k) + 1), Image.LANCZOS)
    x = (im.width - w) // 2; y = (im.height - h) // 2
    return im.crop((x, y, x + w, y + h)).convert("RGBA")


def with_shadow(canvas, img, pos, blur, alpha=0.45):
    a = img.split()[3].point(lambda v: int(v * alpha))
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0)); sh.putalpha(a)
    pad = blur * 3
    big = Image.new("RGBA", (img.width + 2 * pad, img.height + 2 * pad), (0, 0, 0, 0)); big.alpha_composite(sh, (pad, pad))
    big = big.filter(ImageFilter.GaussianBlur(blur))
    canvas.alpha_composite(big, (pos[0] - pad, pos[1] - pad + blur // 2))
    canvas.alpha_composite(img, pos)


def text(canvas, s, size, cy, color=(255, 255, 255, 255)):
    f = ImageFont.truetype(FONT, size)
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    w = d.textlength(s, font=f)
    x = (canvas.width - w) / 2
    sh = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((x, cy + size * 0.04), s, font=f, fill=(60, 30, 0, 140))
    canvas.alpha_composite(sh.filter(ImageFilter.GaussianBlur(size * 0.05)))
    d.text((x, cy), s, font=f, fill=color)
    canvas.alpha_composite(layer)


if __name__ == "__main__":
    out = sys.argv[1]; bgpath = sys.argv[2]
    os.makedirs(out, exist_ok=True)
    L = logo(1024)
    L.save(os.path.join(out, "logo-1024.png"))
    for n in (300, 150, 71):
        L.resize((n, n), Image.LANCZOS).save(os.path.join(out, f"icono-{n}.png"))
    bg = Image.open(bgpath).convert("RGB")
    box = cover(bg, 2160, 2160)
    lg = L.resize((980, 980), Image.LANCZOS)
    with_shadow(box, lg, ((2160 - 980) // 2, 440), 40)
    text(box, "McBar", 210, 1500)
    box.convert("RGB").save(os.path.join(out, "arte-caja-2160.png"))
    post = cover(bg, 1440, 2160)
    lg = L.resize((820, 820), Image.LANCZOS)
    with_shadow(post, lg, ((1440 - 820) // 2, 480), 36)
    text(post, "McBar", 190, 1390)
    text(post, "Dock y barra superior para Windows", 62, 1640)
    post.convert("RGB").save(os.path.join(out, "arte-poster-1440x2160.png"))
    print("ok")
