"""Round 17 (owner: "切った時の音が安っぽい・聞こえづらい", "長押しはシャクシャク/ズバズバ", "硬い物の音が安っぽい").

Builds the cut sounds from CC0 recordings (Kenney "Impact Sounds" and "RPG Audio", tools/audio/kenney_cc0, licence
files next to them) layered with synthesised parts:
  blade swish "シュッ" + edge ring "キン" + material recording + low body "ドン".
Every sound starts on its transient (the cut lands on the beat), ends with a fade, and peaks at the same level.

Writes Unity Resources/Audio/Sfx/<Sfx name>.wav (loaded by AudioDirector over the synthesised fallbacks) and
review/round17_sounds/ preview files for listening on a PC.

    python tools/audio/build_cut_sfx.py
"""
import os
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt, resample_poly

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SRC = os.path.join(HERE, "kenney_cc0")
OUT = os.path.join(ROOT, "unity_project", "Assets", "RhythmRunner", "Resources", "Audio", "Sfx")
PREVIEW = os.path.join(ROOT, "review", "round17_sounds")
SR = 44100
rng = np.random.default_rng(17)


def load(name):
    d, sr = sf.read(os.path.join(SRC, name + ".ogg"), dtype="float64")
    if d.ndim > 1:
        d = d.mean(axis=1)
    if sr != SR:
        d = resample_poly(d, SR, sr)
    return d


def onset(x, rel=.12, pre_ms=1.5):
    """Cut the silence before the transient so the hit is heard the moment the sound starts."""
    a = np.abs(x)
    i = int(np.argmax(a > a.max() * rel))
    return x[max(0, i - int(SR * pre_ms / 1000)):]


def fit(x, ms, fade_ms=25):
    x = x[:int(SR * ms / 1000)].copy()
    f = min(len(x), int(SR * fade_ms / 1000))
    x[-f:] *= np.linspace(1, 0, f) ** 2
    return x


def filt(x, kind, f):
    sos = butter(2, f, btype=kind, fs=SR, output="sos")
    return sosfilt(sos, x)


def pitch(x, ratio):
    """Resample: ratio > 1 = higher and shorter."""
    up, down = 1000, int(round(1000 * ratio))
    return resample_poly(x, up, down)


def t_axis(sec):
    return np.arange(int(SR * sec)) / SR


def env(t, attack, decay):
    return np.minimum(1, t / max(attack, 1e-5)) * np.exp(-t / decay)


def mix(*parts):
    n = max(len(p) for p, _ in parts)
    out = np.zeros(n)
    for p, g in parts:
        out[:len(p)] += p * g
    return out


def swish(sec=.09, hi=7500, lo=2600, gain=1.0):
    """Air torn by the blade: noise whose brightness sweeps down."""
    t = t_axis(sec)
    noise = rng.standard_normal(len(t))
    bright = filt(noise, "highpass", hi)
    mid = filt(filt(noise, "highpass", lo), "lowpass", hi)
    k = np.clip(t / sec, 0, 1)
    return (bright * (1 - k) + mid * k) * env(t, .004, sec * .35) * gain


def ring(freqs, sec, decay, gain=1.0, attack=.0008):
    """Inharmonic metal/crystal partials (the blade edge, a gem)."""
    t = t_axis(sec)
    out = np.zeros(len(t))
    for i, f in enumerate(freqs):
        ph = rng.uniform(0, 2 * np.pi)
        out += np.sin(2 * np.pi * f * t + ph) * np.exp(-t / (decay * (1 - .12 * i))) / (1 + .35 * i)
    return out * np.minimum(1, t / attack) * gain


def thump(f0=130, f1=50, sec=.12, gain=1.0):
    """The weight of the cut: a short falling sine (felt more than heard on phones, so a little 2nd harmonic)."""
    t = t_axis(sec)
    f = f1 + (f0 - f1) * np.exp(-t / .025)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.tanh(1.6 * (np.sin(ph) + .3 * np.sin(2 * ph))) * env(t, .002, sec * .3) * gain


def sparkle(sec=.35, count=14, gain=1.0):
    """Tiny shards falling: short high pings scattered in time."""
    t = t_axis(sec)
    out = np.zeros(len(t))
    for _ in range(count):
        at = int(rng.uniform(.01, sec * .7) * SR)
        f = rng.uniform(5000, 9500)
        tt = t_axis(.03)
        p = np.sin(2 * np.pi * f * tt) * np.exp(-tt / .006) * rng.uniform(.3, 1)
        out[at:at + len(p)] += p[:len(out) - at]
    return out * gain


def finish(x, peak=.89, ms=None):
    x = x - np.mean(x[:64]) * 0
    if ms:
        x = fit(x, ms)
    x = filt(x, "highpass", 35)                      # no DC / sub rumble phones can't play
    x = np.tanh(x / np.max(np.abs(x)) * 1.4) / np.tanh(1.4)  # gentle glue: denser, louder at the same peak
    return x * peak


