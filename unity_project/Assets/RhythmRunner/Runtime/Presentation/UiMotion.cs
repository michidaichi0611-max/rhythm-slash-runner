using UnityEngine;
using UnityEngine.UI;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>Slow scale breathing (scale only — never brightness).</summary>
    public sealed class Breathe : MonoBehaviour
    {
        public float Amount = .03f, Period = 1.2f;
        Vector3 baseScale = Vector3.one;
        void OnEnable() { baseScale = Vector3.one; }
        void Update() { float s = 1f + Amount * (.5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Period)); transform.localScale = baseScale * s; }
    }

    /// <summary>A soft highlight sweeping across a button every few seconds (UI P1 #10).</summary>
    public sealed class Sheen : MonoBehaviour
    {
        public float Interval = 3f;
        RectTransform band;
        void Start()
        {
            var mask = gameObject.AddComponent<RectMask2D>();
            mask.padding = new Vector4(8, 8, 8, 8);
            Image img = UiKit.Image("Sheen", transform, ArtLibrary.Glow, new Color(1, 1, 1, .35f), new Vector2(0, -.4f), new Vector2(0, 1.4f));
            band = img.rectTransform;
            band.sizeDelta = new Vector2(120, 0);
            band.localRotation = Quaternion.Euler(0, 0, -20);
            band.SetAsLastSibling();
        }
        void Update()
        {
            if (band == null) return;
            float t = Mathf.Repeat(Time.unscaledTime, Interval) / .6f;
            var parent = (RectTransform)transform;
            band.gameObject.SetActive(t < 1f);
            band.anchoredPosition = new Vector2(Mathf.Lerp(-80, parent.rect.width + 80, t), 0);
        }
    }

    /// <summary>Loops sprites at a fixed rate (splash loader).</summary>
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        public Sprite[] Frames;
        public float Fps = 8f;
        Image image;
        void Awake() { image = GetComponent<Image>(); }
        void Update()
        {
            if (Frames == null || Frames.Length == 0 || image == null) return;
            Sprite s = Frames[(int)(Time.unscaledTime * Fps) % Frames.Length];
            if (s != null) image.sprite = s;
        }
    }

    /// <summary>Menu hamster: breathing from the feet, random blinks every 3–5 s, cheer reaction on tap (character #11).</summary>
    public sealed class IdleHamster : MonoBehaviour
    {
        public Sprite Idle, Blink, Cheer;
        public Image Weapon;
        Image image;
        float nextBlink, blinkUntil, cheerUntil;

        void Awake() { image = GetComponent<Image>(); nextBlink = Time.unscaledTime + Random.Range(3f, 5f); }

        void OnEnable()
        {
            // breathe from the feet: stretched rects keep their edges when only the pivot changes
            var rect = (RectTransform)transform;
            Vector2 min = rect.offsetMin, max = rect.offsetMax;
            rect.pivot = new Vector2(.5f, 0f);
            rect.offsetMin = min; rect.offsetMax = max;
        }

        public void React()
        {
            cheerUntil = Time.unscaledTime + .45f;
            Anim.Run(.35f, t => transform.localScale = new Vector3(1f - .08f * Anim.Punch(t), 1f + .12f * Anim.Punch(t), 1), null, this);
        }

        void Update()
        {
            if (image == null) return;
            float now = Time.unscaledTime;
            if (now > nextBlink) { blinkUntil = now + .12f; nextBlink = now + Random.Range(3f, 5f); }
            Sprite s = now < cheerUntil && Cheer != null ? Cheer : now < blinkUntil && Blink != null ? Blink : Idle;
            if (s != null && image.sprite != s) image.sprite = s;
            if (Weapon != null) Weapon.enabled = Weapon.sprite != null && !(now < cheerUntil && Cheer != null);
            if (now >= cheerUntil)
            {
                float breathe = 1f + .015f * Mathf.Sin(now * Mathf.PI * 2f / 2.4f);
                transform.localScale = new Vector3(1f, breathe, 1f);
            }
        }
    }
}
