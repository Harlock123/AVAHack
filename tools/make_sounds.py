#!/usr/bin/env python3
"""Synthesises AVAHack's sound effects and ambience loops (spec 16.3) with nothing but the standard library.

Every sound is built from oscillators, noise, filters and envelopes with a fixed seed, so running this again
gives the same files. Output: src/AVAHack.App/Assets/Sounds/<cue>.wav (22,050 Hz, mono, 16-bit); a cue with
several takes has <cue>-2.wav, <cue>-3.wav ... and the game picks one at random each time.

Usage: python3 tools/make_sounds.py [cue ...]   (no cues: make them all)
"""
import math, os, random, struct, sys, wave

SR = 22050
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "AVAHack.App", "Assets", "Sounds")
TAU = 2 * math.pi


# ---- Building blocks --------------------------------------------------------------------------------------

def n(seconds):
    return max(1, int(seconds * SR))

def silence(seconds):
    return [0.0] * n(seconds)

def noise(seconds, rng):
    return [rng.uniform(-1, 1) for _ in range(n(seconds))]

def brown(seconds, rng, step=0.02):
    out, v = [], 0.0
    for _ in range(n(seconds)):
        v = max(-1, min(1, v + rng.uniform(-step, step)))
        out.append(v)
    return out

def osc(freq, seconds, shape="sine", phase=0.0):
    """freq: a number or a function of time (s) giving Hz."""
    out, ph = [], phase
    f = freq if callable(freq) else (lambda t, c=freq: c)
    for i in range(n(seconds)):
        t = i / SR
        x = ph % 1.0
        if shape == "sine": out.append(math.sin(TAU * x))
        elif shape == "saw": out.append(2 * x - 1)
        elif shape == "square": out.append(1.0 if x < 0.5 else -1.0)
        else: out.append(1 - 4 * abs(x - 0.5))  # triangle
        ph += f(t) / SR
    return out

def env(sig, attack=0.005, decay=0.2, curve=4.0, sustain=0.0, hold=0.0):
    """Attack, an optional hold, then an exponential decay to the sustain level."""
    out = []
    for i, x in enumerate(sig):
        t = i / SR
        if t < attack: g = t / attack
        elif t < attack + hold: g = 1
        else: g = sustain + (1 - sustain) * math.exp(-curve * (t - attack - hold) / max(1e-4, decay))
        out.append(x * g)
    return out

def fade(sig, fin=0.01, fout=0.05):
    a, b, m = n(fin), n(fout), len(sig)
    return [x * min(1, i / a if a else 1, (m - i) / b if b else 1) for i, x in enumerate(sig)]

def lowpass(sig, cutoff):
    out, y = [], 0.0
    for i, x in enumerate(sig):
        c = cutoff(i / SR) if callable(cutoff) else cutoff
        a = 1 - math.exp(-TAU * c / SR)
        y += a * (x - y)
        out.append(y)
    return out

def highpass(sig, cutoff):
    lo = lowpass(sig, cutoff)
    return [x - l for x, l in zip(sig, lo)]

def bandpass(sig, centre, q=4.0):
    """An RBJ band-pass biquad; centre may be a function of time."""
    out, x1, x2, y1, y2 = [], 0.0, 0.0, 0.0, 0.0
    for i, x in enumerate(sig):
        f = centre(i / SR) if callable(centre) else centre
        w = TAU * max(20, min(f, SR * 0.45)) / SR
        alpha = math.sin(w) / (2 * q)
        b0, b2, a0, a1, a2 = alpha, -alpha, 1 + alpha, -2 * math.cos(w), 1 - alpha
        y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out

def mix(*layers):
    """Each layer: a list, or (list, gain), or (list, gain, start seconds)."""
    parts = []
    for l in layers:
        if isinstance(l, tuple):
            sig, g = l[0], l[1]
            start = n(l[2]) if len(l) > 2 else 0
        else:
            sig, g, start = l, 1.0, 0
        parts.append((sig, g, start))
    length = max(start + len(sig) for sig, _, start in parts)
    out = [0.0] * length
    for sig, g, start in parts:
        for i, x in enumerate(sig):
            out[start + i] += x * g
    return out

def gain(sig, g):
    return [x * g for x in sig]

def reverb(sig, wet=0.3, size=1.0, tail=0.6):
    """A small Schroeder reverb: four combs into two all-passes."""
    out = sig + [0.0] * n(tail)
    combs = [int(d * size) for d in (1116, 1188, 1277, 1356)]
    fb = 0.78
    total = [0.0] * len(out)
    for d in combs:
        d = max(1, d * SR // 44100)
        buf = [0.0] * len(out)
        for i in range(len(out)):
            buf[i] = out[i] + (buf[i - d] * fb if i >= d else 0)
            total[i] += buf[i] / 4
    for d in (225, 556):
        d = max(1, d * SR // 44100)
        res = [0.0] * len(total)
        for i in range(len(total)):
            delayed = res[i - d] if i >= d else 0.0
            xd = total[i - d] if i >= d else 0.0
            res[i] = -0.5 * total[i] + xd + 0.5 * delayed
        total = res
    return [x * (1 - wet) + t * wet for x, t in zip(out, total)]

def partials(base, ratios, decays, seconds, amps=None):
    """A struck metal or bell: inharmonic sine partials, each with its own decay."""
    amps = amps or [1.0 / (k + 1) for k in range(len(ratios))]
    layers = [(env(osc(base * r, seconds), 0.001, d), a) for r, d, a in zip(ratios, decays, amps)]
    return mix(*layers)

def normalise(sig, peak=0.85):
    top = max(1e-6, max(abs(x) for x in sig))
    return [x * peak / top for x in sig]

def save(name, sig, peak=0.85, rms_db=None):
    """Writes a WAV, scaled to a peak level or, for the ambience loops, to an average (RMS) loudness."""
    sig = fade(sig, 0.002, 0.02) if rms_db is None else sig
    if rms_db is None:
        sig = normalise(sig, peak)
    else:
        rms = math.sqrt(sum(x * x for x in sig) / len(sig))
        g = 10 ** (rms_db / 20) / max(1e-9, rms)
        top = max(abs(x) for x in sig) * g
        sig = [x * g * min(1, 0.95 / top) for x in sig]
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x)) * 32767)) for x in sig))