# ------------------------------------------------------------------ recordings
knife = onset(load("knifeSlice2"))
chop = onset(load("chop"))
glass_l = onset(load("impactGlass_light_002"))
glass_m = onset(load("impactGlass_medium_001"))
glass_h = onset(load("impactGlass_heavy_000"))
metal_h = onset(load("impactMetal_heavy_003"))
metal_l = onset(load("impactMetal_light_001"))
bell = onset(load("impactBell_heavy_004"))
plank = onset(load("impactPlank_medium_004"))
wood_m = onset(load("impactWood_medium_002"))
wood_h = onset(load("impactWood_heavy_001"))
generic = onset(load("impactGeneric_light_004"))
plate = onset(load("impactPlate_light_000"))
soft = onset(load("impactSoft_medium_002"))
punch = onset(load("impactPunch_medium_000"), rel=.3)
tin = onset(load("impactTin_medium_001"))
mining = onset(load("impactMining_002"), rel=.3)


def blade():
    """The common cut: "ズバッ" — knife recording, swish, edge ring, body. Played with every slash."""
    return finish(mix(
        (filt(knife, "highpass", 1200), .8),
        (swish(.1), .5),
        (ring([3950, 5870, 8120], .16, .045), .12),
        (thump(140, 55, .13), .55),
    ), ms=230)


