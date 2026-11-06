from __future__ import annotations

import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent

REQUIRED = [
    "README_START_HERE.md", "DECISIONS.md", "GAME_SPEC.md", "MVP_SCOPE.md",
    "UX_UI_SPEC.md", "ART_BIBLE.md", "SOUND_SPEC.md", "TECH_SPEC.md",
    "ACCEPTANCE.md", "AGENTS.md", "CLAUDE.md", "START_CLAUDE_CODE.txt",
    "IMPLEMENTATION_HANDOFF.md",
    "TEST_EVIDENCE.md",
    "data/game_config.json", "data/patterns.json", "data/content_catalog.json",
    "data/asset_manifest.csv", "art/IMAGE_INDEX.md",
    "prototype_web/index.html", "prototype_web/styles.css", "prototype_web/src/app.js",
    "prototype_web/src/engine.js", "prototype_web/tests/engine.test.js", "prototype_web/package.json",
    "unity_project/Packages/manifest.json", "unity_project/ProjectSettings/ProjectVersion.txt",
    "unity_project/Assets/RhythmRunner/Runtime/Core/Domain.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Core/RunEngine.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Persistence/ProfileStore.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Core/RunConfig.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Core/ClockBridge.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Presentation/AudioDirector.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Presentation/GameApp.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Presentation/InputRouter.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Presentation/PlayfieldView.cs",
    "unity_project/Assets/RhythmRunner/Runtime/Presentation/SafeAreaFitter.cs",
    "unity_project/Assets/RhythmRunner/Tests/EditMode/RunEngineTests.cs",
    "unity_project/Assets/RhythmRunner/Tests/EditMode/ClockBridgeTests.cs",
    "prototype_web/src/clock.js",
    "tools/art/generate_assets.py", "tools/art/process_assets.py"
]

errors: list[str] = []
for rel in REQUIRED:
    if not (ROOT / rel).is_file():
        errors.append(f"missing: {rel}")

def load_json(rel: str):
    with (ROOT / rel).open(encoding="utf-8") as fh:
        return json.load(fh)

if not errors:
    config = load_json("data/game_config.json")
    patterns = load_json("data/patterns.json")
    catalog = load_json("data/content_catalog.json")
    object_ids = {item["id"] for item in catalog["objects"]}
    min_gap = float(config["run"]["minimumIntervalBeats"])
    for pattern in patterns["patterns"]:
        if pattern["durationBeats"] != 8:
            errors.append(f"{pattern['id']}: durationBeats must be 8")
        beats = []
        event_ids = set()
        for event in pattern["events"]:
            beat = float(event["beat"])
            if not 0 <= beat < 8:
                errors.append(f"{pattern['id']}: beat out of range {beat}")
            if beat * 2 != int(beat * 2):
                errors.append(f"{pattern['id']}: unsupported subdivision {beat}")
            if event["lane"] not in (0, 1, 2):
                errors.append(f"{pattern['id']}: invalid lane")
            if event["gesture"] not in ("tap", "hold", "slide", "mash"):
                errors.append(f"{pattern['id']}: gesture must be tap, hold, slide or mash")
            if event["gesture"] == "slide":
                # round 11: a slide turns on whole beats, at least a beat apart, changing lane each time
                path, turns = event.get("path") or [], event.get("turns") or []
                if not path or len(path) != len(turns):
                    errors.append(f"{pattern['id']}: slide needs matching path and turns")
                last, lane = 0.0, event["lane"]
                for to, at in zip(path, turns):
                    if at - last < 1 or at > float(event.get("length", 0)) - 1 or (beat + at) != int(beat + at) or to == lane or to not in (0, 1, 2):
                        errors.append(f"{pattern['id']}: invalid slide turn at {at}")
                    last, lane = at, to
            if event["gesture"] != "tap" and not 1 <= float(event.get("length", 0)) <= 4:
                errors.append(f"{pattern['id']}: hold/mash length must be 1-4 beats")
            if event["objectId"] not in object_ids:
                errors.append(f"{pattern['id']}: unknown object {event['objectId']}")
            if event["id"] in event_ids:
                errors.append(f"{pattern['id']}: duplicate event id {event['id']}")
            event_ids.add(event["id"])
            if event["objectId"] != "object_bomb":  # round 27: bombs are never cut, so they never crowd the hand
                beats.append((beat, beat + (float(event.get("length", 0)) if event["gesture"] != "tap" else 0)))
        beats.sort()
        for (_, a), (b, _) in zip(beats, beats[1:]):
            if b - a < min_gap:
                errors.append(f"{pattern['id']}: interval below {min_gap}")
    with (ROOT / "data/asset_manifest.csv").open(encoding="utf-8", newline="") as fh:
        rows = list(csv.DictReader(fh))
        if not rows:
            errors.append("asset manifest is empty")
    resources_patterns = ROOT / "unity_project/Assets/RhythmRunner/Resources/patterns.json"
    if not resources_patterns.is_file():
        errors.append("Unity Resources patterns.json is missing")
    elif resources_patterns.read_bytes() != (ROOT / "data/patterns.json").read_bytes():
        errors.append("Unity Resources patterns.json is out of sync")
    try:
        load_json("prototype_web/package.json")
        load_json("unity_project/Packages/manifest.json")
    except (json.JSONDecodeError, OSError) as exc:
        errors.append(f"project JSON error: {exc}")

    for source in (ROOT / "unity_project/Assets/RhythmRunner").rglob("*.cs"):
        text = source.read_text(encoding="utf-8")
        if text.count("{") != text.count("}"):
            errors.append(f"unbalanced braces: {source.relative_to(ROOT)}")

if errors:
    for error in errors:
        print(f"ERROR: {error}")
    raise SystemExit(1)

print("OK: RHYTHM SLASH RUNNER package is internally readable.")

