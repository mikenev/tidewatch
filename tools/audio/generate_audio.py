#!/usr/bin/env python3
"""Procedural audio generator for Tidewatch (all original; no samples or recordings).

Theme "Lantern Quay" (Music/theme_shanty_loop.ogg): G Mixolydian, 6/8 at 116 dotted-quarter BPM,
32 bars. Concertina (detuned reedy pulses with bellows swell), fiddle (bowed saw with vibrato),
boot stomps, hand claps, and a low synthetic "hey" choir (formant-filtered saw chords).
The melody is a newly written tune, not taken from any existing shanty.
Battle "Broadside Weather" (Music/battle_loop.ogg): D Aeolian, 4/4 at 140 BPM, 18 bars. Driving low
strings, brass stabs, war drums, snare rolls.
Stings: victory (G major, concertina and fiddle flourish), defeat (descending D minor with a sinking
horn). SFX are mono.

Usage: python3 tools/audio/generate_audio.py [out_dir=Assets/Resources/Audio]
       python3 tools/audio/generate_audio.py --verify   (decode + check peaks <= -1.5 dBFS and loop seams)
Needs numpy, scipy, and ffmpeg with libvorbis. It does not touch .meta files.
"""
import os, sys, subprocess, tempfile
import numpy as np
from scipy import signal

SR = 44100
rng = np.random.default_rng(1721)
_ARGS = [a for a in sys.argv[1:] if not a.startswith("--")]
OUT = _ARGS[0] if _ARGS else "Assets/Resources/Audio"
PEAK_DB = -2.5   # pre-encode ceiling; decoded peaks are checked to be <= -1.5 dBFS
def N(d): return int(d * SR)
def tt(n): return np.arange(n) / SR
def expd(n, tau): return np.exp(-np.arange(n) / (tau * SR))
def adsr(n, a, d, s, r):
    e = np.full(n, float(s)); a, d, r = N(a), N(d), min(N(r), n)
    a = min(a, n); e[:a] = np.linspace(0, 1, a, endpoint=False)
    dd = e[a:a+d]; e[a:a+d] = np.linspace(1, s, len(dd))
    if r: e[n-r:] *= np.linspace(1, 0, r)
    return e
def sos(kind, fc, o=2): return signal.butter(o, fc, kind, fs=SR, output='sos')
def lp(x, fc, o=2): return signal.sosfilt(sos('low', min(fc, SR*.45), o), x)
def hp(x, fc, o=2): return signal.sosfilt(sos('high', fc, o), x)
def bp(x, lo, hi, o=2): return signal.sosfilt(sos('band', [lo, min(hi, SR*.45)], o), x)
def noise(n): return rng.standard_normal(n)
def phase(f, n): return np.cumsum(np.broadcast_to(f, (n,)).astype(float)) / SR
def saw(f, n, off=0.): return 2*((phase(f, n)+off) % 1) - 1
def pulse(f, n, w=.5): return np.where((phase(f, n) % 1) < w, 1., -1.)
def sine(f, n): return np.sin(2*np.pi*phase(f, n))
def tvlp(x, cut, q=2):
    y = np.zeros_like(x); zi = None
    for i in range(0, len(x), 256):
        s = sos('low', float(np.clip(cut[i], 40, SR*.45)), q)
        if zi is None: zi = np.zeros((s.shape[0], 2))
        y[i:i+256], zi = signal.sosfilt(s, x[i:i+256], zi=zi)
    return y
def midi(m): return 440*2**((m-69)/12)

# ---------- instruments
def harpsichord(f, d, v=1.):
    n = N(d); x = saw(f, n) + .5*pulse(f*2, n, .3)
    x = tvlp(x, 1500 + 7000*expd(n, .05)) * expd(n, .35) * adsr(n, .001, 0, 1, .03)
    return x * v * .22
def celesta(f, d, v=1.):
    n = N(d); x = sine(f, n) + .3*sine(f*4, n)*expd(n, .08) + .15*sine(f*9.2, n)*expd(n, .03)
    return x * expd(n, .5) * adsr(n, .002, 0, 1, .05) * v * .3
def pizz(f, d, v=1.):
    n = N(d); x = tvlp(saw(f, n) + saw(f*1.004, n, .3), 400 + 3000*expd(n, .03))
    return x * expd(n, .12) * adsr(n, .002, 0, 1, .02) * v * .25
def bassoon(f, d, v=1.):
    n = N(d); vib = 1 + .003*np.sin(2*np.pi*5*tt(n))
    x = pulse(f*vib, n, .18) + .4*saw(f*vib, n)
    x = bp(x, 80, 1400) * adsr(n, .025, .08, .75, min(.06, d*.4))
    return x * v * .3
