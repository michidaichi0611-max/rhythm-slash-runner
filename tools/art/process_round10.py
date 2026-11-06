"""Round 10: the third boss (Soft-Serve General, ソフトクリーム将軍) -> Resources/Art.
Poses are trimmed and scaled like the other bosses (768 px wide at most); the shot like the other shots (320 px tall)."""
from PIL import Image
import os
SRC = os.path.join(os.path.dirname(__file__), "round10")
DST = os.path.join(os.path.dirname(__file__), "..", "..", "unity_project", "Assets", "RhythmRunner", "Resources", "Art")

def clean(im):
    im = im.convert("RGBA")
    im = im.crop(im.split()[3].point(lambda v: 255 if v > 12 else 0).getbbox())
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            if px[x, y][3] <= 12: px[x, y] = (0, 0, 0, 0)
    return im

for name in ("boss_icecream", "boss_icecream_attack", "boss_icecream_hurt", "boss_icecream_rage"):
    p = os.path.join(SRC, name + ".png")
    if not os.path.exists(p): print("missing", name); continue
    im = clean(Image.open(p))
    im.thumbnail((768, 768), Image.LANCZOS)
    im.save(os.path.join(DST, name + ".png"), optimize=True)
    print(name, im.size)
p = os.path.join(SRC, "shot_icecream.png")
if os.path.exists(p):
    im = clean(Image.open(p))
    im = im.resize((round(im.width * 320 / im.height), 320), Image.LANCZOS)
    im.save(os.path.join(DST, "shot_icecream.png"), optimize=True)
    print("shot", im.size)
