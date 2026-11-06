using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;
using RhythmSlashRunner.Persistence;

namespace RhythmSlashRunner.Presentation
{
    public sealed partial class GameApp
    {
        Text homeCoins, homeBest, dialogTitle, dialogText, dialogYesLabel, dialogNoLabel, loadError;
        Button dialogYes, dialogNo;
        Image dialogImage, homeHero, homeWeapon, weaponBadge;
        Action dialogAction;
        bool settingsReturnToPause;

        // ------------------------------------------------------------------ splash

        void BuildSplash()
        {
            splash = MakeScreen("Splash", true);
            Background(splash, ArtLibrary.Has("launch_bg") ? "launch_bg" : "key_visual", Color.white); // round 32: the launch art
            Image logo = UiKit.Image("Logo", splash.transform, ArtLibrary.Get("logo"), Color.white, new Vector2(.06f, .74f), new Vector2(.94f, .95f), true);
            if (logo.sprite == null) { logo.enabled = false; UiKit.Label("RHYTHM\nSLASH RUNNER", splash.transform, 96, Color.white, new Vector2(.06f, .74f), new Vector2(.94f, .95f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true); }
            // running hamster loader (no fake percentage — UX_UI_SPEC ロード)
            Image runner = UiKit.Image("Runner", splash.transform, ArtLibrary.Get("hamster_run_a"), Color.white, Vector2.zero, Vector2.zero, true);
            UiKit.At(runner.rectTransform, new Vector2(.5f, .1f), new Vector2(150, 150));
            runner.gameObject.AddComponent<SpriteFlipbook>().Frames = new[] { ArtLibrary.Get("hamster_run_a"), ArtLibrary.Get("hamster_run_c"), ArtLibrary.Get("hamster_run_b"), ArtLibrary.Get("hamster_run_d") };
            UiKit.Label("Loading…", splash.transform, 34, Color.white, new Vector2(.3f, .02f), new Vector2(.7f, .07f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            loadError = UiKit.Label("データの読み込みに失敗しました。\nアプリを再インストールしてください。", splash.transform, 44, UiKit.Coral, new Vector2(.08f, .2f), new Vector2(.92f, .32f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            loadError.gameObject.SetActive(false);
        }

        void ShowLoadError() { loadError.gameObject.SetActive(true); }

        // ------------------------------------------------------------------ home

        void BuildHome()
        {
            home = MakeScreen("Home", true);
            bool layered = ArtLibrary.Has("home_backdrop_empty");
            Background(home, layered ? "home_backdrop_empty" : "key_visual", Color.white);
            RectTransform content = UiKit.Rect("Content", home.transform, Vector2.zero, Vector2.one);
            content.gameObject.AddComponent<SafeAreaFitter>();
            Image logo = UiKit.Image("Logo", content, ArtLibrary.Get("logo"), Color.white, new Vector2(.05f, .76f), new Vector2(.95f, .93f), true);
            if (logo.sprite == null) { logo.enabled = false; UiKit.Label("RHYTHM\nSLASH RUNNER", content, 96, Color.white, new Vector2(.06f, .76f), new Vector2(.94f, .93f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true); }
            logo.gameObject.AddComponent<Breathe>().Amount = .015f;

            Image coinPill = UiKit.Image("Coins", content, ArtLibrary.Pill, UiKit.Ink, Vector2.zero, Vector2.zero);
            UiKit.At(coinPill.rectTransform, new Vector2(1, 1), new Vector2(300, 92), new Vector2(1, 1)).anchoredPosition = new Vector2(-28, -24);
            Image coin = UiKit.Image("Icon", coinPill.transform, ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle, ArtLibrary.Has("icon_coin") ? Color.white : UiKit.Gold, Vector2.zero, Vector2.zero, true);
            UiKit.At(coin.rectTransform, new Vector2(0, .5f), new Vector2(72, 72)).anchoredPosition = new Vector2(46, 0);
            homeCoins = UiKit.Label("0", coinPill.transform, 50, UiKit.Gold, new Vector2(.32f, .05f), new Vector2(.92f, .95f), TextAnchor.MiddleRight);

            Image bestPill = UiKit.Image("Best", content, ArtLibrary.Pill, UiKit.Ink, Vector2.zero, Vector2.zero);
            UiKit.At(bestPill.rectTransform, new Vector2(0, 1), new Vector2(330, 92), new Vector2(0, 1)).anchoredPosition = new Vector2(28, -24);
            Image crown = UiKit.Image("Crown", bestPill.transform, ArtLibrary.Get("icon_crown") ?? ArtLibrary.Star, ArtLibrary.Has("icon_crown") ? Color.white : UiKit.Gold, Vector2.zero, Vector2.zero, true);
            UiKit.At(crown.rectTransform, new Vector2(0, .5f), new Vector2(66, 66)).anchoredPosition = new Vector2(46, 0);
            homeBest = UiKit.Label("0 m", bestPill.transform, 46, Color.white, new Vector2(.3f, .05f), new Vector2(.92f, .95f), TextAnchor.MiddleLeft);

            if (layered)
            {
                // hamster as its own layer so the equipped weapon shows; tapping it gets a reaction
                RectTransform stage = UiKit.Rect("HeroStage", content, new Vector2(.18f, .3f), new Vector2(.82f, .72f));
                Image pedestal = UiKit.Image("Pedestal", stage, ArtLibrary.Get("ui_pedestal"), Color.white, new Vector2(.05f, -.06f), new Vector2(.95f, .2f), true);
                pedestal.enabled = pedestal.sprite != null;
                homeHero = UiKit.Image("Hero", stage, ArtLibrary.Get(HoldPose), Color.white, new Vector2(.05f, .04f), new Vector2(.95f, 1f), true);
                homeHero.raycastTarget = true;
                homeWeapon = AttachWeapon(homeHero, profile.equippedWeapon);
                var idle = homeHero.gameObject.AddComponent<IdleHamster>();
                idle.Idle = ArtLibrary.Get(HoldPose); idle.Blink = ArtLibrary.Get("hamster_front_hold_blink"); idle.Cheer = ArtLibrary.Get("hamster_front_cheer");
                idle.Weapon = homeWeapon;
                var react = homeHero.gameObject.AddComponent<Button>();
                react.transition = Selectable.Transition.None;
                react.onClick.AddListener(() => { idle.React(); audioDirector.Play(WeaponSound(profile.equippedWeapon)); });
                refreshers.Add(() => SetWeaponSprite(homeWeapon, profile.equippedWeapon));
            }

            BuildEnergyHud(content);
            Button play = UiKit.Button("", content, UiKit.Cyan, new Vector2(.12f, .165f), new Vector2(.88f, .265f), StartRun, 84, true);
            Image tri = UiKit.Image("Play", play.transform, ArtLibrary.Play, Color.white, new Vector2(.17f, .22f), new Vector2(.3f, .78f), true);
            UiKit.AddOutline(tri.gameObject, new Color(.02f, .2f, .35f, .5f), 2);
            Text playLabel = UiKit.Label("プレイ", play.transform, 96, Color.white, new Vector2(.28f, .08f), new Vector2(.9f, .92f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.AddOutline(playLabel.gameObject, new Color(.02f, .2f, .35f, .55f), 3);
            play.gameObject.AddComponent<Breathe>().Amount = .04f;
            play.gameObject.AddComponent<Sheen>();

            // owner round 2: no stage select — the scenery changes by itself while running
            // round 32: one shop (characters, weapons, items, specials), the weekly ranking and events
            // round 36: the event button waits until events exist (Features.ComingSoon)
            string[] labels = Features.ComingSoon ? new[] { "ショップ", "ランキング", "イベント", "設定" } : new[] { "ショップ", "ランキング", "設定" };
            int[] targets = Features.ComingSoon ? new[] { 0, 4, 5, 3 } : new[] { 0, 4, 3 };
            float pitch = .92f / labels.Length, width = pitch - .02f;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = targets[i];
                float x0 = .04f + i * pitch;
                Button b = UiKit.Button("", content, UiKit.NavyLight, new Vector2(x0, .045f), new Vector2(x0 + width, .145f), () => OpenMenu(index), 40);
                string iconName = index == 0 ? "hamster_front_idle" : index == 4 ? "icon_crown" : index == 5 ? "giftbox_closed" : "icon_settings";
                Image icon = UiKit.Image("Icon", b.transform, ArtLibrary.Get(iconName), Color.white, new Vector2(.2f, .36f), new Vector2(.8f, .94f), true);
                if (index == 0) refreshers.Add(() => icon.sprite = ArtLibrary.Get("hamster_front_idle") ?? icon.sprite);
                if (icon.sprite == null) { icon.sprite = ArtLibrary.Ring; icon.color = UiKit.Cyan; icon.rectTransform.anchorMin = new Vector2(.3f, .42f); icon.rectTransform.anchorMax = new Vector2(.7f, .86f); }
                if (index == 0)
                {
                    // "!" badge when something new is affordable (UI P1 #11)
                    weaponBadge = UiKit.Image("Badge", b.transform, ArtLibrary.Circle, UiKit.Coral, Vector2.zero, Vector2.zero);
                    UiKit.At(weaponBadge.rectTransform, new Vector2(.92f, .9f), new Vector2(54, 54));
                    UiKit.Label("!", weaponBadge.transform, 40, Color.white, Vector2.zero, Vector2.one);
                    weaponBadge.gameObject.AddComponent<Breathe>().Amount = .1f;
                    refreshers.Add(() => weaponBadge.gameObject.SetActive(HasAffordableUnowned()));
                }
                UiKit.Label(labels[i], b.transform, 34, Color.white, new Vector2(0, .04f), new Vector2(1, .36f));
            }
            refreshers.Add(() => { homeCoins.text = profile.coinBalance.ToString("N0"); homeBest.text = profile.bestDistance > 0 ? profile.bestDistance + " m" : "BEST --"; });

            // today's goals (owner round 10): a chip under the best distance; tapping lists the three goals
            Button daily = UiKit.Button("今日の目標", content, UiKit.Ink, Vector2.zero, Vector2.zero, () =>
            {
                audioDirector.Play(Sfx.UiConfirm);
                DailyGoals.Refresh(profile, Today());
                ShowDialog("今日の目標", DailyGoals.Describe(profile) + "\n\nひとつ達成するごとに コイン +" + DailyGoals.RewardCoins + "\n（毎日かわります）", "OK", null, null);
            }, 36);
            // under the logo on the right, beside the hamster's head (the top corners belong to the best distance and coins)
            RectTransform dailyRect = (RectTransform)daily.transform;
            dailyRect.anchorMin = new Vector2(.6f, .695f); dailyRect.anchorMax = new Vector2(.97f, .748f);
            Text dailyLabel = daily.GetComponentInChildren<Text>();
            refreshers.Add(() =>
            {
                DailyGoals.Refresh(profile, Today());
                int n = DailyGoals.DoneCount(profile);
                dailyLabel.text = (n >= DailyGoals.Count ? "★ " : "") + "今日の目標 " + n + "/" + DailyGoals.Count;
                dailyLabel.color = n >= DailyGoals.Count ? UiKit.Gold : Color.white;
            });
        }

        const string HoldPose = "hamster_front_hold";

        bool HasAffordableUnowned()
        {
            if (catalog == null) return false;
            foreach (CatalogItem w in catalog.weapons) if (!profile.Owns(w.id) && w.Playable && profile.coinBalance >= w.price) return true;
            return false;
        }

        /// <summary>Weapon sprite parented to the hold pose's fist (grip pivot shared by every weapon).</summary>
        Image AttachWeapon(Image hero, string weaponId)
        {
            Vector2 fist = ArtLibrary.Has(HoldPose) ? new Vector2(.2f, .52f) : new Vector2(.12f, .64f);
            Image w = UiKit.Image("Weapon", hero.transform, null, Color.white, fist, fist, true);
            w.transform.SetAsFirstSibling();
            SetWeaponSprite(w, weaponId);
            return w;
        }

        static void SetWeaponSprite(Image w, string weaponId)
        {
            if (w == null) return;
            WeaponVisual v = WeaponVisual.For(weaponId);
            w.sprite = ArtLibrary.Get(weaponId);
            w.enabled = w.sprite != null;
            w.rectTransform.pivot = new Vector2(.5f, v.PivotY);
            w.rectTransform.sizeDelta = new Vector2(210, 330) * v.Scale;
            w.rectTransform.localRotation = Quaternion.Euler(0, 0, 16);
        }

        void OpenMenu(int index)
        {
            if (index == 3) settingsReturnToPause = false;
            if (index == 0 || index == 1) { OpenShop(index); return; }
            ShowMenu(index == 4 ? rankingScreen : index == 5 ? eventScreen : settingsScreen);
        }

        RectTransform MenuScreen(string name, string title, out GameObject screen, Action onBack = null)
        {
            screen = MakeScreen(name, true);
            Background(screen, "kitchen_backdrop", new Color(.45f, .45f, .55f));
            RectTransform content = UiKit.Rect("Content", screen.transform, Vector2.zero, Vector2.one);
            content.gameObject.AddComponent<SafeAreaFitter>();
            Image header = UiKit.Image("Header", content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(.03f, .9f), new Vector2(.97f, .975f));
            Button back = UiKit.Button("", header.transform, UiKit.NavyLight, new Vector2(.015f, .1f), new Vector2(.14f, .9f), () => { audioDirector.Play(Sfx.UiBack); if (onBack != null) onBack(); else ShowMenu(home); }, 72);
            Image arrow = UiKit.Image("Arrow", back.transform, ArtLibrary.Play, Color.white, new Vector2(.3f, .25f), new Vector2(.7f, .75f), true);
            arrow.rectTransform.localScale = new Vector3(-1, 1, 1);
            Text t = UiKit.Label(title, header.transform, 58, Color.white, new Vector2(.18f, .05f), new Vector2(.82f, .95f));
            t.resizeTextForBestFit = false;
            return content;
        }

        // ------------------------------------------------------------------ character & stage

        Image characterHero;
        Text characterName;

        static readonly string[][] CharacterCards =
        {
            new[] { "character_hamster", "hamster_front_idle", "ハムスター", "ゴールデンハムスター" },
            new[] { "character_cat", "cat_front_idle", "黒猫", "クロネコ" },
            new[] { "character_robot", "robot_front_idle", "ロボット", "ロボ" },
            new[] { "character_penguin", "penguin_front_idle", "ペンギン", "ペンギン" },
            new[] { "character_shiba", "shiba_front_idle", "柴犬", "シバイヌ" },
            new[] { "character_otottsan", "otottsan_front_idle", "おとっつぁん", "おとっつぁん" },
            new[] { "character_toumei", "toumei_front_idle", "透明人間", "トウメイニンゲン" },
            new[] { "character_tori", "tori_front_idle", "鳥じい", "トリジイ" },
            // round 25 art finished in round 30: the long-haired white hamster and the jelly shark
            new[] { "character_whamster", "whamster_front_idle", "白ハム", "シロハム" },
            new[] { "character_shark", "shark_front_idle", "ゼリーザメ", "ゼリーザメ" },
        };

        /// <summary>Characters (owner round 5): bought with coins, equipped with one tap. Looks only — judgement and score never change.</summary>
        void BuildCharacter()
        {
            RectTransform content = MenuScreen("Character", "ショップ", out characterScreen);
            ShopTabs(content, 0);
            UiKit.Image("Glow", content, ArtLibrary.Glow, new Color(1f, .85f, .5f, .55f), new Vector2(.1f, .45f), new Vector2(.9f, .88f));
            // Ten characters (5 × 2); unfinished catalog entries stay hidden until their art sets exist.
            Image pedestal = UiKit.Image("Pedestal", content, ArtLibrary.Get("ui_pedestal"), Color.white, new Vector2(.22f, .46f), new Vector2(.78f, .55f), true);
            pedestal.enabled = pedestal.sprite != null;
            characterHero = UiKit.Image("Hero", content, ArtLibrary.Get("hamster_front_idle"), Color.white, new Vector2(.22f, .5f), new Vector2(.78f, .83f), true);
            var idle = characterHero.gameObject.AddComponent<IdleHamster>();
            characterName = UiKit.Label("", content, 54, Color.white, new Vector2(.1f, .41f), new Vector2(.9f, .465f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            for (int i = 0; i < CharacterCards.Length; i++)
            {
                string[] info = CharacterCards[i];
                // two rows, leaving enough room for the large preview above
                int col = i % 5, row = i / 5;
                float x0 = .025f + col * .192f, y1 = .395f - row * .145f;
                Image card = UiKit.Image("Card_" + info[0], content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(x0, y1 - .135f), new Vector2(x0 + .18f, y1));
                card.raycastTarget = true;
                Button button = card.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                card.gameObject.AddComponent<PressFeel>();
                Image frame = UiKit.Image("Frame", card.transform, ArtLibrary.Rounded, new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, .55f), new Vector2(-.03f, -.02f), new Vector2(1.03f, 1.02f));
                frame.transform.SetAsFirstSibling();
                Image art = UiKit.Image("Art", card.transform, ArtLibrary.GetExact(info[1]) ?? ArtLibrary.Get(info[0] == "character_cat" ? "card_cat" : "card_robot") /* round 20: the hamster card showed the equipped character */, Color.white, new Vector2(.08f, .3f), new Vector2(.92f, .96f), true);
                UiKit.Label(info[2], card.transform, 28, Color.white, new Vector2(.02f, .15f), new Vector2(.98f, .3f)).verticalOverflow = VerticalWrapMode.Truncate; // round 36: long English names shrink instead of running into the price
                Text state = UiKit.Label("", card.transform, 26, UiKit.Cyan, new Vector2(.02f, .02f), new Vector2(.98f, .15f));
                string id = info[0];
                button.onClick.AddListener(() => OnCharacterAction(id));
                refreshers.Add(() =>
                {
                    CatalogItem item = CatalogQueries.Find(catalog != null ? catalog.characters : null, id);
                    bool owned = profile.Owns(id), equipped = profile.equippedCharacter == id;
                    frame.enabled = equipped;
                    card.color = equipped ? new Color(.1f, .28f, .42f, .95f) : UiKit.Ink;
                    art.color = owned ? Color.white : new Color(.55f, .55f, .62f, 1f);
                    state.text = equipped ? "使用中" : owned ? "タップで使う" : "\u25CF " + (item != null ? item.price : 300) + " コイン";
                    state.color = equipped ? UiKit.Cyan : owned ? Color.white : UiKit.Gold;
                });
            }
            refreshers.Add(() =>
            {
                characterHero.sprite = ArtLibrary.Get("hamster_front_idle");
                idle.Idle = characterHero.sprite; idle.Blink = ArtLibrary.Get("hamster_front_idle_blink"); idle.Cheer = ArtLibrary.Get("hamster_front_cheer");
                foreach (string[] info in CharacterCards) if (info[0] == profile.equippedCharacter) characterName.text = info[3];
            });
            UiKit.Label("キャラクターの違いは見た目だけ。判定とスコアは全員共通です。", content, 32, new Color(1, 1, 1, .8f), new Vector2(.06f, .04f), new Vector2(.94f, .1f));
        }

        void OnCharacterAction(string id)
        {
            CatalogItem item = CatalogQueries.Find(catalog != null ? catalog.characters : null, id);
            string name = id == "character_cat" ? "黒猫" : id == "character_robot" ? "ロボット" : id == "character_penguin" ? "ペンギン" : id == "character_shiba" ? "柴犬" : id == "character_otottsan" ? "おとっつぁん" : id == "character_toumei" ? "透明人間" : id == "character_tori" ? "鳥じい" : id == "character_whamster" ? "白ハム" : id == "character_shark" ? "ゼリーザメ" : "ハムスター";
            if (profile.Owns(id)) { EquipCharacter(id); return; }
            if (item == null || !item.Playable) { audioDirector.Play(Sfx.UiDenied); return; }
            if (profile.coinBalance < item.price)
            {
                audioDirector.Play(Sfx.UiDenied);
                ShowDialog("コインが足りません", name + "まで あと " + (item.price - profile.coinBalance) + " コイン。\n走ってコインを集めよう！", "OK", null, null);
                return;
            }
            ShowDialog(name + "を仲間にする", item.price + " コインで仲間にしますか？\n（見た目だけ。判定とスコアは同じです）", "仲間にする", () =>
            {
                PurchaseResult result = ProfileStore.TryPurchase(profile, item);
                if (result != PurchaseResult.Bought) { audioDirector.Play(Sfx.UiDenied); ShowDialog("購入できませんでした", "もう一度お試しください。", "OK", null, null); return; }
                audioDirector.Play(Sfx.Purchase);
                EquipCharacter(id);
                ShowDialog(name + "が仲間になった！", "ホームからそのまま走れます。", "OK", null, null);
            }, "キャンセル");
        }

        void EquipCharacter(string id)
        {
            profile.equippedCharacter = id;
            ProfileStore.TrySave(profile);
            ArtLibrary.CharacterPrefix = ArtLibrary.PrefixFor(id);
            audioDirector.Play(Sfx.UiConfirm);
            // cached pictures that were built with the previous character
            foreach (Image img in new[] { homeHero, weaponHero })
            {
                if (img == null) continue;
                img.sprite = ArtLibrary.Get(HoldPose);
                var idleHero = img.GetComponent<IdleHamster>();
                if (idleHero != null) { idleHero.Idle = img.sprite; idleHero.Blink = ArtLibrary.Get("hamster_front_hold_blink"); idleHero.Cheer = ArtLibrary.Get("hamster_front_cheer"); }
            }
            fx.RefreshCharacter();
            RefreshAll();
            var react = characterHero.GetComponent<IdleHamster>();
            if (react != null) react.React();
        }

        Button Card(RectTransform parent, Vector2 min, Vector2 max, string sprite, string name, string state, bool enabled, bool selected)
        {
            Image card = UiKit.Image("Card_" + name, parent, ArtLibrary.Rounded, selected ? new Color(.1f, .28f, .42f, .95f) : UiKit.Ink, min, max);
            card.raycastTarget = true;
            Button button = card.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            card.gameObject.AddComponent<PressFeel>();
            if (selected) UiKit.Image("Frame", card.transform, ArtLibrary.Rounded, new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, .55f), new Vector2(-.03f, -.02f), new Vector2(1.03f, 1.02f)).transform.SetAsFirstSibling();
            Image art = UiKit.Image("Art", card.transform, ArtLibrary.Get(sprite), enabled ? Color.white : new Color(.35f, .35f, .42f, .85f), new Vector2(.08f, .3f), new Vector2(.92f, .96f), true);
            if (art.sprite == null) { art.sprite = ArtLibrary.Circle; art.color = new Color(.3f, .35f, .45f); }
            if (!enabled)
            {
                Image lockIcon = UiKit.Image("Lock", card.transform, ArtLibrary.Get("icon_lock") ?? ArtLibrary.Circle, ArtLibrary.Has("icon_lock") ? Color.white : UiKit.Gold, new Vector2(.32f, .45f), new Vector2(.68f, .75f), true);
                lockIcon.raycastTarget = false;
            }
            else if (selected)
            {
                Image check = UiKit.Image("Check", card.transform, ArtLibrary.Circle, UiKit.Cyan, Vector2.zero, Vector2.zero);
                UiKit.At(check.rectTransform, new Vector2(.86f, .9f), new Vector2(54, 54));
                UiKit.Image("Mark", check.transform, ArtLibrary.Check, Color.white, Vector2.zero, Vector2.one, true);
            }
            UiKit.Label(name, card.transform, 34, Color.white, new Vector2(.02f, .15f), new Vector2(.98f, .3f));
            UiKit.Label(state, card.transform, 26, enabled ? UiKit.Cyan : new Color(1, 1, 1, .6f), new Vector2(.02f, .02f), new Vector2(.98f, .15f));
            return button;
        }

        void BuildSettings()
        {
            RectTransform content = MenuScreen("Settings", "設定", out settingsScreen, LeaveSettings);
            Image panel = UiKit.Image("Panel", content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(.04f, .04f), new Vector2(.96f, .885f));
            float y = .965f;
            Slider(panel.transform, ref y, "音楽", 0, 100, () => profile.musicVolume * 100, v => { profile.musicVolume = v / 100f; ApplyVolumes(); }, v => Mathf.RoundToInt(v) + "%", 1);
            Slider(panel.transform, ref y, "効果音", 0, 100, () => profile.sfxVolume * 100, v => { profile.sfxVolume = v / 100f; ApplyVolumes(); }, v => Mathf.RoundToInt(v) + "%", 1);
            Slider(panel.transform, ref y, "合図音", 0, 100, () => profile.cueVolume * 100, v => { profile.cueVolume = v / 100f; ApplyVolumes(); }, v => Mathf.RoundToInt(v) + "%", 1);
            Slider(panel.transform, ref y, "タイミング補正", -250, 250, () => profile.calibrationMs, v => profile.calibrationMs = Mathf.RoundToInt(v / 10f) * 10, v => (Mathf.RoundToInt(v / 10f) * 10 > 0 ? "+" : "") + Mathf.RoundToInt(v / 10f) * 10 + "ms", 10);
            UiKit.Label("判定が音より遅く感じたら＋へ", panel.transform, 28, new Color(1, 1, 1, .75f), new Vector2(.05f, y - .045f), new Vector2(.56f, y + .005f), TextAnchor.MiddleLeft, FontStyle.Normal);
            UiKit.Button("自動で合わせる", panel.transform, UiKit.Cyan, new Vector2(.58f, y - .048f), new Vector2(.95f, y + .002f), OpenCalibration, 34, true);
            y -= .05f;
            Segment(panel.transform, ref y, "演出の強さ", new[] { "弱", "標準", "強" }, () => profile.effectLevel, v => profile.effectLevel = v);
            // round 34: language (the whole interface switches after a restart; the labels are built once)
            Segment(panel.transform, ref y, "言語 Language", new[] { "日本語", "English" }, () => (string.IsNullOrEmpty(profile.language) ? L10n.Auto() : profile.language) == "en" ? 1 : 0, v =>
            {
                string lang = v == 1 ? "en" : "ja";
                if (lang == (string.IsNullOrEmpty(profile.language) ? L10n.Auto() : profile.language)) return;
                profile.language = lang;
                ProfileStore.TrySave(profile);
                ShowDialog(lang == "en" ? "Language: English" : "言語：日本語", lang == "en" ? "Restart the app to switch the language." : "アプリを再起動すると 日本語になります。", "OK", null, null);
            });
            Toggle(panel.transform, ref y, "画面の揺れ", () => profile.screenShake, v => profile.screenShake = v);
            Toggle(panel.transform, ref y, "振動", () => profile.vibration, v => { profile.vibration = v; if (v) Haptics.Play(Haptics.Strength.Medium); });
            Toggle(panel.transform, ref y, "背景の動きをおさえる（酔いやすい方に）", () => profile.calmMotion, v => profile.calmMotion = v);
            Toggle(panel.transform, ref y, "プレイ記録を送る（匿名・改善のため）", () => profile.analyticsConsent == 1, v =>
            {
                profile.analyticsConsent = v ? 1 : 2;
                if (v) Analytics.Start(); else Analytics.Stop(true);
            });
            if (Debug.isDebugBuild || Application.isEditor) Toggle(panel.transform, ref y, "診断表示（開発用）", () => profile.diagnostics, v => profile.diagnostics = v);
            UiKit.Button("チュートリアルとヒントをもう一度", panel.transform, UiKit.NavyLight, new Vector2(.05f, .1f), new Vector2(.95f, .16f), () =>
            {
                profile.tutorialDone = false; profile.seenTips.Clear(); ProfileStore.TrySave(profile);
                ShowDialog("次のプレイで表示します", "最初の練習と、はじめて出てくる物のヒントをもう一度表示します。", "OK", null, null);
            }, 38);
            UiKit.Button("ライセンス", panel.transform, UiKit.NavyLight, new Vector2(.05f, .02f), new Vector2(.47f, .085f), () => ShowMenu(licenseScreen), 40);
            UiKit.Button("初期値に戻す", panel.transform, UiKit.NavyLight, new Vector2(.53f, .02f), new Vector2(.95f, .085f), () => ShowDialog("設定を初期値に戻す", "音量・補正・演出の設定を初期値に戻します。\nコインと所持品は消えません。", "戻す", ResetSettings, "キャンセル", true), 40);
        }

        void LeaveSettings()
        {
            ProfileStore.TrySave(profile); // saved on leaving, not only at run start (UI P0 #5)
            playfield.Effects = profile.Effects; fx.Effects = profile.Effects; fx.ScreenShake = profile.screenShake; playfield.CalmMotion = profile.calmMotion;
            if (settingsReturnToPause) { settingsReturnToPause = false; game.SetActive(true); Show(pauseScreen); }
            else ShowMenu(home);
        }

        void ResetSettings()
        {
            var d = new PlayerProfile();
            profile.musicVolume = d.musicVolume; profile.sfxVolume = d.sfxVolume; profile.cueVolume = d.cueVolume;
            profile.vibration = d.vibration; profile.calibrationMs = d.calibrationMs; profile.effectLevel = d.effectLevel; profile.screenShake = d.screenShake; profile.diagnostics = d.diagnostics;
            ApplyVolumes();
            ProfileStore.TrySave(profile);
            RefreshAll();
        }

        void ApplyVolumes() { audioDirector.SetVolumes(profile.musicVolume, profile.sfxVolume, profile.cueVolume); }

        void Slider(Transform parent, ref float y, string title, float min, float max, Func<float> get, Action<float> set, Func<float, string> format, float step)
        {
            UiKit.Label(title, parent, 38, Color.white, new Vector2(.05f, y - .042f), new Vector2(.6f, y), TextAnchor.MiddleLeft);
            Text value = UiKit.Label("", parent, 38, UiKit.Cyan, new Vector2(.6f, y - .042f), new Vector2(.95f, y), TextAnchor.MiddleRight);
            RectTransform area = UiKit.Rect("Slider", parent, new Vector2(.05f, y - .082f), new Vector2(.95f, y - .045f));
            UiKit.Image("Track", area, ArtLibrary.Pill, new Color(1, 1, 1, .15f), new Vector2(0, .3f), new Vector2(1, .7f));
            RectTransform fillArea = UiKit.Rect("FillArea", area, new Vector2(0, .3f), new Vector2(1, .7f));
            Image fill = UiKit.Image("Fill", fillArea, ArtLibrary.Pill, UiKit.Cyan, Vector2.zero, Vector2.one);
            RectTransform handleArea = UiKit.Rect("HandleArea", area, Vector2.zero, Vector2.one);
            handleArea.offsetMin = new Vector2(30, 0); handleArea.offsetMax = new Vector2(-30, 0);
            Image handle = UiKit.Image("Handle", handleArea, ArtLibrary.Circle, Color.white, new Vector2(0, 0), new Vector2(0, 1));
            handle.rectTransform.sizeDelta = new Vector2(60, 0);
            handle.raycastTarget = true;
            var slider = area.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = step >= 1;
            Image hit = area.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); // whole bar is draggable
            slider.onValueChanged.AddListener(v =>
            {
                if (step > 1) { float snapped = Mathf.Round(v / step) * step; if (!Mathf.Approximately(snapped, v)) { slider.SetValueWithoutNotify(snapped); v = snapped; } }
                set(v); value.text = format(v);
            });
            refreshers.Add(() => { slider.SetValueWithoutNotify(get()); value.text = format(get()); });
            y -= .086f; // round 10/34: tighter so every row fits above the bottom buttons
        }

        void Toggle(Transform parent, ref float y, string title, Func<bool> get, Action<bool> set)
        {
            UiKit.Label(title, parent, 36, Color.white, new Vector2(.05f, y - .056f), new Vector2(.7f, y), TextAnchor.MiddleLeft);
            // switch-style toggle: track + knob (UI P2 #18)
            Button b = UiKit.Button("", parent, UiKit.NavyLight, new Vector2(.74f, y - .052f), new Vector2(.95f, y - .006f), null, 40);
            ((Image)b.targetGraphic).sprite = ArtLibrary.Pill;
            Image knob = UiKit.Image("Knob", b.transform, ArtLibrary.Circle, Color.white, Vector2.zero, Vector2.zero);
            knob.rectTransform.sizeDelta = new Vector2(64, 64);
            Text label = UiKit.Label("", b.transform, 30, Color.white, new Vector2(.08f, .1f), new Vector2(.92f, .9f));
            b.onClick.AddListener(() => { set(!get()); RefreshAll(); });
            refreshers.Add(() =>
            {
                bool on = get();
                ((Image)b.targetGraphic).color = on ? UiKit.Cyan : new Color(.25f, .3f, .42f);
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(on ? .78f : .22f, .5f);
                knob.rectTransform.anchoredPosition = Vector2.zero;
                label.text = on ? "ON " : " OFF";
                label.alignment = on ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            });
            y -= .06f;
        }

        void Segment(Transform parent, ref float y, string title, string[] options, Func<int> get, Action<int> set)
        {
            UiKit.Label(title, parent, 38, Color.white, new Vector2(.05f, y - .056f), new Vector2(.42f, y), TextAnchor.MiddleLeft);
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                float x0 = .44f + i * .172f;
                Button b = UiKit.Button(options[i], parent, UiKit.NavyLight, new Vector2(x0, y - .054f), new Vector2(x0 + .16f, y - .006f), () => { set(index); RefreshAll(); }, 36);
                refreshers.Add(() => ((Image)b.targetGraphic).color = get() == index ? UiKit.Cyan : new Color(.25f, .3f, .42f));
            }
            y -= .064f;
        }

        void BuildLicense()
        {
            RectTransform content = MenuScreen("License", "ライセンス", out licenseScreen, () => ShowMenu(settingsScreen));
            // round 36: the privacy policy (the stores want it reachable in the app) once its page is online
            bool policy = !string.IsNullOrEmpty(Features.PrivacyPolicyUrl);
            if (policy)
                UiKit.Button("プライバシーポリシー", content, UiKit.Cyan, new Vector2(.12f, .03f), new Vector2(.88f, .1f), () => { audioDirector.Play(Sfx.UiConfirm); Application.OpenURL(Features.PrivacyPolicyUrl); }, 44);
            Image panel = UiKit.Image("Panel", content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(.04f, policy ? .12f : .04f), new Vector2(.96f, .885f));
            RectTransform viewport = UiKit.Rect("Viewport", panel.transform, new Vector2(.04f, .03f), new Vector2(.96f, .97f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            RectTransform body = UiKit.Rect("Body", viewport, new Vector2(0, 1), new Vector2(1, 1));
            body.pivot = new Vector2(.5f, 1);
            TextAsset ofl = Resources.Load<TextAsset>("Fonts/OFL_MPLUSRounded1c");
            Text text = UiKit.Label("フォント: M PLUS Rounded 1c (Bold / ExtraBold)\nCopyright 2016 The Rounded M+ Project Authors.\nSIL Open Font License 1.1\n\nゲーム内の画像はAI画像生成で制作しています。\n効果音とBGMはアプリ内で合成しています。\n\n" + (ofl != null ? ofl.text : ""),
                body, 26, Color.white, Vector2.zero, Vector2.one, TextAnchor.UpperLeft, FontStyle.Normal);
            text.resizeTextForBestFit = false;
            var fit = text.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            text.rectTransform.anchorMin = new Vector2(0, 1); text.rectTransform.anchorMax = new Vector2(1, 1); text.rectTransform.pivot = new Vector2(.5f, 1);
            var bodyFit = body.gameObject.AddComponent<VerticalLayoutGroup>(); bodyFit.childControlHeight = true; bodyFit.childForceExpandHeight = false;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = body; scroll.horizontal = false; scroll.viewport = viewport; scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        // ------------------------------------------------------------------ pause

        void BuildPause()
        {
            pauseScreen = MakeScreen("Pause", true);
            UiKit.Image("Dim", pauseScreen.transform, null, new Color(.02f, .04f, .1f, .78f), Vector2.zero, Vector2.one).raycastTarget = true;
            Image panel = UiKit.Image("Panel", pauseScreen.transform, ArtLibrary.Rounded, UiKit.Navy, new Vector2(.12f, .26f), new Vector2(.88f, .74f));
            UiKit.Label("一時停止", panel.transform, 72, Color.white, new Vector2(.05f, .83f), new Vector2(.95f, .97f));
            UiKit.Button("再開", panel.transform, UiKit.Cyan, new Vector2(.1f, .64f), new Vector2(.9f, .8f), Resume, 60, true);
            UiKit.Button("設定", panel.transform, UiKit.NavyLight, new Vector2(.1f, .45f), new Vector2(.9f, .6f), () => { settingsReturnToPause = true; ShowMenu(settingsScreen); }, 52);
            UiKit.Button("やり直す", panel.transform, UiKit.NavyLight, new Vector2(.1f, .26f), new Vector2(.9f, .41f), () => ShowDialog("やり直す", "最初からやり直しますか？\n今回の記録とコインは保存されません。", "やり直す", StartRun, "キャンセル", true), 52);
            UiKit.Button("ホームへ", panel.transform, UiKit.NavyLight, new Vector2(.1f, .07f), new Vector2(.9f, .22f), () => ShowDialog("ホームへ戻る", "ホームに戻りますか？\n今回の記録とコインは保存されません。", "ホームへ戻る", QuitToHome, "キャンセル", true), 52);
        }

        // ------------------------------------------------------------------ dialogs & notice

        void BuildDialog()
        {
            dialog = MakeScreen("Dialog", true);
            UiKit.Image("Dim", dialog.transform, null, new Color(0, 0, 0, .62f), Vector2.zero, Vector2.one).raycastTarget = true;
            Image panel = UiKit.Image("Panel", dialog.transform, ArtLibrary.Rounded, UiKit.NavyLight, new Vector2(.08f, .3f), new Vector2(.92f, .7f));
            dialogTitle = UiKit.Label("", panel.transform, 56, Color.white, new Vector2(.06f, .82f), new Vector2(.94f, .96f));
            dialogImage = UiKit.Image("Image", panel.transform, null, Color.white, new Vector2(.35f, .5f), new Vector2(.65f, .82f), true);
            dialogText = UiKit.Label("", panel.transform, 40, Color.white, new Vector2(.06f, .26f), new Vector2(.94f, .8f));
            dialogText.resizeTextForBestFit = true;
            dialogYes = UiKit.Button("", panel.transform, UiKit.Cyan, new Vector2(.52f, .05f), new Vector2(.94f, .22f), () => { Action a = dialogAction; CloseDialog(); if (a != null) a(); }, 46, true);
            dialogYesLabel = UiKit.Label("OK", dialogYes.transform, 44, Color.white, new Vector2(.04f, .1f), new Vector2(.96f, .9f));
            dialogNo = UiKit.Button("", panel.transform, UiKit.Navy, new Vector2(.06f, .05f), new Vector2(.48f, .22f), () => { audioDirector.Play(Sfx.UiBack); Action a = dialogNoAction; CloseDialog(); if (a != null) a(); }, 44);
            dialogNoLabel = UiKit.Label("キャンセル", dialogNo.transform, 42, Color.white, new Vector2(.04f, .1f), new Vector2(.96f, .9f));
            dialog.SetActive(false);
        }

        /// <summary>Titled dialog with verb buttons. <paramref name="noLabel"/> null = single-button info dialog.</summary>
        Action dialogNoAction;

        void ShowDialog(string title, string body, string yesLabel, Action onYes, string noLabel = "キャンセル", bool danger = false, Sprite image = null, Action onNo = null)
        {
            dialogNoAction = onNo;
            dialogTitle.text = title ?? "";
            dialogText.text = body ?? "";
            dialogImage.sprite = image;
            dialogImage.enabled = image != null;
            dialogText.rectTransform.anchorMax = new Vector2(.94f, image != null ? .5f : .8f);
            dialogYesLabel.text = yesLabel ?? "OK";
            dialogAction = onYes;
            ((Image)dialogYes.targetGraphic).color = danger ? UiKit.Coral : UiKit.Cyan;
            bool single = noLabel == null;
            dialogNo.gameObject.SetActive(!single);
            ((RectTransform)dialogYes.transform).anchorMin = new Vector2(single ? .25f : .52f, .05f);
            ((RectTransform)dialogYes.transform).anchorMax = new Vector2(single ? .75f : .94f, .22f);
            if (!single) dialogNoLabel.text = noLabel;
            dialog.SetActive(true);
            dialog.transform.SetAsLastSibling();
            var panel = (RectTransform)dialog.transform.GetChild(1);
            Anim.Run(.15f, t => panel.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, Anim.OutBack(t)), null, panel);
        }

        void CloseDialog() { dialog.SetActive(false); dialogAction = null; dialogNoAction = null; }

        /// <summary>First launch: photosensitivity notice + effect strength choice (release 1-4).</summary>
        void BuildNotice()
        {
            notice = MakeScreen("Notice", true);
            UiKit.Image("Dim", notice.transform, null, new Color(.02f, .04f, .1f, .94f), Vector2.zero, Vector2.one).raycastTarget = true;
            Image panel = UiKit.Image("Panel", notice.transform, ArtLibrary.Rounded, UiKit.NavyLight, new Vector2(.06f, .2f), new Vector2(.94f, .8f));
            UiKit.Label("プレイの前に", panel.transform, 60, UiKit.Gold, new Vector2(.05f, .86f), new Vector2(.95f, .97f));
            UiKit.Label("このゲームには光の演出や画面の動きがあります。\nまれに光の刺激で体調を崩す方がいます。\n明るい部屋で、画面から離れてプレイし、\n気分が悪くなったらすぐに休んでください。\n\n演出の強さはあとから設定で変えられます。", panel.transform, 36, Color.white, new Vector2(.06f, .38f), new Vector2(.94f, .85f), TextAnchor.MiddleLeft, FontStyle.Normal);
            float y = .33f;
            Segment(panel.transform, ref y, "演出の強さ", new[] { "弱", "標準", "強" }, () => profile.effectLevel, v => profile.effectLevel = v);
            UiKit.Button("OK", panel.transform, UiKit.Cyan, new Vector2(.25f, .05f), new Vector2(.75f, .18f), () =>
            {
                profile.photosensitivityNoticeShown = true;
                Anim.Delay(.4f, CheckLoginBonus, home.transform);
                ProfileStore.TrySave(profile);
                notice.SetActive(false);
            }, 56, true);
            notice.SetActive(false);
        }

        void ShowNotice() { RefreshAll(); notice.SetActive(true); notice.transform.SetAsLastSibling(); }
    }
}
