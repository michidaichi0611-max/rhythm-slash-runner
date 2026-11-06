"""Round-22 weapons (owner 2026-10-15): tape lint roller, saibashi (long cooking chopsticks), ice claw, cowboy lasso.

Each weapon is drawn perfectly vertical (tip up, grip at the bottom) like the first three, plus its own slash trail.
Usage: python tools/art/generate_round22.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round1 import SLASH

STYLE = g.STYLE
W = "perfectly vertical, tip pointing straight up, handle at the bottom. Only the weapon, centered."
JOBS = {
    "weapon_roller": (f"A single toy-like sticky tape lint roller (a cleaning roller used on carpets and clothes): a white roll of sticky tape on a "
                      f"pastel blue plastic frame with a long blue handle, a few bits of lint and dust stuck on the tape, used as a comedic sword, {W} {STYLE}",
                      g.OBJ_REF, True, "1024x1024"),
    "weapon_saibashi": (f"A single pair of very long Japanese wooden cooking chopsticks (saibashi), light bamboo wood, the two sticks held together side by side, "
                        f"tied at the top end with a short string, used as a comedic sword, {W} {STYLE}", g.OBJ_REF, True, "1024x1024"),
    "weapon_ice_claw": (f"A single fantasy ice claw weapon: three long curved claw blades of clear glowing blue ice crystal rising from a frosted silver "
                        f"knuckle grip, frost mist and tiny ice sparkles, {W} {STYLE}", g.OBJ_REF, True, "1024x1024"),
    "weapon_lasso": (f"A single cowboy lasso weapon: a coiled brown rope with a big open loop at the top and a leather-wrapped grip at the bottom, "
                     f"the loop standing upright like a blade, {W} {STYLE}", g.OBJ_REF, True, "1024x1024"),
    "vfx_trail_tape": ("A curved slash trail swoosh made of a wide ribbon of sticky white tape with a few tiny lint bits and dust specks stuck on it, "
                       "pale blue glow at the edges, same crescent shape as the reference, game VFX sprite, nothing else.", [SLASH], True, "1536x1024"),
    "vfx_trail_saibashi": ("A curved slash trail swoosh made of two thin parallel streaks in warm bamboo wood color with a few tiny sparkles, "
                           "same crescent shape as the reference, game VFX sprite, nothing else.", [SLASH], True, "1536x1024"),
    "vfx_trail_ice": ("A curved slash trail made of three parallel icy blue claw streaks with frost crystals and tiny ice shards, "
                      "same crescent shape as the reference, game VFX sprite, nothing else.", [SLASH], True, "1536x1024"),
    "vfx_trail_lasso": ("A curved slash trail swoosh made of a whipping brown rope loop with motion lines and a little dust, "
                        "same crescent shape as the reference, game VFX sprite, nothing else.", [SLASH], True, "1536x1024"),
}


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (g.RAW / f"{n}.png").exists()]
    with cf.ThreadPoolExecutor(max_workers=4) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}", flush=True)


if __name__ == "__main__":
    main()
