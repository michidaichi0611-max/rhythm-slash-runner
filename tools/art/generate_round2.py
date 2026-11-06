"""Round-2 sprites (owner feedback 2026-10-12): non-food targets, hold/mash objects, bosses, run backdrops per stage, gate.

Usage: python tools/art/generate_round2.py [name ...]   (no names = every missing asset)
"""
import concurrent.futures as cf
import sys

import generate_assets as g

RAW, ART, STYLE = g.RAW, g.ART, g.STYLE
ROOT = g.ROOT
OBJ_REF = [RAW / "barrel_intact.png"]
BACKDROP = RAW / "kitchen_backdrop.png"
BOSS_REF = [ROOT / "review" / "concepts" / "boss_a_reflect.png"]

TARGET = "game note object for a rhythm game, slight three-quarter view, same chunky toy-like style and lighting as the attached reference object"
SPLIT = "cut cleanly in half vertically: the two halves are separated with a clear gap between them (left half and right half, not touching), the cut faces clearly visible"
RUN_BG = ("Portrait 9:16 background for a vertical endless runner. Keep EXACTLY the same camera, horizon height and composition as the attached kitchen backdrop: "
          "a long wooden cutting-board road starts at the bottom edge (full width) and recedes straight to a vanishing point at the upper center (about 24% from the top). "
          "The road itself must be completely empty, plain wood. Replace only the surroundings and the far view with: {}. No characters, no text, no UI. " + STYLE)


def tap(name, what, split_what):
    return {
        f"{name}_intact": (f"{what}, {TARGET}. {STYLE}", OBJ_REF, True, "1024x1024"),
        f"{name}_split": (f"The same {split_what} as the attached image, {SPLIT}. {STYLE}", None, True, "1024x1024"),
    }


JOBS = {}
# --- non-food tap targets (owner: "ダイヤとか木箱とか、食べ物以外の面白いやつ")
JOBS.update(tap("crate", "A small sturdy wooden crate with metal corner brackets and a stamped paw-print mark", "wooden crate, splintered wood edges and a few chips"))
JOBS.update(tap("gem", "A big sparkling cyan-blue cut diamond gem with bright facets and a glint", "diamond gem, glittering crystal cut faces and tiny sparkles"))
JOBS.update(tap("dice", "A big glossy white toy dice with rounded corners and red and black pips", "white toy dice, solid white interior"))
JOBS.update(tap("clock", "A round red twin-bell alarm clock with a cute face, little feet and a hammer on top", "alarm clock, little brass gears and a spring popping out"))
JOBS.update(tap("duck", "A glossy yellow rubber duck bath toy with an orange beak", "rubber duck, hollow yellow rubber shell inside, a squeaky toy look"))
# --- hold objects: long items lying along the lane (drawn stretched along the road) + one round slice
for k, what, piece in (
    ("baguette", "A single long golden French baguette with diagonal score marks", "A single round slice of French baguette showing the soft white crumb and golden crust"),
    ("leek", "A single long Japanese green onion (negi): white stalk at the bottom, green leaves at the top", "A single short round slice of green onion showing the rings"),
    ("pencil", "A single giant yellow wooden pencil with a pink eraser at the bottom and a sharpened tip at the top", "A single short round slice of a yellow pencil showing the wood and graphite core"),
):
    JOBS[f"hold_{k}"] = (f"{what}, lying perfectly vertical from the bottom edge to the top edge of the image, seen from above and slightly behind, filling the full height, nothing else. {STYLE}", None, True, "1024x1536")
    JOBS[f"hold_{k}_slice"] = (f"{piece}, cut face toward the camera, game VFX piece, nothing else. {STYLE}", None, True, "1024x1024")
# --- mash objects: big and hard, broken by rapid taps
JOBS.update(tap("pumpkin", "A huge round orange pumpkin with a thick curly stem, heavy and hard-looking", "huge pumpkin, thick orange flesh and seeds inside"))
JOBS.update(tap("safe", "A chunky small steel safe with a big round dial and a golden handle, heavy and hard-looking", "steel safe, broken open with shiny gold coins spilling out"))
# --- bosses (A案: shots are slashed back at the boss)
JOBS["boss_onion"] = ("A giant grumpy onion king boss for a cute mobile rhythm game: a huge purple-white onion with an angry frowning face, white bushy eyebrows and mustache, golden crown with a ruby, red royal cape, "
                      "front view facing the camera, full body, menacing but cute, same design as the boss in the attached concept image. " + STYLE, BOSS_REF, True, "1024x1024")
JOBS["boss_toaster"] = ("A giant chrome toaster monster boss for a cute mobile rhythm game: shiny retro toaster with angry glowing eyes, a jagged grin, little stubby arms, two slices of toast popping out of the top, "
                        "front view facing the camera, full body, menacing but cute. " + STYLE, BOSS_REF, True, "1024x1024")
JOBS["shot_onion"] = ("A small glowing purple onion bulb projectile with a magenta energy aura, game VFX object, nothing else. " + STYLE, BOSS_REF, True, "1024x1024")
JOBS["shot_toaster"] = ("A single slice of golden toast flying as a projectile with a faint orange heat glow, slight tilt, game VFX object, nothing else. " + STYLE, None, True, "1024x1024")
# --- run backdrops for the automatic stage changes (same camera as the kitchen)
for k, desc in (
    ("toy_room", "a giant colorful toy room: towers of toy blocks, a toy train, plush bears, a big window with blue sky, blue/yellow/red palette"),
    ("sky_orchard", "a sky orchard above the clouds: floating islands with peach trees, fluffy clouds on both sides, a bright sky blue/peach palette"),
    ("neon_market", "a neon night market: lantern-lit food stalls on both sides, glowing signs without readable text, navy/purple/red palette, night sky"),
    ("candy_land", "a candy land: giant lollipops, cupcakes, cream frosting hills with sprinkles, a cake castle far away, pink sky"),
    ("freezer", "the inside of a giant freezer turned ice world: frosty shelves, ice cream tubs, ice crystals and soft cold blue light, gentle snow sparkles"),
):
    JOBS[f"stage_bg_{k}"] = (RUN_BG.format(desc), [BACKDROP], False, "1024x1536")
# --- the gate passed at every stage change
JOBS["gate_arch"] = ("A big glowing magical archway gate seen straight from the front, rounded stone-and-gold arch with sparkling rainbow light filling its frame edges, the opening itself is fully transparent (empty), game object, nothing else. " + STYLE, None, True, "1536x1024")

# split images reference the intact one so both halves match
DEPENDENT = {n: n.replace("_split", "_intact") for n in JOBS if n.endswith("_split")}


def main():
    g.JOBS.update(JOBS)
    for name, src in DEPENDENT.items():
        p, _, t, s = JOBS[name]
        g.JOBS[name] = (p, [RAW / f"{src}.png"], t, s)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    first = [n for n in names if n not in DEPENDENT]
    later = [n for n in names if n in DEPENDENT]
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, first):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)
        for name, ok, tail in pool.map(g.run, later):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
