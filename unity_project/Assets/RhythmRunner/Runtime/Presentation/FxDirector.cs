using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// "Pachinko-grade" run effects that live only in the allowed areas (review/round0 effects §8): the top band
    /// (10–24% height), the left/right edges (8% each) and below the slash line. Everything that pulses brightness asks
    /// <see cref="FlashBudget"/> first; milestones fire on the next beat head so effects ride the music.
    /// </summary>
    public sealed class FxDirector : MonoBehaviour
    {
        sealed class Flyer { public RectTransform Rect; public Image Image; public Vector2 From, Control, To; public float Born, Life; }
        sealed class Confetti { public RectTransform Rect; public Image Image; public Vector2 Velocity; public float Spin, Born, Life; }

        public event Action<int> Milestone;      // combo value
        public event Action FeverEntered, FeverEnded, CoinLanded, NewRecordPassed;
        /// <summary>A stage was cleared (passed its gate): the number of the stage just finished.</summary>
        public event Action<int> StageCleared;

        RectTransform root, band, edgeL, edgeR, flyLayer, confettiLayer;
        Image edgeImageL, edgeImageR, cutin, bandBack;
        Text bandText, bandSub, countdown;
        Image feverLogo, recordLogo, recordRibbon;
        RectTransform bossBar;
        Image bossFill, bossIcon;
        Text bossName;
        float bossShown, bossBarAlpha;
        int lastStage;
        HudView hud;
        PlayfieldView playfield;
        readonly FlashBudget budget = new FlashBudget();
        readonly List<Flyer> flyers = new List<Flyer>();
        readonly Stack<Flyer> freeFlyers = new Stack<Flyer>();
        readonly List<Confetti> confetti = new List<Confetti>();
        readonly Stack<Confetti> freeConfetti = new Stack<Confetti>();
        int lastCombo, lastCoins, lastDistanceMark, pendingMilestone, bestDistance;
        bool lastFever, recordPassed;
        double lastBeatHead = -1;
        float edgePulseAt = -9, bandUntil = -9;
        public EffectLevel Effects = EffectLevel.Strong;
        public bool ScreenShake = true;
        /// <summary>
        /// Owner round 6: during a boss fight the boss is the star. Only boss messages use the banner (which sits over
        /// the boss's face); combo milestones, distance marks and the FEVER cut-in stay silent visually.
        /// </summary>
        public bool BossMode;
        /// <summary>
        /// Owner round 8: from the WARNING until the boss is gone, messages use one fixed slot just under the boss's feet
        /// (no dark plate, one short line) instead of the top band over its face; during the finisher a lower slot.
        /// </summary>
        public bool BossSlot, BossLow;
        /// <summary>Rage is shown by the screen edges glowing red (plus the boss's pose and roar), not by reading.</summary>
        public bool BossRageEdge;
        int bandPriority;

        public void Build(RectTransform gameRoot, RectTransform hudSafe, HudView hudView, PlayfieldView field)
        {
            hud = hudView; playfield = field;
            root = UiKit.Rect("Fx", gameRoot, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<Canvas>();
            edgeL = UiKit.Rect("EdgeL", root, new Vector2(0, 0), new Vector2(.08f, 1));
            edgeImageL = edgeL.gameObject.AddComponent<Image>(); edgeImageL.sprite = ArtLibrary.EdgeGlow; edgeImageL.raycastTarget = false;
            edgeR = UiKit.Rect("EdgeR", root, new Vector2(.92f, 0), new Vector2(1, 1));
            edgeImageR = edgeR.gameObject.AddComponent<Image>(); edgeImageR.sprite = ArtLibrary.EdgeGlow; edgeImageR.raycastTarget = false;
            edgeR.localScale = new Vector3(-1, 1, 1);

            band = UiKit.Rect("TopBand", hudSafe, new Vector2(0, .74f), new Vector2(.7f, .87f)); // left of the combo badge, below the HUD pills // 10–24% from the top, above the horizon
            bandBack = UiKit.Image("BandBack", band, ArtLibrary.Pill, new Color(.05f, .06f, .12f, .78f), new Vector2(.03f, .2f), new Vector2(.99f, .8f));
            cutin = UiKit.Image("Cutin", band, ArtLibrary.Get("hamster_cutin_fever"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
            cutin.rectTransform.sizeDelta = new Vector2(620, 420);
            feverLogo = UiKit.Image("FeverLogo", band, ArtLibrary.Get("logo_fever"), Color.white, new Vector2(.05f, -.05f), new Vector2(.92f, 1.05f), true);
            recordRibbon = UiKit.Image("Ribbon", band, ArtLibrary.Get("record_ribbon"), Color.white, new Vector2(.05f, -.3f), new Vector2(.95f, 1.2f), true);
            recordLogo = UiKit.Image("RecordLogo", band, ArtLibrary.Get("logo_new_record"), Color.white, new Vector2(.12f, -.05f), new Vector2(.88f, 1.05f), true);
            bandText = UiKit.Label("", band, 76, Color.white, new Vector2(.05f, .3f), new Vector2(.97f, .85f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            bandSub = UiKit.Label("", band, 38, UiKit.Gold, new Vector2(.05f, .05f), new Vector2(.97f, .35f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            countdown = UiKit.Label("", hudSafe, 150, UiKit.Gold, new Vector2(.62f, .62f), new Vector2(.98f, .76f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            // boss HP bar: under the beat dots, between the coin pill and the hearts
            Image barBack = UiKit.Image("BossBar", hudSafe, ArtLibrary.Pill, new Color(.08f, .02f, .06f, .85f), Vector2.zero, Vector2.zero);
            bossBar = UiKit.At(barBack.rectTransform, new Vector2(.5f, 1), new Vector2(440, 26), new Vector2(.5f, 1));
            bossBar.anchoredPosition = new Vector2(0, -116);
            Image fillBack = UiKit.Image("FillBack", bossBar, ArtLibrary.Pill, new Color(.3f, .05f, .1f, 1), new Vector2(.03f, .2f), new Vector2(.97f, .8f));
            bossFill = UiKit.Image("Fill", fillBack.transform, ArtLibrary.Pill, UiKit.Coral, Vector2.zero, Vector2.one);
            bossName = UiKit.Label("BOSS", bossBar, 30, Color.white, new Vector2(.05f, .05f), new Vector2(.95f, .95f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            bossName.enabled = false; // the bar stays thin and wordless; the boss's look tells the rest
            bossBar.gameObject.SetActive(false);
            flyLayer = UiKit.Rect("Coins", root, Vector2.zero, Vector2.one);
            confettiLayer = UiKit.Rect("Confetti", root, Vector2.zero, Vector2.one);
            HideBand();
        }

        void HideBand()
        {
            bandBack.enabled = false; cutin.enabled = false; feverLogo.enabled = false; recordLogo.enabled = false; recordRibbon.enabled = false;
            bandText.text = ""; bandSub.text = "";
        }

        public void ResetRun(int best)
        {
            lastCombo = 0; lastCoins = 0; lastDistanceMark = 0; pendingMilestone = 0; lastFever = false; recordPassed = false;
            bestDistance = best; lastBeatHead = -1; bandUntil = -9; lastStage = 0; bossShown = 1; bossBarAlpha = 0;
            bossBar.gameObject.SetActive(false);
            budget.Reset();
            HideBand();
            countdown.text = "";
            foreach (Flyer f in flyers) { f.Rect.gameObject.SetActive(false); freeFlyers.Push(f); }
            flyers.Clear();
            foreach (Confetti c in confetti) { c.Rect.gameObject.SetActive(false); freeConfetti.Push(c); }
            confetti.Clear();
        }

        static bool IsMilestone(int combo) { return combo == 10 || combo == 25 || combo == 50 || (combo >= 100 && combo % 50 == 0); }

        public void Tick(RunSnapshot s, double beat, bool running)
        {
            float now = Time.unscaledTime;
            double beatHead = Math.Floor(beat);
            bool newBeat = beatHead != lastBeatHead;
            lastBeatHead = beatHead;

            if (running)
            {
                for (int c = lastCombo + 1; c <= s.Combo; c++) if (IsMilestone(c)) pendingMilestone = c;
                if (s.Combo < lastCombo) pendingMilestone = 0;
                // milestones are quantized to the next beat head so the celebration lands on the music
                if (pendingMilestone > 0 && newBeat) { FireMilestone(pendingMilestone, s); pendingMilestone = 0; }
                if (s.Fever && !lastFever) EnterFever(s);
                if (!s.Fever && lastFever) ExitFever(s);
                // FEVER countdown 5..1 (combo 45-49)
                if (now >= bossCountUntil)
                {
                    countdown.text = s.Combo >= 45 && s.Combo < 50 ? (50 - s.Combo).ToString() : "";
                    countdown.color = UiKit.Gold;
                    if (countdown.text.Length > 0) countdown.rectTransform.localScale = Vector3.one * (1f + .3f * Anim.Punch(Mathf.Clamp01((float)(beat - beatHead) * 2f)));
                }
                else countdown.rectTransform.localScale = Vector3.one * (1f + .45f * Anim.Punch(Mathf.Clamp01((now - bossCountAt) / .25f)));
                // coins: one flyer per coin gained, capped per frame
                if (s.Coins > lastCoins) for (int i = 0; i < Mathf.Min(3, s.Coins - lastCoins); i++) LaunchCoin(i * .06f);
                // 100 m marks
                int mark = s.Distance / 100;
                if (mark > lastDistanceMark && mark > 0) { lastDistanceMark = mark; if (now > bandUntil) ShowText(mark * 100 + "m!", "+" + RunConfig.Default.DistanceBonusCoins * 2 + " コイン", UiKit.Cyan, .9f, 1); }
                // new record passes the BEST flag exactly when the distance overtakes the old best
                if (!recordPassed && bestDistance > 0 && s.Distance > bestDistance) { recordPassed = true; ShowRecord(); }
                if (s.Stage > lastStage) { lastStage = s.Stage; SpeedUp(s); }
            }
            UpdateBossBar(s);
            lastCombo = s.Combo; lastCoins = s.Coins; lastFever = s.Fever;

            UpdateEdges(s, beat, newBeat, now);
            if (now > bandUntil) HideBand();
            UpdateFlyers(now);
            UpdateConfetti(now);
        }

        void FireMilestone(int combo, RunSnapshot s)
        {
            if (Milestone != null) Milestone(combo);
            playfield.ComboReaction();
            hud.PunchCombo();
            if (combo == 50) return; // FEVER entry has its own cut-in
            if (combo == 10) ShowText("10 COMBO!", null, UiKit.Cyan, .6f, 1);
            else if (combo == 25) ShowText("25 COMBO!", "スコア倍率 MAX ×1.5", UiKit.Hex("5BE37D"), .8f, 1);
            else ShowText(combo + " COMBO!", "FEVER Lv" + s.FeverLevel, HudView.TierColor(combo, Time.unscaledTime), 1f, 1);
            if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(Effects == EffectLevel.Strong ? 6f : 3f);
            if (Effects == EffectLevel.Strong && combo >= 100) { Burst(12, new Vector2(.02f, .45f)); Burst(12, new Vector2(.98f, .45f)); }
        }

        void EnterFever(RunSnapshot s)
        {
            if (FeverEntered != null) FeverEntered();
            playfield.SetFever(true);
            if (BossMode) return; // the rainbow road, aura and music say it; the boss keeps the stage
            HideBand();
            bandUntil = Time.unscaledTime + 1.3f;
            if (Effects == EffectLevel.Weak)
            {
                ShowText("FEVER!!", "コイン ×2", UiKit.Gold, 1.3f);
                return;
            }
            bandBack.enabled = true;
            bandUntil = Time.unscaledTime + 2.1f;
            float cutScale = Effects == EffectLevel.Strong ? 1f : .6f;
            const float cutLife = .95f;
            if (cutin.sprite != null)
            {
                cutin.enabled = true;
                feverLogo.enabled = false;
                // 1) the hamster dashes in alone and poses (0.2 s in, 0.55 s hold, 0.2 s out) — nothing covers it
                Anim.Run(cutLife, t =>
                {
                    float x = t < .2f ? Mathf.Lerp(760, 30, Anim.OutCubic(t / .2f)) : t < .78f ? Mathf.Lerp(30, -10, (t - .2f) / .58f) : Mathf.Lerp(-10, -980, Anim.InOutSine((t - .78f) / .22f));
                    cutin.rectTransform.anchoredPosition = new Vector2(x, 10);
                    cutin.rectTransform.localScale = Vector3.one * cutScale * (t < .2f ? 1.1f - .1f * (t / .2f) : 1f);
                }, () => cutin.enabled = false, cutin);
            }
            // 2) as it leaves, FEVER!! slams into the band it just vacated
            Anim.Delay(cutin.sprite != null ? cutLife * .8f : 0f, () =>
            {
                if (feverLogo.sprite != null)
                {
                    feverLogo.enabled = true;
                    Anim.Run(.35f, t => feverLogo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, Anim.OutBack(t)), null, feverLogo);
                }
                else { bandText.text = "FEVER!!"; bandText.color = UiKit.Gold; }
                bandSub.text = "コイン ×2";
                if (ScreenShake) hud.Shake(Effects == EffectLevel.Strong ? 6f : 3f);
            }, feverLogo);
            bandSub.text = "";
            if (ScreenShake) hud.Shake(Effects == EffectLevel.Strong ? 6f : 3f);
            if (Effects == EffectLevel.Strong) { Burst(20, new Vector2(.02f, .45f)); Burst(20, new Vector2(.98f, .45f)); }
        }

        void ExitFever(RunSnapshot s)
        {
            if (FeverEnded != null) FeverEnded();
            playfield.SetFever(false);
            // emphasise what was earned, not the loss (no red, no flash)
            ShowText("FEVER END", "FEVERボーナス +" + FeverBonusCoins(s) + "枚", Color.white, 1f);
        }

        /// <summary>Exact coins earned only because of FEVER: coins(units) - coins(units without the FEVER doubling).</summary>
        public static int FeverBonusCoins(RunSnapshot s) { return s.CoinUnits / 3 - (s.CoinUnits - s.FeverHits) / 3; }

        // ------------------------------------------------------------------ stages & bosses

        void SpeedUp(RunSnapshot s)
        {
            // owner round 10: passing the gate is an achievement first ("STAGE n CLEAR!"), then the news of the next stage
            string name = playfield.ThemeName(s.Theme);
            ShowText("STAGE " + s.Stage + " CLEAR!", "次は " + name + "   ♪" + Mathf.RoundToInt((float)s.Bpm), UiKit.Gold, 2.2f, 3);
            playfield.StageClearPose();
            if (StageCleared != null) StageCleared(s.Stage);
            if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(Effects == EffectLevel.Strong ? 7f : 3f);
            if (Effects != EffectLevel.Weak) { Burst(Effects == EffectLevel.Strong ? 26 : 12, new Vector2(.02f, .45f)); Burst(Effects == EffectLevel.Strong ? 26 : 12, new Vector2(.98f, .45f)); }
        }

        public static string BossDisplayName(string bossId)
        {
            switch (bossId)
            {
                case "boss_toaster": return "トースター魔人";
                case "boss_onion": return "たまねぎ大王";
                case "boss_icecream": return "ソフトクリーム将軍";
                case "boss_donut": return "ドーナツ伯爵";
                case "boss_zenmai": return "ゼンマイ大王";
                case "boss_kaminari": return "カミナリ雲大将";
                case "boss_yukidaruma": return "雪だるま将軍";
                default: return "BOSS";
            }
        }

        public void BossWarning(string bossId)
        {
            ShowText("WARNING!", BossDisplayName(bossId) + " があらわれた！", UiKit.Coral, 1.8f, 3);
            bossName.text = BossDisplayName(bossId);
            if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(5f);
        }

        public void BossDefeated(int coinBonus)
        {
            ShowText("BOSS撃破！", "♥ +1   コイン +" + coinBonus, UiKit.Gold, 2f, 3);
            if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(Effects == EffectLevel.Strong ? 8f : 4f);
            if (Effects != EffectLevel.Weak) { Burst(24, new Vector2(.02f, .45f)); Burst(24, new Vector2(.98f, .45f)); }
            for (int i = 0; i < 4; i++) LaunchCoin(i * .07f);
        }

        public void BossRage(string bossId)
        {
            ShowText("怒りモード！", null, UiKit.Coral, 1.3f, 3);
            if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(Effects == EffectLevel.Strong ? 9f : 4f);
        }

        float bossCountUntil = -9, bossCountAt = -9;

        /// <summary>
        /// Beat-synced countdown before the boss (4, 3, 2, 1). Owner round 9: the big number used to sit on the lanes and
        /// hide targets; it now punches in at the side, above the wall, where nothing has to be read or hit.
        /// </summary>
        public void BossCount(int n)
        {
            countdown.text = n.ToString();
            countdown.color = UiKit.Coral;
            bossCountAt = Time.unscaledTime; bossCountUntil = bossCountAt + .55f;
            if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(2f + n);
        }

        public void BossArrive(string bossId)
        {
            ShowText("BOSS!!", BossDisplayName(bossId), UiKit.Coral, 1.2f, 3);
            if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(Effects == EffectLevel.Strong ? 12f : 5f);
            if (Effects == EffectLevel.Strong) { Burst(14, new Vector2(.02f, .45f)); Burst(14, new Vector2(.98f, .45f)); }
        }

        /// <summary>One-time hint (tutorial): stays a little longer than other banners.</summary>
        public void ShowTip(string title, string sub) { ShowText(title, sub, UiKit.Cyan, 2.6f, 3); bandText.resizeTextMaxSize = 56; }

        public void RefreshCharacter() { cutin.sprite = ArtLibrary.Get("hamster_cutin_fever"); }

        public void ChargeWarning() { ShowText("ためこうげき！", null, UiKit.Offbeat, 1.4f, 3); }

        public void ItemStarted(ItemKind item, bool healed = false)
        {
            if (item == ItemKind.Heart)
            {
                if (healed) ShowText("ハート回復！", null, UiKit.Coral, 1.2f);
                else ShowText("ハート満タン！", "コイン +5", UiKit.Gold, 1.2f);
                return;
            }
            if (item == ItemKind.Booster)
            {
                ShowText("斬撃ラッシュ!!", "前方をぜんぶ斬る！ +20m", UiKit.Gold, 1.6f, 3);
                if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(5f);
                if (Effects == EffectLevel.Strong) { Burst(10, new Vector2(.02f, .45f)); Burst(10, new Vector2(.98f, .45f)); }
            }
            else if (item == ItemKind.Giant)
            {
                ShowText("巨大化!!", "8拍のあいだ どのレーンでも斬れる", UiKit.Gold, 1.6f, 3);
                if (ScreenShake && Effects != EffectLevel.Weak) hud.Shake(6f);
            }
            else ShowText("バリア！", "ミス1回を防ぐ", UiKit.Cyan, 1.4f);
        }

        /// <summary>Round 27: the giant shrank back to normal size.</summary>
        public void GiantEnded() { ShowText("元に戻った！", "レーンに注意", Color.white, 1.0f); }

        public void BossEscaped() { ShowText("逃げられた…", "次のボスで取り返そう", Color.white, 1.4f, 3); }

        void UpdateBossBar(RunSnapshot s)
        {
            float target = s.BossActive ? 1f : 0f;
            bossBarAlpha = Mathf.MoveTowards(bossBarAlpha, target, Time.unscaledDeltaTime / .3f);
            bossBar.gameObject.SetActive(bossBarAlpha > 0);
            if (bossBarAlpha <= 0) { bossShown = 1; return; }
            if (s.BossActive) bossShown = Mathf.MoveTowards(bossShown, s.BossHp01, Time.unscaledDeltaTime * 1.5f);
            bossFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(bossShown), 1);
            bossFill.color = bossShown < .3f ? Color.Lerp(UiKit.Coral, UiKit.Gold, Mathf.PingPong(Time.unscaledTime * 4f, 1f)) : UiKit.Coral;
            bossBar.localScale = new Vector3(1, bossBarAlpha, 1);
        }

        void ShowRecord()
        {
            if (NewRecordPassed != null) NewRecordPassed();
            HideBand();
            bandUntil = Time.unscaledTime + 1.6f;
            if (recordLogo.sprite != null && Effects != EffectLevel.Weak)
            {
                recordRibbon.enabled = recordRibbon.sprite != null;
                recordLogo.enabled = true;
                Anim.Run(.25f, t => recordLogo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Anim.OutCubic(t)), null, recordLogo);
            }
            else ShowText("NEW RECORD!", null, UiKit.Gold, 1.6f);
            if (Effects != EffectLevel.Weak) { Burst(24, new Vector2(.02f, .45f)); Burst(24, new Vector2(.98f, .45f)); }
        }

        /// <summary>priority: 1 = small news (combo, metres), 2 = normal, 3 = boss / must-read. A lower one never covers a higher one.</summary>
        void ShowText(string text, string sub, Color color, float seconds, int priority = 2)
        {
            if (Time.unscaledTime < bandUntil && priority < bandPriority) return;
            if (BossMode && priority < 3) return;
            bandPriority = priority;
            HideBand();
            bool slot = BossSlot;
            band.anchorMin = slot ? new Vector2(.06f, BossLow ? .29f : .47f) : new Vector2(0, .74f);
            band.anchorMax = slot ? new Vector2(.94f, BossLow ? .38f : .56f) : new Vector2(.7f, .87f);
            bandBack.enabled = !slot;
            bandText.resizeTextMaxSize = slot ? 68 : 76;
            bandText.text = text; bandText.color = color;
            bandSub.text = sub ?? "";
            bandUntil = Time.unscaledTime + seconds;
            if (Effects == EffectLevel.Weak) { bandText.rectTransform.localScale = Vector3.one; return; }
            Anim.Run(.3f, t => bandText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Anim.OutBack(t)), null, bandText);
        }

        // ------------------------------------------------------------------ edges

        void UpdateEdges(RunSnapshot s, double beat, bool newBeat, float now)
        {
            if (BossRageEdge && Effects != EffectLevel.Weak)
            {
                // rage: a slow red glow on both edges (no strobe)
                Color red = new Color(1f, .16f, .1f, .42f + .12f * Mathf.Sin(now * 3f));
                edgeImageL.color = red; edgeImageR.color = red;
                edgeL.anchorMax = new Vector2(.1f, 1); edgeR.anchorMin = new Vector2(.9f, 0);
                edgeImageL.enabled = edgeImageR.enabled = true;
                return;
            }
            Color tier = HudView.TierColor(s.Combo, Effects == EffectLevel.Weak ? 0f : now); // weak: no hue drift
            float baseAlpha = s.Combo >= 10 ? (Effects == EffectLevel.Weak ? .25f : Effects == EffectLevel.Standard ? .45f : .6f) : 0f;
            if (s.Combo >= 50 && Effects == EffectLevel.Strong) baseAlpha = .75f;
            // pulse on beat heads only, through the shared 2 Hz budget (edges are >10% of the screen: ±0.10 amplitude)
            if (newBeat && baseAlpha > 0 && Effects != EffectLevel.Weak && budget.TryFlash(FlashBudget.Region.Edge, now)) edgePulseAt = now;
            float pulse = Mathf.Clamp01(1f - (now - edgePulseAt) / .25f) * .1f;
            float width = s.Fever ? 1.25f : 1f;
            Color c = new Color(tier.r, tier.g, tier.b, baseAlpha > 0 ? baseAlpha + pulse : 0);
            edgeImageL.color = c; edgeImageR.color = c;
            edgeL.anchorMax = new Vector2(.09f * width, 1);
            edgeR.anchorMin = new Vector2(1 - .09f * width, 0);
            edgeImageL.enabled = edgeImageR.enabled = baseAlpha > 0;
        }

        // ------------------------------------------------------------------ coins

        Vector2 ToFxLocal(RectTransform target)
        {
            Vector3 world = target.TransformPoint(target.rect.center);
            return (Vector2)root.InverseTransformPoint(world);
        }

        void LaunchCoin(float delay)
        {
            Sprite sprite = ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle;
            Flyer f = freeFlyers.Count > 0 ? freeFlyers.Pop() : NewFlyer();
            f.Image.sprite = sprite;
            f.Rect.gameObject.SetActive(true);
            Vector2 start = playfield.LastHitPosition;
            // playfield and fx share the same full-screen rect, so local positions match
            Vector2 end = ToFxLocal(hud.CoinTarget);
            // path: bounce down below the slash line, then up the left edge — never across the road above the line
            f.From = start;
            f.Control = new Vector2(root.rect.xMin + root.rect.width * .03f, start.y - root.rect.height * .1f);
            f.To = end;
            f.Born = Time.unscaledTime + delay;
            f.Life = .55f;
            f.Rect.anchoredPosition = start;
            f.Rect.localScale = Vector3.zero;
            flyers.Add(f);
        }

        Flyer NewFlyer()
        {
            Image image = UiKit.Image("Coin", flyLayer, null, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
            image.rectTransform.sizeDelta = new Vector2(64, 64);
            return new Flyer { Rect = image.rectTransform, Image = image };
        }

        void UpdateFlyers(float now)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                Flyer f = flyers[i];
                float t = (now - f.Born) / f.Life;
                if (t < 0) continue;
                if (t >= 1f)
                {
                    f.Rect.gameObject.SetActive(false); freeFlyers.Push(f); flyers.RemoveAt(i);
                    hud.PunchCoins();
                    if (CoinLanded != null) CoinLanded();
                    continue;
                }
                float e = Anim.InOutSine(t);
                Vector2 p = (1 - e) * (1 - e) * f.From + 2 * (1 - e) * e * f.Control + e * e * f.To;
                f.Rect.anchoredPosition = p;
                float spin = Mathf.Cos(now * 18f); // coin turns by squashing X
                f.Rect.localScale = new Vector3(Mathf.Abs(spin) * .9f + .1f, 1, 1) * Mathf.Lerp(1.2f, .8f, t);
            }
        }

        // ------------------------------------------------------------------ confetti (low brightness, no twinkle)

        public void Burst(int count, Vector2 normalized)
        {
            if (Effects == EffectLevel.Weak) return;
            if (Effects == EffectLevel.Standard) count /= 2;
            Vector2 origin = new Vector2(root.rect.xMin + root.rect.width * normalized.x, root.rect.yMin + root.rect.height * normalized.y);
            for (int i = 0; i < count && confetti.Count < 60; i++)
            {
                Confetti c = freeConfetti.Count > 0 ? freeConfetti.Pop() : NewConfetti();
                c.Rect.gameObject.SetActive(true);
                c.Image.color = Color.HSVToRGB(UnityEngine.Random.value, .65f, .95f);
                c.Rect.anchoredPosition = origin;
                // edge fountains: mostly upward, at most ~15% of the width inward, so pieces never cross the road or the HUD
                float inward = normalized.x < .5f ? 1f : -1f;
                c.Velocity = new Vector2(inward * UnityEngine.Random.Range(30f, 140f), UnityEngine.Random.Range(450f, 850f));
                c.Spin = UnityEngine.Random.Range(-360f, 360f);
                c.Born = Time.unscaledTime; c.Life = UnityEngine.Random.Range(1.2f, 2f);
                c.Rect.sizeDelta = new Vector2(UnityEngine.Random.Range(14f, 22f), UnityEngine.Random.Range(24f, 36f));
                confetti.Add(c);
            }
        }

        Confetti NewConfetti()
        {
            Image image = UiKit.Image("Confetti", confettiLayer, ArtLibrary.Rounded, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            image.type = Image.Type.Simple;
            return new Confetti { Rect = image.rectTransform, Image = image };
        }

        void UpdateConfetti(float now)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                Confetti c = confetti[i];
                float t = (now - c.Born) / c.Life;
                if (t >= 1f) { c.Rect.gameObject.SetActive(false); freeConfetti.Push(c); confetti.RemoveAt(i); continue; }
                c.Velocity.y -= 1400f * dt;
                c.Velocity *= 1f - 1.2f * dt;
                c.Rect.anchoredPosition += c.Velocity * dt;
                c.Rect.localRotation *= Quaternion.Euler(0, 0, c.Spin * dt);
                c.Image.canvasRenderer.SetAlpha(1f - t * t);
            }
        }
    }
}
