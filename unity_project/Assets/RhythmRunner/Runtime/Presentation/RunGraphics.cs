using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Beat guide lines painted across the road (owner round 2): a white line on every beat, and in offbeat sections a
    /// dashed purple line exactly halfway between them, so a target sitting on a purple line is an offbeat target.
    /// The lines scroll with the music, so faster stages also read as faster.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BeatLinesGraphic : MaskableGraphic
    {
        public struct Line { public double Ahead; public bool Half; }

        public readonly List<Line> Lines = new List<Line>(24);
        public double LeadBeats = 4;
        public float Strength = 1f;
        /// <summary>Lines farther than this depth are hidden (the boss stands on the road there).</summary>
        public float FromDepth;
        /// <summary>
        /// Long 3D objects lying on the road (round 13): the painted lines stop at their edges instead of being drawn across
        /// them (this UI layer is drawn over the 3D world, so a line over an object made it look pasted on).
        /// </summary>
        public readonly List<HoldBodyGraphic> Occluders = new List<HoldBodyGraphic>(4);
        const float OccluderHalf = .27f; // lateral half width of a long object with its shadow

        public void Refresh() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            foreach (Line line in Lines)
            {
                float depth = Perspective.DepthFromBeats(line.Ahead, LeadBeats);
                if (depth <= .02f || depth < FromDepth) continue;
                float y = Perspective.Y(depth);
                if (y > 1.02f) continue;
                float scale = Perspective.Scale(depth);
                // fade in from the horizon and out just past the slash line
                float alpha = Mathf.Clamp01(depth * 3f) * (line.Ahead < 0 ? Mathf.Clamp01(1f + (float)line.Ahead / .35f) : 1f) * Strength;
                if (alpha <= 0) continue;
                float thickness = (line.Half ? .0045f : .006f) * Mathf.Lerp(.35f, 1f, scale) * r.height;
                Color c = line.Half ? new Color(UiKit.Offbeat.r, UiKit.Offbeat.g, UiKit.Offbeat.b, .95f * alpha) : new Color(1f, 1f, 1f, .42f * alpha);
                float half = Perspective.HalfWidth(depth) * .97f;
                if (!line.Half) Gapped(vh, r, .5f - half, .5f + half, y, thickness, c, line.Ahead, depth);
                else
                {
                    const int dashes = 9;
                    for (int i = 0; i < dashes; i++)
                    {
                        float a = Mathf.Lerp(.5f - half, .5f + half, (i + .2f) / dashes);
                        float b = Mathf.Lerp(.5f - half, .5f + half, (i + .8f) / dashes);
                        Gapped(vh, r, a, b, y, thickness, c, line.Ahead, depth);
                    }
                }
            }
        }

        static readonly List<Vector2> gaps = new List<Vector2>(4);

        /// <summary>A line segment with holes where long objects lie on it.</summary>
        void Gapped(VertexHelper vh, Rect r, float x0, float x1, float y, float thicknessPx, Color32 c, double ahead, float depth)
        {
            gaps.Clear();
            foreach (HoldBodyGraphic o in Occluders)
            {
                if (o == null || !o.gameObject.activeInHierarchy) continue;
                double near = o.NearAhead - (o.Glowing ? .5 : 0), far = o.FarAhead;
                if (ahead < near || ahead > far) continue;
                float lat = o.LateralAt(ahead);
                gaps.Add(new Vector2(Perspective.X(lat - OccluderHalf, depth), Perspective.X(lat + OccluderHalf, depth)));
            }
            if (gaps.Count == 0) { Quad(vh, r, x0, x1, y, thicknessPx, c); return; }
            gaps.Sort((p, q) => p.x.CompareTo(q.x));
            float from = x0;
            foreach (Vector2 g in gaps)
            {
                if (g.x > from) Quad(vh, r, from, Mathf.Min(g.x, x1), y, thicknessPx, c);
                from = Mathf.Max(from, g.y);
                if (from >= x1) return;
            }
            if (from < x1) Quad(vh, r, from, x1, y, thicknessPx, c);
        }

        static void Quad(VertexHelper vh, Rect r, float x0, float x1, float y, float thicknessPx, Color32 c)
        {
            int start = vh.currentVertCount;
            Vector2 left = RoadGraphic.ToLocal(r, x0, y), right = RoadGraphic.ToLocal(r, x1, y);
            Vector2 up = new Vector2(0, thicknessPx * .5f);
            vh.AddVert(left - up, c, Vector2.zero);
            vh.AddVert(left + up, c, Vector2.zero);
            vh.AddVert(right + up, c, Vector2.zero);
            vh.AddVert(right - up, c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    /// <summary>
    /// A side wall standing along the road (owner round 4: the scenery itself must move). Drawn in perspective from the
    /// horizon to below the screen; its texture scrolls with the world, so shelves, stalls or clouds rush past.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WallGraphic : MaskableGraphic
    {
        Texture texture;
        /// <summary>Setting it re-binds the material (a plain field would keep drawing the white default).</summary>
        public Texture Texture { get { return texture; } set { if (texture == value) return; texture = value; SetMaterialDirty(); } }
        /// <summary>-1 = left wall, +1 = right wall (road edges are ±1).</summary>
        public float Side = -1f;
        public float Lateral = 1.25f;
        /// <summary>Wall height at the slash line, in fractions of the screen height.</summary>
        public float Height = .5f;
        public double Scroll, LeadBeats = 4;
        /// <summary>World beats covered by one repeat of the texture.</summary>
        public float TileBeats = 2.2f;

        public override Texture mainTexture { get { return texture != null ? texture : s_WhiteTexture; } }

        public void Refresh(double scroll) { Scroll = scroll; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Texture == null) return;
            Rect r = rectTransform.rect;
            const int segments = 40;
            float bottom = Perspective.DepthFromY(1.6f);
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float depth = Mathf.Lerp(0, bottom, t * t);
                float y = Perspective.Y(depth);
                float x = Perspective.X(Side * Lateral, depth);
                float h = Height * Perspective.Scale(depth);
                double ahead = Perspective.BeatsFromDepth(depth, LeadBeats);
                float u = (float)((Scroll + ahead) / TileBeats) * (Side < 0 ? -1f : 1f);
                float shade = Mathf.Lerp(.55f, 1f, Mathf.Clamp01(depth * 1.5f)); // darker far away: depth cue
                Color32 c = new Color(color.r * shade, color.g * shade, color.b * shade, color.a * Mathf.Clamp01(depth * 6f));
                vh.AddVert(RoadGraphic.ToLocal(r, x, y), c, new Vector2(u, 0));
                vh.AddVert(RoadGraphic.ToLocal(r, x, y - h), c, new Vector2(u, 1));
                if (i > 0)
                {
                    int k = i * 2;
                    vh.AddTriangle(k - 2, k - 1, k + 1);
                    vh.AddTriangle(k - 2, k + 1, k);
                }
            }
        }
    }

    /// <summary>
    /// A long hold object (baguette, leek, pencil…) lying along its lane from the head beat to the tail beat, drawn in
    /// perspective. While it is being held the part that already passed the slash line is cut away.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HoldBodyGraphic : MaskableGraphic
    {
        Texture texture;
        public Texture Texture { get { return texture; } set { if (texture == value) return; texture = value; SetMaterialDirty(); } }
        /// <summary>Vertex tint (the charge attack's energy tail uses a plain coloured band).</summary>
        public Color Tint = Color.white;
        public float Lateral;
        public double NearAhead, FarAhead, LeadBeats = 4;
        /// <summary>Texture v at the near end (0 = the whole object, &gt;0 = already eaten).</summary>
        public float NearV;
        /// <summary>Width at the slash line in canvas units.</summary>
        public float Width = 100;
        public float Alpha = 1f;
        public bool Glowing;
        public double LengthBeats = 1;
        /// <summary>Height of the lying rod as a fraction of its width (owner round 7: "ペラペラに見える").</summary>
        public float Thickness = .42f;
        /// <summary>Draws the cut faces of the split part (a child graphic: it needs its own texture).</summary>
        public HoldCutGraphic Cut;
        /// <summary>
        /// Slide (round 11): the object snakes to these lateral positions, each centred on a turn given in beats ahead of
        /// now (negative = already passed). Null for a straight hold.
        /// </summary>
        public float[] TurnLaterals;
        public double[] TurnAheads;
        public bool IsSlide { get { return TurnLaterals != null && TurnLaterals.Length > 0; } }
        const double TurnHalfWidth = .32; // beats over which a turn bends (on each side of the beat)

        /// <summary>Lateral position of the object <paramref name="ahead"/> beats down the road.</summary>
        public float LateralAt(double ahead)
        {
            if (!IsSlide) return Lateral;
            float lat = Lateral;
            for (int i = 0; i < TurnLaterals.Length; i++)
            {
                float k = Mathf.Clamp01((float)((ahead - TurnAheads[i] + TurnHalfWidth) / (2 * TurnHalfWidth)));
                lat = Mathf.Lerp(lat, TurnLaterals[i], k * k * (3f - 2f * k));
            }
            return lat;
        }

        public override Texture mainTexture { get { return texture != null ? texture : s_WhiteTexture; } }

        public void Refresh() { SetVerticesDirty(); if (Cut != null) Cut.SetVerticesDirty(); }

        /// <summary>How much of the cut part is still on screen, as beats and as texture v.</summary>
        internal bool Splitting(out double span, out float spanV)
        {
            span = System.Math.Min(.7, NearV * LengthBeats);
            spanV = (float)(span / System.Math.Max(.01, LengthBeats));
            return Glowing && NearV > 0 && Texture != null && !IsSlide; // only real objects split (not the charge energy tail); a slide is eaten instead
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (FarAhead <= NearAhead) return;
            Rect r = rectTransform.rect;
            double span; float spanV;
            bool solid = Texture != null;
            if (Splitting(out span, out spanV)) Split(vh, r);
            if (solid)
            {
                // contact shadow, then the rod's side stacked up to its top face: a real round loaf, not a paper strip
                Ribbon(vh, r, 0, 1.18f, new Color(0, 0, 0, .3f), -.12f);
                // enough layers that no step shows (≤ ~1.5 px apart even near the camera)
                int layers = Mathf.Clamp(Mathf.CeilToInt(Width * Thickness * 1.8f / 1.5f), 4, 32);
                for (int l = 0; l < layers; l++)
                {
                    float k = (float)l / layers;
                    float shade = Mathf.Lerp(.5f, .78f, k);
                    Ribbon(vh, r, Thickness * k, 1f - .1f * (1f - k), new Color(shade, shade * .96f, shade * .92f, 1f), 0);
                }
            }
            Color tint = Glowing ? Color.Lerp(Tint, new Color(1f, .95f, .7f), .35f) : Tint;
            Ribbon(vh, r, solid ? Thickness : 0, 1f, tint, 0);
        }

        /// <summary>The object's strip lifted <paramref name="lift"/> widths off the road.</summary>
        void Ribbon(VertexHelper vh, Rect r, float lift, float widen, Color tint, float drop)
        {
            int segments = IsSlide ? 48 : 16; // a snaking object needs enough points for smooth bends
            int first = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                double ahead = NearAhead + (FarAhead - NearAhead) * t;
                float depth = Perspective.DepthFromBeats(ahead, LeadBeats);
                float y = Perspective.Y(depth);
                float x = Perspective.X(LateralAt(ahead), depth);
                float w = Width * Perspective.Scale(depth) * .5f;
                float fade = Mathf.Clamp01(depth * 6f) * Alpha;
                Color32 c = new Color(tint.r, tint.g, tint.b, fade * tint.a);
                Vector2 center = RoadGraphic.ToLocal(r, x, y) + new Vector2(0, w * 2f * (lift + drop));
                float v = Mathf.Lerp(NearV, 1f, t);
                vh.AddVert(center + new Vector2(-w * widen, 0), c, new Vector2(0, v));
                vh.AddVert(center + new Vector2(w * widen, 0), c, new Vector2(1, v));
                if (i > 0)
                {
                    int k = first + i * 2;
                    vh.AddTriangle(k - 2, k, k + 1);
                    vh.AddTriangle(k - 2, k + 1, k - 1);
                }
            }
        }

        /// <summary>
        /// The part already cut (owner round 3: "縦にちょっとずつ裂ける"): below the slash line the object is split down the
        /// middle and the two halves peel outward like a zipper opening.
        /// </summary>
        void Split(VertexHelper vh, Rect r)
        {
            // the crust underneath each half (its round back), stacked like the intact rod; the cut faces go on top (HoldCutGraphic)
            bool faces = Cut != null && Cut.Texture != null;
            int layers = Mathf.Clamp(Mathf.CeilToInt(Width * Thickness * .5f * 2.6f / 1.5f), 3, 24);
            for (int l = 0; l <= layers; l++)
            {
                float k = (float)l / layers;
                float shade = faces ? Mathf.Lerp(.45f, .7f, k) : (l == layers ? 1f : Mathf.Lerp(.45f, .7f, k));
                if (faces && l == layers) break;
                for (int side = 0; side < 2; side++)
                    HalfStrip(vh, r, side, Thickness * .5f * k, new Color(shade, shade * .96f, shade * .92f, 1f), side == 0 ? 0f : .5f, side == 0 ? .5f : 1f);
            }
        }

        /// <summary>One peeled half below the slash line, lifted <paramref name="lift"/> widths, mapped to u0..u1.</summary>
        internal void HalfStrip(VertexHelper vh, Rect r, int side, float lift, Color tint, float u0, float u1)
        {
            double span; float spanV;
            Splitting(out span, out spanV);
            const int segments = 10;
            float dir = side == 0 ? -1f : 1f;
            int first = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments; // 0 = at the blade, 1 = farthest cut part (nearest the camera)
                double ahead = -span * t;
                float depth = Perspective.DepthFromBeats(ahead, LeadBeats);
                float y = Perspective.Y(depth);
                float w = Width * Perspective.Scale(depth) * .5f;
                float peel = w * (.55f + 1.6f * t); // rigid halves: straight edges opening at a constant angle from the blade
                Vector2 center = RoadGraphic.ToLocal(r, Perspective.X(Lateral, depth), y) + new Vector2(dir * peel, w * 2f * lift);
                float v = Mathf.Max(0, NearV - spanV * t);
                Color32 c = new Color(tint.r, tint.g, tint.b, Alpha * tint.a); // solid: the halves are real pieces, not a fading ghost
                // each half keeps its own width; the inner (cut) edge faces the middle
                vh.AddVert(center + new Vector2(-w * .5f, 0), c, new Vector2(u0, v));
                vh.AddVert(center + new Vector2(w * .5f, 0), c, new Vector2(u1, v));
                if (i > 0)
                {
                    int k = first + i * 2;
                    vh.AddTriangle(k - 2, k, k + 1);
                    vh.AddTriangle(k - 2, k + 1, k - 1);
                }
            }
        }
    }

    /// <summary>
    /// The cut faces of a split hold object (owner round 7: the long objects looked paper-thin when cut). Each half
    /// shows the inside — crumb, leek layers, wood and lead — from a lengthwise cross-section picture.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HoldCutGraphic : MaskableGraphic
    {
        Texture texture;
        public Texture Texture { get { return texture; } set { if (texture == value) return; texture = value; SetMaterialDirty(); SetVerticesDirty(); } }
        public HoldBodyGraphic Body;

        public override Texture mainTexture { get { return texture != null ? texture : s_WhiteTexture; } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            double span; float spanV;
            if (Body != null && Body.IsSlide) { TurnMarkers(vh); return; }
            if (Body == null || texture == null || !Body.Splitting(out span, out spanV)) return;
            Rect r = rectTransform.rect;
            // the left half shows the picture as is, the right half its mirror: the two faces match like an opened book
            Body.HalfStrip(vh, r, 0, Body.Thickness * .5f, Color.white, 0f, 1f);
            Body.HalfStrip(vh, r, 1, Body.Thickness * .5f, Color.white, 1f, 0f);
        }

        /// <summary>Glowing purple dots on a slide's turns (with this graphic's plain white texture): "turn here, on this beat".</summary>
        void TurnMarkers(VertexHelper vh)
        {
            Rect r = rectTransform.rect;
            for (int i = 0; i < Body.TurnAheads.Length; i++)
            {
                double ahead = Body.TurnAheads[i];
                if (ahead < -.15 || ahead > Body.LeadBeats) continue;
                float depth = Perspective.DepthFromBeats(System.Math.Max(0, ahead), Body.LeadBeats);
                float size = Body.Width * Perspective.Scale(depth) * .42f;
                Vector2 c = RoadGraphic.ToLocal(r, Perspective.X(Body.LateralAt(ahead), depth), Perspective.Y(depth)) + new Vector2(0, Body.Width * Perspective.Scale(depth) * Body.Thickness * 2f + size * .3f);
                float fade = Mathf.Clamp01(depth * 6f) * Body.Alpha * (ahead < 0 ? Mathf.Clamp01(1f + (float)ahead / .15f) : 1f);
                Diamond(vh, c, size * 1.7f, new Color(.85f, .45f, 1f, .35f * fade)); // halo
                Diamond(vh, c, size, new Color(.95f, .75f, 1f, .95f * fade));
            }
        }

        static void Diamond(VertexHelper vh, Vector2 c, float s, Color32 col)
        {
            int k = vh.currentVertCount;
            vh.AddVert(c + new Vector2(0, s), col, Vector2.zero); vh.AddVert(c + new Vector2(s, 0), col, Vector2.zero);
            vh.AddVert(c + new Vector2(0, -s), col, Vector2.zero); vh.AddVert(c + new Vector2(-s, 0), col, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
        }
    }

    /// <summary>
    /// A sprite with thickness (owner round 7: "ペラペラに見える"): its silhouette is stacked toward the back face in darker
    /// shades, then the art goes on top, so targets and cut pieces read as solid chunks instead of paper cut-outs.
    /// </summary>
    public sealed class SolidImage : Image
    {
        Vector2 backFace;
        /// <summary>Brightness of the farthest side layer.</summary>
        public float SideShade = .5f;
        static readonly List<UIVertex> front = new List<UIVertex>(64), output = new List<UIVertex>(1024);

        /// <summary>Where the back face lies, in parent (screen) units; kept in that direction however the piece spins.</summary>
        public void SetDepth(Vector2 screenDepth)
        {
            RectTransform rt = rectTransform;
            Vector2 local = Quaternion.Inverse(rt.localRotation) * (Vector3)screenDepth;
            Vector3 s = rt.localScale;
            local.x /= Mathf.Abs(s.x) < .05f ? (s.x < 0 ? -.05f : .05f) : s.x;
            local.y /= Mathf.Abs(s.y) < .05f ? (s.y < 0 ? -.05f : .05f) : s.y;
            if ((local - backFace).sqrMagnitude < .3f) return;
            backFace = local;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            float len = backFace.magnitude;
            if (len < 1f || overrideSprite == null) return;
            vh.GetUIVertexStream(front);
            output.Clear();
            int layers = Mathf.Clamp(Mathf.CeilToInt(len / 1.5f), 2, 14);
            for (int l = layers; l >= 1; l--)
            {
                float t = (float)l / layers; // 1 = back face
                float shade = Mathf.Lerp(SideShade + .25f, SideShade, t);
                Vector3 off = backFace * t;
                for (int i = 0; i < front.Count; i++)
                {
                    UIVertex v = front[i];
                    v.position += off;
                    Color32 c = v.color;
                    v.color = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), c.a);
                    output.Add(v);
                }
            }
            output.AddRange(front);
            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
        }
    }
}
