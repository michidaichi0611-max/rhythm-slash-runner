"""Round 36: Google Play's feature graphic (1024x500, required) and the 512x512 store icon, from the game's own art.
Run:  python tools/store/make_feature_graphic.py   → store/feature_graphic.png, store/icon_512.png
"""
from pathlib import Path
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "unity_project/Assets/RhythmRunner"
OUT = ROOT / "store"
W, H = 1024, 500


def cover(img, w, h, focus_y=.5):
    """Scale to cover w x h, then crop (focus_y: 0 top .. 1 bottom)."""
    s = max(w / img.width, h / img.height)
    img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    x = (img.width - w) // 2
    y = round((img.height - h) * focus_y)
    return img.crop((x, y, x + w, y + h))


def fit(img, max_w, max_h):
    s = min(max_w / img.width, max_h / img.height)
    return img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)


def shadow(img, blur=10, alpha=110):
    a = img.split()[3].point(lambda v: v * alpha // 255)
    sh = Image.new("RGBA", img.size, (10, 20, 45, 0))
    sh.putalpha(a)
    return sh.filter(ImageFilter.GaussianBlur(blur))


def main():
    bg = cover(Image.open(ASSETS / "Resources/Art/home_backdrop_empty.png").convert("RGB"), W, H, .4).convert("RGBA")
    # darken the left so the logo reads, keep the right bright for the hamster
    veil = Image.new("RGBA", (W, H))
    for x in range(W):
        a = int(max(0, 120 * (1 - x / (W * .62))))
        veil.paste((8, 16, 40, a), (x, 0, x + 1, H))
    bg = Image.alpha_composite(bg, veil)

    hero = Image.open(ASSETS / "Resources/Art/hamster_cutin_fever.png").convert("RGBA")
    hero = fit(hero, 470, 470)
    hx, hy = W - hero.width - 30, H - hero.height + 10
    bg.alpha_composite(shadow(hero), (hx + 8, hy + 10))
    bg.alpha_composite(hero, (hx, hy))

    logo = fit(Image.open(ASSETS / "Resources/Art/logo.png").convert("RGBA"), 560, 230)
    lx, ly = 36, (H - logo.height) // 2 - 10
    bg.alpha_composite(shadow(logo, 8, 140), (lx + 6, ly + 8))
    bg.alpha_composite(logo, (lx, ly))

    OUT.mkdir(exist_ok=True)
    bg.convert("RGB").save(OUT / "feature_graphic.png")
    Image.open(ASSETS / "Icons/icon_app.png").convert("RGB").resize((512, 512), Image.LANCZOS).save(OUT / "icon_512.png")
    print("feature_graphic.png", (W, H), "icon_512.png", (512, 512))


if __name__ == "__main__":
    main()
