"""Turns art_generated/raw/*.png into game-ready sprites in unity_project/Assets/RhythmRunner/Resources/Art.

- trims transparent margins, removes the faint colored halo the generator leaves on alpha edges
- splits *_split images into *_split_l / *_split_r at the emptiest column near the middle
- resizes to the sizes the game draws at (keeps mobile memory low)
Usage: python tools/art/process_assets.py
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

from see_through import see_through

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "art_generated" / "raw"
OUT = ROOT / "unity_project" / "Assets" / "RhythmRunner" / "Resources" / "Art"

# name prefix -> max size of the longer side after trimming
SIZES = {
    "hamster_": 640, "strawberry_": 384, "watermelon_": 384, "barrel_": 384, "weapon_": 512, "prop_": 384,
    "vfx_": 512, "icon_": 160, "logo": 1024, "card_": 512, "stage_": 512,
    # round 2
    "crate_": 384, "gem_": 384, "dice_": 384, "clock_": 384, "duck_": 384, "pumpkin_": 512, "safe_": 512,
    "hold_": 640, "boss_": 768, "shot_": 320, "gate_": 768,
    # round 3
    "cat_": 640, "robot_": 640, "penguin_": 640, "shiba_": 640, "otottsan_": 640, "toumei_": 640, "tori_": 640, "whamster_": 640, "shark_": 640, "soldier_": 448,
    "item_": 320, "rocket_": 384, "drone_": 320, "side_": 640,
    # round 27: split melon, bomb
    "melon_": 448, "bomb": 384,
}
OPAQUE = {"launch_bg": (1080, 1620), "kitchen_backdrop": (1080, 1620), "key_visual": (1080, 1620), "wood_road_tile": (512, 512), "home_backdrop_empty": (1080, 1620), "kitchen_backdrop_fever": (1080, 1620)}


# generated earlier but no longer drawn (stage select and roadside food were removed in round 2): kept out of the build
UNUSED = {"app_icon_v2", "app_icon_fg", "card_runner", "stage_toy_room", "stage_sky_orchard", "stage_neon_market", "card_penguin", "concept_boss_donut", "card_shiba", "card_otottsan", "card_toumei", "card_tori", "card_whamster", "card_shark"}


def clean_alpha(img):
    a = np.asarray(img).astype(np.float32)
    alpha = a[..., 3]
    # Remap so the generator's faint halo (alpha < ~35) disappears and edges stay smooth.
    alpha = np.clip((alpha - 35) * 255 / 220, 0, 255)
    # Pull edge colors toward the nearest solid interior color to kill the yellow fringe.
    solid = alpha >= 250
    rgb = a[..., :3]
    edge = (alpha > 0) & ~solid
    if edge.any():
        blur = np.zeros_like(rgb)
        weight = np.zeros(alpha.shape, np.float32)
        for dy in range(-3, 4):
            for dx in range(-3, 4):
                shifted = np.roll(np.roll(solid, dy, 0), dx, 1)
                blur += np.roll(np.roll(rgb, dy, 0), dx, 1) * shifted[..., None]
                weight += shifted
        has = edge & (weight > 0)
        rgb[has] = blur[has] / weight[has][:, None]
    out = np.dstack([rgb, alpha]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def trim(img, pad=4):
    box = img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    if not box:
        return img
    l, t, r, b = box
    return img.crop((max(0, l - pad), max(0, t - pad), min(img.width, r + pad), min(img.height, b + pad)))


def fit(img, longest):
    scale = longest / max(img.size)
    if scale >= 1:
        return img
    return img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)


def split_halves(img):
    alpha = np.asarray(img.getchannel("A")).astype(np.float32)
    w = alpha.shape[1]
    columns = alpha.sum(axis=0)
    lo, hi = int(w * 0.3), int(w * 0.7)
    cut = lo + int(np.argmin(columns[lo:hi]))
    return trim(img.crop((0, 0, cut, img.height))), trim(img.crop((cut, 0, w, img.height)))


# round 35 (owner: app size): character poses and bosses 15% smaller than what the phone drew before
# (poses 512 → 435, bosses 768 → 653); the full-width FEVER cut-ins keep their size
CHARACTERS = ("hamster_", "cat_", "robot_", "penguin_", "shiba_", "otottsan_", "toumei_", "tori_", "whamster_", "shark_")


def size_for(name):
    if name.startswith(CHARACTERS) and not name.endswith("cutin_fever"):
        return 435
    if name.startswith("boss_"):
        return 653
    if name.endswith("_slice"):
        return 256
    if name == "shot_charge":
        return 512
    for prefix, size in SIZES.items():
        if name.startswith(prefix):
            return size
    return 512


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    force = "--force" in sys.argv
    for path in sorted(RAW.glob("*.png")):
        name = path.stem
        if name.endswith("_old") or name == "icon_app" or name in UNUSED or name.startswith("prop_"):  # icon lives in Assets/RhythmRunner/Icons; UNUSED art is no longer drawn
            continue
        outputs = [OUT / f"{name}_l.png", OUT / f"{name}_r.png"] if name.endswith("_split") else [OUT / f"{name}.png"]
        if not force and all(o.exists() and o.stat().st_mtime >= path.stat().st_mtime for o in outputs):
            continue  # up to date
        img = Image.open(path)
        if name.startswith("wall_"):
            # side walls: wrap horizontally without a seam (cross-blend the two ends), then shrink for mobile
            img = img.convert("RGB").resize((1024, 683), Image.LANCZOS)
            a = np.asarray(img).astype(np.float32)
            w = a.shape[1]; band = w // 8
            out = a[:, band // 2: w - band // 2].copy()
            ramp = np.linspace(0, 1, band)[None, :, None]
            right = a[:, w - band:]
            left = a[:, :band]
            blend = right * (1 - ramp) + left * ramp
            out[:, -band // 2:] = blend[:, :band // 2]
            out[:, :band - band // 2] = blend[:, band // 2:]
            Image.fromarray(out.clip(0, 255).astype(np.uint8)).save(OUT / f"{name}.png", optimize=True)
            print(f"{name}: wrapped {out.shape[1]}x{out.shape[0]}")
            continue
        if name in OPAQUE or name.startswith("stage_bg_"):
            w, h = OPAQUE.get(name, (1080, 1620))
            img = img.convert("RGB")
            scale = max(w / img.width, h / img.height)
            img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
            left, top = (img.width - w) // 2, (img.height - h) // 2
            img.crop((left, top, left + w, top + h)).save(OUT / f"{name}.png", optimize=True)
            print(f"{name}: {w}x{h} opaque")
            continue
        img = trim(clean_alpha(img.convert("RGBA")))
        if name.endswith("_split"):
            left, right = split_halves(img)
            longest = size_for(name)
            scale = min(1.0, longest / max(img.size))
            for suffix, half in (("_l", left), ("_r", right)):
                half = half.resize((max(1, round(half.width * scale)), max(1, round(half.height * scale))), Image.LANCZOS)
                half.save(OUT / f"{name}{suffix}.png", optimize=True)
                print(f"{name}{suffix}: {half.size}")
            continue
        img = fit(img, size_for(name))
        if name.startswith("toumei_"):
            img = see_through(img)  # round 20: the invisible man lets the stage show through
        if name.startswith("shark_"):
            img = see_through(img, body=.78, rim=.95, icy=0)  # round 25: jelly, a little see-through
        img.save(OUT / f"{name}.png", optimize=True)
        print(f"{name}: {img.size}")


if __name__ == "__main__":
    main()
