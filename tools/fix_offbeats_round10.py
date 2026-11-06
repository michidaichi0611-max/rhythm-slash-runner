"""Round 10 (owner: "a lone offbeat that suddenly comes is what I miss"): every offbeat note is announced by a note
half a beat earlier in the SAME lane, so offbeats always read as a "ta-TA" pair. Lane changes on the 8th-note grid
(the hardest move) are removed; an offbeat after a gap gets a lead-in note. Rewrites Resources/patterns.json."""
import json, os

PATH = os.path.join(os.path.dirname(__file__), "..", "unity_project", "Assets", "RhythmRunner", "Resources", "patterns.json")
data = json.load(open(PATH, encoding="utf-8"))

def is_off(b): return abs(b - round(b)) > 1e-6

def busy(events, b):
    # a hold covers its span; nothing may be added inside it
    for e in events:
        if abs(e["beat"] - b) < 1e-6: return True
        if e["gesture"] in ("hold", "mash") and e["beat"] - 1e-6 <= b <= e["beat"] + e.get("length", 0) + 1e-6: return True
    return False

changed = 0
for pat in data["patterns"]:
    ev = sorted(pat["events"], key=lambda e: e["beat"])
    for e in list(ev):
        b = e["beat"]
        if not is_off(b): continue
        prev = [x for x in ev if abs(x["beat"] - (b - .5)) < 1e-6]
        if prev:
            if prev[0]["lane"] != e["lane"] and e["gesture"] == "tap":
                e["lane"] = prev[0]["lane"]; changed += 1
        elif not busy(ev, b - .5) and b - .5 >= 0:
            ev.append({"id": e["id"] + "a", "beat": b - .5, "lane": e["lane"], "objectId": "object_strawberry", "gesture": "tap"}); changed += 1
        else:
            # no room for a lead-in: put the note on the beat before instead
            e["beat"] = b - .5 if not busy([x for x in ev if x is not e], b - .5) else b + .5; changed += 1
        ev.sort(key=lambda x: x["beat"])
    # never jump left<->right on a half beat
    for i in range(1, len(ev)):
        a, c = ev[i - 1], ev[i]
        end = a["beat"] + (a.get("length", 0) if a["gesture"] != "tap" else 0)
        if c["beat"] - end <= .5 + 1e-6 and abs(a["lane"] - c["lane"]) == 2 and c["gesture"] == "tap":
            c["lane"] = 1; changed += 1
    pat["events"] = ev

# keep the file's layout: one event per line
out = ['{', '  "schemaVersion": %d,' % data["schemaVersion"], '  "patterns": [']
for pi, pat in enumerate(data["patterns"]):
    head = {k: v for k, v in pat.items() if k != "events"}
    out.append('    {')
    for k, v in head.items(): out.append('      %s: %s,' % (json.dumps(k), json.dumps(v, ensure_ascii=False)))
    out.append('      "events": [')
    for ei, e in enumerate(pat["events"]):
        out.append('        ' + json.dumps(e, ensure_ascii=False, separators=(",", ":")) + (',' if ei < len(pat["events"]) - 1 else ''))
    out.append('      ]')
    out.append('    }' + (',' if pi < len(data["patterns"]) - 1 else ''))
out.append('  ]')
extra = {k: v for k, v in data.items() if k not in ("schemaVersion", "patterns")}
if extra:
    out[-1] += ','
    items = list(extra.items())
    for i, (k, v) in enumerate(items): out.append('  %s: %s%s' % (json.dumps(k), json.dumps(v, ensure_ascii=False), ',' if i < len(items) - 1 else ''))
out.append('}')
open(PATH, "w", encoding="utf-8").write("\n".join(out) + "\n")
print("changes", changed)
for pat in data["patterns"]:
    offs = [e for e in pat["events"] if is_off(e["beat"])]
    if offs: print(pat["id"], len(pat["events"]), ' '.join("%g%s" % (e["beat"], "LCR"[e["lane"]]) for e in pat["events"]))

# hand fixes where the automatic rule over-filled or collided
data = json.load(open(PATH, encoding="utf-8"))
def T(i, b, l, o="object_strawberry"): return {"id": i, "beat": b, "lane": l, "objectId": o, "gesture": "tap"}
for pat in data["patterns"]:
    if pat["id"] == "offbeat_intro_01":  # the first offbeats a player meets: two clean "ta-TA" pairs
        pat["events"] = [T("e01", 0, 1), T("e02", 1, 1, "object_barrel"), T("e03", 2, 2, "object_watermelon"), T("e04", 2.5, 2),
                         T("e05", 4, 1, "object_barrel"), T("e06", 5, 1, "object_watermelon"), T("e07", 6, 0), T("e08", 6.5, 0, "object_barrel")]
    if pat["id"] == "hold_groove_01":  # the offbeat right after the hold's release is gone; one pair on the right instead
        hold = [e for e in pat["events"] if e["gesture"] == "hold"][0]
        pat["events"] = [T("e01", 0, 1, "object_watermelon"), T("e02", .5, 1), T("e03", 1, 0), hold,
                         T("e06", 5, 2), T("e07", 5.5, 2), T("e08", 6, 1, "object_watermelon"), T("e09", 7, 0)]
json.dump(data, open(PATH + ".tmp", "w", encoding="utf-8"), ensure_ascii=False)