def strings(f, d, v=1.):
    n = N(d); x = sum(saw(f*(1+k), n, i*.21) for i, k in enumerate((-.005, 0, .006)))
    return tvlp(x, 500 + 1800*expd(n, .06)) * adsr(n, .006, .05, .7, min(.04, d*.3)) * v * .2
def brass(f, d, v=1., br=1.):
    n = N(d); x = sum(saw(f*(1+k), n, i*.17) for i, k in enumerate((-.006, 0, .007)))
    e = adsr(n, .025, .12, .7, min(.08, d*.4))
    return tvlp(x, 300 + br*4000*expd(n, .12) + 1000*e) * e * v * .22
def membrane(f0, d, f1=None, tau=.15, click=.3):
    n = N(d); f1 = f1 or f0*.6; f = f1 + (f0-f1)*expd(n, .03)
    return sine(f, n)*expd(n, tau) + click*lp(noise(n), 3000)*expd(n, .004)
def woodblock(f=900, d=.08):
    n = N(d); return (sine(f, n) + .5*sine(f*2.7, n)) * expd(n, .015)
def brush(d=.12, v=1.):
    n = N(d); return bp(noise(n), 2000, 9000) * expd(n, .03) * v * .35
def hat(d=.05):
    n = N(d); return hp(noise(n), 7000) * expd(n, .012) * .3

def reverb(x, wet=.2, dur=1.6, tau=.4, circular=False):
    n = N(dur); ir = lp(noise(n)*expd(n, tau), 5000); ir[:N(.01)] = 0
    ir /= np.sqrt(np.sum(ir**2)); ir *= .5
    if circular:
        y = np.real(np.fft.ifft(np.fft.fft(x) * np.fft.fft(ir, len(x))))
        return x*(1-wet) + 2*wet*y
    y = signal.fftconvolve(x, ir); o = np.zeros(len(y)); o[:len(x)] = x*(1-wet); return o + 2*wet*y

class Track:
    def __init__(s, dur): s.L = N(dur); s.L -= s.L % 2; s.b = np.zeros((s.L, 2))
    def put(s, x, pos, g=1., pan=0.):
        i = int(pos*SR); idx = (i + np.arange(len(x))) % s.L
        for c, gc in ((0, np.sqrt((1-pan)/2)), (1, np.sqrt((1+pan)/2))):
            np.add.at(s.b[:, c], idx, x*g*gc*1.414)
    def done(s, wet=.2):
        y = np.stack([reverb(s.b[:, c], wet, circular=True) for c in (0, 1)], 1)
        y -= y.mean(0)
        # pre-roll: start the file 25 ms before the downbeat so the loop point sits in the quiet
        # tail of the previous bar instead of on a drum attack (the loop stays exactly circular)
        y = np.roll(y, N(.025), axis=0)
        return y / np.abs(y).max() * 10**(PEAK_DB/20)

def concertina(f, d, v=1.):
    n = N(d); trem = 1 + .08*np.sin(2*np.pi*6.5*tt(n))
    x = pulse(f, n, .35) + .7*pulse(f*1.004, n, .42) + .3*pulse(f*2, n, .25)
    x = bp(x, 250, 3500) * adsr(n, .03, .1, .8, min(.06, d*.4)) * trem
    return x * v * .16
def fiddle(f, d, v=1.):
    n = N(d); vib = 1 + .006*np.sin(2*np.pi*5.8*tt(n))*np.clip(tt(n)/.15, 0, 1)
    x = saw(f*vib, n) + .3*saw(f*vib*1.002, n, .4)
    x = bp(x, 400, 5000) + .3*bp(x, 2500, 3500)
    return x * adsr(n, .025, .08, .75, min(.05, d*.3)) * v * .2
def choir_hey(f, d, v=1., vowel='eh'):
    # clearly synthetic: stacked saws through fixed vowel formants, no consonant or breath
    F = {'eh': ((530, 1.), (1840, .5), (2480, .25)), 'oh': ((450, 1.), (800, .6), (2830, .15))}[vowel]
    n = N(d); vib = 1 + .004*np.sin(2*np.pi*5*tt(n))
    src = sum(saw(f*vib*(1+k), n, i*.3) for i, k in enumerate((-.008, 0, .009)))
    x = sum(g*bp(src, fc*.85, fc*1.15) for fc, g in F)
    return x * adsr(n, .02, .1, .7, min(.12, d*.5)) * v * .35
