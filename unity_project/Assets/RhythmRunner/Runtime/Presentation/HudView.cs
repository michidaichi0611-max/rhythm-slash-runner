using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Play HUD per UX_UI_SPEC: distance + run coins left, 8 beat dots centre, score + pause right, hearts, combo badge with
    /// FEVER gauge. Text is rebuilt only when its value changes (no per-frame string allocation).
    /// </summary>
    public sealed class HudView
    {
        // colour rank shared by every effect: white < cyan < green < gold < rainbow (review/round0 effects §2)
        public static Color TierColor(int combo, float time)
        {
            if (combo >= 50) return Color.HSVToRGB(Mathf.Repeat(time * .25f, 1f), .7f, 1f);
            if (combo >= 40) return UiKit.Gold;
            if (combo >= 25) return UiKit.Hex("5BE37D");
            if (combo >= 10) return UiKit.Cyan;
            return Color.white;
        }

        RectTransform root, shakeRoot;
        Text distance, score, combo, comboLabel, countIn, banner, diagnostics, coins, coinX2;
        RectTransform comboBadge, bannerRect, coinPill, coinIcon, itemPill;
        Image itemIcon, itemFill, shieldIcon;
        Image comboFrame, gaugeFill, gaugeBack;
        readonly Image[] dots = new Image[8];
        readonly Image[] halfDots = new Image[8];
        readonly Image[] hearts = new Image[4];
        int lastLabelKey = -1;
        int lastCombo = -1, lastScore = -1, lastDistance = -1, lastCoins = -1, lastHp = 4;
        float comboPopAt = -9, comboBigAt = -9, coinPopAt = -9, shakeAt = -9, shakePx;
        readonly float[] heartLostAt = { -9, -9, -9, -9 };
        readonly float[] heartGainAt = { -9, -9, -9, -9 };

        public RectTransform Root { get { return root; } }
        /// <summary>Boss fights: the combo badge shrinks out of the boss's way.</summary>
        public bool Compact;
        /// <summary>Where flying coins land.</summary>
        public RectTransform CoinTarget { get { return coinIcon; } }

        public void Build(RectTransform safeArea, System.Action onPause)
        {
            root = UiKit.Rect("Hud", safeArea, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<Canvas>();
            root.gameObject.AddComponent<GraphicRaycaster>();
            shakeRoot = UiKit.Rect("Shake", root, Vector2.zero, Vector2.one);

            Image distPill = UiKit.Image("DistancePill", shakeRoot, ArtLibrary.Pill, UiKit.Ink, Vector2.zero, Vector2.zero);
            UiKit.At(distPill.rectTransform, new Vector2(0, 1), new Vector2(250, 96), new Vector2(0, 1)).anchoredPosition = new Vector2(28, -28);
            Sprite flagSprite = ArtLibrary.Get("icon_flag");
            Image flag = UiKit.Image("Flag", distPill.transform, flagSprite ?? ArtLibrary.Circle, flagSprite != null ? Color.white : UiKit.Cyan, Vector2.zero, Vector2.zero, true);
            UiKit.At(flag.rectTransform, new Vector2(0, .5f), flagSprite != null ? new Vector2(60, 60) : new Vector2(22, 22)).anchoredPosition = new Vector2(42, 0);
            distance = UiKit.Label("0 m", distPill.transform, 52, Color.white, new Vector2(.3f, .05f), new Vector2(.95f, .95f), TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            distance.resizeTextForBestFit = false;

            Image coinBg = UiKit.Image("CoinPill", shakeRoot, ArtLibrary.Pill, UiKit.Ink, Vector2.zero, Vector2.zero);
            coinPill = UiKit.At(coinBg.rectTransform, new Vector2(0, 1), new Vector2(210, 80), new Vector2(0, 1));
            coinPill.anchoredPosition = new Vector2(28, -134);
            Image coinImg = UiKit.Image("Coin", coinBg.transform, ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle, ArtLibrary.Has("icon_coin") ? Color.white : UiKit.Gold, Vector2.zero, Vector2.zero, true);
            coinIcon = UiKit.At(coinImg.rectTransform, new Vector2(0, .5f), new Vector2(62, 62));
            coinIcon.anchoredPosition = new Vector2(40, 0);
            coins = UiKit.Label("0", coinBg.transform, 46, UiKit.Gold, new Vector2(.36f, .05f), new Vector2(.92f, .95f), TextAnchor.MiddleLeft, FontStyle.Bold);
            coins.resizeTextForBestFit = false;
            Image x2Chip = UiKit.Image("X2", coinBg.transform, ArtLibrary.Pill, UiKit.Gold, new Vector2(1.02f, .12f), new Vector2(1.5f, .88f));
            coinX2 = UiKit.Label("×2", x2Chip.transform, 40, UiKit.Navy, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, FontStyle.Bold);
            x2Chip.gameObject.SetActive(false); // the chip (not the label) is what Render toggles

            // active power-up: icon + time left (under the coin pill)
            Image itemBg = UiKit.Image("ItemPill", shakeRoot, ArtLibrary.Pill, UiKit.Ink, Vector2.zero, Vector2.zero);
            itemPill = UiKit.At(itemBg.rectTransform, new Vector2(0, 1), new Vector2(210, 70), new Vector2(0, 1));
            itemPill.anchoredPosition = new Vector2(28, -224);
            itemIcon = UiKit.Image("Icon", itemBg.transform, null, Color.white, new Vector2(.02f, -.15f), new Vector2(.38f, 1.15f), true);
            Image itemBack = UiKit.Image("Bar", itemBg.transform, ArtLibrary.Pill, new Color(1, 1, 1, .15f), new Vector2(.42f, .32f), new Vector2(.92f, .68f));
            itemFill = UiKit.Image("Fill", itemBack.transform, ArtLibrary.Pill, UiKit.Gold, Vector2.zero, Vector2.one);
            itemPill.gameObject.SetActive(false);
            shieldIcon = UiKit.Image("Shield", shakeRoot, ArtLibrary.Get("item_barrier") ?? ArtLibrary.Ring, Color.white, Vector2.zero, Vector2.zero, true);
            UiKit.At(shieldIcon.rectTransform, new Vector2(1, 1), new Vector2(84, 84), new Vector2(.5f, .5f)).anchoredPosition = new Vector2(-66 - 4 * 80, -182);
            shieldIcon.enabled = false;

            Image dotsPill = UiKit.Image("BeatDots", shakeRoot, ArtLibrary.Pill, UiKit.Ink, Vector2.zero, Vector2.zero);
            UiKit.At(dotsPill.rectTransform, new Vector2(.5f, 1), new Vector2(340, 70), new Vector2(.5f, 1)).anchoredPosition = new Vector2(-28, -40);
            for (int i = 0; i < 8; i++)
            {
                float x = 37 + i * 38f;
                dots[i] = UiKit.Image("Dot" + i, dotsPill.transform, ArtLibrary.Circle, new Color(.3f, .4f, .52f), Vector2.zero, Vector2.zero);
                UiKit.At(dots[i].rectTransform, new Vector2(0, .5f), new Vector2(26, 26)).anchoredPosition = new Vector2(x, 0);
                halfDots[i] = UiKit.Image("Half" + i, dotsPill.transform, ArtLibrary.Diamond, UiKit.Offbeat, Vector2.zero, Vector2.zero);
                UiKit.At(halfDots[i].rectTransform, new Vector2(0, .5f), new Vector2(16, 16)).anchoredPosition = new Vector2(x + 19f, 0);
            }

            Image scorePill = UiKit.Image("ScorePill", shakeRoot, ArtLibrary.Pill, UiKit.Ink, Vector2.zero, Vector2.zero);
            UiKit.At(scorePill.rectTransform, new Vector2(1, 1), new Vector2(250, 96), new Vector2(1, 1)).anchoredPosition = new Vector2(-140, -28);
            Image star = UiKit.Image("Star", scorePill.transform, ArtLibrary.Star, Color.white, Vector2.zero, Vector2.zero, true);
            UiKit.At(star.rectTransform, new Vector2(0, .5f), new Vector2(46, 46)).anchoredPosition = new Vector2(36, 0);
            score = UiKit.Label("0", scorePill.transform, 44, Color.white, new Vector2(.26f, .05f), new Vector2(.93f, .95f), TextAnchor.MiddleRight, FontStyle.Bold);
            score.horizontalOverflow = HorizontalWrapMode.Overflow; // never wraps; best-fit shrinks long scores

            Button pause = UiKit.Button("", root, UiKit.Ink, Vector2.zero, Vector2.zero, () => onPause());
            UiKit.At((RectTransform)pause.transform, new Vector2(1, 1), new Vector2(104, 96), new Vector2(1, 1)).anchoredPosition = new Vector2(-28, -28);
            for (int i = 0; i < 2; i++)
            {
                Image bar = UiKit.Image("Bar", pause.transform, null, Color.white, Vector2.zero, Vector2.zero);
                UiKit.At(bar.rectTransform, new Vector2(.5f, .5f), new Vector2(14, 40)).anchoredPosition = new Vector2(i == 0 ? -12 : 12, 0);
            }

            for (int i = 0; i < hearts.Length; i++)
            {
                Sprite heart = ArtLibrary.Get("icon_heart");
                hearts[i] = UiKit.Image("Heart" + i, shakeRoot, heart ?? ArtLibrary.Circle, heart != null ? Color.white : UiKit.Coral, Vector2.zero, Vector2.zero, true);
                UiKit.At(hearts[i].rectTransform, new Vector2(1, 1), new Vector2(76, 76), new Vector2(.5f, .5f)).anchoredPosition = new Vector2(-66 - (hearts.Length - 1 - i) * 80, -182);
            }

            comboBadge = UiKit.Rect("Combo", shakeRoot, Vector2.zero, Vector2.zero);
            UiKit.At(comboBadge, new Vector2(1, 1), new Vector2(300, 170), new Vector2(1, 1)).anchoredPosition = new Vector2(-24, -236);
            comboBadge.localRotation = Quaternion.Euler(0, 0, 6);
            comboFrame = UiKit.Image("Frame", comboBadge, ArtLibrary.Rounded, Color.white, new Vector2(-.02f, -.02f), new Vector2(1.02f, 1.02f));
            UiKit.Image("Bg", comboBadge, ArtLibrary.Rounded, new Color(.05f, .06f, .1f, .88f), Vector2.zero, Vector2.one);
            comboLabel = UiKit.Label("COMBO", comboBadge, 38, Color.white, new Vector2(.08f, .68f), new Vector2(.92f, .95f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            combo = UiKit.Label("x0", comboBadge, 92, UiKit.Gold, new Vector2(.05f, .2f), new Vector2(.95f, .76f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            combo.resizeTextForBestFit = false;
            gaugeBack = UiKit.Image("GaugeBack", comboBadge, ArtLibrary.Pill, new Color(1, 1, 1, .15f), new Vector2(.1f, .07f), new Vector2(.9f, .17f));
            gaugeFill = UiKit.Image("GaugeFill", gaugeBack.transform, ArtLibrary.Pill, UiKit.Gold, Vector2.zero, new Vector2(0, 1));

            Image bannerBg = UiKit.Image("OffbeatBanner", root, ArtLibrary.Pill, new Color(.36f, .12f, .5f, .88f), Vector2.zero, Vector2.zero);
            bannerRect = UiKit.At(bannerBg.rectTransform, new Vector2(.5f, 1), new Vector2(640, 84), new Vector2(.5f, 1));
            bannerRect.anchoredPosition = new Vector2(0, -132);
            banner = UiKit.Label("♪ 裏拍がくるよ！ 拍と拍のあいだ", bannerBg.transform, 40, Color.white, new Vector2(.04f, .05f), new Vector2(.96f, .95f));

            // owner round 9: the count used to sit in the middle of the lanes and hid the targets already coming;
            // it now punches in at the side above the wall (same place as the boss countdown)
            countIn = UiKit.Label("", root, 220, Color.white, new Vector2(.6f, .6f), new Vector2(.98f, .78f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            diagnostics = UiKit.Label("", root, 30, new Color(.6f, 1f, 1f), new Vector2(.02f, .62f), new Vector2(.6f, .84f), TextAnchor.UpperLeft, FontStyle.Normal, true);
        }

        public void ResetRun()
        {
            lastCombo = lastScore = lastDistance = lastCoins = -1; lastHp = hearts.Length;
            for (int i = 0; i < hearts.Length; i++) { heartLostAt[i] = -9; heartGainAt[i] = -9; }
        }

        public void PunchCoins() { coinPopAt = Time.unscaledTime; }
        public void PunchCombo() { comboBigAt = Time.unscaledTime; }
        /// <summary>Edge-only shake for HUD and frame (never the road or slash line). Amplitude in reference px.</summary>
        public void Shake(float px) { shakeAt = Time.unscaledTime; shakePx = px; }

        public void Render(RunSnapshot s, double beat, bool offbeatChunk, bool preview, string countText, EffectLevel effects)
        {
            float now = Time.unscaledTime;
            if (s.Distance != lastDistance) { lastDistance = s.Distance; distance.text = s.Distance + " m"; }
            if (s.Score != lastScore) { lastScore = s.Score; score.text = s.Score.ToString("N0"); }
            if (s.Coins != lastCoins) { lastCoins = s.Coins; coins.text = s.Coins.ToString(); }
            coinX2.transform.parent.gameObject.SetActive(s.Fever);
            float coinPop = 1f + .2f * Anim.Punch(Mathf.Clamp01((now - coinPopAt) / .2f));
            coinPill.localScale = new Vector3(coinPop, coinPop, 1);

            for (int i = 0; i < hearts.Length; i++)
            {
                bool alive = i < s.Hp;
                hearts[i].enabled = i < Mathf.Max(1, s.MaxHp);
                if (!alive && i < lastHp && heartLostAt[i] < 0) heartLostAt[i] = now;
                if (alive && i >= lastHp) { heartLostAt[i] = -9; heartGainAt[i] = now; } // healed by a boss win
                float lost = heartLostAt[i] > 0 ? Mathf.Clamp01((now - heartLostAt[i]) / .35f) : 1f;
                float scale = alive ? 1f + .6f * Anim.Punch(Mathf.Clamp01((now - heartGainAt[i]) / .5f)) : Mathf.Lerp(1.35f, .82f, lost);
                // last remaining heart breathes slowly (0.5 Hz, scale only — no red flashing)
                if (alive && s.Hp == 1) scale *= 1f + .06f * Mathf.Sin(now * Mathf.PI);
                hearts[i].rectTransform.localScale = Vector3.one * scale;
                hearts[i].rectTransform.localRotation = Quaternion.Euler(0, 0, alive ? 0 : -18f * lost);
                hearts[i].color = alive ? Color.white : new Color(.25f, .25f, .3f, Mathf.Lerp(1f, .5f, lost));
            }
            lastHp = s.Hp;
            bool item = s.Boost || s.Giant;
            itemPill.gameObject.SetActive(item);
            if (item)
            {
                string icon = s.Boost ? "item_booster" : "item_giant";
                Sprite sprite = ArtLibrary.Get(icon);
                if (itemIcon.sprite != sprite) itemIcon.sprite = sprite;
                float left = s.Boost ? s.Boost01 : s.Giant01;
                itemFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(left), 1);
                // round 27: the giant's gauge turns red and blinks on its last three beats (the end must be seen coming)
                bool ending = s.Giant && !s.Boost && s.GiantBeatsLeft < 3;
                itemFill.color = s.Boost ? UiKit.Gold : ending ? Color.Lerp(UiKit.Coral, Color.white, Mathf.Abs(Mathf.Sin((float)s.GiantBeatsLeft * Mathf.PI))) : UiKit.Gold;
                itemPill.localScale = Vector3.one * (ending ? 1f + .08f * Mathf.Abs(Mathf.Sin((float)s.GiantBeatsLeft * Mathf.PI)) : 1f);
            }
            shieldIcon.enabled = s.Shields > 0 && shieldIcon.sprite != null;
            if (shieldIcon.enabled) shieldIcon.rectTransform.localScale = Vector3.one * (1f + .05f * Mathf.Sin(now * 3f));

            if (s.Combo != lastCombo)
            {
                if (s.Combo > lastCombo) comboPopAt = now;
                lastCombo = s.Combo;
                combo.text = "x" + s.Combo;
            }
            comboBadge.gameObject.SetActive(s.Combo >= 2);
            Color tier = TierColor(s.Combo, effects == EffectLevel.Weak ? 0f : now); // weak: fixed colour
            comboFrame.color = new Color(tier.r, tier.g, tier.b, s.Combo >= 10 ? 1f : 0f);
            combo.color = s.Combo >= 50 ? tier : UiKit.Gold;
            int labelKey = s.Fever ? s.FeverLevel : 0;
            if (labelKey != lastLabelKey) { lastLabelKey = labelKey; comboLabel.text = s.Fever ? (s.FeverLevel > 1 ? "FEVER Lv" + s.FeverLevel : "FEVER!!") : "COMBO"; }
            float big = Anim.Punch(Mathf.Clamp01((now - comboBigAt) / .25f));
            float pop = 1f + .25f * Mathf.Clamp01(1f - (now - comboPopAt) / .15f) + .5f * big;
            combo.rectTransform.localScale = new Vector3(pop, pop, 1);
            // FEVER gauge: 0→50 before FEVER, then progress toward the next level
            float progress = s.Combo < 50 ? s.Combo / 50f : ((s.Combo - 50) % 50) / 50f;
            gaugeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
            gaugeFill.color = s.Combo >= 40 && s.Combo < 50 ? UiKit.Gold : tier;
            if (s.Fever && effects != EffectLevel.Weak)
            {
                float beatPulse = 1f + .04f * Mathf.Pow(1f - (float)(beat - System.Math.Floor(beat)), 4f); // scale, not brightness
                comboBadge.localScale = new Vector3(beatPulse, beatPulse, 1);
            }
            else comboBadge.localScale = Vector3.one;
            if (Compact) comboBadge.localScale *= .7f;

            double local = ((beat % 8) + 8) % 8;
            int whole = (int)System.Math.Floor(local);
            double frac = local - whole;
            for (int i = 0; i < 8; i++)
            {
                bool on = beat >= 0 && i == whole;
                float flash = on ? 1f - (float)frac * .6f : 0f;
                // step-up preview before the first offbeat chunk: dots fill purple one per beat (effects E15)
                Color idle = preview && beat >= 0 && i <= whole ? new Color(UiKit.Offbeat.r * .8f, UiKit.Offbeat.g * .6f, UiKit.Offbeat.b * .9f) : new Color(.3f, .4f, .52f);
                dots[i].color = on ? Color.Lerp(idle, i == 0 ? UiKit.Gold : UiKit.Cyan, flash) : idle;
                dots[i].rectTransform.localScale = Vector3.one * (on ? 1f + .35f * flash : 1f);
                halfDots[i].enabled = offbeatChunk && i < 7;
                bool halfOn = offbeatChunk && i == whole && frac >= .5 && frac < .75;
                halfDots[i].color = halfOn ? Color.white : new Color(UiKit.Offbeat.r, UiKit.Offbeat.g, UiKit.Offbeat.b, .55f);
                halfDots[i].rectTransform.localScale = Vector3.one * (halfOn ? 1.6f : 1f);
            }
            // owner round 2: no text banner for offbeats — the purple beat lines on the road say it
            bannerRect.gameObject.SetActive(false);
            if (countIn.text != (countText ?? "")) countIn.text = countText ?? "";

            float shakeT = (now - shakeAt) / .09f;
            shakeRoot.anchoredPosition = shakeT < 1f ? new Vector2(Mathf.Sin(shakeT * Mathf.PI * 6f) * shakePx * (1f - shakeT), 0) : Vector2.zero;
        }

        public void SetDiagnostics(string text) { if (diagnostics.text != (text ?? "")) diagnostics.text = text ?? ""; }
    }
}
