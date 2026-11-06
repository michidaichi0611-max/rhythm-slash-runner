"""Round-3 sprites (owner 2026-10-12): power-up items, boss gimmicks, roadside posts for speed.

Usage: python tools/art/generate_round3.py [name ...]   (no names = every missing asset)
"""
import concurrent.futures as cf
import sys

import generate_assets as g

RAW, STYLE, ROOT = g.RAW, g.STYLE, g.ROOT
SHEET = [ROOT / "review" / "concepts2" / "items_sheet.png"]
CAPSULE = "inside a glossy transparent glowing capsule bubble with sparkles, game power-up item, centered, nothing else, same design as the matching item in the attached sheet."

JOBS = {
    "item_booster": (f"A red-and-white toy rocket booster with a flame, {CAPSULE} Warm golden bubble. {STYLE}", SHEET, True, "1024x1024"),
    "item_auto": (f"A small cute round white flying robot drone with cyan smiling eyes and a spinning blade rotor, {CAPSULE} Blue bubble. {STYLE}", SHEET, True, "1024x1024"),
    "item_barrier": (f"A cyan hexagon-patterned shield sphere with a glowing white star in the middle, {CAPSULE} Light cyan bubble. {STYLE}", SHEET, True, "1024x1024"),
    "rocket_pack": (f"A small red-and-white toy rocket backpack with two short nozzles, seen exactly from behind (back view), as worn on the back of a small character, no character, no flame, centered, nothing else. {STYLE}", SHEET, True, "1024x1024"),
    "drone_cutter": (f"A small cute round white flying robot drone with cyan smiling eyes, a spinning blade rotor on top and a tiny glowing cyan blade arm, three-quarter view, same design as the drone in the attached sheet, nothing else. {STYLE}", SHEET, True, "1024x1024"),
    "vfx_barrier": ("A translucent cyan hexagon-patterned bubble shield dome, glowing edges, mostly transparent in the middle, game VFX sprite, centered, nothing else.", None, True, "1024x1024"),
    "shot_charge": ("A huge crackling purple-and-magenta energy ball with dark core, lightning arcs and glowing aura, boss charge attack projectile, game VFX sprite, nothing else.", None, True, "1024x1024"),
    "shot_gold": ("A glowing golden orb projectile with a bright star core and sparkles, weak-point bonus target, game VFX object, nothing else. " + STYLE, None, True, "1024x1024"),
    "side_lamp": ("A tall slim decorative street lamp post with a warm glowing lantern on top, standing upright, front view, whole post visible from base to top, nothing else. " + STYLE, None, True, "1024x1536"),
    "side_banner": ("A tall slim wooden pole with a long colorful triangular pennant flag fluttering, standing upright, front view, whole pole visible from base to top, nothing else. " + STYLE, None, True, "1024x1536"),
}


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