def clap():
    n = N(.12); x = np.zeros(n)
    for k in range(3): i = N(.008*k); m = n - i; x[i:] += bp(noise(m), 900, 4000) * expd(m, .012 if k < 2 else .04)
    return x * .35
def stomp(): return membrane(65, .3, 40, .07, .9) + .3*lp(noise(N(.3)), 600)*expd(N(.3), .03)
def snare(v=1.):
    n = N(.18); return (bp(noise(n), 1500, 7000)*expd(n, .05)*.5 + .4*sine(190, n)*expd(n, .03)) * v

# ---------- music
def shanty_theme():
    bpm = 116; e8 = 60/bpm/3; bar = 6*e8; bars = 32; T = Track(bars*bar)
    # G Mixolydian progression, one chord per bar: I I bVII I | IV I V(m) I  (x4 with variations)
    prog = [55, 55, 53, 55, 48, 55, 50, 55] * 4
    qual = {55: (0, 4, 7), 53: (0, 4, 7), 48: (0, 4, 7), 50: (0, 3, 7)}
    for b, r in enumerate(prog):
        s = b*bar
        T.put(stomp(), s, 1.0, -.1); T.put(stomp(), s + 3*e8, .85, .1)
        T.put(clap(), s + 3*e8, .9, .35)
        if b % 2: T.put(clap(), s + 5*e8, .5, -.35)
        T.put(hat(.04), s + 2*e8, .3, .5); T.put(hat(.04), s + 5*e8, .3, .5)
        # concertina oom-pah-pah in 6/8: bass on 1 & 4, chord on 2-3 & 5-6
        for k in (0, 3):
            T.put(concertina(midi(r - 12), e8*.95, 1.1), s + k*e8, 1, -.25)
            for j, iv in enumerate(qual[r]):
                T.put(concertina(midi(r + iv), e8*1.8, .55), s + (k+1)*e8, 1, -.25 + j*.05)
        # synthetic "hey" pad on the downbeat of bars 4, 8 (every 4th bar), and sustained choir in B-section
        if b % 4 == 3:
            for iv in (0, 7, 12): T.put(choir_hey(midi(r - 12 + iv), e8*1.4, .9, 'eh'), s + 3*e8, 1, 0)
        if 16 <= b < 24:
            for iv in qual[r]: T.put(choir_hey(midi(r - 12 + iv), bar*.98, .3, 'oh'), s, 1, .2)
    # fiddle melody (bars 0-15 A/A', 24-31 A''), 2-bar phrases written for this game (pos in eighths, midi, len)
    A1 = [(0, 74, 2), (2, 71, 1), (3, 72, 1), (4, 74, 1), (5, 76, 1), (6, 77, 2), (8, 76, 1), (9, 74, 3)]
    A2 = [(0, 72, 1), (1, 71, 1), (2, 69, 1), (3, 67, 2), (5, 69, 1), (6, 71, 2), (8, 72, 1), (9, 74, 3)]
    A3 = [(0, 79, 2), (2, 77, 1), (3, 76, 2), (5, 74, 1), (6, 72, 1), (7, 74, 1), (8, 76, 1), (9, 77, 3)]
    A4 = [(0, 76, 1), (1, 74, 1), (2, 72, 1), (3, 71, 2), (5, 72, 1), (6, 69, 2), (8, 71, 1), (9, 67, 3)]
    A = [A1, A2, A3, A4]
    for ph in list(range(0, 8)) + list(range(12, 16)):
        notes = A[ph % 4]; s = ph*2*bar
        for pos, m, d in notes:
            T.put(fiddle(midi(m), d*e8*.95, .9), s + pos*e8, 1, .25)
            if ph >= 4: T.put(concertina(midi(m - 12), d*e8*.95, .35), s + pos*e8, 1, .3)  # doubled 2nd time
    # B-section (bars 16-23): fiddle answers the choir with a lower counter-line
    B = [(0, 67, 3), (3, 71, 3), (6, 72, 2), (8, 71, 1), (9, 69, 3)]
    for ph in range(8, 12):
        for pos, m, d in B: T.put(fiddle(midi(m + (2 if ph % 2 else 0)), d*e8*.95, .7), ph*2*bar + pos*e8, 1, .25)
    return T.done(.16)

