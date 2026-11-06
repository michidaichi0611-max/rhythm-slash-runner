using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Long objects — holds and slides — as real 3D shapes lying on the road (owner round 12: "ラーメンの形をした工作",
    /// "切って貼ったような違和感"). They used to be one picture stretched over a flat strip in the UI layer; now each kind is
    /// modelled (a rounded loaf with a tiled crust, a leek fading from white to green, a hexagonal pencil with a sharpened
    /// tip and an eraser, a twisted bundle of noodles with toppings, a toy train of separate cars), lit, fogged and
    /// shadowed like the rest of the world. Purely visual: positions come from the same beat → road mapping as the
    /// targets, so the judgement and the UI guides are unchanged.
    /// </summary>
    public sealed partial class World3D
    {
        sealed class LongObj
        {
            public GameObject Go;
            public Mesh Mesh, ShadowMesh, SplitMesh;
            public MeshRenderer R, Shadow, Split, Guide;
            public Mesh GuideMesh;
            public SpriteRenderer Cap;
            public int Chops = -1;
            public bool Used;
        }

        public static bool IsLongKind(string kind) { return kind == "baguette" || kind == "leek" || kind == "pencil" || kind == "ramen" || kind == "train" || kind == "candy"; }

        readonly Dictionary<string, LongObj> longs = new Dictionary<string, LongObj>();
        readonly Stack<LongObj> freeLongs = new Stack<LongObj>();
        readonly List<string> longScratch = new List<string>();
        Material solidPlain, solidCrust, solidLeek, longShadowMat, brothMat, guideMat, solidNoodle;
        Color lightTint = Color.white;

        // shared mesh buffers (one object is built at a time)
        static readonly List<Vector3> V = new List<Vector3>(4096), N = new List<Vector3>(4096);
        static readonly List<Vector2> UV = new List<Vector2>(4096);
        static readonly List<Color> C = new List<Color>(4096);
        static readonly List<int> T = new List<int>(12000), T2 = new List<int>(2000);
        readonly Dictionary<string, Material> faceMats = new Dictionary<string, Material>();

        /// <summary>The lengthwise cross-section picture (crumb, leek layers, wood and lead) on the split faces.</summary>
        Material FaceMat(string kind)
        {
            Material m;
            if (faceMats.TryGetValue(kind, out m)) return m;
            Texture2D tex = ArtLibrary.Texture("hold_" + kind + "_half");
            m = new Material(Resources.Load<Shader>("Shaders/RRSolid")) { mainTexture = tex != null ? tex : Texture2D.whiteTexture };
            m.SetColor("_LightTint", Color.Lerp(Color.white, lightTint, .3f));
            faceMats[kind] = m;
            return m;
        }

        void EnsureLongMaterials()
        {
            if (solidPlain != null) return;
            Shader solid = Resources.Load<Shader>("Shaders/RRSolid"), trans = Resources.Load<Shader>("Shaders/RRUnlitTransparent");
            solidPlain = new Material(solid) { mainTexture = Texture2D.whiteTexture };
            Texture2D crust = ArtLibrary.Texture("tile_crust");
            if (crust != null) { crust.wrapMode = TextureWrapMode.Repeat; crust.anisoLevel = 4; }
            solidCrust = new Material(solid) { mainTexture = crust != null ? crust : Texture2D.whiteTexture };
            // round 16: real surfaces tiled along the objects (never stretched): leek skin, noodles
            Texture2D leekTile = ArtLibrary.Texture("tile_leek");
            if (leekTile != null) { leekTile.wrapMode = TextureWrapMode.Repeat; leekTile.anisoLevel = 4; }
            solidLeek = new Material(solid) { mainTexture = leekTile != null ? (Texture)leekTile : Stripes() };
            Texture2D noodleTile = ArtLibrary.Texture("tile_noodle");
            if (noodleTile != null) { noodleTile.wrapMode = TextureWrapMode.Repeat; noodleTile.anisoLevel = 4; }
            solidNoodle = new Material(solid) { mainTexture = noodleTile != null ? (Texture)noodleTile : Texture2D.whiteTexture };
            // a glowing lane painted under a slide: where the finger has to go, readable from far away
            guideMat = new Material(trans) { mainTexture = SoftBand(), color = new Color(.92f, .55f, 1f, 1f) };
            guideMat.renderQueue = 2449;
            longShadowMat = new Material(trans) { mainTexture = SoftBand(), color = new Color(0, 0, 0, .42f) };
            longShadowMat.renderQueue = 2450;
            // ramen lies in a little of its broth: the noodles stand out from the pale wooden road
            brothMat = new Material(trans) { mainTexture = SoftBand(), color = new Color(.45f, .22f, .08f, 1f) };
            brothMat.renderQueue = 2451;
            SetLightTint(lightTint);
        }

        /// <summary>The stage's light colour (warm kitchen, blue freezer, purple neon…), shared with the UI targets.</summary>
        public void SetLightTint(Color c)
        {
            lightTint = c;
            // half the stage's colour on the objects: a yellow pencil must stay yellow under the freezer's blue light
            Color half = Color.Lerp(Color.white, c, .3f); // round 17: less of the (often dark) stage light
            foreach (Material m in new[] { solidPlain, solidCrust, solidLeek, solidNoodle }) if (m != null) m.SetColor("_LightTint", half);
            foreach (Material m in faceMats.Values) m.SetColor("_LightTint", half);
        }

        public void BeginLongFrame() { foreach (LongObj o in longs.Values) o.Used = false; }

        public void EndLongFrame()
        {
            longScratch.Clear();
            foreach (KeyValuePair<string, LongObj> p in longs) if (!p.Value.Used) longScratch.Add(p.Key);
            foreach (string id in longScratch) { LongObj o = longs[id]; o.Go.SetActive(false); freeLongs.Push(o); longs.Remove(id); }
        }

        LongObj NewLong()
        {
            var go = new GameObject("LongObject");
            go.transform.SetParent(world, false);
            var o = new LongObj { Go = go, Mesh = new Mesh(), ShadowMesh = new Mesh() };
            o.Mesh.MarkDynamic(); o.ShadowMesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = o.Mesh;
            o.R = go.AddComponent<MeshRenderer>();
            o.R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; o.R.receiveShadows = false;
            var sh = new GameObject("Shadow");
            sh.transform.SetParent(go.transform, false);
            sh.AddComponent<MeshFilter>().sharedMesh = o.ShadowMesh;
            o.Shadow = sh.AddComponent<MeshRenderer>();
            o.Shadow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var guide = new GameObject("Guide");
            guide.transform.SetParent(go.transform, false);
            o.GuideMesh = new Mesh(); o.GuideMesh.MarkDynamic();
            guide.AddComponent<MeshFilter>().sharedMesh = o.GuideMesh;
            o.Guide = guide.AddComponent<MeshRenderer>();
            o.Guide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var split = new GameObject("Split");
            split.transform.SetParent(go.transform, false);
            o.SplitMesh = new Mesh(); o.SplitMesh.MarkDynamic();
            split.AddComponent<MeshFilter>().sharedMesh = o.SplitMesh;
            o.Split = split.AddComponent<MeshRenderer>();
            o.Split.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; o.Split.receiveShadows = false;
            o.Split.enabled = false;
            var cap = new GameObject("CutFace");
            cap.transform.SetParent(go.transform, false);
            o.Cap = cap.AddComponent<SpriteRenderer>();
            return o;
        }

        // ---------------------------------------------------------------- placement

        double nearA, farA, headA, lengthB, leadB;
        Func<double, float> lateralOf;

        /// <summary>World point on the road for a spot <paramref name="ahead"/> beats down a lane (lateral −1…1 = road edges).</summary>
        Vector3 Road(double ahead, float lateral, float height)
        {
            float depth = Perspective.DepthFromBeats(ahead, leadB);
            return new Vector3(lateral * roadHalf, height, ZForY(Perspective.Y(depth)));
        }

        Vector3 Centre(double ahead, float height) { return Road(ahead, lateralOf(ahead), height); }

        /// <summary>
        /// Draws (or updates) one long object this frame. <paramref name="nearAhead"/>/<paramref name="farAhead"/> are the
        /// visible ends in beats ahead of now (near = 0 while it is being cut), <paramref name="headAhead"/> where its head
        /// is (texture and shape are anchored to the object, not to the screen).
        /// </summary>
        public void DrawLong(string id, string kind, double nearAhead, double farAhead, double headAhead, double lengthBeats, double leadBeats,
                             Func<double, float> lateral, float alpha, bool cutting, Sprite cutFace, bool slide = false)
        {
            EnsureLongMaterials();
            LongObj o;
            if (!longs.TryGetValue(id, out o))
            {
                o = freeLongs.Count > 0 ? freeLongs.Pop() : NewLong();
                o.Go.SetActive(true);
                longs[id] = o;
            }
            o.Used = true;
            nearA = nearAhead; farA = farAhead; headA = headAhead; lengthB = Math.Max(.1, lengthBeats); leadB = leadBeats; lateralOf = lateral;
            float r0 = roadHalf / 5f * Mathf.Clamp01(alpha * 1.2f);
            // owner round 14: while held it is chopped finely, "ta-ta-ta-ta" — a slice comes off at the blade every 16th
            // note and the body ends in a clean flat cut showing the inside (no lengthwise split)
            bool chops = cutting && headAhead < 0 && kind != "train";
            o.Cap.enabled = false;
            if (chops)
            {
                double held = Math.Min(-headAhead, lengthBeats);
                int count = (int)Math.Floor(held / ChopBeats);
                nearAhead = -(held - count * ChopBeats); // the cut end waits just past the blade until the next chop
                nearA = nearAhead;
                if (o.Chops >= 0 && count > o.Chops) for (int k = o.Chops; k < count && k < o.Chops + 3; k++) SpawnChop(kind, r0, k == count - 1);
                o.Chops = count;
                if (farAhead > nearAhead + .02 && cutFace != null)
                {
                    // the fresh cut face on the end of the body
                    Vector2 rad; float lift; Color col; float v;
                    Section(kind, r0, nearAhead, out rad, out lift, out col, out v);
                    Vector3 at = Centre(nearAhead, lift), fwd = Centre(nearAhead + .03, lift) - Centre(nearAhead - .03, lift);
                    if (kind == "ramen") { rad = new Vector2(r0 * .62f, r0 * .5f); at = Centre(nearAhead, r0 * .45f); }
                    o.Cap.enabled = true;
                    o.Cap.sprite = cutFace;
                    o.Cap.transform.position = world.TransformPoint(at - fwd.normalized * r0 * .02f);
                    o.Cap.transform.rotation = world.rotation * Quaternion.LookRotation(fwd.normalized, Vector3.up);
                    o.Cap.transform.localScale = new Vector3(rad.x * 2.15f / (cutFace.rect.width / cutFace.pixelsPerUnit), rad.y * 2.15f / (cutFace.rect.height / cutFace.pixelsPerUnit), 1);
                    o.Cap.color = Color.Lerp(Color.white, lightTint, .5f);
                }
            }
            else if (!cutting) o.Chops = -1;
            o.Guide.enabled = slide && farAhead > nearAhead + .02; // round 27: a straight ramen has no path to follow
            if (o.Guide.enabled) { BuildGuide(o.GuideMesh, r0); o.Guide.sharedMaterial = guideMat; }
            if (farAhead <= nearAhead + .02) { o.R.enabled = o.Shadow.enabled = o.Cap.enabled = false; return; }
            o.R.enabled = o.Shadow.enabled = true;
            float r = roadHalf / 5f * Mathf.Clamp01(alpha * 1.2f); // a good half lane wide; a missed one shrinks away
            Clear();
            switch (kind)
            {
                case "baguette": BuildBaguette(r); o.R.sharedMaterial = solidCrust; break;
                case "leek": BuildLeek(r); o.R.sharedMaterial = solidLeek; break;
                case "pencil": BuildPencil(r); o.R.sharedMaterial = solidPlain; break;
                case "ramen": BuildRamen(r); o.R.sharedMaterial = solidNoodle; break;
                case "candy": BuildCandy(r); o.R.sharedMaterial = solidPlain; break;
                default: BuildTrain(r); o.R.sharedMaterial = solidPlain; break;
            }
            Apply(o.Mesh);
            BuildShadow(o.ShadowMesh, kind == "train" ? r * 1.5f : kind == "ramen" ? r * 1.6f : r * 1.25f);
            o.Shadow.sharedMaterial = longShadowMat; // (the broth puddle hid the slide guide)
        }

        // ---------------------------------------------------------------- chopping

        const double ChopBeats = .25;

        sealed class ChopPiece { public Transform T; public MeshFilter F; public MeshRenderer R; public SpriteRenderer A, B; public Vector3 V, Axis, Scale; public float Spin, Born, Life, Rest; }
        readonly List<ChopPiece> chopPieces = new List<ChopPiece>();
        readonly Stack<ChopPiece> freeChops = new Stack<ChopPiece>();

        ChopPiece NewChop()
        {
            var go = new GameObject("Chop");
            go.transform.SetParent(world, false);
            var p = new ChopPiece { T = go.transform, F = go.AddComponent<MeshFilter>(), R = go.AddComponent<MeshRenderer>() };
            p.F.sharedMesh = new Mesh();
            p.R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; p.R.receiveShadows = false;
            foreach (int e in new[] { 0, 1 })
            {
                var face = new GameObject(e == 0 ? "FaceA" : "FaceB");
                face.transform.SetParent(go.transform, false);
                SpriteRenderer s = face.AddComponent<SpriteRenderer>();
                if (e == 0) p.A = s; else p.B = s;
            }
            return p;
        }

        /// <summary>One slice comes off at the blade: a short solid round with the inside on both faces, flung aside.</summary>
        void SpawnChop(string kind, float r, bool newest)
        {
            Vector2 rad; float lift; Color col; float v;
            Section(kind, r, 0, out rad, out lift, out col, out v);
            float len = Mathf.Max(r * .25f, (Centre(0, 0) - Centre(-ChopBeats, 0)).magnitude * .55f);
            ChopPiece p = freeChops.Count > 0 ? freeChops.Pop() : NewChop();
            p.T.gameObject.SetActive(true);
            // the round's side (crust, leek skin, painted wood, noodles), both ends carry the cross-section picture
            Clear();
            const int sides = 12;
            for (int ring = 0; ring < 2; ring++)
                for (int j = 0; j <= sides; j++)
                {
                    float ang = (float)j / sides * Mathf.PI * 2f;
                    V.Add(new Vector3(Mathf.Cos(ang) * rad.x, Mathf.Sin(ang) * rad.y, (ring - .5f) * len));
                    N.Add(new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0)); UV.Add(new Vector2((float)j / sides, ring * .3f)); C.Add(col);
                }
            for (int j = 0; j < sides; j++) { int a = j, b = sides + 1 + j; T.Add(a); T.Add(b); T.Add(b + 1); T.Add(a); T.Add(b + 1); T.Add(a + 1); }
            Apply(p.F.sharedMesh);
            p.R.sharedMaterial = kind == "baguette" ? solidCrust : kind == "leek" ? solidLeek : solidPlain;
            Sprite face = ArtLibrary.Get("hold_" + kind + "_slice");
            foreach (SpriteRenderer s in new[] { p.A, p.B })
            {
                s.enabled = face != null;
                if (!s.enabled) continue;
                s.sprite = face;
                bool front = s == p.A;
                s.transform.localPosition = new Vector3(0, 0, (front ? -.5f : .5f) * len * 1.02f);
                s.transform.localRotation = front ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                s.transform.localScale = new Vector3(rad.x * 2.15f / (face.rect.width / face.pixelsPerUnit), rad.y * 2.15f / (face.rect.height / face.pixelsPerUnit), 1);
                s.color = Color.Lerp(Color.white, lightTint, .5f);
            }
            Vector3 at = Centre(-ChopBeats * .5, lift), fwd = (Centre(.03, lift) - Centre(-.03, lift)).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
            p.T.localPosition = at;
            p.T.localRotation = Quaternion.LookRotation(fwd, Vector3.up);
            p.T.localScale = p.Scale = Vector3.one;
            float dir = UnityEngine.Random.value < .5f ? -1f : 1f;
            p.V = side * dir * r * UnityEngine.Random.Range(4f, 7f) + Vector3.up * r * UnityEngine.Random.Range(5f, 8f) - fwd * r * UnityEngine.Random.Range(0f, 2f);
            p.Axis = (side * UnityEngine.Random.Range(.4f, 1f) + fwd * UnityEngine.Random.Range(-.5f, .5f) + Vector3.up * UnityEngine.Random.Range(-.3f, .3f)).normalized;
            p.Spin = UnityEngine.Random.Range(500f, 900f) * dir;
            p.Born = Time.unscaledTime; p.Life = .75f; p.Rest = rad.y;
            chopPieces.Add(p);
        }

        void UpdateChops()
        {
            if (chopPieces.Count == 0) return;
            float now = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, .05f), g = roadHalf / 5f * 34f;
            for (int i = chopPieces.Count - 1; i >= 0; i--)
            {
                ChopPiece p = chopPieces[i];
                float age = now - p.Born;
                if (age > p.Life) { p.T.gameObject.SetActive(false); freeChops.Push(p); chopPieces.RemoveAt(i); continue; }
                p.V.y -= g * dt;
                Vector3 pos = p.T.localPosition + p.V * dt;
                if (pos.y < p.Rest && p.V.y < 0) { pos.y = p.Rest; p.V = new Vector3(p.V.x * .5f, -p.V.y * .3f, p.V.z * .5f); p.Spin *= .5f; }
                p.T.localPosition = pos;
                p.T.localRotation = Quaternion.AngleAxis(p.Spin * dt, p.Axis) * p.T.localRotation;
                p.T.localScale = p.Scale * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p.Life - age) / .25f));
            }
        }

        // ---------------------------------------------------------------- the lengthwise split (round 13; replaced by chopping)

        /// <summary>Section of a kind at a point: half width / half height, centre height, colour, texture v.</summary>
        void Section(string kind, float r, double a, out Vector2 rad, out float lift, out Color col, out float v)
        {
            double u = U(a);
            switch (kind)
            {
                case "baguette":
                {
                    float t = Taper(u, .12f);
                    rad = new Vector2(r * 1.05f * t, r * .8f * Mathf.Max(.45f, t)); lift = r * .8f; col = Color.white; v = (float)((a - headA) * 1.3);
                    return;
                }
                case "leek":
                {
                    float rr = r * .62f;
                    rad = LeekRad(a, rr); lift = rr; v = (float)((a - headA) * 2.0); col = LeekColor(a);
                    return;
                }
                case "ramen":
                    rad = new Vector2(r * .62f, r * .45f); lift = r * .45f; col = new Color(1f, .74f, .26f); v = 0;
                    return;
                case "candy":
                    rad = new Vector2(r * .55f, r * .55f); lift = r * .55f; col = new Color(1f, .3f, .35f); v = 0;
                    return;
                default:
                {
                    float rr = r * .66f;
                    rad = PencilRad(a, rr); lift = rr; v = 0; col = PencilColor(a);
                    return;
                }
            }
        }

        /// <summary>
        /// The stretch already cut (from the blade back toward the camera, <paramref name="span"/> beats): two half shells,
        /// each rotated open around its own length so the flat cut faces turn up toward the camera, and pushed apart the
        /// farther they are from the blade — a zipper tearing open. The faces show the lengthwise cross-section picture.
        /// </summary>
        void BuildSplit(string kind, float r, double span)
        {
            Clear(); T2.Clear();
            const int n = 18, sides = 10;
            for (int half = 0; half < 2; half++)
            {
                float s = half == 0 ? -1f : 1f;
                int shellFirst = V.Count;
                var faceVerts = new List<int>(n * 2);
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / (n - 1);                  // 0 at the blade, 1 nearest the camera
                    double a = -span * t;
                    Vector2 rad; float lift; Color col; float v;
                    Section(kind, r, a, out rad, out lift, out col, out v);
                    Vector3 c = Centre(a, lift);
                    Vector3 fwd = Centre(a + .02, lift) - Centre(a - .02, lift);
                    fwd = fwd.sqrMagnitude > 1e-10f ? fwd.normalized : Vector3.forward;
                    Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized, up = Vector3.Cross(fwd, side).normalized;
                    float ease = 1f - (1f - t) * (1f - t);
                    float open = Mathf.Deg2Rad * (18f + 72f * ease);     // the halves fall open like a book
                    float psi = -s * open;
                    float cs = Mathf.Cos(psi), sn = Mathf.Sin(psi);
                    float peel = s * r * (.1f + .75f * ease);
                    rad *= 1f - .3f * t; // nearer the camera the halves shrink a little, so they never swamp the hero
                    // section points of this half (the cut plane is at local x = 0), rotated open
                    var pts = new Vector2[sides + 1]; var nrm = new Vector2[sides + 1];
                    float minY = float.MaxValue;
                    for (int j = 0; j <= sides; j++)
                    {
                        float phi = Mathf.PI * ((float)j / sides - .5f);   // -90°..90° around the outside of this half
                        Vector2 p = new Vector2(s * Mathf.Cos(phi) * rad.x, Mathf.Sin(phi) * rad.y);
                        Vector2 q = new Vector2(s * Mathf.Cos(phi), Mathf.Sin(phi));
                        pts[j] = new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
                        nrm[j] = new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);
                        minY = Mathf.Min(minY, pts[j].y);
                    }
                    float rest = (-lift + r * .02f) - minY; // the half lies on the road
                    for (int j = 0; j <= sides; j++)
                    {
                        V.Add(c + side * (pts[j].x + peel) + up * (pts[j].y + rest));
                        N.Add((side * nrm[j].x + up * nrm[j].y).normalized);
                        UV.Add(new Vector2((float)j / sides, v));
                        C.Add(col);
                    }
                    if (i > 0)
                    {
                        int stride = sides + 3; // each ring: the shell, then the two face vertices
                        int r0 = shellFirst + (i - 1) * stride, r1 = shellFirst + i * stride;
                        for (int j = 0; j < sides; j++) { T.Add(r0 + j); T.Add(r1 + j); T.Add(r1 + j + 1); T.Add(r0 + j); T.Add(r1 + j + 1); T.Add(r0 + j + 1); }
                    }
                    // the flat cut face between the two edges of the half (j = 0 and j = sides)
                    Vector2 fn = new Vector2(-s * cs, -s * sn);
                    Vector3 faceN = (side * fn.x + up * fn.y).normalized;
                    float fv = (float)Mathf.Clamp01((float)U(a));
                    for (int e = 0; e < 2; e++)
                    {
                        int j = e == 0 ? sides : 0;
                        faceVerts.Add(V.Count);
                        V.Add(c + side * (pts[j].x + peel) + up * (pts[j].y + rest) + faceN * r * .01f);
                        N.Add(faceN);
                        // the left half shows the picture, the right half its mirror: the two faces match like an opened book
                        UV.Add(new Vector2(half == 0 ? (e == 0 ? 1f : 0f) : (e == 0 ? 0f : 1f), fv));
                        C.Add(Color.white);
                    }
                    if (i > 0)
                    {
                        int a0 = faceVerts[(i - 1) * 2], a1 = faceVerts[(i - 1) * 2 + 1], b0 = faceVerts[i * 2], b1 = faceVerts[i * 2 + 1];
                        T2.Add(a0); T2.Add(b0); T2.Add(b1); T2.Add(a0); T2.Add(b1); T2.Add(a1);
                    }
                }
            }
        }

        static void ApplySplit(Mesh m)
        {
            m.Clear();
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetColors(C);
            m.subMeshCount = 2;
            m.SetTriangles(T, 0); m.SetTriangles(T2, 1);
            m.RecalculateBounds();
        }

        static float CentreHeight(string kind, float r) { return kind == "baguette" ? r * .8f : kind == "leek" || kind == "pencil" ? r * .62f : kind == "ramen" ? r * .55f : r; }

        /// <summary>0 at the head, 1 at the tail, for a point <paramref name="ahead"/> beats down the road.</summary>
        double U(double ahead) { return (ahead - headA) / lengthB; }

        double SampleAhead(int i, int n) { return nearA + (farA - nearA) * i / (n - 1); }

        // ---------------------------------------------------------------- kinds

        void BuildBaguette(float r)
        {
            const int n = 40;
            Tube(n, 14, i => SampleAhead(i, n), a => { float t = Taper(U(a), .12f); return new Vector2(r * 1.05f * t, r * .8f * Mathf.Max(.45f, t)); },
                 a => r * .8f, a => Color.white, a => (float)((a - headA) * 1.3), true, true);
        }

        // round 15: the parts that say what it is (the pencil's sharpened tip and eraser, the leek's white root and fanned
        // leaves) have a fixed size in beats, so they stay big and readable however long the object is

        double FromHead(double a) { return a - headA; }
        double FromTail(double a) { return headA + lengthB - a; }

        static readonly Color LeekWhite = new Color(1f, 1f, .95f), LeekPale = new Color(.82f, 1f, .55f), LeekGreen = new Color(.3f, .85f, .25f); // round 17: brighter, fresher green

        Color LeekColor(double a)
        {
            double h = FromHead(a);
            if (h < .7) return Color.Lerp(LeekWhite, new Color(.96f, .98f, .88f), (float)(h / .7));
            return Color.Lerp(LeekPale, LeekGreen, Mathf.SmoothStep(0, 1, (float)((h - .7) / .8)));
        }

        Vector2 LeekRad(double a, float rr)
        {
            double h = FromHead(a);
            float t = h < .08 ? Mathf.Lerp(.55f, 1f, (float)(h / .08)) : 1f; // a rounded root end
            return new Vector2(rr * t, rr * t);
        }

        void BuildLeek(float r)
        {
            const int n = 40;
            float rr = r * .62f;
            double leafFrom = Math.Max(FromHead(nearA), lengthB - .9); // the last 0.9 beat splits into three leaves
            double stalkFar = Math.Min(farA, headA + Math.Max(.2, lengthB - .9) + .05);
            if (stalkFar > nearA + .02)
            {
                double sNear = nearA, sFar = stalkFar;
                Tube(n, 12, i => sNear + (sFar - sNear) * i / (n - 1), a => LeekRad(a, rr), a => rr, LeekColor, a => (float)((a - headA) * 2.0), true, false);
            }
            // three leaves fanning out (left, middle lifted, right), dark green, slightly flattened
            double l0 = Math.Max(nearA, headA + leafFrom), l1 = farA;
            if (l1 > l0 + .02)
                for (int k = -1; k <= 1; k++)
                {
                    int kk = k;
                    Tube(16, 8, i => l0 + (l1 - l0) * i / 15,
                         a => { float t = (float)((FromHead(a) - (lengthB - .9)) / .9); return new Vector2(rr * .6f * (1f - .45f * t), rr * .4f * (1f - .4f * t)); },
                         a => rr * .8f, a => LeekColor(a) * (kk == 0 ? 1f : .88f), a => 0f, false, true,
                         a => { float t = Mathf.Clamp01((float)((FromHead(a) - (lengthB - .9)) / .9)); return new Vector2(kk * rr * 1.9f * t * t, (kk == 0 ? rr * .55f : 0f) * t); });
                }
            // the white root hairs, a little tuft beyond the root end
            if (nearA <= headA + .01)
                for (int k = 0; k < 5; k++)
                {
                    float ang = (k - 2) * .45f;
                    Tube(5, 4, i => headA - .16 * i / 4.0, a => new Vector2(rr * .07f, rr * .07f), a => rr * .55f, a => new Color(.93f, .88f, .76f), a => 0f, false, false,
                         a => { float t = (float)((headA - a) / .16); return new Vector2(Mathf.Sin(ang) * rr * 1.2f * t, -rr * .3f * t + Mathf.Cos(ang * 2f) * rr * .2f * t); });
                }
        }

        static readonly Color Graphite = new Color(.12f, .12f, .14f), Wood = new Color(1f, .88f, .66f), Paint = new Color(1f, .86f, .1f),
                              Metal = new Color(.82f, .82f, .88f), Eraser = new Color(1f, .5f, .58f);

        Color PencilColor(double a)
        {
            double h = FromHead(a), t = FromTail(a);
            if (h < .12) return Graphite;
            if (h < .5) return Wood;
            if (t < .28) return Eraser;
            if (t < .5) return Metal * ((int)(t * 30) % 2 == 0 ? 1f : .78f); // ridged ferrule
            return Paint;
        }

        Vector2 PencilRad(double a, float rr)
        {
            double h = FromHead(a), t = FromTail(a);
            float rad = h < .5 ? rr * Mathf.Max(.05f, (float)(h / .5)) : rr;
            if (t < .5 && t >= .28) rad *= 1.06f;
            if (t < .06) rad *= Mathf.Sqrt(Mathf.Max(.05f, (float)(t / .06)));
            return new Vector2(rad, rad);
        }

        void BuildPencil(float r)
        {
            const int n = 56;
            float rr = r * .66f;
            Tube(n, 6, i => SampleAhead(i, n), a => PencilRad(a, rr), a => rr, PencilColor, a => 0f, true, true, null, true);
        }

        void BuildRamen(float r)
        {
            const int n = 46;
            float strand = r * .3f;
            Color noodle = new Color(1.25f, 1.15f, .9f); // the noodle tile carries the curls; round 17: brighter gold
            for (int k = 0; k < 5; k++)
            {
                float phase = k * Mathf.PI * .4f;
                Color tint = noodle * (.93f + .04f * k); tint.a = 1;
                Tube(n, 7, i => SampleAhead(i, n), a => new Vector2(strand, strand),
                     a => 0, a => tint, a => (float)((a - headA) * 2.5), true, true,
                     a =>
                     {
                         // the strands twist around each other along the bundle
                         float th = (float)((a - headA) * Mathf.PI * 2.4) + phase;
                         // plus a small fast wave of their own: noodles are curly, not rods
                         float wave = Mathf.Sin((float)((a - headA) * Mathf.PI * 9.0) + phase * 2.3f) * r * .07f;
                         return new Vector2(Mathf.Cos(th) * r * .3f + wave, r * .4f + Mathf.Sin(th) * r * .2f + wave * .6f);
                     });
            }
            // toppings sit on the bundle: naruto discs and green onion rings, once per beat of noodle
            for (double s = .5; s < lengthB; s += 1.0)
            {
                double a = headA + s;
                if (a < nearA || a > farA) continue;
                // round 17: bigger, brighter toppings so the bowl reads as ramen from far away
                Disc(Centre(a, r * .86f), r * .66f, r * .18f);
                Ring(Centre(a + .28, r * .8f) + new Vector3(r * .3f, 0, 0), r * .21f, new Color(.42f, .95f, .3f));
                Ring(Centre(a - .25, r * .78f) - new Vector3(r * .25f, 0, 0), r * .2f, new Color(.5f, 1f, .35f));
            }
        }

        /// <summary>A twisted candy rope (round 16, replaces the toy train): red and white stripes spiralling along it.</summary>
        void BuildCandy(float r)
        {
            const int n = 52;
            float rr = r * .55f;
            Color red = new Color(1f, .16f, .22f), white = new Color(1f, .98f, .96f);
            Tube(n, 16, i => SampleAhead(i, n), a => { double h = FromHead(a), t = FromTail(a); float k = h < .1 ? Mathf.Lerp(.6f, 1f, (float)(h / .1)) : t < .1 ? Mathf.Lerp(.6f, 1f, (float)(t / .1)) : 1f; return new Vector2(rr * k, rr * k); },
                 a => rr, a => white, a => 0f, true, true, null, false,
                 (a, ang) =>
                 {
                     // three red stripes winding round the rope
                     float s = Mathf.Repeat(ang / (Mathf.PI * 2f) * 3f + (float)((a - headA) * 2.2), 1f);
                     return s < .42f ? red : white;
                 });
        }

        /// <summary>The glowing lane under a slide, with chevrons on its bends pointing where the finger goes next.</summary>
        void BuildGuide(Mesh m, float r)
        {
            Clear();
            const int n = 40;
            float half = r * 2.1f, pulse = .8f + .2f * Mathf.Sin(Time.unscaledTime * 8f);
            for (int i = 0; i < n; i++)
            {
                double a = SampleAhead(i, n);
                Vector3 c = Centre(a, .006f);
                Vector3 fwd = Centre(a + .03, .006f) - Centre(a - .03, .006f);
                Vector3 side = Vector3.Cross(Vector3.up, fwd.sqrMagnitude > 0 ? fwd.normalized : Vector3.forward).normalized;
                float fade = Mathf.Clamp01((float)(Math.Min(a - nearA, farA - a) / .25 + .2)) * pulse;
                V.Add(c - side * half); V.Add(c + side * half);
                N.Add(Vector3.up); N.Add(Vector3.up);
                UV.Add(new Vector2(0, .5f)); UV.Add(new Vector2(1, .5f));
                C.Add(new Color(1, 1, 1, fade)); C.Add(new Color(1, 1, 1, fade));
                if (i > 0) { int k = i * 2; T.Add(k - 2); T.Add(k); T.Add(k + 1); T.Add(k - 2); T.Add(k + 1); T.Add(k - 1); }
            }
            // chevrons: on a bend, every 0.2 beat, pointing toward the lane it turns to
            for (double a = Math.Ceiling(nearA / .2) * .2; a < farA; a += .2)
            {
                float slope = lateralOf(a + .06) - lateralOf(a - .06);
                if (Mathf.Abs(slope) < .05f) continue;
                float dir = Mathf.Sign(slope);
                Vector3 c = Centre(a, .012f);
                Vector3 fwd = (Centre(a + .03, .012f) - Centre(a - .03, .012f)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
                float s = r * 1.2f;
                int k = V.Count;
                V.Add(c + side * dir * s); V.Add(c - side * dir * s * .2f + fwd * s * .7f); V.Add(c - side * dir * s * .2f - fwd * s * .7f);
                for (int q = 0; q < 3; q++) { N.Add(Vector3.up); UV.Add(new Vector2(.5f, .5f)); C.Add(new Color(1, 1, 1, 1)); }
                T.Add(k); T.Add(k + 1); T.Add(k + 2);
            }
            Apply(m);
        }

        void BuildTrain(float r)
        {
            int cars = Mathf.Clamp(Mathf.RoundToInt((float)lengthB * 1.25f), 2, 6);
            double carLen = lengthB / cars;
            Color[] paint = { new Color(.9f, .18f, .14f), new Color(1f, .82f, .2f), new Color(.2f, .5f, .95f), new Color(.3f, .78f, .35f), new Color(1f, .55f, .15f), new Color(.7f, .35f, .9f) };
            for (int k = 0; k < cars; k++)
            {
                double s0 = headA + k * carLen + carLen * .07, s1 = headA + (k + 1) * carLen - carLen * .07;
                if (s0 < nearA - 1e-6 || s0 > farA) continue; // a car that reached the blade has come loose
                s1 = Math.Min(s1, farA + carLen);
                Vector3 p0 = Centre(s0, r * .2f), p1 = Centre(s1, r * .2f);
                Vector3 dir = p1 - p0; float len = dir.magnitude;
                if (len < 1e-4f) continue;
                dir /= len;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                float w = r * 1.1f, h = r * 1.15f;
                Color body = paint[k % paint.Length];
                Box(p0 + dir * len * .5f + Vector3.up * (r * .28f + h * .5f), dir, side, len * .5f, w, h * .5f, body);
                // a darker roof rim and four wheels
                Box(p0 + dir * len * .5f + Vector3.up * (r * .28f + h + r * .06f), dir, side, len * .46f, w * .92f, r * .06f, body * .75f + new Color(0, 0, 0, .25f));
                foreach (float f in new[] { .22f, .78f })
                    foreach (float sd in new[] { -1f, 1f })
                        Box(p0 + dir * len * f + side * sd * w * 1.02f + Vector3.up * r * .28f, dir, side, r * .26f, r * .08f, r * .26f, new Color(.25f, .16f, .1f));
                // windows on both sides (dark panes with a light frame)
                foreach (float sd in new[] { -1f, 1f })
                    foreach (float f in k == 0 ? new[] { .78f } : new[] { .3f, .7f })
                        Box(p0 + dir * len * f + side * sd * (w + r * .015f) + Vector3.up * (r * .28f + h * .68f), dir, side, len * (k == 0 ? .1f : .12f), r * .02f, h * .16f, new Color(.18f, .22f, .32f));
                if (k == 0)
                {
                    // locomotive: a taller cab at the back, a chimney at the front and a golden lamp
                    Box(p0 + dir * len * .78f + Vector3.up * (r * .28f + h + r * .22f), dir, side, len * .2f, w * .95f, r * .22f, body * .85f + new Color(0, 0, 0, .15f));
                    Box(p0 + dir * len * .25f + Vector3.up * (r * .28f + h + r * .32f), dir, side, r * .2f, r * .2f, r * .3f, new Color(.15f, .15f, .17f));
                    Box(p0 + dir * .01f + Vector3.up * (r * .28f + h * .55f), dir, side, r * .05f, r * .28f, r * .22f, new Color(1f, .85f, .35f));
                }
            }
        }

        // ---------------------------------------------------------------- mesh pieces

        static float Taper(double u, float edge)
        {
            // rounded ends: a quarter-circle over the first and last part of the object
            if (u < edge) return Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((float)((edge - u) / edge), 2f))) * .92f + .08f;
            if (u > 1 - edge) return Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((float)((u - (1 - edge)) / edge), 2f))) * .92f + .08f;
            return 1f;
        }

        static void Clear() { V.Clear(); N.Clear(); UV.Clear(); C.Clear(); T.Clear(); }

        static void Apply(Mesh m)
        {
            m.Clear();
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetColors(C); m.SetTriangles(T, 0);
            m.RecalculateBounds();
        }

        /// <summary>
        /// A tube along the object: <paramref name="radius"/> gives (half width, half height) of its section at a point,
        /// <paramref name="lift"/> its centre height, <paramref name="offset"/> an extra (sideways, up) shift (twisted strands).
        /// </summary>
        void Tube(int n, int sides, Func<int, double> aheadAt, Func<double, Vector2> radius, Func<double, float> lift, Func<double, Color> color,
                  Func<double, float> v, bool capNear, bool capFar, Func<double, Vector2> offset = null, bool flat = false, Func<double, float, Color> colorAt = null)
        {
            int first = V.Count;
            int stride = flat ? sides * 2 : sides + 1; // flat: every facet has its own two edge vertices (crisp edges, e.g. a hexagonal pencil)
            var centres = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                double a = aheadAt(i);
                Vector3 c = Centre(a, lift(a));
                if (offset != null)
                {
                    Vector3 fwd = Centre(a + .02, 0) - Centre(a - .02, 0);
                    Vector3 sd = Vector3.Cross(Vector3.up, fwd.sqrMagnitude > 0 ? fwd.normalized : Vector3.forward).normalized;
                    Vector2 off = offset(a);
                    c += sd * off.x + Vector3.up * off.y;
                }
                centres[i] = c;
            }
            for (int i = 0; i < n; i++)
            {
                double a = aheadAt(i);
                Vector3 tangent = (centres[Math.Min(n - 1, i + 1)] - centres[Math.Max(0, i - 1)]);
                tangent = tangent.sqrMagnitude > 1e-10f ? tangent.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized, up = Vector3.Cross(tangent, side).normalized;
                Vector2 rad = radius(a);
                Color col = color(a);
                float vv = v(a);
                if (flat)
                {
                    float rot = Mathf.PI / sides + Mathf.PI * .5f; // a flat facet faces straight up
                    for (int j = 0; j < sides; j++)
                    {
                        float a0 = (float)j / sides * Mathf.PI * 2f + rot, a1 = (float)(j + 1) / sides * Mathf.PI * 2f + rot, am = (a0 + a1) * .5f;
                        Vector3 fn = side * Mathf.Cos(am) + up * Mathf.Sin(am);
                        foreach (float ang in new[] { a0, a1 })
                        {
                            V.Add(centres[i] + side * Mathf.Cos(ang) * rad.x + up * Mathf.Sin(ang) * rad.y);
                            N.Add(fn); UV.Add(new Vector2((float)j / sides, vv)); C.Add(col);
                        }
                    }
                }
                else
                    for (int j = 0; j <= sides; j++)
                    {
                        float ang = (float)j / sides * Mathf.PI * 2f;
                        Vector3 dirOut = side * Mathf.Cos(ang) + up * Mathf.Sin(ang);
                        V.Add(centres[i] + side * Mathf.Cos(ang) * rad.x + up * Mathf.Sin(ang) * rad.y);
                        N.Add(dirOut);
                        UV.Add(new Vector2((float)j / sides, vv));
                        C.Add(colorAt != null ? colorAt(a, ang) : col);
                    }
                if (i > 0)
                {
                    int r0 = first + (i - 1) * stride, r1 = first + i * stride;
                    for (int j = 0; j < sides; j++)
                    {
                        int p = flat ? 2 * j : j, q = flat ? 2 * j + 1 : j + 1;
                        T.Add(r0 + p); T.Add(r1 + p); T.Add(r1 + q); T.Add(r0 + p); T.Add(r1 + q); T.Add(r0 + q);
                    }
                }
            }
            if (capNear) Cap(first, sides, centres[0], (centres[0] - centres[Math.Min(1, n - 1)]).normalized, color(aheadAt(0)), flat);
            if (capFar) Cap(first + (n - 1) * stride, sides, centres[n - 1], (centres[n - 1] - centres[Math.Max(0, n - 2)]).normalized, color(aheadAt(n - 1)), flat);
        }

        static void Cap(int ring, int sides, Vector3 centre, Vector3 normal, Color col, bool flat = false)
        {
            int c = V.Count;
            V.Add(centre); N.Add(normal); UV.Add(new Vector2(.5f, .5f)); C.Add(col);
            for (int j = 0; j < sides; j++) { T.Add(c); T.Add(ring + (flat ? 2 * j : j)); T.Add(ring + (flat ? 2 * j + 1 : j + 1)); }
        }

        /// <summary>A naruto slice lying flat: white with a pink swirl suggested by rings.</summary>
        static void Disc(Vector3 centre, float radius, float thickness)
        {
            const int sides = 16;
            Color white = new Color(1f, .98f, .96f), pink = new Color(1f, .45f, .6f);
            int c = V.Count;
            Vector3 top = centre + Vector3.up * thickness;
            V.Add(top); N.Add(Vector3.up); UV.Add(Vector2.zero); C.Add(pink);
            float[] radii = { .22f, .45f, .7f, 1f };
            Color[] cols = { white, pink, white, white };
            int prev = -1;
            for (int ringIx = 0; ringIx < radii.Length; ringIx++)
            {
                int start = V.Count;
                for (int j = 0; j <= sides; j++)
                {
                    float ang = (float)j / sides * Mathf.PI * 2f + ringIx * .6f; // the rings drift round: a swirl
                    V.Add(top + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * radius * radii[ringIx]); N.Add(Vector3.up); UV.Add(Vector2.zero); C.Add(cols[ringIx]);
                }
                for (int j = 0; j < sides; j++)
                {
                    if (prev < 0) { T.Add(c); T.Add(start + j); T.Add(start + j + 1); }
                    else { T.Add(prev + j); T.Add(start + j); T.Add(start + j + 1); T.Add(prev + j); T.Add(start + j + 1); T.Add(prev + j + 1); }
                }
                prev = start;
            }
            // the rim
            int rim0 = V.Count;
            for (int j = 0; j <= sides; j++)
            {
                float ang = (float)j / sides * Mathf.PI * 2f + 1.8f;
                Vector3 o = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                V.Add(top + o * radius); N.Add(o); UV.Add(Vector2.zero); C.Add(white * .92f);
                V.Add(centre + o * radius); N.Add(o); UV.Add(Vector2.zero); C.Add(white * .85f);
            }
            for (int j = 0; j < sides; j++) { int k = rim0 + j * 2; T.Add(k); T.Add(k + 1); T.Add(k + 3); T.Add(k); T.Add(k + 3); T.Add(k + 2); }
        }

        /// <summary>A small green onion ring (a short hollow-looking tube standing on its side).</summary>
        static void Ring(Vector3 centre, float radius, Color col)
        {
            const int sides = 10;
            int first = V.Count;
            for (int layer = 0; layer < 2; layer++)
                for (int j = 0; j <= sides; j++)
                {
                    float ang = (float)j / sides * Mathf.PI * 2f;
                    Vector3 o = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0);
                    V.Add(centre + o * radius + new Vector3(0, 0, layer == 0 ? -radius * .45f : radius * .45f)); N.Add(o); UV.Add(Vector2.zero); C.Add(col * (layer == 0 ? 1f : .85f));
                }
            for (int j = 0; j < sides; j++) { int a = first + j, b = first + sides + 1 + j; T.Add(a); T.Add(b); T.Add(b + 1); T.Add(a); T.Add(b + 1); T.Add(a + 1); }
        }

        /// <summary>An oriented box (half sizes along the object, across it and up).</summary>
        static void Box(Vector3 centre, Vector3 fwd, Vector3 side, float halfLen, float halfWidth, float halfHeight, Color col)
        {
            Vector3 f = fwd * halfLen, s = side * halfWidth, u = Vector3.up * halfHeight;
            Face(centre + u, f, s, Vector3.up, col);
            Face(centre - u, s, f, -Vector3.up, col * .6f);
            Face(centre + s, u, f, side, col * .9f);
            Face(centre - s, f, u, -side, col * .9f);
            Face(centre + f, s, u, fwd, col * .95f);
            Face(centre - f, u, s, -fwd, col * .8f);
        }

        static void Face(Vector3 c, Vector3 a, Vector3 b, Vector3 n, Color col)
        {
            col.a = 1;
            int k = V.Count;
            V.Add(c - a - b); V.Add(c + a - b); V.Add(c + a + b); V.Add(c - a + b);
            for (int i = 0; i < 4; i++) { N.Add(n); UV.Add(Vector2.zero); C.Add(col); }
            T.Add(k); T.Add(k + 1); T.Add(k + 2); T.Add(k); T.Add(k + 2); T.Add(k + 3);
        }

        /// <summary>A soft dark band on the road under the object.</summary>
        void BuildShadow(Mesh m, float halfWidth)
        {
            Clear();
            const int n = 30;
            for (int i = 0; i < n; i++)
            {
                double a = SampleAhead(i, n);
                Vector3 c = Centre(a, .004f);
                Vector3 fwd = Centre(a + .02, .004f) - Centre(a - .02, .004f);
                Vector3 side = Vector3.Cross(Vector3.up, fwd.sqrMagnitude > 0 ? fwd.normalized : Vector3.forward).normalized;
                float fade = Mathf.Clamp01((float)(Math.Min(a - nearA, farA - a) / .2 + .3));
                V.Add(c - side * halfWidth); V.Add(c + side * halfWidth);
                N.Add(Vector3.up); N.Add(Vector3.up);
                UV.Add(new Vector2(0, .5f)); UV.Add(new Vector2(1, .5f));
                C.Add(new Color(1, 1, 1, fade)); C.Add(new Color(1, 1, 1, fade));
                if (i > 0) { int k = i * 2; T.Add(k - 2); T.Add(k); T.Add(k + 1); T.Add(k - 2); T.Add(k + 1); T.Add(k - 1); }
            }
            Apply(m);
        }

        /// <summary>Fine lengthwise fibres for the leek (a tiny repeating texture, no picture stretched).</summary>
        static Texture2D Stripes()
        {
            var t = new Texture2D(64, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "leek_fibres" };
            for (int x = 0; x < 64; x++)
            {
                float v = .82f + .18f * Mathf.Abs(Mathf.Sin(x * .9f)) * (.6f + .4f * Mathf.Sin(x * .23f + 1f)); // leek fibres
                for (int y = 0; y < 8; y++) t.SetPixel(x, y, new Color(v, v, v, 1));
            }
            t.Apply();
            return t;
        }

        /// <summary>A horizontal soft falloff (centre dark, edges clear) for the long shadows.</summary>
        static Texture2D SoftBand()
        {
            var t = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "long_shadow" };
            for (int x = 0; x < 64; x++)
            {
                float d = Mathf.Abs(x / 63f - .5f) * 2f;
                float a = Mathf.Clamp01(1f - d * d) * Mathf.Clamp01(1f - d);
                for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply();
            return t;
        }
    }
}
