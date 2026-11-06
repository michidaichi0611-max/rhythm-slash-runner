"""Generates the MVP sprite set with the Codex image tool (~/bin/imagegen.py), 3 jobs in parallel.

Raw outputs go to art_generated/raw/<name>.png; tools/art/process_assets.py trims/resizes them into Unity Resources.
Usage: python tools/art/generate_assets.py [name ...]   (no names = every missing asset)
"""
import concurrent.futures as cf
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "art_generated" / "raw"
ART = ROOT / "art"
TOOL = Path.home() / "bin" / "imagegen.py"

STYLE = ("Cute chunky toy-like 3D render for a premium mobile rhythm game, soft warm studio lighting, rounded bevelled shapes, "
         "clean bold silhouette readable at small size, saturated but soft colors, same art style as the reference concept art. No text, no watermark.")
HAMSTER = ("the golden hamster hero from the reference model sheet: honey-orange fluffy fur, cream belly, small pink ears, "
           "red scarf tied around the neck with the knot and tails behind, dark brown leather belt with a small pouch, fluffy white round tail")
BACK = "seen exactly from behind (back view), camera slightly above and behind like a third-person runner game, full body, centered"

HAM_REF = [ART / "03_hamster_model_sheet.png", ART / "06_gameplay_hud.png"]
OBJ_REF = [ART / "04_weapons_objects.png"]
STAGE_REF = [ART / "05_stage_concepts.png", ART / "06_gameplay_hud.png"]