def battle_theme():
    bpm = 140; q = 60/bpm; e16 = q/4; bar = 4*q; bars = 18; T = Track(bars*bar)
    roots = [38, 38, 46, 45, 38, 38, 43, 45, 38, 41, 46, 45, 38, 38, 46, 48, 45, 45]  # D Aeolian: i i bVI V(m) ...
    for b, r in enumerate(roots):
        s = b*bar
        for k in range(8):                                      # low string 8ths with accent pattern 3+3+2
            acc = k in (0, 3, 6)
            T.put(strings(midi(r), q/2*.9, 1.2 if acc else .7), s + k*q/2, 1, -.2)
            T.put(strings(midi(r + 12), q/2*.9, .5 if acc else .3), s + k*q/2, 1, .2)
        for k in (0, 3, 6): T.put(membrane(80, .35, 45, .12, .6), s + k*q/2, 1.0)            # war drum
        T.put(membrane(130, .25, 80, .08, .4), s + 7*q/2, .6, .3)
        T.put(snare(), s + q, .8, -.1); T.put(snare(), s + 3*q, .8, -.1)
        for k in range(16): T.put(hat(.03), s + k*e16, .35 if k % 4 else .55, .5)
        if b % 4 == 3:                                           # snare roll into the next phrase
            for k in range(8): T.put(snare(.4 + .07*k), s + 3*q + k*e16/2, .6, -.1)
        third = 3 if r in (38, 43, 45) else 4
        if b % 2 == 0:
            for iv in (0, third, 7): T.put(brass(midi(r + 24 + iv), q*.7, .45), s + 1.5*q, 1, (iv-3)/8)
        for iv in (0, third, 7): T.put(brass(midi(r + 24 + iv), q*1.4, .4, .8), s + 3*q, 1, (iv-3)/8)
    mel = [(0, 62, 3), (3, 65, 1), (4, 64, 2), (6, 62, 2), (8, 60, 2), (10, 62, 6),
           (16, 69, 3), (19, 67, 1), (20, 65, 2), (22, 64, 2), (24, 65, 4), (28, 62, 4)]
    for rep in (1, 2):                                           # melody over bars 4-11
        for pos, m, d in mel:
            T.put(brass(midi(m), d*q/2*.95, .75, 1.3), (4*rep - 0)*bar + pos*q/2 + (rep-1)*4*bar*0, 1, .1)
    return T.done(.14)

VICTORY_RMS = .091   # match defeat_sting (~0.089 decoded); reached by soft-knee compression, not gain
def glue(y, target):
    """Raise loudness to `target` RMS with a tanh soft-knee compressor, keeping the peak at PEAK_DB."""
    c = 10**(PEAK_DB/20)
    for drive in np.arange(1., 12., .05):
        z = np.tanh(y/c*drive); z = z/np.abs(z).max()*c
        if np.sqrt(np.mean(z**2)) >= target: return z
    return z

def sting(kind):
    x = np.zeros(N(2.4))
    def add(s, p, g=1.):
        i = int(p*SR); e = min(len(x), i+len(s)); x[i:e] += s[:e-i]*g
    if kind == 'victory':
        for i, m in enumerate((67, 71, 74, 79)): add(fiddle(midi(m), .16), i*.11, .9); add(stomp(), i*.11, .4)
        for m in (43, 55, 59, 62, 67): add(concertina(midi(m), 1.3), .48)
        add(fiddle(midi(83), 1.2), .48, .8); add(choir_hey(midi(43), .5, .8), .48); add(clap(), .48, .8)
    else:
        for i, m in enumerate((69, 68, 67, 65)): add(fiddle(midi(m), .3), i*.26, .7)
        n = N(1.4); f = midi(50)*(1 - .08*(1-expd(n, .5)))
        add(tvlp(saw(f, n) + saw(f*1.006, n, .3), 300 + 1400*expd(n, .4)) * adsr(n, .05, .2, .7, .6) * .3, 1.0)
        for m in (50, 53, 57): add(concertina(midi(m), 1.2), 1.0, .6)
        add(membrane(60, .8, 35, .3, .5), 1.0)
    y = reverb(x, .25, 1.2, .3); y = y[np.argmax(np.abs(y) > 1e-4):][:N(3.4)]
    y = y - y.mean(); a, b = N(.002), N(.5)
    y[:a] *= np.linspace(0, 1, a); y[-b:] *= np.linspace(1, 0, b)**2; y[0] = y[-1] = 0
    y = y / np.abs(y).max() * 10**(PEAK_DB/20)
    if kind == 'victory': y = glue(y, VICTORY_RMS)
    return np.stack([y, .97*y + .03*np.roll(y, 300)], 1)

# ---------- sfx (mono); every length is kept ~40 ms under the spec limit to allow for Vorbis padding
def cannon_fire():
    n = N(.52); x = membrane(70, .52, 38, .14, 1.4)
    x += .9*tvlp(noise(n), 5000*expd(n, .05) + 200)*expd(n, .12)
    x += .2*bp(noise(n), 200, 900)*expd(n, .25)
    return x
