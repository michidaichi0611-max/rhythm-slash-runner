using UnityEngine;
using UnityEngine.UI;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Perspective mapping shared by the road, props and targets so everything sticks to the same board.
    /// Coordinates are normalized playfield space: x 0..1 left→right, y 0..1 top→bottom (TECH_SPEC origin top_left).
    /// </summary>
    public static class Perspective
    {
        public const float HorizonY = .255f;
        public const float SlashY = .72f;
        public const float CharacterY = .85f;
        public const float HorizonHalfWidth = .055f;
        public const float LineHalfWidth = .5f;
        public const float Exponent = 1.7f;

        /// <summary>0 at the horizon, 1 on the slash line, &gt;1 below it.</summary>
        public static float DepthFromBeats(double beatsAhead, double leadBeats)
        {
            float p = (float)(1.0 - beatsAhead / leadBeats);
            return p <= 0 ? 0 : Mathf.Pow(p, Exponent);
        }

        public static double BeatsFromDepth(float depth, double leadBeats)
        {
            return leadBeats * (1.0 - Mathf.Pow(Mathf.Max(0, depth), 1f / Exponent));
        }

        public static float Y(float depth) { return HorizonY + (SlashY - HorizonY) * depth; }
        public static float DepthFromY(float y) { return (y - HorizonY) / (SlashY - HorizonY); }
        /// <summary>Unclamped: below the slash line the board keeps widening, so it reads as a flat floor, not a waterfall.</summary>
        public static float HalfWidth(float depth) { return Mathf.LerpUnclamped(HorizonHalfWidth, LineHalfWidth, depth); }
        public static float Scale(float depth) { return HalfWidth(depth) / LineHalfWidth; }

        /// <summary>x of a lateral offset (-1 = left road edge, +1 = right edge) at a depth.</summary>
        public static float X(float lateral, float depth) { return .5f + lateral * HalfWidth(depth); }

        public static float LaneLateral(int lane) { return (lane - 1) * (2f / 3f); }
    }

    /// <summary>Scrolling wooden cutting-board road drawn as perspective-correct horizontal strips.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadGraphic : MaskableGraphic
    {
        public Texture Wood;
        public double Beat;
        public double LeadBeats = 4;
        public float TilesAcross = 1.2f;
        public float BeatsPerTile = 2f;
        const int Strips = 56;

        public override Texture mainTexture { get { return Wood != null ? Wood : s_WhiteTexture; } }

        public void Refresh(double beat) { Beat = beat; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float bottomDepth = Perspective.DepthFromY(1.08f);
            for (int i = 0; i <= Strips; i++)
            {
                // denser strips near the horizon where the projection changes fastest
                float t = (float)i / Strips;
                float depth = Mathf.Lerp(0, bottomDepth, t * t);
                float y = Perspective.Y(depth);
                float half = Perspective.HalfWidth(depth);
                double ahead = Perspective.BeatsFromDepth(depth, LeadBeats);
                float v = (float)((Beat + ahead) / BeatsPerTile);
                float fade = Mathf.Clamp01(depth * 7f);
                Color32 c = new Color(color.r * Mathf.Lerp(.75f, 1f, Mathf.Clamp01(depth)), color.g * Mathf.Lerp(.72f, 1f, Mathf.Clamp01(depth)), color.b * Mathf.Lerp(.7f, 1f, Mathf.Clamp01(depth)), color.a * fade);
                Vector2 left = ToLocal(r, .5f - half, y), right = ToLocal(r, .5f + half, y);
                vh.AddVert(left, c, new Vector2(0, -v));
                vh.AddVert(right, c, new Vector2(TilesAcross, -v));
                if (i > 0)
                {
                    int k = i * 2;
                    vh.AddTriangle(k - 2, k - 1, k + 1);
                    vh.AddTriangle(k - 2, k + 1, k);
                }
            }
        }

        public static Vector2 ToLocal(Rect r, float x, float y) { return new Vector2(r.xMin + x * r.width, r.yMax - y * r.height); }
    }

    /// <summary>Inlaid lane strips and board rims on top of the road.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LaneLinesGraphic : MaskableGraphic
    {
        float boost;
        /// <summary>0 = normal inlaid strips, 1 = wide bright FEVER strips (constant brightness, colour drifts).</summary>
        public float Boost { get { return boost; } set { if (Mathf.Abs(value - boost) > .001f) { boost = value; SetVerticesDirty(); } } }
        float fromDepth;
        /// <summary>The strips start at this depth (the part behind a boss standing on the road is not painted over it).</summary>
        public float FromDepth { get { return fromDepth; } set { if (Mathf.Abs(value - fromDepth) > .002f) { fromDepth = value; SetVerticesDirty(); } } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float bottom = Perspective.DepthFromY(1.08f);
            from = fromDepth;
            float w = Mathf.Lerp(.014f, .03f, boost), a = Mathf.Lerp(.55f, .9f, boost);
            Strip(vh, r, -1f / 3f, w, bottom, new Color(1f, .93f, .78f, a));
            Strip(vh, r, 1f / 3f, w, bottom, new Color(1f, .93f, .78f, a));
            Strip(vh, r, -1f, .03f, bottom, new Color(.33f, .18f, .08f, .6f));
            Strip(vh, r, 1f, .03f, bottom, new Color(.33f, .18f, .08f, .6f));
        }

        static float from;

        static void Strip(VertexHelper vh, Rect r, float lateral, float width, float bottomDepth, Color c)
        {
            const int segments = 12;
            int start = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float depth = Mathf.Lerp(from, bottomDepth, (float)i / segments);
                float y = Perspective.Y(depth);
                float x = Perspective.X(lateral, depth);
                float w = width * Perspective.Scale(depth) * .5f;
                Color32 col = new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(depth * 4f));
                vh.AddVert(RoadGraphic.ToLocal(r, x - w, y), col, Vector2.zero);
                vh.AddVert(RoadGraphic.ToLocal(r, x + w, y), col, Vector2.zero);
                if (i > 0)
                {
                    int k = start + i * 2;
                    vh.AddTriangle(k - 2, k - 1, k + 1);
                    vh.AddTriangle(k - 2, k + 1, k);
                }
            }
        }
    }
}
