using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;
using RhythmSlashRunner.Persistence;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Weapon shop: direct purchase only (no random draws). Cards show normal / selected / owned / locked / new states;
    /// tapping a card tries it on; buying asks for confirmation, then plays a fixed-content unboxing.
    /// </summary>
    public sealed partial class GameApp
    {
        Text weaponCoins, weaponName;
        Image weaponHero, weaponPreview;
        RectTransform weaponCoinIcon;
        string tryOnId;
        GameObject purchaseOverlay;
        bool purchasing;

        void BuildWeapon()
        {
            RectTransform content = MenuScreen("Weapon", "ショップ", out weaponScreen);
            ShopTabs(content, 1, false);
            Image coinPill = UiKit.Image("Coins", content, ArtLibrary.Pill, UiKit.NavyLight, Vector2.zero, Vector2.zero);
            UiKit.At(coinPill.rectTransform, new Vector2(1, .968f), new Vector2(250, 64), new Vector2(1, 1)).anchoredPosition = new Vector2(-46, 0);
            Image coinIcon = UiKit.Image("Icon", coinPill.transform, ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle, Color.white, new Vector2(.04f, .1f), new Vector2(.28f, .9f), true);
            weaponCoinIcon = coinIcon.rectTransform;
            weaponCoins = UiKit.Label("0", coinPill.transform, 38, UiKit.Gold, new Vector2(.3f, .05f), new Vector2(.93f, .95f), TextAnchor.MiddleRight);

            UiKit.Image("Glow", content, ArtLibrary.Glow, new Color(.5f, .95f, 1f, .45f), new Vector2(.1f, .42f), new Vector2(.9f, .86f));
            Image pedestal = UiKit.Image("Pedestal", content, ArtLibrary.Get("ui_pedestal"), Color.white, new Vector2(.18f, .41f), new Vector2(.82f, .52f), true);
            pedestal.enabled = pedestal.sprite != null;
            weaponHero = UiKit.Image("Hero", content, ArtLibrary.Get(ArtLibrary.Has(HoldPose) ? HoldPose : "hamster_front_idle"), Color.white, new Vector2(.22f, .45f), new Vector2(.78f, .83f), true);
            weaponPreview = AttachWeapon(weaponHero, profile.equippedWeapon);
            var idle = weaponHero.gameObject.AddComponent<IdleHamster>();
            idle.Idle = weaponHero.sprite; idle.Blink = ArtLibrary.Get(ArtLibrary.Has(HoldPose) ? "hamster_front_hold_blink" : "hamster_front_idle_blink");
            weaponName = UiKit.Label("", content, 44, Color.white, new Vector2(.06f, .37f), new Vector2(.94f, .42f), TextAnchor.MiddleCenter, FontStyle.Bold, true);

            CatalogItem[] weapons = catalog != null ? catalog.weapons : new CatalogItem[0];
            for (int i = 0; i < weapons.Length; i++)
            {
                CatalogItem item = weapons[i];
                // round 24: nine weapons, two rows of five
                int col = i % 5, row = i / 5;
                float x0 = .025f + col * .192f, y1 = .36f - row * .15f;
                Image card = UiKit.Image("Card", content, ArtLibrary.Rounded, UiKit.Ink, new Vector2(x0, y1 - .14f), new Vector2(x0 + .18f, y1));
                card.raycastTarget = true;
                Button tryOn = card.gameObject.AddComponent<Button>();
                tryOn.transition = Selectable.Transition.None;
                card.gameObject.AddComponent<PressFeel>();
                tryOn.onClick.AddListener(() => TryOn(item));
                Image frame = UiKit.Image("Selected", card.transform, ArtLibrary.Rounded, new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, .55f), new Vector2(-.03f, -.02f), new Vector2(1.03f, 1.02f));
                frame.transform.SetAsFirstSibling();
                Image art = UiKit.Image("Art", card.transform, ArtLibrary.Get(item.id), Color.white, new Vector2(.15f, .44f), new Vector2(.85f, .96f), true);
                if (art.sprite == null) { art.sprite = ArtLibrary.Diamond; art.color = UiKit.Cyan; }
                Image lockIcon = UiKit.Image("Lock", card.transform, ArtLibrary.Get("icon_lock") ?? ArtLibrary.Circle, Color.white, new Vector2(.62f, .74f), new Vector2(.94f, .98f), true);
                Image check = UiKit.Image("Check", card.transform, ArtLibrary.Circle, UiKit.Cyan, Vector2.zero, Vector2.zero);
                UiKit.At(check.rectTransform, new Vector2(.86f, .9f), new Vector2(54, 54));
                UiKit.Image("Mark", check.transform, ArtLibrary.Check, Color.white, Vector2.zero, Vector2.one, true);
                Image newTag = UiKit.Image("New", card.transform, ArtLibrary.Pill, UiKit.Coral, Vector2.zero, Vector2.zero);
                UiKit.At(newTag.rectTransform, new Vector2(.18f, .93f), new Vector2(92, 40));
                UiKit.Label("NEW", newTag.transform, 26, Color.white, Vector2.zero, Vector2.one);
                Text owned = UiKit.Label("所持", card.transform, 26, UiKit.Hex("5BE37D"), new Vector2(.05f, .86f), new Vector2(.45f, .98f), TextAnchor.MiddleLeft);
                UiKit.Label(item.displayName, card.transform, 30, Color.white, new Vector2(.03f, .28f), new Vector2(.97f, .43f));
                Button action = UiKit.Button("", card.transform, UiKit.Cyan, new Vector2(.06f, .04f), new Vector2(.94f, .26f), () => OnWeaponAction(item), 36);
                Text label = UiKit.Label("", action.transform, 34, Color.white, new Vector2(.04f, .1f), new Vector2(.96f, .92f));
                refreshers.Add(() =>
                {
                    bool own = profile.Owns(item.id), equipped = profile.equippedWeapon == item.id, affordable = profile.coinBalance >= item.price;
                    bool previewing = (tryOnId ?? profile.equippedWeapon) == item.id;
                    frame.enabled = previewing;
                    check.gameObject.SetActive(equipped);
                    lockIcon.enabled = false; // locks are reserved for "coming soon" items; buyable weapons show their price
                    owned.gameObject.SetActive(own && !equipped);
                    newTag.gameObject.SetActive(!profile.seenIds.Contains(item.id));
                    art.color = own || affordable ? Color.white : new Color(.55f, .55f, .62f);
                    label.text = equipped ? "装備中" : own ? "装備する" : affordable ? "★" + item.price + " 入手" : "あと " + (item.price - profile.coinBalance);
                    action.interactable = !equipped; // still pressable when short: explains how many coins are missing (UI P0 #4)
                    ((Image)action.targetGraphic).color = equipped ? UiKit.NavyLight : own ? UiKit.Cyan : affordable ? UiKit.Gold : new Color(.35f, .38f, .5f);
                });
            }
            refreshers.Add(() =>
            {
                weaponCoins.text = profile.coinBalance.ToString("N0");
                string shown = tryOnId ?? profile.equippedWeapon;
                SetWeaponSprite(weaponPreview, shown);
                CatalogItem item = CatalogQueries.Find(catalog != null ? catalog.weapons : null, shown);
                weaponName.text = item != null ? item.displayName + (tryOnId != null && tryOnId != profile.equippedWeapon ? "（試着中）" : "") : "";
            });
            UiKit.Label("武器は見た目と効果音だけが変わります（判定は共通）", content, 30, new Color(1, 1, 1, .75f), new Vector2(.06f, .015f), new Vector2(.94f, .06f));
            BuildPurchaseOverlay();
        }

        void MarkWeaponsSeen()
        {
            tryOnId = null;
            bool changed = false;
            if (catalog != null) foreach (CatalogItem w in catalog.weapons) if (!profile.seenIds.Contains(w.id)) changed = true;
            if (!changed) return;
            // NEW badges stay visible for this visit, then are remembered as seen
            Anim.Delay(1.5f, () =>
            {
                foreach (CatalogItem w in catalog.weapons) if (!profile.seenIds.Contains(w.id)) profile.seenIds.Add(w.id);
                ProfileStore.TrySave(profile);
            });
        }

        void TryOn(CatalogItem item)
        {
            tryOnId = item.id;
            RefreshAll();
            SwingPreview();
            audioDirector.Play(WeaponSound(item.id));
        }

        void SwingPreview()
        {
            RectTransform w = weaponPreview.rectTransform;
            Anim.Run(.32f, t =>
            {
                float a = t < .3f ? Mathf.Lerp(16f, 55f, t / .3f) : Mathf.Lerp(55f, -60f, Anim.OutCubic((t - .3f) / .7f));
                if (t >= 1f) a = 16f;
                w.localRotation = Quaternion.Euler(0, 0, a);
            }, () => w.localRotation = Quaternion.Euler(0, 0, 16f), w);
        }

        void OnWeaponAction(CatalogItem item)
        {
            if (purchasing) return;
            if (profile.Owns(item.id))
            {
                profile.equippedWeapon = item.id;
                tryOnId = null;
                ProfileStore.TrySave(profile);
                audioDirector.Play(WeaponSound(item.id));
                if (profile.vibration) Haptics.Play(Haptics.Strength.Light);
                RefreshAll();
                SwingPreview();
                return;
            }
            tryOnId = item.id;
            RefreshAll();
            if (profile.coinBalance < item.price)
            {
                audioDirector.Play(Sfx.UiDenied);
                int missing = item.price - profile.coinBalance;
                ShowDialog("コインが足りません", item.displayName + "まで あと " + missing + " 枚。\nランでコインを集めよう！", "プレイする", StartRun, "とじる", false, ArtLibrary.Get(item.id));
                return;
            }
            ShowDialog(item.displayName + "を入手",
                "★" + item.price + " コイン\n所持 " + profile.coinBalance + " → " + (profile.coinBalance - item.price),
                "入手する", () => Purchase(item), "やめる", false, ArtLibrary.Get(item.id));
        }

        void Purchase(CatalogItem item)
        {
            if (purchasing) return;
            int before = profile.coinBalance;
            PurchaseResult result = ProfileStore.TryPurchase(profile, item);
            if (result != PurchaseResult.Bought)
            {
                ShowDialog("入手できませんでした", result == PurchaseResult.SaveFailed ? "保存に失敗しました。もう一度お試しください。" : "コインが足りません。", "OK", null, null);
                return;
            }
            PlayUnboxing(item, before);
        }

        // ------------------------------------------------------------------ unboxing (fixed content, skippable)

        Image boxImage, boxRays, boxItem;
        Text boxGet, boxName;
        Button boxEquip, boxLater, boxSkip;
        RectTransform boxCoinLayer;

        void BuildPurchaseOverlay()
        {
            purchaseOverlay = UiKit.Rect("Unboxing", weaponScreen.transform, Vector2.zero, Vector2.one).gameObject;
            Image dim = UiKit.Image("Dim", purchaseOverlay.transform, null, new Color(.02f, .04f, .1f, .86f), Vector2.zero, Vector2.one);
            dim.raycastTarget = true;
            boxSkip = dim.gameObject.AddComponent<Button>();
            boxSkip.transition = Selectable.Transition.None;
            boxRays = UiKit.Image("Rays", purchaseOverlay.transform, ArtLibrary.Rays, new Color(1f, .85f, .4f, 0), Vector2.zero, Vector2.zero);
            UiKit.At(boxRays.rectTransform, new Vector2(.5f, .55f), new Vector2(1300, 1300));
            boxImage = UiKit.Image("Box", purchaseOverlay.transform, ArtLibrary.Get("giftbox_closed") ?? ArtLibrary.Rounded, Color.white, Vector2.zero, Vector2.zero, true);
            UiKit.At(boxImage.rectTransform, new Vector2(.5f, .5f), new Vector2(560, 560));
            boxItem = UiKit.Image("Item", purchaseOverlay.transform, null, Color.white, Vector2.zero, Vector2.zero, true);
            UiKit.At(boxItem.rectTransform, new Vector2(.5f, .6f), new Vector2(300, 520));
            boxGet = UiKit.Label("GET!", purchaseOverlay.transform, 150, UiKit.Gold, new Vector2(.1f, .72f), new Vector2(.9f, .84f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            boxName = UiKit.Label("", purchaseOverlay.transform, 56, Color.white, new Vector2(.1f, .3f), new Vector2(.9f, .36f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            boxCoinLayer = UiKit.Rect("Coins", purchaseOverlay.transform, Vector2.zero, Vector2.one);
            boxEquip = UiKit.Button("すぐ装備", purchaseOverlay.transform, UiKit.Cyan, new Vector2(.12f, .16f), new Vector2(.88f, .25f), null, 64, true);
            boxLater = UiKit.Button("あとで", purchaseOverlay.transform, UiKit.NavyLight, new Vector2(.25f, .06f), new Vector2(.75f, .13f), null, 48);
            purchaseOverlay.SetActive(false);
        }

        void PlayUnboxing(CatalogItem item, int balanceBefore)
        {
            purchasing = true;
            purchaseOverlay.SetActive(true);
            purchaseOverlay.transform.SetAsLastSibling();
            boxEquip.gameObject.SetActive(false); boxLater.gameObject.SetActive(false);
            boxGet.text = ""; boxName.text = "";
            boxItem.sprite = ArtLibrary.Get(item.id); boxItem.enabled = boxItem.sprite != null;
            boxItem.canvasRenderer.SetAlpha(.45f); boxItem.rectTransform.localScale = Vector3.one * .7f;
            boxName.text = item.displayName + " を開封！";
            boxImage.sprite = ArtLibrary.Get("giftbox_closed") ?? ArtLibrary.Rounded; boxImage.enabled = true;
            boxRays.color = new Color(1f, .85f, .4f, 0);
            bool weak = profile.Effects == EffectLevel.Weak;
            int balanceAfter = profile.coinBalance;
            bool finished = false;

            Action reveal = null;
            Action finish = () =>
            {
                if (finished) return;
                finished = true;
                Anim.Cancel(boxImage); Anim.Cancel(boxRays); Anim.Cancel(boxItem); Anim.Cancel(weaponCoins);
                foreach (Transform c in boxCoinLayer) Destroy(c.gameObject);
                weaponCoins.text = balanceAfter.ToString("N0");
                boxImage.sprite = ArtLibrary.Get("giftbox_open") ?? boxImage.sprite;
                boxImage.rectTransform.localScale = Vector3.one; boxImage.rectTransform.localRotation = Quaternion.identity;
                boxItem.enabled = boxItem.sprite != null; boxItem.rectTransform.localScale = Vector3.one; boxItem.canvasRenderer.SetAlpha(1);
                boxRays.color = new Color(1f, .85f, .4f, weak ? .25f : .55f);
                boxGet.text = "GET!"; boxName.text = item.displayName;
                boxEquip.gameObject.SetActive(true); boxLater.gameObject.SetActive(true);
            };
            boxSkip.onClick.RemoveAllListeners();
            boxSkip.onClick.AddListener(() => finish());
            boxEquip.onClick.RemoveAllListeners();
            boxEquip.onClick.AddListener(() => { CloseUnboxing(); profile.equippedWeapon = item.id; tryOnId = null; ProfileStore.TrySave(profile); RefreshAll(); SwingPreview(); audioDirector.Play(WeaponSound(item.id)); });
            boxLater.onClick.RemoveAllListeners();
            boxLater.onClick.AddListener(() => { CloseUnboxing(); tryOnId = null; RefreshAll(); });

            // 1) coins fly from the wallet into the box while the balance counts down
            audioDirector.Play(Sfx.Purchase);
            if (!weak) audioDirector.Play(Sfx.Jara, .7f);
            Vector2 from = boxCoinLayer.InverseTransformPoint(weaponCoinIcon.TransformPoint(weaponCoinIcon.rect.center));
            int flying = weak ? 0 : 14;
            for (int i = 0; i < flying; i++)
            {
                Image coin = UiKit.Image("Coin", boxCoinLayer, ArtLibrary.Get("icon_coin") ?? ArtLibrary.Circle, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
                coin.rectTransform.sizeDelta = new Vector2(70, 70);
                Vector2 to = new Vector2(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(-40f, 40f));
                Vector2 ctrl = new Vector2((from.x + to.x) * .5f + UnityEngine.Random.Range(-200f, 200f), Mathf.Max(from.y, to.y) + 250f);
                float delay = i * .035f;
                Image c = coin;
                Anim.Run(.5f + delay, t =>
                {
                    if (c == null) return; // destroyed by skip
                    float u = Mathf.Clamp01((t * (.5f + delay) - delay) / .5f);
                    Vector2 p = (1 - u) * (1 - u) * from + 2 * (1 - u) * u * ctrl + u * u * to;
                    c.rectTransform.anchoredPosition = p;
                    c.rectTransform.localScale = Vector3.one * (u >= 1 ? 0 : 1);
                });
            }
            Anim.Run(.6f, t => weaponCoins.text = Mathf.RoundToInt(Mathf.Lerp(balanceBefore, balanceAfter, t)).ToString("N0"), null, weaponCoins);

            // 2) the box shakes with rising tension, 3) bursts open with rays and the item
            Anim.Run(weak ? .2f : .75f, t =>
            {
                if (finished) return;
                float shake = weak ? 0 : Mathf.Sin(t * 60f) * 9f * t;
                boxImage.rectTransform.localRotation = Quaternion.Euler(0, 0, shake);
                boxImage.rectTransform.localScale = Vector3.one * (1f + .06f * t);
            }, () => { if (!finished) reveal(); }, boxImage);
            reveal = () =>
            {
                audioDirector.Play(Sfx.Fanfare);
                audioDirector.Play(Sfx.PerfectShimmer);
                if (profile.vibration) Haptics.Play(Haptics.Strength.Success);
                boxImage.sprite = ArtLibrary.Get("giftbox_open") ?? boxImage.sprite;
                boxImage.rectTransform.localRotation = Quaternion.identity;
                boxItem.enabled = boxItem.sprite != null;
                boxItem.canvasRenderer.SetAlpha(1);
                Anim.Run(.45f, t =>
                {
                    boxItem.rectTransform.localScale = Vector3.one * Anim.OutBack(t);
                    boxRays.color = new Color(1f, .85f, .4f, (weak ? .25f : .55f) * t);
                }, finish, boxItem);
                Anim.Run(60f, t => boxRays.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * 60f * 20f), null, boxRays); // 20°/s, no strobing
            };
        }

        void CloseUnboxing()
        {
            purchasing = false;
            foreach (Transform c in boxCoinLayer) Destroy(c.gameObject);
            Anim.Cancel(boxRays);
            purchaseOverlay.SetActive(false);
        }
    }
}