def gunboat_fire():
    x = np.zeros(N(.34))
    for k, g in enumerate((1., .8, .65)):                 # quick 3-gun ripple, light
        n = N(.2); s = membrane(120 - 12*k, .2, 70, .05, .8) + .6*tvlp(noise(n), 4500*expd(n, .03) + 300)*expd(n, .05)
        i = N(.045*k); x[i:i+n] += s[:len(x)-i]*g
    return x
def ball_hit():
    n = N(.33); x = .8*bp(noise(n), 700, 3500)*expd(n, .02)       # wood crack
    for f in (310, 470, 820): x += .3*sine(f*rng.uniform(.95, 1.05), n)*expd(n, .04)
    x += .5*membrane(140, .33, 90, .04, .3)
    for k in range(4):
        m = N(.03); i = N(.03 + .05*k*rng.uniform(.7, 1.3)); x[i:i+m] += .3*bp(noise(m), 1500, 5000)*expd(m, .006)
    return x
def ship_sink():
    n = N(1.12); t = np.arange(n)/n
    x = .7*tvlp(noise(n), 3500*expd(n, .3) + 300)*expd(n, .35)       # splash
    for k in range(14):                                             # glugs (bubbles)
        m = N(.06); f = rng.uniform(250, 650); i = N(.25 + .06*k + rng.uniform(0, .03))
        if i + m < n: x[i:i+m] += .35*sine(f*(1 + 1.5*np.arange(m)/m), m)*np.sin(np.pi*np.arange(m)/m)
    x += .4*bp(noise(n), 120, 500)*expd(n, .5)                       # hull creak/wash
    for f in (110, 165): x += .15*saw(f*(1 - .2*t), n)*np.sin(np.pi*t)**2 * .5
    return x
def harbor_breach():
    n = N(.92); x = .9*membrane(55, .92, 35, .25, 1.2) + .6*tvlp(noise(n), 2500*expd(n, .15) + 150)*expd(n, .2)
    bell = sum(sine(f, n)*g for f, g in ((587, 1.), (1415, .4), (2280, .2)))
    i = N(.08); x[i:] += .35*bell[:n-i]*expd(n-i, .3)               # alarm bell clang
    i = N(.38); x[i:] += .3*bell[:n-i]*expd(n-i, .2)
    return x
def coin():
    x = np.zeros(N(.34))
    for k, m in enumerate((88, 95)):
        n = N(.26); s = (sine(midi(m), n) + .4*sine(midi(m)*2.76, n))*expd(n, .07)
        i = N(.06*k); x[i:i+n] += s[:len(x)-i]
    return x
def build():
    x = np.zeros(N(.74))
    for k, (p, f) in enumerate(((0, 180), (.16, 200), (.32, 170))):  # mallet knocks on timber
        n = N(.22); s = membrane(f, .22, f*.7, .05, .9) + .4*bp(noise(n), 500, 2500)*expd(n, .015)
        i = N(p); x[i:i+n] += s[:len(x)-i]
    n = N(.3); i = N(.44); x[i:i+n] += .6*(sine(midi(79), n) + .3*sine(midi(86), n))*expd(n, .1)
    return x
def upgrade():
    x = np.zeros(N(.74))
    for k, m in enumerate((67, 71, 74, 79)):
        n = N(.3); s = concertina(midi(m), .3, 3.) + .4*sine(midi(m+12), n)*expd(n, .08)
        i = N(.09*k); x[i:i+n] += s[:len(x)-i]
    n = N(.25); i = N(.0); x[i:i+n] += .4*membrane(160, .25, 110, .05, .7)
    return x
def sell():
    x = np.zeros(N(.54))
    for k, m in enumerate((84, 79, 76)):
        n = N(.2); s = (sine(midi(m), n) + .4*sine(midi(m)*2.76, n))*expd(n, .06)
        i = N(.07*k); x[i:i+n] += s[:len(x)-i]
    n = N(.3); i = N(.22); x[i:i+n] += .5*bp(noise(n), 2000, 7000)*expd(n, .06)*(1 + np.sin(2*np.pi*40*tt(n)))  # coin rattle
    return x
def wave_horn():
    n = N(1.6); f = midi(43)*(1 + .03*np.clip(tt(n)/.15, 0, 1) - .03)   # conch-like horn, slight scoop
    x = sum(g*saw(f*h*(1 + .003*np.sin(2*np.pi*4.5*tt(n))), n) for h, g in ((1, 1.), (1.5, .35), (2, .3)))
    x = tvlp(x, 400 + 1800*adsr(n, .25, .3, .7, .4)) * adsr(n, .15, .2, .85, .45)
    return x + .1*bp(noise(n), 500, 2000)*adsr(n, .1, .2, .5, .4)