def loop(make, seconds, overlap=2.0):
    """A seamless loop: make a little more than asked, then fold the overlap back onto the start."""
    sig = make(seconds + overlap)
    k = n(overlap)
    body = sig[: n(seconds)]
    for i in range(k):
        t = i / k
        body[i] = body[i] * t + sig[n(seconds) + i] * (1 - t)
    return body


# ---- Voices -----------------------------------------------------------------------------------------------

def voice(f0, seconds, formants, rng, rough=0.0, jitter=0.0):
    """A throat: a sawtooth pulse train through formant filters. f0 may change over time."""
    f = f0 if callable(f0) else (lambda t, c=f0: c)
    src = osc(lambda t: f(t) * (1 + jitter * math.sin(TAU * 7 * t)), seconds, "saw")
    if rough: src = mix(src, (noise(seconds, rng), rough))
    return mix(*[(bandpass(src, fr, q), g) for fr, q, g in formants])

AH = [(750, 6, 1.0), (1150, 7, 0.6), (2700, 9, 0.25)]
OH = [(500, 6, 1.0), (850, 7, 0.5), (2500, 9, 0.15)]
EE = [(300, 6, 1.0), (2200, 8, 0.5), (3000, 9, 0.2)]


# ---- The sounds -------------------------------------------------------------------------------------------

