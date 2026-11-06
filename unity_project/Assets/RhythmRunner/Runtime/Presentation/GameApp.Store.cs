using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;
using RhythmSlashRunner.Persistence;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>Calls an action every frame (live countdowns on menu screens).</summary>
    public sealed class Ticker : MonoBehaviour
    {
        public Action Tick;
        void Update() { if (Tick != null) Tick(); }
    }

    /// <summary>
    /// Round 32 (owner): plays and energy drinks, one shop for characters / weapons / items / specials, the weekly
    /// ranking and the event tab. Rules live in <see cref="Energy"/> and <see cref="Weekly"/>; real money goes through
    /// <see cref="IPurchases"/>.
    /// </summary>
    public sealed partial class GameApp
    {
        GameObject itemScreen, specialScreen, rankingScreen, eventScreen;
        readonly IPurchases purchases = new StorePurchases();

        /// <summary>Round 33: connects the store; every paid order gives its drinks here (also ones finished on a later launch).</summary>
        void InitStore()
        {
            purchases.Granted = id =>
            {
                int n = Products.DrinksIn(id);
                if (n <= 0) return;
                profile.drinks += n;
                ProfileStore.TrySave(profile);
                Analytics.Event("drink_purchase", "product", id, "drinks", n);
                RefreshAll();
            };
            purchases.Connect();
        }
        bool refundedThisRun;

        static string Clock(TimeSpan t) { return ((int)t.TotalMinutes).ToString("00") + ":" + t.Seconds.ToString("00"); }

        string EnergyLine(bool withDrinks)
        {
            DateTime now = DateTime.UtcNow;
            string line;
            if (Energy.FreePeriod(profile, now))
            {
                int days = Energy.FreeDays - (int)TimeSpan.FromTicks(now.Ticks - profile.firstPlayTicks).TotalDays;
                line = "あと" + Mathf.Max(1, days) + "日 遊びほうだい！";
            }
            else
            {
                int n = Energy.Current(profile, now);
                line = "プレイ " + n + "/" + Energy.Max + (n < Energy.Max ? "  あと" + Clock(Energy.UntilNext(profile, now)) + "で+1" : "  満タン");
            }
            return withDrinks ? line + "\nエナジードリンク ×" + profile.drinks : line;
        }

        // ------------------------------------------------------------------ home: plays left, the play gate

        /// <summary>The plays line over the play button; "+" drinks or opens the shop.</summary>
        void BuildEnergyHud(RectTransform content)
        {
            Image pill = UiKit.Image("Energy", content, ArtLibrary.Pill, UiKit.Ink, new Vector2(.12f, .272f), new Vector2(.88f, .312f));
            Image icon = UiKit.Image("Drink", pill.transform, ArtLibrary.Get("item_drink") ?? ArtLibrary.Circle, ArtLibrary.Has("item_drink") ? Color.white : UiKit.Cyan, new Vector2(.01f, -.2f), new Vector2(.1f, 1.2f), true);
            Text label = UiKit.Label("", pill.transform, 34, Color.white, new Vector2(.11f, .05f), new Vector2(.84f, .95f), TextAnchor.MiddleLeft, FontStyle.Bold);
            label.resizeTextForBestFit = false;
            Button plus = UiKit.Button("+", pill.transform, UiKit.Gold, new Vector2(.86f, .08f), new Vector2(.99f, .92f), () => { audioDirector.Play(Sfx.UiConfirm); OfferDrink(); }, 40);
            plus.GetComponentInChildren<Text>().color = UiKit.Navy;
            pill.gameObject.AddComponent<Ticker>().Tick = () =>
            {
                if (Time.frameCount % 15 != 0) return;
                label.text = EnergyLine(false);
            };
        }

        /// <summary>Starting a run uses one play (round 32); with none left, offers a drink or the shop.</summary>
        bool UsePlay()
        {
            if (Automation.Enabled) return true;
            if (Energy.TryUse(profile, DateTime.UtcNow)) return true;
            Analytics.Event("plays_empty", "drinks", profile.drinks);
            audioDirector.Play(Sfx.UiDenied);
            OfferDrink();
            return false;
        }

        void OfferDrink()
        {
            DateTime now = DateTime.UtcNow;
            bool full = Energy.FreePeriod(profile, now) || Energy.Current(profile, now) >= Energy.Max;
            if (full) { ShowDialog("プレイ回数", EnergyLine(true), "ショップへ", () => OpenShop(2), "とじる", false, ArtLibrary.Get("item_drink")); return; }
            if (profile.drinks > 0)
            {
                ShowDialog("エナジードリンクを飲む？", EnergyLine(true) + "\n\n1本でプレイ回数が 満タンに！", "飲む", DrinkNow, "やめる", false, ArtLibrary.Get("item_drink"));
                return;
            }
            ShowDialog("プレイ回数がありません", EnergyLine(true) + "\n\nエナジードリンクで すぐ満タンに。", "ショップへ", () => OpenShop(2), "とじる", false, ArtLibrary.Get("item_drink"));
        }

        void DrinkNow()
        {
            if (!Energy.Drink(profile, DateTime.UtcNow)) return;
            ProfileStore.TrySave(profile);
            Analytics.Event("drink_used", "left", profile.drinks);
            audioDirector.Play(Sfx.Heal);
            if (profile.vibration) Haptics.Play(Haptics.Strength.Success);
            RefreshAll();
            ShowDialog("ゴクッ！", "プレイ回数が 満タンになった！\n" + EnergyLine(true), "OK", null, null, false, ArtLibrary.Get("item_drink"));
        }

        /// <summary>Round 34: asked once — may the game send anonymous play data? (No is the default if the app is closed.)</summary>
        bool AskConsent()
        {
            if (profile.analyticsConsent != 0 || Automation.Enabled) return false;
            profile.analyticsConsent = 2;
            ProfileStore.TrySave(profile);
            ShowDialog("ゲームの改善にご協力ください",
                "どこで失敗しやすいか などの遊び方の記録を、名前などの個人情報なしで送ってもいいですか？\nあとから設定で いつでも止められます。",
                "送ってOK", () => { profile.analyticsConsent = 1; ProfileStore.TrySave(profile); Analytics.Start(); CheckLoginBonus(); },
                "送らない", false, null, CheckLoginBonus);
            return true;
        }

        /// <summary>Once a day on the home screen: one drink.</summary>
        void CheckLoginBonus()
        {
            if (Automation.Enabled || !profile.photosensitivityNoticeShown) return;
            if (AskConsent()) return; // the bonus comes right after the answer
            if (!Energy.LoginBonus(profile, Today())) return;
            ProfileStore.TrySave(profile);
            audioDirector.Play(Sfx.ItemGet);
            ShowDialog("ログインボーナス", "エナジードリンク +1\n（所持 " + profile.drinks + "本）\n\nまた明日も来てね！", "OK", null, null, false, ArtLibrary.Get("item_drink"));
        }

        /// <summary>Run end: this week's best, the platform board, a play back for a run lost to a boss.</summary>
        void OnRunFinishedStore(RunSnapshot s, bool lostInBossFight)
        {
            Weekly.Record(profile, s.Distance, DateTime.Now);
            Ranking.Submit(s.Distance);
            refundedThisRun = false;
            if (lostInBossFight && s.Hp <= 0 && !Automation.Enabled)
            {
                Energy.Refund(profile, DateTime.UtcNow);
                refundedThisRun = !Energy.FreePeriod(profile, DateTime.UtcNow);
            }
        }

        // ------------------------------------------------------------------ shop: one place, four tabs

        static readonly string[] ShopTabNames = { "キャラ", "武器", "アイテム", "特別" };

        void OpenShop(int tab)
        {
            ShowMenu(tab == 0 ? characterScreen : tab == 1 ? weaponScreen : tab == 2 ? itemScreen : specialScreen);
            if (tab == 1) MarkWeaponsSeen();
        }

        /// <summary>The tab bar under the header of every shop page, and the coin wallet in the header.</summary>
        void ShopTabs(RectTransform content, int active, bool wallet = true)
        {
            int tabs = Features.ComingSoon ? ShopTabNames.Length : 3; // round 36: the "特別" tab waits for its items
            float pitch = .95f / tabs;
            for (int i = 0; i < tabs; i++)
            {
                int tab = i;
                float x0 = .03f + i * pitch;
                Button b = UiKit.Button(ShopTabNames[i], content, i == active ? UiKit.Cyan : UiKit.NavyLight, new Vector2(x0, .842f), new Vector2(x0 + pitch - .0125f, .892f), () =>
                {
                    if (tab == active) return;
                    audioDirector.Play(Sfx.UiSelect);
                    OpenShop(tab);
                }, 38);
                if (i == 1)
                {
                    Image badge = UiKit.Image("Badge", b.transform, ArtLibrary.Circle, UiKit.Coral, Vector2.zero, Vector2.zero);
                    UiKit.At(badge.rectTransform, new Vector2(.92f, .85f), new Vector2(34, 34));
                    refreshers.Add(() => badge.gameObject.SetActive(HasAffordableUnowned()));
                }
            }
            if (!wallet) return;
            Image pill = UiKit.Image("Wallet", content, ArtLibrary.Pill, UiKit.NavyLight, Vector2.zero, Vector2.zero);
            UiKit.At(pill.rectTransform, new Vector2(1, .968f), new Vector2(250, 64), new Vector2(1, 1)).anchoredPosition = new Vector2(-46, 0);
            UiKit.Image("Coin", pill.transform, ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle, Color.white, new Vector2(.04f, .08f), new Vector2(.27f, .92f), true);
            Text coins = UiKit.Label("0", pill.transform, 38, UiKit.Gold, new Vector2(.3f, .05f), new Vector2(.93f, .95f), TextAnchor.MiddleRight, FontStyle.Bold);
            refreshers.Add(() => coins.text = profile.coinBalance.ToString("N0"));
        }

        void BuildItemShop()
        {
            RectTransform content = MenuScreen("Items", "ショップ", out itemScreen);
            ShopTabs(content, 2);
            // what the player has now
            Image status = UiKit.Image("Status", content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(.05f, .63f), new Vector2(.95f, .825f));
            UiKit.Image("Drink", status.transform, ArtLibrary.Get("item_drink") ?? ArtLibrary.Circle, ArtLibrary.Has("item_drink") ? Color.white : UiKit.Cyan, new Vector2(.02f, .08f), new Vector2(.26f, .92f), true);
            Text line = UiKit.Label("", status.transform, 38, Color.white, new Vector2(.28f, .38f), new Vector2(.97f, .95f), TextAnchor.MiddleLeft, FontStyle.Bold, true);
            line.resizeTextForBestFit = false;
            Button drink = UiKit.Button("飲んで満タン", status.transform, UiKit.Cyan, new Vector2(.3f, .08f), new Vector2(.94f, .36f), () => { audioDirector.Play(Sfx.UiConfirm); OfferDrink(); }, 40);
            status.gameObject.AddComponent<Ticker>().Tick = () => { if (Time.frameCount % 15 == 0) line.text = EnergyLine(true); };

            ProductRow(content, .44f, "エナジードリンク 1本", "★" + Energy.DrinkCoinPrice + " コイン", UiKit.Gold, () =>
            {
                if (!Energy.BuyDrinkWithCoins(profile))
                {
                    audioDirector.Play(Sfx.UiDenied);
                    ShowDialog("コインが足りません", "あと " + (Energy.DrinkCoinPrice - profile.coinBalance) + " 枚。\nランでコインを集めよう！", "OK", null, null);
                    return;
                }
                ProfileStore.TrySave(profile);
                Analytics.Event("drink_coins", "coins_left", profile.coinBalance);
                audioDirector.Play(Sfx.Purchase);
                RefreshAll();
            });
            GameObject pack3 = ProductRow(content, .27f, "エナジードリンク 3本パック", null, UiKit.Coral, () => BuyPack(Products.Drink3), Products.Drink3);
            GameObject pack10 = ProductRow(content, .10f, "エナジードリンク 10本パック", null, UiKit.Coral, () => BuyPack(Products.Drink10), Products.Drink10);
            // round 36: until the store answers, the paid packs are replaced by how to get drinks for free
            Image ways = UiKit.Image("Ways", content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(.05f, .1f), new Vector2(.95f, .42f));
            UiKit.Label("ドリンクの手に入れ方", ways.transform, 44, UiKit.Gold, new Vector2(.05f, .78f), new Vector2(.95f, .95f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Label("・毎日のログインボーナスで 1本\n・その日 はじめてボスを倒すと 1本\n・ボスにやられたら そのプレイは回数を使わない\n・コイン " + Energy.DrinkCoinPrice + "枚で 1本と交換", ways.transform, 36, Color.white, new Vector2(.07f, .08f), new Vector2(.95f, .74f), TextAnchor.MiddleLeft, FontStyle.Normal, true);
            refreshers.Add(() =>
            {
                bool paid = purchases.Available || Features.ComingSoon;
                pack3.SetActive(paid);
                pack10.SetActive(paid);
                ways.gameObject.SetActive(!paid);
            });
            UiKit.Label("ドリンク1本で プレイ回数が満タン（" + Energy.Max + "回）。プレイ回数は " + Energy.RegenMinutes + "分で1回 回復します。", content, 28, new Color(1, 1, 1, .75f), new Vector2(.05f, .02f), new Vector2(.95f, .07f));
        }

        /// <summary>A shop row; with <paramref name="productId"/> the price comes from the store (localised) once it answers.</summary>
        GameObject ProductRow(RectTransform content, float y0, string name, string price, Color color, Action buy, string productId = null)
        {
            Image row = UiKit.Image("Product", content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(.05f, y0), new Vector2(.95f, y0 + .15f));
            UiKit.Image("Drink", row.transform, ArtLibrary.Get("item_drink") ?? ArtLibrary.Circle, ArtLibrary.Has("item_drink") ? Color.white : UiKit.Cyan, new Vector2(.02f, .1f), new Vector2(.2f, .9f), true);
            UiKit.Label(name, row.transform, 40, Color.white, new Vector2(.22f, .5f), new Vector2(.98f, .92f), TextAnchor.MiddleLeft, FontStyle.Bold);
            Button b = UiKit.Button(price ?? "準備中", row.transform, color, new Vector2(.55f, .08f), new Vector2(.97f, .46f), () => { audioDirector.Play(Sfx.UiConfirm); buy(); }, 40);
            Text label = b.GetComponentInChildren<Text>();
            label.color = color == UiKit.Gold ? UiKit.Navy : Color.white; // round 35: navy on coral was hard to read
            if (productId != null) refreshers.Add(() => label.text = purchases.Available ? purchases.PriceOf(productId) : "準備中");
            if (productId != null) label.text = purchases.Available ? purchases.PriceOf(productId) : "準備中";
            return row.gameObject;
        }

        void BuyPack(string productId)
        {
            if (!purchases.Available) { ShowDialog("準備中", "お金での購入は 準備中です（ストアにつながっていません）。\nコインでの購入や ログインボーナスを使ってね。", "OK", null, null); return; }
            int n = Products.DrinksIn(productId);
            ShowDialog("エナジードリンク " + n + "本", purchases.PriceOf(productId) + " で購入します。", "購入する", () => purchases.Buy(productId, (ok, error) =>
            {
                if (!ok) { ShowDialog("購入できませんでした", error ?? "もう一度お試しください。", "OK", null, null); return; }
                // the drinks were given by Granted (it runs for every paid order)
                audioDirector.Play(Sfx.Purchase);
                RefreshAll();
                ShowDialog("ありがとう！", "エナジードリンク +" + n + "\n（所持 " + profile.drinks + "本）", "OK", null, null, false, ArtLibrary.Get("item_drink"));
            }), "やめる");
        }

        void BuildSpecialShop()
        {
            RectTransform content = MenuScreen("Special", "ショップ", out specialScreen);
            ShopTabs(content, 3);
            UiKit.Image("Panel", content, ArtLibrary.Rounded, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, .82f), new Vector2(.05f, .18f), new Vector2(.95f, .45f)); // round 35: text stays readable over the kitchen
            Image box = UiKit.Image("Gift", content, ArtLibrary.Get("giftbox_closed") ?? ArtLibrary.Rounded, Color.white, new Vector2(.25f, .45f), new Vector2(.75f, .78f), true);
            box.gameObject.AddComponent<Breathe>().Amount = .03f;
            UiKit.Label("限定アイテム", content, 64, UiKit.Gold, new Vector2(.05f, .35f), new Vector2(.95f, .43f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Label("イベントの景品や 限定の武器・キャラが\nここに並びます。（準備中）", content, 40, Color.white, new Vector2(.06f, .2f), new Vector2(.94f, .34f), TextAnchor.MiddleCenter, FontStyle.Normal, true);
        }

        // ------------------------------------------------------------------ weekly ranking, events

        void BuildRanking()
        {
            RectTransform content = MenuScreen("Ranking", "週間ランキング", out rankingScreen);
            UiKit.Image("Panel", content, ArtLibrary.Rounded, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, .82f), new Vector2(.05f, .1f), new Vector2(.95f, .62f)); // round 35: text stays readable over the kitchen
            UiKit.Image("Crown", content, ArtLibrary.Get("icon_crown") ?? ArtLibrary.Star, Color.white, new Vector2(.32f, .62f), new Vector2(.68f, .86f), true);
            UiKit.Label("今週のベスト", content, 48, Color.white, new Vector2(.05f, .54f), new Vector2(.95f, .6f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            Text best = UiKit.Label("", content, 120, UiKit.Gold, new Vector2(.05f, .42f), new Vector2(.95f, .54f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            Text reset = UiKit.Label("", content, 36, new Color(1, 1, 1, .8f), new Vector2(.05f, .36f), new Vector2(.95f, .41f), TextAnchor.MiddleCenter, FontStyle.Normal, true);
            Text allTime = UiKit.Label("", content, 34, UiKit.Cyan, new Vector2(.05f, .31f), new Vector2(.95f, .36f), TextAnchor.MiddleCenter, FontStyle.Normal, true);
            refreshers.Add(() =>
            {
                int b = Weekly.Best(profile, DateTime.Now);
                best.text = b > 0 ? b + " m" : "-- m";
                reset.text = "月曜日にリセット（あと " + Weekly.DaysLeft(DateTime.Now) + " 日）";
                allTime.text = "これまでのベスト " + (profile.bestDistance > 0 ? profile.bestDistance + " m" : "--");
            });
            UiKit.Button("ランキングを見る", content, UiKit.Cyan, new Vector2(.12f, .19f), new Vector2(.88f, .29f), () =>
            {
                audioDirector.Play(Sfx.UiConfirm);
                if (Ranking.Show()) return;
                string why = Ranking.Supported ? "Game Center に サインインしてから もう一度押してね。"
                    : Application.platform == RuntimePlatform.Android ? "Android版のランキングは 準備中です（Google Play ゲームズとの連携が必要）。"
                    : "ランキングは スマホ版で見られます。";
                ShowDialog("ランキング", why, "OK", null, null);
            }, 56, true);
            UiKit.Label("iPhone と Android は 別々のランキングです。", content, 30, new Color(1, 1, 1, .75f), new Vector2(.05f, .12f), new Vector2(.95f, .17f));
        }

        void BuildEvent()
        {
            RectTransform content = MenuScreen("Event", "イベント", out eventScreen);
            UiKit.Image("Panel", content, ArtLibrary.Rounded, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, .82f), new Vector2(.05f, .14f), new Vector2(.95f, .5f)); // round 35: text stays readable over the kitchen
            Image art = UiKit.Image("Art", content, ArtLibrary.Get("giftbox_open") ?? ArtLibrary.Get("giftbox_closed") ?? ArtLibrary.Rounded, Color.white, new Vector2(.2f, .5f), new Vector2(.8f, .85f), true);
            art.gameObject.AddComponent<Breathe>().Amount = .03f;
            UiKit.Label("季節のイベント", content, 64, UiKit.Gold, new Vector2(.05f, .41f), new Vector2(.95f, .49f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Label("期間中に流れてくる 季節の特別な物を斬って\nポイントを集めよう！\n終わったら 限定の武器やキャラと交換できるよ。\n\n（ただいま準備中）", content, 38, Color.white, new Vector2(.06f, .16f), new Vector2(.94f, .4f), TextAnchor.MiddleCenter, FontStyle.Normal, true);
        }
    }
}