def ui_click(): return woodblock(1300, .1)*.8 + .2*bp(noise(N(.1)), 3000, 8000)*expd(N(.1), .002)
def ui_error():
    x = np.zeros(N(.24))
    for k in range(2):
        n = N(.09); s = bp(pulse(midi(46), n, .3), 150, 1500)*adsr(n, .003, .02, .8, .02)
        i = N(.11*k); x[i:i+n] += s
    return x

# ---------- M2 sfx
def mortar_fire():          # deep thump + hollow tube "toonk" + smoke hiss
    n = N(.56); x = 1.2*membrane(52, .56, 32, .16, 1.0)
    x += .5*sine(210*(1 - .25*np.arange(n)/n), n)*expd(n, .07)
    x += .5*tvlp(noise(n), 2500*expd(n, .08) + 200)*expd(n, .15)
    return x
def mortar_impact():        # falling whistle tail, then heavy splash-boom
    x = np.zeros(N(.76)); n = N(.16); f = 1400*(1 - .45*np.arange(n)/n)
    x[:n] += .25*sine(f, n)*np.linspace(.2, 1, n)
    m = N(.6); b = 1.1*membrane(48, .6, 30, .2, 1.2) + .8*tvlp(noise(m), 4000*expd(m, .1) + 250)*expd(m, .2)
    b += .3*bp(noise(m), 300, 1800)*expd(m, .3)
    x[n:n+m] += b[:len(x)-n]
    return x
def chainshot_fire():       # sharp crack + whirling chain (amplitude-modulated rattle)
    n = N(.48); x = .9*membrane(95, .48, 55, .07, 1.2) + .6*tvlp(noise(n), 6000*expd(n, .03) + 300)*expd(n, .06)
    whirl = (1 + np.sin(2*np.pi*(18 - 8*np.arange(n)/n)*tt(n)))/2
    x += .45*bp(noise(n), 2500, 7500)*whirl*np.clip(tt(n)/.05, 0, 1)*expd(n, .2)
    for k in range(10):
        m = N(.012); i = N(.05 + .035*k)
        if i + m < n: x[i:i+m] += .25*bp(noise(m), 3000, 9000)*expd(m, .003)
    return x
def net_cast():             # rope whoosh, lead weights clinking, soft splash
    x = np.zeros(N(.62)); n = N(.34); t = np.arange(n)/n
    x[:n] += .8*tvlp(noise(n), 500*2**(2.2*np.sin(np.pi*t)))*np.sin(np.pi*t)**2
    for k in range(5):
        m = N(.05); i = N(.2 + .03*k + rng.uniform(0, .015)); f = rng.uniform(1900, 2600)
        x[i:i+m] += .2*(sine(f, m) + .5*sine(f*2.4, m))*expd(m, .012)
    m = N(.3); i = N(.32); x[i:i+m] += .5*tvlp(noise(m), 2500*expd(m, .1) + 300)*expd(m, .1)
    return x
def fireraft_explode():     # powder-keg blast, crackling fire, debris
    n = N(.96); x = 1.2*membrane(60, .96, 30, .2, 1.4)
    x += 1.0*tvlp(noise(n), 7000*expd(n, .06) + 300)*expd(n, .22)
    crack = (rng.random(n) > .985)*hp(noise(n), 2500); x += .5*crack*expd(n, .45)
    for k in range(6):
        m = N(.04); i = N(.12 + .1*k + rng.uniform(0, .04)); x[i:i+m] += .25*bp(noise(m), 600, 2500)*expd(m, .01)
    return x
def galleon_horn():         # deep, ominous horn: low fundamental, minor-second beating partner, slow swell
    n = N(1.9); f = midi(31)*(1 - .03*(1 - np.clip(tt(n)/.3, 0, 1)))   # G1, scoops up
    vib = 1 + .004*np.sin(2*np.pi*3.5*tt(n))
    x = sum(g*saw(f*h*vib, n, i*.2) for i, (h, g) in enumerate(((1, 1.), (1.0595, .35), (2, .45), (3, .2))))
    x = tvlp(x, 180 + 1100*adsr(n, .45, .3, .65, .5)) * adsr(n, .3, .25, .85, .55)
    return x + .15*bp(noise(n), 200, 800)*adsr(n, .3, .2, .6, .5)
