"""Round-21 sprites (owner 2026-10-15): an old grandfather bird in a cool, restrained Japanese style, painted entirely in sumi ink.
(Replaces the low-poly dot turtle the owner dropped.)

Every pose redrawn from the matching hamster pose (as round 5 / 16 / 18-20).
Usage: python tools/art/generate_round21.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round5 import POSES

RAW = g.RAW
INK_STYLE = ("Japanese sumi-e ink wash painting: the whole character is painted ONLY with black sumi ink brush strokes on nothing (transparent "
             "background), confident expressive dry-brush and wet-wash strokes, shades of black and grey ink only, no other colours, "
             "elegant, cool, restrained and dignified (shibui) wabi-sabi taste, clean silhouette readable at small size. No text, no seal stamp, no watermark.")
TORI = ("the old grandfather bird from the second reference image: a chubby little old bird standing upright on two legs, a wise elderly kite/hawk-like "
        "bird with long drooping white eyebrow feathers, a long wispy white beard of feathers under the hooked beak, calm narrowed eyes, "
        "a short haori jacket over the round body, small wing-hands, thin bird legs, all painted in sumi ink")

JOBS = {
    "card_tori": ("Character design card for a mobile rhythm game hero, same chunky proportions as the hamster hero of the attached model sheet "
                  "(big head, short round body, short legs): a cute but cool old grandfather bird standing upright on two legs, a wise elderly "
                  "kite/hawk-like bird with long drooping white eyebrow feathers, a long wispy white beard of feathers under the hooked beak, calm "
                  "narrowed eyes, a short haori jacket over his round body, small wing-hands, thin bird legs. "
                  f"Front view facing the camera, full body, centered, empty wing-hands. {INK_STYLE}",
                  [g.ART / "03_hamster_model_sheet.png"], True, "1024x1024"),
}
for pose in POSES:
    size = "1536x1024" if pose == "cutin_fever" else "1024x1024"
    JOBS[f"tori_{pose}"] = (
        f"Redraw the FIRST attached image with a different character: {TORI}. Keep EXACTLY the same pose, body orientation, camera angle, "
        f"framing, scale, limb positions and gesture as the first image (same view: if the first image is a back view, this is a back view too). "
        f"Any empty clenched fist stays an empty clenched wing-hand (the weapon is drawn separately; nothing in the hands) unless the first image shows a weapon "
        f"(then the weapon is painted in ink too). No scarf, no belt. Keep the ink-painting look of the second image. {INK_STYLE}",
        [RAW / f"hamster_{pose}.png", RAW / "card_tori.png"], True, size)


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    if "card_tori" in names:
        names.remove("card_tori")
        print(g.run("card_tori")[:2], flush=True)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}", flush=True)


if __name__ == "__main__":
    main()
