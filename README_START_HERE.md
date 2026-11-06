# RHYTHM SLASH RUNNER 制作パッケージ

仕様版: 2.0 / 2026-10-08

このフォルダは、Unity製スマートフォンゲームの仕様、コンセプト画像、実装済み試作、テスト、Claude Codeへの引き継ぎ資料をまとめた制作パッケージです。

## ゲームを一文で

ハムスターが自動で走り続け、画面奥から迫る食材や物体を、手前の斬撃ラインで音楽に合わせて切り続ける縦画面エンドレス・リズムゲーム。

## 最初に読む順番

1. `DECISIONS.md`
2. `GAME_SPEC.md`
3. `MVP_SCOPE.md`
4. `UX_UI_SPEC.md`
5. `TECH_SPEC.md`
6. `ART_BIBLE.md` と `art/IMAGE_INDEX.md`
7. `SOUND_SPEC.md`
8. `ACCEPTANCE.md`
9. `AGENTS.md`
10. `CLAUDE.md`
11. `IMPLEMENTATION_HANDOFF.md`

## 現在入っている実装（2026-10-12）

- `unity_project/`: **Unity 6000.3.22f1** の本体。3レーン・120BPM・8拍チャンク・裏拍・P/G/M・HP3・コンボ・距離・コイン・購入/装備・設定・一時停止・結果まで遊べる。Input Systemのイベント時刻をDSPへ変換する`ClockBridge`実装済み。EditModeテスト28件成功。
- `Builds/Windows/RhythmSlashRunner.exe`: PCで試遊できるビルド（540×960ウィンドウ）。
- `Builds/Android/RhythmSlashRunner.apk`: Android用APK（実機未検証）。
- `prototype_web/`: 同じルール・データ・素材のブラウザ版（`npm start`）。テスト19件成功。
- `unity_project/Assets/RhythmRunner/Resources/Art/`: コンセプト画像を元に生成したキャラ・対象・背景・UI・VFX素材41点（`tools/art/`で再生成可能）。
- 実行確認と未確認の区別は `TEST_EVIDENCE.md`、残課題は `DEVELOPMENT_STATUS.md`。

## すぐ遊ぶ

- PC: `Builds/Windows/RhythmSlashRunner.exe` を起動。下部の左・中・右をクリック、または `A S D` キー。`Esc`で一時停止。
- ブラウザ: `cd prototype_web` → `npm start` → `http://127.0.0.1:4173`
- Android: `Builds/Android/RhythmSlashRunner.apk` を端末へインストール（提供元不明アプリの許可が必要）。

## 正本

- ゲーム設定: `data/game_config.json`
- 譜面パターン: `data/patterns.json`
- コンテンツ: `data/content_catalog.json`
- 素材台帳: `data/asset_manifest.csv`
- ゲームルール: `GAME_SPEC.md`
- 技術: `TECH_SPEC.md`
- 画面: `UX_UI_SPEC.md`

文書と JSON が矛盾する場合、実装値は JSON を優先し、仕様変更として `DECISIONS.md` に理由と影響を記録してから関連文書を揃えます。

## 固定した制作方針

- Unity 6000.3 LTS + C#、縦 9:16、2.5D（生成2Dスプライトの遠近配置）、固定カメラ。
- 自動走行。MVPでは左右移動、ジャンプ、回避操作を入れない。
- 3レーンをタップして斬る。方向スワイプは Phase 2。
- 1チャンク8拍。止まらず連結する。旧「見本8拍→再現8拍」はMVPへ混ぜない。
- 120 BPM固定。表拍から始め、後半に予告付きの裏拍を追加する。
- HP 3。誤レーンまたは取り逃しで1減少し、0で終了。
- キャラ、武器、背景は外見と音だけを変更し、判定や性能は変えない。
- 完全ランダム生成ではなく、検証済みパターンを制約付きで連結する。
- コンセプト画像は方向資料。完成UI、3Dモデル、配布可能素材ではない。

## Claude Codeへの渡し方

このフォルダ全体をClaude Codeの作業場所へ置き、`START_CLAUDE_CODE.txt` の文章を送ってください。`unity_project/`には既存実装があるため、作り直さず、最初にUnityでコンパイル・EditModeテスト・Play確認を行います。

