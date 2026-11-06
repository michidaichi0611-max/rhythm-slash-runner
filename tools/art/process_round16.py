"""Round 16: the candy rope's cross-section (slices of the slide that replaced the toy train)."""
from PIL import Image
import os
SRC = os.path.join(os.path.dirname(__file__), "round16")
DST = os.path.join(os.path.dirname(__file__), "..", "..", "unity_project", "Assets", "RhythmRunner", "Resources", "Art")
im = Image.open(os.path.join(SRC, "hold_candy_slice.png")).convert("RGBA")
im = im.crop(im.split()[3].point(lambda v: 255 if v > 12 else 0).getbbox())
im.thumbnail((256, 256), Image.LANCZOS)
im.save(os.path.join(DST, "hold_candy_slice.png"), optimize=True)
print("hold_candy_slice", im.size)
