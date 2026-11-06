# Unity版 Rhythm Slash Runner

Unity **6000.3.22f1（Unity 6.3 LTS）**。2022.3からの変更理由は `../DECISIONS.md` D-008。

## 開き方・遊び方（エディタ）

1. Unity Hubで `unity_project` を追加し、6000.3.22f1で開く。
2. `Assets/RhythmRunner/Scenes/Main.unity`（空Scene）を開いてPlay。どの空Sceneでも `GameApp` が自動生成される。
3. 操作: 下部の左・中央・右をタップ／クリック、またはキーボード `A S D`（`← ↓ →`）。`Esc` / `P` で一時停止。

## ビルド

| 対象 | 方法 | 出力 |
|---|---|---|
| Windows | メニュー `Rhythm Slash Runner > Build Windows`、またはバッチ `-executeMethod RhythmSlashRunner.EditorTools.BuildTools.BuildWindows` | `../Builds/Windows/RhythmSlashRunner.exe` |
| Android APK | `powershell -File ../tools/build_android.ps1`（日本語パスをAndroidツールが扱えないため、ASCIIパスへ複製してビルド） | `../Builds/Android/RhythmSlashRunner.apk` |

## テスト

Test Runner → EditMode、またはバッチ:

```
Unity.exe -batchmode -projectPath unity_project -runTests -testPlatform EditMode -testResults results.xml
```

## 構成

| 場所 | 内容 |
|---|---|
| `Runtime/Core` | 純粋C#: `RunEngine`（判定・HP・得点・チャンク連結・裏拍予告）、`RunConfig`（game_config.json）、`ClockBridge`（入力時刻→DSP）、`Catalog`、`Domain` |
| `Runtime/Presentation` | `GameApp`（状態遷移と全画面）、`PlayfieldView`（2.5D走路・対象・ハムスター・VFX・pool）、`RoadGraphic`（遠近の共通式）、`HudView`、`InputRouter`（Input Systemのevent time）、`AudioDirector`（DSP予約の合成BGM/SE）、`Automation`（診断用自動撮影） |
| `Runtime/Persistence` | `ProfileStore`（temp→検証→置換→backup、runId報酬、購入の一括保存） |
| `Resources/` | `game_config.json` `patterns.json` `content_catalog.json`（`data/`の写し、テストで同期検査）と `Art/`（生成スプライト） |
| `Editor/` | `BuildTools`、`ArtImportPostprocessor`（Art配下を自動でSprite設定） |

## 診断起動（ビルド版）

```
RhythmSlashRunner.exe -rsrShots <保存先> -rsrAutoplay [-rsrTier 3]
```

全画面と1ランを自動で撮影して終了する。設定画面の「診断表示」ONで、拍・seed・パターン・直近の判定誤差・補正値・ClockBridgeオフセット・fpsをプレイ中に表示。
