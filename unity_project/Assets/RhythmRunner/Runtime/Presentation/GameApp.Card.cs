using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Result card (owner round 2): one shareable picture of the run — title, distance, what was cut, max combo, FEVERs,
    /// bosses. Saved as a PNG, or (round 34) handed to the phone's share sheet where the player picks the app; the game never
    /// posts by itself.
    /// </summary>
    public sealed partial class GameApp
    {
        GameObject cardOverlay;
        RectTransform cardFrame;
        Image cardBackdrop, cardHero;
        Text cardTitle, cardDistance, cardStatus, cardCombo, cardFever, cardBoss;
        readonly Image[] cardKindIcons = new Image[3];
        readonly Text[] cardKindLabels = new Text[3];
        Button cardSave, cardClose, cardShare;
        string cardTitleText = "";

        static readonly Dictionary<string, string> KindNames = new Dictionary<string, string>
        {
            { "strawberry", "いちご" }, { "watermelon", "スイカ" }, { "barrel", "たる" }, { "crate", "木箱" }, { "gem", "ダイヤ" },
            { "dice", "サイコロ" }, { "clock", "目覚まし" }, { "duck", "アヒル" }, { "baguette", "フランスパン" }, { "leek", "ネギ" },
            { "pencil", "えんぴつ" }, { "pumpkin", "カボチャ" }, { "safe", "金庫" },
        };

        static string KindName(string kind) { string n; return KindNames.TryGetValue(kind, out n) ? n : kind; }

        void BuildCard()
        {
            RectTransform overlay = UiKit.Rect("CardOverlay", resultScreen.transform, Vector2.zero, Vector2.one);
            cardOverlay = overlay.gameObject;
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(.01f, .02f, .06f, .92f);
            RectTransform safe = UiKit.Rect("Safe", overlay, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // the captured area: a 9:16 card in the upper part of the screen
            // AspectRatioFitter takes over the anchors of its own rect, so it sits in a holder that reserves room for the buttons
            RectTransform holder = UiKit.Rect("CardArea", safe, new Vector2(.06f, .145f), new Vector2(.94f, .985f));
            cardFrame = UiKit.Rect("Card", holder, Vector2.zero, Vector2.one);
            var fit = cardFrame.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 9f / 16f;
            UiKit.Image("Gold", cardFrame, ArtLibrary.Rounded, UiKit.Gold, new Vector2(-.015f, -.01f), new Vector2(1.015f, 1.01f));
            RectTransform inner = UiKit.Rect("Inner", cardFrame, Vector2.zero, Vector2.one);
            inner.gameObject.AddComponent<RectMask2D>();
            cardBackdrop = UiKit.Image("Backdrop", inner, null, Color.white, Vector2.zero, Vector2.one);
            cardBackdrop.gameObject.AddComponent<CoverFitter>();
            UiKit.Image("Shade", inner, null, new Color(.02f, .03f, .1f, .45f), Vector2.zero, Vector2.one);
            UiKit.Image("Rays", inner, ArtLibrary.Rays, new Color(1f, .85f, .45f, .35f), new Vector2(-.3f, .45f), new Vector2(1.3f, 1.15f));
            cardHero = UiKit.Image("Hero", inner, null, Color.white, new Vector2(.2f, .6f), new Vector2(.8f, .97f), true);

            UiKit.Image("Ribbon", inner, ArtLibrary.Get("record_ribbon"), Color.white, new Vector2(.02f, .5f), new Vector2(.98f, .64f), true);
            cardTitle = UiKit.Label("", inner, 64, Color.white, new Vector2(.12f, .545f), new Vector2(.88f, .615f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Label("称号", inner, 30, UiKit.Gold, new Vector2(.3f, .505f), new Vector2(.7f, .545f), TextAnchor.MiddleCenter, FontStyle.Bold, true);

            Image distPanel = UiKit.Image("DistPanel", inner, ArtLibrary.Rounded, new Color(1f, .97f, .9f, .95f), new Vector2(.06f, .4f), new Vector2(.94f, .5f));
            cardDistance = UiKit.Label("", distPanel.transform, 110, UiKit.Honey, new Vector2(.03f, .03f), new Vector2(.97f, .97f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false);
            UiKit.AddOutline(cardDistance.gameObject, new Color(.45f, .25f, .05f, .6f), 3);
            cardStatus = UiKit.Label("", inner, 30, Color.white, new Vector2(.05f, .36f), new Vector2(.95f, .4f), TextAnchor.MiddleCenter, FontStyle.Bold, true);

            UiKit.Label("斬った数", inner, 32, UiKit.Gold, new Vector2(.3f, .32f), new Vector2(.7f, .36f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            for (int i = 0; i < 3; i++)
            {
                float x0 = .05f + i * .305f;
                Image cell = UiKit.Image("Kind" + i, inner, ArtLibrary.Rounded, new Color(1f, .97f, .9f, .92f), new Vector2(x0, .2f), new Vector2(x0 + .285f, .315f));
                cardKindIcons[i] = UiKit.Image("Icon", cell.transform, null, Color.white, new Vector2(.04f, .1f), new Vector2(.46f, .9f), true);
                cardKindLabels[i] = UiKit.Label("", cell.transform, 34, UiKit.Navy, new Vector2(.46f, .05f), new Vector2(.97f, .95f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            cardCombo = StatTile(inner, 0, "MAX COMBO", new Color(.95f, .45f, .15f, .95f));
            cardFever = StatTile(inner, 1, "FEVER", new Color(.6f, .25f, .85f, .95f));
            cardBoss = StatTile(inner, 2, "ボス撃破", new Color(.15f, .35f, .85f, .95f));
            UiKit.Image("Logo", inner, ArtLibrary.Get("logo"), Color.white, new Vector2(.2f, .005f), new Vector2(.8f, .075f), true);

            // round 34: share through the phone's share sheet (the player picks where); saving stays beside it
            cardShare = UiKit.Button("シェア", safe, UiKit.Gold, new Vector2(.06f, .04f), new Vector2(.42f, .12f), () => StartCoroutine(ShareCard()), 52, true);
            cardShare.GetComponentInChildren<Text>().color = UiKit.Navy;
            cardSave = UiKit.Button("保存", safe, UiKit.Cyan, new Vector2(.45f, .04f), new Vector2(.7f, .12f), () => StartCoroutine(SaveCard()), 44);
            cardClose = UiKit.Button("閉じる", safe, UiKit.NavyLight, new Vector2(.73f, .04f), new Vector2(.94f, .12f), () => { audioDirector.Play(Sfx.UiBack); cardOverlay.SetActive(false); }, 40);
            cardOverlay.SetActive(false);
        }

        static Text StatTile(RectTransform parent, int index, string label, Color color)
        {
            float x0 = .05f + index * .305f;
            Image tile = UiKit.Image("Stat" + index, parent, ArtLibrary.Rounded, color, new Vector2(x0, .085f), new Vector2(x0 + .285f, .19f));
            UiKit.Label(label, tile.transform, 26, Color.white, new Vector2(.04f, .6f), new Vector2(.96f, .95f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            Text value = UiKit.Label("", tile.transform, 56, Color.white, new Vector2(.04f, .03f), new Vector2(.96f, .66f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            return value;
        }

        /// <summary>A title earned by the run's most remarkable stat.</summary>
        string RunTitle(RunSnapshot s, KeyValuePair<string, int> top)
        {
            int perfectRate = s.Perfect + s.Good > 0 ? 100 * s.Perfect / (s.Perfect + s.Good) : 0;
            if (s.BossesDefeated >= 3) return "ボスハンター";
            if (s.Distance >= 1000) return "伝説のランナー";
            if (s.MaxCombo >= 150) return "コンボの鬼";
            if (s.FeverCount >= 3) return "フィーバー職人";
            if (s.Stage >= 2) return "世界を駆ける者";
            if (s.BossesDefeated >= 1) return "ボスキラー";
            if (perfectRate >= 90 && s.Perfect >= 50) return "パーフェクト名人";
            if (top.Value >= 50) return KindName(top.Key) + "マスター";
            if (s.Distance >= 300) return "まな板のサムライ";
            return "見習いランナー";
        }

        void OpenCard()
        {
            if (finalSnapshot == null) return;
            RunSnapshot s = finalSnapshot;
            var ranked = new List<KeyValuePair<string, int>>(sliced);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
            KeyValuePair<string, int> top = ranked.Count > 0 ? ranked[0] : new KeyValuePair<string, int>("strawberry", 0);
            CatalogItem stage = catalog != null && catalog.stages != null && catalog.stages.Length > 0 ? catalog.stages[Mathf.Clamp(s.Theme, 0, catalog.stages.Length - 1)] : null;
            cardBackdrop.sprite = ArtLibrary.Get(stage != null && !string.IsNullOrEmpty(stage.backdropId) ? stage.backdropId : "kitchen_backdrop") ?? ArtLibrary.Get("kitchen_backdrop");
            cardHero.sprite = ArtLibrary.Get(finalNewRecord ? "hamster_front_newrecord" : "hamster_front_cheer");
            cardTitle.text = cardTitleText = RunTitle(s, top);
            cardDistance.text = s.Distance.ToString("N0") + " m";
            cardStatus.text = "STAGE " + (s.Stage + 1) + (stage != null ? "  " + stage.displayName : "") + "   SCORE " + s.Score.ToString("N0");
            for (int i = 0; i < 3; i++)
            {
                bool has = i < ranked.Count;
                cardKindIcons[i].transform.parent.gameObject.SetActive(has);
                if (!has) continue;
                cardKindIcons[i].sprite = ArtLibrary.Get(ranked[i].Key + "_intact") ?? ArtLibrary.Get("hold_" + ranked[i].Key + "_slice") ?? ArtLibrary.Circle;
                cardKindLabels[i].text = KindName(ranked[i].Key) + "\n" + ranked[i].Value;
            }
            cardCombo.text = s.MaxCombo.ToString();
            cardFever.text = s.FeverCount + "回";
            cardBoss.text = s.BossesDefeated.ToString();
            cardSave.interactable = true;
            cardOverlay.SetActive(true);
            var group = cardOverlay.GetComponent<CanvasGroup>() ?? cardOverlay.AddComponent<CanvasGroup>();
            Anim.Run(.25f, t => { group.alpha = t; cardFrame.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, Anim.OutBack(t)); }, null, group);
        }

        /// <summary>The card as a PNG (call after WaitForEndOfFrame).</summary>
        byte[] CaptureCard()
        {
            var corners = new Vector3[4];
            cardFrame.GetWorldCorners(corners); // overlay canvas: world = screen pixels
            var rect = Rect.MinMaxRect(Mathf.Max(0, corners[0].x), Mathf.Max(0, corners[0].y), Mathf.Min(Screen.width, corners[2].x), Mathf.Min(Screen.height, corners[2].y));
            var shot = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGB24, false);
            shot.ReadPixels(rect, 0, 0);
            shot.Apply();
            byte[] png = shot.EncodeToPNG();
            Destroy(shot);
            return png;
        }

        /// <summary>The post that goes with the card (the player can edit it in the app they share to).</summary>
        string ShareText()
        {
            RunSnapshot s = finalSnapshot;
            string boss = s != null && s.BossesDefeated > 0 ? "、ボス" + s.BossesDefeated + "体撃破" : "";
            return "リズムスラッシュランナーで " + (s != null ? s.Distance.ToString("N0") : "0") + "m 走った！" + boss + "\n称号「" + cardTitleText + "」 " + Sharing.Hashtag;
        }

        IEnumerator ShareCard()
        {
            cardShare.interactable = false;
            yield return new WaitForEndOfFrame();
            byte[] png = CaptureCard();
            string name = "RhythmSlashRunner_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            bool ok = Sharing.Share(png, name, ShareText());
            Analytics.Event("share", "ok", ok ? 1 : 0);
            if (!ok) { audioDirector.Play(Sfx.UiDenied); ShowDialog("シェアできませんでした", "「保存」で画像を保存してから、好きなアプリで投稿してね。", "OK", null, null); }
#if UNITY_STANDALONE || UNITY_EDITOR
            else ShowDialog("シェアの準備ができました", "画像を「ピクチャ」の RhythmSlashRunner に保存し、投稿文をコピーしました。", "OK", null, null);
#endif
            cardShare.interactable = true;
        }

        IEnumerator SaveCard()
        {
            cardSave.interactable = false;
            yield return new WaitForEndOfFrame();
            byte[] png = CaptureCard();
            string name = "RhythmSlashRunner_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            string where = null;
            try { where = WritePicture(name, png); }
            catch (Exception e) { Debug.LogException(e); }
            if (where != null)
            {
                audioDirector.Play(Sfx.Purchase);
                ShowDialog("保存しました", where, "OK", null, null);
            }
            else
            {
                audioDirector.Play(Sfx.UiDenied);
                ShowDialog("保存できませんでした", "端末の空き容量や権限を確認してください。", "OK", null, null);
            }
            cardSave.interactable = true;
        }

        /// <summary>Writes the PNG where the player can find it. Returns a human-readable location.</summary>
        static string WritePicture(string fileName, byte[] png)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android 10+: the shared Pictures collection through MediaStore (no storage permission needed)
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                if (version.GetStatic<int>("SDK_INT") >= 29)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                    using (var values = new AndroidJavaObject("android.content.ContentValues"))
                    using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
                    using (var collection = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
                    {
                        values.Call("put", "_display_name", fileName);
                        values.Call("put", "mime_type", "image/png");
                        values.Call("put", "relative_path", "Pictures/RhythmSlashRunner");
                        using (var item = resolver.Call<AndroidJavaObject>("insert", collection, values))
                        using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", item))
                        {
                            stream.Call("write", (sbyte[])(Array)png);
                            stream.Call("close");
                        }
                    }
                    return "写真アプリの「RhythmSlashRunner」アルバム";
                }
#endif
            string folder;
#if UNITY_STANDALONE || UNITY_EDITOR
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RhythmSlashRunner");
#else
            folder = Path.Combine(Application.persistentDataPath, "Pictures");
#endif
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, png);
            return path;
        }

        // automation hook
        internal void AutoOpenCard() { if (state == State.Result) OpenCard(); }
    }
}
