"""Round-19 sprites (owner 2026-10-15): "おとっつぁん", a cute little old-dad character.

Comb-over ("barcode") hair with one single hair standing up on the crown, a white T-shirt and a yellow haramaki belly band.
First a front-view design card, then every pose redrawn from the matching hamster pose (as round 5 / 16 / 18).
Usage: python tools/art/generate_round19.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round5 import POSES

RAW, STYLE = g.RAW, g.STYLE
OTOTTSAN = ("the cute chibi old dad 'Otottsan' from the second reference image: a small round friendly middle-aged Japanese man with a big head, "
            "a bald shiny head with a thin comb-over of a few parallel strands of black hair combed across the top (barcode hair) and ONE single "
            "hair standing straight up from the very top of the head, kind droopy eyes, rosy cheeks, a little round nose, a small moustache, "
            "a plain white T-shirt, a bright yellow haramaki belly band around the round belly, brown short trousers and sandals, "
            "a brown adventurer belt with a small pouch worn over the haramaki. He wears NO scarf")

JOBS = {
    "card_otottsan": ("Character design card for a mobile rhythm game hero, same chunky toy-like proportions as the hamster hero of the attached model sheet "
                      "(big head, short round body, short legs): 'Otottsan', a cute chibi friendly middle-aged Japanese dad. Bald shiny head with a thin "
                      "comb-over of a few parallel strands of black hair combed across the top (barcode hair) and ONE single hair standing straight up "
                      "from the very top of the head, kind droopy eyes, rosy cheeks, a little round nose, a small moustache, a warm grin. A plain white "
                      "T-shirt, a bright yellow haramaki belly band around his round belly, brown short trousers, sandals, a brown adventurer belt with a "
                      f"small pouch worn over the haramaki. No scarf. Front view facing the camera, full body, centered, empty hands. {STYLE}",
                      [g.ART / "03_hamster_model_sheet.png", RAW / "card_shiba.png"], True, "1024x1024"),
}
for pose in POSES:
    size = "1536x1024" if pose == "cutin_fever" else "1024x1024"
    JOBS[f"otottsan_{pose}"] = (
        f"Redraw the FIRST attached image with a different character: {OTOTTSAN}. Keep EXACTLY the same pose, body orientation, camera angle, "
        f"framing, scale, limb positions and gesture as the first image (same view: if the first image is a back view, this is a back view too). "
        f"Any empty clenched fist stays an empty clenched fist (the weapon is drawn separately; no weapon in the hands) unless the first image shows a weapon. "
        f"Keep the character design and colors of the second image. {STYLE}",
        [RAW / f"hamster_{pose}.png", RAW / "card_otottsan.png"], True, size)


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    if "card_otottsan" in names:
        names.remove("card_otottsan")
        print(g.run("card_otottsan")[:2], flush=True)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
