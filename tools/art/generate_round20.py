"""Round-20 sprites (owner 2026-10-15): the invisible man ("透明人間").

A chibi figure with no face at all (no eyes, no mouth): just a hazy pale silhouette with a soft glowing rim. It is drawn
opaque here; process_assets.py turns the body see-through (toumei_* sprites) so the stage shows through it.
Usage: python tools/art/generate_round20.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round5 import POSES

RAW, STYLE = g.RAW, g.STYLE
TOUMEI = ("the invisible man from the second reference image: a chubby chibi humanoid figure with the same big round head and short round body, "
          "but with NO face at all (no eyes, no mouth, no nose, no hair, no clothes): the whole body is a smooth hazy ghostly silhouette of pale "
          "icy white-blue frosted mist, lighter and glowing softly at the outline, slightly darker and misty inside, plain uniform surface")
TOUMEI_STYLE = ("Cute chunky toy-like 3D render for a premium mobile rhythm game, soft studio lighting, clean bold silhouette readable at small size. "
                "No text, no watermark.")

JOBS = {
    "card_toumei": ("Character design card for a mobile rhythm game hero, same chunky toy-like proportions as the hamster hero of the attached model sheet "
                    "(big round head, short round body, short arms and legs): a cute chibi invisible man. He has NO face at all (no eyes, no mouth, "
                    "no nose, no hair) and wears nothing: the whole body is a smooth hazy ghostly silhouette of pale icy white-blue frosted mist, "
                    "glowing softly at the outline and a little darker and misty inside, plain uniform surface. Friendly cheerful pose, one hand waving. "
                    f"Front view facing the camera, full body, centered, empty hands. {TOUMEI_STYLE}",
                    [g.ART / "03_hamster_model_sheet.png"], True, "1024x1024"),
}
for pose in POSES:
    size = "1536x1024" if pose == "cutin_fever" else "1024x1024"
    JOBS[f"toumei_{pose}"] = (
        f"Redraw the FIRST attached image with a different character: {TOUMEI}. Keep EXACTLY the same pose, body orientation, camera angle, "
        f"framing, scale, limb positions and gesture as the first image (same view: if the first image is a back view, this is a back view too). "
        f"Any empty clenched fist stays an empty clenched fist (the weapon is drawn separately; no weapon in the hands) unless the first image shows a weapon. "
        f"No scarf, no belt, no clothes, no face. Keep the look of the second image. {TOUMEI_STYLE}",
        [RAW / f"hamster_{pose}.png", RAW / "card_toumei.png"], True, size)


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    if "card_toumei" in names:
        names.remove("card_toumei")
        print(g.run("card_toumei")[:2], flush=True)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}", flush=True)


if __name__ == "__main__":
    main()
