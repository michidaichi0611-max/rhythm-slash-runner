"""Round 8: lengthwise cut faces of the hold objects -> Resources/Art/hold_<kind>_half.png (trimmed, 640 px tall)."""
from PIL import Image
import os
SRC = os.path.join(os.path.dirname(__file__), "round8")
DST = os.path.join(os.path.dirname(__file__), "..", "..", "unity_project", "Assets", "RhythmRunner", "Resources", "Art")
for kind in ("baguette", "leek", "pencil"):
    im = Image.open(os.path.join(SRC, "hold_%s_half.png" % kind)).convert("RGBA")
    a = im.split()[3].point(lambda v: 255 if v > 12 else 0)
    im = im.crop(a.getbbox())
    # transparent pixels keep the generator's glow colour; clear them so no halo bleeds in with bilinear filtering
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, al = px[x, y]
            if al <= 12: px[x, y] = (0, 0, 0, 0)
    h = 640
    im = im.resize((max(8, round(im.width * h / im.height)), h), Image.LANCZOS)
    im.save(os.path.join(DST, "hold_%s_half.png" % kind), optimize=True)
    print(kind, im.size)

# tapping hand for the boss prompts (mash / hold): trimmed, 256 px
im = Image.open(os.path.join(SRC, "ui_tap_hand.png")).convert("RGBA")
im = im.crop(im.split()[3].point(lambda v: 255 if v > 12 else 0).getbbox())
im.thumbnail((256, 256), Image.LANCZOS)
im.save(os.path.join(DST, "ui_tap_hand.png"), optimize=True)
print("hand", im.size)
