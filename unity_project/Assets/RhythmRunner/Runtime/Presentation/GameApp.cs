using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using RhythmSlashRunner.Core;
using RhythmSlashRunner.Persistence;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Boot → Home → CountIn → Running ⇄ Paused/Resuming → Ending → Result. Everything is generated from code so an
    /// empty scene is enough; the music DSP clock is the only clock used for timing. Screens live in partial files:
    /// GameApp.Menus (home, character, stage, settings, pause, dialogs), GameApp.Shop (weapons + purchase), GameApp.Result.
    /// </summary>
    public sealed partial class GameApp : MonoBehaviour
    {
        enum State { Boot, Menu, CountIn, Running, Paused, Resuming, Ending, Result }

        const double StallSeconds = .5;
        const int ResumeBeats = 3;
        const double CueLookahead = .75; // 1.5 beats: cues survive a frame hitch (review/round0 sound P0)

        State state = State.Boot;
        RunConfig config;
        PatternDefinition[] patterns;
        ContentCatalog catalog;
        PlayerProfile profile;
        RunEngine engine;
        string runId;
        int? forcedTier;

        AudioDirector audioDirector;
        InputRouter input;
        RectTransform canvasRect, safeRect;
        PlayfieldView playfield;
        HudView hud;
        FxDirector fx;
        GameObject splash, home, characterScreen, weaponScreen, settingsScreen, licenseScreen, game, pauseScreen, resultScreen, dialog, notice;
        GameObject currentScreen;
        readonly List<Action> refreshers = new List<Action>();

        double musicStartDsp, pausedBeat, resumeMusicDsp, scheduledUpTo, lastFrameDsp, endedAt;
        readonly RunSnapshot liveSnapshot = new RunSnapshot();
        RunSnapshot finalSnapshot;
        int finalReward, finalBestBonus, previousBestDistance;
        bool finalNewRecord, rewardSaved, bootFinished;
        bool teaseScheduled;
        string lastDiagnostic = "-";
        readonly List<ScheduledEvent> autoplayDue = new List<ScheduledEvent>();
        readonly List<double> teaseBeats = new List<double>(16);
        /// <summary>What was cut this run, by drawn object kind (result card).</summary>
        readonly Dictionary<string, int> sliced = new Dictionary<string, int>();
        double lastHoldHalf = double.NaN, lastAutoMashBeat = -99;
        int warnedStage = -1, speedUpScheduledFor = -1, holdTicks, countdownStage = -1, countShown;
        bool lastRage, lastRush;
        float lastWhooshAt = -9;
        readonly HashSet<string> chargeWarned = new HashSet<string>();
        readonly Vector3[] corners = new Vector3[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<GameApp>() == null) new GameObject("RhythmSlashRunner").AddComponent<GameApp>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Haptics.EnsurePermissionReference();
            // automated captures play on a throw-away save, never on the player's own coins and progress
            if (Automation.Enabled) { ProfileStore.Directory = System.IO.Path.Combine(Application.persistentDataPath, "automation"); try { System.IO.Directory.Delete(ProfileStore.Directory, true); } catch (Exception) { } }
            profile = ProfileStore.Load();
            L10n.Language = Automation.Lang ?? (string.IsNullOrEmpty(profile.language) ? L10n.Auto() : profile.language); // round 34
            if (Automation.Energy.HasValue)
            {
                profile.firstPlayTicks = DateTime.UtcNow.AddDays(-10).Ticks; profile.energy = Automation.Energy.Value;
                profile.energyUpdatedTicks = DateTime.UtcNow.AddMinutes(-3).Ticks; profile.drinks = 2;
            }
            if (Automation.Character != null) { profile.ownedIds.Add(Automation.Character); profile.equippedCharacter = Automation.Character; }
            if (Automation.Weapon != null) { profile.ownedIds.Add(Automation.Weapon); profile.equippedWeapon = Automation.Weapon; }
            ArtLibrary.CharacterPrefix = ArtLibrary.PrefixFor(profile.Owns(profile.equippedCharacter) ? profile.equippedCharacter : "character_hamster");
            audioDirector = gameObject.AddComponent<AudioDirector>();
            audioDirector.Initialize(profile.musicVolume, profile.sfxVolume, profile.cueVolume);
            UiKit.OnButtonSound = primary =>
            {
                audioDirector.Play(primary ? Sfx.UiConfirm : Sfx.UiSelect);
                if (profile.vibration) Haptics.Play(Haptics.Strength.Light);
                if (fx.ScreenShake && profile.Effects != EffectLevel.Weak && playfield.Boss.LastHitDamage >= 5) hud.Shake(5f); // a heavy hit lands
            };
            input = gameObject.AddComponent<InputRouter>();
            input.BackRequested = OnBack;
            BuildCanvas();
            LoadData();
            BuildSplash();
            BuildHome();
            BuildCharacter();
            BuildWeapon();
            BuildItemShop();
            BuildSpecialShop();
            BuildRanking();
            BuildEvent();
            BuildSettings();
            BuildLicense();
            BuildGame();
            BuildPause();
            BuildTip();
            BuildResult();
            BuildDialog();
            BuildNotice();
            PrewarmFont();
            Show(splash);
            if (Automation.Enabled) { Application.runInBackground = true; StartCoroutine(Automation.Run(this)); }
        }

        void FinishBoot()
        {
            bootFinished = true;
            if (patterns == null) { ShowLoadError(); return; } // invalid data refuses the run
            audioDirector.Play(Sfx.LoadDone);
            state = State.Menu;
            ShowMenu(home);
            audioDirector.PlayMenuMusic();
            if (!profile.photosensitivityNoticeShown && !Automation.Enabled) ShowNotice();
            Ranking.SignIn();
            if (!Automation.Enabled) InitStore();
            Analytics.HookErrors();
            if (profile.analyticsConsent == 1) Analytics.Start();
            Analytics.Event("app_start", "runs", profile.runsPlayed, "coins", profile.coinBalance);
            CheckLoginBonus();
        }

        /// <summary>Bakes the glyphs the run uses so the first FEVER does not hitch on a dynamic-font atlas rebuild.</summary>
        void PrewarmFont()
        {
            const string glyphs = "0123456789,m×xX!+-/.%: COMBOFEVERLvNEWRECORDPERFECTGOODMISSENDSCORE★♪ 裏拍がくるよ拍と間ボーナス約枚コインスコア倍率距離最大ベスト更新あと連打でとどめ！SPEDUPTAGWARNINGBS撃破逃げられた…次のボスで取り返そうあらわれたCMPLTKH粉砕たまねぎ大王トースター魔人おもちゃ部屋空中果樹園ネオン市場おかしの国こおりの冷凍庫巨大キッチン♥RUSH前方をぜんぶ+長押しそのままはなせ怒りモード本気を出したためこうげきでガード→はね返せカウンターCRITICALオートカッター8拍のあいだ自動で斬るバリアミス1回を防ぐGET";
            foreach (Font f in new[] { UiKit.Font, UiKit.Heavy })
                foreach (int size in new[] { 40, 46, 52, 72, 80, 92, 150 })
                {
                    f.RequestCharactersInTexture(glyphs, size, FontStyle.Normal);
                    f.RequestCharactersInTexture(glyphs, size, FontStyle.Italic);
                }
        }

        // ------------------------------------------------------------------ data

        void LoadData()
        {
            try
            {
                config = RunConfig.FromFile(JsonUtility.FromJson<GameConfigFile>(Resources.Load<TextAsset>("game_config").text));
                patterns = JsonUtility.FromJson<PatternFile>(Resources.Load<TextAsset>("patterns").text).patterns;
                if (!string.IsNullOrEmpty(Automation.ForcedPatterns))
                    patterns = Array.FindAll(patterns, p => !string.IsNullOrEmpty(p.role) || p.id.StartsWith(Automation.ForcedPatterns) || p.id == config.FallbackPatternId);
                if (!string.IsNullOrEmpty(Automation.ForcedSoldier))
                    patterns = Array.FindAll(patterns, p => p.role != RunEngine.RoleSoldier || Array.Exists(p.events, e => e.objectId == RunEngine.SoldierPrefix + Automation.ForcedSoldier));
                catalog = JsonUtility.FromJson<ContentCatalog>(Resources.Load<TextAsset>("content_catalog").text);
                if (catalog.stages != null && catalog.stages.Length > 0) config.ThemeCount = catalog.stages.Length;
                new RunEngine(patterns, 1, config); // validates every pattern up front
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                patterns = null;
                config = config ?? RunConfig.Default;
            }
        }

        // ------------------------------------------------------------------ canvas & screen switching

        void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                DontDestroyOnLoad(es);
            }
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRect = (RectTransform)canvasGo.transform;
            // Letterbox to 9:16 on wide screens (PC window, tablets) so the playfield proportions never change.
            var frame = UiKit.Rect("Frame", canvasRect, Vector2.zero, Vector2.one);
            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 9f / 16f;
            // letterbox bars are painted by the clear camera behind the 3D play screen (a UI image here would cover the 3D view)
            canvasRect = frame;
            safeRect = UiKit.Rect("SafeArea", frame, Vector2.zero, Vector2.one);
            safeRect.gameObject.AddComponent<SafeAreaFitter>();
        }

        GameObject MakeScreen(string name, bool fullBleed = false)
        {
            RectTransform rect = UiKit.Rect(name, fullBleed ? canvasRect : safeRect, Vector2.zero, Vector2.one);
            rect.gameObject.AddComponent<CanvasGroup>();
            return rect.gameObject;
        }

        Image Background(GameObject screen, string sprite, Color tint)
        {
            RectTransform holder = UiKit.Rect("Bg", screen.transform, Vector2.zero, Vector2.one);
            holder.gameObject.AddComponent<RectMask2D>();
            Image bg = UiKit.Image("Image", holder, ArtLibrary.Get(sprite), tint, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            if (bg.sprite == null) { bg.color = UiKit.Navy; UiKit.Stretch(bg.rectTransform); }
            else
            {
                // layout-driven cover: correct from the very first frame (review: splash showed a small centred picture)
                UiKit.Stretch(bg.rectTransform);
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = bg.sprite.rect.width / bg.sprite.rect.height;
            }
            return bg;
        }

        void ShowMenu(GameObject target)
        {
            RefreshAll();
            Show(target);
        }

        /// <summary>Switches screens with a short fade + slide; input is blocked until the new screen settles.</summary>
        void Show(GameObject target)
        {
            bool overlay = target == pauseScreen || target == tipScreen;
            // owner round 16: settings opened from the pause hide the game (it used to draw over the settings: the hero showed through)
            foreach (GameObject s in new[] { splash, home, characterScreen, weaponScreen, itemScreen, specialScreen, rankingScreen, eventScreen, settingsScreen, licenseScreen, game, pauseScreen, resultScreen, tipScreen })
                if (s != null) s.SetActive(s == target || (s == game && overlay));
            if (dialog != null) dialog.SetActive(false);
            if (target == settingsScreen && settingsReturnToPause) pauseScreen.SetActive(false);
            GameObject previous = currentScreen;
            currentScreen = target;
            if (target == null || target == game || target == previous) return;
            var group = target.GetComponent<CanvasGroup>();
            var rect = (RectTransform)target.transform;
            if (group == null) return;
            bool weak = profile.Effects == EffectLevel.Weak;
            group.blocksRaycasts = false;
            Anim.Run(weak ? .12f : .2f, t =>
            {
                group.alpha = Anim.OutCubic(t);
                rect.anchoredPosition = new Vector2(weak ? 0 : Mathf.Lerp(48f, 0f, Anim.OutCubic(t)), 0);
            }, () => { group.blocksRaycasts = true; group.alpha = 1; rect.anchoredPosition = Vector2.zero; }, group);
        }

        void RefreshAll() { foreach (Action refresh in refreshers) refresh(); }

        /// <summary>Android back / Esc: close dialog → leave sub screen → pause/resume → confirm exit on Home.</summary>
        void OnBack()
        {
            if (dialog != null && dialog.activeSelf) { CloseDialog(); return; }
            if (tipOpen) { DismissTip(); return; }
            if (notice != null && notice.activeSelf) return;
            switch (state)
            {
                case State.Running: case State.CountIn: case State.Resuming: Pause(CurrentBeat()); return;
                case State.Paused: Resume(); return;
                case State.Result:
                    if (cardOverlay != null && cardOverlay.activeSelf) { audioDirector.Play(Sfx.UiBack); cardOverlay.SetActive(false); return; }
                    QuitToHome(); return;
                case State.Ending: case State.Boot: return;
            }
            if (currentScreen == settingsScreen) { LeaveSettings(); return; }
            if (currentScreen == licenseScreen) { audioDirector.Play(Sfx.UiBack); ShowMenu(settingsScreen); return; }
            if (currentScreen != home) { audioDirector.Play(Sfx.UiBack); ShowMenu(home); return; }
            ShowDialog("アプリを終了しますか？", null, "終了する", Application.Quit, "キャンセル", true);
        }

        // ------------------------------------------------------------------ game screen

        void BuildGame()
        {
            game = MakeScreen("Game", true);
            playfield = game.AddComponent<PlayfieldView>();
            playfield.Build((RectTransform)game.transform, config.SpawnLeadBeats);
            playfield.SetWeapon(profile.equippedWeapon);
            if (catalog != null) playfield.SetStages(catalog.stages);
            playfield.Boss.Landed += () =>
            {
                fx.BossArrive(playfield.BossIdAt(engine, CurrentBeat() + 4));
                audioDirector.Play(Sfx.Critical);
                audioDirector.Play(Sfx.BossGrunt, 1f, .7f);
                if (profile.vibration) Haptics.Play(Haptics.Strength.Success);
            };
            playfield.PostPassed += () =>
            {
                // one whoosh per few posts: the ear hears speed without a machine-gun of noise
                if (Time.unscaledTime - lastWhooshAt < .2f) return;
                lastWhooshAt = Time.unscaledTime;
                audioDirector.Play(Sfx.PassWhoosh, .8f, .9f + UnityEngine.Random.Range(0f, .3f) + .1f * Mathf.Clamp01(playfield.WorldSpeed / 6f));
            };
            playfield.Boss.CometHit += () =>
            {
                audioDirector.Play(Sfx.BossHit, .8f, 1f + UnityEngine.Random.Range(-.05f, .05f));
                if (UnityEngine.Random.value < .5f) audioDirector.Play(Sfx.BossGrunt, .9f, .9f + UnityEngine.Random.Range(0f, .25f)); // the boss feels it
                if (profile.vibration) Haptics.Play(Haptics.Strength.Light);
                if (fx.ScreenShake && profile.Effects != EffectLevel.Weak && playfield.Boss.LastHitDamage >= 5) hud.Shake(5f); // a heavy hit lands
            };
            RectTransform hudArea = UiKit.Rect("HudSafe", game.transform, Vector2.zero, Vector2.one);
            hudArea.gameObject.AddComponent<SafeAreaFitter>();
            hud = new HudView();
            hud.Build(hudArea, () => Pause(CurrentBeat()));
            fx = game.AddComponent<FxDirector>();
            fx.Build((RectTransform)game.transform, hudArea, hud, playfield);
            hud.Root.SetAsLastSibling();
            fx.Milestone += OnMilestone;
            fx.FeverEntered += OnFeverEntered;
            fx.StageCleared += stage => { audioDirector.Play(Sfx.Fanfare, .85f); if (profile.vibration) Haptics.Play(Haptics.Strength.Success); };
            fx.FeverEnded += OnFeverEnded;
            fx.CoinLanded += () => audioDirector.Play(Sfx.Coin, 1f, 1f + UnityEngine.Random.Range(-.012f, .012f));
            fx.NewRecordPassed += () => { audioDirector.Play(Sfx.NewRecord); audioDirector.Duck(-6, .6); if (profile.vibration) Haptics.Play(Haptics.Strength.Success); };
        }

        // ------------------------------------------------------------------ run flow

        /// <summary>Seconds per beat at the start of the run (count-in).</summary>
        double Spb { get { return config.SecondsPerBeat; } }
        /// <summary>Music beat at a DSP time, through the run's tempo map (stages speed up).</summary>
        double BeatAt(double dsp) { return engine != null ? engine.Tempo.BeatAt(dsp - musicStartDsp) : (dsp - musicStartDsp) / Spb; }
        double DspAt(double beat) { return musicStartDsp + (engine != null ? engine.Tempo.SecondsAt(beat) : beat * Spb); }
        double SpbAt(double beat) { return engine != null ? engine.Tempo.SecondsPerBeatAt(beat) : Spb; }
        double CurrentBeat() { return state == State.Paused || state == State.Resuming ? pausedBeat : BeatAt(AudioSettings.dspTime); }

        double latencySec = -1;
        /// <summary>
        /// Owner round 16 ("速くなるとずれる"): AudioSettings.dspTime is when a sound is MIXED; it is HEARD one output buffer
        /// queue later. The playfield and the judgement follow what is heard (this delay plus the player's calibration), so
        /// a note reaches the line exactly with its sound at any tempo (a fixed delay in ms is a bigger slice of a fast beat).
        /// </summary>
        public double OutputLatency
        {
            get
            {
                if (latencySec < 0)
                {
                    int length, buffers;
                    AudioSettings.GetDSPBufferSize(out length, out buffers);
                    latencySec = Mathf.Clamp(length * Mathf.Max(1, buffers) / (float)Mathf.Max(8000, AudioSettings.outputSampleRate), 0f, .12f);
                    Debug.Log("[Audio] output latency compensation " + (latencySec * 1000).ToString("0") + " ms (buffer " + length + " x " + buffers + ")");
                }
                return latencySec;
            }
        }

        /// <summary>The beat being HEARD at <paramref name="dsp"/> (judgement and drawing use this).</summary>
        double PlayBeat(double dsp) { return BeatAt(dsp - OutputLatency - profile.calibrationMs / 1000.0); }

        void StartRun()
        {
            if (patterns == null) return;
            if (!UsePlay()) return; // round 32: one play per run (free days, drinks, the shop)
            ProfileStore.TrySave(profile); // persist settings / equipment before playing
            engine = new RunEngine(patterns, Environment.TickCount, config);
            // the very first run opens with practice chunks (tap → hold → mash) whose MISSes cost no heart
            StartPacing();
            runId = Guid.NewGuid().ToString("N");
            forcedTier = null;
            BeginRun();
        }

        void BeginRun()
        {
            engine.EnsureScheduled(0, forcedTier);
            playfield.ResetRun();
            playfield.SetWeapon(profile.equippedWeapon);
            playfield.Effects = profile.Effects;
            playfield.CalmMotion = profile.calmMotion;
            playfield.HintWords = !profile.seenTips.Contains(BossClearedKey);
            lastHp = config.InitialHp; lossCauses.Clear(); offbeatPerfect = 0; heartsTaken = 0;
            playfield.SetBestDistance(profile.bestDistance, config.DistanceMetersPerBeat);
            playfield.SetReady(true);
            fx.Effects = profile.Effects;
            fx.ScreenShake = profile.screenShake;
            fx.ResetRun(profile.bestDistance);
            hud.ResetRun();
            teaseScheduled = false;
            sliced.Clear();
            lastHoldHalf = double.NaN; warnedStage = -1; speedUpScheduledFor = -1; lastAutoMashBeat = -99; holdTicks = 0; lastRage = false;
            chargeWarned.Clear();
            countdownStage = -1; countShown = 0;
            fx.RefreshCharacter();
            double now = AudioSettings.dspTime;
            musicStartDsp = now + .25 + config.CountInBeats * Spb;
            audioDirector.SetLayer(0, true);
            audioDirector.StopMenuMusic(true);
            audioDirector.ResetTrack(StageTrack(0));
            audioDirector.Prepare(MusicTrack.Fever, (int)config.BpmForStage(0));
            audioDirector.StartMusic(musicStartDsp, 0, engine.Tempo);
            for (int i = 0; i < config.CountInBeats; i++)
                audioDirector.PlayAt(i == 0 ? Sfx.CountHigh : Sfx.CountLow, musicStartDsp - (config.CountInBeats - i) * Spb);
            scheduledUpTo = 0;
            lastFrameDsp = now;
            input.Clear();
            input.DebounceMs = config.InputDebounceMs;
            state = State.CountIn;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // only while playing (release P2)
            Show(game);
        }

        void Pause(double atBeat)
        {
            if (state != State.Running && state != State.CountIn && state != State.Resuming) return;
            if (state != State.Resuming) pausedBeat = atBeat; // resuming keeps the beat it was paused at
            state = State.Paused;
            input.Accepting = false;
            input.Clear();
            audioDirector.StopAll();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Show(pauseScreen);
        }

        void Resume()
        {
            if (state != State.Paused) return;
            double now = AudioSettings.dspTime + .1;
            double spb = SpbAt(pausedBeat);
            for (int i = 0; i < ResumeBeats; i++) audioDirector.PlayAt(i == 0 ? Sfx.CountHigh : Sfx.CountLow, now + i * spb);
            resumeMusicDsp = now + ResumeBeats * spb;
            audioDirector.SetLayer(LayerFor(engine.Combo, engine.Fever), true);
            audioDirector.StartMusic(resumeMusicDsp, pausedBeat, engine.Tempo);
            state = State.Resuming;
            lastFrameDsp = AudioSettings.dspTime;
            teaseScheduled = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Show(game);
        }

        void QuitToHome()
        {
            audioDirector.StopAll();
            input.Accepting = false;
            state = State.Menu;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            ShowMenu(home);
            audioDirector.PlayMenuMusic();
            CheckLoginBonus();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            Pause(CurrentBeat());
            ProfileStore.TrySave(profile); // settings are never lost when the app is backgrounded (UI P0 #5)
        }

        void OnApplicationFocus(bool focused) { if (!focused && !Automation.Enabled) Pause(CurrentBeat()); }

        void Update()
        {
            if (!bootFinished && audioDirector.Ready && Time.realtimeSinceStartup > .9f) FinishBoot();
            if (tipOpen && Automation.Enabled && Time.unscaledTime - tipOpenedAt > 1.2f) DismissTip();
            if (engine == null) return;
            double dsp = AudioSettings.dspTime;
            input.Playfield = ScreenRect(canvasRect);

            if (state == State.Resuming && dsp >= resumeMusicDsp)
            {
                musicStartDsp = resumeMusicDsp - engine.Tempo.SecondsAt(pausedBeat);
                scheduledUpTo = pausedBeat;
                state = State.Running;
                playfield.SetReady(false); // a tip during the count-in resumes straight into the run
            }
            if (state == State.CountIn && BeatAt(dsp) >= 0) { state = State.Running; playfield.SetReady(false); }

            if (state == State.Running || state == State.CountIn)
            {
                // A long hitch (debugger, OS stall, audio device loss) becomes a pause, never a burst of timeout MISSes.
                if (Time.unscaledDeltaTime > StallSeconds && state == State.Running) { Pause(BeatAt(lastFrameDsp)); return; }
                lastFrameDsp = dsp;
                double beat = BeatAt(dsp);
                input.Accepting = beat >= -1;
                LaneTap tap;
                while (input.TryDequeue(out tap))
                {
                    // Input events first (same-time inputs win over expiry), judged at their own timestamps.
                    double tapBeat = PlayBeat(tap.Dsp);
                    // while sliding, a release is never final by itself: Steer below decides, with a grace period, so a finger
                    // can lift and land again and a keyboard player can simply press the next lane's key (owner round 15)
                    if (tap.Release && engine.SlideHolding) continue;
                    JudgeResult result = tap.Release ? engine.Release(tap.Lane, tapBeat) : engine.JudgeTap(tap.Lane, tapBeat);
                    OnJudged(result, beat, !tap.Release, tap.Lane);
                }
                // slides: the finger must stay on the snaking object (the bot always does)
                if (!Automation.Autoplay && beat >= 0 && engine.SlideHolding)
                {
                    JudgeResult steer = engine.Steer(input.HeldLane, PlayBeat(dsp), input.LastWasKey ? 150 : 0);
                    if (steer.Rating != Rating.Ignored) OnJudged(steer, beat, false, -1);
                }
                if (Automation.Autoplay && beat >= 0) AutoplayHits(beat);
                Tips(beat);
                if (beat >= 0)
                {
                    engine.EnsureScheduled(beat, forcedTier);
                    ScheduleCues(dsp);
                    foreach (JudgeResult result in engine.Expire(PlayBeat(dsp))) OnJudged(result, beat, false, -1);
                    StageEvents(beat, dsp);
                    UpdateMusic(beat);
                }
                if (engine.Ended)
                {
                    state = State.Ending; endedAt = Time.unscaledTime; input.Accepting = false;
                    audioDirector.EndMusic();
                    Screen.sleepTimeout = SleepTimeout.SystemSetting;
                    FinishRun(beat);
                }
            }

            double viewBeat = state == State.Ending || state == State.Result ? PlayBeat(lastFrameDsp) : state == State.Paused || state == State.Resuming ? pausedBeat : PlayBeat(AudioSettings.dspTime);
            if (game.activeSelf) RenderGame(viewBeat, dsp);
            if (state == State.Ending && Time.unscaledTime - endedAt > .8f) { state = State.Result; Show(resultScreen); PlayResultSequence(); audioDirector.PlayMenuMusic(); }
        }

        static int LayerFor(int combo, bool fever) { return fever ? 4 : combo >= 40 ? 3 : combo >= 25 ? 2 : combo >= 10 ? 1 : 0; }

        /// <summary>Material layer of the slash sound by the object actually drawn.</summary>
        static Sfx MaterialSound(string kind)
        {
            switch (kind)
            {
                // round 9: every kind sounds like what it is
                case "strawberry": case "watermelon": case "melon_big": case "melon_half": return Sfx.MatSplash;
                case "leek": case "pumpkin": return Sfx.MatCrisp;
                case "baguette": return Sfx.MatBread;
                case "duck": return Sfx.MatSqueak;
                case "clock": return Sfx.MatSpring;
                case "dice": return Sfx.MatDice;
                case "barrel": case "crate": case "pencil": return Sfx.MatWood;
                case "gem": return Sfx.MatCrystal;
                case "safe": return Sfx.MatMetal;
                case "ramen": return Sfx.MatSlurp;
                case "candy": return Sfx.MatCrisp;
                case "train": return Sfx.MatClack;
                default: return kind != null && kind.StartsWith("shot_") ? Sfx.MatJuicy : Sfx.MatSoft;
            }
        }

        /// <summary>The chop heard every 16th note while a hold is being cut (round 17).</summary>
        static Sfx ChopSound(string kind)
        {
            switch (kind)
            {
                case "leek": case "candy": case "pumpkin": return Sfx.ChopCrisp;
                case "pencil": case "train": return Sfx.ChopWood;
                case "baguette": return Sfx.ChopBread;
                case "ramen": return Sfx.ChopNoodle;
                default: return Sfx.ChopBlade;
            }
        }

        string HeldKind()
        {
            foreach (ScheduledEvent e in engine.Events)
                if (e.Holding && !e.Resolved) return playfield.KindOf(engine, e);
            return null;
        }

        static Sfx WeaponSound(string id)
        {
            if (id.Contains("roller")) return Sfx.WeaponRoller;
            if (id.Contains("saibashi")) return Sfx.WeaponSaibashi;
            if (id.Contains("ice_claw")) return Sfx.WeaponIce;
            if (id.Contains("lasso")) return Sfx.WeaponLasso;
            if (id.Contains("cardboard")) return Sfx.WeaponCardboard;
            if (id.Contains("smoke")) return Sfx.WeaponSmoke;
            return id.Contains("wooden") ? Sfx.WeaponWood : id.Contains("banana") ? Sfx.WeaponBanana : Sfx.WeaponEnergy;
        }

        // why hearts were lost this run (owner round 9: find out what stops players before the second boss)
        int lastHp, offbeatPerfect, heartsTaken, parryStep;
        readonly Dictionary<string, int> lossCauses = new Dictionary<string, int>();

        static string LossCause(ScheduledEvent e)
        {
            if (e == null) return "空振り";
            if (e.Charge) return "ためこうげき";
            if (e.Soldier != null) return "兵隊のこうげき";
            if (e.ObjectId == "object_boss_shot" || e.Gold) return "ボスの弾";
            if (e.Gesture == Gesture.Hold) return "長い物";
            if (e.Gesture == Gesture.Mash) return "連打";
            if (e.Offbeat) return "裏拍";
            return "ふつうの物";
        }

        /// <summary>"ハートを失った原因：ボスの弾×2・裏拍×1" (most frequent first), or empty.</summary>
        string LossLine()
        {
            if (lossCauses.Count == 0) return "";
            var list = new List<KeyValuePair<string, int>>(lossCauses);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> p in list) parts.Add(p.Key + "×" + p.Value);
            return "ハートを失った原因：" + string.Join("・", parts.ToArray());
        }

        void OnJudged(JudgeResult result, double beat, bool fromTap, int lane)
        {
            if (result.Hp < lastHp)
            {
                string cause = LossCause(result.Event);
                int n; lossCauses.TryGetValue(cause, out n);
                lossCauses[cause] = n + (lastHp - result.Hp);
            }
            lastHp = result.Hp;
            if (result.Rating == Rating.Perfect && result.Event != null && result.Event.Offbeat) offbeatPerfect++;
            if (result.ItemGot == ItemKind.Heart) heartsTaken++;
            if (result.BossDefeated || result.BossEscaped) OnBossResolved(result);
            if (result.Dodged || result.Crushed)
            {
                playfield.ShowJudgement(result, beat, fromTap, lane);
                audioDirector.Play(result.Crushed ? Sfx.GiantStomp : Sfx.BombDodge);
                return;
            }
            if (result.Rating == Rating.Ignored)
            {
                if (result.Giant && fromTap) playfield.GiantSwing(lane); // a giant's swing at nothing still stomps
                return;
            }
            if (result.Rating == Rating.Tick)
            {
                // mash hit: free mashing, but the hit sound lands on the next 16th so it always sounds musical
                playfield.MashTap(engine, result.Event);
                double now = AudioSettings.dspTime;
                double at = DspAt(Math.Ceiling(BeatAt(now + .01) * 4) / 4);
                if (at - now > .07) audioDirector.Play(Sfx.MashHit); else audioDirector.PlayAt(Sfx.MashHit, at);
                if (profile.vibration) Haptics.Play(Haptics.Strength.Light);
                return;
            }
            playfield.SetPerfectStreak(engine.PerfectStreak);
            playfield.ShowJudgement(result, beat, fromTap, lane);
            if (result.ItemGot != ItemKind.None)
            {
                audioDirector.Play(Sfx.ItemGet);
                if (result.ItemGot == ItemKind.Booster) { audioDirector.Play(Sfx.BoostStart); audioDirector.Play(Sfx.Critical, .8f); audioDirector.Duck(-4, .6); playfield.RushStart(); }
                else if (result.ItemGot == ItemKind.Barrier) audioDirector.Play(Sfx.BarrierUp);
                else if (result.ItemGot == ItemKind.Heart) audioDirector.Play(result.Healed ? Sfx.Heal : Sfx.CoinBurst);
                fx.ItemStarted(result.ItemGot, result.Healed);
                if (profile.vibration) Haptics.Play(Haptics.Strength.Medium);
            }
            if (result.ShieldBlocked) { audioDirector.Play(Sfx.BarrierBreak); if (profile.vibration) Haptics.Play(Haptics.Strength.Medium); }
            if (result.Critical) { audioDirector.Play(Sfx.Critical); audioDirector.Duck(-4, .3); if (profile.vibration) Haptics.Play(Haptics.Strength.Success); }
            if (result.Giant && (result.Rating == Rating.Perfect || result.Rating == Rating.Good)) { audioDirector.Play(Sfx.GiantStomp, .9f); if (profile.vibration) Haptics.Play(Haptics.Strength.Medium); }
            if (result.BombHit) { audioDirector.Play(Sfx.BombBoom); audioDirector.Duck(-6, .5); if (profile.vibration) Haptics.Play(Haptics.Strength.Success); }
            if (result.Rush) audioDirector.Play(Sfx.RushSlash, 1f, 1f + .04f * UnityEngine.Random.Range(0, 4));
            if (result.Event != null && result.Event.Charge && result.Kind == ResultKind.HoldTail) { audioDirector.Play(Sfx.GuardHit); audioDirector.Duck(-5, .5); if (profile.vibration) Haptics.Play(Haptics.Strength.Success); }
            string kind = result.Event != null ? playfield.KindOf(engine, result.Event) : null;
            bool hit = result.Rating == Rating.Perfect || result.Rating == Rating.Good;
            if (result.Kind == ResultKind.HoldBreak)
            {
                audioDirector.Play(Sfx.HoldBreak);
                if (teaseScheduled) { audioDirector.StopTease(); teaseScheduled = false; }
                audioDirector.SetLayer(0);
                return;
            }
            if (hit)
            {
                if (result.Kind == ResultKind.HoldTail) audioDirector.Play(Sfx.HoldDone);
                bool unbroken = result.Kind == ResultKind.MashEnd && result.Event != null && !result.Event.Boss && result.Event.MashCount < result.Event.MashRequired;
                if (unbroken) audioDirector.Play(Sfx.HoldBreak); // round 31: not full, so it does not break (no cut sound)
                else if (result.Kind == ResultKind.MashEnd && !(result.Event != null && result.Event.Boss)) { audioDirector.Play(Sfx.MashBreak); audioDirector.Duck(-4, .4); }
                bool counts = kind != null && !kind.StartsWith("shot_") && result.Kind != ResultKind.HoldHead && !(result.Event != null && result.Event.Boss && result.Kind == ResultKind.MashEnd);
                if (counts) { int n; sliced.TryGetValue(kind, out n); sliced[kind] = n + 1; }
                if (result.Event != null && result.Event.Soldier != null && !result.Rush && result.Kind != ResultKind.HoldHead)
                {
                    // round 17: "キン・キン・キン" then the counter "ズバァン"
                    bool final = result.Event.SoldierFinal;
                    audioDirector.Play(final ? Sfx.ParryFinish : Sfx.ParryClang, result.Rating == Rating.Perfect ? 1f : .85f, final ? 1f : 1f + .04f * (parryStep++ % 4));
                    if (final) { audioDirector.Play(Sfx.Blade); audioDirector.Duck(-4, .25); parryStep = 0; }
                    else audioDirector.Duck(-2.5, .09);
                }
                else if (!unbroken) audioDirector.PlaySlash(MaterialSound(kind), result.Combo, engine.Fever, result.Rating == Rating.Perfect, WeaponSound(profile.equippedWeapon));
                if (profile.vibration) Haptics.Play(result.Kind == ResultKind.MashEnd ? Haptics.Strength.Medium : Haptics.Strength.Light);
                audioDirector.SetLayer(Math.Max(LayerFor(result.Combo, engine.Fever), engine.BossRage ? 3 : 0));
                if (result.Combo == 45) ScheduleFeverTease();
            }
            else
            {
                bool broke = engine.Combo == 0;
                if (result.Rating == Rating.Miss || (fromTap && result.Rating == Rating.Empty)) audioDirector.Play(Sfx.Miss, result.Rating == Rating.Empty ? .5f : 1f);
                if (teaseScheduled && broke) { audioDirector.StopTease(); teaseScheduled = false; }
                if (broke) audioDirector.SetLayer(0);
            }
            if (profile.diagnostics && fromTap) lastDiagnostic = result.Rating + (result.Rating == Rating.Perfect || result.Rating == Rating.Good ? " " + result.ErrorMs.ToString("+0;-0") + "ms" : " " + result.Reason);
        }

        void OnBossResolved(JudgeResult result)
        {
            // any result can carry the outcome: a shot, the finisher's mash hit, the mash closing, or a timeout
            if (result.BossDefeated) playfield.Boss.Defeat();
            if (result.BossEscaped) playfield.Boss.Escape();
            if (result.BossDefeated)
            {
                audioDirector.PlayAt(Sfx.BossDefeat, AudioSettings.dspTime + .3); // lands with the burst after the death shake
                audioDirector.PlayAt(Sfx.BossShatter, AudioSettings.dspTime + .32);
                audioDirector.Duck(-6, 1.3);
                audioDirector.PlayAt(Sfx.Heal, AudioSettings.dspTime + .6);
                fx.BossDefeated(config.BossBonusCoinUnits / 3);
                if (!profile.seenTips.Contains(BossClearedKey)) profile.seenTips.Add(BossClearedKey); // from now on the boss prompts are the hand alone
                if (Energy.BossBonus(profile, Today())) Anim.Delay(1.6f, () => fx.ShowTip("エナジードリンク +1", "今日はじめての ボス撃破ボーナス"), home.transform);
                if (profile.vibration) Haptics.Play(Haptics.Strength.Success);
            }
            else if (result.BossEscaped)
            {
                audioDirector.Play(Sfx.BossEscape);
                fx.BossEscaped();
            }
        }

        MusicTrack StageTrack(int stage)
        {
            int theme = engine.ThemeForStage(stage);
            return SynthBank.TrackForStage(catalog != null && catalog.stages != null && theme < catalog.stages.Length ? catalog.stages[theme].id : "stage_kitchen");
        }

        /// <summary>
        /// The piece that should be playing (round 6: ten pieces): the boss's from its WARNING until it falls or runs, else
        /// FEVER's while FEVER lasts, else the stage's (looking a few beats ahead so a stage change is one clean switch).
        /// Upcoming pieces are rendered in the background well before they are needed.
        /// </summary>
        void UpdateMusic(double beat)
        {
            int stage = engine.StageAt(beat);
            int bpm = (int)Math.Round(engine.Tempo.BpmAt(beat));
            double bossStart = engine.BossStartBeat(stage);
            string bossId = playfield.BossIdAt(engine, beat);
            bool bossPhase = engine.HasBossFights && warnedStage == stage && (engine.BossActive || beat < bossStart + 1); // +1: the fight starts a moment after its beat (judgement follows the heard sound)
            // round 17 (owner): the boss's own piece from the WARNING again, with its low intro (the round-16 remix is not used)
            MusicTrack desired = bossPhase ? SynthBank.TrackForBoss(bossId) : engine.Fever ? MusicTrack.Fever : StageTrack(engine.StageAt(beat + 6));
            int desiredBpm = (int)Math.Round(engine.Tempo.BpmAt(beat + 6));
            audioDirector.Prepare(desired, desiredBpm);
            audioDirector.SetTrack(desired);
            // ahead of time: next stage, this stage's boss, FEVER at this and the next tempo
            int nextBpm = (int)config.BpmForStage(stage + 1);
            if (beat >= engine.StageStartBeat(stage + 1) - 40) { audioDirector.Prepare(StageTrack(stage + 1), nextBpm); audioDirector.Prepare(MusicTrack.Fever, nextBpm); }
            if (engine.HasBossFights && beat >= bossStart - 40) audioDirector.Prepare(SynthBank.TrackForBoss(bossId), bpm);
            if (engine.Combo >= 15) audioDirector.Prepare(MusicTrack.Fever, bpm);
        }

        /// <summary>Boss warnings, hold ticks and the speed-up riser that lands exactly on the next stage's first beat.</summary>
        void StageEvents(double beat, double dsp)
        {
            int stage = engine.StageAt(beat);
            double bossStart = engine.BossStartBeat(stage);
            if (engine.HasBossFights && warnedStage != stage && beat >= bossStart - 12 && beat < bossStart)
            {
                warnedStage = stage;
                fx.BossWarning(playfield.BossIdAt(engine, beat));
                // WARNING (4 beats) → countdown 4-3-2-1 on the beats → "BOSS!!" as it lands; its music from the next bar
                audioDirector.Play(Sfx.BossDrumRoll);
                audioDirector.SetLayer(Math.Max(audioDirector.Layer, 2));
                for (int k = 0; k < 4; k++) audioDirector.PlayAt(k == 3 ? Sfx.CountHigh : Sfx.CountLow, DspAt(bossStart - 8 + k));
                audioDirector.PlayAt(Sfx.RageRoar, DspAt(bossStart - 4));
                countdownStage = stage; countShown = 0;
            }
            if (countdownStage == stage)
            {
                int n = (int)Math.Floor(beat - (bossStart - 8));
                if (n >= 0 && n < 4 && n >= countShown) { countShown = n + 1; fx.BossCount(4 - n); }
            }
            double boundary = engine.StageStartBeat(stage + 1);
            double at = DspAt(boundary);
            if (speedUpScheduledFor != stage + 1 && at - dsp < 1.0)
            {
                speedUpScheduledFor = stage + 1;
                if (at - .8 > dsp + .01) audioDirector.PlayAt(Sfx.SpeedUp, at - .8); else audioDirector.Play(Sfx.SpeedUp);
                audioDirector.PlayAt(Sfx.GateWhoosh, Math.Max(dsp + .02, at - .3));
            }
            if (playfield.HoldingLane >= 0)
            {
                // chopping (owner round 14): a cut every 16th note, "ta-ta-ta-ta", climbing a little in pitch
                double half = Math.Floor(beat * 4);
                if (half != lastHoldHalf)
                {
                    lastHoldHalf = half;
                    // round 17 (owner: "シャクシャク / ズバズバ"): the material's own chop, left and right in turn, a blade accent on the beat
                    int tick = holdTicks++;
                    audioDirector.Play(ChopSound(HeldKind()), 1f, (tick % 2 == 0 ? 1f : 1.07f) * Mathf.Min(1.2f, 1f + .012f * tick), tick % 2 == 0 ? -.25f : .25f);
                    if (half % 4 == 0) audioDirector.Play(Sfx.ChopBlade, 1f, 1f + .03f * (tick / 4 % 3));
                    playfield.HoldSlice(engine);
                    // a slide also sounds like what it is on every beat: "zuru" (noodles), "gatan" (train)
                    if (engine.SlideHolding && half % 4 == 0)
                        foreach (ScheduledEvent e in engine.Events)
                            if (e.IsSlide && e.Holding && !e.Resolved) { audioDirector.Play(MaterialSound(playfield.KindOf(engine, e)), .7f); break; }
                }
            }
            else { lastHoldHalf = double.NaN; holdTicks = 0; }
            // boss gimmicks: rage below half HP, warning before a charge attack
            bool rage = engine.BossRage;
            if (rage && !lastRage)
            {
                audioDirector.Play(Sfx.RageRoar);
                audioDirector.Duck(-5, .8);
                audioDirector.SetLayer(Math.Max(audioDirector.Layer, 3));
                fx.BossRage(playfield.BossIdAt(engine, beat));
                if (profile.vibration) Haptics.Play(Haptics.Strength.Medium);
            }
            lastRage = rage;
            playfield.Boss.Rage = rage;
            bool charging = false;
            foreach (ScheduledEvent e in engine.Events)
            {
                if (!e.Charge || e.Resolved || e.Beat - beat > 6 || e.Beat - beat < -.3) continue;
                charging |= !e.Holding;
                if (chargeWarned.Add(e.Id)) { fx.ChargeWarning(); audioDirector.Play(Sfx.ChargeWarn); }
            }
            playfield.Boss.Charging = charging;
        }

        // ------------------------------------------------------------------ one-time hints (tutorial)

        /// <summary>Stored in seenTips once a boss has been beaten.</summary>
        const string BossClearedKey = "boss_clear";

        static readonly Dictionary<string, string[]> TipTexts = new Dictionary<string, string[]>
        {
            { "tap", new[] { "ラインに来たら タップ！", "物が水色の線に重なった瞬間に、\nそのレーン（左・中央・右）を押して斬ろう。" } },
            { "hold", new[] { "長い物は 押しっぱなし！", "先頭がラインに来たら押して、\n最後まで離さずに 斬りきろう。" } },
            { "slide", new[] { "くねった物は なぞれ！", "押したまま、物に沿って\n指を左右にすべらせよう。曲がり角は 紫の点。" } },
            { "split", new[] { "ひびの入ったメロンは 割れる！", "斬ると2つに割れて、\n半分ずつ となりのレーンに 転がってくる。" } },
            { "bomb", new[] { "爆弾は 斬るな！", "導火線がチリチリ言ったら 見送ろう。\n斬るとコンボが切れる。よけたら ボーナス！" } },
            { "mash", new[] { "かたい物は 連打！", "どのレーンでもOK。\nリングが一周するまで たくさん叩いて割ろう。" } },
            { "offbeat", new[] { "紫の点線は 裏拍", "白い線と白い線の まんなか。\n音の「ウラ」で斬ろう。" } },
            { "item", new[] { "光るカプセルは アイテム", "斬ると効果が出るよ。\n取り逃してもミスにはならない。" } },
            { "boss", new[] { "ボスの弾を 斬って返せ！", "斬った弾が ボスにはね返ってダメージ。\n最後は連打で とどめ！" } },
            { "soldier", new[] { "兵隊のこうげきを 弾け！", "剣が光ったら そのレーンをタップ。\nキン・キン・キン…最後の一撃で ふっとばそう！" } },
            { "crow", new[] { "カラスのコックは 気まぐれ！", "レーンを跳ぶ・ためこうげき…何が来るかは毎回ちがう。\n紫の帯は 押しっぱなしで 炎をガード！" } },
            { "gold", new[] { "金色の弾は チャンス！", "PERFECTで返すと\nクリティカルで 大ダメージ！" } },
        };

        GameObject tipScreen;
        Text tipTitle, tipBody;
        Image tipIcon;
        bool tipOpen;
        float tipOpenedAt;

        /// <summary>The tutorial stops the game for each first-time thing (owner round 6: text was unreadable while running).</summary>
        void BuildTip()
        {
            tipScreen = MakeScreen("Tip", true);
            Image dim = tipScreen.AddComponent<Image>();
            dim.color = new Color(.01f, .02f, .06f, .55f);
            Button anywhere = tipScreen.AddComponent<Button>();
            anywhere.transition = Selectable.Transition.None;
            anywhere.onClick.AddListener(DismissTip);
            RectTransform safe = UiKit.Rect("Safe", tipScreen.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            UiKit.Image("Frame", safe, ArtLibrary.Rounded, new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, .7f), new Vector2(.062f, .296f), new Vector2(.938f, .624f)); // border behind the panel
            Image panel = UiKit.Image("Panel", safe, ArtLibrary.Rounded, new Color(.05f, .09f, .2f, .97f), new Vector2(.07f, .3f), new Vector2(.93f, .62f));
            UiKit.Label("はじめての物", panel.transform, 34, UiKit.Gold, new Vector2(.05f, .84f), new Vector2(.95f, .96f), TextAnchor.MiddleCenter, FontStyle.Bold);
            tipIcon = UiKit.Image("Icon", panel.transform, null, Color.white, new Vector2(.36f, .52f), new Vector2(.64f, .84f), true);
            tipTitle = UiKit.Label("", panel.transform, 66, Color.white, new Vector2(.04f, .36f), new Vector2(.96f, .52f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            tipBody = UiKit.Label("", panel.transform, 38, new Color(1, 1, 1, .9f), new Vector2(.06f, .12f), new Vector2(.94f, .36f), TextAnchor.MiddleCenter, FontStyle.Normal);
            Text next = UiKit.Label("タップして続ける", safe, 44, UiKit.Cyan, new Vector2(.1f, .22f), new Vector2(.9f, .28f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            next.gameObject.AddComponent<Breathe>().Amount = .06f;
            tipScreen.SetActive(false);
        }

        void OpenTip(string key, Sprite icon)
        {
            if (state != State.Running && state != State.CountIn) return;
            pausedBeat = CurrentBeat();
            state = State.Paused;
            input.Accepting = false;
            input.Clear();
            audioDirector.StopAll();
            string[] text = TipTexts[key];
            tipTitle.text = text[0]; tipBody.text = text[1];
            tipIcon.sprite = icon; tipIcon.enabled = icon != null;
            tipOpen = true; tipOpenedAt = Time.unscaledTime;
            audioDirector.Play(Sfx.UiConfirm);
            Show(tipScreen);
        }

        void DismissTip()
        {
            if (!tipOpen) return;
            tipOpen = false;
            audioDirector.Play(Sfx.UiSelect);
            Resume(); // the usual 3-beat count back in
        }

        Sprite TipIcon(ScheduledEvent e)
        {
            string kind = playfield.KindOf(engine, e);
            return ArtLibrary.Get(kind + "_intact") ?? ArtLibrary.Get(kind) ?? ArtLibrary.Get("hold_" + kind) ?? ArtLibrary.Get(kind + "_walk");
        }

        static string TipKey(ScheduledEvent e)
        {
            if (e.Item != ItemKind.None) return "item";
            if (e.Bomb) return "bomb";
            if (e.Splits) return "split";
            if (e.Gold) return "gold";
            if (e.Soldier == "crow") return "crow";
            if (e.Charge || (e.Boss && e.Gesture == Gesture.Mash)) return null; // these have their own on-screen prompts
            if (e.Soldier != null) return "soldier";
            if (e.Boss) return "boss";
            if (e.Gesture == Gesture.Hold) return e.IsSlide ? "slide" : "hold";
            if (e.Gesture == Gesture.Mash) return "mash";
            if (e.Offbeat) return "offbeat";
            return "tap";
        }

        // round 29 (owner: "連続で出てくるのでゲームがかなりの頻度で止まる"): new things come one at a time
        const double NoveltyGapSeconds = 30;
        /// <summary>Things that are clear at a glance: a banner instead of stopping the game.</summary>
        static readonly HashSet<string> BannerTips = new HashSet<string> { "item", "gold", "split", "boss" };
        /// <summary>The first-run basics come at fixed points (every 4 chunks), so they always stop.</summary>
        static readonly HashSet<string> BasicTips = new HashSet<string> { "tap", "hold", "mash" };
        double lastStopSeconds = double.NegativeInfinity;
        int lastPracticeChunk = -1;

        /// <summary>Practice chunks every 4 chunks on the first run; new things at least 30 s apart, each followed by practice.</summary>
        void StartPacing()
        {
            if (!profile.tutorialDone) engine.EnableTutorial(4, "tutorial_tap", "tutorial_hold", "tutorial_mash");
            var known = new List<string>(profile.seenTips);
            if (!profile.tutorialDone) known.RemoveAll(k => k == "hold" || k == "mash");
            engine.TrackNovelty(known, NoveltyGapSeconds);
            lastStopSeconds = double.NegativeInfinity; lastPracticeChunk = -1;
        }

        /// <summary>The first time each kind of target comes up, the game stops and explains it (once per player).</summary>
        void Tips(double beat)
        {
            if (tipOpen) return;
            double seconds = engine.Tempo.SecondsAt(Math.Max(0, beat));
            ChunkInfo chunk = engine.ChunkAt(beat);
            if (chunk != null && chunk.Practice && chunk.Index != lastPracticeChunk && !BasicChunk(chunk))
            {
                lastPracticeChunk = chunk.Index;
                fx.ShowTip("れんしゅうタイム", "この間は ミスしても ハートは減らない");
            }
            foreach (ScheduledEvent e in engine.Events)
            {
                if (e.Resolved) continue;
                double ahead = e.Beat - beat;
                if (ahead > 2.6 || ahead < 1.2) continue; // already on the road, still far enough to react after the count
                string key = TipKey(e);
                if (key == null || profile.seenTips.Contains(key)) continue;
                if (BannerTips.Contains(key))
                {
                    profile.seenTips.Add(key);
                    string[] text = TipTexts[key];
                    fx.ShowTip(L10n.T(text[0]), L10n.T(text[1]).Replace("\n", " ")); // translate before the line break goes
                    continue;
                }
                // never stop twice within the gap (the next one waits for its next appearance)
                if (!BasicTips.Contains(key) && seconds - lastStopSeconds < NoveltyGapSeconds) continue;
                profile.seenTips.Add(key);
                lastStopSeconds = seconds;
                Analytics.Event("tip_shown", "key", key, "seconds", (int)seconds);
                OpenTip(key, TipIcon(e));
                return;
            }
        }

        static bool BasicChunk(ChunkInfo c) { return c.PatternId != null && c.PatternId.StartsWith("tutorial_"); }

        /// <summary>At combo 45 the "kyuin" tease is scheduled so its glide peaks right on the 50th hit's beat (effects §4).</summary>
        void ScheduleFeverTease()
        {
            int needed = config.FeverCombo - engine.Combo;
            teaseBeats.Clear();
            foreach (ScheduledEvent e in engine.Events) if (!e.Resolved) teaseBeats.Add(e.Beat);
            if (teaseBeats.Count < needed) return;
            teaseBeats.Sort();
            double hitDsp = DspAt(teaseBeats[needed - 1]);
            audioDirector.PlayAt(Sfx.Kyuin, Math.Max(AudioSettings.dspTime + .02, hitDsp - .35));
            teaseScheduled = true;
        }

        void OnMilestone(int combo)
        {
            if (combo == 10) audioDirector.Play(Sfx.Combo10);
            else if (combo == 25) audioDirector.Play(Sfx.Combo25);
            else if (combo >= 100) { audioDirector.Play(Sfx.Fanfare); audioDirector.Duck(-3, .8); }
            if (profile.vibration) Haptics.Play(Haptics.Strength.Medium);
        }

        void OnFeverEntered()
        {
            teaseScheduled = false;
            audioDirector.Play(Sfx.FeverEnter);
            audioDirector.Duck(-6, .3);
            audioDirector.SetLayer(4);
            double nextBar = DspAt(Math.Ceiling(BeatAt(AudioSettings.dspTime) / 4 + 1e-6) * 4);
            audioDirector.PlayAt(Sfx.FeverSweep, nextBar);
            if (profile.vibration) Haptics.Play(Haptics.Strength.Success);
        }

        void OnFeverEnded()
        {
            audioDirector.Play(Sfx.FeverExit);
            audioDirector.Play(Sfx.ComboBreak);
        }

        /// <summary>Diagnostic bot: judges every due event exactly on its beat through the normal result path.</summary>
        void AutoplayHits(double beat)
        {
            autoplayDue.Clear();
            foreach (ScheduledEvent e in engine.Events)
            {
                if (e.Resolved || e.Holding || e.Bomb) continue; // held notes complete by themselves at their end; bombs are left alone
                if (e.Gesture == Gesture.Mash)
                {
                    // the bot mashes at 1.5× the required rate
                    if (beat >= e.Beat && beat <= e.EndBeat && beat - lastAutoMashBeat >= 1 / (config.MashTapsPerBeat * 1.5)) { lastAutoMashBeat = beat; OnJudged(engine.JudgeTap(1, beat), beat, true, 1); }
                    continue;
                }
                if (e.Beat <= beat) autoplayDue.Add(e);
            }
            foreach (ScheduledEvent e in autoplayDue) OnJudged(engine.JudgeTap(e.Lane, e.Beat + Automation.AutoplayErrorBeats), beat, true, e.Lane);
        }

        void ScheduleCues(double dsp)
        {
            double horizon = BeatAt(dsp + CueLookahead);
            if (horizon <= scheduledUpTo) return;
            for (int half = (int)Math.Floor(scheduledUpTo * 2) + 1; half / 2.0 <= horizon; half++)
            {
                double b = half / 2.0;
                if (b <= scheduledUpTo) continue;
                double at = DspAt(b);
                ChunkInfo chunk = engine.ChunkAt(b);
                bool offbeatSection = engine.IsOffbeatPreview(b) || (chunk != null && chunk.HasOffbeat);
                if (half % 2 == 1 && offbeatSection) audioDirector.PlayAt(Sfx.OffbeatTick, at, .8f);
                foreach (ScheduledEvent e in engine.Events)
                {
                    if (e.Resolved || Math.Abs(e.Beat - 1 - b) >= 1e-6) continue;
                    // round 27: a bomb hisses instead of the "cut here" cue
                    audioDirector.PlayAt(e.Bomb ? Sfx.BombFuse : Sfx.Cue, at, .8f);
                }
            }
            scheduledUpTo = horizon;
        }

        void RenderGame(double beat, double dsp)
        {
            bool preview = engine.IsOffbeatPreview(beat);
            ChunkInfo chunk = engine.ChunkAt(beat);
            bool offbeatChunk = beat >= 0 && (preview || (chunk != null && chunk.HasOffbeat));
            engine.Snapshot(Math.Max(0, beat), liveSnapshot);
            playfield.SetPowerups(liveSnapshot.Boost, liveSnapshot.Giant, liveSnapshot.Shields, liveSnapshot.GiantBeatsLeft);
            GiantSounds(liveSnapshot);
            if (lastRush && !liveSnapshot.Boost) { playfield.RushFinish(); audioDirector.Play(Sfx.GuardHit); if (profile.vibration) Haptics.Play(Haptics.Strength.Success); }
            lastRush = liveSnapshot.Boost;
            playfield.Render(engine, beat, preview);
            audioDirector.SetWind(state == State.Running ? Mathf.Clamp01((playfield.WorldSpeed - 1.5f) / 4f) : 0f);
            string count = null;
            if (state == State.CountIn) count = beat < -1 ? CountText((int)Math.Ceiling(-beat - 1)) : beat < 0 ? "GO!" : null;
            else if (state == State.Resuming) { double left = (resumeMusicDsp - dsp) / SpbAt(pausedBeat); count = left > 0 ? CountText((int)Math.Ceiling(left)) : null; }
            fx.BossMode = hud.Compact = playfield.Boss.Visible;
            int slotStage = engine.StageAt(beat);
            fx.BossSlot = playfield.Boss.Visible || (engine.HasBossFights && beat >= engine.BossStartBeat(slotStage) - 12 && beat < engine.BossStartBeat(slotStage));
            fx.BossLow = playfield.Boss.InFinisher;
            fx.BossRageEdge = playfield.Boss.Visible && engine.BossRage;
            hud.Render(liveSnapshot, beat, offbeatChunk, preview, count, profile.Effects);
            fx.Tick(liveSnapshot, beat, state == State.Running);
            hud.SetDiagnostics(profile.diagnostics
                ? string.Format("beat {0:0.00}  state {1}\nseed {2}  chunk {3}\nlast {4}\ncal {5}ms  bridge {6}\nfps {7:0}  layer {8}", beat, state, engine.Seed, chunk != null ? chunk.PatternId : "-", lastDiagnostic, profile.calibrationMs, input.Bridge.Ready ? (input.Bridge.Offset * 1000).ToString("0") + "ms" : "warming", 1f / Mathf.Max(.001f, Time.smoothDeltaTime), audioDirector.Layer)
                : null);
        }

        // round 27 giant: "pi-pi-pi" on each of its last three beats, a "pon" when it shrinks back
        double giantLastLeft;
        void GiantSounds(RunSnapshot s)
        {
            if (s.Giant && state == State.Running)
            {
                for (int n = 3; n >= 1; n--)
                    if (giantLastLeft > n && s.GiantBeatsLeft <= n) { audioDirector.Play(Sfx.GiantBeep, 1f, n == 1 ? 1.25f : 1f); if (profile.vibration) Haptics.Play(Haptics.Strength.Light); }
            }
            else if (giantLastLeft > 0 && state == State.Running) { audioDirector.Play(Sfx.GiantPop); fx.GiantEnded(); }
            giantLastLeft = s.Giant ? s.GiantBeatsLeft : 0;
        }

        static readonly string[] CountTexts = { "0", "1", "2", "3", "4", "5", "6", "7", "8" };
        static string CountText(int n) { return n >= 0 && n < CountTexts.Length ? CountTexts[n] : n.ToString(); }

        Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        // ------------------------------------------------------------------ automation hooks (diagnostics only)

        internal void AutoOpen(string screen)
        {
            GameObject target = screen == "weapon" ? weaponScreen : screen == "character" ? characterScreen : screen == "settings" ? settingsScreen
                : screen == "items" ? itemScreen : screen == "special" ? specialScreen : screen == "ranking" ? rankingScreen : screen == "event" ? eventScreen : home;
            state = State.Menu; ShowMenu(target);
        }
        internal void AutoStart(int? tier)
        {
            if (patterns == null) return;
            ProfileStore.TrySave(profile);
            if (Automation.StageChunks.HasValue) { config.ChunksPerStage = Automation.StageChunks.Value; config.FirstStageChunks = 0; }
            if (Automation.ForcedItem != null)
            {
                config.BoosterWeight = Automation.ForcedItem == "booster" ? 1 : 0;
                config.GiantWeight = Automation.ForcedItem == "giant" ? 1 : 0;
                config.BarrierWeight = Automation.ForcedItem == "barrier" ? 1 : 0;
            } // short stages to reach the boss and the speed-up quickly
            engine = new RunEngine(patterns, 4242, config);
            engine.ThemeOverride = Automation.ForcedTheme;
            StartPacing();
            runId = Guid.NewGuid().ToString("N");
            forcedTier = tier;
            BeginRun();
        }
        internal void AutoPause() { Pause(CurrentBeat()); }
        internal void AutoResume() { Resume(); }
        internal void AutoPauseSettings(bool open) { if (open) { settingsReturnToPause = true; ShowMenu(settingsScreen); } else LeaveSettings(); }
        internal bool AutoInResult { get { return state == State.Result; } }
        internal bool AutoBooted { get { return bootFinished; } }
        internal int AutoCoins { get { return profile.coinBalance; } }
        internal int AutoCombo { get { return engine != null ? engine.Combo : 0; } }
        internal bool AutoHolding { get { return playfield.HoldingLane >= 0; } }
        internal bool AutoSliding { get { return engine != null && engine.SlideHolding; } }
        /// <summary>A soldier is on the road close to the line (round 17 captures).</summary>
        internal bool AutoSoldier(bool boss)
        {
            if (engine == null || state != State.Running) return false;
            double beat = CurrentBeat();
            foreach (ScheduledEvent e in engine.Events)
                if (!e.Resolved && e.Soldier != null && e.Boss == boss && e.Beat - beat < 1.2 && e.Beat - beat > .1) return true;
            return false;
        }
        internal bool AutoSlideComing
        {
            get
            {
                if (engine == null) return false;
                double beat = CurrentBeat();
                foreach (ScheduledEvent e in engine.Events) if (e.IsSlide && !e.Resolved && !e.Holding && e.Beat - beat > 1.2 && e.Beat - beat < 2.2) return true;
                return false;
            }
        }
        internal bool AutoBoost { get { return liveSnapshot.Boost; } }
        internal bool AutoMashFull { get { return playfield.MashFullShowing; } }
        internal bool AutoTipOpen { get { return tipOpen; } }
        internal bool AutoCountdown { get { return countShown > 0 && countShown < 4; } }
        internal bool AutoGiant { get { return liveSnapshot.Giant; } }
        internal bool AutoGiantEnding { get { return liveSnapshot.Giant && liveSnapshot.GiantBeatsLeft < 2.5; } }
        /// <summary>Round 27 captures: a bomb / a split half close to the line.</summary>
        internal bool AutoNear(Func<ScheduledEvent, bool> what)
        {
            if (engine == null || state != State.Running) return false;
            double beat = CurrentBeat();
            foreach (ScheduledEvent e in engine.Events)
                if (!e.Resolved && what(e) && e.Beat - beat < 1.3 && e.Beat - beat > .2) return true;
            return false;
        }
        internal bool AutoShield { get { return liveSnapshot.Shields > 0; } }
        internal bool AutoRage { get { return engine != null && engine.BossRage; } }
        internal bool AutoCharging { get { if (engine == null) return false; foreach (ScheduledEvent e in engine.Events) if (e.Charge && e.Holding) return true; return false; } }
        internal bool AutoBossVisible { get { return playfield.Boss.Visible; } }
        internal bool AutoBossResolved { get { return engine != null && engine.BossesDefeated + engine.BossesEscaped > 0; } }
        internal int AutoStage { get { return engine != null ? engine.StageAt(CurrentBeat()) : 0; } }
        internal bool AutoMashOpen(bool boss)
        {
            if (engine == null) return false;
            double beat = CurrentBeat();
            foreach (ScheduledEvent e in engine.Events) if (e.Gesture == Gesture.Mash && e.Boss == boss && !e.Resolved && beat >= e.Beat && beat <= e.EndBeat) return true;
            return false;
        }
        internal void AutoGrantCoins(int coins) { profile.coinBalance += coins; ProfileStore.TrySave(profile); RefreshAll(); }
        internal void AutoBuyFirstWeapon() { if (catalog != null) foreach (CatalogItem item in catalog.weapons) if (!profile.Owns(item.id)) { OnWeaponAction(item); return; } }
        internal void AutoConfirmDialog() { if (dialog.activeSelf && dialogAction != null) { Action a = dialogAction; CloseDialog(); a(); } }
    }

    /// <summary>Keeps a sprite image aspect-filling its parent when the parent resizes.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class CoverFitter : MonoBehaviour
    {
        Vector2 last;
        void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            Vector2 size = parent.rect.size;
            if (size == last) return;
            last = size;
            UiKit.Cover(GetComponent<Image>(), size);
        }
    }
}
