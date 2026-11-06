using System.Collections.Generic;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Sprites from Resources/Art plus procedural UI shapes. A missing art file falls back to a tinted shape so the
    /// game stays playable while assets are being replaced.
    /// </summary>
    public static class ArtLibrary
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Sprite circle, ring, diamond, rounded, button, glow, pill, softShadow;

        /// <summary>
        /// Sprite prefix of the equipped character ("hamster_", "cat_", "robot_"). Every "hamster_*" request is answered
        /// with the same pose of the equipped character when it exists, so all screens follow the character choice.
        /// </summary>
        public static string CharacterPrefix = "hamster_";

        public static string PrefixFor(string characterId)
        {
            if (characterId == "character_cat") return "cat_";
            if (characterId == "character_robot") return "robot_";
            if (characterId == "character_penguin") return "penguin_";
            if (characterId == "character_shiba") return "shiba_";
            if (characterId == "character_otottsan") return "otottsan_";
            if (characterId == "character_toumei") return "toumei_";
            if (characterId == "character_tori") return "tori_";
            if (characterId == "character_whamster") return "whamster_";
            if (characterId == "character_shark") return "shark_";
            return "hamster_";
        }

        public static Sprite Get(string name)
        {
            if (CharacterPrefix != "hamster_" && name.StartsWith("hamster_"))
            {
                Sprite mapped = Load(CharacterPrefix + name.Substring(8), false);
                if (mapped != null) return mapped;
            }
            return Load(name, true);
        }

        static Sprite Load(string name, bool warn)
        {
            Sprite sprite;
            if (cache.TryGetValue(name, out sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Art/" + name);
            if (sprite == null && warn) Debug.LogWarning("Missing art: Resources/Art/" + name + ".png (using a placeholder shape)");
            cache[name] = sprite;
            return sprite;
        }

        public static bool Has(string name) { return Get(name) != null; }

        /// <summary>The sprite with exactly this name, never swapped for the equipped character (character cards).</summary>
        public static Sprite GetExact(string name) { return Load(name, true); }

        public static Texture2D Texture(string name) { return Resources.Load<Texture2D>("Art/" + name); }

        public static Sprite Circle { get { return circle ?? (circle = Shape("circle", 128, (x, y) => Disc(x, y, .5f, 1f), Vector4.zero)); } }
        public static Sprite Ring { get { return ring ?? (ring = Shape("ring", 256, (x, y) => Disc(x, y, .5f, 1f) * (1 - Disc(x, y, .42f, 1f)), Vector4.zero)); } }
        public static Sprite Glow { get { return glow ?? (glow = Shape("glow", 128, (x, y) => { float d = Dist(x, y) / .5f; return Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d); }, Vector4.zero)); } }
        public static Sprite SoftShadow { get { return softShadow ?? (softShadow = Shape("shadow", 128, (x, y) => { float d = Dist(x, y) / .5f; return Mathf.Clamp01(1 - d * d) * .8f; }, Vector4.zero)); } }

        public static Sprite Diamond
        {
            get
            {
                return diamond ?? (diamond = Shape("diamond", 256, (x, y) =>
                {
                    float d = Mathf.Abs(x - .5f) + Mathf.Abs(y - .5f);
                    return Mathf.Clamp01((.5f - d) * 256) * Mathf.Clamp01((d - .43f) * 256);
                }, Vector4.zero));
            }
        }

        /// <summary>9-sliced rounded rectangle, radius 32px.</summary>
        public static Sprite Rounded { get { return rounded ?? (rounded = Shape("rounded", 128, (x, y) => RoundRect(x, y, 128, 32), new Vector4(40, 40, 40, 40))); } }

        /// <summary>Fully rounded capsule for pills and badges.</summary>
        public static Sprite Pill { get { return pill ?? (pill = Shape("pill", 128, (x, y) => RoundRect(x, y, 128, 63), new Vector4(63, 63, 63, 63))); } }

        /// <summary>Glossy 9-sliced button: lighter top, darker bottom rim; tint with Image.color.</summary>
        public static Sprite Button
        {
            get
            {
                if (button != null) return button;
                const int size = 160;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "button" };
                var px = new Color[size * size];
                for (int j = 0; j < size; j++)
                    for (int i = 0; i < size; i++)
                    {
                        float x = (i + .5f) / size, y = (j + .5f) / size;
                        float a = RoundRect(x, y, size, 44);
                        float body = RoundRect(x, y + .05f, size, 44); // top part; bottom 5% shows as darker rim
                        float shade = Mathf.Lerp(.72f, 1f, y);
                        float rim = body > .5f ? 1f : .62f;
                        float gloss = y > .62f ? Mathf.Lerp(0, .18f, (y - .62f) / .38f) : 0;
                        float v = Mathf.Clamp01(shade * rim + gloss);
                        px[j * size + i] = new Color(v, v, v, a);
                    }
                tex.SetPixels(px); tex.Apply();
                button = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(52, 52, 52, 52));
                return button;
            }
        }

        static Sprite star, play, edge, rays, checkmark;

        public static Sprite Star
        {
            get
            {
                return star ?? (star = Shape("star", 256, (x, y) =>
                {
                    float dx = x - .5f, dy = y - .5f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx) + Mathf.PI / 2;
                    float k = Mathf.Cos(Mathf.PI / 5) / Mathf.Cos(Mathf.Repeat(a, 2 * Mathf.PI / 5) - Mathf.PI / 5);
                    float edge = Mathf.Lerp(.22f, .48f, Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 6)) * k;
                    return Mathf.Clamp01((edge - r) * 256);
                }, Vector4.zero));
            }
        }

        /// <summary>Right-pointing play triangle.</summary>
        public static Sprite Play { get { return play ?? (play = Shape("play", 128, (x, y) => Mathf.Clamp01(Mathf.Min((x - .18f) * 128, Mathf.Min((.86f - x) - Mathf.Abs(y - .5f) * 1.36f, 1) * 128)), Vector4.zero)); } }

        public static Sprite Check
        {
            get
            {
                return checkmark ?? (checkmark = Shape("check", 128, (x, y) =>
                {
                    float d1 = SegmentDistance(x, y, .2f, .52f, .42f, .28f), d2 = SegmentDistance(x, y, .42f, .28f, .82f, .74f);
                    return Mathf.Clamp01((.075f - Mathf.Min(d1, d2)) * 128);
                }, Vector4.zero));
            }
        }

        /// <summary>Horizontal gradient: opaque at the left edge, transparent to the right. Flip X for the right edge.</summary>
        public static Sprite EdgeGlow { get { return edge ?? (edge = Shape("edge", 128, (x, y) => Mathf.Pow(1 - x, 1.3f) * Mathf.Clamp01(Mathf.Min(y, 1 - y) * 6), Vector4.zero)); } }

        /// <summary>Soft radial rays (12 spokes), low contrast; rotate slowly, never strobe.</summary>
        public static Sprite Rays
        {
            get
            {
                return rays ?? (rays = Shape("rays", 256, (x, y) =>
                {
                    float dx = x - .5f, dy = y - .5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2, a = Mathf.Atan2(dy, dx);
                    float spoke = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 6)), 8);
                    return spoke * Mathf.Clamp01(1 - r) * Mathf.Clamp01(r * 4) * .9f;
                }, Vector4.zero));
            }
        }

        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
            float cx = ax + t * vx - px, cy = ay + t * vy - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        static float Dist(float x, float y) { return Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)); }
        static float Disc(float x, float y, float radius, float soft) { return Mathf.Clamp01((radius - Dist(x, y)) * 256 * soft); }

        static float RoundRect(float x, float y, int size, float radiusPx)
        {
            float r = radiusPx / size;
            float dx = Mathf.Max(Mathf.Abs(x - .5f) - (.5f - r), 0), dy = Mathf.Max(Mathf.Abs(y - .5f) - (.5f - r), 0);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((r - d) * size);
        }

        static Sprite Shape(string name, int size, System.Func<float, float, float> alpha, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color[size * size];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                    px[j * size + i] = new Color(1, 1, 1, alpha((i + .5f) / size, (j + .5f) / size));
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
        }
    }
}
