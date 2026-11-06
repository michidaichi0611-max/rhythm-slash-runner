# ブラウザ試作

Unity版と同じルール（`src/engine.js` ⇔ `RunEngine.cs`）とデータ（`../data/*.json`）、同じスプライト（`../unity_project/Assets/RhythmRunner/Resources/Art`）を使う操作検証版。

## 起動

```
cd prototype_web
npm start
```

`http://127.0.0.1:4173` を開く。Node.js 18以上（動作確認は v24.20.0）。サーバーは `/data/` と `/art/` を正本フォルダから配信する。

## 操作

- 下部の左・中央・右をタップ、またはキーボード `A S D` / `← ↓ →`
- `Esc` / `P` で一時停止。タブを離れると自動停止、再開時は3拍のカウント。
- 設定: 音量、判定補正（-250〜+250ms）、振動、診断表示、テスト難易度（Tier 0〜5固定）

## テスト

```
npm test
```

判定境界、二重解決、3MISS、空打ち、誤レーン、裏拍精度、難易度曲線、200seedのチャンク境界、ClockBridge、30/60/120Hzでの判定一致、データとUnity Resourcesの同期を検査。

## 時計

音声時計（AudioContext）が基準。入力は `Event.timeStamp` を `ClockBridge`（`src/clock.js`）で音声時計へ変換して判定する。拍音・合図音は音声時計上に先行予約する。