# name: (prompt, refs, transparent, size)
JOBS = {
    "hamster_run_a": (f"{HAMSTER}, {BACK}, running away from the camera, left foot lifted and right foot on the ground, arms pumping, scarf tails fluttering, right paw empty and slightly out to the side (no weapon). {STYLE}", HAM_REF, True, "1024x1024"),
    "hamster_run_b": (f"Edit the attached hamster image into the OTHER step of the same run cycle. Keep everything identical (same hamster, same back view, same size, same position, belt pouch still on the viewer's LEFT side, right paw still out to the viewer's RIGHT side, scarf blowing to the viewer's left) EXCEPT the legs: now the viewer's RIGHT foot is lifted with its pink sole showing and the viewer's LEFT foot is planted on the ground. {STYLE}", None, True, "1024x1024"),
    "hamster_slash": (f"{HAMSTER}, {BACK}, mid-slash pose: body twisted, right arm swung forward and across to the left as if slicing something in front, empty clenched right paw (the weapon is added separately), scarf flying. {STYLE}", HAM_REF, True, "1024x1024"),
    "hamster_miss": (f"{HAMSTER}, {BACK}, stumbling after a missed swing, wobbling to one side, head tilted, small dizzy swirl above the head, empty paws. {STYLE}", HAM_REF, True, "1024x1024"),
    "hamster_front_idle": (f"{HAMSTER}, front view facing the camera, happy open-mouth smile, big shiny eyes, standing proudly, right paw raised at shoulder height as a clenched fist holding nothing (a weapon will be attached there), left paw on the belt. {STYLE}", HAM_REF, True, "1024x1024"),
    "hamster_front_cheer": (f"{HAMSTER}, front view, jumping with joy, eyes closed in a big happy smile, both arms up, one leg kicked up, celebratory pose for a results screen, empty paws. {STYLE}", HAM_REF, True, "1024x1024"),
    "strawberry_intact": (f"A single big glossy strawberry with green leaves and yellow seeds, slight three-quarter view, game note object, bright rim light. {STYLE}", OBJ_REF, True, "1024x1024"),
    "strawberry_split": (f"The same strawberry sliced cleanly in half vertically: the two halves are separated with a clear gap between them (left half and right half, not touching), bright red juicy cut faces showing white core and flesh, a few small juice droplets. {STYLE}", OBJ_REF, True, "1024x1024"),
    "watermelon_intact": (f"A single round watermelon with dark green stripes, slight three-quarter view, game note object, glossy highlight. {STYLE}", OBJ_REF, True, "1024x1024"),
    "watermelon_split": (f"The same round watermelon cut cleanly in half vertically: the two halves separated with a clear gap (left and right, not touching), bright red flesh with black seeds on the cut faces, green rind. {STYLE}", OBJ_REF, True, "1024x1024"),
    "barrel_intact": (f"A single small wooden barrel with two dark metal bands and rivets, slight three-quarter view, game note object, warm wood grain. {STYLE}", OBJ_REF, True, "1024x1024"),
    "barrel_split": (f"The same wooden barrel split vertically into two broken halves with a clear gap between them (left and right, not touching), splintered wood edges, a few small wood chips. {STYLE}", OBJ_REF, True, "1024x1024"),
    "weapon_energy_blade": (f"A single toy-like glowing cyan energy blade sword, perfectly vertical, tip pointing straight up, handle at the bottom, metal guard and grip, soft glow. Only the weapon, centered. {STYLE}", OBJ_REF, True, "1024x1024"),
    "weapon_wooden_sword": (f"A single toy-like wooden sword (bokken) with a wrapped cloth grip, perfectly vertical, tip pointing straight up, handle at the bottom. Only the weapon, centered. {STYLE}", OBJ_REF, True, "1024x1024"),
    "weapon_banana": (f"A single comedic peeled-banana blade weapon with a wrapped brown grip, perfectly vertical, banana tip pointing straight up, handle at the bottom. Only the weapon, centered. {STYLE}", OBJ_REF, True, "1024x1024"),
    "kitchen_backdrop": ("Portrait 9:16 background for a vertical endless runner, viewpoint of a tiny hamster on a giant kitchen countertop: a long wooden cutting-board road starts at the bottom edge (full width) and recedes straight to a vanishing point at the upper center (about 24% from the top), "
                         "at the far end a bright arched window showing a sunny village with a windmill. Left and right of the road: giant glass jars, a cheese wedge, ripe tomatoes, basil and herb pots, a wooden pepper mill, a red gingham cloth, wooden crates. "
                         "The road itself must be completely empty (no food, no characters, no obstacles), only plain wood. Warm golden afternoon light, soft depth of field in the distance. No text, no labels, no UI. " + STYLE, STAGE_REF, False, "1024x1536"),
    "wood_road_tile": ("Seamless tileable top-down texture of a light honey-colored wooden cutting board, straight vertical wood grain, a few faint knife marks and a light dusting of flour, even lighting, no perspective, no seams, fills the whole square. No text.", None, False, "1024x1024"),
    "prop_jar": (f"A giant glass storage jar with a metal lid filled with flour, warm kitchen prop, three-quarter view. No text or labels. {STYLE}", STAGE_REF, True, "1024x1024"),
    "prop_tomatoes": (f"A cluster of three ripe red tomatoes on the vine, kitchen prop. {STYLE}", STAGE_REF, True, "1024x1024"),
    "prop_cheese": (f"A big yellow cheese wedge with round holes, kitchen prop. {STYLE}", STAGE_REF, True, "1024x1024"),
    "prop_pepper_mill": (f"A tall wooden pepper mill, kitchen prop, upright. {STYLE}", STAGE_REF, True, "1024x1024"),
    "prop_crate": (f"A small wooden crate with a cute carved hamster face emblem on the front and a little green vine, kitchen prop. {STYLE}", STAGE_REF, True, "1024x1024"),
    "prop_herbs": (f"A terracotta pot with lush green basil, kitchen prop. {STYLE}", STAGE_REF, True, "1024x1024"),
    "vfx_slash": ("A single bright glowing slash streak effect: a thin curved crescent swoosh going diagonally from lower-left to upper-right, white-hot core with cyan glow edges and tiny sparkles, game VFX sprite, nothing else.", None, True, "1536x1024"),
    "vfx_juice": ("Game VFX sprite: a radial burst of glossy red juice droplets and small splashes flying outward from the center, nothing else.", None, True, "1024x1024"),
    "vfx_chips": ("Game VFX sprite: a radial burst of small light-brown wood chips and splinters flying outward from the center, nothing else.", None, True, "1024x1024"),
    "vfx_sparkle": ("Game VFX sprite: a golden four-point star sparkle burst with small twinkles around it, glowing, nothing else.", None, True, "1024x1024"),
    "icon_heart": (f"A glossy red heart icon for game HUD lives, chunky 3D, white highlight, thin dark outline. {STYLE}", OBJ_REF, True, "1024x1024"),
    "icon_coin": (f"A glossy gold coin icon with a star embossed in the middle, chunky 3D, for a game HUD currency. {STYLE}", None, True, "1024x1024"),
    "icon_settings": (f"A chunky glossy cyan gear (cog) icon for a game settings button, simple bold shape, white highlight, thin dark navy outline. {STYLE}", None, True, "1024x1024"),
    "icon_flag": (f"A small chunky checkered finish-line flag icon (black and white squares) on a short pole, for a distance counter in a game HUD, bold outline. {STYLE}", None, True, "1024x1024"),
    "icon_app": (f"Square mobile app icon: close-up of {HAMSTER} face, cheerful, holding a glowing cyan energy blade diagonally, warm golden kitchen background with soft bokeh, bold readable composition, fills the whole square, no text. {STYLE}", [ART / "03_hamster_model_sheet.png"], False, "1024x1024"),
    "logo": ("Game title logo that reads exactly \"RHYTHM SLASH RUNNER\" on two lines: small \"RHYTHM\" above, big bold italic \"SLASH RUNNER\" below. Chunky playful 3D letters, white-to-cyan gradient with a dark navy outline and a coral slash streak cutting across the word SLASH, mobile game logo, centered, nothing else.", [ART / "01_splash_loading.png"], True, "1536x1024"),
    "key_visual": (f"Portrait 9:16 key visual for the home screen: {HAMSTER} in front view, cheerful, holding a glowing cyan energy blade in the right paw, standing on a wooden cutting board in a warm giant kitchen with bokeh lights, jars and fruit in the background. Keep the top 30% of the image calm (soft blurred background) for a logo, character in the lower-middle. {STYLE}", [ART / "01_splash_loading.png", ART / "03_hamster_model_sheet.png"], False, "1024x1536"),
    "card_cat": (f"Character select portrait: a cute chunky black cat runner (dark bluish-gray, not pure black), triangle ears, long tail, red scarf, front three-quarter view, waist up. {STYLE}", [ART / "08_character_weapon_roster.png"], True, "1024x1024"),
    "card_runner": (f"Character select portrait: a cute chibi human runner kid with big sneakers and a coral outfit, toy-like proportions, front three-quarter view, waist up. {STYLE}", [ART / "08_character_weapon_roster.png"], True, "1024x1024"),
    "card_robot": (f"Character select portrait: a small round cute robot with glowing cyan eyes and short joints, front three-quarter view, waist up. {STYLE}", [ART / "08_character_weapon_roster.png"], True, "1024x1024"),
    "stage_toy_room": (f"Square diorama card of a toy room endless-runner stage: a 3-lane track of toy blocks receding into the distance, toy train, plush toys, blue/yellow/red palette, no characters. {STYLE}", [ART / "09_world_variations.png"], False, "1024x1024"),
    "stage_sky_orchard": (f"Square diorama card of a sky orchard endless-runner stage: a cloud bridge path receding into the distance among floating islands with peach trees, sky blue/peach/white palette, no characters. {STYLE}", [ART / "09_world_variations.png"], False, "1024x1024"),
    "stage_neon_market": (f"Square diorama card of a neon night market endless-runner stage: a wet street path receding into the distance with lanterns and food stalls, navy/purple/red palette, no characters. {STYLE}", [ART / "09_world_variations.png"], False, "1024x1024"),
}


def run(name):
    prompt, refs, transparent, size = JOBS[name]
    out = RAW / f"{name}.png"
    if refs is None and name == "hamster_run_b":
        refs = [RAW / "hamster_run_a.png"]
    cmd = [sys.executable, str(TOOL), prompt, "-o", str(out), "--size", size, "--timeout", "1200"]
    for ref in refs or []:
        cmd += ["-i", str(ref)]
    if transparent:
        cmd.append("--transparent")
    result = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    ok = result.returncode == 0 and out.exists()
    return name, ok, (result.stdout + result.stderr)[-400:]


def main():
    RAW.mkdir(parents=True, exist_ok=True)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    first = [n for n in names if n == "hamster_run_a"]
    rest = [n for n in names if n != "hamster_run_a"]
    if first:
        print(run(first[0])[:2], flush=True)
    # run_b references run_a, so it waits until run_a exists.
    if "hamster_run_b" in rest and not (RAW / "hamster_run_a.png").exists():
        rest.remove("hamster_run_b")
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(run, rest):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
