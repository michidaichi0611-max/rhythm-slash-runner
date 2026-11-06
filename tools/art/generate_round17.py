"""Round-17 sprites (owner 2026-10-14): the soldiers that walk in and attack on the beat (parried "キンキンキン").

Prototype first: the toy soldier only. Front view (they come toward the camera like every target).
Usage: python tools/art/generate_round17.py [name ...]
"""
import concurrent.futures as cf
import sys

import generate_assets as g

RAW, STYLE = g.RAW, g.STYLE
TOY = ("a small cute wooden toy soldier (nutcracker style): tall red shako hat with gold badge, round rosy painted cheeks, "
       "dot eyes, little black moustache, red jacket with gold buttons and white cross belts, navy blue trousers, black boots, "
       "holding a short silver toy sabre in the right hand, glossy painted-wood finish")

JOBS = {
    "soldier_toy_walk": (f"Game enemy sprite: {TOY}. Front view facing the camera, full body, marching toward the viewer with one leg lifted, "
                         f"sabre held upright at the shoulder, determined cute face. Centered, compact silhouette. {STYLE}",
                         g.OBJ_REF, True, "1024x1024"),
}
POSES = {
    "attack": "lunging forward and slashing the sabre down diagonally toward the viewer, the blade flashing with a bright white glint, fierce cute face",
    "hurt": "knocked backward off balance after its sabre was parried, arms flung out, sabre flying loose, dizzy swirl eyes, hat tilted",
}
# round 23 (owner: two more enemies): a gingerbread soldier and a frog swordsman
GINGER = ("a small cute gingerbread-man cookie soldier: golden-brown baked cookie body with white icing piping outlining a little military jacket, "
          "icing buttons, a tiny red-and-white peppermint candy hat, gumdrop eyes, a candy cane sabre in the right hand, crumbly cookie edges")
FROG = ("a small cute green frog swordsman: round green frog body with a pale yellow belly, big round eyes on top of the head, a cape made of a big "
        "leaf, a little brown belt, holding a thin silver rapier in the right hand, light springy legs")
for key, desc in (("ginger", GINGER), ("frog", FROG)):
    JOBS[f"soldier_{key}_walk"] = (f"Game enemy sprite: {desc}. Front view facing the camera, full body, marching toward the viewer with one leg lifted, "
                                   f"weapon held upright at the shoulder, determined cute face. Centered, compact silhouette. {STYLE}",
                                   [RAW / "soldier_toy_walk.png"], True, "1024x1024")
# round 26 (owner: an enemy that mixes every move at random): a crow cook. Its long strike is a held flame (hold to guard)
CROW = ("a small cute black crow cook: glossy blue-black feathers, a tall white chef's hat, a white double-breasted chef jacket with a red "
        "neckerchief, a short yellow beak, sly clever eyes, holding a black frying pan in its right wing like a weapon")
JOBS["soldier_crow_walk"] = (f"Game enemy sprite: {CROW}. Front view facing the camera, full body, marching toward the viewer with one leg lifted, "
                             f"frying pan held upright at the shoulder, cocky cute face. Centered, compact silhouette. {STYLE}",
                             [RAW / "soldier_toy_walk.png"], True, "1024x1024")
JOBS["soldier_crow_charge"] = (f"The same enemy character as the attached image, same design, colors and proportions, front view facing the camera, "
                               f"full body, now thrusting the frying pan forward at the viewer with both wings while a big burst of purple and orange "
                               f"flame jets out of the pan toward the viewer, fierce grinning face. Centered, compact silhouette. {STYLE}",
                               [RAW / "soldier_crow_walk.png"], True, "1024x1024")
for key in ("toy", "ginger", "frog", "crow"):
    for pose, desc in POSES.items():
        JOBS[f"soldier_{key}_{pose}"] = (f"The same enemy character as the attached image, same design, colors and proportions, "
                                         f"front view facing the camera, full body, now {desc.replace('sabre', 'weapon')}. Centered, compact silhouette. {STYLE}",
                                         [RAW / f"soldier_{key}_walk.png"], True, "1024x1024")


def main():
    g.JOBS.update(JOBS)
    names = sys.argv[1:] or [n for n in JOBS if not (RAW / f"{n}.png").exists()]
    walks = [n for n in names if n.endswith("_walk")]  # the other poses are drawn from the walk
    for n in walks:
        names.remove(n)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, walks):
            print(f"{'OK ' if ok else 'ERR'} {name}", flush=True)
    with cf.ThreadPoolExecutor(max_workers=3) as pool:
        for name, ok, tail in pool.map(g.run, names):
            print(f"{'OK ' if ok else 'ERR'} {name}" + ("" if ok else f"\n{tail}"), flush=True)


if __name__ == "__main__":
    main()
