"""Round-5 sprites (owner 2026-10-12): two new playable characters (black cat, robot) and boss poses.

Every character pose is redrawn from the matching hamster pose (same camera, framing and fist position) so the run
animation, the weapon grip points and the timing of poses line up across characters.
Usage: python tools/art/generate_round5.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g

RAW, STYLE = g.RAW, g.STYLE
CHARACTERS = {
    "cat": ("the black cat runner from the second reference image: fluffy black fur with a soft bluish sheen, big golden eyes, pink inner ears, "
            "red scarf, white-and-black tech jacket with red straps, small red pouch with a cat emblem, long curling tail", RAW / "card_cat.png"),
    "robot": ("the small round robot from the second reference image: glossy white-and-orange round body and head, black visor face with glowing cyan smiling eyes, "
              "a small green sprout leaf on top of the head, orange joints, black hands, brown shoulder bag. The robot wears NO scarf and NO cloth", RAW / "card_robot.png"),
}
POSES = ["run_a", "run_b", "run_c", "run_d", "slash_c", "slash_down", "slash_r", "slash_l", "hit", "combo_pose",
         "fever_dash_a", "fever_dash_b", "back_ready", "front_hold", "front_hold_blink", "front_cheer", "front_lose",
         "front_newrecord", "front_idle", "cutin_fever"]

JOBS = {}
for key, (desc, card) in CHARACTERS.items():
    for pose in POSES:
        size = "1536x1024" if pose == "cutin_fever" else "1024x1024"
        JOBS[f"{key}_{pose}"] = (
            f"Redraw the FIRST attached image with a different character: {desc}. Keep EXACTLY the same pose, body orientation, camera angle, "
            f"framing, scale, limb positions and gesture as the first image (same view: if the first image is a back view, this is a back view too). "
            f"Any empty clenched fist stays an empty clenched fist (the weapon is drawn separately; no weapon in the paws or hands) unless the first image shows a weapon. "
            f"Keep the character design and colors of the second image. {STYLE}",
            [RAW / f"hamster_{pose}.png", card], True, size)

BOSS_POSES = {
    "attack": "leaning forward with the mouth wide open, about to spit a projectile, arms thrown forward, aggressive",
    "hurt": "recoiling backward in pain, eyes squeezed shut, a small tear, body squashed, comic hurt expression",
    "rage": "furious rage mode: glowing red eyes, steam puffing from the head, veins popping, red angry aura, fists raised",
}
for boss in ("onion", "toaster"):
    for pose, desc in BOSS_POSES.items():
        JOBS[f"boss_{boss}_{pose}"] = (
            f"The same boss character as the attached image, same design, colors and proportions, front view facing the camera, full body, now {desc}. {STYLE}",
            [RAW / f"boss_{boss}.png"], True, "1024x1024")


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
