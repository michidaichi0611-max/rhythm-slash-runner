# 技術実装仕様

## 1. エンジンと表現

Unity + C#。既存プロジェクトがある場合はそのUnity版とパッケージを調査して固定し、制作中に無断更新しない。固定カメラの2.5D、3Dモデルと2Dレーン入力を組み合わせる。Web版との同時完成を目指さない。

## 2. シーンと状態

- `Boot`: Catalog、設定、保存、依存関係を初期化。
- `Main`: Home、Loading、CountIn、Running、Paused、Resultを状態とUIで切替。

```text
Boot → Home → Loading → CountIn → Running
Running → Paused → ResumeCountIn → Running
Running → Ending → PendingSave → Result
Result → Home / Retry
```

## 3. 共通時計

`AudioSettings.dspTime`をBGMと譜面の基準にし、BGMは`AudioSource.PlayScheduled`で予約する。

```text
secondsPerBeat = 60 / bpm
currentBeat = (dspNow - musicStartDsp) / secondsPerBeat
hitDsp = musicStartDsp + absoluteBeat * secondsPerBeat
spawnDsp = hitDsp - spawnLeadBeats * secondsPerBeat
errorMs = (convertedInputDsp - hitDsp) * 1000 - calibrationMs
```

Input Systemのイベント時刻はDSPとは別時計なので`ClockBridge`で変換する。処理したフレーム時刻を入力時刻にしない。正の補正値は入力を早いものとして扱う、と符号を統一する。

対象の奥行きは現在拍から直接算出する。Rigidbody衝突、transformが線を越えたフレーム、`deltaTime`累積で採点しない。VFXやアニメーションの完了を待って音楽時計を止めない。

## 4. レーンと入力

- Safe Area内のPlayfieldRectを正規化。
- レーン中心xは`1/6, 1/2, 5/6`。
- 斬撃ラインy初期値0.72。
- 下部タップ領域を横3分割。
- 単一pointer。入力debounce 100ms。
- 最小ノーツ間隔0.5拍。同拍複数対象は禁止。

判定順:

1. 入力時刻±160msの未解決イベントを検索。
2. 正しいレーンの最短時差イベントを1件だけ解決。
3. 正しいレーンがなく別レーンが窓内なら誤レーンMISSとして消費。
4. 候補なしは空打ち。comboを0、HPは維持。
5. 期限切れMISSと入力を発生時刻順に統合処理。同時刻は入力を先に評価。
6. HP0以後の得点、入力、報酬を受け付けない。

## 5. モジュール

| Class | 責務 |
|---|---|
| GameBootstrap | 初期化と依存関係 |
| RunDirector | run状態、HP0、pause、終了 |
| RhythmClock | DSP原点、拍秒変換 |
| ClockBridge | input clockからDSPへの変換 |
| InputRouter | UIとプレイ入力、単一pointer |
| TapJudge | 時刻とレーンの純粋判定 |
| EventLedger | イベント一意解決、二重防止 |
| PatternGenerator | seedとtierによるチャンク選択 |
| EventScheduler | 先読み、出現、期限処理 |
| FlightPresenter | 時刻から対象位置を表示 |
| SlicePresenter | 分割Prefab、武器、VFX |
| AudioDirector | BGM予約、拍、斬撃音 |
| PoolService | 対象、破片、VFX、音源再利用 |
| RunStats | 得点、combo、距離、精度 |
| RewardService | 終了報酬を一度だけ計算 |
| SaveRepository | atomic保存、backup、復旧 |
| CatalogRepository | コンテンツIDの解決 |
| CalibrationController | 補正計測・保存 |
| DiagnosticRecorder | 入力時差、frame time、異常 |

`TapJudge`、`RunStats`、パターン意味検証は可能な限りMonoBehaviourやPrefabに依存しない純粋C#としてEditModeテストする。

## 6. エンドレス生成

完全ランダムではなく`data/patterns.json`から難易度tierに合う8拍パターンをseed付きで選ぶ。globalなUnity Randomに依存しない。

生成制約:

- 直前チャンク末尾との間隔を含め0.5拍以上。
- 0.5拍間隔の左右端移動は禁止。
- 同拍複数、未解放gesture、同イベントIDを禁止。
- 同一パターン過剰連続を避ける。
- 生成失敗時は`basic_center_01`へfallback。
- 飛来リードを超える2〜3チャンクを先読み。
- 過去の全イベントをメモリに残さず、集計値だけ保持。

## 7. 保存

Profile: schemaVersion、generation、coinBalance、ownedIds、equippedIds、bests、settings、appliedRewardRunIds。

- run開始時にrunIdを発行。
- 終了時、報酬、ベスト、runId適用済みを同一snapshotで保存。
- 一時ファイル→検証→正式置換→直前backup。
- 購入は残高減少と所有追加を1 transactionとして保存。
- 保存失敗を成功表示しない。
- 途中runのプロセス終了後復元はMVP外。

## 8. Poolと性能

対象、分割片、斬撃、浮き文字、短い音源をpool。返却時に購読、timer、judgment、mesh、色を初期化。破片は表示用で床との大量衝突をしない。見えている対象とVFXの上限を設定し、初回使用素材をプレイ前にpreloadする。

毎フレームのLINQ、文字列、配列確保を避け、判定は近傍イベントのみ検索。30/60/120Hzの入力replay結果を一致させる。

## 9. 推奨Unityフォルダ

```text
Assets/RhythmRunner/
  Runtime/{Core,Clock,Input,Judgment,Generation,Presentation,Audio,Persistence,Economy,UI,Diagnostics}/
  Data/{Patterns,RunPresets,Catalogs}/
  Art/{Characters,Weapons,Objects,Environments,VFX}/
  Audio/{Music,SFX}/
  Prefabs/
  Scenes/
  Editor/Validators/
  Tests/{EditMode,PlayMode}/
Documentation/
```

## 10. JSON検証

- `additionalProperties:false`を基本。
- durationBeats=8、`0 <= beat < 8`、laneは0/1/2。
- MVP gestureはtapのみ。
- 表拍・裏拍だけなら`beat*2`が整数。
- pattern内event idは一意。
- objectIdがcatalogに存在。
- 同拍、最小間隔、境界間隔はSchema以外の意味検証も行う。
- 不正データはrun開始を拒否し、プレイヤーのMISSにしない。

## 11. 禁止事項

- 旧見本・再現ループをMVPへ混ぜる。
- 物理衝突や描画フレームでリズム判定する。
- DSP clockとinput clockを直接差し引く。
- 武器スキンで判定や攻撃性能を変える。
- 完全ランダム配置をプレイヤーの失敗として扱う。
- pauseでHPやscoreを巻き戻す。
- Result表示ごとに報酬を付与する。
- Editor起動を実機確認済みと報告する。
- 未実行テストを完了扱いにする。

