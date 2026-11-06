"""Round-32 art (owner 2026-10-19): the energy drink, a new app icon and the launch screen.

- item_drink: the energy drink (shop, plays line, dialogs)
- app_icon_v2: the store icon (opaque square, read at 48 px: one hamster, one cut, one strawberry)
- app_icon_fg: the Android adaptive icon foreground (transparent; the background is a plain colour)
- launch_bg: the launch screen behind the logo (portrait)
Usage: python tools/art/generate_round32.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g

RAW, STYLE, ART = g.RAW, g.STYLE, g.ART
HERO = [RAW / "hamster_front_cheer.png"]
JOBS = {
    "item_drink": ("Game item sprite: a cute chunky energy drink can, glossy cyan and orange can with a big yellow lightning bolt and a "
                   "little hamster paw print on it, a bendy straw, small sparkles and fizz bubbles around it. Front three-quarter view, "
                   f"centered, nothing else. {STYLE}", None, True, "1024x1024"),
    "app_icon_v2": ("Mobile game app icon, square, full bleed (no border, no rounded corners, no text): the golden hamster hero from the "
                    "attached image, close-up from the chest up, mid-swing with a glowing cyan energy blade, slicing a big shiny "
                    "strawberry clean in half right in front of it, juice droplets and a bright cyan slash arc, dynamic diagonal "
                    "composition, warm orange-to-magenta gradient background with soft light rays. Bold, very readable at small size. "
                    f"{STYLE}", HERO, False, "1024x1024"),
    "app_icon_fg": ("App icon foreground layer: the golden hamster hero from the attached image, head and shoulders, cheerful face, "
                    "holding a glowing cyan energy blade raised diagonally, a small strawberry cut in half beside it. Everything kept "
                    f"inside the central 60% of the square with empty margins all around. Transparent background. {STYLE}",
                    HERO, True, "1024x1024"),
    "launch_bg": ("Vertical launch screen illustration for a mobile rhythm game, portrait: a long sunny wooden road through a cosy "
                  "kitchen market lined with lanterns and colourful flags, stretching to a castle on the horizon, the golden hamster "
                  "hero from the attached image running toward the viewer in the lower third with a glowing cyan blade, strawberries "
                  "flying, motion lines, the upper third is open sky with soft light (room for the title). No text. "
                  f"{STYLE}", HERO, False, "1024x1536"),
}


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    with cf.ThreadPoolExecutor(max_workers=4) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail[-400:]}"), flush=True)


if __name__ == "__main__":
    main()
