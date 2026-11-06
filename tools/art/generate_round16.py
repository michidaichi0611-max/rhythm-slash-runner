"""Round-16 sprites (owner 2026-10-14): the penguin character (chosen from the round-15 concepts) and the Count Donut boss.

Like round 5, every character pose is redrawn from the matching hamster pose so animation, weapon grip and timing line up.
Usage: python tools/art/generate_round16.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round5 import POSES, BOSS_POSES

RAW, STYLE = g.RAW, g.STYLE
PENGUIN = ("the penguin runner from the second reference image: chubby round penguin with glossy black back and head, soft white belly, "
           "orange beak and orange webbed feet, rosy cheeks, a blue scarf and a brown adventurer belt with a small pouch. "
           "The penguin wears NO red scarf")

JOBS = {}
for pose in POSES:
    size = "1536x1024" if pose == "cutin_fever" else "1024x1024"
    JOBS[f"penguin_{pose}"] = (
        f"Redraw the FIRST attached image with a different character: {PENGUIN}. Keep EXACTLY the same pose, body orientation, camera angle, "
        f"framing, scale, limb positions and gesture as the first image (same view: if the first image is a back view, this is a back view too). "
        f"Any empty clenched fist stays an empty clenched flipper (the weapon is drawn separately; no weapon in the flippers) unless the first image shows a weapon. "
        f"Keep the character design and colors of the second image. {STYLE}",
        [RAW / f"hamster_{pose}.png", RAW / "card_penguin.png"], True, size)

JOBS["boss_donut"] = (
    "The same boss character as the attached concept image: 'Count Donut', a giant glazed pink donut vampire count with rainbow sprinkles, "
    "a tall dark red-and-black collar cape, small fangs, a gold monocle, white gloves, holding a cane, black shoes. Front view facing the camera, "
    f"full body, menacing but cute, compact silhouette. {STYLE}",
    [RAW / "concept_boss_donut.png"], True, "1024x1024")
for pose, desc in BOSS_POSES.items():
    JOBS[f"boss_donut_{pose}"] = (
        f"The same boss character as the attached image, same design, colors and proportions, front view facing the camera, full body, now {desc}. {STYLE}",
        [RAW / "boss_donut.png"], True, "1024x1024")
JOBS["shot_donut"] = (
    "Game projectile sprite: a small glossy pink frosted mini donut with rainbow sprinkles, spinning, with a faint pink magic glow. "
    f"Centered, compact, front view. {STYLE}", [RAW / "boss_donut.png"], True, "1024x1024")


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    # the boss base first (its poses and shot use it as reference)
    if "boss_donut" in names:
        names.remove("boss_donut")
        print(g.run("boss_donut")[:2], flush=True)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
