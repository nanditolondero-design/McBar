# McBar — Creado por Andrés Londero
# (1.8.0) Para regenerar: python3 gen_sonidos.py ../../src/McBar/Assets/Sonidos
# Genera los sonidos "estilo Mac" de McBar (1.8.0): cortos, suaves y ORIGINALES (sintetizados por código,
# no son los de Apple). WAV 16 bits, 44,1 kHz, mono.
# Uso: python3 gen_sonidos.py <carpeta-de-salida>
import os, sys, wave
import numpy as np

SR = 44100


def env(n, attack=0.004, decay=0.25):
    t = np.arange(n) / SR
    a = np.clip(t / attack, 0, 1)
    return a * np.exp(-t / decay)


def tone(freq, dur, decay, partials=((1, 1.0),), attack=0.004, vib=0.0):
    n = int(dur * SR); t = np.arange(n) / SR
    s = np.zeros(n)
    for mult, amp in partials:
        s += amp * np.sin(2 * np.pi * freq * mult * t + vib * np.sin(2 * np.pi * 5 * t))
    return s * env(n, attack, decay)


def noise(dur, decay, lp=0.2, seed=1):
    rng = np.random.default_rng(seed)
    n = int(dur * SR)
    x = rng.standard_normal(n)
    y = np.zeros(n); a = lp
    for i in range(1, n):  # filtro pasa bajos simple
        y[i] = y[i - 1] + a * (x[i] - y[i - 1])
    return y * env(n, 0.002, decay)


def at(base, snd, start):
    i = int(start * SR)
    end = min(len(base), i + len(snd))
    base[i:end] += snd[: end - i]
    return base


def silence(d):
    return np.zeros(int(d * SR))


def save(path, s, gain=0.5):
    s = s / (np.max(np.abs(s)) + 1e-9) * gain
    # fundido final para que no haga "clic"
    f = min(len(s), int(0.01 * SR)); s[-f:] *= np.linspace(1, 0, f)
    data = (s * 32767).astype("<i2").tobytes()
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data)


GLASS = ((1, 1.0), (2.76, 0.35), (5.4, 0.12))  # parciales tipo copa (inarmónicos)


def build(out):
    os.makedirs(out, exist_ok=True)
    # Aviso / notificación: "ting" de cristal
    save(os.path.join(out, "aviso.wav"), at(silence(0.9), tone(1568, 0.9, 0.22, GLASS), 0.0), 0.45)
    # Alerta: dos notas suaves que bajan
    s = silence(0.7); at(s, tone(988, 0.5, 0.12, GLASS), 0); at(s, tone(784, 0.6, 0.16, GLASS), 0.11)
    save(os.path.join(out, "alerta.wav"), s, 0.45)
    # Error: golpecito grave y redondo
    s = silence(0.45); at(s, tone(196, 0.45, 0.08, ((1, 1), (2, 0.4), (3, 0.15))), 0); at(s, noise(0.05, 0.01, 0.08, 3) * 0.6, 0)
    save(os.path.join(out, "error.wav"), s, 0.5)
    # Sonido general (".Default"): "pop" corto
    s = silence(0.25); at(s, tone(880, 0.25, 0.05, ((1, 1), (2, 0.25))), 0)
    save(os.path.join(out, "pop.wav"), s, 0.4)
    # Conectar: dos notas que suben / desconectar: que bajan
    s = silence(0.6); at(s, tone(659, 0.4, 0.1, GLASS), 0); at(s, tone(988, 0.5, 0.14, GLASS), 0.09)
    save(os.path.join(out, "conectar.wav"), s, 0.42)
    s = silence(0.6); at(s, tone(988, 0.4, 0.1, GLASS), 0); at(s, tone(659, 0.5, 0.14, GLASS), 0.09)
    save(os.path.join(out, "desconectar.wav"), s, 0.42)
    # Papelera: papel arrugado (ruido en ráfagas)
    s = silence(0.8)
    for i, st in enumerate((0.0, 0.07, 0.15, 0.22, 0.33, 0.41)):
        at(s, noise(0.12, 0.03, 0.35 + 0.05 * (i % 3), seed=10 + i) * (1.0 - i * 0.1), st)
    save(os.path.join(out, "papelera.wav"), s, 0.5)
    # Captura: obturador (dos clics con cuerpo)
    s = silence(0.35)
    at(s, noise(0.06, 0.012, 0.5, 21), 0.0); at(s, tone(2200, 0.05, 0.01), 0.0)
    at(s, noise(0.08, 0.02, 0.35, 22) * 0.8, 0.09); at(s, tone(1500, 0.06, 0.015) * 0.6, 0.09)
    save(os.path.join(out, "captura.wav"), s, 0.55)


if __name__ == "__main__":
    o = sys.argv[1] if len(sys.argv) > 1 else "sonidos"
    build(o)
    print("ok", sorted(os.listdir(o)))