SOUNDS = {
    # the blade layer itself
    "Blade": blade,
    # hard things (owner: "ダイヤモンドとか硬いもの")
    "MatCrystal": lambda: finish(mix(
        (glass_l, .9), (fit(pitch(glass_m, 1.25), 420), .55),
        (ring([2637, 3951, 5274, 7040, 8372], .7, .22), .28),
        (sparkle(.45, 18), .32), (thump(180, 80, .08), .25),
    ), ms=650),
    "MatMetal": lambda: finish(mix(
        (metal_h, 1.0), (fit(bell, 300), .55), (fit(pitch(metal_l, .8), 300), .35),
        (ring([620, 1490, 2310, 3170], .6, .2), .16), (thump(110, 45, .16), .6),
    ), ms=600),
    "MatWood": lambda: finish(mix(
        (fit(plank, 360), .9), (wood_m, .6), (fit(chop, 200), .35), (thump(120, 60, .1), .45),
    ), ms=380),
    "MatDice": lambda: finish(mix(
        (generic, .9), (fit(pitch(generic, 1.3), 120), .5), (plate, .25), (thump(200, 90, .05), .2),
    ), ms=300),
    "MatSpring": lambda: finish(mix(
        (tin, .8), (fit(metal_l, 300), .45), (ring([1760, 2794, 4186], .4, .12), .18), (thump(150, 70, .07), .3),
    ), ms=420),
    # crisp and soft things
    "MatCrisp": lambda: finish(mix(
        (chop, 1.0), (fit(pitch(chop, 1.35), 160), .45), (filt(knife, "highpass", 2500), .3), (thump(160, 70, .07), .3),
    ), ms=280),
    "MatBread": lambda: finish(mix(
        (filt(chop, "lowpass", 3500), .8), (soft, .7), (swish(.08, 5000, 1500), .25), (thump(120, 60, .09), .4),
    ), ms=300),
    "MatSplash": lambda: finish(mix(
        (filt(chop, "lowpass", 5000), .6), (soft, .8), (punch, .35), (swish(.12, 4000, 900), .3),
    ), ms=360),
    "MatSoft": lambda: finish(mix((filt(chop, "lowpass", 4000), .7), (soft, .8), (thump(120, 60, .08), .35)), ms=260),
    "MatJuicy": lambda: finish(mix((soft, .8), (punch, .45), (swish(.1, 4500, 1000), .3)), ms=320),
    "MatClack": lambda: finish(mix((wood_h, .8), (generic, .5), (thump(100, 50, .1), .4)), ms=320),
    # hold chopping, every 16th note (owner: "シャクシャク / ズバズバ"): short, punchy, two pitches alternate
    "ChopCrisp": lambda: finish(mix((fit(chop, 110), 1.0), (filt(knife, "highpass", 3000), .25), (thump(170, 80, .05), .25)), ms=120),
    "ChopWood": lambda: finish(mix((fit(plank, 110), .8), (fit(chop, 110), .5), (thump(140, 70, .05), .3)), ms=120),
    "ChopBread": lambda: finish(mix((filt(fit(chop, 110), "lowpass", 3200), .9), (fit(soft, 110), .5), (thump(120, 60, .05), .3)), ms=120),
    "ChopNoodle": lambda: finish(mix((fit(soft, 110), .7), (filt(fit(chop, 110), "lowpass", 2500), .6), (swish(.06, 3500, 900), .25)), ms=120),
    "ChopBlade": lambda: finish(mix((fit(filt(knife, "highpass", 1500), 100), .8), (swish(.06), .4), (thump(150, 70, .05), .35)), ms=110),
    # round 22 weapons: a little signature sound on every swing (played quietly under the cut)
    "WeaponRoller": lambda: finish(mix(
        (filt(rng.standard_normal(int(SR * .12)) * np.exp(-t_axis(.12) / .03) * (rng.random(int(SR * .12)) > .55), "highpass", 1800), .9),
        (swish(.08, 6000, 2000), .4), (thump(220, 140, .04), .2),
    ), ms=150),
    "WeaponSaibashi": lambda: finish(mix(
        (fit(pitch(generic, 1.6), 60), 1.0), (np.concatenate([np.zeros(int(SR * .045)), fit(pitch(generic, 1.9), 60)]), .8),
        (swish(.07, 7000, 3000), .25),
    ), ms=140),
    "WeaponIce": lambda: finish(mix(
        (fit(pitch(glass_l, 1.5), 200), .7), (ring([3136, 4699, 6272, 8372], .35, .09), .3), (sparkle(.3, 10), .35), (swish(.1, 8000, 3500), .35),
    ), ms=350),
    "WeaponLasso": lambda: finish(mix(
        (swish(.16, 3000, 600), .8),
        (np.concatenate([np.zeros(int(SR * .09)), filt(rng.standard_normal(int(SR * .03)) * np.exp(-t_axis(.03) / .006), "highpass", 2500)]), 1.0),
    ), ms=220),
    # round 24 weapons
    "WeaponCardboard": lambda: finish(mix((filt(soft, "lowpass", 2500), .9), (swish(.1, 3500, 900), .5), (fit(pitch(generic, .7), 80), .3)), ms=170),
    "WeaponSmoke": lambda: finish(mix((swish(.22, 2500, 400), 1.0), (thump(90, 45, .2), .5)), ms=260),
    # parrying a soldier "キン" (used by the soldiers, round 17)
    "ParryClang": lambda: finish(mix(
        (metal_l, .8), (ring([2489, 3729, 5588, 7459], .45, .1), .3), (fit(metal_h, 120), .35), (sparkle(.15, 6), .2),
    ), ms=450),
    "ParryFinish": lambda: finish(mix(
        (metal_h, .8), (fit(bell, 400), .5), (ring([1864, 2794, 4186, 5588], .7, .2), .3), (thump(90, 40, .2), .7), (fit(mining, 400), .3),
    ), ms=700),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(PREVIEW, exist_ok=True)
    rendered = {}
    for name, make in SOUNDS.items():
        x = make()
        rendered[name] = x
        sf.write(os.path.join(OUT, name + ".wav"), x.astype(np.float32), SR, subtype="PCM_16")
        print(f"{name:12s} {len(x) / SR * 1000:5.0f} ms")
    # listening files: each cut as heard in the game (blade + material), and hold chopping at 140 BPM 16ths
    def seq(names, gap):
        out = np.zeros(int(SR * (gap * len(names) + .8)))
        for i, n in enumerate(names):
            parts = [rendered[k] for k in n.split("+")]
            at = int(SR * gap * i)
            for p in parts:
                out[at:at + len(p)] += p * (.62 if len(parts) > 1 else 1)
        return out / max(1, np.max(np.abs(out))) * .9
    sixteenth = 60 / 140 / 4
    sf.write(os.path.join(PREVIEW, "01_cuts_normal.wav"), seq(["Blade+MatCrisp", "Blade+MatBread", "Blade+MatSplash", "Blade+MatSoft", "Blade+MatJuicy"], .6), SR)
    sf.write(os.path.join(PREVIEW, "02_cuts_hard.wav"), seq(["Blade+MatCrystal", "Blade+MatMetal", "Blade+MatWood", "Blade+MatDice", "Blade+MatSpring"], .8), SR)
    sf.write(os.path.join(PREVIEW, "03_hold_chop.wav"), seq(["ChopCrisp"] * 16 + ["ChopWood"] * 16 + ["ChopBread"] * 16 + ["ChopNoodle"] * 16, sixteenth), SR)
    sf.write(os.path.join(PREVIEW, "05_weapons.wav"), seq(["Blade+WeaponRoller", "Blade+WeaponSaibashi", "Blade+WeaponIce", "Blade+WeaponLasso", "Blade+WeaponCardboard", "Blade+WeaponSmoke"], .7), SR)
    sf.write(os.path.join(PREVIEW, "04_parry.wav"), seq(["ParryClang", "ParryClang", "ParryClang", "ParryFinish"], 60 / 140), SR)
    print("wrote", OUT, "and", PREVIEW)


if __name__ == "__main__":
    main()
