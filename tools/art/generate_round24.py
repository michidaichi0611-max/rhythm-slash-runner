"""Round-24 weapons (owner 2026-10-15): cardboard sword, smoke baseball bat (each with its own slash trail).
Usage: python tools/art/generate_round24.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round1 import SLASH

STYLE = g.STYLE
W = "perfectly vertical, tip pointing straight up, handle at the bottom. Only the weapon, centered."
JOBS = {
    "weapon_cardboard": (f"A single handmade toy sword cut out of brown corrugated cardboard, visible corrugated edges, packing tape wrapped around the "
                         f"grip, a cardboard cross guard, a little marker doodle on the blade, {W} {STYLE}", g.OBJ_REF, True, "1024x1024"),
    "weapon_smoke_bat": (f"A single baseball bat made entirely of swirling grey-white smoke, soft billowing wisps trailing off it, the bat shape still clear, "
                         f"a wrapped grip at the bottom, {W} {STYLE}", g.OBJ_REF, True, "1024x1024"),
    "vfx_trail_cardboard": ("A curved slash trail swoosh in brown cardboard color with a few tiny cardboard scraps and tape bits flying, "
                            "same crescent shape as the reference, game VFX sprite, nothing else.", [SLASH], True, "1536x1024"),
    "vfx_trail_smoke": ("A curved slash trail made of soft swirling grey-white smoke puffs, "
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
