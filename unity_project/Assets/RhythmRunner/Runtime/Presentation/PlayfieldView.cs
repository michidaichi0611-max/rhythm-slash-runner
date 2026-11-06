using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>Simple reuse pool for UI views. Reset happens in the caller's Acquire path.</summary>
    public sealed class ViewPool<T> where T : Component
    {
        readonly Stack<T> free = new Stack<T>();
        readonly System.Func<T> create;
        public ViewPool(System.Func<T> create) { this.create = create; }
        public T Get() { T item = free.Count > 0 ? free.Pop() : create(); item.gameObject.SetActive(true); return item; }
        public void Release(T item) { item.gameObject.SetActive(false); free.Push(item); }
    }

    /// <summary>Cosmetic weapon parameters. Never affects judgement or score (GAME_SPEC 9).</summary>
    public struct WeaponVisual
    {
        public float Scale, RestAngle, PivotY;
        public Color Trail;
        public string TrailSprite;

        public static WeaponVisual For(string id)
        {
            if (id != null && id.Contains("wooden")) return new WeaponVisual { Scale = 1.08f, RestAngle = -20, PivotY = .08f, Trail = UiKit.Hex("9BD86A"), TrailSprite = "vfx_trail_wood" };
            if (id != null && id.Contains("banana")) return new WeaponVisual { Scale = .85f, RestAngle = -35, PivotY = .18f, Trail = UiKit.Hex("FFD84A"), TrailSprite = "vfx_trail_banana" };
            // round 22
            if (id != null && id.Contains("roller")) return new WeaponVisual { Scale = .95f, RestAngle = -30, PivotY = .1f, Trail = UiKit.Hex("BFE6FF"), TrailSprite = "vfx_trail_tape" };
            if (id != null && id.Contains("saibashi")) return new WeaponVisual { Scale = 1.12f, RestAngle = -24, PivotY = .1f, Trail = UiKit.Hex("E8C48A"), TrailSprite = "vfx_trail_saibashi" };
            if (id != null && id.Contains("ice_claw")) return new WeaponVisual { Scale = .95f, RestAngle = -28, PivotY = .12f, Trail = UiKit.Hex("8FE3FF"), TrailSprite = "vfx_trail_ice" };
            if (id != null && id.Contains("cardboard")) return new WeaponVisual { Scale = 1.05f, RestAngle = -26, PivotY = .1f, Trail = UiKit.Hex("C9995E"), TrailSprite = "vfx_trail_cardboard" };
            if (id != null && id.Contains("smoke")) return new WeaponVisual { Scale = 1.05f, RestAngle = -26, PivotY = .1f, Trail = UiKit.Hex("D8DCE4"), TrailSprite = "vfx_trail_smoke" };
            if (id != null && id.Contains("lasso")) return new WeaponVisual { Scale = 1f, RestAngle = -30, PivotY = .1f, Trail = UiKit.Hex("C98B4E"), TrailSprite = "vfx_trail_lasso" };
            return new WeaponVisual { Scale = 1f, RestAngle = -28, PivotY = .12f, Trail = UiKit.Hex("5EE7FF"), TrailSprite = "vfx_slash" };
        }
    }

    /// <summary>
    /// Draws the run from the music beat alone: positions come from (event beat - current beat), never from
    /// accumulated deltaTime, and nothing here feeds back into judgement. Protected areas (review/round0 effects §8):
    /// the road trapezoid and slash line only ever carry targets, their slices for ~0.12 s and the approach rings.
    /// </summary>
    public sealed class PlayfieldView : MonoBehaviour
    {
        sealed class TargetView
        {
            public RectTransform Root;
            public Image Shadow, Contact, Glow, Ring, Diamond, OffRing;
            public SolidImage Body;
            /// <summary>The body's own colour before the stage's light and haze are applied.</summary>
            public Color BaseColor = Color.white;
            public bool Offbeat;
            /// <summary>Thickness as a fraction of the size (0 = a round shot, drawn as a ball).</summary>
            public float Thick;
        }

        sealed class Effect
        {
            public RectTransform Rect;
            public Image Image;
            public Text Text;
            public float Born, Life, Hold;
            public Vector2 Velocity;
            public float Spin, ScaleFrom, ScaleTo, Gravity;
            public Color Color;
            public bool Rise;
            /// <summary>A cut piece: it tumbles in 3D and shows its thickness (owner round 7: "ペラペラ").</summary>
            public bool Slice, Mirror;
            public Vector2 Solid;
            public float Tumble;
        }

        sealed class Streak { public RectTransform Rect; public Image Image; public Vector2 Dir; public float Born, Life, Speed, Start; }
        sealed class Post { public RectTransform Rect; public Image Image; public double Z; public float Lateral; public bool Passed; }

        sealed class Prop
        {
            public RectTransform Rect;
            public Image Image;
            public Text Label;
            public double Beat;
            public float Lateral;
            public float Size;
        }

        struct Pose { public string Sprite; public Vector2 Hand; public float Angle; }

        // Hand points (normalized sprite space, bottom-left origin) where the weapon grip sits, plus the resting blade angle.
        static readonly Dictionary<string, Vector2> Hands = new Dictionary<string, Vector2>
        {
            { "hamster_run_a", new Vector2(.94f, .46f) }, { "hamster_run_b", new Vector2(.94f, .46f) },
            { "hamster_run_c", new Vector2(.94f, .45f) }, { "hamster_run_d", new Vector2(.94f, .39f) },
            // generated art: slash_c reaches to the viewer's left, slash_l/slash_r reach right (see LaneSlash)
            { "hamster_slash_c", new Vector2(.31f, .48f) }, { "hamster_slash_down", new Vector2(.83f, .95f) }, { "hamster_slash_l", new Vector2(.95f, .53f) }, { "hamster_slash_r", new Vector2(.97f, .55f) },
            { "hamster_slash", new Vector2(.95f, .58f) }, { "hamster_hit", new Vector2(.93f, .5f) }, { "hamster_miss", new Vector2(.9f, .5f) },
            { "hamster_combo_pose", new Vector2(.9f, .92f) }, { "hamster_fever_dash_a", new Vector2(.92f, .45f) }, { "hamster_fever_dash_b", new Vector2(.93f, .48f) },
            { "hamster_back_ready", new Vector2(.96f, .42f) },
        };

        /// <summary>Pose per tapped lane, chosen by where the fist actually points in each generated sprite.</summary>
        static readonly string[] LaneSlash = { "hamster_slash_c", "hamster_slash_down", "hamster_slash_r" };
        static readonly string[] LaneSlashFallback = { "hamster_slash_c", "hamster_slash_l", "hamster_slash_r" };

        // Swing per lane (uGUI z rotation, + = counter-clockwise). The blade sweeps through "pointing up-screen" (toward the
        // target) and follows through toward the tapped lane (committee character #2).
        static readonly float[] SwingFrom = { -45f, -50f, 40f };
        static readonly float[] SwingTo = { 80f, 50f, -85f };
        const float SlashPose = .22f, HitPose = .45f, ComboPose = .4f, MissPose = .35f;

        RectTransform root;
        Image backdrop, feverBackdrop, feverAura;
        RoadGraphic road;
        LaneLinesGraphic laneLines;
        RectTransform propLayer, targetLayer, fxLayer, heroLayer, popupLayer, streakLayer, cometLayer;
        Image backdropNext, backdropLoop, feverTint, gate, mashRing;
        float zoomPhase;
        Text mashLabel;
        BeatLinesGraphic beatLines;
        readonly BossView boss = new BossView();
        readonly List<Streak> streaks = new List<Streak>();
        readonly Stack<Streak> freeStreaks = new Stack<Streak>();
        readonly Dictionary<string, HoldBodyGraphic> holdBodies = new Dictionary<string, HoldBodyGraphic>();
        readonly Stack<HoldBodyGraphic> freeHoldBodies = new Stack<HoldBodyGraphic>();
        readonly List<string> holdScratch = new List<string>();
        CatalogItem[] stages;
        RunEngine current;
        int theme, fadingTheme = -1, holdingLane = -1, slashFlip;
        float themeFadeAt = -9, streakCarry, mashHitAt = -9;
        double bpm = 120;
        string holdingKind;
        // round 3: the world (road, posts, wind) runs faster than the notes; notes keep their readable approach
        float worldSpeed = 1f, feverKickAt = -9, shieldPopAt = -9, criticalAt = -9, lastGhostAt = -9, giantScale = 1f, giantChangedAt = -9, lastFuseSpark = -9;
        int rushHits;
        Text rushCounter;
        float rushCounterAt = -9;
        double worldScroll, roadScroll, wallScroll, lastRenderBeat = double.NaN, nextPostZ;
        World3D world;
        float zoom1 = 1, zoom2 = 1, loopAlpha;
        /// <summary>Settings "背景の動きをおさえる": slower walls, fewer posts, no approaching background, no speed lines.</summary>
        public bool CalmMotion;
        public World3D World { get { return world; } }

        /// <summary>Light colour per stage (round 12): the same tint for the 3D objects and the UI targets.</summary>
        static readonly Dictionary<string, Color> LightColors = new Dictionary<string, Color>
        {
            { "stage_kitchen", new Color(1f, .96f, .88f) }, { "stage_toy_room", new Color(1f, .97f, 1f) }, { "stage_sky_orchard", new Color(.98f, 1f, 1f) },
            { "stage_neon_market", new Color(.9f, .84f, 1f) }, { "stage_candy_land", new Color(1f, .93f, .97f) }, { "stage_freezer", new Color(.88f, .95f, 1f) },
        };
        Color stageFog = Color.white, stageLight = Color.white;

        static readonly Dictionary<string, Color> FogColors = new Dictionary<string, Color>
        {
            { "stage_kitchen", new Color(.93f, .76f, .55f) }, { "stage_toy_room", new Color(.78f, .86f, .98f) }, { "stage_sky_orchard", new Color(.82f, .9f, 1f) },
            { "stage_neon_market", new Color(.3f, .18f, .38f) }, { "stage_candy_land", new Color(1f, .82f, .9f) }, { "stage_freezer", new Color(.74f, .87f, 1f) },
        };
        WallGraphic wallL, wallR;
        int wallTheme = -1;
        float rushDash;
        int postCount;
        readonly List<Post> posts = new List<Post>();
        readonly Stack<Post> freePosts = new Stack<Post>();
        Image rocket, rocketFlame, barrierBubble, guardDome;
        Text giantCount;
        double giantLeft;
        bool boostOn, giantOn, chargeGuard;
        int shields;
        readonly Dictionary<string, Sprite[]> halfSprites = new Dictionary<string, Sprite[]>();
        /// <summary>A roadside post rushed past the camera (for the whoosh).</summary>
        public System.Action PostPassed;
        /// <summary>World speed in beats of road per music beat (1 = notes' speed).</summary>
        public float WorldSpeed { get { return worldSpeed; } }
        readonly Image[] laneFlash = new Image[3];
        readonly Image[] lineFlash = new Image[3];
        readonly float[] laneFlashAt = { -9, -9, -9 };
        Image slashLine, slashGlow;
        RectTransform hero;
        Image heroImage, heroShadow, weapon;
        readonly Image[] ghosts = new Image[3];
        readonly float[] ghostAngles = new float[3];
        readonly Dictionary<string, TargetView> live = new Dictionary<string, TargetView>();
        // round 17 soldiers: the strike each soldier is on (it stands where that strike is), when it was last parried
        readonly Dictionary<string, ScheduledEvent> soldierLead = new Dictionary<string, ScheduledEvent>();
        readonly Dictionary<string, float> soldierParryAt = new Dictionary<string, float>();
        readonly Dictionary<string, int> soldierLaneBefore = new Dictionary<string, int>();
        readonly List<string> scratch = new List<string>();
        ViewPool<RectTransform> targetPool;
        readonly Dictionary<RectTransform, TargetView> targetViews = new Dictionary<RectTransform, TargetView>();
        readonly List<Effect> effects = new List<Effect>();
        readonly Stack<Effect> freeEffects = new Stack<Effect>();
        readonly Stack<Effect> freeTexts = new Stack<Effect>();
        readonly Stack<Effect> freeSlices = new Stack<Effect>();
        Image tapHand;
        float lastSweatAt = -9;
        /// <summary>Words under the boss prompts ("長押し！", "連打！！") until the player has beaten a boss once; then the hand alone.</summary>
        public bool HintWords = true;
        readonly Effect[] lanePopup = new Effect[3];
        readonly List<Prop> props = new List<Prop>();
        readonly Stack<Prop> freeProps = new Stack<Prop>();
        Prop bestFlagL, bestFlagR;
        float hitStopUntil = -1, slashEndAngle;
        readonly FlashBudget budget = new FlashBudget();
        float slashUntil = -1, missUntil = -1, hitUntil = -1, comboUntil = -1, leanAt = -9, squashAt = -9;
        int slashLane = 1, leanLane = 1, lastFootfall = -1;
        double leadBeats = 4, lastGlowBeat = -1;
        float glowPulseAt = -9;
        WeaponVisual weaponVisual = WeaponVisual.For(null);
        float feverBlend;
        bool fever, ready;
        int perfectStreak;
        double bestBeat = -1;

        public EffectLevel Effects = EffectLevel.Strong;
        public RectTransform Root { get { return root; } }
        public BossView Boss { get { return boss; } }
        /// <summary>The lane being held (-1 = none).</summary>
        public int HoldingLane { get { return holdingLane; } }
        /// <summary>Canvas-local position of the last sliced target (coin flights start there).</summary>
        public Vector2 LastHitPosition { get; private set; }

        public void Build(RectTransform parent, double spawnLeadBeats)
        {
            leadBeats = spawnLeadBeats;
            root = UiKit.Rect("Playfield", parent, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<RectMask2D>();
            root.gameObject.AddComponent<Canvas>(); // sub-canvas: the per-frame road/targets rebuild stays isolated (release P1)

            backdrop = UiKit.Image("Backdrop", root, ArtLibrary.Get("kitchen_backdrop"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            if (backdrop.sprite == null) { backdrop.color = new Color(.83f, .55f, .27f); UiKit.Stretch(backdrop.rectTransform); }
            backdropLoop = UiKit.Image("BackdropLoop", root, backdrop.sprite, new Color(1, 1, 1, 0), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            backdropNext = UiKit.Image("BackdropNext", root, null, new Color(1, 1, 1, 0), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            backdropNext.enabled = false;
            feverBackdrop = UiKit.Image("FeverBackdrop", root, ArtLibrary.Get("kitchen_backdrop_fever"), new Color(1, 1, 1, 0), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            feverBackdrop.enabled = false;
            // other stages have no painted FEVER version: a warm sunset wash over the scenery instead
            feverTint = UiKit.Image("FeverTint", root, ArtLibrary.EdgeGlow, new Color(1f, .55f, .25f, 0), Vector2.zero, new Vector2(1, 1 - Perspective.HorizonY + .25f));
            feverTint.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            feverTint.enabled = false;
            Image haze = UiKit.Image("Haze", root, ArtLibrary.Glow, new Color(1f, .9f, .7f, .55f), Vector2.zero, Vector2.zero);
            UiKit.At(haze.rectTransform, new Vector2(.5f, 1 - Perspective.HorizonY), new Vector2(700, 260));

            wallL = UiKit.Rect("WallL", root, Vector2.zero, Vector2.one).gameObject.AddComponent<WallGraphic>();
            wallR = UiKit.Rect("WallR", root, Vector2.zero, Vector2.one).gameObject.AddComponent<WallGraphic>();
            wallL.Side = -1; wallR.Side = 1; wallL.raycastTarget = wallR.raycastTarget = false;
            wallL.Height = wallR.Height = .85f; // tall: only the truly far view (sky, window) stays still
            wallL.LeadBeats = wallR.LeadBeats = leadBeats;
            propLayer = UiKit.Rect("Props", root, Vector2.zero, Vector2.one);
            // the boss stands in front of the side walls and posts (owner round 6), still behind the road and the targets
            RectTransform bossHolder = UiKit.Rect("BossHolder", root, Vector2.zero, Vector2.one);
            RectTransform roadRect = UiKit.Rect("Road", root, Vector2.zero, Vector2.one);
            road = roadRect.gameObject.AddComponent<RoadGraphic>();
            road.Wood = ArtLibrary.Texture("wood_road_tile");
            road.color = road.Wood != null ? Color.white : new Color(.78f, .55f, .32f);
            road.LeadBeats = leadBeats;
            road.raycastTarget = false;
            propLayer.SetAsLastSibling();
            laneLines = UiKit.Rect("LaneLines", root, Vector2.zero, Vector2.one).gameObject.AddComponent<LaneLinesGraphic>();
            laneLines.raycastTarget = false;
            beatLines = UiKit.Rect("BeatLines", root, Vector2.zero, Vector2.one).gameObject.AddComponent<BeatLinesGraphic>();
            beatLines.raycastTarget = false;
            beatLines.LeadBeats = leadBeats;
            streakLayer = UiKit.Rect("Streaks", root, Vector2.zero, Vector2.one);

            for (int lane = 0; lane < 3; lane++)
            {
                laneFlash[lane] = UiKit.Image("LaneFlash" + lane, root, ArtLibrary.Glow, new Color(.4f, .95f, 1f, 0), new Vector2(lane / 3f, 0), new Vector2((lane + 1) / 3f, 1 - Perspective.SlashY + .06f));
                laneFlash[lane].enabled = false;
            }
            slashGlow = UiKit.Image("SlashGlow", root, ArtLibrary.Glow, new Color(.13f, .85f, .95f, .5f), new Vector2(-.2f, 1 - Perspective.SlashY - .025f), new Vector2(1.2f, 1 - Perspective.SlashY + .025f));
            slashLine = UiKit.Image("SlashLine", root, null, UiKit.Cyan, new Vector2(0, 1 - Perspective.SlashY - .0025f), new Vector2(1, 1 - Perspective.SlashY + .0025f));
            for (int lane = 0; lane < 3; lane++)
            {
                lineFlash[lane] = UiKit.Image("LineFlash" + lane, root, null, Color.white, new Vector2(lane / 3f, 1 - Perspective.SlashY - .004f), new Vector2((lane + 1) / 3f, 1 - Perspective.SlashY + .004f));
                lineFlash[lane].enabled = false;
            }

            targetLayer = UiKit.Rect("Targets", root, Vector2.zero, Vector2.one);
            heroLayer = UiKit.Rect("Hero", root, Vector2.zero, Vector2.one);
            fxLayer = UiKit.Rect("Effects", root, Vector2.zero, Vector2.one);
            cometLayer = UiKit.Rect("Comets", root, Vector2.zero, Vector2.one);
            popupLayer = UiKit.Rect("Popups", root, Vector2.zero, Vector2.one);
            boss.Build(bossHolder, cometLayer);
            boss.Exploded = OnBossExploded;
            gate = UiKit.Image("Gate", propLayer, ArtLibrary.Get("gate_arch"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
            gate.rectTransform.pivot = new Vector2(.5f, 0);
            gate.enabled = false;
            mashRing = UiKit.Image("MashRing", popupLayer, ArtLibrary.Ring, UiKit.Gold, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            mashRing.type = Image.Type.Filled; mashRing.fillMethod = Image.FillMethod.Radial360; mashRing.fillOrigin = 2;
            mashRing.enabled = false;
            rushCounter = UiKit.Label("", popupLayer, 110, UiKit.Gold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false);
            rushCounter.rectTransform.sizeDelta = new Vector2(900, 160);
            rushCounter.horizontalOverflow = HorizontalWrapMode.Overflow; rushCounter.resizeTextForBestFit = false;
            UiKit.AddOutline(rushCounter.gameObject, new Color(.4f, .1f, .02f, .95f), 5);
            tapHand = UiKit.Image("TapHand", popupLayer, ArtLibrary.Get("ui_tap_hand"), Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
            tapHand.rectTransform.pivot = new Vector2(.5f, 0); // the fingertip
            tapHand.enabled = false;
            boss.HitAt = OnBossHit;
            mashLabel = UiKit.Label("", popupLayer, 80, UiKit.Gold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false);
            mashLabel.rectTransform.sizeDelta = new Vector2(900, 120);
            mashLabel.horizontalOverflow = HorizontalWrapMode.Overflow; mashLabel.resizeTextForBestFit = false;
            UiKit.AddOutline(mashLabel.gameObject, new Color(.35f, .1f, .02f, .95f), 4);

            feverAura = UiKit.Image("FeverAura", heroLayer, ArtLibrary.Get("vfx_fever_aura") ?? ArtLibrary.Glow, new Color(1f, .85f, .4f, 0), Vector2.zero, Vector2.zero, true);
            feverAura.enabled = false;
            heroShadow = UiKit.Image("HeroShadow", heroLayer, ArtLibrary.SoftShadow, new Color(.2f, .1f, .03f, .45f), Vector2.zero, Vector2.zero);
            hero = UiKit.Rect("Hamster", heroLayer, Vector2.zero, Vector2.zero);
            heroImage = hero.gameObject.AddComponent<Image>();
            heroImage.raycastTarget = false; heroImage.preserveAspect = true;
            for (int i = 0; i < ghosts.Length; i++)
            {
                ghosts[i] = UiKit.Image("WeaponGhost" + i, hero, null, Color.white, Vector2.zero, Vector2.zero, true);
                ghosts[i].enabled = false;
            }
            weapon = UiKit.Image("Weapon", hero, null, Color.white, Vector2.zero, Vector2.zero, true);
            // power-up visuals (generic: they fit any character)
            rocket = UiKit.Image("Rocket", hero, ArtLibrary.Get("rocket_pack"), Color.white, new Vector2(.28f, .38f), new Vector2(.72f, .82f), true);
            rocketFlame = UiKit.Image("Flame", rocket.transform, ArtLibrary.Glow, new Color(1f, .6f, .2f, .9f), new Vector2(.15f, -.9f), new Vector2(.85f, .15f));
            rocket.transform.SetAsFirstSibling();
            rocket.enabled = false; rocketFlame.enabled = false;
            barrierBubble = UiKit.Image("Barrier", heroLayer, ArtLibrary.Get("vfx_barrier") ?? ArtLibrary.Ring, new Color(.6f, .95f, 1f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
            barrierBubble.enabled = false;
            guardDome = UiKit.Image("GuardDome", heroLayer, ArtLibrary.Get("vfx_barrier") ?? ArtLibrary.Ring, new Color(.7f, .95f, 1f, .85f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
            guardDome.enabled = false;
            // round 27: the giant's last three beats are counted over its head
            giantCount = UiKit.Label("", heroLayer, 120, UiKit.Gold, new Vector2(.5f, .5f), new Vector2(.5f, .5f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false);
            giantCount.rectTransform.sizeDelta = new Vector2(300, 160);
            giantCount.horizontalOverflow = HorizontalWrapMode.Overflow; giantCount.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.AddOutline(giantCount.gameObject, new Color(.35f, .1f, .02f, .95f), 5);
            giantCount.enabled = false;

            targetPool = new ViewPool<RectTransform>(CreateTarget);
            bestFlagL = CreateFlag(); bestFlagR = CreateFlag();
            Layout();
            // the environment lives in a real 3D scene behind this UI layer (owner round 7)
            var worldRoot = new GameObject("World3DRoot");
            DontDestroyOnLoad(worldRoot);
            world = worldRoot.AddComponent<World3D>();
            world.Build();
            world.PostPassed = () => { if (PostPassed != null) PostPassed(); };
            boss.World = world;
            road.enabled = false; wallL.enabled = wallR.enabled = false;
            gate.enabled = false;
        }

        void OnEnable() { if (world != null) world.SetActive(true); }
        void OnDisable() { if (world != null) world.SetActive(false); }

        readonly Vector3[] frameCorners = new Vector3[4];

        Rect FramePixels()
        {
            root.GetWorldCorners(frameCorners); // overlay canvas: world space = screen pixels
            return Rect.MinMaxRect(frameCorners[0].x, frameCorners[0].y, frameCorners[2].x, frameCorners[2].y);
        }

        /// <summary>Hands the backdrop, fog and FEVER mood to the 3D scene; the UI copies stay hidden.</summary>
        void PushScenery()
        {
            string id = stages != null && theme >= 0 && theme < stages.Length ? stages[theme].id : "stage_kitchen";
            Color fog; if (!FogColors.TryGetValue(id, out fog)) fog = FogColors["stage_kitchen"];
            bool kitchen = theme == 0 && fadingTheme < 0;
            Color mood = !kitchen && Effects != EffectLevel.Weak ? Color.Lerp(Color.white, new Color(1f, .78f, .62f), feverBlend) : Color.white;
            world.SetFog(Color.Lerp(fog, new Color(1f, .7f, .45f), feverBlend * .4f));
            Color light; if (!LightColors.TryGetValue(id, out light)) light = Color.white;
            stageLight = Color.Lerp(light, new Color(1f, .9f, .78f), feverBlend * .35f);
            stageFog = Color.Lerp(fog, new Color(1f, .7f, .45f), feverBlend * .4f);
            world.SetLightTint(stageLight);
            world.SetBackdrop(0, backdrop.sprite != null ? backdrop.sprite.texture : null, 1f, zoom1, mood);
            world.SetBackdrop(1, backdropLoop.sprite != null && fadingTheme < 0 && !CalmMotion ? backdropLoop.sprite.texture : null, loopAlpha, zoom2, mood);
            world.SetBackdrop(2, fadingTheme >= 0 && backdropNext.sprite != null ? backdropNext.sprite.texture : null, backdropNext.color.a, zoom1, mood);
            world.SetBackdrop(3, feverBackdrop.enabled && feverBackdrop.sprite != null ? feverBackdrop.sprite.texture : null, feverBackdrop.color.a, zoom1, Color.white);
            backdrop.enabled = backdropNext.enabled = backdropLoop.enabled = feverBackdrop.enabled = feverTint.enabled = false;
            wallMood = mood;
        }

        Color wallMood = Color.white;

        public void SetWeapon(string id)
        {
            weaponVisual = WeaponVisual.For(id);
            weapon.sprite = ArtLibrary.Get(id);
            weapon.enabled = weapon.sprite != null;
            foreach (Image g in ghosts) g.sprite = weapon.sprite;
        }

        public void SetFever(bool on)
        {
            if (on && !fever) { feverKickAt = Time.unscaledTime; streakCarry += Effects == EffectLevel.Strong ? 24 : 8; } // FEVER: a burst of speed
            fever = on;
        }

        /// <summary>Active power-ups from the engine snapshot.</summary>
        public void SetPowerups(bool boost, bool giant, int shieldCount, double giantBeatsLeft = 0)
        {
            if (boost && !boostOn) streakCarry += Effects == EffectLevel.Weak ? 0 : 20;
            if (giant != giantOn)
            {
                giantChangedAt = Time.unscaledTime;
                // shrinking back: a puff of smoke where the giant stood
                if (!giant && hero != null)
                {
                    Sprite puff = ArtLibrary.Get("vfx_dust_puff") ?? ArtLibrary.Glow;
                    Vector2 c = hero.anchoredPosition + new Vector2(0, hero.sizeDelta.y * .5f);
                    for (int i = 0; i < (Effects == EffectLevel.Weak ? 3 : 8); i++)
                    {
                        float a = i / 8f * Mathf.PI * 2;
                        Spawn(puff, c, Vector2.one * Unit * .16f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Unit * .35f, 0, .45f, .6f, 1.5f, new Color(1, 1, 1, .85f), 0);
                    }
                }
            }
            boostOn = boost; giantOn = giant; shields = shieldCount; giantLeft = giantBeatsLeft;
        }

        /// <summary>Round 27: a giant's tap that hit nothing still swings across the whole road.</summary>
        public void GiantSwing(int lane)
        {
            float now = Time.unscaledTime;
            leanLane = lane; leanAt = now; slashLane = lane; slashUntil = now + SlashPose;
            GiantSlash(lane, .6f);
        }

        /// <summary>One huge slash across all three lanes on the slash line (the giant's reach).</summary>
        void GiantSlash(int lane, float alpha)
        {
            Sprite slash = ArtLibrary.Get(weaponVisual.TrailSprite) ?? ArtLibrary.Get("vfx_slash");
            Vector2 mid = Local(.5f, Perspective.SlashY - .02f);
            Spawn(slash ?? ArtLibrary.Circle, mid, new Vector2(Unit * 1.1f, Unit * .34f), Vector2.zero, 0, .2f, .85f, 1.1f, new Color(1f, .92f, .55f, alpha), 0, (lane - 1) * -8f);
            if (Effects == EffectLevel.Weak) return;
            Sprite dust = ArtLibrary.Get("vfx_dust_puff");
            if (dust != null) Spawn(dust, Local(.5f, Perspective.CharacterY + .1f), new Vector2(Unit * .5f, Unit * .18f), Vector2.zero, 0, .3f, .7f, 1.3f, new Color(1, 1, 1, .6f), 0);
        }

        /// <summary>RUSH begins: a freeze frame, then the world blasts past while everything ahead is cut.</summary>
        public void RushStart()
        {
            float now = Time.unscaledTime;
            rushHits = 0;
            hitStopUntil = now + .12f; slashUntil = now + .12f + SlashPose;
            streakCarry += Effects == EffectLevel.Weak ? 0 : 30;
            feverKickAt = now;
        }

        /// <summary>The rush is over: one huge slash across the whole screen.</summary>
        public void RushFinish()
        {
            if (rushHits <= 0) return;
            Vector2 mid = Local(.5f, Perspective.SlashY - .12f);
            Sprite arc = ArtLibrary.Get("vfx_slash_rainbow") ?? ArtLibrary.Get("vfx_slash");
            Spawn(arc ?? ArtLibrary.Circle, mid, new Vector2(Unit * 1.5f, Unit * .55f), Vector2.zero, 0, .35f, .7f, 1.3f, Color.white, 0, -12f);
            KoPopup(mid + new Vector2(0, Unit * .12f), "一閃!!");
            rushCounter.text = "";
            rushHits = 0;
        }

        /// <summary>The barrier took a MISS.</summary>
        public void ShieldPop()
        {
            shieldPopAt = Time.unscaledTime;
            Vector2 at = hero.anchoredPosition + new Vector2(0, hero.sizeDelta.y * .5f);
            for (int i = 0; i < (Effects == EffectLevel.Weak ? 3 : 10); i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Spawn(ArtLibrary.Diamond, at, new Vector2(.04f, .04f) * Unit, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Unit * Random.Range(.3f, .6f), 300f, .45f, 1f, .5f, new Color(.6f, .95f, 1f, .9f), Unit * .8f);
            }
            MashPopup("GUARD!", UiKit.Cyan);
        }

        /// <summary>Stage list from the catalog (scenery, object set and boss per theme).</summary>
        public void SetStages(CatalogItem[] list) { stages = list; }

        CatalogItem StageItem(RunEngine engine, int stage)
        {
            if (stages == null || stages.Length == 0 || engine == null) return null;
            int t = engine.ThemeForStage(stage);
            return stages[Mathf.Clamp(t, 0, stages.Length - 1)];
        }

        /// <summary>Sprite prefix of what is drawn for an event ("crate", "baguette", "shot_onion"…). Cosmetic only.</summary>
        public string KindOf(RunEngine engine, ScheduledEvent e)
        {
            if (e == null) return "strawberry";
            if (e.Soldier != null) return "soldier_" + e.Soldier;
            CatalogItem stage = StageItem(engine, e.Stage);
            if (e.ObjectId == "object_boss_shot") return ShotKind(stage, engine, e.Stage);
            if (e.ObjectId == "object_boss_gold") return "shot_gold";
            if (e.ObjectId == "object_boss_charge") return "shot_charge";
            if (e.Item != ItemKind.None) return "item_" + e.Item.ToString().ToLowerInvariant();
            return CatalogQueries.Kind(CatalogQueries.DisplayObject(stage, e.ObjectId));
        }

        /// <summary>Boss of the stage that contains <paramref name="beat"/>.</summary>
        public string BossIdAt(RunEngine engine, double beat) { int s = engine.StageAt(beat); return BossSprite(StageItem(engine, s), engine, s); }

        /// <summary>
        /// The stage's boss. Round 30: a stage may list several (<see cref="CatalogItem.bossIds"/>); each run picks one per stage
        /// from its seed, among those whose art is in the build (a boss whose sprites are missing never shows up).
        /// </summary>
        static string BossSprite(CatalogItem stage, RunEngine engine, int stageIndex)
        {
            if (Automation.ForcedBoss != null) return Automation.ForcedBoss;
            if (stage == null) return "boss_onion";
            if (stage.bossIds != null && stage.bossIds.Length > 0 && engine != null)
            {
                var ready = new List<string>(stage.bossIds.Length);
                foreach (string id in stage.bossIds) if (ArtLibrary.Has(id) && ArtLibrary.Has("shot_" + id.Replace("boss_", ""))) ready.Add(id);
                if (ready.Count > 0) return ready[(int)((uint)(engine.Seed * 31 + stageIndex * 7919) % (uint)ready.Count)];
            }
            return !string.IsNullOrEmpty(stage.bossId) ? stage.bossId : "boss_onion";
        }
        static string ShotKind(CatalogItem stage, RunEngine engine, int stageIndex) { return "shot_" + BossSprite(stage, engine, stageIndex).Replace("boss_", ""); }

        /// <summary>Display name of a stage theme.</summary>
        public string ThemeName(int themeIndex) { return stages != null && themeIndex >= 0 && themeIndex < stages.Length ? stages[themeIndex].displayName : ""; }
        public void SetReady(bool on) { ready = on; }
        public void SetPerfectStreak(int streak) { perfectStreak = streak; }

        /// <summary>Places the BEST flag at the beat where the previous best distance is reached (0 = none).</summary>
        public void SetBestDistance(int meters, double metersPerBeat) { bestBeat = meters > 0 ? meters / metersPerBeat : -1; }

        /// <summary>Character reaction for combo milestones (10/25/50/100…).</summary>
        public void ComboReaction() { comboUntil = Time.unscaledTime + ComboPose; }

        /// <summary>The hamster's victory pose when a stage is cleared (held a little longer than a combo cheer).</summary>
        public void StageClearPose() { comboUntil = Time.unscaledTime + 1.1f; }

        void Layout()
        {
            Vector2 size = root.rect.size;
            if (backdrop.sprite != null) UiKit.Cover(backdrop, size);
            if (backdropNext.sprite != null) UiKit.Cover(backdropNext, size);
            if (backdropLoop != null && backdropLoop.sprite != null) UiKit.Cover(backdropLoop, size);
            if (feverBackdrop.sprite != null) UiKit.Cover(feverBackdrop, size);
        }

        void OnRectTransformDimensionsChange() { if (backdrop != null) Layout(); }

        RectTransform CreateTarget()
        {
            RectTransform rect = UiKit.Rect("Target", targetLayer, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            var view = new TargetView { Root = rect };
            view.Shadow = UiKit.Image("Shadow", rect, ArtLibrary.SoftShadow, new Color(.2f, .08f, .02f, .4f), new Vector2(.12f, -.12f), new Vector2(.88f, .12f));
            // round 12: a tight dark contact patch right under the object, so it stands on the road instead of floating
            view.Contact = UiKit.Image("Contact", rect, ArtLibrary.SoftShadow, new Color(0, 0, 0, .5f), new Vector2(.24f, -.05f), new Vector2(.76f, .07f));
            view.Glow = UiKit.Image("Glow", rect, ArtLibrary.Glow, new Color(.4f, .95f, 1f, 0), new Vector2(-.35f, -.35f), new Vector2(1.35f, 1.35f));
            view.Body = NewSolid("Body", rect, Vector2.zero, Vector2.one);
            view.OffRing = UiKit.Image("OffbeatRing", rect, ArtLibrary.Ring, UiKit.Offbeat, new Vector2(-.18f, -.18f), new Vector2(1.18f, 1.18f));
            view.Diamond = UiKit.Image("OffbeatDiamond", rect, ArtLibrary.Diamond, UiKit.Offbeat, new Vector2(-.32f, -.32f), new Vector2(1.32f, 1.32f));
            view.Ring = UiKit.Image("ApproachRing", rect, ArtLibrary.Ring, Color.white, Vector2.zero, Vector2.one);
            targetViews[rect] = view;
            return rect;
        }

        static SolidImage NewSolid(string name, Transform parent, Vector2 min, Vector2 max)
        {
            RectTransform rect = UiKit.Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<SolidImage>();
            image.raycastTarget = false; image.preserveAspect = true;
            return image;
        }

        /// <summary>How deep an object is compared with its size: boxes and blocks are deep, round fruit shows little side.</summary>
        static float ThicknessOf(string kind)
        {
            switch (kind)
            {
                case "crate": case "barrel": case "safe": case "dice": case "clock": case "gem": return .11f;
                case "duck": case "pumpkin": return .08f;
                default: return .06f;
            }
        }

        Prop CreateFlag()
        {
            Sprite flag = ArtLibrary.Get("icon_flag");
            Image image = UiKit.Image("BestFlag", propLayer, flag ?? ArtLibrary.Star, flag != null ? Color.white : UiKit.Gold, Vector2.zero, Vector2.zero, true);
            Text label = UiKit.Label("BEST", image.transform, 64, UiKit.Gold, new Vector2(-.6f, 1f), new Vector2(1.6f, 1.45f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            image.gameObject.SetActive(false);
            return new Prop { Rect = image.rectTransform, Image = image, Label = label };
        }


        Vector2 Local(float x, float y) { return RoadGraphic.ToLocal(root.rect, x, y); }
        float Unit { get { return root.rect.width; } }
        int Particles { get { return Effects == EffectLevel.Strong ? 8 : Effects == EffectLevel.Standard ? 4 : 0; } }

        public void ResetRun()
        {
            soldierParryAt.Clear(); soldierLaneBefore.Clear();
            foreach (TargetView v in live.Values) targetPool.Release(v.Root);
            live.Clear();
            foreach (Effect e in effects) Recycle(e);
            effects.Clear();
            foreach (Prop p in props) { p.Rect.gameObject.SetActive(false); freeProps.Push(p); }
            props.Clear();
            foreach (Streak s in streaks) { s.Rect.gameObject.SetActive(false); freeStreaks.Push(s); }
            streaks.Clear();
            foreach (HoldBodyGraphic h in holdBodies.Values) { h.gameObject.SetActive(false); freeHoldBodies.Push(h); }
            holdBodies.Clear();
            boss.Reset();
            if (world != null) world.ResetPosts();
            wallScroll = 0;
            foreach (Post p in posts) { p.Rect.gameObject.SetActive(false); freePosts.Push(p); }
            posts.Clear();
            worldSpeed = 1f; worldScroll = 0; roadScroll = 0; lastRenderBeat = double.NaN; nextPostZ = 1; postCount = 0; wallTheme = -1; rushDash = 0;
            boostOn = giantOn = chargeGuard = false; shields = 0; giantScale = 1f; giantLeft = 0;
            holdingLane = -1; fadingTheme = -1; backdropNext.enabled = false; gate.enabled = false;
            mashRing.enabled = false; mashLabel.text = "";
            SetTheme(0);
            slashUntil = missUntil = hitUntil = comboUntil = -1;
            feverBlend = 0; fever = false; perfectStreak = 0; lastFootfall = -1;
            gatePassedStage = 0; gatePassAt = -9;
        }

        public void Render(RunEngine engine, double beat, bool offbeatPreview)
        {
            if (root.rect.width <= 0) return;
            current = engine;
            bpm = engine.Tempo.BpmAt(beat);
            boss.Effects = Effects;
            UpdateWorld(engine, beat);
            UpdateTheme(engine, beat);
            UpdateStreaks(engine, beat);
            UpdateGate(engine, beat);
            UpdateBeatLines(engine, beat, offbeatPreview);
            int stageNow = engine.StageAt(beat);
            boss.FinisherProgress = FinisherProgress(engine, beat);
            boss.Hp01 = engine.BossHp01;
            boss.Tick(engine, beat, BossSprite(StageItem(engine, stageNow), engine, stageNow), root.rect, Unit, FinisherOpen(engine, beat));
            // the road's painted lines stop at the boss's feet: it stands on them, they never run across its body
            float behind = boss.Visible ? Perspective.DepthFromY(boss.FootY) : 0f;
            laneLines.FromDepth = behind; beatLines.FromDepth = behind;
            UpdateBossWear();
            UpdateBestFlag(beat);
            UpdateTargets(engine, beat);
            UpdateFever();
            PushScenery(); // after the FEVER state is known, so its picture goes to the 3D backdrop (never the UI)
            UpdateHero(beat);
            UpdateEffects();
            float now = Time.unscaledTime;
            Color line = offbeatPreview ? UiKit.Offbeat : UiKit.Cyan; // judgement guide colour never changes with FEVER
            slashLine.color = line;
            if (beat >= 0 && System.Math.Floor(beat) != lastGlowBeat) { lastGlowBeat = System.Math.Floor(beat); if (Effects != EffectLevel.Weak && budget.TryFlash(FlashBudget.Region.Screen, now)) glowPulseAt = now; }
            float pulse = .45f + .25f * Mathf.Pow(Mathf.Clamp01(1f - (now - glowPulseAt) / .5f), 3f);
            slashGlow.color = new Color(line.r, line.g, line.b, pulse);
            if (holdingLane >= 0) laneFlashAt[holdingLane] = now - .04f; // a held lane keeps glowing (steady, no strobe)
            for (int lane = 0; lane < 3; lane++)
            {
                float a = Mathf.Clamp01(1f - (now - laneFlashAt[lane]) / .16f);
                laneFlash[lane].enabled = a > 0 && Effects != EffectLevel.Weak;
                if (laneFlash[lane].enabled) laneFlash[lane].color = new Color(.4f, .95f, 1f, .32f * a);
                float l = Mathf.Clamp01(1f - (now - laneFlashAt[lane]) / .1f);
                lineFlash[lane].enabled = l > 0;
                if (l > 0) lineFlash[lane].color = new Color(1, 1, 1, .3f + .5f * l);
            }
        }

        // ------------------------------------------------------------------ fever backdrop

        void UpdateFever()
        {
            float target = fever ? 1f : 0f;
            feverBlend = Mathf.MoveTowards(feverBlend, target, Time.unscaledDeltaTime / .5f);
            bool kitchen = theme == 0 && fadingTheme < 0;
            bool showBackdrop = feverBackdrop.sprite != null && Effects == EffectLevel.Strong && kitchen;
            feverBackdrop.enabled = showBackdrop && feverBlend > 0;
            if (feverBackdrop.enabled) feverBackdrop.color = new Color(1, 1, 1, feverBlend);
            feverTint.enabled = !kitchen && Effects != EffectLevel.Weak && feverBlend > 0;
            if (feverTint.enabled) feverTint.color = new Color(1f, .55f, .3f, .38f * feverBlend);
            // rainbow lane strips during FEVER: hue drifts at 0.25 rev/s, constant value (no strobing)
            if (fever && Effects != EffectLevel.Weak)
            {
                Color c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * .25f, 1f), .6f, 1f);
                laneLines.color = new Color(c.r, c.g, c.b, 1);
                laneLines.Boost = 1f;
            }
            else { laneLines.color = Color.white; laneLines.Boost = 0f; }
            feverAura.enabled = fever && Effects != EffectLevel.Weak;
        }

        // ------------------------------------------------------------------ props

        // ------------------------------------------------------------------ scenery: theme, speed streaks, gate, beat lines

        Sprite ThemeSprite(int t)
        {
            string id = stages != null && t >= 0 && t < stages.Length ? stages[t].backdropId : null;
            return ArtLibrary.Get(string.IsNullOrEmpty(id) ? "kitchen_backdrop" : id) ?? ArtLibrary.Get("kitchen_backdrop");
        }

        void SetTheme(int t)
        {
            theme = t;
            Sprite s = ThemeSprite(t);
            if (s != null) { backdrop.sprite = s; backdrop.color = Color.white; backdropLoop.sprite = s; Layout(); }
        }

        /// <summary>The scenery changes the moment the hamster passes the gate (crossfade, 0.45 s).</summary>
        void UpdateTheme(RunEngine engine, double beat)
        {
            float now = Time.unscaledTime;
            int target = engine.ThemeForStage(engine.StageAt(beat));
            if (target != theme && fadingTheme != target)
            {
                fadingTheme = target; themeFadeAt = now;
                backdropNext.sprite = ThemeSprite(target);
                backdropNext.enabled = backdropNext.sprite != null;
                Layout();
            }
            if (fadingTheme < 0) return;
            float k = Mathf.Clamp01((now - themeFadeAt) / .45f);
            backdropNext.color = new Color(1, 1, 1, k);
            if (k >= 1f) { SetTheme(fadingTheme); fadingTheme = -1; backdropNext.enabled = false; }
        }

        /// <summary>
        /// Speed lines shooting from the vanishing point toward the sides (never over the road): more and faster every
        /// stage and during FEVER, so the speed-up is felt without touching the judgement guides.
        /// </summary>
        void UpdateStreaks(RunEngine engine, double beat)
        {
            float now = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            float tempo = (float)(bpm / 120.0);
            int stage = engine.StageAt(beat);
            float rate = Effects == EffectLevel.Weak || CalmMotion ? 0f : (6f + 5f * stage + (fever ? 8f : 0f) + (boostOn ? 18f : 0f)) * tempo * (worldSpeed / 2.2f) * (Effects == EffectLevel.Standard ? .5f : 1f);
            if (beat < 0 || ready) rate = 0;
            streakCarry += rate * dt;
            Vector2 vp = Local(.5f, Perspective.HorizonY);
            while (streakCarry >= 1f && streaks.Count < 40)
            {
                streakCarry -= 1f;
                Streak s = freeStreaks.Count > 0 ? freeStreaks.Pop() : NewStreak();
                s.Rect.gameObject.SetActive(true);
                float side = Random.value < .5f ? -1f : 1f;
                float angle = Random.Range(-35f, 50f) * Mathf.Deg2Rad; // above/along the horizon only; the road is steeper than -43°
                s.Dir = new Vector2(Mathf.Cos(angle) * side, Mathf.Sin(angle));
                s.Born = now; s.Life = Random.Range(.35f, .55f);
                s.Speed = Unit * Random.Range(1.3f, 2.1f) * tempo * (worldSpeed / 2.2f) * (1f + .15f * stage);
                s.Start = Unit * Random.Range(.12f, .25f);
                s.Image.color = fever ? Color.HSVToRGB(Random.value, .35f, 1f) : new Color(1f, .97f, .9f);
                streaks.Add(s);
            }
            if (streakCarry > 1f) streakCarry = 0;
            for (int i = streaks.Count - 1; i >= 0; i--)
            {
                Streak s = streaks[i];
                float t = (now - s.Born) / s.Life;
                if (t >= 1f) { s.Rect.gameObject.SetActive(false); freeStreaks.Push(s); streaks.RemoveAt(i); continue; }
                float dist = s.Start + s.Speed * (now - s.Born) * (.5f + t);
                float length = Unit * Mathf.Lerp(.03f, .16f, t) * (1f + .2f * stage);
                s.Rect.anchoredPosition = vp + s.Dir * dist;
                s.Rect.sizeDelta = new Vector2(length, Mathf.Lerp(3f, 9f, t) * (1f + .1f * stage));
                s.Rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(s.Dir.y, s.Dir.x) * Mathf.Rad2Deg);
                s.Image.canvasRenderer.SetAlpha(.45f * Mathf.Sin(t * Mathf.PI));
            }
        }

        /// <summary>
        /// Speed of the world (owner round 3: "風を切るように"): the road and the roadside posts run 2-4× faster than the
        /// notes and keep accelerating through a stage; boosts and FEVER push it further. Notes never change speed here.
        /// </summary>
        void UpdateWorld(RunEngine engine, double beat)
        {
            float now = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            double d = double.IsNaN(lastRenderBeat) ? 0 : beat - lastRenderBeat;
            lastRenderBeat = beat;
            if (d < 0 || d > 1) d = 0;
            int stage = engine.StageAt(beat);
            float progress = Mathf.Clamp01((float)((beat - engine.StageStartBeat(stage)) / engine.StageLengthBeats(stage)));
            float target = beat < 0 || ready ? 1f : 2.2f + .45f * stage + .6f * progress;
            if (boostOn) target *= 3f; // rush: the world blasts past
            if (fever) target *= 1.15f;
            float kick = 2.5f * Mathf.Clamp01(1f - (now - feverKickAt) / .6f);
            worldSpeed = Mathf.MoveTowards(worldSpeed, target + kick, dt * (kick > 0 ? 12f : 3f));
            if (gateFlash != null && gateFlash.color.a > 0) gateFlash.color = new Color(1f, .98f, .9f, Mathf.MoveTowards(gateFlash.color.a, 0f, dt * 2.2f));
            d *= GatePace();
            worldScroll += d * worldSpeed;
            // the road itself stays close to the notes' speed: scrolling it fast read as a waterfall (owner round 4)
            roadScroll += d * Mathf.Min(worldSpeed, 1.2f);
            // the walls move calmer than the posts: large, detailed surfaces rushing by cause motion sickness (owner round 7)
            wallScroll += d * Mathf.Min(worldSpeed, 2.4f) * (CalmMotion ? .35f : .8f);
            world.Frame(FramePixels());
            world.SetRoad(road.Wood, roadScroll);
            UpdateFarZoom(dt);
            UpdateWalls(engine, beat);
            world.UpdatePosts(worldScroll, CalmMotion);
            rushDash = Mathf.MoveTowards(rushDash, boostOn ? 1f : 0f, dt / (boostOn ? .12f : .3f));
        }

        /// <summary>
        /// The far background slowly comes closer (owner round 5: "背景も動くように"): two copies zoom toward the vanishing
        /// point half a cycle apart; the top copy fades in and out so neither restart is ever visible.
        /// </summary>
        void UpdateFarZoom(float dt)
        {
            const float zoom = .14f;
            if (!CalmMotion) zoomPhase += dt * .07f * Mathf.Clamp(worldSpeed / 2.2f, .4f, 2.5f);
            float p1 = Mathf.Repeat(zoomPhase, 1f), p2 = Mathf.Repeat(zoomPhase + .5f, 1f);
            zoom1 = 1f + zoom * p1; zoom2 = 1f + zoom * p2; loopAlpha = 1f - Mathf.Abs(2f * p2 - 1f);
            Vector2 vp = Local(.5f, Perspective.HorizonY);
            ScaleAbout(backdrop.rectTransform, 1f + zoom * p1, vp);
            ScaleAbout(backdropNext.rectTransform, 1f + zoom * p1, vp);
            ScaleAbout(feverBackdrop.rectTransform, 1f + zoom * p1, vp);
            ScaleAbout(backdropLoop.rectTransform, 1f + zoom * p2, vp);
            backdropLoop.enabled = backdropLoop.sprite != null && fadingTheme < 0;
            if (backdropLoop.enabled) backdropLoop.color = new Color(1, 1, 1, 1f - Mathf.Abs(2f * p2 - 1f));
        }

        static void ScaleAbout(RectTransform rect, float scale, Vector2 point)
        {
            rect.localScale = new Vector3(scale, scale, 1);
            rect.anchoredPosition = (1f - scale) * point;
        }

        void UpdateWalls(RunEngine engine, double beat)
        {
            int t = engine.ThemeForStage(engine.StageAt(beat));
            if (t != wallTheme)
            {
                wallTheme = t;
                string id = stages != null && t >= 0 && t < stages.Length ? stages[t].id : "stage_kitchen";
                wallTexture = ArtLibrary.Texture("wall_" + id.Replace("stage_", ""));
            }
            world.SetWalls(wallTexture, wallScroll, wallMood);
        }

        Texture2D wallTexture;

        void UpdatePosts()
        {
            while (nextPostZ < worldScroll + leadBeats)
            {
                Post p = freePosts.Count > 0 ? freePosts.Pop() : NewPost();
                p.Rect.gameObject.SetActive(true);
                bool left = (postCount & 1) == 0;
                p.Image.sprite = ArtLibrary.Get(((postCount >> 1) % 3 == 2) ? "side_banner" : "side_lamp");
                p.Image.enabled = p.Image.sprite != null;
                p.Z = nextPostZ; p.Lateral = left ? -1.12f : 1.12f; p.Passed = false; // between the road edge and the wall
                p.Rect.SetAsFirstSibling();
                posts.Add(p);
                postCount++;
                nextPostZ += .55; // alternating sides: one post per side every 1.1 world beats
            }
            for (int i = posts.Count - 1; i >= 0; i--)
            {
                Post p = posts[i];
                double ahead = p.Z - worldScroll;
                float depth = Perspective.DepthFromBeats(ahead, leadBeats);
                float y = Perspective.Y(depth);
                float x = Perspective.X(p.Lateral, depth);
                if (y > 1.5f || x < -.6f || x > 1.6f) { p.Rect.gameObject.SetActive(false); freePosts.Push(p); posts.RemoveAt(i); continue; }
                if (!p.Passed && x > 1.02f || !p.Passed && x < -.02f) { p.Passed = true; if (PostPassed != null) PostPassed(); }
                float height = .7f * Perspective.Scale(depth) * Unit;
                float aspect = p.Image.sprite != null ? p.Image.sprite.rect.width / p.Image.sprite.rect.height : .3f;
                p.Rect.sizeDelta = new Vector2(height * aspect, height);
                p.Rect.anchoredPosition = Local(x, y);
                p.Image.canvasRenderer.SetAlpha(Mathf.Clamp01(depth * 5f));
            }
        }

        Post NewPost()
        {
            Image image = UiKit.Image("Post", propLayer, null, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
            image.rectTransform.pivot = new Vector2(.5f, 0f);
            return new Post { Rect = image.rectTransform, Image = image };
        }

        Streak NewStreak()
        {
            Image image = UiKit.Image("Streak", streakLayer, ArtLibrary.Pill, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            image.type = Image.Type.Simple;
            image.rectTransform.pivot = new Vector2(0, .5f);
            return new Streak { Rect = image.rectTransform, Image = image };
        }

        /// <summary>The stage gate stands on the road at the next stage boundary and swallows the camera when passed.</summary>
        void UpdateGate(RunEngine engine, double beat)
        {
            int stage = engine.StageAt(beat);
            double next = engine.StageStartBeat(stage + 1), passed = engine.StageStartBeat(stage);
            double ahead = next - beat;
            bool approaching = ahead > 0 && ahead <= leadBeats + 2 && !boss.Visible; // appears once the boss is gone
            bool passing = stage > 0 && beat - passed < 1.0;
            if (passing && gatePassedStage != stage && beat >= passed) { gatePassedStage = stage; GatePassBurst(); }
            if (!approaching && !passing) { world.SetGate(false, 0, 0, 0); return; }
            if (approaching)
            {
                float d3 = Perspective.DepthFromBeats(Mathf.Min((float)ahead, (float)leadBeats), leadBeats);
                // brighter as it nears, with a beat pulse in the last two beats ("here comes the gate")
                float near = Mathf.Clamp01(1f - (float)ahead / ((float)leadBeats + 2f));
                if (ahead < 2) near = Mathf.Min(1f, near + .3f * Mathf.Pow(1f - (float)(beat - System.Math.Floor(beat)), 3f));
                world.SetGate(true, Perspective.Y(d3), Mathf.Clamp01((float)(leadBeats + 2 - ahead) / 2f), 0, near);
            }
            else world.SetGate(true, Perspective.SlashY, 1f, Mathf.Clamp01((float)(beat - passed)), 1f);
            return;
#pragma warning disable 0162
            RectTransform r = gate.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            if (approaching)
            {
                float depth = Perspective.DepthFromBeats(Mathf.Min((float)ahead, (float)leadBeats), leadBeats);
                float width = Perspective.HalfWidth(depth) * 2f * Unit * 1.3f;
                float aspect = gate.sprite.rect.width / gate.sprite.rect.height;
                r.sizeDelta = new Vector2(width, width / aspect);
                r.anchoredPosition = Local(.5f, Perspective.Y(depth));
                r.localScale = Vector3.one;
                gate.canvasRenderer.SetAlpha(Mathf.Clamp01((float)(leadBeats + 2 - ahead) / 2f));
            }
            else
            {
                // just passed: the arch rushes past the camera
                float k = (float)((beat - passed) / .6);
                float width = Unit * 1.3f * (1f + 1.5f * k);
                float aspect = gate.sprite.rect.width / gate.sprite.rect.height;
                r.sizeDelta = new Vector2(width, width / aspect);
                r.anchoredPosition = Local(.5f, Perspective.SlashY + .25f * k);
                gate.canvasRenderer.SetAlpha(1f - k);
            }
#pragma warning restore 0162
        }

        int gatePassedStage;
        float gatePassAt = -9;
        Image gateFlash;

        /// <summary>
        /// The moment the gate is passed (owner round 11: "くぐるけど分かりづらい"): a white flash that hides the scenery
        /// swap, rainbow rings racing out from the middle, a burst of speed lines, and the world holding its breath for a
        /// moment before rushing on faster.
        /// </summary>
        void GatePassBurst()
        {
            float now = Time.unscaledTime;
            gatePassAt = now;
            if (gateFlash == null)
            {
                gateFlash = UiKit.Image("GateFlash", popupLayer, null, new Color(1, 1, 1, 0), Vector2.zero, Vector2.one);
                gateFlash.transform.SetAsFirstSibling();
            }
            if (Effects != EffectLevel.Weak && budget.TryFlash(FlashBudget.Region.Screen, now)) gateFlash.color = new Color(1f, .98f, .9f, Effects == EffectLevel.Strong ? .55f : .35f);
            Vector2 mid = Local(.5f, (Perspective.HorizonY + Perspective.SlashY) * .5f);
            Color[] hues = { new Color(1f, .45f, .5f), new Color(1f, .85f, .35f), new Color(.45f, .9f, 1f) };
            for (int i = 0; i < (Effects == EffectLevel.Weak ? 1 : 3); i++)
                Spawn(ArtLibrary.Ring, mid, Vector2.one * Unit * (.35f + .12f * i), Vector2.zero, 0, .55f + .08f * i, 1f, CalmMotion ? 3f : 7f, new Color(hues[i].r, hues[i].g, hues[i].b, .85f), 0);
            Spawn(ArtLibrary.Glow, mid, Vector2.one * Unit * 1.4f, Vector2.zero, 0, .4f, .5f, 2.2f, new Color(1f, 1f, 1f, .8f), 0);
            if (!CalmMotion && Effects != EffectLevel.Weak) streakCarry += 30;
            if (fadingTheme >= 0) themeFadeAt = now - .45f; // the new scenery is simply there when the flash clears
        }

        /// <summary>World speed around the gate: a held breath (slow) right at the pass, then a surge (the SPEED UP).</summary>
        float GatePace()
        {
            if (CalmMotion) return 1f;
            float t = Time.unscaledTime - gatePassAt;
            if (t < 0 || t > 1.1f) return 1f;
            if (t < .3f) return Mathf.Lerp(.25f, 1f, t / .3f);
            return 1f + .9f * (1f - (t - .3f) / .8f);
        }

        void UpdateBeatLines(RunEngine engine, double beat, bool preview)
        {
            beatLines.Lines.Clear();
            if (beat < -leadBeats) { beatLines.Refresh(); return; }
            int first = (int)System.Math.Floor(beat * 2) - 1;
            int last = (int)System.Math.Ceiling((beat + leadBeats) * 2);
            for (int h = first; h <= last; h++)
            {
                double b = h / 2.0;
                if (b < 0) continue;
                double ahead = b - beat;
                if (ahead < -.35 || ahead > leadBeats) continue;
                bool half = (h & 1) == 1;
                if (half)
                {
                    ChunkInfo chunk = engine.ChunkAt(b);
                    bool offbeatZone = (chunk != null && chunk.HasOffbeat) || engine.IsOffbeatPreview(b);
                    if (!offbeatZone) continue;
                }
                beatLines.Lines.Add(new BeatLinesGraphic.Line { Ahead = ahead, Half = half });
            }
            beatLines.Refresh();
        }

        float FinisherProgress(RunEngine engine, double beat)
        {
            foreach (ScheduledEvent e in engine.Events)
                if (e.Boss && e.Gesture == Gesture.Mash && !e.Resolved && beat >= e.Beat - 1) return Mathf.Clamp01((float)e.MashCount / Mathf.Max(1, e.MashRequired));
            return 0f;
        }

        bool FinisherOpen(RunEngine engine, double beat)
        {
            foreach (ScheduledEvent e in engine.Events)
                if (e.Boss && e.Gesture == Gesture.Mash && !e.Resolved && beat >= e.Beat - 1 && beat <= e.EndBeat) return true;
            return false;
        }

        /// <summary>A returned shot strikes the boss: sparks and a flash where it lands (no damage numbers, owner round 8).</summary>
        void OnBossHit(Vector2 at, int damage)
        {
            bool big = damage >= 5;
            float s = Unit * (big ? .55f : damage >= 3 ? .34f : .24f);
            Vector2 p = at + Random.insideUnitCircle * Unit * .07f;
            Sprite spark = ArtLibrary.Get("vfx_sparkle");
            if (spark != null) Spawn(spark, p, new Vector2(s, s), Vector2.zero, 180f, .28f, .5f, 1.3f, Color.white, 0);
            Spawn(ArtLibrary.Glow, p, new Vector2(s * 1.5f, s * 1.5f), Vector2.zero, 0, .2f, .6f, 1.25f, big ? new Color(1f, .85f, .35f, .9f) : new Color(1f, .7f, .45f, .75f), 0);
            int bits = Effects == EffectLevel.Weak ? 2 : big ? 9 : 5;
            for (int i = 0; i < bits; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float b = Unit * Random.Range(.018f, .032f);
                Spawn(ArtLibrary.Circle, p, new Vector2(b, b), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Unit * Random.Range(.4f, .9f), 0, .35f, 1f, .5f, UiKit.Gold, Unit * 1.2f);
            }
        }

        /// <summary>A beaten-up boss looks it: sweat flies off below half HP, smoke rises when it is nearly done.</summary>
        void UpdateBossWear()
        {
            if (!boss.Visible || boss.Hp01 > .5f || Effects == EffectLevel.Weak || boss.InFinisher) return;
            float now = Time.unscaledTime;
            if (now - lastSweatAt < (boss.Hp01 < .25f ? .25f : .45f)) return;
            lastSweatAt = now;
            float h = boss.ScreenHeight * root.rect.height;
            Vector2 head = boss.Center + new Vector2(0, h * .3f);
            float side = Random.value < .5f ? -1f : 1f;
            Spawn(ArtLibrary.Circle, head + new Vector2(side * h * .28f, 0), new Vector2(Unit * .028f, Unit * .042f), new Vector2(side * Unit * .35f, Unit * .3f), 0, .6f, 1f, .7f, new Color(.62f, .86f, 1f, .95f), Unit * 1.6f);
            if (boss.Hp01 < .25f) Spawn(ArtLibrary.Glow, head + new Vector2(Random.Range(-.25f, .25f) * h, h * .1f), Vector2.one * Unit * .18f, new Vector2(0, Unit * .16f), 0, .9f, .6f, 1.7f, new Color(.42f, .4f, .4f, .5f), 0);
        }

        void OnBossExploded(Vector2 at)
        {
            // K.O.: the boss splits in two and flies apart, then the burst
            Sprite s = boss.CurrentSprite;
            if (s != null && s.texture != null)
            {
                Rect r = s.textureRect;
                Sprite left = Sprite.Create(s.texture, new Rect(r.x, r.y, r.width / 2, r.height), new Vector2(.5f, .5f));
                Sprite right = Sprite.Create(s.texture, new Rect(r.x + r.width / 2, r.y, r.width / 2, r.height), new Vector2(.5f, .5f));
                float h = Unit * .7f, w = h * (r.width / 2) / r.height;
                Spawn(left, at + new Vector2(-w * .5f, 0), new Vector2(w, h), new Vector2(-Unit * .9f, Unit * .5f), 160f, .9f, 1f, .8f, Color.white, Unit * 1.6f);
                Spawn(right, at + new Vector2(w * .5f, 0), new Vector2(w, h), new Vector2(Unit * .9f, Unit * .55f), -160f, .9f, 1f, .8f, Color.white, Unit * 1.6f);
            }
            KoPopup(at);
            if (Effects == EffectLevel.Weak) return;
            float size = Unit * .3f;
            Sprite spark = ArtLibrary.Get("vfx_sparkle"), coin = ArtLibrary.Get("icon_coin");
            Spawn(ArtLibrary.Glow, at, new Vector2(size * 3f, size * 3f), Vector2.zero, 0, .5f, .5f, 1.6f, new Color(1f, .85f, .4f, .9f), 0);
            for (int i = 0; i < (Effects == EffectLevel.Strong ? 14 : 7); i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .6f) * Unit * Random.Range(.4f, .9f);
                Spawn(i % 2 == 0 ? (coin ?? ArtLibrary.Circle) : (spark ?? ArtLibrary.Star), at, new Vector2(size * .35f, size * .35f), v, Random.Range(-360f, 360f), .8f, 1f, .7f, Color.white, Unit * 1.2f);
            }
        }

        bool PlaceOnRoad(Prop p, double beat)
        {
            double ahead = p.Beat - beat;
            float depth = Perspective.DepthFromBeats(ahead, leadBeats);
            float y = Perspective.Y(depth);
            if (y > 1.35f) return false;
            float size = p.Size * Perspective.Scale(depth) * Unit;
            p.Rect.anchorMin = p.Rect.anchorMax = new Vector2(.5f, .5f);
            p.Rect.pivot = new Vector2(.5f, 0f);
            p.Rect.sizeDelta = new Vector2(size, size);
            p.Rect.anchoredPosition = Local(Perspective.X(p.Lateral, depth), y);
            p.Image.canvasRenderer.SetAlpha(Mathf.Clamp01(depth * 6f));
            return true;
        }

        void UpdateBestFlag(double beat)
        {
            bool visible = bestBeat > 0 && bestBeat - beat <= leadBeats && bestBeat - beat > -3;
            foreach (Prop flag in new[] { bestFlagL, bestFlagR })
            {
                flag.Rect.gameObject.SetActive(visible);
                if (!visible) continue;
                flag.Beat = bestBeat;
                flag.Lateral = flag == bestFlagL ? -1.25f : 1.25f;
                flag.Size = .28f;
                PlaceOnRoad(flag, beat);
                flag.Rect.SetAsLastSibling();
                float near = Mathf.Clamp01(1f - (float)(bestBeat - beat) / 20f);
                flag.Label.color = Color.Lerp(Color.white, UiKit.Gold, near);
            }
        }

        // ------------------------------------------------------------------ targets

        void UpdateTargets(RunEngine engine, double beat)
        {
            world.BeginLongFrame();
            UpdateTargetsInner(engine, beat);
            world.EndLongFrame();
        }

        void UpdateTargetsInner(RunEngine engine, double beat)
        {
            beatLines.Occluders.Clear();
            scratch.Clear();
            foreach (KeyValuePair<string, TargetView> pair in live) scratch.Add(pair.Key);
            holdScratch.Clear();
            foreach (KeyValuePair<string, HoldBodyGraphic> pair in holdBodies) holdScratch.Add(pair.Key);
            float goodBeats = (float)(engine.Config.GoodMs / 1000.0 * bpm / 60.0);
            float now = Time.unscaledTime;
            ScheduledEvent openMash = null;
            soldierLead.Clear();
            foreach (ScheduledEvent e in engine.Events)
            {
                if (e.SoldierGroup == null || e.Resolved) continue;
                ScheduledEvent lead;
                if (!soldierLead.TryGetValue(e.SoldierGroup, out lead) || e.Beat < lead.Beat) soldierLead[e.SoldierGroup] = e;
            }
            foreach (ScheduledEvent e in engine.Events)
            {
                double ahead = e.Beat - beat;
                if (e.Gesture == Gesture.Hold) UpdateHoldBody(engine, e, beat);
                bool lingering = e.Gesture != Gesture.Tap && beat <= e.EndBeat;
                bool visible = !e.Resolved && ahead <= leadBeats && (ahead >= -.6 || lingering);
                // round 27: a half of a split fruit only exists once its big fruit has reached the line
                if (!double.IsNaN(e.SplitFromBeat) && beat < e.SplitFromBeat - .02) visible = false;
                if (!visible) continue;
                if (e.Gesture == Gesture.Mash && beat >= e.Beat - 1) openMash = e;
                scratch.Remove(e.Id);
                TargetView view;
                bool shot = e.ObjectId == "object_boss_shot" || e.Gold || e.Charge;
                if (!live.TryGetValue(e.Id, out view))
                {
                    view = targetViews[targetPool.Get()];
                    view.Root.SetAsFirstSibling(); // new targets appear farthest away
                    string kind = KindOf(engine, e);
                    // soldiers pick their pose sprite below (no "_intact" art: skip the lookup and its warning)
                    view.Body.sprite = e.Soldier != null ? ArtLibrary.Get(kind + "_walk") : ArtLibrary.Get(kind + "_intact") ?? ArtLibrary.Get(kind);
                    view.Body.color = Color.white;
                    if (view.Body.sprite == null) { view.Body.sprite = ArtLibrary.Circle; view.Body.color = new Color(.9f, .2f, .25f); }
                    view.BaseColor = view.Body.color;
                    // a hold is drawn by its body graphic; the finisher mash has no object (the boss is the target)
                    view.Body.enabled = e.Gesture == Gesture.Tap || e.Charge || (e.Gesture == Gesture.Mash && !e.Boss);
                    view.Shadow.enabled = view.Contact.enabled = view.Body.enabled;
                    view.Body.rectTransform.localRotation = Quaternion.identity;
                    view.Thick = shot || e.Charge ? 0f : ThicknessOf(kind);
                    view.Offbeat = e.Offbeat;
                    view.OffRing.enabled = view.Diamond.enabled = e.Offbeat;
                    live[e.Id] = view;
                    if (shot) boss.Spit();
                    if (e.Boss && e.Soldier != null && soldierLead.ContainsKey(e.SoldierGroup) && soldierLead[e.SoldierGroup] == e) boss.Spit(); // the boss calls a soldier in
                }
                // a soldier (round 17) stands on the strike it is about to make: walks in, its sword flashes, it strikes;
                // a parry knocks it back to its next strike. Its later strikes show as small glints along the lane.
                bool soldierLeads = false;
                if (e.Soldier != null)
                {
                    soldierLeads = soldierLead.ContainsKey(e.SoldierGroup) && soldierLead[e.SoldierGroup] == e;
                    float parried;
                    bool knocked = soldierParryAt.TryGetValue(e.SoldierGroup, out parried) && now - parried < .18f;
                    string pose = knocked ? "_hurt" : ahead < .5 ? "_attack" : "_walk";
                    string kindS = "soldier_" + e.Soldier;
                    if (e.Charge && !knocked && ahead < 1.5 && ArtLibrary.Get(kindS + "_charge") != null) pose = "_charge"; // round 26: the crow's flame push
                    Sprite s = soldierLeads ? (ArtLibrary.Get(kindS + pose) ?? ArtLibrary.Get(kindS + "_walk")) : (ArtLibrary.Get("vfx_sparkle") ?? ArtLibrary.Star);
                    if (view.Body.sprite != s) view.Body.sprite = s ?? ArtLibrary.Circle;
                    view.BaseColor = soldierLeads ? Color.white : new Color(1f, .92f, .6f, .85f);
                    view.Body.enabled = true;
                    view.Shadow.enabled = view.Contact.enabled = soldierLeads;
                    view.Thick = 0f;
                    // a little march: the soldier rocks from foot to foot on the beat
                    view.Body.rectTransform.localRotation = Quaternion.Euler(0, 0, soldierLeads && !knocked && ahead >= .5 ? Mathf.Sin((float)beat * Mathf.PI) * 6f : soldierLeads ? 0f : (float)(beat * 90.0));
                }
                if (e.Holding || (e.Gesture == Gesture.Mash && ahead < 0)) ahead = 0; // held heads and open mashes wait on the slash line
                float depth = Perspective.DepthFromBeats(ahead, leadBeats);
                float y = Perspective.Y(depth);
                float scaleFactor = e.Soldier != null ? (soldierLeads ? .34f : .09f) : e.Charge ? .36f : e.Gesture == Gesture.Mash ? .42f : e.Gesture == Gesture.Hold ? .16f : e.Item != ItemKind.None ? .25f : .21f;
                float size = scaleFactor * Perspective.Scale(depth) * Unit;
                RectTransform rect = view.Root;
                rect.sizeDelta = new Vector2(size, size);
                Vector2 shake = Vector2.zero;
                if (e.Gesture == Gesture.Mash)
                {
                    float hit = Mathf.Clamp01(1f - (now - mashHitAt) / .12f);
                    shake = new Vector2(Mathf.Sin(now * 80f) * hit * Unit * .02f, 0);
                    float crack = e.MashRequired > 0 ? Mathf.Clamp01((float)e.MashCount / e.MashRequired) : 0;
                    view.Body.rectTransform.localScale = Vector3.one * (1f + .08f * hit + .06f * crack);
                    view.BaseColor = Color.Lerp(Color.white, new Color(1f, .82f, .7f), crack * .6f);
                }
                if (shot && !e.Charge) view.Body.rectTransform.localRotation = Quaternion.Euler(0, 0, (float)(beat * 180.0));
                if (e.Charge) { float pulse = 1f + .08f * Mathf.Sin(now * 18f) + (e.Holding ? .05f * Mathf.Sin(now * 70f) : 0); view.Body.rectTransform.localScale = Vector3.one * pulse; }
                if (e.Item != ItemKind.None) shake += new Vector2(0, Mathf.Sin(now * 6f) * size * .06f); // capsules bob
                float lateral = Perspective.LaneLateral(e.Lane);
                if (soldierLeads)
                {
                    // round 23: the frog hops to its next lane after a parry (a short arc instead of a jump cut)
                    float parriedAt; int fromLane;
                    if (soldierParryAt.TryGetValue(e.SoldierGroup, out parriedAt) && soldierLaneBefore.TryGetValue(e.SoldierGroup, out fromLane) && fromLane != e.Lane)
                    {
                        float hop = Mathf.Clamp01((now - parriedAt) / .22f);
                        lateral = Mathf.Lerp(Perspective.LaneLateral(fromLane), lateral, Mathf.SmoothStep(0, 1, hop));
                        shake += new Vector2(0, Mathf.Sin(hop * Mathf.PI) * size * .5f);
                    }
                }
                rect.anchoredPosition = Local(Perspective.X(lateral, depth), y) + new Vector2(0, size * .45f) + shake;
                if (!double.IsNaN(e.SplitFromBeat))
                {
                    // round 27: the half flies out of the cut melon in an arc, lands in its lane and rolls in
                    float pop = Mathf.Clamp01((float)(beat - e.SplitFromBeat) / .45f);
                    if (pop < 1f)
                    {
                        float lineSize = scaleFactor * Perspective.Scale(1f) * Unit;
                        Vector2 from = Local(Perspective.X(Perspective.LaneLateral(e.SplitFromLane), 1f), Perspective.SlashY) + new Vector2(0, lineSize * .45f);
                        rect.anchoredPosition = Vector2.Lerp(from, rect.anchoredPosition, Anim.OutCubic(pop)) + new Vector2(0, Mathf.Sin(pop * Mathf.PI) * Unit * .16f);
                        rect.sizeDelta = Vector2.Lerp(new Vector2(lineSize, lineSize), rect.sizeDelta, pop);
                    }
                    view.Body.rectTransform.localRotation = Quaternion.Euler(0, 0, (e.SplitFromLane < e.Lane ? -1f : 1f) * (float)(beat - e.SplitFromBeat) * 220f);
                }
                if (e.Bomb && Effects != EffectLevel.Weak && now - lastFuseSpark > .05f && ahead > -.3)
                {
                    // the fuse crackles: sparks jump off its tip
                    lastFuseSpark = now;
                    Vector2 tip = rect.anchoredPosition + new Vector2(size * .22f, size * .48f);
                    Spawn(ArtLibrary.Get("vfx_sparkle") ?? ArtLibrary.Star, tip, Vector2.one * size * Random.Range(.12f, .22f), new Vector2(Random.Range(-.25f, .25f), Random.Range(.1f, .4f)) * Unit, 400f, .25f, 1f, .3f, new Color(1f, .8f, .3f, 1f), Unit * .8f);
                }
                // a solid object: its back face lies toward the vanishing point, so we see a little of its top and side
                Vector2 toVp = Local(.5f, Perspective.HorizonY) - rect.anchoredPosition;
                view.Body.SetDepth(view.Thick > 0 ? toVp.normalized * size * view.Thick : Vector2.zero);
                float fadeIn = Mathf.Clamp01(depth * 8f);
                float missed = ahead < -goodBeats ? Mathf.Clamp01(1f - (float)(-ahead - goodBeats) / .25f) : 1f;
                float alpha = fadeIn * missed;
                view.Body.canvasRenderer.SetAlpha(alpha);
                view.Shadow.canvasRenderer.SetAlpha(alpha);
                view.Contact.canvasRenderer.SetAlpha(alpha);
                // round 12: lit by the stage and taken by its haze in the distance, like the 3D world around it
                Color tint = Color.Lerp(stageFog, stageLight, Mathf.Clamp01(.3f + depth * 1.35f));
                Color bodyColor = new Color(view.BaseColor.r * tint.r, view.BaseColor.g * tint.g, view.BaseColor.b * tint.b, view.BaseColor.a);
                if (view.Body.color != bodyColor) view.Body.color = bodyColor;
                Color shadowColor = new Color(stageFog.r * .2f, stageFog.g * .16f, stageFog.b * .16f, .38f);
                if (view.Shadow.color != shadowColor) view.Shadow.color = shadowColor;
                // timing highlight inside the GOOD window (intact → highlight state)
                float inWindow = Mathf.Clamp01(1f - Mathf.Abs((float)ahead) / goodBeats);
                view.Glow.enabled = inWindow > 0 || e.Offbeat;
                if (view.Glow.enabled)
                {
                    view.Glow.color = e.Offbeat ? new Color(.95f, .6f, 1f, 1) : new Color(.4f, .95f, 1f, 1);
                    // offbeats glow purple all the way down the road, pulsing on the half beat they belong to
                    float offGlow = e.Offbeat ? (.45f + .2f * Mathf.Abs(Mathf.Sin((float)beat * Mathf.PI * 2f))) * alpha : 0f;
                    view.Glow.canvasRenderer.SetAlpha(Mathf.Max(.8f * inWindow * missed, offGlow));
                }
                // gold shots, items and the charge ball always glow (they are special)
                // the soldier's sword flashes white just before it strikes: the cue to parry
                if (soldierLeads && ahead < .6 && ahead > -goodBeats)
                {
                    view.Glow.enabled = true;
                    view.Glow.color = new Color(1f, 1f, .85f, 1);
                    view.Glow.canvasRenderer.SetAlpha(alpha * (.5f + .45f * Mathf.Abs(Mathf.Sin(now * 22f))));
                }
                if (e.Bomb)
                {
                    // a bomb glows red, faster as it comes: never mistaken for something to cut
                    view.Glow.enabled = true;
                    view.Glow.color = new Color(1f, .25f, .15f, 1);
                    view.Glow.canvasRenderer.SetAlpha(alpha * (.45f + .35f * Mathf.Abs(Mathf.Sin(now * (6f + 10f * Mathf.Clamp01(1f - (float)ahead / 3f))))));
                }
                else if (e.Giant)
                {
                    // round 27: everything the giant can reach glows gold (so the end of the giant is visible on the road)
                    view.Glow.enabled = true;
                    view.Glow.color = new Color(1f, .82f, .25f, 1);
                    view.Glow.canvasRenderer.SetAlpha(alpha * (.85f + .15f * Mathf.Sin(now * 9f)));
                    view.Body.color = new Color(view.Body.color.r, view.Body.color.g * .93f, view.Body.color.b * .6f, view.Body.color.a); // a golden sheen
                }
                if (e.Gold || e.Item != ItemKind.None || e.Charge)
                {
                    view.Glow.enabled = true;
                    view.Glow.color = e.Charge ? new Color(.85f, .3f, 1f, 1) : new Color(1f, .85f, .3f, 1);
                    view.Glow.canvasRenderer.SetAlpha(alpha * (.55f + .25f * Mathf.Sin(now * 8f)));
                }
                bool ringOn = ahead <= 1 && ahead >= 0 && !e.Holding && !e.Bomb && !(e.Gesture == Gesture.Mash && beat >= e.Beat) && (e.Soldier == null || soldierLeads); // never "cut here" on a bomb
                view.Ring.enabled = ringOn;
                if (ringOn)
                {
                    float k = 1f + 1.3f * (float)ahead;
                    view.Ring.rectTransform.localScale = new Vector3(k, k, 1);
                    view.Ring.canvasRenderer.SetAlpha(.85f * (1f - (float)ahead * .5f));
                }
                if (e.Offbeat)
                {
                    view.Diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, (float)(beat * 22.5));
                    view.Diamond.canvasRenderer.SetAlpha(alpha);
                    view.OffRing.canvasRenderer.SetAlpha(alpha);
                }
            }
            foreach (string id in scratch)
            {
                targetPool.Release(live[id].Root);
                live.Remove(id);
            }
            foreach (string id in holdScratch)
            {
                HoldBodyGraphic h = holdBodies[id];
                h.gameObject.SetActive(false); freeHoldBodies.Push(h); holdBodies.Remove(id);
            }
            UpdateMashUi(openMash, beat);
            if (openMash == null) { UpdateChargeUi(engine, beat); UpdateSlideUi(engine, beat); }
        }

        /// <summary>The charge attack spells out what to do: the lane glows, "長押し！", then a gauge fills while guarding.</summary>
        void UpdateChargeUi(RunEngine engine, double beat)
        {
            ScheduledEvent charge = null;
            foreach (ScheduledEvent e in engine.Events) if (e.Charge && !e.Resolved && e.Beat - beat <= 2 && beat <= e.EndBeat) { charge = e; break; }
            if (charge == null) return;
            float now = Time.unscaledTime;
            Vector2 lineAt = Local(Perspective.X(Perspective.LaneLateral(charge.Lane), 1f), Perspective.SlashY);
            if (!charge.Holding)
            {
                laneFlashAt[charge.Lane] = now - .04f; // steady glow on the lane to hold
                ShowHand(lineAt, 0f, false);
                string text = HintWords ? "長押し！" : "";
                if (mashLabel.text != text) mashLabel.text = text;
                mashLabel.color = UiKit.Offbeat;
                mashLabel.rectTransform.anchoredPosition = lineAt + new Vector2(0, Unit * .32f);
                mashLabel.rectTransform.localScale = Vector3.one * (1f + .12f * Mathf.Abs(Mathf.Sin(now * 8f)));
                mashRing.enabled = false;
                return;
            }
            // guarding: a ring gauge fills up to the moment the counter fires
            float fill = Mathf.Clamp01((float)((beat - charge.Beat) / System.Math.Max(.01, charge.Length)));
            mashRing.enabled = true;
            mashRing.fillAmount = fill;
            mashRing.color = fill > .95f ? UiKit.Gold : UiKit.Cyan;
            mashRing.rectTransform.anchoredPosition = lineAt + new Vector2(0, Unit * .1f);
            mashRing.rectTransform.sizeDelta = Vector2.one * Unit * .34f;
            ShowHand(lineAt, 0f, true);
            string guard = fill > .95f ? "はなせ！" : HintWords ? "そのまま！" : "";
            if (mashLabel.text != guard) mashLabel.text = guard;
            mashLabel.color = fill > .95f ? UiKit.Gold : UiKit.Cyan;
            mashLabel.rectTransform.anchoredPosition = lineAt + new Vector2(0, Unit * .32f);
            mashLabel.rectTransform.localScale = Vector3.one;
        }

        /// <summary>The tapping hand: bobs <paramref name="rate"/> taps per second (0 = presses and stays down).</summary>
        void ShowHand(Vector2 tip, float rate, bool pressed)
        {
            if (tapHand == null || tapHand.sprite == null) return;
            tapHand.enabled = true;
            float h = Unit * .2f;
            tapHand.rectTransform.sizeDelta = new Vector2(h * tapHand.sprite.rect.width / tapHand.sprite.rect.height, h);
            float up = rate > 0 ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * rate * Mathf.PI)) : pressed ? 0f : .35f + .25f * Mathf.Sin(Time.unscaledTime * 6f);
            tapHand.rectTransform.anchoredPosition = tip + new Vector2(0, -h * .05f + h * .3f * up);
            tapHand.rectTransform.localScale = Vector3.one * (pressed ? .92f : 1f);
            tapHand.color = new Color(1, 1, 1, .95f);
        }

        /// <summary>
        /// Slides (owner round 11): while one is held, the tapping hand sits on the lane the slide is in and glides to the
        /// next lane as a turn comes up, so the finger knows where and when to go ("なぞれ！" until a boss is beaten).
        /// </summary>
        void UpdateSlideUi(RunEngine engine, double beat)
        {
            ScheduledEvent slide = null;
            foreach (ScheduledEvent e in engine.Events) if (e.IsSlide && e.Holding && !e.Resolved) { slide = e; break; }
            if (slide == null) return;
            int now = slide.LaneAt(beat), next = now;
            double turnAt = double.MaxValue;
            for (int i = 0; i < slide.SlideBeats.Length; i++) if (slide.SlideBeats[i] > beat - .25) { turnAt = slide.SlideBeats[i]; next = slide.SlideLanes[i]; if (slide.SlideBeats[i] > beat) now = i == 0 ? slide.Lane : slide.SlideLanes[i - 1]; break; }
            float k = turnAt - beat < 1.0 ? Mathf.Clamp01((float)(1.0 - (turnAt - beat))) : 0f;
            k = k * k * (3f - 2f * k);
            Vector2 from = Local(Perspective.X(Perspective.LaneLateral(now), 1f), Perspective.SlashY - .01f);
            Vector2 to = Local(Perspective.X(Perspective.LaneLateral(next), 1f), Perspective.SlashY - .01f);
            Vector2 tip = Vector2.Lerp(from, to, k);
            ShowHand(tip, 0f, true);
            // the lane to slide into lights up on the judgement line as the turn comes (owner round 16: "スライドが分かりにくい")
            if (next != now && k > 0) laneFlashAt[next] = Time.unscaledTime - .16f * (1f - k);
            string text = HintWords ? (next != now && k > 0 ? (next < now ? "← なぞれ！" : "なぞれ！ →") : "なぞれ！") : "";
            if (mashLabel.text != text) mashLabel.text = text;
            mashLabel.color = new Color(.95f, .75f, 1f);
            // beside the hand, on the side away from where it is going (never over the slide itself)
            float away = next < now ? 1f : next > now ? -1f : (now == 0 ? 1f : -1f);
            mashLabel.rectTransform.anchoredPosition = tip + new Vector2(away * Unit * .34f, Unit * .1f);
            mashLabel.rectTransform.localScale = Vector3.one;
        }

        void UpdateHoldBody(RunEngine engine, ScheduledEvent e, double beat)
        {
            double headAhead = e.Beat - beat, tailAhead = e.EndBeat - beat;
            bool visible = !e.Resolved && headAhead <= leadBeats && tailAhead > -.2;
            if (!visible) return;
            holdScratch.Remove(e.Id);
            HoldBodyGraphic body;
            if (!holdBodies.TryGetValue(e.Id, out body))
            {
                body = freeHoldBodies.Count > 0 ? freeHoldBodies.Pop() : NewHoldBody();
                body.gameObject.SetActive(true);
                body.transform.SetAsFirstSibling();
                body.Texture = e.Charge ? null : ArtLibrary.Texture("hold_" + KindOf(engine, e));
                body.Cut.Texture = e.Charge || e.IsSlide ? null : ArtLibrary.Texture("hold_" + KindOf(engine, e) + "_half");
                body.TurnLaterals = e.IsSlide ? System.Array.ConvertAll(e.SlideLanes, l => Perspective.LaneLateral(l)) : null;
                body.TurnAheads = e.IsSlide ? new double[e.SlideLanes.Length] : null;
                body.Tint = e.Charge ? new Color(.8f, .35f, 1f, .55f) : Color.white;
                holdBodies[e.Id] = body;
            }
            body.Lateral = Perspective.LaneLateral(e.Lane);
            body.LeadBeats = leadBeats;
            if (body.IsSlide)
            {
                for (int i = 0; i < e.SlideBeats.Length; i++) body.TurnAheads[i] = e.SlideBeats[i] - beat;
                if (e.Holding) holdingLane = e.LaneAt(beat); // the hamster swings at the lane the slide is in now
            }
            body.Width = (e.Charge ? .2f : .17f) * Unit;
            body.Glowing = e.Holding;
            body.LengthBeats = e.Length;
            if (e.Holding)
            {
                body.NearAhead = 0;
                body.NearV = Mathf.Clamp01((float)((beat - e.Beat) / System.Math.Max(.01, e.Length)));
            }
            else { body.NearAhead = headAhead; body.NearV = 0; }
            body.FarAhead = System.Math.Min(tailAhead, leadBeats + .5);
            float goodBeats = (float)(engine.Config.GoodMs / 1000.0 * bpm / 60.0);
            body.Alpha = !e.Holding && headAhead < -goodBeats ? Mathf.Clamp01(1f - (float)(-headAhead - goodBeats) / .25f) : 1f;
            // round 12: food and things are real 3D objects in the world now (the boss's energy tail stays a glow strip)
            string kind = e.Charge ? null : KindOf(engine, e);
            bool solid = kind != null && World3D.IsLongKind(kind);
            body.enabled = !solid;
            if (solid) beatLines.Occluders.Add(body);
            if (solid && !e.IsSlide) body.Cut.Texture = null; // the 3D object shows its own cut face (no flat split halves)
            if (solid)
                world.DrawLong(e.Id, kind, e.Holding ? 0 : headAhead, body.FarAhead, headAhead, e.Length, leadBeats, body.LateralAt, body.Alpha, e.Holding,
                               ArtLibrary.Get("hold_" + kind + "_slice"), e.IsSlide);
            body.Refresh();
        }

        HoldBodyGraphic NewHoldBody()
        {
            RectTransform rect = UiKit.Rect("HoldBody", targetLayer, Vector2.zero, Vector2.one);
            var g = rect.gameObject.AddComponent<HoldBodyGraphic>();
            g.raycastTarget = false;
            RectTransform cut = UiKit.Rect("Cut", rect, Vector2.zero, Vector2.one);
            g.Cut = cut.gameObject.AddComponent<HoldCutGraphic>();
            g.Cut.raycastTarget = false; g.Cut.Body = g;
            return g;
        }

        // round 31 (owner: "ゲージが満杯になってから切れる"): the full ring stays up for a moment as the object breaks
        float mashFullUntil = -9;
        public bool MashFullShowing { get { return Time.unscaledTime < mashFullUntil; } }

        void UpdateMashUi(ScheduledEvent mash, double beat)
        {
            bool show = mash != null && !mash.Resolved && beat >= mash.Beat - .25 && !(mash.Boss && (!boss.Visible || current == null || !current.BossActive));
            bool flash = !show && Time.unscaledTime < mashFullUntil;
            mashRing.enabled = show || flash;
            if (tapHand != null) tapHand.enabled = false;
            if (flash)
            {
                mashRing.fillAmount = 1f;
                mashRing.color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 2f, 1f), .5f, 1f);
                if (mashLabel.text != "MAX!") mashLabel.text = "MAX!";
                return;
            }
            if (!show) { if (mashLabel.text.Length > 0) mashLabel.text = ""; mashLabel.color = UiKit.Gold; return; }
            mashLabel.color = UiKit.Gold;
            Vector2 at = mash.Boss ? boss.Center : Local(.5f, Perspective.SlashY) + new Vector2(0, .2f * Unit);
            float hit = Mathf.Clamp01(1f - (Time.unscaledTime - mashHitAt) / .12f);
            mashRing.rectTransform.anchoredPosition = at;
            mashRing.rectTransform.sizeDelta = Vector2.one * Unit * (mash.Boss ? .75f : .5f) * (1f + .06f * hit);
            mashRing.fillAmount = Mathf.Clamp01((float)mash.MashCount / Mathf.Max(1, mash.MashRequired));
            mashRing.color = mash.MashCount >= mash.MashRequired ? Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime, 1f), .5f, 1f) : UiKit.Gold;
            // no numbers: the ring fills and the boss cracks wider with every hit; a hand taps on the line
            bool full = mash.MashCount >= mash.MashRequired;
            // owner round 16: the finisher always says it, big — it is the climax of the fight
            string text = mash.Boss ? (full ? "とどめ！！" : "連打！！") : full ? "MAX!" : "連打！";
            if (mashLabel.text != text) mashLabel.text = text;
            Vector2 handAt = mash.Boss ? Local(.5f, Perspective.SlashY - .015f) : at + new Vector2(Unit * .22f, -Unit * .05f);
            ShowHand(handAt, 7f, full);
            mashLabel.rectTransform.anchoredPosition = mash.Boss ? handAt + new Vector2(0, Unit * .27f) : at + new Vector2(0, Unit * .3f);
            mashLabel.rectTransform.localScale = Vector3.one * (1f + .15f * hit);
        }

        /// <summary>A press inside an open mash: the object shakes and cracks, the hero chops, a finisher sends a spark at the boss.</summary>
        public void MashTap(RunEngine engine, ScheduledEvent e)
        {
            float now = Time.unscaledTime;
            mashHitAt = now;
            slashFlip = (slashFlip + 1) % 3;
            slashLane = slashFlip == 0 ? 1 : slashFlip == 1 ? 0 : 2;
            slashUntil = now + .12f;
            Vector2 at = Local(.5f, Perspective.SlashY) + new Vector2(0, (e.Boss ? .1f : .2f) * Unit);
            if (Effects != EffectLevel.Weak && budget.TryFlash(FlashBudget.Region.Lane, now)) laneFlashAt[1] = now;
            if (e.Boss)
            {
                boss.Pound();
                boss.Reflect(ArtLibrary.Get("vfx_sparkle"), at, slashLane, Unit, 0, true);
                Sprite crack = ArtLibrary.Get("vfx_chips");
                if (crack != null && Effects != EffectLevel.Weak) Spawn(crack, boss.Center + Random.insideUnitCircle * Unit * .12f, new Vector2(.22f, .18f) * Unit, Vector2.zero, 0, .25f, .6f, 1.2f, Color.white, 0);
                return;
            }
            Sprite chips = ArtLibrary.Get(MaterialBurst(KindOf(engine, e)));
            if (chips != null && Effects != EffectLevel.Weak) Spawn(chips, at + Random.insideUnitCircle * Unit * .06f, new Vector2(.18f * Unit, .15f * Unit), Vector2.zero, 0, .25f, .5f, 1.1f, Color.white, 0);
        }

        /// <summary>While a lane is held (every half beat): sparks and shavings spray from the point where the blade splits it.</summary>
        public void HoldSlice(RunEngine engine)
        {
            if (holdingLane < 0 || holdingKind == null || chargeGuard) return;
            Vector2 at = Local(Perspective.X(Perspective.LaneLateral(holdingLane), 1f), Perspective.SlashY);
            Sprite bit = ArtLibrary.Get("hold_" + holdingKind + "_slice");
            if (bit != null && holdingKind == "train" && Effects != EffectLevel.Weak) // the other long kinds throw real 3D slices
            {
                // a slide is eaten piece by piece: a tangle of noodles is slurped away, a train car comes loose
                float side = Random.value < .5f ? -1f : 1f, s = Unit * (holdingKind == "train" ? .11f : .09f);
                Spawn(bit, at + new Vector2(0, Unit * .04f), new Vector2(s, s), new Vector2(side * Unit * Random.Range(.4f, .7f), Unit * Random.Range(.15f, .35f)), side * -240f, .5f, 1f, .8f, Color.white, Unit * 2f);
            }
            Sprite spark = ArtLibrary.Get("vfx_sparkle");
            if (spark != null && Effects != EffectLevel.Weak) Spawn(spark, at, new Vector2(.09f, .09f) * Unit, Vector2.zero, 200f, .18f, .6f, 1.2f, new Color(1f, .95f, .7f, 1f), 0);
            for (int i = 0; i < (Effects == EffectLevel.Weak ? 1 : 3); i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float s = Unit * Random.Range(.015f, .03f);
                Spawn(ArtLibrary.Circle, at, new Vector2(s, s * 1.6f), new Vector2(side * Unit * Random.Range(.3f, .6f), -Unit * Random.Range(.05f, .2f)), side * 300f, .35f, 1f, .6f, DropColor(holdingKind), Unit * 1.8f);
            }
        }

        /// <summary>Left and right halves of a hold object's texture (made once per kind).</summary>
        Sprite[] HalvesOf(string kind)
        {
            Sprite[] halves;
            if (halfSprites.TryGetValue(kind, out halves)) return halves;
            // the lengthwise cross-section picture: both halves show the inside (the second one mirrored)
            Sprite face = ArtLibrary.Get("hold_" + kind + "_half");
            if (face != null) { halves = new[] { face, face }; halfSprites[kind] = halves; return halves; }
            Texture2D tex = ArtLibrary.Texture("hold_" + kind);
            halves = tex == null ? new Sprite[0] : new[]
            {
                Sprite.Create(tex, new Rect(0, 0, tex.width / 2, tex.height), new Vector2(.5f, .5f)),
                Sprite.Create(tex, new Rect(tex.width / 2, 0, tex.width - tex.width / 2, tex.height), new Vector2(.5f, .5f)),
            };
            halfSprites[kind] = halves;
            return halves;
        }

        static string MaterialBurst(string kind)
        {
            switch (kind)
            {
                case "barrel": case "crate": case "dice": case "pencil": return "vfx_chips";
                case "gem": case "duck": case "clock": case "safe": return "vfx_sparkle";
                default: return "vfx_juice";
            }
        }

        static Color DropColor(string kind)
        {
            switch (kind)
            {
                case "barrel": case "crate": case "pencil": return new Color(.75f, .55f, .3f);
                case "watermelon": return new Color(.95f, .25f, .3f);
                case "melon_big": case "melon_half": return new Color(1f, .62f, .3f);
                case "gem": return new Color(.45f, .85f, 1f);
                case "dice": return new Color(.95f, .95f, .95f);
                case "clock": return new Color(.85f, .7f, .3f);
                case "duck": return new Color(1f, .85f, .2f);
                case "pumpkin": return new Color(1f, .55f, .15f);
                case "safe": return new Color(1f, .8f, .3f);
                case "leek": return new Color(.5f, .85f, .35f);
                case "baguette": return new Color(.95f, .8f, .5f);
                case "ramen": return new Color(1f, .82f, .4f);
                case "candy": return new Color(1f, .3f, .35f);
                case "train": return new Color(.9f, .2f, .15f);
                default: return new Color(.9f, .1f, .2f);
            }
        }

        /// <summary>Slice/miss presentation for a judged event (called right after judgement).</summary>
        public void ShowJudgement(JudgeResult result, double beat, bool fromTap, int tappedLane)
        {
            float now = Time.unscaledTime;
            if (fromTap)
            {
                if (Effects != EffectLevel.Weak && budget.TryFlash(FlashBudget.Region.Lane, now)) laneFlashAt[tappedLane] = now;
                leanLane = tappedLane; leanAt = now; slashLane = tappedLane;
                if (result.Rating != Rating.Miss) slashUntil = now + SlashPose;
                if (result.Rating == Rating.Perfect && Effects != EffectLevel.Weak) { hitStopUntil = now + .045f; slashUntil += .045f; }
            }
            if (result.Rating == Rating.Miss && result.Kind != ResultKind.HoldBreak)
            {
                hitUntil = now + HitPose; missUntil = -1; // every MISS costs 1 HP (a broken hold costs none)
            }
            ScheduledEvent e = result.Event;
            if (e == null)
            {
                if (fromTap) SwingAtLane(tappedLane, false);
                return;
            }
            Vector2 at = Local(Perspective.X(Perspective.LaneLateral(e.LaneAt(beat)), 1f), Perspective.SlashY); // a slide ends where it has turned to
            float size = .21f * Unit;
            Vector2 center = at + new Vector2(0, size * .45f);
            string kind = KindOf(current, e);
            bool hitRating = result.Rating == Rating.Perfect || result.Rating == Rating.Good;
            if (result.ShieldBlocked) ShieldPop();
            if (e.Bomb)
            {
                ShowBomb(result, at, center, size);
                return;
            }
            if (result.Giant && hitRating) GiantSlash(fromTap ? tappedLane : e.Lane, 1f);
            if (result.Rush)
            {
                // rush: cut where the target is right now (still ahead on the road), sparks fly, no text spam
                double ahead = System.Math.Max(0, e.Beat - beat);
                float depth = Perspective.DepthFromBeats(ahead, leadBeats);
                float sz = .21f * Perspective.Scale(depth) * Unit;
                Vector2 pos = Local(Perspective.X(Perspective.LaneLateral(e.Lane), depth), Perspective.Y(depth)) + new Vector2(0, sz * .45f);
                LastHitPosition = pos;
                slashLane = e.Lane; slashUntil = now + .1f;
                Sprite cutSprite = ArtLibrary.Get(weaponVisual.TrailSprite) ?? ArtLibrary.Get("vfx_slash");
                Spawn(cutSprite ?? ArtLibrary.Circle, pos, new Vector2(sz * 2.4f, sz * 1.4f), Vector2.zero, 0, .16f, .8f, 1.2f, Color.white, 0, Random.Range(-40f, 40f));
                // a screen-wide slash line from the hero through the target, alternating direction
                Vector2 from = hero.anchoredPosition + new Vector2(0, hero.sizeDelta.y * .6f);
                Vector2 dir = pos - from;
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + ((rushHits & 1) == 0 ? 8f : -8f);
                Sprite beam = ArtLibrary.Get("vfx_slash_rainbow") ?? cutSprite;
                if (beam != null && Effects != EffectLevel.Weak) Spawn(beam, Vector2.Lerp(from, pos, .55f), new Vector2(dir.magnitude * 1.5f, Unit * .16f), Vector2.zero, 0, .14f, 1f, 1.1f, new Color(1, 1, 1, .9f), 0, ang);
                rushHits++;
                rushCounter.text = "×" + rushHits + " HIT";
                rushCounterAt = now;
                Sprite rl = ArtLibrary.Get(kind + "_split_l") ?? ArtLibrary.Get("hold_" + kind + "_slice") ?? ArtLibrary.Get(kind);
                Sprite rr = ArtLibrary.Get(kind + "_split_r") ?? rl;
                if (rl != null)
                {
                    SpawnSlice(rl, pos + new Vector2(-sz * .2f, 0), new Vector2(sz * .55f, sz), new Vector2(-Unit * .9f, Unit * .1f), 120f);
                    SpawnSlice(rr, pos + new Vector2(sz * .2f, 0), new Vector2(sz * .55f, sz), new Vector2(Unit * .9f, Unit * .15f), -120f);
                }
                Sprite rb = ArtLibrary.Get(MaterialBurst(kind));
                if (rb != null && Effects != EffectLevel.Weak) Spawn(rb, pos, new Vector2(sz * 1.6f, sz * 1.3f), Vector2.zero, 0, .25f, .6f, 1.3f, Color.white, 0);
                return;
            }
            if (result.ItemGot != ItemKind.None)
            {
                LastHitPosition = at + new Vector2(0, -size * .25f);
                Spawn(ArtLibrary.Glow, center, new Vector2(size * 2.2f, size * 2.2f), Vector2.zero, 0, .3f, .6f, 1.5f, new Color(1f, .9f, .4f, .9f), 0);
                Sprite star = ArtLibrary.Get("vfx_sparkle") ?? ArtLibrary.Star;
                for (int i = 0; i < (Effects == EffectLevel.Weak ? 2 : 8); i++)
                {
                    float a = Random.Range(-30f, 210f) * Mathf.Deg2Rad;
                    if (a > 30 * Mathf.Deg2Rad && a < 150 * Mathf.Deg2Rad) a = -a;
                    Spawn(star, center, new Vector2(.06f, .06f) * Unit, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Unit * Random.Range(.3f, .7f), 180f, .5f, 1f, .6f, Color.white, Unit);
                }
                string name = result.ItemGot == ItemKind.Booster ? "斬撃ラッシュ GET!" : result.ItemGot == ItemKind.Giant ? "巨大化 GET!" : result.ItemGot == ItemKind.Heart ? (result.Healed ? "♥ +1" : "コイン +5") : "バリア GET!";
                Popup(name, result.ItemGot == ItemKind.Heart && result.Healed ? UiKit.Coral : UiKit.Gold, e.Lane, size * 1.2f);
                return;
            }
            if (e.Charge)
            {
                if (result.Kind == ResultKind.HoldHead) { holdingLane = e.Lane; holdingKind = null; chargeGuard = true; Popup("ガード！", UiKit.Cyan, e.Lane, size); return; }
                chargeGuard = false;
                if (holdingLane == e.Lane) holdingLane = -1;
                if (result.Kind == ResultKind.HoldTail && e.Soldier != null) { /* the crow cook's guard ends as a parry (below) */ }
                else if (result.Kind == ResultKind.HoldTail)
                {
                    // guarded to the end: the ball is thrown back at full size
                    criticalAt = now;
                    boss.Reflect(ArtLibrary.Get("shot_charge"), center, e.Lane, Unit * 2.2f, result.BossDamage);
                    MashPopup("カウンター！！", UiKit.Gold);
                    return;
                }
                if (result.Kind == ResultKind.HoldBreak) { Popup("BREAK", UiKit.Coral, e.Lane, size * .8f); return; }
                if (result.Rating == Rating.Miss) { Popup("MISS", UiKit.Coral, e.Lane, size * .8f); return; }
            }
            if (result.Kind == ResultKind.HoldHead)
            {
                holdingLane = e.Lane; holdingKind = kind;
                Spawn(ArtLibrary.Glow, center, new Vector2(size * 1.4f, size * 1.4f), Vector2.zero, 0, .1f, .7f, 1.15f, new Color(1f, .85f, .35f, .85f), 0);
                JudgePopup(result, e.Lane, size);
                return;
            }
            if (result.Kind == ResultKind.HoldBreak)
            {
                if (holdingLane == e.Lane || e.IsSlide) holdingLane = -1;
                Popup("BREAK", UiKit.Coral, e.LaneAt(beat), size * .8f);
                return;
            }
            if (result.Kind == ResultKind.HoldTail && !(e.Charge && e.Soldier != null))
            {
                if (holdingLane == e.Lane || e.IsSlide) holdingLane = -1;
                slashLane = e.LaneAt(beat); slashUntil = Time.unscaledTime + SlashPose;
                // the last stretch splits open: both halves fly apart (a slide bursts into its pieces instead)
                // a chopped long object ends in a little burst of its last slices (never two lengthwise halves)
                bool longKind = World3D.IsLongKind(kind);
                Sprite[] halves = e.IsSlide || longKind ? new Sprite[0] : HalvesOf(kind);
                Sprite bit = e.IsSlide || longKind ? ArtLibrary.Get("hold_" + kind + "_slice") : null;
                if (bit != null)
                    for (int i = 0; i < (Effects == EffectLevel.Weak ? 2 : 5); i++)
                    {
                        float a = Mathf.Lerp(20f, 160f, i / 4f) * Mathf.Deg2Rad;
                        Spawn(bit, center, Vector2.one * Unit * .12f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Unit * Random.Range(.5f, .9f), Random.Range(-300f, 300f), .6f, 1f, .8f, Color.white, Unit * 2.2f);
                    }
                if (halves.Length == 2)
                {
                    float h = Unit * .5f, w = h * halves[0].rect.width / halves[0].rect.height;
                    Vector2 mid = at + new Vector2(0, h * .3f);
                    SpawnSlice(halves[0], mid + new Vector2(-w * .6f, 0), new Vector2(w, h), new Vector2(-Unit * .8f, -Unit * .1f), 70f);
                    SpawnSlice(halves[1], mid + new Vector2(w * .6f, 0), new Vector2(w, h), new Vector2(Unit * .8f, -Unit * .05f), -70f, halves[0] == halves[1]);
                }
                Sprite spark = ArtLibrary.Get("vfx_sparkle");
                if (spark != null && Effects != EffectLevel.Weak) Spawn(spark, center, new Vector2(size, size), Vector2.zero, 90f, .4f, .5f, 1.2f, Color.white, 0);
                Popup("COMPLETE!", UiKit.Gold, e.LaneAt(beat), size);
                return;
            }
            if (result.Kind == ResultKind.MashEnd)
            {
                if (e.Boss) { if (!hitRating) Popup("MISS", UiKit.Coral, 1, size * .8f); return; }
                if (!hitRating) { Popup("MISS", UiKit.Coral, 1, size * .8f); return; }
                float big = .42f * Unit;
                Vector2 mid = Local(.5f, Perspective.SlashY) + new Vector2(0, big * .45f);
                if (e.MashCount < e.MashRequired)
                {
                    // time ran out before the gauge was full: it does not break — it cracks and rolls away ("おしい！")
                    Sprite whole = ArtLibrary.Get(kind + "_intact") ?? ArtLibrary.Get(kind) ?? ArtLibrary.Circle;
                    Spawn(whole, mid, new Vector2(big, big), new Vector2(Unit * .35f, -Unit * .6f), -160f, .5f, 1f, .8f, new Color(.85f, .85f, .85f, 1f), Unit * 1.2f);
                    MashPopup("おしい！", UiKit.Cyan);
                    return;
                }
                mashFullUntil = Time.unscaledTime + .35f;
                LastHitPosition = mid + new Vector2(0, -big * .4f);
                Spawn(ArtLibrary.Glow, mid, new Vector2(big * 1.8f, big * 1.8f), Vector2.zero, 0, .25f, .6f, 1.4f, new Color(1f, .85f, .35f, .9f), 0);
                Sprite ml = ArtLibrary.Get(kind + "_split_l"), mr = ArtLibrary.Get(kind + "_split_r");
                SpawnSlice(ml ?? ArtLibrary.Circle, mid + new Vector2(-big * .2f, -big * .1f), new Vector2(big * .55f, big), new Vector2(-Unit * .9f, -Unit * .2f), 80f);
                SpawnSlice(mr ?? ArtLibrary.Circle, mid + new Vector2(big * .2f, -big * .1f), new Vector2(big * .55f, big), new Vector2(Unit * .9f, -Unit * .1f), -80f);
                Sprite mb = ArtLibrary.Get(MaterialBurst(kind));
                if (mb != null && Effects != EffectLevel.Weak) Spawn(mb, mid, new Vector2(big * 1.6f, big * 1.3f), Vector2.zero, 0, .4f, .6f, 1.4f, Color.white, 0);
                for (int i = 0; i < Particles * 2; i++)
                {
                    float angle = Random.Range(-30f, 210f) * Mathf.Deg2Rad;
                    if (angle > 30 * Mathf.Deg2Rad && angle < 150 * Mathf.Deg2Rad) angle = -angle;
                    float s = big * Random.Range(.05f, .1f);
                    Spawn(kind == "safe" ? (ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle) : ArtLibrary.Circle, mid, new Vector2(s, s), new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Unit * Random.Range(.4f, .9f), 0, .55f, 1f, .6f, kind == "safe" ? Color.white : DropColor(kind), Unit * 1.8f);
                }
                MashPopup(result.Rating == Rating.Perfect ? "粉砕！！" : "GOOD", result.Rating == Rating.Perfect ? UiKit.Gold : UiKit.Cyan);
                return;
            }
            if (hitRating && e.Soldier != null && !result.Rush)
            {
                // round 17: "キン！" — sparks off the blades; the last strike is the counter that sends the soldier flying
                soldierParryAt[e.SoldierGroup] = now;
                soldierLaneBefore[e.SoldierGroup] = e.Lane;
                LastHitPosition = at + new Vector2(0, -size * .25f);
                bool perfectParry = result.Rating == Rating.Perfect;
                Vector2 clash = center + new Vector2(0, size * .35f);
                Spawn(ArtLibrary.Glow, clash, new Vector2(size * 1.7f, size * 1.7f), Vector2.zero, 0, .12f, .6f, 1.3f, perfectParry ? new Color(1f, .95f, .6f, .95f) : new Color(.7f, .95f, 1f, .85f), 0);
                Sprite trailP = ArtLibrary.Get(weaponVisual.TrailSprite) ?? ArtLibrary.Get("vfx_slash");
                Spawn(trailP ?? ArtLibrary.Circle, clash, new Vector2(size * 2.2f, size * 1.3f), Vector2.zero, 0, .16f, .85f, 1.1f, Color.white, 0, e.SoldierFinal ? -30f : 25f);
                Sprite starP = ArtLibrary.Get("vfx_sparkle") ?? ArtLibrary.Star;
                for (int i = 0; i < (Effects == EffectLevel.Weak ? 3 : 9); i++)
                {
                    float a = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                    Spawn(i % 3 == 0 ? starP : ArtLibrary.Circle, clash, Vector2.one * size * Random.Range(.05f, .12f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Unit * Random.Range(.4f, .9f), 360f, .35f, 1f, .4f, new Color(1f, .95f, .7f, 1f), Unit * 2.5f);
                }
                if (!e.SoldierFinal)
                {
                    if (e.Boss) boss.Reflect(ArtLibrary.Star, clash, e.Lane, Unit, result.BossDamage, true);
                    Popup(perfectParry ? "キン！" : "カン", perfectParry ? UiKit.Gold : UiKit.Cyan, e.Lane, size * (perfectParry ? 1.1f : .9f));
                    return;
                }
                string kindF = "soldier_" + e.Soldier;
                Sprite flying = ArtLibrary.Get(kindF + "_hurt") ?? ArtLibrary.Get(kindF + "_walk");
                criticalAt = perfectParry ? now : criticalAt;
                hitStopUntil = now + .08f; slashUntil = now + SlashPose + .08f;
                if (e.Boss) boss.Reflect(flying, center, e.Lane, Unit * 1.8f, result.BossDamage);
                else
                {
                    float side = e.Lane == 0 ? -1f : e.Lane == 2 ? 1f : (Random.value < .5f ? -1f : 1f);
                    Spawn(flying ?? ArtLibrary.Circle, center, new Vector2(size * 1.6f, size * 1.6f), new Vector2(side * Unit * .45f, Unit * 1.5f), side * -540f, .75f, 1f, .25f, Color.white, Unit * 1.2f);
                }
                MashPopup(perfectParry ? "反撃！！" : "はじき返した！", perfectParry ? UiKit.Gold : UiKit.Cyan);
                return;
            }
            if (hitRating && (e.ObjectId == "object_boss_shot" || e.Gold))
            {
                // plan A: the slashed shot flies back and hits the boss
                bool perfectShot = result.Rating == Rating.Perfect;
                if (result.Critical)
                {
                    criticalAt = now;
                    hitStopUntil = now + .1f; slashUntil = now + SlashPose + .1f; // a longer freeze for the weak point
                    boss.Reflect(ArtLibrary.Get(kind), center, e.Lane, Unit * 1.6f, result.BossDamage);
                    MashPopup("CRITICAL!!", UiKit.Gold);
                    return;
                }
                LastHitPosition = at + new Vector2(0, -size * .25f);
                Spawn(ArtLibrary.Glow, center, new Vector2(size * 1.4f, size * 1.4f), Vector2.zero, 0, .1f, .7f, 1.15f, perfectShot ? new Color(1f, .85f, .35f, .85f) : new Color(1f, .45f, .4f, .75f), 0);
                Sprite trailSprite = ArtLibrary.Get(weaponVisual.TrailSprite) ?? ArtLibrary.Get("vfx_slash");
                Spawn(trailSprite ?? ArtLibrary.Circle, center, new Vector2(size * 2.2f, size * 1.4f), Vector2.zero, 0, .2f, .85f, 1.1f, Color.white, 0, -18f);
                boss.Reflect(ArtLibrary.Get(kind), center, e.Lane, Unit, result.BossDamage);
                JudgePopup(result, e.Lane, size);
                return;
            }
            if (hitRating)
            {
                // slices and coins start under the slash line so the next target in this lane stays visible
                Vector2 under = at + new Vector2(0, -size * .25f);
                LastHitPosition = under;
                bool perfect = result.Rating == Rating.Perfect;
                // impact glow (≤1.5% of screen, ≥30 ms rise): coral, gold on PERFECT
                Spawn(ArtLibrary.Glow, center, new Vector2(size * 1.4f, size * 1.4f), Vector2.zero, 0, .1f, .7f, 1.15f, perfect ? new Color(1f, .85f, .35f, .85f) : new Color(1f, .45f, .4f, .75f), 0);
                // slices fly sideways and down, cut faces held toward the camera for 0.12 s, never upward over the next target
                // (a big split melon has no loose slices: its halves fly out as the next targets)
                if (!e.Splits)
                {
                    Sprite l = ArtLibrary.Get(kind + "_split_l"), r = ArtLibrary.Get(kind + "_split_r");
                    SpawnSlice(l ?? ArtLibrary.Circle, under + new Vector2(-size * .2f, 0), new Vector2(size * .55f, size), new Vector2(-Unit * .7f, -Unit * .25f), 60f);
                    SpawnSlice(r ?? ArtLibrary.Circle, under + new Vector2(size * .2f, 0), new Vector2(size * .55f, size), new Vector2(Unit * .7f, -Unit * .15f), -60f);
                }
                else Spawn(ArtLibrary.Glow, center, new Vector2(size * 2.4f, size * 2.4f), Vector2.zero, 0, .2f, .5f, 1.6f, new Color(1f, .75f, .35f, .9f), 0);
                Sprite burst = ArtLibrary.Get(MaterialBurst(kind));
                if (burst != null && Effects != EffectLevel.Weak) Spawn(burst, center + new Vector2(0, -size * .1f), new Vector2(size * 1.5f, size * 1.2f), Vector2.zero, 0, .32f, .55f, 1.25f, Color.white, 0);
                Color dropColor = DropColor(kind);
                for (int i = 0; i < Particles; i++)
                {
                    float angle = Mathf.Lerp(-30f, 210f, Random.value) * Mathf.Deg2Rad; // horizontal ±30° and downward only
                    if (angle > 30 * Mathf.Deg2Rad && angle < 150 * Mathf.Deg2Rad) angle = -angle;
                    float speed = Unit * Random.Range(.35f, .7f);
                    float s = size * Random.Range(.08f, .15f);
                    Spawn(ArtLibrary.Circle, center, new Vector2(s, s), new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, 0, .4f, 1f, .6f, dropColor, Unit * 1.8f);
                }
                string trail = fever || perfectStreak >= 10 ? "vfx_slash_rainbow" : weaponVisual.TrailSprite;
                Sprite slash = ArtLibrary.Get(trail) ?? ArtLibrary.Get("vfx_slash");
                Color slashTint = trail == weaponVisual.TrailSprite && trail == "vfx_slash" ? (perfectStreak >= 5 ? UiKit.Gold : Color.Lerp(Color.white, weaponVisual.Trail, .35f)) : Color.white;
                // arc from the hand side toward the target so the swing and the cut read as one motion
                Vector2 heroHand = Local(.5f, Perspective.CharacterY - .02f);
                Vector2 mid = Vector2.Lerp(heroHand, center, .7f);
                float arcAngle = Mathf.Atan2(center.y - heroHand.y, center.x - heroHand.x) * Mathf.Rad2Deg - 90f;
                Spawn(slash ?? ArtLibrary.Circle, mid, new Vector2(size * 2.4f, size * 1.5f), Vector2.zero, 0, .22f, .85f, 1.12f, slashTint, 0, arcAngle * .35f - 18f);
                if (perfect && Effects != EffectLevel.Weak)
                {
                    Sprite spark = ArtLibrary.Get("vfx_sparkle");
                    int stars = perfectStreak >= 20 ? 2 : 1;
                    for (int i = 0; i < stars; i++)
                        if (spark != null) Spawn(spark, center + new Vector2(size * (.3f - .6f * i), size * .3f), new Vector2(size * .8f, size * .8f), Vector2.zero, 90f, .35f, .4f, 1.1f, fever ? Color.HSVToRGB(Random.value, .4f, 1f) : Color.white, 0);
                }
                JudgePopup(result, e.Lane, size);
            }
            else if (result.Rating == Rating.Miss)
            {
                Popup("MISS", UiKit.Coral, e.Lane, size * .8f);
                if (fromTap) SwingAtLane(tappedLane, true);
            }
        }

        /// <summary>Round 27: a bomb was cut (boom), went by (dodged) or was stomped flat by the giant.</summary>
        void ShowBomb(JudgeResult result, Vector2 at, Vector2 center, float size)
        {
            float now = Time.unscaledTime;
            Sprite bomb = ArtLibrary.Get("bomb") ?? ArtLibrary.Circle;
            if (result.BombHit)
            {
                Sprite boom = ArtLibrary.Get("vfx_explosion");
                Spawn(boom ?? ArtLibrary.Glow, center, Vector2.one * size * 3.2f, Vector2.zero, 0, .5f, .5f, 1.4f, boom != null ? Color.white : new Color(1f, .5f, .15f, .95f), 0);
                Spawn(ArtLibrary.Glow, center, Vector2.one * size * 4f, Vector2.zero, 0, .25f, .6f, 1.5f, new Color(1f, .85f, .5f, .8f), 0);
                for (int i = 0; i < (Effects == EffectLevel.Weak ? 4 : 12); i++)
                {
                    float a = Random.Range(0f, Mathf.PI * 2);
                    Spawn(ArtLibrary.Circle, center, Vector2.one * size * Random.Range(.08f, .16f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Unit * Random.Range(.4f, .9f), 0, .5f, 1f, .5f, new Color(.25f, .22f, .22f, 1f), Unit * 1.5f);
                }
                criticalAt = now;
                MashPopup("ドカン！", UiKit.Coral);
                return;
            }
            if (result.Crushed)
            {
                Effect flat = Spawn(bomb, at + new Vector2(0, size * .1f), new Vector2(size * 1.3f, size * .35f), Vector2.zero, 0, .6f, 1f, 1f, new Color(.8f, .8f, .8f, 1f), 0);
                flat.Rect.SetSiblingIndex(0);
                Sprite dust = ArtLibrary.Get("vfx_dust_puff");
                if (dust != null) Spawn(dust, at, new Vector2(size * 2f, size * .8f), Vector2.zero, 0, .35f, .7f, 1.4f, Color.white, 0);
                Popup("ペシャ！", UiKit.Gold, result.Event.Lane, size);
                return;
            }
            if (result.Dodged)
            {
                // the bomb rolls on past the hero and fizzles out
                Spawn(bomb, center, Vector2.one * size, new Vector2((result.Event.Lane - 1) * Unit * .3f, -Unit * .9f), 200f, .4f, 1f, 1.4f, Color.white, 0);
                Popup("よけた！", UiKit.Cyan, result.Event.Lane, size);
            }
        }

        void SwingAtLane(int lane, bool dim)
        {
            Vector2 at = Local(Perspective.X(Perspective.LaneLateral(lane), 1f), Perspective.SlashY) + new Vector2(0, .09f * Unit);
            Sprite slash = ArtLibrary.Get(weaponVisual.TrailSprite) ?? ArtLibrary.Get("vfx_slash");
            Spawn(slash ?? ArtLibrary.Circle, at, new Vector2(.4f * Unit, .26f * Unit), Vector2.zero, 0, .16f, .7f, 1f, new Color(1, 1, 1, dim ? .35f : .55f), 0, -18f);
        }

        /// <summary>
        /// Owner round 10: PERFECT is a reward — big gold text that punches in with a ring and sparkles, a running
        /// "×n" while the streak lasts (gold → rainbow from 20); GOOD stays small and says FAST or SLOW so the player
        /// learns which way they are off (offbeats especially).
        /// </summary>
        void JudgePopup(JudgeResult result, int lane, float size)
        {
            if (result.Rating != Rating.Perfect)
            {
                string side = result.ErrorMs < 0 ? "<color=#7FE3FF>FAST</color>" : "<color=#FFB347>SLOW</color>";
                Popup("GOOD\n<size=34>" + side + "</size>", UiKit.Cyan, lane, size);
                Effect good = lanePopup[lane];
                if (good != null) { good.ScaleFrom = 1.1f; good.ScaleTo = .9f; }
                return;
            }
            int streak = perfectStreak;
            Color c = streak >= 20 && Effects != EffectLevel.Weak ? Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * .6f, 1f), .45f, 1f) : UiKit.Gold;
            string text = streak >= 3 ? "PERFECT!\n<size=40>×" + streak + "</size>" : "PERFECT!";
            Popup(text, c, lane, size);
            Effect fx = lanePopup[lane];
            if (fx != null) { fx.ScaleFrom = 1.9f; fx.ScaleTo = 1.15f; fx.Life = .55f; }
            if (Effects == EffectLevel.Weak || fx == null) return;
            Vector2 at = fx.Rect.anchoredPosition;
            Spawn(ArtLibrary.Ring, at, new Vector2(Unit * .3f, Unit * .12f), Vector2.zero, 0, .22f, .6f, 1.6f, new Color(c.r, c.g, c.b, .7f), 0);
            Sprite spark = ArtLibrary.Get("vfx_sparkle");
            if (spark != null)
            {
                Spawn(spark, at + new Vector2(-Unit * .17f, Unit * .03f), Vector2.one * Unit * .09f, Vector2.zero, 160f, .3f, .4f, 1.1f, Color.white, 0);
                Spawn(spark, at + new Vector2(Unit * .17f, -Unit * .02f), Vector2.one * Unit * .07f, Vector2.zero, -160f, .3f, .4f, 1.1f, Color.white, 0);
            }
        }

        void Popup(string text, Color color, int lane, float size)
        {
            // one judgement text per lane: a new result replaces the previous one instead of stacking
            Effect old = lanePopup[lane];
            if (old != null && effects.Contains(old)) { effects.Remove(old); Recycle(old); }
            Effect fx = freeTexts.Count > 0 ? freeTexts.Pop() : CreateText();
            lanePopup[lane] = fx;
            fx.Rect.gameObject.SetActive(true);
            // Below the slash line in the lane: incoming objects never travel there. The centre lane shifts aside so the
            // text never sits on the hamster's head (review/round0 character #5).
            Vector2 below = Local(Perspective.X(Perspective.LaneLateral(lane), 1f), Perspective.SlashY + .045f);
            if (lane == 1) below.x += Unit * .28f;
            // clamp by the text box half-width (+ outline) so long words never leave the screen
            float half = fx.Rect.sizeDelta.x * .5f * 1.35f + 8f;
            fx.Rect.anchoredPosition = new Vector2(Mathf.Clamp(below.x, root.rect.xMin + half, root.rect.xMax - half), below.y);
            fx.Text.text = text; fx.Text.color = color; fx.Color = color;
            fx.Born = Time.unscaledTime; fx.Life = .45f; fx.Hold = 0; fx.Velocity = new Vector2(0, -Unit * .05f); fx.ScaleFrom = 1.35f; fx.ScaleTo = 1f; fx.Spin = 0; fx.Gravity = 0; fx.Rise = true;
            effects.Add(fx);
        }

        void KoPopup(Vector2 at, string text = "K.O.!!")
        {
            Effect fx = freeTexts.Count > 0 ? freeTexts.Pop() : CreateText();
            fx.Rect.gameObject.SetActive(true);
            fx.Rect.anchoredPosition = at;
            fx.Text.text = text; fx.Text.color = UiKit.Gold; fx.Color = UiKit.Gold;
            fx.Born = Time.unscaledTime; fx.Life = 1.4f; fx.Hold = 0; fx.Velocity = Vector2.zero; fx.ScaleFrom = 4f; fx.ScaleTo = 2.4f; fx.Spin = 0; fx.Gravity = 0; fx.Rise = true;
            effects.Add(fx);
        }

        void MashPopup(string text, Color color)
        {
            Effect fx = freeTexts.Count > 0 ? freeTexts.Pop() : CreateText();
            fx.Rect.gameObject.SetActive(true);
            fx.Rect.anchoredPosition = Local(.5f, Perspective.SlashY - .1f);
            fx.Text.text = text; fx.Text.color = color; fx.Color = color;
            fx.Born = Time.unscaledTime; fx.Life = .8f; fx.Hold = 0; fx.Velocity = new Vector2(0, Unit * .05f); fx.ScaleFrom = 2f; fx.ScaleTo = 1.3f; fx.Spin = 0; fx.Gravity = 0; fx.Rise = true;
            effects.Add(fx);
        }

        Effect CreateText()
        {
            RectTransform rect = UiKit.Rect("Popup", popupLayer, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            rect.sizeDelta = new Vector2(.4f * Unit, .1f * Unit);
            Text text = UiKit.Label("", rect, 72, Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false);
            text.verticalOverflow = VerticalWrapMode.Overflow; text.supportRichText = true; // two-line PERFECT ×n / GOOD FAST
            UiKit.AddOutline(text.gameObject, new Color(.03f, .06f, .14f, .9f), 3);
            return new Effect { Rect = rect, Text = text };
        }

        void SpawnSlice(Sprite sprite, Vector2 pos, Vector2 size, Vector2 velocity, float spin, bool mirror = false)
        {
            Effect fx = freeSlices.Count > 0 ? freeSlices.Pop() : new Effect { Image = NewSolid("Slice", fxLayer, new Vector2(.5f, .5f), new Vector2(.5f, .5f)), Slice = true };
            // less flat spinning, more turning in depth: the piece shows its edge and thickness as it tumbles away
            Setup(fx, sprite, pos, size, velocity, spin * .45f, .45f, 1f, .92f, stageLight, Unit * 2.2f, 0);
            fx.Hold = .12f; // cut face stays square to the camera before tumbling away
            fx.Mirror = mirror;
            fx.Tumble = (spin >= 0 ? 1f : -1f) * Random.Range(7f, 10f);
            fx.Solid = new Vector2(0, size.y * .12f); // the back face lies up-screen, away from the camera
            fx.Rect.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
            ((SolidImage)fx.Image).SetDepth(fx.Solid);
        }

        Effect Spawn(Sprite sprite, Vector2 pos, Vector2 size, Vector2 velocity, float spin, float life, float scaleFrom, float scaleTo, Color color, float gravity, float rotation = 0)
        {
            Effect fx = freeEffects.Count > 0 ? freeEffects.Pop() : new Effect { Image = UiKit.Image("Fx", fxLayer, null, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), true) };
            Setup(fx, sprite, pos, size, velocity, spin, life, scaleFrom, scaleTo, color, gravity, rotation);
            return fx;
        }

        void Setup(Effect fx, Sprite sprite, Vector2 pos, Vector2 size, Vector2 velocity, float spin, float life, float scaleFrom, float scaleTo, Color color, float gravity, float rotation)
        {
            fx.Rect = fx.Image.rectTransform;
            fx.Rect.gameObject.SetActive(true);
            fx.Image.sprite = sprite; fx.Image.color = color; fx.Color = color;
            fx.Rect.sizeDelta = size; fx.Rect.anchoredPosition = pos; fx.Rect.localRotation = Quaternion.Euler(0, 0, rotation);
            fx.Born = Time.unscaledTime; fx.Life = life; fx.Hold = 0; fx.Velocity = velocity; fx.Spin = spin; fx.ScaleFrom = scaleFrom; fx.ScaleTo = scaleTo; fx.Gravity = gravity; fx.Rise = false;
            effects.Add(fx);
        }

        void UpdateEffects()
        {
            float now = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Effect fx = effects[i];
                float age = now - fx.Born;
                float t = age / fx.Life;
                if (t >= 1f) { Recycle(fx); effects.RemoveAt(i); continue; }
                if (age >= fx.Hold)
                {
                    fx.Velocity.y -= fx.Gravity * dt;
                    fx.Rect.anchoredPosition += fx.Velocity * dt;
                    if (fx.Spin != 0) fx.Rect.localRotation *= Quaternion.Euler(0, 0, fx.Spin * dt);
                }
                float s = Mathf.Lerp(fx.ScaleFrom, fx.ScaleTo, fx.Rise ? Mathf.Min(1, t * 5f) : t);
                fx.Rect.localScale = new Vector3(s, s, 1);
                if (fx.Slice)
                {
                    // turning about its own upright axis: it narrows to its edge and back (never showing a mirrored face),
                    // and its thickness swings to the side it turns toward
                    float turn = age > fx.Hold ? (age - fx.Hold) * fx.Tumble : 0f;
                    float facing = Mathf.Lerp(1f, .3f, (1f - Mathf.Cos(turn)) * .5f);
                    fx.Rect.localScale = new Vector3(s * facing * (fx.Mirror ? -1f : 1f), s, 1);
                    Vector2 size = fx.Rect.sizeDelta;
                    ((SolidImage)fx.Image).SetDepth(new Vector2(Mathf.Sin(turn) * size.x * .3f, size.y * .12f) * s);
                }
                float alpha = fx.Rise ? (t < .7f ? 1f : 1f - (t - .7f) / .3f) : 1f - t * t;
                if (fx.Text != null) fx.Text.canvasRenderer.SetAlpha(alpha); else fx.Image.canvasRenderer.SetAlpha(alpha);
            }
        }

        void Recycle(Effect fx)
        {
            fx.Rect.gameObject.SetActive(false);
            fx.Rect.localScale = Vector3.one; fx.Rect.localRotation = Quaternion.identity;
            if (fx.Text != null) fx.Text.canvasRenderer.SetAlpha(1); else fx.Image.canvasRenderer.SetAlpha(1);
            if (fx.Text != null) freeTexts.Push(fx); else if (fx.Slice) freeSlices.Push(fx); else freeEffects.Push(fx);
        }

        // ------------------------------------------------------------------ hero

        Pose ChoosePose(double beat, float now)
        {
            if (now < hitUntil) return Make("hamster_hit", 40f);
            if (holdingLane >= 0)
            {
                string held = ArtLibrary.Has(LaneSlash[holdingLane]) ? LaneSlash[holdingLane] : LaneSlashFallback[holdingLane];
                return Make(ArtLibrary.Has(held) ? held : "hamster_slash", 0f);
            }
            if (now < slashUntil)
            {
                string lane = ArtLibrary.Has(LaneSlash[slashLane]) ? LaneSlash[slashLane] : LaneSlashFallback[slashLane];
                return Make(ArtLibrary.Has(lane) ? lane : "hamster_slash", 0f);
            }
            if (now < missUntil) return Make("hamster_miss", -10f);
            if (now < comboUntil && ArtLibrary.Has("hamster_combo_pose")) return Make("hamster_combo_pose", 15f);
            if (ready && ArtLibrary.Has("hamster_back_ready")) return Make("hamster_back_ready", weaponVisual.RestAngle);
            if (fever && ArtLibrary.Has("hamster_fever_dash_a"))
                return Make(((int)System.Math.Floor(beat * 2) & 1) == 0 ? "hamster_fever_dash_a" : "hamster_fever_dash_b", weaponVisual.RestAngle - 10f);
            // four-frame run, one cycle per beat: contact A → passing C → contact B → airborne D
            int frame = ((int)System.Math.Floor(beat * 4) % 4 + 4) % 4;
            string[] cycle = ArtLibrary.Has("hamster_run_c") && ArtLibrary.Has("hamster_run_d")
                ? new[] { "hamster_run_a", "hamster_run_c", "hamster_run_b", "hamster_run_d" }
                : new[] { "hamster_run_a", "hamster_run_a", "hamster_run_b", "hamster_run_b" };
            return Make(cycle[frame], weaponVisual.RestAngle + (frame == 2 ? 6f : 0f) + 4f * Mathf.Sin((float)beat * Mathf.PI * 2));
        }

        void UpdatePowerupVisuals(float now, Vector2 foot0, float height, float lean)
        {
            // rocket on the back while boosting, with a flickering flame
            rocket.enabled = boostOn && rocket.sprite != null;
            rocketFlame.enabled = rocket.enabled && Effects != EffectLevel.Weak;
            if (rocketFlame.enabled)
            {
                float f = .8f + .3f * Mathf.PerlinNoise(now * 25f, 0f);
                rocketFlame.rectTransform.localScale = new Vector3(.9f + .2f * Mathf.Sin(now * 40f), f, 1);
                if (Random.value < .5f) Spawn(ArtLibrary.Glow, hero.anchoredPosition + new Vector2(lean * 0 + Random.Range(-.02f, .02f) * Unit, height * .3f), new Vector2(.05f, .05f) * Unit, new Vector2(Random.Range(-.05f, .05f), -.5f) * Unit, 0, .25f, 1f, .3f, new Color(1f, .55f, .15f, .8f), 0);
            }
            // barrier bubble around the hero while a shield is up, popping when it takes a MISS
            float pop = Mathf.Clamp01((now - shieldPopAt) / .35f);
            barrierBubble.enabled = barrierBubble.sprite != null && (shields > 0 || pop < 1f);
            if (barrierBubble.enabled)
            {
                RectTransform b = barrierBubble.rectTransform;
                float s = shields > 0 ? 1f + .03f * Mathf.Sin(now * 3f) : 1f + .5f * pop;
                b.pivot = new Vector2(.5f, 0f); // the sprite is a dome standing on the road
                b.sizeDelta = new Vector2(height * 1.6f, height * 1.6f) * s;
                b.anchoredPosition = foot0 + new Vector2(lean, -height * .03f);
                barrierBubble.color = new Color(.6f, .95f, 1f, shields > 0 ? .3f : .6f * (1f - pop)); // faint while up: never hides the hero
            }
            // charge-attack guard: a dome in front of the hero while the hold is kept
            guardDome.enabled = chargeGuard && guardDome.sprite != null;
            if (guardDome.enabled)
            {
                RectTransform g = guardDome.rectTransform;
                g.pivot = new Vector2(.5f, 0f);
                g.sizeDelta = new Vector2(height * 1.9f, height * 1.9f) * (1f + .04f * Mathf.Sin(now * 30f));
                g.anchoredPosition = foot0 + new Vector2(lean, height * .05f);
            }
        }

        static Pose Make(string sprite, float angle)
        {
            Vector2 hand;
            if (!Hands.TryGetValue(sprite, out hand)) hand = new Vector2(.9f, .5f);
            return new Pose { Sprite = sprite, Hand = hand, Angle = angle };
        }

        void UpdateHero(double beat)
        {
            float now = Time.unscaledTime;
            Pose pose = ChoosePose(beat, now);
            Sprite sprite = ArtLibrary.Get(pose.Sprite) ?? ArtLibrary.Get("hamster_run_a");
            if (sprite != heroImage.sprite) heroImage.sprite = sprite ?? ArtLibrary.Circle;
            if (sprite == null) heroImage.color = UiKit.Honey;

            // footfalls on every half beat: squash, shadow and dust land exactly on the beat grid
            int footfall = (int)System.Math.Floor(beat * 2);
            if (footfall != lastFootfall && beat >= 0 && now >= slashUntil && now >= hitUntil)
            {
                lastFootfall = footfall;
                squashAt = now;
                if (Effects != EffectLevel.Weak && !ready)
                {
                    Sprite dust = ArtLibrary.Get("vfx_dust_puff");
                    Vector2 foot = Local(.5f + ((footfall & 1) == 0 ? -.05f : .05f), Perspective.CharacterY + .11f);
                    if (dust != null) Spawn(dust, foot, new Vector2(.12f * Unit, .08f * Unit), new Vector2(0, Unit * .04f), 0, .35f, .6f, 1.2f, new Color(1, 1, 1, .7f), 0);
                }
            }
            float squash = Mathf.Clamp01(1f - (now - squashAt) / .08f);
            float airborne = Mathf.Abs(Mathf.Sin((float)(beat * System.Math.PI * 2)));
            float sx = 1f + .05f * squash - .015f * airborne, sy = 1f - .06f * squash + .03f * airborne;
            float hitShake = now < hitUntil ? Mathf.Sin((now - (hitUntil - HitPose)) * 70f) * 6f * (hitUntil - now) / HitPose : 0;

            // round 27 giant: grows with a bounce, wobbles smaller on its last beats, pops back to size
            float sinceChange = now - giantChangedAt;
            float giantTarget = giantOn ? (giantLeft < 3 ? 1.55f - .08f * (float)(3 - giantLeft) : 1.65f) : 1f;
            giantScale = giantOn ? Mathf.Lerp(1f, giantTarget, Anim.OutBack(Mathf.Clamp01(sinceChange / .35f))) : Mathf.Lerp(giantScale, 1f, Mathf.Clamp01(sinceChange / .2f));
            float height = .36f * Unit * giantScale;
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 1f;
            float bob = airborne * .012f * Unit;
            float leanT = Mathf.Clamp01((now - leanAt) / .31f);
            // ease out to the tapped lane in 60 ms, ease back over 250 ms (OutBack)
            float leanAmount = leanT < .2f ? Anim.OutCubic(leanT / .2f) : 1f - Anim.OutBack((leanT - .2f) / .8f);
            float lean = Mathf.Clamp(leanAmount, -.2f, 1.1f) * (leanLane - 1) * .12f * Unit;
            float knock = now < hitUntil ? -.03f * Unit * (hitUntil - now) / HitPose : 0;
            Vector2 foot0 = Local(.5f, Perspective.CharacterY + .115f) + new Vector2(0, rushDash * .09f * Unit); // rush: dash forward
            hero.anchorMin = hero.anchorMax = new Vector2(.5f, .5f);
            hero.pivot = new Vector2(.5f, 0);
            hero.sizeDelta = new Vector2(height * aspect, height);
            hero.anchoredPosition = foot0 + new Vector2(lean + hitShake, bob + knock);
            hero.localScale = new Vector3(sx, sy, 1);
            bool hurtTint = now < hitUntil && now < hitUntil - HitPose + .1f; // 0.1 s reddish tint on the character only
            heroImage.color = sprite == null ? UiKit.Honey : hurtTint ? new Color(1f, .6f, .55f) : Color.white;
            // the giant's last three beats: the hero blinks and the beats left are counted over its head
            bool ending = giantOn && giantLeft < 3;
            if (ending && sprite != null && Mathf.Repeat((float)giantLeft * 2f, 1f) > .5f) heroImage.color = new Color(1f, .75f, .55f);
            giantCount.enabled = ending;
            if (ending)
            {
                int n = Mathf.Clamp((int)System.Math.Ceiling(giantLeft), 1, 3);
                giantCount.text = n.ToString();
                float punch = Anim.Punch(1f - Mathf.Repeat((float)giantLeft, 1f));
                giantCount.rectTransform.anchoredPosition = foot0 + new Vector2(lean, height * 1.35f);
                giantCount.rectTransform.localScale = Vector3.one * (1f + .5f * punch);
                giantCount.color = n == 1 ? UiKit.Coral : UiKit.Gold;
            }

            heroShadow.rectTransform.anchorMin = heroShadow.rectTransform.anchorMax = new Vector2(.5f, .5f);
            heroShadow.rectTransform.sizeDelta = new Vector2(height * .75f * (1f - .15f * airborne), height * .16f * (1f - .15f * airborne));
            heroShadow.rectTransform.anchoredPosition = foot0 + new Vector2(lean, height * .02f);
            heroShadow.canvasRenderer.SetAlpha(1f - .3f * airborne);

            if (feverAura.enabled)
            {
                RectTransform a = feverAura.rectTransform;
                a.anchorMin = a.anchorMax = new Vector2(.5f, .5f);
                float pulse = 1f + .06f * Mathf.Sin(now * Mathf.PI * 2 * .75f); // slow 0.75 Hz breathing, scale only
                a.sizeDelta = new Vector2(height * 1.6f, height * 1.6f) * pulse;
                a.anchoredPosition = foot0 + new Vector2(lean, height * .5f);
                feverAura.color = new Color(1f, .85f, .45f, .55f * feverBlend);
            }

            UpdatePowerupVisuals(now, foot0, height, lean);
            // rush: afterimages trail the hero, the hit counter punches on every cut
            if (boostOn && Effects != EffectLevel.Weak && now - lastGhostAt > .045f && heroImage.sprite != null)
            {
                lastGhostAt = now;
                Effect ghost = Spawn(heroImage.sprite, hero.anchoredPosition + new Vector2(0, height * .5f), new Vector2(height * aspect, height), new Vector2(0, -Unit * .5f), 0, .22f, 1f, .96f, new Color(.5f, .95f, 1f, .45f), 0);
                ghost.Rect.SetSiblingIndex(0);
            }
            float rc = Mathf.Clamp01((now - rushCounterAt) / .18f);
            rushCounter.enabled = rushCounter.text.Length > 0 && now - rushCounterAt < 1.2f;
            if (rushCounter.enabled)
            {
                rushCounter.rectTransform.anchoredPosition = Local(.5f, Perspective.HorizonY + .1f);
                rushCounter.rectTransform.localScale = Vector3.one * (1f + .5f * Anim.Punch(rc));
                rushCounter.color = Color.Lerp(UiKit.Gold, Color.white, Anim.Punch(rc));
            }

            // Weapon: shared grip pivot, angle table per lane while slashing, afterimages trail the swing.
            if (weapon.sprite == null) return;
            bool slashing = now < slashUntil;
            float wHeight = height * .62f * weaponVisual.Scale;
            float wAspect = weapon.sprite.rect.width / weapon.sprite.rect.height;
            RectTransform w = weapon.rectTransform;
            w.anchorMin = w.anchorMax = pose.Hand;
            w.pivot = new Vector2(.5f, weaponVisual.PivotY);
            w.sizeDelta = new Vector2(wHeight * wAspect, wHeight);
            w.anchoredPosition = Vector2.zero;
            float angle = pose.Angle;
            if (holdingLane >= 0 && !slashing)
            {
                // blade pressed into the held object, trembling with effort
                angle = SwingTo[holdingLane] * .6f + 3f * Mathf.Sin(now * 45f);
            }
            else if (slashing)
            {
                // render-only hitstop: the character's swing holds 45 ms on PERFECT; judgement and clocks keep running
                float swingStart = slashUntil - SlashPose;
                float t = now < hitStopUntil ? 0f : Mathf.Clamp01((now - Mathf.Max(swingStart, hitStopUntil)) / (slashUntil - Mathf.Max(swingStart, hitStopUntil)));
                angle = Mathf.Lerp(SwingFrom[slashLane], SwingTo[slashLane], Anim.OutCubic(Mathf.Clamp01(t * 1.3f)));
                slashEndAngle = angle;
            }
            else if (now < slashUntil + .09f && slashUntil > 0)
            {
                // follow-through: ease back to the rest angle over 90 ms instead of snapping
                angle = Mathf.Lerp(slashEndAngle, pose.Angle, Anim.OutCubic((now - slashUntil) / .09f));
            }
            w.localRotation = Quaternion.Euler(0, 0, angle);
            for (int i = 0; i < ghosts.Length; i++)
            {
                bool show = slashing && Effects != EffectLevel.Weak;
                ghosts[i].enabled = show;
                if (!show) { ghostAngles[i] = angle; continue; }
                RectTransform g = ghosts[i].rectTransform;
                g.anchorMin = g.anchorMax = pose.Hand; g.pivot = w.pivot; g.sizeDelta = w.sizeDelta; g.anchoredPosition = Vector2.zero;
                float lag = (i + 1) * .028f / SlashPose;
                float tg = Mathf.Clamp01(1f - (slashUntil - now) / SlashPose - lag);
                float ga = Mathf.Lerp(SwingFrom[slashLane], SwingTo[slashLane], Anim.OutCubic(Mathf.Clamp01(tg * 1.3f)));
                g.localRotation = Quaternion.Euler(0, 0, ga);
                ghosts[i].color = new Color(weaponVisual.Trail.r, weaponVisual.Trail.g, weaponVisual.Trail.b, i == 0 ? .45f : i == 1 ? .25f : .12f);
            }
        }
    }
}
