using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// The stage boss standing at the far end of the road (owner round 2, plan A): it fires shots down the lanes, slashed
    /// shots fly back as comets along the outside of the road and hit it, the finisher mash knocks it out. Drawn behind the
    /// road so it never covers a target; everything here is presentation only.
    /// </summary>
    public sealed class BossView
    {
        sealed class Comet { public RectTransform Rect; public Image Image, Glow; public Vector2 From, Control, To; public float Born, Life; public int Damage; public bool Small; }
        sealed class Floater { public Text Text; public float Born; public Vector2 From; }

        enum Phase { Hidden, Entering, Fighting, Defeated, Escaping }

        RectTransform layer, body, cometLayer;
        Image image, glow, stunStars, dropShadow;
        float attackUntil = -9, hurtUntil = -9, landedAt = -9, poundAt = -9, finisherBlend;
        RectMask2D clip;
        /// <summary>The finishing mash is open: the boss staggers forward to the middle of the screen.</summary>
        public bool Finisher;
        /// <summary>0..1 of the finishing mash (the boss swells and cracks as it fills).</summary>
        public float FinisherProgress;
        public Sprite CurrentSprite { get { return image.sprite; } }
        public bool InFinisher { get { return finisherBlend > .5f; } }
        bool landed;
        /// <summary>The boss hit the ground at the end of its entrance (for the shake and the boom).</summary>
        public System.Action Landed;
        /// <summary>The 3D scene the boss is drawn in (it stands on the road; the UI images only keep the timing).</summary>
        public World3D World;
        Rect lastRect;
        readonly List<Comet> comets = new List<Comet>();
        readonly Stack<Comet> freeComets = new Stack<Comet>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly Stack<Floater> freeFloaters = new Stack<Floater>();
        Phase phase = Phase.Hidden;
        float phaseAt, hitAt = -9, spitAt = -9;
        int shownStage = -1;
        string spriteId;
        public EffectLevel Effects = EffectLevel.Strong;
        /// <summary>Below half HP (red, bigger, shaking) / gathering a charge attack (purple glow).</summary>
        public bool Rage, Charging;
        public System.Action<Vector2> Exploded;  // local position, for debris and coins
        public System.Action CometHit;
        /// <summary>A comet struck (canvas-local point, damage): sparks instead of damage numbers (owner round 8).</summary>
        public System.Action<Vector2, int> HitAt;
        public int LastHitDamage { get; private set; }
        /// <summary>Remaining HP 0..1: the boss cracks, darkens and sways as it drops (it replaces reading the bar).</summary>
        public float Hp01 = 1f;
        /// <summary>Screen row of its feet (fraction from the top): the road lines stop there.</summary>
        public float FootY { get; private set; }
        /// <summary>Screen height (fraction) the boss covers.</summary>
        public float ScreenHeight { get { return World != null ? World.BossScreenHeight() : .25f; } }
        float wear;

        public bool Visible { get { return phase != Phase.Hidden; } }
        /// <summary>Canvas-local centre of the boss (comet target).</summary>
        public Vector2 Center
        {
            get
            {
                if (World == null) return body.anchoredPosition + new Vector2(0, body.sizeDelta.y * .45f);
                Vector2 f = World.BossCenter();
                return RoadGraphic.ToLocal(lastRect, f.x, f.y);
            }
        }

        public void Build(RectTransform parent, RectTransform comets)
        {
            layer = UiKit.Rect("Boss", parent, Vector2.zero, Vector2.one);
            clip = layer.gameObject.AddComponent<RectMask2D>(); // hides the part still under the road while it rises
            cometLayer = comets;
            glow = UiKit.Image("BossGlow", layer, ArtLibrary.Glow, new Color(1f, .3f, .55f, 0), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            dropShadow = UiKit.Image("DropShadow", layer, ArtLibrary.SoftShadow, new Color(0, 0, 0, 0), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            body = UiKit.Rect("BossBody", layer, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            body.pivot = new Vector2(.5f, 0);
            image = body.gameObject.AddComponent<Image>();
            image.raycastTarget = false; image.preserveAspect = true;
            stunStars = UiKit.Image("Stun", body, ArtLibrary.Get("vfx_sparkle") ?? ArtLibrary.Star, UiKit.Gold, new Vector2(.25f, .92f), new Vector2(.75f, 1.12f), true);
            layer.gameObject.SetActive(false);
        }

        public void Reset()
        {
            phase = Phase.Hidden; shownStage = -1; wear = 0; Hp01 = 1f;
            layer.gameObject.SetActive(false);
            foreach (Comet c in comets) { c.Rect.gameObject.SetActive(false); freeComets.Push(c); }
            comets.Clear();
            foreach (Floater f in floaters) { f.Text.gameObject.SetActive(false); freeFloaters.Push(f); }
            floaters.Clear();
        }

        /// <summary>Called every frame with the boss that closes the current stage.</summary>
        public void Tick(RunEngine engine, double beat, string bossSprite, Rect rootRect, float unit, bool finisher)
        {
            float now = Time.unscaledTime;
            int stage = engine.StageAt(beat);
            double start = engine.BossStartBeat(stage);
            // entrance over the last two countdown beats: lands exactly on "BOSS!!" (start - 4), when its first shot appears
            const double EnterFrom = 6, LandAt = 4;
            bool inWindow = engine.HasBossFights && beat >= start - EnterFrom && beat < engine.StageStartBeat(stage + 1);
            if (inWindow && shownStage != stage && phase == Phase.Hidden)
            {
                shownStage = stage; phase = Phase.Entering; phaseAt = now; landed = false;
                spriteId = bossSprite; wear = 0;
                if (World != null) World.SetBossKind(bossSprite);
                image.sprite = ArtLibrary.Get(bossSprite) ?? ArtLibrary.Circle;
                layer.gameObject.SetActive(true);
            }
            if (phase == Phase.Entering && beat >= start) phase = Phase.Fighting;
            if (phase == Phase.Entering && !landed && beat >= start - LandAt) { landed = true; landedAt = now; if (Landed != null) Landed(); }
            if (phase == Phase.Entering || phase == Phase.Fighting)
            {
                // poses: attack while spitting, hurt when hit, rage below half HP
                string pose = now < hurtUntil ? "_hurt" : now < attackUntil ? "_attack" : Rage ? "_rage" : "";
                Sprite s = pose.Length > 0 ? ArtLibrary.Get(spriteId + pose) : null;
                image.sprite = s ?? ArtLibrary.Get(spriteId) ?? ArtLibrary.Circle;
            }
            // safety net: never carry a boss into the next stage
            if ((phase == Phase.Entering || phase == Phase.Fighting) && stage != shownStage) Hide();
            lastRect = rootRect;
            if (phase == Phase.Hidden) { if (World != null) World.PlaceBoss(false, null, 0, 0, 0, Vector2.one, 0, Color.white, Color.clear, 0, false, 0); UpdateComets(now, unit); return; }

            // layout: feet on the horizon, centred on the road's vanishing point; big (owner round 6: "ボスを前面に")
            finisherBlend = Mathf.MoveTowards(finisherBlend, finisher && phase == Phase.Fighting ? 1f : phase == Phase.Defeated ? finisherBlend : 0f, Time.unscaledDeltaTime / .35f);
            float fb = Anim.InOutSine(finisherBlend);
            // closer than the far end of the road and bigger (owner round 7: "もうちょっと手前に")
            float height = rootRect.height * .29f * (1f + .25f * fb);
            float aspect = image.sprite != null ? image.sprite.rect.width / image.sprite.rect.height : 1f;
            Vector2 foot = Vector2.Lerp(RoadGraphic.ToLocal(rootRect, .5f, .39f), RoadGraphic.ToLocal(rootRect, .5f, .58f), fb); // head clear of the HUD
            // clip below the horizon only while rising out of the road
            clip.padding = new Vector4(0, phase == Phase.Entering && !Drops ? rootRect.height * (1f - Perspective.HorizonY - .012f) : 0f, 0, 0);
            float t = now - phaseAt;
            float scale = 1f, alpha = 1f, rise = 0, wobble = 0;
            float shadowAlpha = 0;
            switch (phase)
            {
                case Phase.Entering:
                {
                    float e = Mathf.Clamp01((float)((beat - (start - EnterFrom)) / (EnterFrom - LandAt)));
                    if (Drops)
                    {
                        // drops from the sky: the shadow grows first, then a heavy fall and a squash on landing
                        float fall = Mathf.Clamp01((e - .55f) / .45f);
                        rise = Mathf.Lerp(height * 2.6f, 0, fall * fall);
                        shadowAlpha = .5f * Mathf.Clamp01(e * 1.6f);
                        alpha = e > .5f ? 1f : 0f;
                    }
                    else
                    {
                        // rises out of the road: the road (drawn in front) hides the part still underground
                        rise = Mathf.Lerp(-height * 1.05f, 0, Anim.OutCubic(e));
                        wobble = 4f * Mathf.Sin(now * 40f) * (1f - e);
                    }
                    if (landed)
                    {
                        float land = Mathf.Clamp01((now - landedAt) / .3f);
                        scale = 1f + .18f * Anim.Punch(land);
                        wobble = 0;
                    }
                    break;
                }
                case Phase.Fighting:
                    scale = (Rage ? 1.1f : 1f) + .025f * Mathf.Sin((float)beat * Mathf.PI * 2f) + .08f * Mathf.Clamp01(1f - (now - spitAt) / .15f);
                    // lunge forward when spitting, rock back when hit
                    rise -= height * .06f * Mathf.Clamp01(1f - (now - spitAt) / .2f);
                    rise += height * .08f * Mathf.Clamp01((hurtUntil - now) / .25f);
                    wobble += 6f * Mathf.Sin((float)beat * Mathf.PI) ;
                    if (Rage) wobble += 2.5f * Mathf.Sin(now * 31f);
                    wobble += 5f * (1f - Hp01) * Mathf.Sin(now * 2.6f); // groggy: a slow sway that grows as it weakens
                    if (Charging) scale += .05f * Mathf.Sin(now * 14f);
                    if (finisher)
                    {
                        // staggering at the front: swells and shudders more with every hit of the mash
                        float pound = Mathf.Clamp01(1f - (now - poundAt) / .12f);
                        wobble = 9f * Mathf.Sin(now * 9f) + 10f * pound * Mathf.Sin(now * 80f);
                        scale *= 1f + .14f * FinisherProgress + .08f * pound;
                        rise += height * .04f * pound;
                    }
                    break;
                case Phase.Defeated:
                {
                    // a short violent shake, then it bursts (the pieces fly from the playfield's explosion)
                    float d = Mathf.Clamp01(t / .5f);
                    scale = 1f + .25f * d + .1f * Mathf.Sin(t * 70f);
                    alpha = t < .32f ? 1f : 0f; // the shards take over at the burst
                    wobble = 18f * Mathf.Sin(t * 60f);
                    if (!burst && t >= .32f)
                    {
                        burst = true;
                        if (Exploded != null) Exploded(Center);
                        if (World != null) World.Shatter(image.sprite, InsideColor(spriteId));
                    }
                    if (d >= 1f) Hide();
                    break;
                }
                case Phase.Escaping:
                    float k = Mathf.Clamp01(t / .9f);
                    scale = Mathf.Lerp(1f, .3f, k); rise = Mathf.Lerp(0, height * .9f, Anim.InOutSine(k)); alpha = 1f - k * k;
                    wobble = 12f * Mathf.Sin(t * 20f);
                    if (k >= 1f) Hide();
                    break;
            }
            float hit = Mathf.Clamp01(1f - (now - hitAt) / .18f);
            RectTransform sh = dropShadow.rectTransform;
            sh.sizeDelta = new Vector2(height * aspect * .9f, height * .22f) * Mathf.Lerp(.4f, 1f, shadowAlpha * 2f);
            sh.anchoredPosition = foot + new Vector2(0, height * .03f);
            dropShadow.color = new Color(0, 0, 0, shadowAlpha);
            float shake = hit * Mathf.Sin(now * 90f) * unit * .012f;
            body.sizeDelta = new Vector2(height * aspect, height);
            body.anchoredPosition = foot + new Vector2(shake, rise);
            body.localScale = new Vector3(scale * (1f + .06f * hit), scale * (1f - .05f * hit), 1);
            body.localRotation = Quaternion.Euler(0, 0, wobble);
            float rage = Rage && phase == Phase.Fighting ? .25f + .1f * Mathf.Sin(now * 6f) : 0f; // steady red tint, slow (no strobe)
            // wear: cracks spread with lost HP, all the way open during the finishing mash
            float wearTarget = phase == Phase.Defeated ? 1f : Mathf.Max(.7f * (1f - Hp01), finisher && phase == Phase.Fighting ? .55f + .45f * FinisherProgress : 0f);
            if (phase == Phase.Entering) wearTarget = 0;
            wear = Mathf.MoveTowards(wear, wearTarget, Time.unscaledDeltaTime * 1.5f);
            float dull = 1f - .16f * wear; // a little sooty as it takes damage
            image.color = new Color(dull, Mathf.Lerp(1f, .55f, Mathf.Max(hit, rage)) * dull, Mathf.Lerp(1f, .6f, Mathf.Max(hit, rage)) * dull, alpha);
            stunStars.enabled = finisher && phase == Phase.Fighting;
            if (stunStars.enabled) stunStars.rectTransform.localRotation = Quaternion.Euler(0, 0, now * 120f);
            RectTransform g = glow.rectTransform;
            g.sizeDelta = new Vector2(height * 1.8f, height * 1.3f);
            g.anchoredPosition = foot + new Vector2(0, height * .5f + rise);
            Color glowColor = Charging ? new Color(.75f, .3f, 1f) : Rage ? new Color(1f, .2f, .15f) : new Color(1f, .35f, .55f);
            float glowAlpha = phase == Phase.Fighting ? (Charging ? .6f + .2f * Mathf.Sin(now * 10f) : Rage ? .5f : .25f) + .35f * hit : 0f;
            if (phase == Phase.Defeated) { glowColor = new Color(1f, .95f, .8f); glowAlpha = t < .32f ? Mathf.Clamp01(t / .3f) : 0f; }
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha * (Effects == EffectLevel.Weak ? .5f : 1f));
            if (World != null)
            {
                // the 3D scene draws the boss standing on the road; the UI copies stay hidden
                float footY = (rootRect.yMax - foot.y) / rootRect.height;
                FootY = footY;
                World.SetBossWear(wear, phase == Phase.Defeated ? Mathf.Clamp01((now - phaseAt) / .3f) : 0f); // light pours out of the cracks before it bursts
                float ground = phase == Phase.Defeated || phase == Phase.Escaping ? .3f : Drops && phase == Phase.Entering ? shadowAlpha : .5f;
                World.PlaceBoss(true, image.sprite, footY, (rise + (body.anchoredPosition.x - foot.x) * 0) / rootRect.height, height / rootRect.height,
                    new Vector2(body.localScale.x, body.localScale.y), wobble, image.color, glow.color, ground * alpha, stunStars.enabled, now * 120f);
                image.enabled = false; glow.enabled = false; dropShadow.enabled = false; stunStars.enabled = false;
            }
            UpdateComets(now, unit);
            UpdateFloaters(now, unit);
        }

        /// <summary>What the boss is made of, for the broken faces of its shards.</summary>
        static Color InsideColor(string id)
        {
            switch (id)
            {
                case "boss_toaster": return new Color(.78f, .78f, .82f);   // steel
                case "boss_icecream": return new Color(1f, .86f, .88f);    // strawberry-vanilla
                case "boss_donut": return new Color(1f, .86f, .62f);       // fluffy dough
                case "boss_zenmai": return new Color(.85f, .8f, .65f);     // bare tin and brass
                case "boss_kaminari": return new Color(.75f, .78f, .9f);   // storm-cloud grey
                case "boss_yukidaruma": return new Color(.95f, .98f, 1f);  // packed snow
                default: return new Color(1f, .86f, .92f);                 // onion flesh
            }
        }

        /// <summary>Bosses that drop in from the sky (the others rise out of the road).</summary>
        bool Drops { get { return spriteId == "boss_toaster" || spriteId == "boss_icecream" || spriteId == "boss_kaminari" || spriteId == "boss_yukidaruma"; } }

        void Hide()
        {
            phase = Phase.Hidden; Rage = false; Charging = false; layer.gameObject.SetActive(false);
            if (World != null) World.PlaceBoss(false, null, 0, 0, 0, Vector2.one, 0, Color.white, Color.clear, 0, false, 0);
        }

        public void Spit() { spitAt = Time.unscaledTime; attackUntil = spitAt + .22f; }

        /// <summary>One hit of the finishing mash.</summary>
        public void Pound() { poundAt = Time.unscaledTime; hitAt = poundAt; hurtUntil = poundAt + .12f; }

        bool burst;

        public void Defeat()
        {
            if (phase == Phase.Hidden) return;
            phase = Phase.Defeated; phaseAt = Time.unscaledTime; burst = false;
        }

        public void Escape()
        {
            if (phase == Phase.Hidden) return;
            phase = Phase.Escaping; phaseAt = Time.unscaledTime;
        }

        /// <summary>A slashed shot flies back to the boss along the outside of the road (never across the lanes' targets).</summary>
        public void Reflect(Sprite shot, Vector2 from, int lane, float unit, int damage, bool small = false)
        {
            if (phase == Phase.Hidden) return;
            Comet c = freeComets.Count > 0 ? freeComets.Pop() : NewComet();
            c.Rect.gameObject.SetActive(true);
            c.Image.sprite = shot ?? ArtLibrary.Star;
            c.From = from; c.To = Center;
            float side = lane == 0 ? -1f : lane == 2 ? 1f : (Random.value < .5f ? -1f : 1f);
            Rect r = layer.rect;
            c.Control = new Vector2(r.center.x + side * r.width * .62f, Mathf.Lerp(from.y, c.To.y, .45f));
            c.Born = Time.unscaledTime; c.Life = small ? .22f : .3f; c.Damage = damage; c.Small = small;
            float size = unit * (small ? .07f : .12f);
            c.Rect.sizeDelta = new Vector2(size, size);
            c.Glow.rectTransform.sizeDelta = new Vector2(size * 2.4f, size * 2.4f);
            comets.Add(c);
        }

        Comet NewComet()
        {
            RectTransform rect = UiKit.Rect("Comet", cometLayer, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            Image glowImage = UiKit.Image("Glow", rect, ArtLibrary.Glow, new Color(1f, .85f, .35f, .9f), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            Image img = UiKit.Image("Shot", rect, null, Color.white, Vector2.zero, Vector2.one, true);
            return new Comet { Rect = rect, Image = img, Glow = glowImage };
        }

        void UpdateComets(float now, float unit)
        {
            for (int i = comets.Count - 1; i >= 0; i--)
            {
                Comet c = comets[i];
                float t = (now - c.Born) / c.Life;
                if (t >= 1f)
                {
                    c.Rect.gameObject.SetActive(false); freeComets.Push(c); comets.RemoveAt(i);
                    hitAt = now;
                    if (!c.Small) hurtUntil = now + .25f;
                    LastHitDamage = c.Damage;
                    if (HitAt != null) HitAt(Center, c.Damage);
                    if (CometHit != null) CometHit();
                    continue;
                }
                c.To = Center;
                float e = Anim.InOutSine(t);
                c.Rect.anchoredPosition = (1 - e) * (1 - e) * c.From + 2 * (1 - e) * e * c.Control + e * e * c.To;
                float s = Mathf.Lerp(1.2f, .55f, t);
                c.Rect.localScale = new Vector3(s, s, 1);
                c.Rect.localRotation = Quaternion.Euler(0, 0, now * 900f);
            }
        }

        void ShowDamage(int damage, float unit)
        {
            if (damage <= 0) return;
            Floater f = freeFloaters.Count > 0 ? freeFloaters.Pop() : NewFloater();
            f.Text.gameObject.SetActive(true);
            f.Text.text = "-" + damage;
            f.Born = Time.unscaledTime;
            f.From = Center + new Vector2(Random.Range(-.08f, .08f) * unit, .05f * unit);
            floaters.Add(f);
        }

        Floater NewFloater()
        {
            Text text = UiKit.Label("", cometLayer, 64, UiKit.Gold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false);
            text.rectTransform.sizeDelta = new Vector2(220, 90);
            UiKit.AddOutline(text.gameObject, new Color(.35f, .05f, .1f, .9f), 3);
            return new Floater { Text = text };
        }

        void UpdateFloaters(float now, float unit)
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                Floater f = floaters[i];
                float t = (now - f.Born) / .6f;
                if (t >= 1f) { f.Text.gameObject.SetActive(false); freeFloaters.Push(f); floaters.RemoveAt(i); continue; }
                f.Text.rectTransform.anchoredPosition = f.From + new Vector2(0, unit * .08f * Anim.OutCubic(t));
                f.Text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Anim.OutCubic(Mathf.Min(1, t * 3)));
                f.Text.canvasRenderer.SetAlpha(t < .7f ? 1f : 1f - (t - .7f) / .3f);
            }
        }
    }
}
