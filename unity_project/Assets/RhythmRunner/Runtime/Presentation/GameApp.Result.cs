using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;
using RhythmSlashRunner.Persistence;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Result: rows reveal one per 0.3 s with count-up ticks, coins roll like an odometer (fixed value, no slot reels),
    /// a fountain of coins flies into the wallet, then the new-record stamp or the "so close" line. Tap skips to the end.
    /// </summary>
    public sealed partial class GameApp
    {
        Text[] resultValues;
        Text[] resultLabels;
        Text resultCoins, resultCoinDetail, resultWallet, saveErrorText, resultCloseLine, resultProgressLabel;
        Text resultTitle;
        Image resultHamster, resultRays, resultRibbon, resultRecordLogo, resultProgressFill;
        RectTransform resultWalletIcon, resultFountain, resultProgress;
        Button resultRetry, resultHome, resultSkip;
        Coroutine resultRoutine;
        bool resultSkipped;

        static readonly string[] ResultRowNames = { "距離", "スコア", "最大コンボ", "PERFECT / GOOD / MISS" };

        void BuildResult()
        {
            resultScreen = MakeScreen("Result", true);
            Background(resultScreen, "kitchen_backdrop", new Color(.35f, .35f, .45f));
            RectTransform content = UiKit.Rect("Content", resultScreen.transform, Vector2.zero, Vector2.one);
            content.gameObject.AddComponent<SafeAreaFitter>();
            Image skipHit = content.gameObject.AddComponent<Image>(); skipHit.color = new Color(0, 0, 0, 0);
            resultSkip = content.gameObject.AddComponent<Button>(); resultSkip.transition = Selectable.Transition.None;
            resultSkip.onClick.AddListener(() => resultSkipped = true);

            resultRays = UiKit.Image("Rays", content, ArtLibrary.Rays, new Color(1f, .85f, .45f, 0), Vector2.zero, Vector2.zero);
            UiKit.At(resultRays.rectTransform, new Vector2(.5f, .8f), new Vector2(1200, 1200));
            UiKit.Image("Glow", content, ArtLibrary.Glow, new Color(1f, .8f, .4f, .55f), new Vector2(.15f, .66f), new Vector2(.85f, .98f));
            resultHamster = UiKit.Image("Hamster", content, ArtLibrary.Get("hamster_front_cheer"), Color.white, new Vector2(.3f, .69f), new Vector2(.7f, .95f), true);

            Image walletPill = UiKit.Image("Wallet", content, ArtLibrary.Pill, UiKit.Ink, Vector2.zero, Vector2.zero);
            UiKit.At(walletPill.rectTransform, new Vector2(1, 1), new Vector2(300, 88), new Vector2(1, 1)).anchoredPosition = new Vector2(-28, -24);
            Image wIcon = UiKit.Image("Icon", walletPill.transform, ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle, Color.white, Vector2.zero, Vector2.zero, true);
            resultWalletIcon = UiKit.At(wIcon.rectTransform, new Vector2(0, .5f), new Vector2(70, 70));
            resultWalletIcon.anchoredPosition = new Vector2(46, 0);
            resultWallet = UiKit.Label("0", walletPill.transform, 48, UiKit.Gold, new Vector2(.32f, .05f), new Vector2(.92f, .95f), TextAnchor.MiddleRight);

            resultTitle = UiKit.Label("RESULT", content, 110, UiKit.Gold, new Vector2(.1f, .61f), new Vector2(.9f, .7f), TextAnchor.MiddleCenter, FontStyle.Bold, true); // real weight, no synthetic italic (letters split)
            resultTitle.resizeTextForBestFit = false;
            resultRibbon = UiKit.Image("Ribbon", content, ArtLibrary.Get("record_ribbon"), Color.white, new Vector2(.1f, .565f), new Vector2(.9f, .715f), true);
            resultRecordLogo = UiKit.Image("RecordLogo", content, ArtLibrary.Get("logo_new_record"), Color.white, new Vector2(.16f, .59f), new Vector2(.84f, .7f), true);

            Image card = UiKit.Image("Card", content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(.06f, .255f), new Vector2(.94f, .565f));
            resultLabels = new Text[ResultRowNames.Length];
            resultValues = new Text[ResultRowNames.Length];
            for (int i = 0; i < ResultRowNames.Length; i++)
            {
                float y1 = .96f - i * .135f, y0 = y1 - .12f;
                resultLabels[i] = UiKit.Label(ResultRowNames[i], card.transform, 40, Color.white, new Vector2(.06f, y0), new Vector2(.62f, y1), TextAnchor.MiddleLeft, FontStyle.Normal);
                resultValues[i] = UiKit.Label("", card.transform, 50, UiKit.Cyan, new Vector2(.5f, y0), new Vector2(.94f, y1), TextAnchor.MiddleRight, FontStyle.Bold);
                resultValues[i].resizeTextForBestFit = false;
            }
            Image coinLine = UiKit.Image("CoinLine", card.transform, ArtLibrary.Rounded, new Color(1f, .78f, .29f, .14f), new Vector2(.03f, .2f), new Vector2(.97f, .4f));
            Image coinIcon = UiKit.Image("Coin", coinLine.transform, ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle, Color.white, new Vector2(.02f, .1f), new Vector2(.14f, .9f), true);
            UiKit.Label("コイン", coinLine.transform, 42, UiKit.Gold, new Vector2(.16f, .05f), new Vector2(.5f, .95f), TextAnchor.MiddleLeft, FontStyle.Bold);
            resultCoins = UiKit.Label("+0", coinLine.transform, 64, UiKit.Gold, new Vector2(.45f, .02f), new Vector2(.97f, .98f), TextAnchor.MiddleRight, FontStyle.BoldAndItalic, true);
            resultCoins.resizeTextForBestFit = false;
            resultCoinDetail = UiKit.Label("", card.transform, 28, new Color(1, 1, 1, .8f), new Vector2(.05f, .02f), new Vector2(.95f, .19f), TextAnchor.MiddleCenter, FontStyle.Normal);
            resultCloseLine = UiKit.Label("", content, 38, Color.white, new Vector2(.08f, .21f), new Vector2(.92f, .25f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            saveErrorText = UiKit.Label("保存に失敗しました", content, 36, UiKit.Coral, new Vector2(.1f, .21f), new Vector2(.9f, .25f));

            Image progressBack = UiKit.Image("Progress", content, ArtLibrary.Pill, new Color(1, 1, 1, .15f), new Vector2(.12f, .185f), new Vector2(.88f, .205f));
            resultProgress = progressBack.rectTransform;
            resultProgressFill = UiKit.Image("Fill", progressBack.transform, ArtLibrary.Pill, UiKit.Gold, Vector2.zero, new Vector2(0, 1));
            resultProgressLabel = UiKit.Label("", content, 30, Color.white, new Vector2(.1f, .2f), new Vector2(.9f, .23f), TextAnchor.MiddleCenter, FontStyle.Normal);

            resultFountain = UiKit.Rect("Fountain", content, Vector2.zero, Vector2.one);
            resultRetry = UiKit.Button("", content, UiKit.Cyan, new Vector2(.1f, .085f), new Vector2(.9f, .175f), OnRetryPressed, 72, true);
            UiKit.Image("Icon", resultRetry.transform, ArtLibrary.Play, Color.white, new Vector2(.2f, .25f), new Vector2(.3f, .75f), true);
            UiKit.Label("もう一度", resultRetry.transform, 76, Color.white, new Vector2(.28f, .08f), new Vector2(.86f, .92f));
            resultRetry.gameObject.AddComponent<Breathe>().Amount = .03f;
            resultHome = UiKit.Button("ホーム", content, UiKit.NavyLight, new Vector2(.06f, .015f), new Vector2(.46f, .075f), () => { audioDirector.Play(Sfx.UiBack); QuitToHome(); }, 48);
            Button cardButton = UiKit.Button("結果カード", content, UiKit.Gold, new Vector2(.5f, .015f), new Vector2(.94f, .075f), () => { audioDirector.Play(Sfx.UiConfirm); OpenCard(); }, 46);
            cardButton.GetComponentInChildren<Text>().color = UiKit.Navy;
            BuildCard();
        }

        void FinishRun(double beat)
        {
            finalSnapshot = engine.Snapshot(beat);
            profile.tutorialDone = true; // saved with the reward below
            previousBestDistance = profile.bestDistance;
            int previousScore = profile.bestScore;
            finalBestBonus = ProfileStore.NewBestBonus(profile, finalSnapshot);
            rewardSaved = TryApplyReward();
            finalNewRecord = rewardSaved && finalSnapshot.Distance > 0 && finalSnapshot.Distance > previousBestDistance;
            OnRunFinishedStore(finalSnapshot, engine.BossActive);
            profile.runsPlayed++;
            Analytics.Event("run_end", "distance", finalSnapshot.Distance, "score", finalSnapshot.Score, "stage", finalSnapshot.Stage,
                "bosses", engine.BossesDefeated, "max_combo", finalSnapshot.MaxCombo, "lost_in_boss", engine.BossActive, "run_no", profile.runsPlayed,
                "hearts_lost_to", LossLine(), "character", profile.equippedCharacter);
            ApplyDailyGoals();
            if (refundedThisRun) dailyLine += (dailyLine.Length > 0 ? "\n" : "") + "ボス戦でのゲームオーバー：プレイ回数を1回 返しました";
            ProfileStore.TrySave(profile);
        }

        // today's goals (owner round 10): progress from this run, coins for each newly completed goal
        int dailyCoins;
        string dailyLine = "";

        static string Today() { return DateTime.Now.ToString("yyyyMMdd"); }

        void ApplyDailyGoals()
        {
            dailyCoins = 0; dailyLine = "";
            if (!rewardSaved) return;
            var stats = new RunStats
            {
                Distance = finalSnapshot.Distance, Score = finalSnapshot.Score, Perfect = finalSnapshot.Perfect, MaxCombo = finalSnapshot.MaxCombo,
                OffbeatPerfect = offbeatPerfect, HoldsCompleted = engine.HoldsCompleted, BossesDefeated = engine.BossesDefeated, HeartsTaken = heartsTaken,
            };
            List<DailyGoals.Goal> done = DailyGoals.Apply(profile, Today(), stats);
            dailyCoins = done.Count * DailyGoals.RewardCoins;
            profile.coinBalance += dailyCoins;
            ProfileStore.TrySave(profile);
            foreach (DailyGoals.Goal g in done) dailyLine += (dailyLine.Length > 0 ? "\n" : "") + "今日の目標「" + g.Text + "」達成！ +" + DailyGoals.RewardCoins;
            if (done.Count > 0 && DailyGoals.DoneCount(profile) >= DailyGoals.Count)
            {
                // round 32: all of today's goals → an energy drink
                profile.drinks++;
                ProfileStore.TrySave(profile);
                dailyLine += "\n今日の目標を ぜんぶ達成！ エナジードリンク +1";
            }
        }

        bool TryApplyReward()
        {
            try { finalReward = ProfileStore.ApplyRunReward(profile, runId, finalSnapshot); return true; }
            catch (Exception e) { Debug.LogException(e); finalReward = RunEngine.CoinsForRun(finalSnapshot) + finalBestBonus; return false; }
        }

        void PlayResultSequence()
        {
            if (resultRoutine != null) StopCoroutine(resultRoutine);
            resultRoutine = StartCoroutine(ResultSequence());
        }

        IEnumerator ResultSequence()
        {
            resultSkipped = false;
            cardOverlay.SetActive(false);
            bool weak = profile.Effects == EffectLevel.Weak;
            RunSnapshot s = finalSnapshot;
            int walletBefore = profile.coinBalance - (rewardSaved ? finalReward : 0) - dailyCoins;
            float bestRatio = previousBestDistance > 0 ? (float)s.Distance / previousBestDistance : 1f;
            string pose = finalNewRecord ? "hamster_front_newrecord" : bestRatio >= .7f ? "hamster_front_cheer" : "hamster_front_lose";
            resultHamster.sprite = ArtLibrary.Get(pose) ?? ArtLibrary.Get("hamster_front_cheer");
            resultWallet.text = walletBefore.ToString("N0");
            resultRays.color = new Color(1f, .85f, .45f, 0);
            resultRibbon.enabled = false; resultRecordLogo.enabled = false; resultTitle.enabled = true;
            resultCoins.text = ""; resultCoinDetail.text = ""; resultCloseLine.text = "";
            saveErrorText.gameObject.SetActive(!rewardSaved);
            resultRetry.GetComponentsInChildren<Text>()[0].text = rewardSaved ? "もう一度" : "保存を再試行";
            for (int i = 0; i < resultValues.Length; i++) { resultValues[i].text = ""; resultLabels[i].canvasRenderer.SetAlpha(.25f); }
            resultProgress.gameObject.SetActive(false); resultProgressLabel.text = "";
            // hamster pops in from below (character #10)
            RectTransform h = resultHamster.rectTransform;
            Anim.Run(.35f, t => h.localScale = Vector3.one * Anim.OutBack(t), null, h);

            string[] finals = { s.Distance + " m", s.Score.ToString("N0"), s.MaxCombo.ToString(), s.Perfect + " / " + s.Good + " / " + s.Miss };
            int[] numbers = { s.Distance, s.Score, s.MaxCombo, -1 };
            for (int row = 0; row < resultValues.Length; row++)
            {
                float rowStart = Time.unscaledTime;
                resultLabels[row].canvasRenderer.SetAlpha(1);
                if (resultSkipped || weak || numbers[row] <= 0) { resultValues[row].text = finals[row]; audioDirector.Play(Sfx.ResultTick); }
                else
                {
                    float duration = row == 1 ? .8f : .45f, t0 = Time.unscaledTime;
                    int ticks = 0;
                    while (Time.unscaledTime - t0 < duration && !resultSkipped)
                    {
                        float u = Anim.OutCubic((Time.unscaledTime - t0) / duration);
                        int v = Mathf.RoundToInt(numbers[row] * u);
                        resultValues[row].text = row == 0 ? v + " m" : v.ToString("N0");
                        if (++ticks % 3 == 0) audioDirector.Play(Sfx.ResultTick, 1f, 1f + u); // rises an octave over the count
                        yield return null;
                    }
                    resultValues[row].text = finals[row];
                }
                Punch(resultValues[row].rectTransform);
                // one row per beat (0.5 s at 120 BPM) so the reveal rides the music
                while (!resultSkipped && Time.unscaledTime - rowStart < .5f) yield return null;
            }

            // coin breakdown and odometer (value is fixed; digits only roll — no reels, no randomness)
            int units = s.CoinUnits / 3, distanceCoins = config.DistanceBonusCoins * (s.Distance / config.DistanceBonusMeters);
            int feverExtra = FxDirector.FeverBonusCoins(s);
            resultCoinDetail.text = "斬った数 +" + units + (feverExtra > 0 ? "（うちFEVER +" + feverExtra + "）" : "") + "   距離 +" + distanceCoins + (finalBestBonus > 0 ? "   ベスト更新 +" + finalBestBonus : "");
            string loss = LossLine();
            if (loss.Length > 0) resultCoinDetail.text += "\n" + loss;
            int reward = finalReward;
            if (!resultSkipped && !weak && reward > 0)
            {
                float t0 = Time.unscaledTime, duration = Mathf.Clamp(.4f + reward * .02f, .5f, 1.4f);
                int last = -1;
                while (Time.unscaledTime - t0 < duration && !resultSkipped)
                {
                    float u = (Time.unscaledTime - t0) / duration;
                    int v = Mathf.RoundToInt(reward * Anim.OutCubic(u));
                    if (v != last) { last = v; resultCoins.text = "+" + v; audioDirector.Play(Sfx.ResultTick, .8f, 1.5f + u); }
                    yield return null;
                }
            }
            resultCoins.text = "+" + reward;
            Punch(resultCoins.rectTransform);
            audioDirector.Play(Sfx.ResultJan);
            if (reward > 0 && !weak) { audioDirector.Play(Sfx.Jara, .8f); CoinFountain(Mathf.Min(reward, 40)); }
            // wallet counts up as the fountain lands
            int walletAfter = walletBefore + (rewardSaved ? reward : 0) + dailyCoins;
            Anim.Run(weak || resultSkipped ? .01f : 1.1f, t => resultWallet.text = Mathf.RoundToInt(Mathf.Lerp(walletBefore, walletAfter, t)).ToString("N0"), null, resultWallet);
            if (!resultSkipped) yield return new WaitForSecondsRealtime(weak ? .1f : .6f);

            if (finalNewRecord)
            {
                audioDirector.Play(Sfx.NewRecord);
                if (profile.vibration) Haptics.Play(Haptics.Strength.Success);
                resultRibbon.enabled = resultRibbon.sprite != null;
                resultRecordLogo.enabled = resultRecordLogo.sprite != null;
                resultTitle.enabled = !resultRecordLogo.enabled;
                Anim.Run(.25f, t => resultRecordLogo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Anim.OutCubic(t)), null, resultRecordLogo);
                if (!weak)
                {
                    Anim.Run(.5f, t => resultRays.color = new Color(1f, .85f, .45f, .35f * t), null, "result-rays-fade");
                    Anim.Run(60f, t => resultRays.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * 1200f), null, resultRays); // 20°/s, no strobe
                    Confetti(28, -1); Confetti(28, 1);
                }
                Pulse(resultValues[0].rectTransform);
            }
            else if (previousBestDistance > 0 && s.Distance < previousBestDistance)
            {
                resultCloseLine.text = "ベストまで あと " + (previousBestDistance - s.Distance) + " m！";
                Punch(resultCloseLine.rectTransform);
            }
            ShowNextGoal();
            if (dailyLine.Length > 0)
            {
                // a completed daily goal takes the line under the card (it is the news of this result)
                resultProgress.gameObject.SetActive(false); resultProgressLabel.text = "";
                resultCloseLine.text = dailyLine;
                resultCloseLine.color = UiKit.Gold;
                Punch(resultCloseLine.rectTransform);
                audioDirector.Play(Sfx.Combo25);
            }
            else resultCloseLine.color = Color.white;
            MaybeAskReview(s);
        }

        /// <summary>Round 34: after a good moment, a gentle ask for a store rating (Android: the store page, iPhone: the system prompt).</summary>
        void MaybeAskReview(RunSnapshot s)
        {
            bool good = finalNewRecord || s.BossesDefeated > 0;
            if (Automation.Enabled || !ReviewPrompt.ShouldAsk(profile, good, DateTime.UtcNow)) return;
            profile.reviewAskedTicks = DateTime.UtcNow.Ticks;
            ProfileStore.TrySave(profile);
            Anim.Delay(1.2f, () =>
            {
                if (state != State.Result) return;
                Analytics.Event("review_prompt");
                ShowDialog("楽しんでもらえてる？", "よかったら ★で応援してね！\n開発のはげみになります。", "評価する", () =>
                {
                    profile.reviewDone = true;
                    ProfileStore.TrySave(profile);
                    Analytics.Event("review_go");
#if UNITY_IOS && !UNITY_EDITOR
                    UnityEngine.iOS.Device.RequestStoreReview();
#else
                    Application.OpenURL("market://details?id=" + Application.identifier);
#endif
                }, "あとで");
            }, resultCloseLine);
        }

        /// <summary>"木刀まで あと 46" progress toward the cheapest item not yet owned.</summary>
        void ShowNextGoal()
        {
            CatalogItem next = null;
            if (catalog != null) foreach (CatalogItem w in catalog.weapons) if (!profile.Owns(w.id) && w.Playable && (next == null || w.price < next.price)) next = w;
            if (next == null || !string.IsNullOrEmpty(resultCloseLine.text)) return;
            resultProgress.gameObject.SetActive(true);
            float ratio = Mathf.Clamp01((float)profile.coinBalance / next.price);
            resultProgressFill.rectTransform.anchorMax = new Vector2(0, 1);
            Anim.Run(.6f, t => resultProgressFill.rectTransform.anchorMax = new Vector2(ratio * Anim.OutCubic(t), 1), null, resultProgressFill);
            resultProgressLabel.text = profile.coinBalance >= next.price ? next.displayName + "が買えます！" : next.displayName + "まで あと " + (next.price - profile.coinBalance);
        }

        void CoinFountain(int count)
        {
            Vector2 target = resultFountain.InverseTransformPoint(resultWalletIcon.TransformPoint(resultWalletIcon.rect.center));
            Rect r = resultFountain.rect;
            for (int i = 0; i < count; i++)
            {
                Image coin = UiKit.Image("Coin", resultFountain, ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
                coin.rectTransform.sizeDelta = new Vector2(64, 64);
                Vector2 from = new Vector2(UnityEngine.Random.Range(-120f, 120f), r.yMin + r.height * .32f);
                Vector2 apex = new Vector2(from.x + UnityEngine.Random.Range(-260f, 260f), r.yMin + r.height * UnityEngine.Random.Range(.55f, .72f));
                float delay = i * .025f;
                Image c = coin;
                Anim.Run(.95f + delay, t =>
                {
                    if (c == null) return;
                    float u = Mathf.Clamp01((t * (.95f + delay) - delay) / .95f);
                    Vector2 p = (1 - u) * (1 - u) * from + 2 * (1 - u) * u * apex + u * u * target;
                    c.rectTransform.anchoredPosition = p;
                    c.rectTransform.localScale = new Vector3(Mathf.Abs(Mathf.Cos(u * 20f)) * .9f + .1f, 1, 1) * (u <= 0 ? 0 : Mathf.Lerp(1.1f, .6f, u));
                }, () => { if (c != null) Destroy(c.gameObject); });
            }
        }

        /// <summary>Confetti from one side of the result screen (no twinkle, fades out).</summary>
        void Confetti(int count, int side)
        {
            if (profile.Effects == EffectLevel.Standard) count /= 2;
            Rect r = resultFountain.rect;
            for (int i = 0; i < count; i++)
            {
                Image piece = UiKit.Image("Confetti", resultFountain, ArtLibrary.Rounded, Color.HSVToRGB(UnityEngine.Random.value, .65f, .95f), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
                piece.type = Image.Type.Simple;
                piece.rectTransform.sizeDelta = new Vector2(UnityEngine.Random.Range(14f, 22f), UnityEngine.Random.Range(24f, 36f));
                Vector2 from = new Vector2(side * r.width * .48f, r.yMin + r.height * .62f);
                Vector2 v = new Vector2(-side * UnityEngine.Random.Range(250f, 700f), UnityEngine.Random.Range(400f, 1000f));
                float spin = UnityEngine.Random.Range(-360f, 360f), life = UnityEngine.Random.Range(1.3f, 2f);
                Image c = piece;
                Anim.Run(life, t =>
                {
                    if (c == null) return;
                    float time = t * life;
                    c.rectTransform.anchoredPosition = from + v * time + new Vector2(0, -700f * time * time);
                    c.rectTransform.localRotation = Quaternion.Euler(0, 0, spin * time);
                    c.canvasRenderer.SetAlpha(1f - t * t);
                }, () => { if (c != null) Destroy(c.gameObject); });
            }
        }

        static void Punch(RectTransform rect)
        {
            Anim.Run(.25f, t => rect.localScale = Vector3.one * (1f + .25f * Anim.Punch(t)), null, rect);
        }

        static void Pulse(RectTransform rect)
        {
            Anim.Run(3f, t => rect.localScale = Vector3.one * (1f + .06f * Mathf.Sin(t * 3f * Mathf.PI * 2f * .5f)), () => rect.localScale = Vector3.one, rect);
        }

        void OnRetryPressed()
        {
            if (!rewardSaved) { rewardSaved = TryApplyReward(); PlayResultSequence(); return; }
            StartRun();
        }
    }
}
