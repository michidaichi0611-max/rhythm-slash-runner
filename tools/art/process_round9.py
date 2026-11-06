"""Round 9: heart capsule item -> Resources/Art/item_heart.png (trimmed, same size class as the other item capsules)."""
from PIL import Image
import os
SRC = os.path.join(os.path.dirname(__file__), "round9")
DST = os.path.join(os.path.dirname(__file__), "..", "..", "unity_project", "Assets", "RhythmRunner", "Resources", "Art")
ref = Image.open(os.path.join(DST, "item_barrier.png"))
im = Image.open(os.path.join(SRC, "item_heart.png")).convert("RGBA")
im = im.crop(im.split()[3].point(lambda v: 255 if v > 12 else 0).getbbox())
im.thumbnail(ref.size, Image.LANCZOS)
im.save(os.path.join(DST, "item_heart.png"), optimize=True)
print("heart", im.size, "ref", ref.size)

# the bubble came out blue like the barrier: turn the blue/cyan parts pink so the two capsules never get mixed up
import colorsys
px = im.load()
for y in range(im.height):
    for x in range(im.width):
        r, g, b, a = px[x, y]
        if a == 0: continue
        h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
        if .42 < h < .75:  # cyan..blue
            h = .93 + (h - .58) * .3
            r2, g2, b2 = colorsys.hsv_to_rgb(h % 1, min(1, s * .95), v)
            px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
im.save(os.path.join(DST, "item_heart.png"), optimize=True)
print("recoloured")