def ship_move():            # creaking timber, water swish along the hull, rigging rattle
    x = np.zeros(N(.86)); n = len(x); t = np.arange(n)/n
    x += .6*tvlp(noise(n), 400 + 1400*np.sin(np.pi*t))*np.sin(np.pi*t)**1.5        # water swish
    for p in (.05, .4):                                                             # two creaks
        m = N(.28); f = rng.uniform(180, 240)*(1 + .25*np.arange(m)/m)
        cr = bp(pulse(f + 20*np.sin(2*np.pi*9*tt(m)), m, .1), 300, 2200)*np.sin(np.pi*np.arange(m)/m)
        i = N(p); x[i:i+m] += .35*cr[:n-i]
    for k in range(6):                                                              # rigging ticks
        m = N(.02); i = N(.15 + .1*k + rng.uniform(0, .04))
        if i + m < n: x[i:i+m] += .2*bp(noise(m), 1500, 5000)*expd(m, .005)
    return x

# ---------- M3 sfx
def thunder():              # Storm Isle: sharp crack, then a long rolling, lowpassed rumble with uneven swells
    n = N(2.2); t = tt(n)
    crack = hp(noise(n), 1200)*expd(n, .035)*.9 + bp(noise(n), 300, 3000)*expd(n, .08)*.6
    swell = 1 + .5*np.sin(2*np.pi*1.3*t + 1.) + .3*np.sin(2*np.pi*2.9*t + .4)
    roll = tvlp(noise(n), 900*expd(n, .5) + 120)*np.clip(t/.12, 0, 1)*expd(n, .75)*np.clip(swell, .2, None)
    boom = membrane(48, 2.2, 28, .5, .2)*np.clip(t/.05, 0, 1)
    return crack + 1.4*roll + .6*boom

# ---------- write
def edge(x, fout=.03):
    x = x - x.mean(); a, b = N(.002), N(fout)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b)**2; x[0] = x[-1] = 0; return x
def loud(x, target):
    x = x * target/np.sqrt(np.mean(x**2)); c = 10**(PEAK_DB/20)
    if np.abs(x).max() > c:
        x = np.tanh(x/c*.9)/np.tanh(.9)*c
        x *= min(1., target/np.sqrt(np.mean(x**2)))   # the soft limiter adds gain to quiet parts; undo it
    return x
def decoded_peak_db(path):
    raw = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', path, '-f', 'f32le', '-'])
    return 20*np.log10(np.abs(np.frombuffer(raw, '<f4')).max())
def write_ogg(path, x, q, ceil_db=-1.7):
    # encode, then re-encode scaled down if Vorbis overshoot pushes the decoded peak above the ceiling
    for _ in range(4):
        _encode(path, x, q); pk = decoded_peak_db(path)
        if pk <= ceil_db: return
        x = x * 10**((ceil_db - pk - .2)/20)
    raise RuntimeError(f'{path}: peak {pk:.2f} dBFS')
def _encode(path, x, q):
    os.makedirs(os.path.dirname(path), exist_ok=True); ch = 1 if x.ndim == 1 else 2
    with tempfile.NamedTemporaryFile(suffix='.f32', delete=False) as f: f.write(np.clip(x, -1, 1).astype('<f4').tobytes())
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 'f32le', '-ar', str(SR), '-ac', str(ch), '-i', f.name,
                    '-c:a', 'libvorbis', '-q:a', str(q), path], check=True); os.unlink(f.name)

SFX = dict(cannon_fire=cannon_fire, gunboat_fire=gunboat_fire, ball_hit=ball_hit, ship_sink=ship_sink,
           harbor_breach=harbor_breach, coin=coin, build=build, upgrade=upgrade, sell=sell,
           wave_horn=wave_horn, ui_click=ui_click, ui_error=ui_error,
           # M2 (appended last so the shared rng stream, and thus every earlier clip, is unchanged)
           mortar_fire=mortar_fire, mortar_impact=mortar_impact, chainshot_fire=chainshot_fire, net_cast=net_cast,
           fireraft_explode=fireraft_explode, galleon_horn=galleon_horn, ship_move=ship_move,
           thunder=thunder)  # M3, appended last (rng stream for earlier clips unchanged)
