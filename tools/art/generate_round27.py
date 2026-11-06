"""Round-27 sprites (owner 2026-10-18): the giant item (replaces the auto-cutter drone), a big melon that splits in two,
and a bomb you must not cut.

Usage: python tools/art/generate_round27.py [name ...]   (no names = every missing asset)
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round3 import CAPSULE, SHEET

RAW, STYLE = g.RAW, g.STYLE
MELON = ("a big round cantaloupe melon (muskmelon) with a pale green-beige netted rind pattern, about twice the size of a normal fruit, "
         "with a bright glowing orange crack running down its middle from top to bottom (it is about to split in two)")

JOBS = {
    "item_giant": (f"A cute golden mushroom with big white spots and a tiny upward arrow, the classic grow-big power-up, {CAPSULE} "
                   f"Warm orange bubble. {STYLE}", SHEET, True, "1024x1024"),
    "melon_big": (f"Game note object: {MELON}, slight three-quarter view, glossy highlight, centered, nothing else. {STYLE}",
                  g.OBJ_REF, True, "1024x1024"),
    "melon_half": ("Game note object: one half of the same cantaloupe melon cut in half vertically, standing on its round rind side with "
                   "the cut face turned toward the viewer: juicy bright orange flesh, a hollow of pale seeds in the middle, a pale green "
                   f"netted rind edge, glossy, centered, nothing else. {STYLE}", [RAW / "melon_big.png"], True, "1024x1024"),
    "melon_half_split": ("The same melon half from the attached image cut again into two quarter wedges, the two wedges separated with a "
                         "clear gap (left and right, not touching), orange flesh on the cut faces, netted rind, centered, nothing else. "
                         f"{STYLE}", [RAW / "melon_half.png"], True, "1024x1024"),
    "bomb": ("Game note object: a classic round black cartoon bomb with a glossy shine, a short metal cap on top and a lit fuse burning "
             "with bright yellow-orange sparks, a little angry face with white eyes, slight three-quarter view, unmistakably dangerous, "
             f"centered, nothing else. {STYLE}", g.OBJ_REF, True, "1024x1024"),
    "vfx_explosion": ("A cartoon explosion burst for a mobile game: a round puff of bright yellow and orange fire with dark grey smoke "
                      "clouds around the edge and a white-hot centre, comic style, game VFX sprite, centered, nothing else. "
                      f"{STYLE}", None, True, "1024x1024"),
}
# the halves are drawn from the big melon, the quarters from a half
ORDER = [["item_giant", "melon_big", "bomb", "vfx_explosion"], ["melon_half"], ["melon_half_split"]]


def main():
    g.JOBS.update(JOBS)
    wanted = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    for group in ORDER:
        names = [n for n in group if n in wanted]
        with cf.ThreadPoolExecutor(max_workers=4) as pool:
            for name, ok, tail in pool.map(g.run, names):
                print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
