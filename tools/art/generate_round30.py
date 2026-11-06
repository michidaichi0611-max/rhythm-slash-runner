"""Round-30 sprites (owner 2026-10-18): three more bosses, none of them food.

- boss_zenmai: the Wind-up King (toy room) — a tin robot king with a big wind-up key on its back
- boss_kaminari: the Thunder Cloud General (sky orchard) — a grumpy storm cloud that throws lightning
- boss_yukidaruma: the Snowman General (freezer) — a snowman with a bucket helmet and a carrot nose
Each: base sprite, the three boss poses (attack / hurt / rage, like every boss) and its projectile.
Usage: python tools/art/generate_round30.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g
from generate_round5 import BOSS_POSES

RAW, STYLE = g.RAW, g.STYLE
BOSSES = {
    "zenmai": ("'The Wind-up King': a giant chubby tin toy robot king made of shiny painted tin plate with visible rivets, a small golden "
               "crown, round glowing yellow eyes, a square jaw with a mustache painted on, a big brass wind-up key sticking out of its back, "
               "a red royal cape, chunky tin arms with claw hands, short stubby legs",
               "a small spinning brass gear cog with a faint golden glow"),
    "kaminari": ("'The Thunder Cloud General': a giant grumpy dark grey storm cloud with an angry face, thick frowning eyebrows, little "
                 "stubby cloud arms, crackling yellow lightning bolts around it, a small general's peaked cap on top, raindrops falling "
                 "from its underside",
                 "a small crackling ball of yellow-white electricity with tiny lightning sparks"),
    "yukidaruma": ("'The Snowman General': a giant snowman made of three big snowballs, an upside-down metal bucket worn as a helmet, a "
                   "carrot nose, coal-lump eyes and mouth with an angry grin, twig arms holding an icicle sword, a red scarf, coal buttons, "
                   "frosty sparkles",
                   "a small round snowball with icy sparkles and a little frost trail"),
}
JOBS = {}
for key, (desc, shot) in BOSSES.items():
    JOBS[f"boss_{key}"] = (f"Game boss sprite for a cute mobile rhythm game: {desc}. Front view facing the camera, full body, menacing but cute, "
                           f"compact silhouette, same style as the attached boss. {STYLE}", [RAW / "boss_onion.png"], True, "1024x1024")
    for pose, pd in BOSS_POSES.items():
        JOBS[f"boss_{key}_{pose}"] = (f"The same boss character as the attached image, same design, colors and proportions, front view facing "
                                      f"the camera, full body, now {pd}. {STYLE}", [RAW / f"boss_{key}.png"], True, "1024x1024")
    JOBS[f"shot_{key}"] = (f"Game projectile sprite: {shot}, spinning. Centered, compact, front view. {STYLE}", [RAW / f"boss_{key}.png"], True, "1024x1024")


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    bases = [n for n in names if n in (f"boss_{k}" for k in BOSSES)]
    for n in bases:
        names.remove(n)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, bases):
            print(f"{'OK ' if ok else 'ERR'} {name}", flush=True)
    with cf.ThreadPoolExecutor(max_workers=4) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