QUIET = {'ui_click': .07, 'ui_error': .08, 'gunboat_fire': .10, 'coin': .09, 'galleon_horn': .10}
# max length (s): spec (Assets/ART_AUDIO_LIST.md); M2 sounds have no spec length yet, so these are our own lean targets
LIMITS = {'Music/victory_sting': (2, 4), 'Music/defeat_sting': (2, 4), 'Music/theme_shanty_loop': (30, 60),
          'Music/battle_loop': (30, 60), 'SFX/cannon_fire': .6, 'SFX/gunboat_fire': .4, 'SFX/ball_hit': .4,
          'SFX/ship_sink': 1.2, 'SFX/harbor_breach': 1., 'SFX/coin': .4, 'SFX/build': .8, 'SFX/upgrade': .8,
          'SFX/sell': .6, 'SFX/wave_horn': (1, 2), 'SFX/ui_click': .15, 'SFX/ui_error': .3,
          'SFX/mortar_fire': .6, 'SFX/mortar_impact': .8, 'SFX/chainshot_fire': .5, 'SFX/net_cast': .65,
          'SFX/fireraft_explode': 1., 'SFX/galleon_horn': (1.5, 2.), 'SFX/ship_move': .9,
          'SFX/thunder': (1.5, 2.5)}
# clips that are peak-normalized (dBFS) instead of RMS-matched; verify fails if the decoded peak exceeds PEAK_MAX
PEAK_NORM = {'thunder': -7.0}
PEAK_MAX = {'SFX/thunder': -6.0}

def main():
    write_ogg(f'{OUT}/Music/theme_shanty_loop.ogg', shanty_theme(), 2)
    write_ogg(f'{OUT}/Music/battle_loop.ogg', battle_theme(), 2)
    write_ogg(f'{OUT}/Music/victory_sting.ogg', sting('victory'), 3)
    write_ogg(f'{OUT}/Music/defeat_sting.ogg', sting('defeat'), 3)
    for name, fn in SFX.items():
        x = fn(); x = x[np.argmax(np.abs(x) > 1e-3):]
        if name in PEAK_NORM:   # quiet ambience (game mix peak already ~0.97): fixed peak, not matched RMS
            x = edge(x); x = x/np.abs(x).max()*10**(PEAK_NORM[name]/20)
            path = f'{OUT}/SFX/{name}.ogg'
            for _ in range(4):   # Vorbis shifts transient peaks; nudge gain until the decoded peak is within 0.1 dB
                _encode(path, x, 2); pk = decoded_peak_db(path)
                if abs(pk - PEAK_NORM[name]) <= .1: break
                x = x*10**((PEAK_NORM[name] - pk)/20)
            assert pk <= PEAK_MAX[f'SFX/{name}'], (path, pk)
        else:
            x = edge(loud(edge(x), QUIET.get(name, .12)))
            write_ogg(f'{OUT}/SFX/{name}.ogg', x, 2)

def verify():
    """Decode every OGG under OUT; report duration/size/DC/peak/RMS, edges and loop seams. Returns 1 on failure."""
    import glob
    bad = 0
    for k in LIMITS:
        if not os.path.exists(f'{OUT}/{k}.ogg'): print(f'MISSING {k}.ogg'); bad = 1
    for p in sorted(glob.glob(f'{OUT}/**/*.ogg', recursive=True)):
        ch = int(subprocess.check_output(['ffprobe', '-v', 'error', '-show_entries', 'stream=channels', '-of', 'csv=p=0', p]).split()[0])
        x = np.frombuffer(subprocess.check_output(['ffmpeg', '-v', 'error', '-i', p, '-f', 'f32le', '-']), '<f4').reshape(-1, ch)
        m = x.mean(1); pk = 20*np.log10(np.abs(x).max())
        s = f"{os.path.relpath(p, OUT):30s} {len(x)/44100:6.3f}s {os.path.getsize(p)/1024:6.1f}KB ch={ch} dc={m.mean():+.5f} peak={pk:5.1f}dBFS rms={np.sqrt(np.mean(m**2)):.3f}"
        if pk > PEAK_MAX.get(os.path.relpath(p, OUT)[:-4], -1.5): bad = 1; s += ' PEAK!'
        if '_loop' in p:
            seam = np.abs(x[0]-x[-1]).max(); p99 = np.percentile(np.abs(np.diff(x, axis=0)), 99)
            s += f' seam={seam:.4f} p99step={p99:.4f}' + (' SEAM!' if seam > p99 else '')
            bad |= seam > p99
        else:
            s += f' first={np.abs(x[0]).max():.4f} last={np.abs(x[-1]).max():.4f}'
        lim = LIMITS.get(os.path.relpath(p, OUT)[:-4]); dur = len(x)/44100
        if lim is not None:
            lo, hi = lim if isinstance(lim, tuple) else (0, lim)
            if not lo <= dur <= hi: bad = 1; s += f' LENGTH! ({lo}-{hi} s)'
        print(s)
    return int(bad)

if __name__ == '__main__':
    if '--verify' in sys.argv: sys.exit(verify())
    main()
