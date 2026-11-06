# 実装引き継ぎ

更新: 2026-10-12

## 現在の到達点

### Unity（本体）`unity_project/` — Unity 6000.3.22f1

空Sceneで`GameApp`が自動生成され、全画面がコードで組み立てられる。

| 層 | 主なクラス |
|---|---|
| Core（純粋C#・EditModeテスト対象） | `RunEngine`（判定・HP・得点・チャンク選択・裏拍予告）、`PatternValidator`、`RunConfig`（game_config.json）、`ClockBridge`（入力時刻→DSP）、`Catalog`、`Domain` |
| Presentation | `GameApp`（状態遷移: Boot→Menu→CountIn→Running⇄Paused/Resuming→Ending→Result）、`PlayfieldView`、`RoadGraphic`/`Perspective`（奥行き式の唯一の定義）、`HudView`、`InputRouter`、`AudioDirector`、`ArtLibrary`、`UiKit`、`Haptics`、`Automation` |
| Persistence | `ProfileStore`（報酬・購入・設定保存） |
| Editor | `BuildTools`（Windows/Android）、`ArtImportPostprocessor` |

### ブラウザ版 `prototype_web/`

Unityと同じ規則（engine.js ⇔ RunEngine.cs）、同じ`data/*.json`、同じスプライト（`/art/`）を使用。`src/clock.js`が入力→音声時計の変換。

### 素材パイプライン

`tools/art/generate_assets.py`（生成リストとプロンプト、`art/`のコンセプト画像を参照）→ `art_generated/raw/` → `tools/art/process_assets.py` → `unity_project/Assets/RhythmRunner/Resources/Art/`（41点）。素材を差し替える場合は同名PNGを置けば描画側の変更は不要。欠けた素材は図形で代替され、ログに`Missing art`と出る。

## 時計と入力（変更時の注意）

- 判定時刻 = `ClockBridge.ToDsp(InputEvent.time) - calibrationMs` を拍へ換算。処理フレームの時刻は使わない。
- 入力を先に処理し、その後に期限切れMISSを処理する（同時刻は入力優先）。
- 表示は `(イベント拍 - 現在拍)` から算出し、deltaTime累積を使わない。
- 0.5秒超のフレーム停止は一時停止として扱い、MISSへ変換しない。

## 次の実装優先順位

1. Android実機確認（タッチ・音ズレ・Safe Area・発熱）と判定補正初期値の決定。
2. チュートリアル（中央4回→左右→休符→裏拍2回減点なし→通常ランへ）。
3. 一時停止画面からの設定導線、出力経路別の補正保存。
4. 入力録画・再生、強制低FPSでのreplay一致テスト（PlayMode）。
5. 正式BGM（120BPM 8拍ループ）と素材音の差し替え。
6. 追加キャラ・ステージの解放（データは`content_catalog.json`、カードと見た目は用意済み）。

## 変更してはいけない部分

- 8カウントは連続チャンク。旧見本/再現をMVPへ混ぜない。
- MVPは3レーンタップ、自動走行、120 BPM、HP3。
- 武器・キャラで判定や性能を変えない（見た目と装飾音のみ）。
- transform通過や衝突フレームで採点しない。
- 広告、課金、外部送信、公開を追加しない。
