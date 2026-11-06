"""Round-18 sprites (owner 2026-10-15: "キャラ追加。柴犬。"): the Shiba Inu character.

First a front-view design card, then every pose redrawn from the matching hamster pose (as round 5 / round 16),
so animation, weapon grip and timing line up.
Usage: python tools/art/generate_round18.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round5 import POSES

RAW, STYLE = g.RAW, g.STYLE
SHIBA = ("the Shiba Inu runner from the second reference image: a chubby little Shiba Inu dog standing upright on two legs, "
         "fluffy orange-tan fur with a cream white muzzle, chest, belly and paw tips, cream eyebrow dots, pointed triangular ears, "
         "a curled fluffy tail over the back, happy smiling face, a green scarf and a brown adventurer belt with a small pouch. "
         "The Shiba wears NO red scarf")

JOBS = {
    "card_shiba": ("Character design card for a mobile rhythm game hero: a chubby little Shiba Inu dog standing upright on two legs like the "
                   "hamster hero of the attached model sheet (same proportions: big head, short body, short legs), fluffy orange-tan fur with "
                   "a cream white muzzle, chest, belly and paw tips, cream eyebrow dots, pointed triangular ears, a curled fluffy tail, "
                   "happy confident smile, a green scarf tied around the neck and a brown adventurer belt with a small pouch. "
                   f"Front view facing the camera, full body, centered, empty paws. {STYLE}",
                   [g.ART / "03_hamster_model_sheet.png", RAW / "card_penguin.png"], True, "1024x1024"),
}
for pose in POSES:
    size = "1536x1024" if pose == "cutin_fever" else "1024x1024"
    JOBS[f"shiba_{pose}"] = (
        f"Redraw the FIRST attached image with a different character: {SHIBA}. Keep EXACTLY the same pose, body orientation, camera angle, "
        f"framing, scale, limb positions and gesture as the first image (same view: if the first image is a back view, this is a back view too). "
        f"Any empty clenched fist stays an empty clenched paw (the weapon is drawn separately; no weapon in the paws) unless the first image shows a weapon. "
        f"Keep the character design and colors of the second image. {STYLE}",
        [RAW / f"hamster_{pose}.png", RAW / "card_shiba.png"], True, size)


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    if "card_shiba" in names:
        names.remove("card_shiba")
        print(g.run("card_shiba")[:2], flush=True)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
