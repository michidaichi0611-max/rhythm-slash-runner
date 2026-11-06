"""Round-25 characters (owner 2026-10-15): a long-haired pure white hamster, and a shark made of jelly.
Design card first for each, then every pose redrawn from the matching hamster pose.
Usage: python tools/art/generate_round25.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round5 import POSES

RAW, STYLE = g.RAW, g.STYLE
CHARS = {
    "whamster": ("a long-haired pure white hamster (angora hamster): very long silky fluffy snow-white fur flowing down like a cape, pink ears, "
                 "dark shiny eyes, pink nose, a light blue scarf and a brown adventurer belt with a small pouch, standing upright on two legs",
                 "The hamster wears NO red scarf"),
    "shark": ("a cute chubby shark made entirely of translucent glossy blue jelly (gummy jelly), you can see light through its wobbly jelly body, "
              "small bubbles inside, a dorsal fin, a tail fin, little round eyes, a toothy happy grin with soft jelly teeth, standing upright on two "
              "short legs, a small yellow scarf and a brown adventurer belt with a small pouch",
              "No red scarf"),
}
JOBS = {}
for key, (desc, note) in CHARS.items():
    JOBS[f"card_{key}"] = (f"Character design card for a mobile rhythm game hero, same chunky proportions as the hamster hero of the attached model sheet "
                           f"(big head, short round body, short legs): {desc}. Front view facing the camera, full body, centered, empty hands. {STYLE}",
                           [g.ART / "03_hamster_model_sheet.png"], True, "1024x1024")
    for pose in POSES:
        size = "1536x1024" if pose == "cutin_fever" else "1024x1024"
        JOBS[f"{key}_{pose}"] = (
            f"Redraw the FIRST attached image with a different character: the character from the second reference image, {desc}. {note}. "
            f"Keep EXACTLY the same pose, body orientation, camera angle, framing, scale, limb positions and gesture as the first image (same view: "
            f"if the first image is a back view, this is a back view too). Any empty clenched fist stays an empty clenched fist (the weapon is drawn "
            f"separately; nothing in the hands) unless the first image shows a weapon. Keep the design and colors of the second image. {STYLE}",
            [RAW / f"hamster_{pose}.png", RAW / f"card_{key}.png"], True, size)


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    cards = [n for n in names if n.startswith("card_")]
    for n in cards:
        names.remove(n)
        print(g.run(n)[:2], flush=True)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}", flush=True)


if __name__ == "__main__":
    main()
