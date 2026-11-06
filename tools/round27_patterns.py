"""Round 27 (owner): slides are retired (the snaking ramen/candy become straight long objects), split fruit and bombs
are added. Rewrites data/patterns.json and the Unity copy in the same one-event-per-line layout."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATHS = [ROOT / "data" / "patterns.json", ROOT / "unity_project" / "Assets" / "RhythmRunner" / "Resources" / "patterns.json"]
BIG, HALF, BOMB = "object_melon_big", "object_melon_half", "object_bomb"
S, W, B = "object_strawberry", "object_watermelon", "object_barrel"


def ev(i, beat, lane, obj, gesture="tap", **kw):
    e = {"id": f"e{i:02d}", "beat": beat, "lane": lane, "objectId": obj, "gesture": gesture}
    e.update(kw)
    return e


def pattern(pid, tier, rows):
    events = []
    for i, r in enumerate(rows, 1):
        beat, lane, obj = r[:3]
        kw = r[3] if len(r) > 3 else {}
        events.append(ev(i, beat, lane, obj, **kw))
    return {"id": pid, "durationBeats": 8, "difficultyTier": tier, "weight": 1, "events": events}


NEW = [
    # split fruit: the big melon breaks, its halves roll to the other lanes and come back one and two beats later
    pattern("split_melon_a", 1, [(0, 1, S), (2, 1, BIG), (3, 0, HALF, {"from": "e02"}), (4, 2, HALF, {"from": "e02"}), (6, 1, S)]),
    pattern("split_melon_b", 2, [(1, 0, BIG), (2, 1, HALF, {"from": "e01"}), (3, 2, HALF, {"from": "e01"}),
                                 (5, 2, BIG), (6, 1, HALF, {"from": "e04"}), (7, 0, HALF, {"from": "e04"})]),
    pattern("split_melon_c", 3, [(0, 1, S), (1, 1, BIG), (2, 0, HALF, {"from": "e02"}), (3, 2, HALF, {"from": "e02"}), (3.5, 2, S),
                                 (4, 1, S), (5, 1, BIG), (6, 2, HALF, {"from": "e07"}), (7, 0, HALF, {"from": "e07"})]),
    pattern("split_melon_d", 4, [(0, 0, BIG), (1, 1, HALF, {"from": "e01"}), (1.5, 1, S), (2, 2, HALF, {"from": "e01"}), (3, 2, S),
                                 (4, 2, BIG), (5, 1, HALF, {"from": "e06"}), (5.5, 1, S), (6, 0, HALF, {"from": "e06"}), (7, 1, W)]),
    # bombs sit where the hand wants to go next ("タン・タン・(爆弾)") — never cut them
    pattern("bomb_a", 2, [(0, 1, S), (1, 1, S), (2, 1, BOMB), (3, 1, W), (4, 0, S), (5, 0, S), (6, 0, BOMB), (7, 2, B)]),
    pattern("bomb_b", 2, [(0, 0, S), (1, 0, S), (2, 0, BOMB), (2, 1, W), (3, 1, S), (4, 2, S), (5, 2, BOMB), (5, 1, S), (6, 1, W)]),
    pattern("bomb_c", 3, [(0, 1, S), (1, 1, S), (1.5, 1, S), (3, 1, BOMB), (3, 0, W), (4, 0, S), (5, 0, BOMB), (5, 2, B), (6, 2, S), (6.5, 2, S)]),
    pattern("bomb_d", 4, [(0, 1, S), (1, 0, BOMB), (1, 2, S), (2, 2, BOMB), (2, 1, S), (3, 0, S), (3.5, 0, S), (4.5, 0, BOMB),
                          (5, 1, W), (6, 1, S), (7, 1, BOMB), (7, 2, B)]),
]
RENAME = {"slide_intro_01": "long2_intro_01", "slide_wave_01": "long2_wave_01", "slide_snake_01": "long2_snake_01",
          "slide_double_01": "long2_double_01", "slide_cross_01": "long2_cross_01"}


def dump(doc):
    out = ["{", f'  "schemaVersion": {doc["schemaVersion"]},', '  "patterns": [']
    blocks = []
    for p in doc["patterns"]:
        head = {k: v for k, v in p.items() if k != "events"}
        lines = ",\n".join("        " + json.dumps(e, ensure_ascii=False, separators=(",", ":")) for e in p["events"])
        blocks.append("    " + json.dumps(head, ensure_ascii=False)[:-1] + ', "events": [\n' + lines + "\n      ]\n    }")
    out.append(",\n".join(blocks))
    out += ["  ]", "}", ""]
    return "\n".join(out)


def main():
    doc = json.loads(PATHS[0].read_text(encoding="utf-8"))
    ids = {p["id"] for p in doc["patterns"]}
    for p in doc["patterns"]:
        if p["id"] in RENAME:
            p["id"] = RENAME[p["id"]]
        for e in p["events"]:
            if e["gesture"] == "slide":
                # a straight long object in the lane it started in (the "second long object" slot: ramen in its stages)
                e["gesture"] = "hold"
                e.pop("path", None); e.pop("turns", None)
    doc["patterns"] = [p for p in doc["patterns"] if p["id"] not in {n["id"] for n in NEW}] + NEW
    text = dump(doc)
    json.loads(text)
    for path in PATHS:
        path.write_text(text, encoding="utf-8", newline="")
    print(len(doc["patterns"]), "patterns")


if __name__ == "__main__":
    main()
