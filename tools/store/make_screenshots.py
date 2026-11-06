"""Round 34: store screenshots (1080×1920) — a caption band over a real capture from the game.

Captures come from the Windows build:  RhythmSlashRunner.exe -rsrShots <dir> -rsrAutoplay -rsrTier 4 -rsrLang ja|en -rsrShotScale 2
Usage: python tools/store/make_screenshots.py   → store/screenshots/<lang>/01.png …
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
FONT = ROOT / "unity_project" / "Assets" / "RhythmRunner" / "Resources" / "Fonts" / "MPLUSRounded1c-ExtraBold.ttf"
SHOTS = [
    ("22_hold_1", "音に合わせて\nズバッと斬る！", "Slice to the beat!"),
    ("16_fever_hits_1", "コンボで FEVER！\n虹色の道を 駆け抜けろ", "Chain combos into\nrainbow FEVER!"),
    ("26_boss_down_1", "弾を斬り返して\nボスを倒せ！", "Slash shots back\nand beat the boss!"),
    ("29_mash_full_0", "かたい物は\n連打で 粉砕！", "Mash to smash\nthe tough ones!"),
    ("02_character", "10人のキャラと\n9つの武器", "10 characters,\n9 weapons"),
    ("30_result_card", "結果をシェアして\n友だちと競おう", "Share your runs\nand compete!"),
]
W, H, BAND = 1080, 1920, 330


def gradient(w, h):
    top, bottom = (255, 176, 64), (214, 46, 128)
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / (h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)))
    return img


def make(lang, raw_dir, out_dir):
    out_dir.mkdir(parents=True, exist_ok=True)
    for i, (name, ja, en) in enumerate(SHOTS, 1):
        src = raw_dir / f"{name}.png"
        if not src.exists():
            print("missing", src); continue
        canvas = gradient(W, H)
        shot = Image.open(src).convert("RGB")
        scale = (H - BAND - 40) / shot.height
        shot = shot.resize((int(shot.width * scale), int(shot.height * scale)), Image.LANCZOS)
        x, y = (W - shot.width) // 2, BAND
        # soft shadow and rounded corners
        mask = Image.new("L", shot.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle([0, 0, shot.width - 1, shot.height - 1], radius=36, fill=255)
        shadow = Image.new("L", (W, H), 0)
        shadow.paste(mask, (x + 10, y + 14))
        shadow = shadow.filter(ImageFilter.GaussianBlur(18))
        canvas.paste((60, 10, 40), (0, 0), shadow.point(lambda v: int(v * .55)))
        canvas.paste(shot, (x, y), mask)
        text = ja if lang == "ja" else en
        d = ImageDraw.Draw(canvas)
        size = 96 if lang == "ja" else 90
        font = ImageFont.truetype(str(FONT), size)
        lines = text.split("\n")
        total = len(lines) * (size + 18)
        ty = (BAND - total) // 2 + 6
        for line in lines:
            w = d.textlength(line, font=font)
            d.text(((W - w) / 2, ty), line, font=font, fill="white", stroke_width=8, stroke_fill=(90, 20, 60))
            ty += size + 18
        canvas.save(out_dir / f"{i:02d}.png")
    print(lang, "→", out_dir)


def main():
    for lang in ("ja", "en"):
        make(lang, ROOT / "review" / f"store_raw_{lang}", ROOT / "store" / "screenshots" / lang)


if __name__ == "__main__":
    main()
