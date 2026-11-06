"""Round 12: the seamless crust tile for the 3D baguette (512 px, repeating; never stretched over the object)."""
from PIL import Image
import os
SRC = os.path.join(os.path.dirname(__file__), "round12")
DST = os.path.join(os.path.dirname(__file__), "..", "..", "unity_project", "Assets", "RhythmRunner", "Resources", "Art")
im = Image.open(os.path.join(SRC, "crust_tile.png")).convert("RGB").resize((512, 512), Image.LANCZOS)
im.save(os.path.join(DST, "tile_crust.png"), optimize=True)
print("tile_crust", im.size)

# the generated crust reads greyish once lit and hazed in the scene: warm it up and lift it a little
from PIL import ImageEnhance
im = Image.open(os.path.join(DST, "tile_crust.png")).convert("RGB")
im = ImageEnhance.Color(im).enhance(1.35)
im = ImageEnhance.Brightness(im).enhance(1.12)
im.save(os.path.join(DST, "tile_crust.png"), optimize=True)
print("tile_crust warmed")

# round 16: noodle and leek surfaces (tiled along the 3D objects). The leek tile keeps only its fibres (grey) so the
# object's own colours (white root -> green leaves) still show through.
from PIL import ImageOps
im = Image.open(os.path.join(SRC, "noodle_tile.png")).convert("RGB").resize((512, 512), Image.LANCZOS)
im.save(os.path.join(DST, "tile_noodle.png"), optimize=True)
im = Image.open(os.path.join(SRC, "leek_tile.png")).convert("L").resize((512, 512), Image.LANCZOS)
im = ImageOps.autocontrast(im, cutoff=1).point(lambda v: int(185 + v * 70 / 255))
im.convert("RGB").save(os.path.join(DST, "tile_leek.png"), optimize=True)
print("tile_noodle, tile_leek")
