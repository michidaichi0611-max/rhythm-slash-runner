"""Round 11: slide objects (ramen, toy train) as 640 px tall strips, and the pieces that fly off while they are followed."""
from PIL import Image
import os
SRC = os.path.join(os.path.dirname(__file__), "round11")
DST = os.path.join(os.path.dirname(__file__), "..", "..", "unity_project", "Assets", "RhythmRunner", "Resources", "Art")

def clean(im):
    im = im.convert("RGBA")
    im = im.crop(im.split()[3].point(lambda v: 255 if v > 12 else 0).getbbox())
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            if px[x, y][3] <= 12: px[x, y] = (0, 0, 0, 0)
    return im

for name in ("hold_ramen", "hold_train"):
    im = clean(Image.open(os.path.join(SRC, name + ".png")))
    im = im.resize((max(8, round(im.width * 640 / im.height)), 640), Image.LANCZOS)
    im.save(os.path.join(DST, name + ".png"), optimize=True); print(name, im.size)
for name in ("hold_ramen_slice", "hold_train_slice"):
    im = clean(Image.open(os.path.join(SRC, name + ".png")))
    im.thumbnail((256, 256), Image.LANCZOS)
    im.save(os.path.join(DST, name + ".png"), optimize=True); print(name, im.size)
