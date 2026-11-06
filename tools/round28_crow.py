"""Round 28 (owner: "カラスが難しすぎた"): the crow cook keeps its random mix of lane hops and the held flame, but no
offbeats. Rewrites the soldier_crow_* patterns (and their mirrors) in data/patterns.json and the Unity copy."""
import json
from round27_patterns import PATHS, dump

C = "object_soldier_crow"
# (key, tier, [(beat, lane, gesture, length)])
BASE = [
    ("a", 1, [(1, 0, "tap", 0), (2, 1, "tap", 0), (3, 1, "hold", 2), (6, 2, "tap", 0)]),
    ("b", 2, [(1, 2, "tap", 0), (2, 1, "tap", 0), (3, 0, "tap", 0), (4, 0, "hold", 2), (7, 1, "tap", 0)]),
    ("c", 2, [(1, 1, "tap", 0), (2, 0, "tap", 0), (3, 0, "hold", 2), (6, 1, "tap", 0), (7, 2, "tap", 0)]),
    ("d", 3, [(1, 0, "hold", 2), (4, 1, "tap", 0), (5, 2, "tap", 0), (6, 1, "tap", 0)]),
    ("e", 3, [(1, 2, "tap", 0), (2, 1, "tap", 0), (3, 0, "tap", 0), (5, 1, "hold", 2)]),
    ("f", 4, [(1, 1, "tap", 0), (2, 2, "hold", 2), (5, 1, "tap", 0), (6, 0, "tap", 0), (7, 1, "tap", 0)]),
]


def crow_patterns():
    out = []
    for key, tier, evs in BASE:
        for mirror in (False, True):
            events = []
            for i, (beat, lane, g, length) in enumerate(evs, 1):
                e = {"id": f"e{i:02d}", "beat": beat, "lane": 2 - lane if mirror else lane, "objectId": C, "gesture": g}
                if g != "tap":
                    e["length"] = length
                events.append(e)
            out.append({"id": f"soldier_crow_{key}{'_m' if mirror else ''}", "durationBeats": 8, "difficultyTier": tier,
                        "weight": 0.5, "role": "soldier", "events": events})
    return out


def main():
    doc = json.loads(PATHS[0].read_text(encoding="utf-8"))
    new = {p["id"]: p for p in crow_patterns()}
    doc["patterns"] = [new.get(p["id"], p) for p in doc["patterns"]]
    text = dump(doc)
    for path in PATHS:
        path.write_text(text, encoding="utf-8", newline="")
    print(len(new), "crow patterns, offbeats:", sum(1 for p in new.values() for e in p["events"] if e["beat"] % 1))


if __name__ == "__main__":
    main()
