"""Round-4 sprites (owner 2026-10-12): side walls that scroll past, so the scenery itself moves.

Each wall is a horizontally tileable side view; process_assets.py blends its ends so it wraps without a seam.
Usage: python tools/art/generate_round4.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g

RAW, STYLE = g.RAW, g.STYLE
WALL = ("Seamless horizontally tileable side-view wall strip for a game corridor, flat straight-on view (no perspective), "
        "the left and right edges must continue into each other, objects evenly spread across the whole width, warm soft lighting, "
        "no text, no characters. Content: {}. " + STYLE)

THEMES = {
    "kitchen": ("a long wooden kitchen shelf counter with big glass jars, cheese wedges, tomatoes, herb pots, bottles and wooden crates", "kitchen_backdrop"),
    "toy_room": ("a wall of stacked colorful toy blocks, plush bears, toy cars and balls on low shelves", "stage_bg_toy_room"),
    "sky_orchard": ("fluffy white clouds with small peach trees and floating grassy rocks", "stage_bg_sky_orchard"),
    "neon_market": ("a row of night market food stalls with glowing lanterns and colorful awnings, no readable text", "stage_bg_neon_market"),
    "candy_land": ("a wall of frosting, giant lollipops, cupcakes and candy canes with sprinkles", "stage_bg_candy_land"),
    "freezer": ("frosty freezer shelves with ice cream tubs, ice blocks and icicles, cold blue light", "stage_bg_freezer"),
}
JOBS = {f"wall_{k}": (WALL.format(desc), [RAW / f"{ref}.png"], False, "1536x1024") for k, (desc, ref) in THEMES.items()}


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
