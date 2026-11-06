"""Round-1 sprites requested by the review committee (review/round0/*.md): 30 images.

Back-view poses all reference hamster_run_a so the belt pouch stays on the viewer's LEFT hip (committee P0 #1).
Usage: python tools/art/generate_round1.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g

RAW, ART, STYLE, HAMSTER = g.RAW, g.ART, g.STYLE, g.HAMSTER
RUN_A, RUN_B = RAW / "hamster_run_a.png", RAW / "hamster_run_b.png"
FRONT, CHEER = RAW / "hamster_front_idle.png", RAW / "hamster_front_cheer.png"
BACKDROP, KEY, SLASH = RAW / "kitchen_backdrop.png", RAW / "key_visual.png", RAW / "vfx_slash.png"

SAME = ("Keep the exact same hamster design, fur, colors, lighting, proportions and scale as the attached reference. "
        "Back view from slightly above and behind. The brown belt pouch MUST stay on the viewer's LEFT hip exactly like the reference, "
        "red scarf knot at the back of the neck, fluffy white tail. No weapon in the paws (the weapon is drawn separately); "
        "the hamster's right paw (on the viewer's right side) is a clenched fist that could hold a sword grip. Full body, centered.")
FRONT_SAME = ("Keep the exact same hamster design, face, colors, lighting and proportions as the attached reference, front view, "
              "belt pouch in the same place as the reference. Full body, centered.")

JOBS = {
    # --- character (committee: character & animation)
    "hamster_run_c": (f"{SAME} Running cycle passing pose: both feet close together under the body, body slightly lower, scarf tails hanging lower. {STYLE}", [RUN_A], True, "1024x1024"),
    "hamster_run_d": (f"{SAME} Running cycle airborne pose: both feet off the ground tucked up, body slightly stretched upward, scarf tails flicked high. {STYLE}", [RUN_B], True, "1024x1024"),
    "hamster_slash_l": (f"{SAME} Slashing to the LEFT lane: upper body twisted to the viewer's left, the right fist swung horizontally across in front of the body toward the viewer's left, stepping forward on the left foot. {STYLE}", [RUN_A], True, "1024x1024"),
    "hamster_slash_c": (f"{SAME} Slashing straight ahead: both shoulders forward, the right fist swung down in front of the body at center, leaning forward. {STYLE}", [RUN_A], True, "1024x1024"),
    "hamster_slash_r": (f"{SAME} Slashing to the RIGHT lane: upper body leaning to the viewer's right, the right fist swung outward horizontally to the viewer's right side, arm extended. {STYLE}", [RUN_A], True, "1024x1024"),
    "hamster_hit": (f"{SAME} Got hit: recoiling backward, both arms flung open, head tilted back, a small dizzy swirl above the head. {STYLE}", [RUN_A], True, "1024x1024"),
    "hamster_combo_pose": (f"{SAME} Celebrating while running: right fist punched straight up into the air, left foot kicked up, scarf flying. {STYLE}", [RUN_B], True, "1024x1024"),
    "hamster_fever_dash_a": (f"{SAME} FEVER full-speed dash: strongly leaning forward, left leg reaching forward, scarf streaming horizontally, a faint golden rim light on the fur. {STYLE}", [RUN_A], True, "1024x1024"),
    "hamster_fever_dash_b": (f"{SAME} Same FEVER full-speed dash as the second reference image but the other step: right leg reaching forward. Strong forward lean, scarf streaming horizontally, faint golden rim light. {STYLE}", [RUN_B], True, "1024x1024"),
    "hamster_back_ready": (f"{SAME} Ready stance before the run starts: knees bent, low crouch, right fist held forward, determined posture. {STYLE}", [RUN_A], True, "1024x1024"),
    "hamster_front_idle_blink": (f"{FRONT_SAME} Exactly the same pose and composition as the reference, only the eyes are closed in a gentle blink (happy closed-eye arcs). {STYLE}", [FRONT], True, "1024x1024"),
    "hamster_front_lose": (f"{FRONT_SAME} Mildly disappointed but cute: ears slightly drooping, wry smile, scratching the back of its head with one paw. Not sad or crying. {STYLE}", [FRONT], True, "1024x1024"),
    "hamster_front_newrecord": (f"{FRONT_SAME} Ecstatic new-record jump: both feet off the ground, right fist thrust high, huge open-mouth smile, sparkles around. {STYLE}", [CHEER], True, "1024x1024"),
    "hamster_front_hold": (f"{FRONT_SAME} Standing proudly, right paw (viewer's left) held at chest height as a clenched fist turned slightly outward so a sword grip can be placed in it, left paw on the belt, confident smile. No weapon. {STYLE}", [FRONT], True, "1024x1024"),
    "hamster_front_hold_blink": (f"{FRONT_SAME} Exactly the same pose, arm positions and composition as the attached reference (right paw raised as a fist at chest height, left paw on the belt), only the eyes are closed in a gentle happy blink. {STYLE}", [RAW / "hamster_front_hold.png"], True, "1024x1024"),
    "hamster_slash_down": (f"{SAME} Overhead downward chop straight ahead at the center lane: the right fist raised high above the right shoulder and coming down in front of the body, upper body leaning forward, both feet planted. Clearly different from a sideways swing. {STYLE}", [RUN_A], True, "1024x1024"),
    # --- effects (committee: effects & game feel)
    "hamster_cutin_fever": (f"Cut-in illustration for a FEVER moment: {HAMSTER}, upper body, excited fierce grin, holding a glowing cyan energy blade raised high, dynamic diagonal composition as if dashing in from the right, motion streaks behind. Same design as the reference. {STYLE}", [FRONT, CHEER], True, "1536x1024"),
    "kitchen_backdrop_fever": ("Same composition, camera and road layout as the attached kitchen backdrop, but at a festive golden evening: the window shows a purple-to-gold sunset sky with hanging string lights and lanterns, warm magical glow, a few floating sparkles. The wooden road stays empty and low-contrast. No text.", [BACKDROP], False, "1024x1536"),
    "vfx_slash_rainbow": ("Same crescent slash streak shape as the reference VFX, but with a white-hot core and rainbow-colored edges with tiny sparkles. Game VFX sprite, nothing else.", [SLASH], True, "1536x1024"),
    "logo_fever": ("Game logo that reads exactly \"FEVER!!\": very bold italic chunky 3D letters with a gold outline and a rainbow gradient inside, small sparkles, energetic, centered, nothing else.", None, True, "1536x1024"),
    "logo_new_record": ("Game logo that reads exactly \"NEW RECORD!\": bold italic chunky 3D letters, gold and white with a dark navy outline, celebratory sparkles, centered, nothing else.", None, True, "1536x1024"),
    "record_ribbon": ("A golden ribbon banner with a laurel wreath emblem, no text on it, celebratory game UI decoration, centered, nothing else.", None, True, "1536x1024"),
    "vfx_trail_wood": ("A curved slash trail swoosh in warm green and wood colors with a few tiny leaves and wood chips, same crescent shape as the reference, game VFX sprite, nothing else.", [SLASH], True, "1536x1024"),
    "vfx_trail_banana": ("A curved slash trail swoosh in bright banana yellow with a few tiny stars, same crescent shape as the reference, game VFX sprite, nothing else.", [SLASH], True, "1536x1024"),
    "vfx_fever_aura": ("A soft radial golden aura glow with gentle light rays, transparent in the very center, to be placed behind a character, game VFX sprite, nothing else.", None, True, "1024x1024"),
    "vfx_dust_puff": ("A small soft cream-colored dust puff cloud kicked up from a wooden floor, cartoon game VFX sprite, nothing else.", None, True, "1024x1024"),
    # --- UI / shop (committee: UI & shop)
    "ui_pedestal": (f"A round display pedestal for a character preview: a thick round wooden cutting-board disk seen from slightly above, with a glowing cyan ring around its edge. Nothing on top. {STYLE}", None, True, "1536x1024"),
    "giftbox_closed": (f"A chunky cute gift box with a red ribbon and bow, kitchen-themed pattern (tiny strawberries), closed, three-quarter view. {STYLE}", None, True, "1024x1024"),
    "giftbox_open": (f"The same gift box as the reference, now open: the lid popped off and floating above, bright golden light beaming upward out of the box. {STYLE}", [RAW / "giftbox_closed.png"], True, "1024x1024"),
    "home_backdrop_empty": ("Same image as the attached key visual but with the hamster character completely removed: only the warm giant kitchen background, cutting board, cheese and strawberries, bokeh lights. Keep everything else identical. No text.", [KEY], False, "1024x1536"),
    "icon_lock": (f"A chunky golden padlock icon for locked items in a game shop, glossy, thin dark outline. {STYLE}", None, True, "1024x1024"),
    "icon_crown": (f"A chunky golden crown icon with small gems for best records in a game UI, glossy, thin dark outline. {STYLE}", None, True, "1024x1024"),
}
DEPENDENT = {"giftbox_open": "giftbox_closed"}


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    first = [n for n in names if n not in DEPENDENT]
    later = [n for n in names if n in DEPENDENT]
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, first):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)
    for name in later:
        name, ok, tail = g.run(name)
        print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