def make_all(rng, only):
    def want(name):
        return not only or name.split("-")[0] in only

    def S(name, sig, peak=0.85):
        if want(name):
            save(name, sig, peak)
            print(name)

    # Footsteps on stone: a low knock and a gritty scuff, a little different each time.
    for k in range(3):
        r = random.Random(100 + k)
        knock = env(lowpass(noise(0.12, r), 260 + k * 30), 0.002, 0.04)
        grit = env(bandpass(noise(0.12, r), 2200 + k * 300, 2), 0.004, 0.03)
        S("step" + ("" if k == 0 else f"-{k + 1}"), reverb(mix((knock, 1.0), (grit, 0.35)), 0.18, 0.7, 0.25), 0.6)
    for k in range(2):
        r = random.Random(110 + k)
        splash = env(bandpass(noise(0.3, r), lambda t: 1800 - 2400 * t, 1.5), 0.01, 0.12)
        S("step.water" + ("" if k == 0 else "-2"), mix((splash, 1.0), (env(lowpass(noise(0.2, r), 300), 0.003, 0.05), 0.5)), 0.6)
    S("step.monster", env(lowpass(noise(0.1, rng), 220), 0.003, 0.05), 0.45)
    S("step.heavy", reverb(mix((env(osc(lambda t: 70 - 30 * t, 0.35), 0.003, 0.15), 1.0), (env(lowpass(noise(0.3, rng), 180), 0.003, 0.1), 0.8)), 0.3, 1.1, 0.4), 0.75)
    S("bump", env(lowpass(noise(0.18, rng), 200), 0.002, 0.06), 0.7)

    # Swings and blows.
    for k in range(2):
        r = random.Random(200 + k)
        S("swing" + ("" if k == 0 else "-2"), env(bandpass(noise(0.28, r), lambda t, k=k: 700 + 2600 * t / 0.28 + k * 300, 2.5), 0.08, 0.12, 3, hold=0.05), 0.55)
    S("swing.heavy", env(bandpass(noise(0.45, rng), lambda t: 400 + 1400 * t / 0.45, 2), 0.15, 0.15, 3, hold=0.08), 0.65)
    S("miss", env(bandpass(noise(0.22, rng), lambda t: 1600 + 3000 * t, 3), 0.05, 0.08, 3), 0.4)
    for k in range(3):
        r = random.Random(300 + k)
        thud = env(osc(lambda t, k=k: 110 + k * 12 - 60 * t, 0.2), 0.002, 0.07)
        wet = env(bandpass(noise(0.2, r), 900 + k * 200, 1.5), 0.002, 0.06)
        S("hit.flesh" + ("" if k == 0 else f"-{k + 1}"), mix((thud, 1.0), (wet, 0.6)), 0.8)
    for k in range(2):
        r = random.Random(320 + k)
        clicks = mix(*[(env(bandpass(noise(0.05, r), 3000 + i * 500, 4), 0.001, 0.015), 1.0 / (i + 1), i * 0.025) for i in range(4)])
        S("hit.bone" + ("" if k == 0 else "-2"), mix((clicks, 1.0), (env(lowpass(noise(0.1, r), 500), 0.001, 0.03), 0.4)), 0.8)
    for k in range(2):
        S("hit.metal" + ("" if k == 0 else "-2"), mix((partials(420 + k * 90, [1, 2.76, 5.4, 8.93], [0.5, 0.3, 0.15, 0.08], 0.7), 1.0), (env(noise(0.05, rng), 0.001, 0.01), 0.5)), 0.75)
    S("hit.slime", lowpass(mix((env(osc(lambda t: 180 + 80 * math.sin(TAU * 18 * t), 0.3), 0.005, 0.12), 1.0), (env(bandpass(noise(0.3, rng), 600, 1.2), 0.01, 0.1), 0.8)), 1400), 0.75)
    S("hit.crit", mix((env(osc(lambda t: 140 - 90 * t, 0.3), 0.002, 0.1), 1.0), (env(highpass(noise(0.08, rng), 2500), 0.001, 0.02), 0.9), (env(bandpass(noise(0.25, rng), 800, 1.5), 0.002, 0.08), 0.6)), 0.9)
    S("block", mix((env(lowpass(noise(0.2, rng), 400), 0.002, 0.05), 1.0), (partials(260, [1, 2.3, 4.1], [0.25, 0.12, 0.06], 0.4), 0.5)), 0.8)

    # Heroes in pain.
    for k in range(3):
        r = random.Random(400 + k)
        f0 = (lambda t, k=k: (150 - k * 20) * (1 - 0.35 * t / 0.35))
        g = env(voice(f0, 0.35, AH if k != 1 else OH, r, rough=0.15), 0.02, 0.12, 3, hold=0.05)
        S("hero.hurt" + ("" if k == 0 else f"-{k + 1}"), g, 0.7)
    S("hero.down", env(voice(lambda t: 140 * (1 - 0.45 * t / 1.1), 1.1, OH, rng, rough=0.1), 0.05, 0.5, 2.5, hold=0.2), 0.7)
    S("party.dead", reverb(mix(*[(env(osc(f, 3.0, "saw"), 0.3, 1.6, 2.5), 0.3) for f in (55, 65.4, 77.8, 110)]), 0.4, 1.4, 1.5), 0.7)

    # Creatures: a bark when one notices you, a sound when it dies.
    def squeak(t0, f):
        return (env(osc(lambda t: f + 900 * math.sin(TAU * 9 * t), 0.09), 0.005, 0.03), 0.8, t0)
    S("bark.vermin", mix(squeak(0, 2600), squeak(0.12, 3000), squeak(0.22, 2400)), 0.55)
    S("bark.spider", env(highpass(noise(0.6, rng), 3500), 0.05, 0.3, 3, hold=0.15), 0.5)
    S("bark.undead", reverb(mix((env(voice(lambda t: 85 + 8 * math.sin(TAU * 3 * t), 1.0, OH, rng, rough=0.35), 0.15, 0.5, 2.5, hold=0.2), 1.0),
                               (mix(*[(env(bandpass(noise(0.04, rng), 2500, 4), 0.001, 0.01), 0.4, 0.6 + i * 0.07) for i in range(5)]), 1.0)), 0.3, 1.2, 0.6), 0.65)
    S("bark.goblin", mix(*[(env(voice(lambda t, i=i: 260 + 80 * math.sin(TAU * 11 * t) + i * 15, 0.11, EE, rng, rough=0.1), 0.005, 0.05), 1.0, i * 0.13) for i in range(5)]), 0.6)
    S("bark.ooze", lowpass(mix(*[(env(osc(lambda t, i=i: 90 + i * 30 + 300 * t, 0.15), 0.01, 0.06), 0.8, i * 0.09) for i in range(6)]), 900), 0.6)
    S("bark.deepFolk", env(voice(lambda t: 95 + 10 * math.sin(TAU * 5 * t), 0.8, OH, rng, rough=0.25, jitter=0.03), 0.08, 0.35, 2.5, hold=0.25), 0.6)
    S("bark.constructs", mix((env(highpass(noise(0.7, rng), 2000), 0.02, 0.4, 2.5, hold=0.2), 0.6), (partials(180, [1, 2.4, 3.9], [0.3, 0.15, 0.1], 0.5), 0.9, 0.05)), 0.65)
    S("bark.elementals", mix((env(lowpass(brown(0.9, rng, 0.08), 900), 0.1, 0.4, 2.5, hold=0.2), 1.0), (mix(*[(env(highpass(noise(0.02, rng), 3000), 0.001, 0.006), 0.6, rng.uniform(0, 0.8)) for _ in range(14)]), 0.8)), 0.65)
    S("bark.sporeKin", mix((env(lowpass(noise(0.4, rng), 700), 0.02, 0.15), 1.0), (env(osc(lambda t: 140 + 40 * t, 0.6, "triangle"), 0.1, 0.25, 2.5), 0.5)), 0.6)
    S("bark.serpentCult", mix((env(highpass(noise(0.9, rng), 2800), 0.08, 0.5, 2.5, hold=0.25), 0.8), (env(bandpass(noise(0.9, rng), lambda t: 4000 + 1500 * math.sin(TAU * 25 * t), 6), 0.1, 0.5, 2.5, hold=0.2), 0.6)), 0.55)
    S("bark.aberrations", reverb(env([a * b for a, b in zip(osc(lambda t: 300 + 120 * math.sin(TAU * 2 * t), 1.0), osc(173, 1.0))], 0.1, 0.5, 2.5, hold=0.3), 0.35, 1.2, 0.7), 0.55)
    S("bark.unmade", reverb(mix(*[(env(osc(f, 1.6, "saw"), 0.3, 0.8, 2.5, hold=0.4), 0.25) for f in (41, 43.5, 61.7, 87)]), 0.4, 1.5, 1.2), 0.7)
    S("bark.inkbound", mix(*[(env(bandpass(noise(0.12, rng), rng.uniform(2500, 5000), 1.5), 0.02, 0.05), rng.uniform(0.4, 0.9), i * 0.07) for i in range(9)],
                           (env(bandpass(noise(0.5, rng), lambda t: 6000 - 3000 * t, 8), 0.05, 0.2), 0.4, 0.15)), 0.55)
    S("bark.mourners", reverb(env(mix(*[(voice(lambda t, k=k: (330 + k * 40) * (1 - 0.25 * t / 1.4) * (1 + 0.04 * math.sin(TAU * 5 * t)), 1.4, OH if k else EE, rng, rough=0.05), 0.5) for k in range(2)]),
                              0.2, 0.7, 2.5, hold=0.4), 0.5, 1.5, 1.0), 0.6)
    S("bark.beast", mix((env(voice(lambda t: 120 - 30 * t, 0.5, AH, rng, rough=0.5, jitter=0.06), 0.03, 0.2, 2.5, hold=0.1), 1.0),), 0.65)
    S("bark.vermin.big", env(voice(lambda t: 200 + 40 * math.sin(TAU * 12 * t), 0.4, EE, rng, rough=0.3), 0.02, 0.15, 3, hold=0.1), 0.6)
    S("die.flesh", mix((env(voice(lambda t: 130 * (1 - 0.5 * t / 0.7), 0.7, AH, rng, rough=0.3), 0.02, 0.3, 2.5, hold=0.1), 0.8), (env(lowpass(noise(0.3, rng), 250), 0.005, 0.1), 1.0, 0.45)), 0.75)
    S("die.bones", mix(*[(env(bandpass(noise(0.06, rng), rng.uniform(1800, 4200), 3), 0.001, 0.02), rng.uniform(0.4, 1), i * 0.045 + rng.uniform(0, 0.02)) for i in range(14)]), 0.75)
    S("die.slime", lowpass(mix((env(noise(0.4, rng), 0.005, 0.15), 1.0), (env(osc(lambda t: 220 - 180 * t, 0.4), 0.005, 0.15), 0.8)), 1200), 0.75)
    S("die.machine", reverb(mix((partials(150, [1, 2.2, 3.7, 5.1], [0.6, 0.4, 0.25, 0.15], 1.0), 1.0), (env(highpass(noise(1.2, rng), 1800), 0.05, 0.6, 2.5, hold=0.3), 0.5, 0.2), (env(lowpass(noise(0.3, rng), 200), 0.002, 0.1), 1.0, 0.5)), 0.3, 1.2, 0.6), 0.8)
    S("die.paper", mix((env(bandpass(noise(0.5, rng), lambda t: 4500 - 2500 * t, 1.2), 0.005, 0.2), 1.0),
                       *[(env(bandpass(noise(0.08, rng), rng.uniform(2000, 4000), 1.5), 0.01, 0.04), 0.5, 0.1 + i * 0.09) for i in range(4)]), 0.7)
    S("die.spirit", reverb(env(mix(*[(osc(lambda t, f=f: f * (1 + 0.6 * t), 1.2), 0.3) for f in (440, 554, 659, 880)]), 0.05, 0.6, 3), 0.5, 1.4, 1.0), 0.6)

    # Doors, mechanisms and traps.
    S("door.open", mix((env(bandpass(noise(0.9, rng), lambda t: 300 + 500 * t, 8), 0.05, 0.5, 2, hold=0.4), 0.9), (env(lowpass(noise(0.1, rng), 300), 0.002, 0.04), 0.8, 0.85)), 0.6)
    S("door.close", reverb(env(lowpass(noise(0.35, rng), 220), 0.002, 0.12), 0.3, 1.0, 0.4), 0.8)
    S("portcullis", mix(*[(env(partials(rng.uniform(900, 1500), [1, 2.7, 4.1], [0.08, 0.05, 0.03], 0.12), 0.001, 0.05), rng.uniform(0.3, 0.8), i * 0.06) for i in range(22)]), 0.65)
    S("lever", mix((env(lowpass(noise(0.15, rng), 600), 0.002, 0.05), 1.0), (partials(320, [1, 2.5], [0.15, 0.08], 0.3), 0.6, 0.05), (env(bandpass(noise(0.3, rng), 1200, 3), 0.01, 0.1), 0.4, 0.1)), 0.7)
    S("plate", mix((env(bandpass(noise(0.05, rng), 1800, 3), 0.001, 0.015), 1.0), (env(bandpass(noise(0.6, rng), 250, 2), 0.05, 0.3, 2, hold=0.2), 0.6, 0.08)), 0.6)
    S("locked", mix(*[(env(partials(700 + i * 40, [1, 2.4], [0.04, 0.02], 0.08), 0.001, 0.03), 0.8, i * 0.07) for i in range(5)]), 0.55)
    S("unlock", mix((partials(1200, [1, 2.3, 3.8], [0.08, 0.04, 0.02], 0.2), 1.0), (partials(900, [1, 2.3], [0.06, 0.03], 0.2), 0.8, 0.12)), 0.6)
    S("secret", reverb(env(lowpass(brown(1.4, rng, 0.05), 400), 0.2, 0.7, 2, hold=0.5), 0.3, 1.2, 0.6), 0.7)
    S("chest", mix((env(bandpass(noise(0.6, rng), lambda t: 500 + 300 * t, 6), 0.05, 0.3, 2, hold=0.2), 0.8), (env(lowpass(noise(0.15, rng), 250), 0.002, 0.05), 1.0, 0.55)), 0.65)
    S("pit", mix((env(bandpass(noise(0.8, rng), lambda t: 2000 - 1800 * t / 0.8, 2), 0.05, 0.5, 2, hold=0.3), 0.6), (reverb(env(lowpass(noise(0.3, rng), 180), 0.002, 0.12), 0.4, 1.3, 0.5), 1.0, 0.8)), 0.8)
    S("stairs", reverb(mix(*[(env(lowpass(noise(0.1, rng), 260 - i * 25), 0.002, 0.04), 1.0 - i * 0.15, i * 0.28) for i in range(5)]), 0.4, 1.3, 0.8), 0.6)
    S("trap.dart", mix((partials(600, [1, 1.5], [0.06, 0.04], 0.15), 0.8), (env(bandpass(noise(0.15, rng), lambda t: 5000 - 20000 * t, 3), 0.003, 0.05), 0.8, 0.02)), 0.65)
    S("trap.fire", env(lowpass(mix((noise(1.0, rng), 1.0), (brown(1.0, rng, 0.1), 1.5)), lambda t: 3000 - 2000 * t), 0.03, 0.4, 2.5, hold=0.25), 0.75)
    S("trap.spikes", mix((env(highpass(noise(0.25, rng), 3000), 0.002, 0.08), 0.8), (partials(1500, [1, 1.9, 3.1], [0.1, 0.06, 0.04], 0.3), 0.6)), 0.7)
    S("trap.blade", mix((env(bandpass(noise(0.4, rng), lambda t: 800 + 3000 * t, 3), 0.1, 0.1, 3, hold=0.1), 0.8), (partials(900, [1, 2.76, 5.4], [0.3, 0.15, 0.08], 0.6), 0.5, 0.2)), 0.7)
    S("trap.boulder", reverb(env(lowpass(brown(2.0, rng, 0.06), 160), 0.2, 1.2, 2, hold=0.8), 0.3, 1.3, 0.8), 0.8)
    S("trap.gas", env(highpass(noise(1.4, rng), 1500), 0.08, 0.8, 2, hold=0.5), 0.5)
    S("trap.alarm", reverb(partials(523, [1, 2.0, 2.4, 3.0, 4.2], [1.2, 0.8, 0.6, 0.4, 0.3], 2.0), 0.3, 1.3, 1.0), 0.75)
    S("trap.lightning", mix((env(noise(0.5, rng), 0.001, 0.15), 1.0), (env(osc(lambda t: 60 + 4000 * math.exp(-30 * t), 0.5, "saw"), 0.001, 0.2), 0.5), (reverb(env(lowpass(brown(1.0, rng, 0.1), 200), 0.05, 0.6), 0.3), 0.8, 0.1)), 0.85)
    S("trap.teleport", reverb(env(mix(*[(osc(lambda t, f=f: f * (1 + 2 * t), 0.9), 0.3) for f in (300, 450, 600)]), 0.2, 0.4, 2.5, hold=0.2), 0.4, 1.3, 0.6), 0.6)
    S("trap.flood", env(lowpass(mix((noise(2.0, rng), 1.0), (brown(2.0, rng, 0.08), 1.2)), 1200), 0.4, 1.0, 2, hold=0.8), 0.7)

    # Things in hand.
    S("pickup", env(bandpass(noise(0.18, rng), 2500, 1.5), 0.01, 0.06), 0.45)
    S("gold", mix(*[(partials(rng.uniform(2200, 3400), [1, 2.4, 3.7], [0.12, 0.06, 0.03], 0.25), 0.7, i * 0.05 + rng.uniform(0, 0.02)) for i in range(6)]), 0.6)
    S("drink", lowpass(mix(*[(env(osc(lambda t, i=i: 300 + i * 40 + 600 * t, 0.12), 0.01, 0.05), 0.8, i * 0.16) for i in range(4)]), 1500), 0.6)
    S("eat", mix(*[(env(bandpass(noise(0.07, rng), 1800, 1.2), 0.002, 0.025), 0.8, i * 0.17) for i in range(4)]), 0.55)
    S("read", mix(*[(env(bandpass(noise(0.25, rng), 3500, 1.0), 0.05, 0.08), 0.6, i * 0.18) for i in range(2)]), 0.4)
    S("equip", mix((env(bandpass(noise(0.2, rng), 1400, 1.5), 0.01, 0.06), 0.7), (partials(900, [1, 2.7], [0.06, 0.03], 0.15), 0.4, 0.08)), 0.5)
    S("torch", env(lowpass(noise(0.6, rng), lambda t: 500 + 2500 * t), 0.15, 0.25, 2.5), 0.55)
    S("snuff", mix((env(lowpass(noise(0.25, rng), lambda t: 2500 - 2000 * t / 0.25), 0.005, 0.08), 0.8), (env(highpass(noise(0.4, rng), 3000), 0.05, 0.2), 0.25, 0.08)), 0.45)
    S("archive", reverb(mix((env(lowpass(noise(0.3, rng), 300), 0.002, 0.1), 1.0), (env(bandpass(noise(0.4, rng), lambda t: 900 - 600 * t, 4), 0.05, 0.2), 0.6),
                            (partials(240, [1, 2.3], [0.2, 0.1], 0.4), 0.5, 0.05)), 0.35, 1.2, 0.6), 0.8)

    # Magic, by school, and where a bolt lands.
    S("cast.destruction", env(lowpass(mix((noise(0.6, rng), 1.0), (brown(0.6, rng, 0.1), 1.5)), lambda t: 600 + 4000 * t), 0.08, 0.25, 3, hold=0.1), 0.7)
    S("cast.restoration", reverb(mix(*[(env(osc(f, 1.2), 0.005, 0.5), 0.3, i * 0.08) for i, f in enumerate((784, 988, 1175, 1568))]), 0.4, 1.3, 0.8), 0.6)
    S("cast.warding", reverb(env(mix((osc(220, 1.0), 0.5), (osc(330.5, 1.0), 0.4), (osc(441, 1.0), 0.3)), 0.2, 0.5, 2.5, hold=0.2), 0.4, 1.2, 0.6), 0.55)
    S("cast.conjuration", reverb(env(bandpass(noise(1.0, rng), lambda t: 300 + 2500 * t, 6), 0.3, 0.3, 3, hold=0.3), 0.4, 1.3, 0.6), 0.6)
    S("cast.illusion", reverb(env([a * (0.6 + 0.4 * math.sin(TAU * 6 * i / SR)) for i, a in enumerate(osc(lambda t: 600 + 400 * math.sin(TAU * 1.5 * t), 1.0))], 0.2, 0.5, 2.5, hold=0.2), 0.5, 1.3, 0.7), 0.55)
    S("impact.fire", env(lowpass(mix((noise(0.5, rng), 1.0), (brown(0.5, rng, 0.12), 1.5)), lambda t: 3000 - 2500 * t), 0.005, 0.25), 0.75)
    S("impact.frost", mix((env(highpass(noise(0.2, rng), 3000), 0.001, 0.06), 0.7), *[(partials(rng.uniform(2500, 4500), [1, 2.3], [0.15, 0.08], 0.3), 0.5, i * 0.04) for i in range(4)]), 0.65)
    S("impact.spark", mix((env(noise(0.25, rng), 0.001, 0.06), 0.8), (env(osc(lambda t: 2000 - 1500 * t, 0.25, "square"), 0.001, 0.08), 0.3)), 0.6)
    S("impact.acid", env(highpass(noise(0.8, rng), 2500), 0.01, 0.5, 2.5, hold=0.2), 0.5)
    S("impact.dark", reverb(env(osc(lambda t: 90 - 50 * t, 0.6), 0.005, 0.3), 0.4, 1.2, 0.6), 0.75)
    S("shoot", mix((partials(500, [1, 1.5, 2.1], [0.08, 0.05, 0.03], 0.15), 0.8), (env(bandpass(noise(0.25, rng), lambda t: 4500 - 8000 * t, 3), 0.003, 0.08), 0.7, 0.02)), 0.6)

    # Progress, crafts and the Waystone.
    S("levelup", reverb(mix(*[(env(osc(f, 0.7, "triangle"), 0.005, 0.35), 0.35, i * 0.11) for i, f in enumerate((523, 659, 784, 1047))]), 0.3, 1.2, 0.7), 0.6)
    S("waystone", reverb(env(mix(*[(osc(f * (1 + 0.003 * math.sin(i)), 2.4), 0.25) for i, f in enumerate((130.8, 196, 261.6, 329.6))]), 0.6, 1.0, 2.5, hold=0.6), 0.5, 1.4, 1.0), 0.55)
    S("anvil", reverb(partials(880, [1, 2.76, 5.4, 8.93], [0.6, 0.3, 0.15, 0.08], 0.9), 0.3, 1.1, 0.5), 0.7)
    S("brew", lowpass(mix(*[(env(osc(lambda t, i=i: 250 + i * 60 + 500 * t, 0.1), 0.005, 0.04), 0.7, i * 0.07 + rng.uniform(0, 0.03)) for i in range(9)]), 1400), 0.6)
    S("cook", env(highpass(noise(1.0, rng), 2000), 0.1, 0.6, 2, hold=0.3), 0.45)
    S("enchant", reverb(env(mix(*[(osc(lambda t, f=f: f * (1 + 0.5 * t), 1.0), 0.3) for f in (660, 880, 990)]), 0.2, 0.4, 3, hold=0.2), 0.5, 1.4, 0.8), 0.55)

    # Bosses: a sting when one wakes, a resolving chord when it falls, and the great bell.
    timp = lambda t0: (env(osc(lambda t: 65 - 15 * t, 1.0), 0.003, 0.4), 1.0, t0)
    brass = [(env(lowpass(osc(f, 2.2, "saw"), 1400), 0.15, 1.2, 2.5, hold=0.4), 0.22, 0.2) for f in (73.4, 87.3, 110, 146.8)]
    S("sting.boss", reverb(mix(timp(0), timp(0.18), *brass), 0.35, 1.3, 1.2), 0.85)
    S("sting.victory", reverb(mix(*[(env(lowpass(osc(f, 2.6, "saw"), 2000), 0.08, 1.4, 2.5, hold=0.5), 0.2, i * 0.12) for i, f in enumerate((130.8, 164.8, 196, 261.6, 329.6))]), 0.35, 1.3, 1.2), 0.8)
    S("bell", reverb(partials(110, [0.56, 0.92, 1.19, 1.71, 2, 2.74, 3, 3.76, 4.07], [3, 2.5, 2.2, 1.6, 1.4, 1.0, 0.9, 0.7, 0.6], 3.5,
                              [1, 0.8, 0.7, 0.5, 0.6, 0.35, 0.3, 0.25, 0.2]), 0.4, 1.5, 1.5), 0.85)

    # The interface.
    S("ui.click", env(bandpass(noise(0.03, rng), 2500, 3), 0.001, 0.01), 0.35)
    S("ui.open", env(bandpass(noise(0.15, rng), lambda t: 800 + 3000 * t, 2), 0.03, 0.06), 0.35)

    # Ambience: a loop for each region.
    def cellar(seconds):
        r = random.Random(900)
        bed = lowpass(brown(seconds, r, 0.015), 160)
        drips = [0.0] * n(seconds)
        t = 0.3
        while t < seconds - 0.5:
            d = env(osc(lambda u, f=r.uniform(1100, 1900): f * (1 + 1.5 * u), 0.08), 0.001, 0.025)
            for i, x in enumerate(d):
                if n(t) + i < len(drips): drips[n(t) + i] += x * r.uniform(0.3, 0.7)
            t += r.uniform(0.7, 2.8)
        return mix((bed, 1.6), (reverb(drips, 0.6, 1.5, 0.0)[: n(seconds)], 0.5))
    def works(seconds):
        r = random.Random(901)
        hum = mix((osc(55, seconds), 0.3), (osc(110.4, seconds), 0.15), (lowpass(brown(seconds, r, 0.02), 220), 1.2))
        clanks = [0.0] * n(seconds)
        t = 0.6
        while t < seconds - 1:
            c = partials(r.uniform(180, 320), [1, 2.6, 4.3], [0.4, 0.2, 0.1], 0.8)
            for i, x in enumerate(c):
                if n(t) + i < len(clanks): clanks[n(t) + i] += x * r.uniform(0.15, 0.35)
            t += r.uniform(1.5, 4.0)
        steam = env(highpass(noise(seconds, r), 2500), 0.5, seconds, 0.0, sustain=0.06)
        return mix((hum, 1.0), (reverb(clanks, 0.6, 1.6, 0.0)[: n(seconds)], 0.8), (steam, 0.6))
    def choir(seconds):
        r = random.Random(902)
        pad = mix(*[(voice(lambda u, f=f: f * (1 + 0.004 * math.sin(TAU * 0.3 * u + f)), seconds, AH if k % 2 == 0 else OH, r), 0.18)
                    for k, f in enumerate((130.8, 164.8, 196, 246.9))])
        swell = [x * (0.55 + 0.45 * math.sin(TAU * i / SR / seconds * 2)) for i, x in enumerate(pad)]
        water = lowpass(noise(seconds, r), 500)
        return mix((swell, 1.0), (water, 0.12))
    def heart(seconds):
        r = random.Random(903)
        drone = mix(*[(lowpass(osc(f, seconds, "saw"), 300), 0.2) for f in (36.7, 38.2, 55)])
        beats = [0.0] * n(seconds)
        t = 0.2
        while t < seconds - 0.5:
            for dt in (0.0, 0.22):
                b = env(osc(lambda u: 52 - 20 * u, 0.25), 0.004, 0.1)
                for i, x in enumerate(b):
                    if n(t + dt) + i < len(beats): beats[n(t + dt) + i] += x * 0.8
            t += 1.6
        whisper = bandpass(noise(seconds, r), lambda u: 1800 + 600 * math.sin(TAU * 0.2 * u), 3)
        return mix((drone, 1.0), (beats, 0.9), (whisper, 0.12))
    def stacks(seconds):
        # A hushed archive: paper stirring, candles guttering, a far-off chime, and a held breath of a hum.
        r = random.Random(904)
        hum = lowpass(mix((osc(73.4, seconds), 0.25), (osc(110, seconds), 0.12), (brown(seconds, r, 0.01), 0.8)), 260)
        rustle = [0.0] * n(seconds)
        t = 0.4
        while t < seconds - 0.6:
            p = env(bandpass(noise(0.35, r), r.uniform(2500, 4500), 1.2), 0.08, 0.15)
            for i, x in enumerate(p):
                if n(t) + i < len(rustle): rustle[n(t) + i] += x * r.uniform(0.15, 0.4)
            t += r.uniform(0.9, 2.6)
        crackle = [0.0] * n(seconds)
        for _ in range(int(seconds * 3)):
            at = n(r.uniform(0, seconds - 0.05))
            for i, x in enumerate(env(highpass(noise(0.015, r), 3000), 0.001, 0.004)):
                if at + i < len(crackle): crackle[at + i] += x * r.uniform(0.1, 0.3)
        chime = partials(660, [1, 2.4, 3.9], [1.5, 0.9, 0.6], 3.0)
        return mix((hum, 1.0), (rustle, 0.6), (crackle, 0.5), (reverb(chime, 0.7, 1.6, 0.0)[: n(3.0)], 0.08, seconds * 0.45))
    def rootdark(seconds):
        # Roots creaking under the weight of the world, water through soil, things chittering.
        r = random.Random(905)
        rumble = lowpass(brown(seconds, r, 0.02), 140)
        creaks = [0.0] * n(seconds)
        t = 0.5
        while t < seconds - 1.2:
            c = env(bandpass(noise(1.0, r), lambda u, f=r.uniform(180, 320): f + 60 * math.sin(TAU * 3 * u), 10), 0.2, 0.5, 2, hold=0.3)
            for i, x in enumerate(c):
                if n(t) + i < len(creaks): creaks[n(t) + i] += x * r.uniform(0.3, 0.6)
            t += r.uniform(2.0, 4.5)
        chitter = [0.0] * n(seconds)
        t = 1.0
        while t < seconds - 0.4:
            for k in range(r.randint(3, 7)):
                b = env(bandpass(noise(0.02, r), r.uniform(4000, 6000), 6), 0.001, 0.006)
                at = n(t + k * 0.035)
                for i, x in enumerate(b):
                    if at + i < len(chitter): chitter[at + i] += x * 0.4
            t += r.uniform(1.5, 3.5)
        seep = lowpass(noise(seconds, r), 700)
        return mix((rumble, 1.4), (creaks, 0.8), (chitter, 0.5), (seep, 0.08))
    def glass(seconds):
        # Halls of fused glass: a wind that whistles through cracks, and the glass ringing faintly as it settles.
        r = random.Random(906)
        wind = bandpass(noise(seconds, r), lambda u: 900 + 400 * math.sin(TAU * 0.13 * u) + 200 * math.sin(TAU * 0.31 * u), 8)
        tone = mix(*[(osc(f * (1 + 0.002 * math.sin(TAU * 0.2 * k)), seconds), 0.05) for k, f in enumerate((523.3, 784, 1046.5))])
        pings = [0.0] * n(seconds)
        t = 0.3
        while t < seconds - 1.6:
            pg = partials(r.uniform(1800, 3600), [1, 2.76, 5.4], [1.2, 0.6, 0.3], 1.5)
            for i, x in enumerate(pg):
                if n(t) + i < len(pings): pings[n(t) + i] += x * r.uniform(0.1, 0.3)
            t += r.uniform(0.8, 2.4)
        return mix((wind, 0.9), (tone, 1.0), (reverb(pings, 0.6, 1.7, 0.0)[: n(seconds)], 0.7))
    for name, f, secs in (("amb.cellar", cellar, 20), ("amb.works", works, 20), ("amb.choir", choir, 18), ("amb.heart", heart, 16),
                          ("amb.stacks", stacks, 20), ("amb.rootdark", rootdark, 20), ("amb.glass", glass, 18)):
        if want(name):
            save(name, loop(f, secs), rms_db=-24)
            print(name)

    # The Rootdark (spec 38). Its own generator, so the sounds above stay as they were.
    rng = random.Random(3838)
    S("bark.burrowers", mix((env(lowpass(brown(0.8, rng, 0.08), 500), 0.05, 0.4, 2.5, hold=0.2), 1.0),
                            *[(env(bandpass(noise(0.05, rng), rng.uniform(900, 1800), 3), 0.002, 0.03), 0.7, 0.1 + i * 0.08) for i in range(6)]), 0.65)
    S("bark.sporeThralls", reverb(mix((env(voice(lambda t: 110 + 12 * math.sin(TAU * 4 * t), 0.9, OH, rng, rough=0.4, jitter=0.04), 0.1, 0.4, 2.5, hold=0.25), 1.0),
                                      (env(lowpass(noise(0.6, rng), 900), 0.05, 0.3), 0.5, 0.3)), 0.3, 1.1, 0.5), 0.6)
    S("burrow", mix((env(lowpass(brown(1.0, rng, 0.1), 260), 0.1, 0.5, 2, hold=0.3), 1.0),
                    *[(env(bandpass(noise(0.08, rng), rng.uniform(600, 1400), 2), 0.002, 0.05), rng.uniform(0.4, 0.8), 0.3 + i * 0.06) for i in range(8)]), 0.75)
    S("spore", mix((env(lowpass(noise(0.12, rng), 400), 0.002, 0.06), 1.0), (env(bandpass(noise(1.0, rng), lambda t: 1800 - 1000 * t, 2), 0.05, 0.6, 2, hold=0.3), 0.6, 0.04)), 0.6)
    S("ignite", reverb(mix((env(lowpass(brown(0.3, rng, 0.2), 200), 0.001, 0.15), 1.0),
                           (env(lowpass(mix((noise(1.2, rng), 1.0), (brown(1.2, rng, 0.1), 1.5)), lambda t: 5000 - 3500 * t), 0.01, 0.7, 2.5, hold=0.3), 0.9, 0.02)), 0.3, 1.3, 0.6), 0.85)
    S("chop", mix((env(lowpass(noise(0.1, rng), 700), 0.001, 0.04), 1.0), (env(bandpass(noise(0.15, rng), 2200, 2), 0.002, 0.05), 0.6, 0.01),
                  (env(bandpass(noise(0.3, rng), 900, 3), 0.02, 0.15), 0.3, 0.06)), 0.7)

    # The Glass Roads (spec 39), on their own generator too.
    rng = random.Random(3939)
    def tinkle(t0, lo, hi, count, spread):
        return [(env(partials(rng.uniform(lo, hi), [1, 2.76, 5.4], [0.25, 0.12, 0.06], 0.3), 0.001, 0.08), rng.uniform(0.3, 0.8), t0 + rng.uniform(0, spread)) for _ in range(count)]
    S("bark.shardborn", reverb(mix(*tinkle(0, 2500, 5200, 14, 0.5), (env(highpass(noise(0.5, rng), 4000), 0.02, 0.2), 0.4)), 0.4, 1.2, 0.6), 0.6)
    S("bark.hollowed", reverb(env(mix(*[(voice(lambda t, k=k: (180 + k * 2.5) * (1 - 0.1 * t), 1.3, OH, rng, rough=0.08), 0.4) for k in range(3)]), 0.3, 0.7, 2.5, hold=0.4), 0.6, 1.6, 1.0), 0.55)
    S("glass", mix((env(bandpass(noise(0.12, rng), 3000, 1.2), 0.002, 0.05), 1.0), *tinkle(0.02, 3000, 6000, 10, 0.25)), 0.65)
    S("hit.glass", mix((env(bandpass(noise(0.08, rng), 2500, 1.5), 0.001, 0.03), 1.0), *tinkle(0.0, 2200, 4800, 6, 0.12)), 0.75)
    S("die.glass", reverb(mix((env(highpass(noise(0.4, rng), 2000), 0.002, 0.15), 0.9), *tinkle(0.03, 1800, 6000, 26, 0.7)), 0.4, 1.2, 0.6), 0.8)
    S("rift", reverb(mix((env(osc(lambda t: 55 + 30 * t, 1.4, "saw"), 0.3, 0.8, 2.5, hold=0.3), 0.5), (env(lowpass(noise(1.4, rng), lambda t: 300 + 1500 * t), 0.4, 0.6, 2.5, hold=0.3), 0.7)), 0.5, 1.5, 1.0), 0.7)
    S("seal", reverb(mix(*[(env(osc(f, 1.6), 0.002, 0.9), 0.3, i * 0.04) for i, f in enumerate((523, 659, 784, 1047))]), 0.5, 1.4, 1.0), 0.6)
    S("beam.charge", env(mix((osc(lambda t: 300 + 900 * t / 1.5, 1.5), 0.5), (osc(lambda t: 450 + 1350 * t / 1.5, 1.5), 0.3)), 1.3, 0.2, 2, hold=0.0), 0.5)
    S("beam", reverb(mix((env(osc(lambda t: 1800 - 1200 * t, 0.7, "saw"), 0.005, 0.6), 0.5), (env(highpass(noise(0.7, rng), 2500), 0.005, 0.5), 0.8)), 0.4, 1.3, 0.7), 0.8)


if __name__ == "__main__":
    make_all(random.Random(1234), set(sys.argv[1:]))
