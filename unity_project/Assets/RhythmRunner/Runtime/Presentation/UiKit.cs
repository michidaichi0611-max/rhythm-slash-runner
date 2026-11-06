using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>Small helpers to build the code-generated uGUI screens (reference resolution 1080x1920).</summary>
    public static class UiKit
    {
        public static readonly Color Navy = Hex("0B1733");
        public static readonly Color NavyLight = Hex("16294F");
        public static readonly Color Cyan = Hex("20D9F3");
        public static readonly Color Coral = Hex("FF5B61");
        public static readonly Color Gold = Hex("FFC84A");
        public static readonly Color Honey = Hex("D98A2B");
        public static readonly Color Cream = Hex("FFE3B5");
        public static readonly Color Offbeat = Hex("E98CFF");
        public static readonly Color Ink = new Color(.04f, .07f, .16f, .78f);

        static Font font, heavy, osFallback;
        public static System.Action<bool> OnButtonSound;

        /// <summary>Body font: bundled M PLUS Rounded 1c Bold (OFL), OS Japanese font as fallback.</summary>
        public static Font Font { get { return font ?? (font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Bold") ?? OsFont); } }

        /// <summary>Titles and numbers: M PLUS Rounded 1c ExtraBold.</summary>
        public static Font Heavy { get { return heavy ?? (heavy = Resources.Load<Font>("Fonts/MPLUSRounded1c-ExtraBold") ?? Font); } }

        static Font OsFont
        {
            get
            {
                if (osFallback != null) return osFallback;
                osFallback = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "Noto Sans CJK JP", "Noto Sans JP", "Hiragino Sans", "Roboto", "Arial" }, 48);
                if (osFallback == null) osFallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return osFallback;
            }
        }

        /// <summary>Real weights instead of synthetic bold: Bold → ExtraBold face, Normal → Bold face; italics stay synthetic.</summary>
        public static void ApplyFont(Text label, FontStyle style)
        {
            bool heavyFace = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;
            label.font = heavyFace ? Heavy : Font;
            label.fontStyle = style == FontStyle.BoldAndItalic || style == FontStyle.Italic ? FontStyle.Italic : FontStyle.Normal;
        }

        public static Color Hex(string value) { Color c; return ColorUtility.TryParseHtmlString("#" + value, out c) ? c : Color.white; }

        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Rect anchored at a normalized point with a fixed size in reference pixels.</summary>
        public static RectTransform At(RectTransform rect, Vector2 anchor, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 min, Vector2 max, bool preserveAspect = false)
        {
            RectTransform rect = Rect(name, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.preserveAspect = preserveAspect; image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        public static Text Label(string text, Transform parent, int size, Color color, Vector2 min, Vector2 max, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold, bool outline = false)
        {
            RectTransform rect = Rect("Text", parent, min, max);
            var label = rect.gameObject.AddComponent<Text>();
            ApplyFont(label, style); label.text = L10n.T(text); label.fontSize = size; label.color = color; label.alignment = anchor;
            rect.gameObject.AddComponent<L10nText>(); // round 34: English at display time
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = Mathf.Max(10, size / 3); label.resizeTextMaxSize = size;
            if (outline) AddOutline(label.gameObject, new Color(.03f, .06f, .14f, .9f), 4);
            return label;
        }

        public static void AddOutline(GameObject go, Color color, float size)
        {
            var o = go.AddComponent<Outline>(); o.effectColor = color; o.effectDistance = new Vector2(size, -size);
            var s = go.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .35f); s.effectDistance = new Vector2(0, -size * 1.6f);
        }

        public static Button Button(string label, Transform parent, Color color, Vector2 min, Vector2 max, UnityAction onClick, int fontSize = 56, bool primary = false)
        {
            Image image = Image("Button_" + label, parent, ArtLibrary.Button, color, min, max);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(.85f, .85f, .85f, 1); colors.disabledColor = new Color(.55f, .55f, .6f, .7f); colors.fadeDuration = .05f;
            button.colors = colors;
            button.onClick.AddListener(() => { if (OnButtonSound != null) OnButtonSound(primary); });
            button.onClick.AddListener(onClick);
            image.gameObject.AddComponent<PressFeel>();
            if (!string.IsNullOrEmpty(label))
            {
                Text text = Label(label, image.transform, fontSize, Color.white, new Vector2(.04f, .1f), new Vector2(.96f, .92f), TextAnchor.MiddleCenter, FontStyle.Bold);
                AddOutline(text.gameObject, new Color(0, 0, 0, .25f), 2);
                text.name = "Label";
            }
            return button;
        }

        public static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        /// <summary>Aspect-fill a sprite image inside its parent (like CSS background-size: cover).</summary>
        public static void Cover(Image image, Vector2 parentSize)
        {
            if (image.sprite == null || parentSize.x <= 0 || parentSize.y <= 0) return;
            float sw = image.sprite.rect.width, sh = image.sprite.rect.height;
            float scale = Mathf.Max(parentSize.x / sw, parentSize.y / sh);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(sw * scale, sh * scale);
        }
    }

    /// <summary>Scale-down feedback for pressed buttons (normal / pressed / disabled states).</summary>
    public sealed class PressFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float target = 1f;
        public void OnPointerDown(PointerEventData e) { var b = GetComponent<Selectable>(); if (b == null || b.interactable) target = .95f; }
        public void OnPointerUp(PointerEventData e) { target = 1f; }
        public void OnPointerExit(PointerEventData e) { target = 1f; }
        void Update()
        {
            float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(s, s, 1);
        }
    }
}
