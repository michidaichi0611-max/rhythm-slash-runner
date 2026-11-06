using System.Collections.Generic;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// The play screen's environment as a real 3D scene (owner round 7): road, side walls, far background, roadside posts,
    /// the stage gate and the boss. Perspective is now exact (no more wavy, affinely-stretched walls), the boss stands on
    /// the road with a shadow and recedes into the same fog as the walls, and depth sorting decides what is in front.
    ///
    /// The camera is solved so the road edges land exactly where the UI layer (targets, lanes, hero, HUD) already draws
    /// them: the slash line at <see cref="Perspective.SlashY"/> spans the full width, and the edges converge to the same
    /// vanishing point. The UI layer renders on top of this camera.
    /// </summary>
    public sealed partial class World3D : MonoBehaviour
    {
        const float Fov = 50f, CamHeight = 1f, BackdropDistance = 90f;

        Camera cam, clearCam;
        Transform world;
        float theta, tanHalf, zCam, roadHalf, zFar, aspect = 9f / 16f;
        Material roadMat, wallMatL, wallMatR, shadowMat;
        MeshRenderer road, wallL, wallR;
        readonly Renderer[] backdrops = new Renderer[4]; // 0 main, 1 loop copy, 2 next theme, 3 FEVER picture
        readonly Material[] backdropMats = new Material[4];
        SpriteRenderer gate, gateGlow, gateVeil, bossBody, bossGlow, bossStars, bossCracks, bossFlash;
        Material crackMat, shardMat, shockMat;
        MeshRenderer shock;
        float bossFitAspect, shockAt = -9, flashAt = -9, shockSize = 1;
        static readonly Color CrackDark = new Color(.12f, .03f, .06f), CrackHot = new Color(1f, .97f, .86f);
        MeshRenderer bossShadow;
        readonly List<SpriteRenderer> posts = new List<SpriteRenderer>();
        readonly Stack<SpriteRenderer> freePosts = new Stack<SpriteRenderer>();
        readonly List<double> postZ = new List<double>();
        readonly List<bool> postPassed = new List<bool>();
        double nextPost;
        int postCount;

        public Camera Camera { get { return cam; } }
        public float RoadHalfWidth { get { return roadHalf; } }
        public System.Action PostPassed;

        public void Build()
        {
            world = new GameObject("World3D").transform;
            world.SetParent(transform, false);
            // a full-screen clear behind everything (letterbox bars), then the world camera inside the 9:16 frame
            clearCam = new GameObject("ClearCamera").AddComponent<Camera>();
            clearCam.transform.SetParent(transform, false); // stays on with menus too: it paints the letterbox bars
            clearCam.clearFlags = CameraClearFlags.SolidColor; clearCam.backgroundColor = new Color(.02f, .04f, .09f);
            clearCam.cullingMask = 0; clearCam.depth = -10;
            cam = new GameObject("WorldCamera").AddComponent<Camera>();
            cam.transform.SetParent(world, false);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.02f, .04f, .09f);
            cam.fieldOfView = Fov; cam.nearClipPlane = .05f; cam.farClipPlane = 200f; cam.depth = -5;
            cam.allowHDR = false; cam.allowMSAA = true;
            Shader unlit = Resources.Load<Shader>("Shaders/RRUnlit"), trans = Resources.Load<Shader>("Shaders/RRUnlitTransparent"), back = Resources.Load<Shader>("Shaders/RRBackdrop");
            roadMat = new Material(unlit); wallMatL = new Material(unlit); wallMatR = new Material(unlit);
            shadowMat = new Material(trans) { mainTexture = ArtLibrary.SoftShadow.texture, color = new Color(0, 0, 0, .55f) };
            road = Quad("Road", roadMat);
            wallL = Quad("WallL", wallMatL);
            wallR = Quad("WallR", wallMatR);
            for (int i = 0; i < backdrops.Length; i++)
            {
                backdropMats[i] = new Material(back);
                backdropMats[i].renderQueue = 2900 + i; // fixed order behind everything transparent on the road
                var q = Quad("Backdrop" + i, backdropMats[i]);
                q.transform.SetParent(cam.transform, false);
                backdrops[i] = q;
            }
            gate = Sprite("Gate"); gate.sprite = ArtLibrary.Get("gate_arch");
            // round 11: the gate announces itself — a halo behind it and a shimmering rainbow veil in its opening
            gateGlow = Sprite("GateGlow"); gateGlow.sprite = ArtLibrary.Glow; gateGlow.enabled = false;
            gateVeil = Sprite("GateVeil"); gateVeil.sprite = ArtLibrary.Glow; gateVeil.enabled = false;
            bossShadow = Quad("BossShadow", shadowMat);
            bossGlow = Sprite("BossGlow"); bossGlow.sprite = ArtLibrary.Glow;
            bossBody = Sprite("BossBody");
            bossStars = Sprite("BossStars"); bossStars.sprite = ArtLibrary.Get("vfx_sparkle") ?? ArtLibrary.Star;
            bossStars.transform.SetParent(bossBody.transform, false);
            // cracks drawn over the boss's own silhouette, revealed little by little as it loses HP (owner round 8)
            Shader crackShader = Resources.Load<Shader>("Shaders/RRBossCracks");
            if (crackShader != null)
            {
                bossCracks = Sprite("BossCracks");
                bossCracks.transform.SetParent(bossBody.transform, false);
                crackMat = new Material(crackShader);
                crackMat.SetTexture("_CrackTex", MakeCracks(512));
                bossCracks.sharedMaterial = crackMat;
                bossCracks.sortingOrder = 1;
                bossCracks.enabled = false;
            }
            // K.O.: shards, a shockwave ring on the road and a flash (owner round 9: the split looked cheap)
            Shader shardShader = Resources.Load<Shader>("Shaders/RRShard");
            shardMat = new Material(shardShader != null ? shardShader : trans) { mainTexture = Texture2D.whiteTexture };
            shockMat = new Material(trans) { mainTexture = ArtLibrary.Ring.texture, color = new Color(1f, .9f, .6f, 0) };
            shock = Quad("Shockwave", shockMat);
            shock.enabled = false;
            bossFlash = Sprite("BossFlash"); bossFlash.sprite = ArtLibrary.Glow; bossFlash.enabled = false;
            // the scene's default camera would render this world again from its own viewpoint, on top: only ours draw
            foreach (Camera other in Camera.allCameras) if (other != cam && other != clearCam) other.enabled = false;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            SetActive(false);
        }

        MeshRenderer Quad(string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(world, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        SpriteRenderer Sprite(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            return go.AddComponent<SpriteRenderer>();
        }

        public void SetActive(bool on) { if (world != null) world.gameObject.SetActive(on); }

        // ---------------------------------------------------------------- camera solving & mapping

        /// <summary>Fits the camera into the 9:16 frame (screen pixels) and solves its pitch / road size against the UI layout.</summary>
        public void Frame(Rect pixels)
        {
            if (pixels.width < 4 || pixels.height < 4) return;
            cam.pixelRect = pixels;
            aspect = pixels.width / pixels.height;
            tanHalf = Mathf.Tan(Fov * .5f * Mathf.Deg2Rad);
            // vanishing point of the UI road edges (straight lines through the slash line and the horizon end)
            float slope = (Perspective.LineHalfWidth - Perspective.HorizonHalfWidth) / (Perspective.SlashY - Perspective.HorizonY);
            float vpY = Perspective.HorizonY - Perspective.HorizonHalfWidth / slope;
            theta = Mathf.Atan((.5f - vpY) * 2f * tanHalf);
            zCam = 0; zCam = -Ground(Perspective.SlashY); // slash line at z = 0
            float g0 = Ground(Perspective.SlashY);
            roadHalf = (CamHeight * Mathf.Sin(theta) + g0 * Mathf.Cos(theta)) * tanHalf * aspect; // road edge exactly at the screen edge
            zFar = ZForY(Perspective.HorizonY);
            cam.transform.localPosition = new Vector3(0, CamHeight, zCam);
            cam.transform.localRotation = Quaternion.Euler(theta * Mathf.Rad2Deg, 0, 0);
            LayoutStatic();
        }

        /// <summary>Ground distance ahead of the camera foot for a screen row (fraction from the top).</summary>
        float Ground(float y)
        {
            float alpha = Mathf.Atan((.5f - y) * 2f * tanHalf);
            float phi = Mathf.Max(.002f, theta - alpha);
            return CamHeight / Mathf.Tan(phi);
        }

        /// <summary>World z of the ground point seen at screen row <paramref name="y"/> (0 = top of the frame).</summary>
        public float ZForY(float y) { return Ground(y) + zCam; }

        /// <summary>World height that covers one screen-height fraction at distance z (for sizing upright things).</summary>
        public float WorldPerHeight(float z)
        {
            float forward = CamHeight * Mathf.Sin(theta) + (z - zCam) * Mathf.Cos(theta);
            return 2f * forward * tanHalf;
        }

        /// <summary>Screen position (x from left, y from top, 0..1 of the frame) of a world point.</summary>
        public Vector2 ToFrame(Vector3 worldPoint)
        {
            Vector3 v = cam.WorldToViewportPoint(world.TransformPoint(worldPoint));
            return new Vector2(v.x, 1f - v.y);
        }

        void LayoutStatic()
        {
            float zNear = ZForY(1.25f);
            float length = zFar - zNear, mid = (zFar + zNear) * .5f;
            road.transform.localPosition = new Vector3(0, 0, mid);
            road.transform.localRotation = Quaternion.Euler(90, 0, 0);
            road.transform.localScale = new Vector3(roadHalf * 2f, length, 1);
            float wallH = .85f * WorldPerHeight(0);
            foreach (MeshRenderer w in new[] { wallL, wallR })
            {
                float side = w == wallL ? -1f : 1f;
                w.transform.localPosition = new Vector3(side * roadHalf * 1.25f, wallH * .5f, mid);
                w.transform.localRotation = Quaternion.Euler(0, side * 90f, 0);
                w.transform.localScale = new Vector3(length, wallH, 1);
            }
            roadLength = length; wallLength = length;
            RenderSettings.fogStartDistance = (zFar - zCam) * .35f;
            RenderSettings.fogEndDistance = (zFar - zCam) * 1.02f;
        }

        float roadLength = 10, wallLength = 10;

        // ---------------------------------------------------------------- scenery

        public void SetRoad(Texture tex, double scroll)
        {
            if (roadMat.mainTexture != tex) { roadMat.mainTexture = tex; if (tex != null) { tex.wrapMode = TextureWrapMode.Repeat; tex.anisoLevel = 8; } }
            const float tile = 1.6f; // world units per tile along the road
            roadMat.mainTextureScale = new Vector2(1.2f, roadLength / tile);
            roadMat.mainTextureOffset = new Vector2(0, (float)(-scroll * .55 % 1.0));
        }

        public void SetWalls(Texture tex, double scroll, Color tint)
        {
            foreach (Material m in new[] { wallMatL, wallMatR })
            {
                if (m.mainTexture != tex) { m.mainTexture = tex; if (tex != null) { tex.wrapMode = TextureWrapMode.Repeat; tex.anisoLevel = 8; tex.filterMode = FilterMode.Trilinear; } }
                float repeats = wallLength / (2.2f * .85f * WorldPerHeight(0) * 1.3f);
                m.mainTextureScale = new Vector2(repeats, 1);
                m.color = tint;
            }
            float u = (float)(scroll * .18 % 1.0);
            wallMatL.mainTextureOffset = new Vector2(-u, 0);
            wallMatR.mainTextureOffset = new Vector2(u, 0);
            wallL.enabled = wallR.enabled = tex != null;
        }

        public void SetFog(Color c) { RenderSettings.fogColor = c; cam.backgroundColor = c; }

        /// <summary>Far background: a picture filling the frame, two copies slowly approaching, plus crossfades.</summary>
        public void SetBackdrop(int layer, Texture tex, float alpha, float zoom, Color tint)
        {
            Renderer r = backdrops[layer];
            r.enabled = tex != null && alpha > .001f;
            if (!r.enabled) return;
            Material m = backdropMats[layer];
            if (m.mainTexture != tex) m.mainTexture = tex;
            m.color = new Color(tint.r, tint.g, tint.b, alpha);
            float dist = BackdropDistance - layer * .5f;
            float h = 2f * dist * tanHalf, w = h * aspect;
            float texAspect = (float)tex.width / tex.height;
            Vector2 size = texAspect > aspect ? new Vector2(h * texAspect, h) : new Vector2(w, w / texAspect);
            // zoom toward the old horizon row so the far view seems to come closer
            float pivotY = (.5f - Perspective.HorizonY) * h;
            r.transform.localPosition = new Vector3(0, pivotY * (1f - zoom), dist);
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(size.x * zoom, size.y * zoom, 1);
        }

        // ---------------------------------------------------------------- roadside posts (world speed, calmer in "弱")

        public void UpdatePosts(double worldScroll, bool calm)
        {
            const double spacing = .55, unitsPerBeat = 1.6;
            double far = worldScroll + (zFar - 0) / unitsPerBeat;
            while (nextPost < far)
            {
                SpriteRenderer p = freePosts.Count > 0 ? freePosts.Pop() : Sprite("Post");
                p.gameObject.SetActive(true);
                p.sprite = ArtLibrary.Get(((postCount >> 1) % 3 == 2) ? "side_banner" : "side_lamp");
                posts.Add(p); postZ.Add(nextPost); postPassed.Add(false);
                p.transform.localPosition = new Vector3((postCount & 1) == 0 ? -1.12f : 1.12f, 0, 0);
                postCount++;
                nextPost += calm ? spacing * 2 : spacing;
            }
            float height = .7f * roadHalf * 2f;
            for (int i = posts.Count - 1; i >= 0; i--)
            {
                float z = (float)((postZ[i] - worldScroll) * unitsPerBeat);
                SpriteRenderer p = posts[i];
                if (z < zCam + .2f) { p.gameObject.SetActive(false); freePosts.Push(p); posts.RemoveAt(i); postZ.RemoveAt(i); postPassed.RemoveAt(i); continue; }
                if (!postPassed[i] && z < 0) { postPassed[i] = true; if (PostPassed != null) PostPassed(); }
                Vector3 pos = p.transform.localPosition;
                float side = pos.x < 0 ? -1f : 1f;
                p.transform.localPosition = new Vector3(side * roadHalf * 1.12f, 0, z);
                if (p.sprite != null)
                {
                    float s = height / (p.sprite.rect.height / p.sprite.pixelsPerUnit);
                    p.transform.localScale = new Vector3(s, s, 1);
                    // pivot of generated sprites is the centre: lift so the base sits on the ground
                    p.transform.localPosition += new Vector3(0, height * .5f, 0);
                }
                p.color = new Color(1, 1, 1, Mathf.Clamp01((zFar - z) / 2f));
            }
        }

        public void ResetPosts()
        {
            foreach (SpriteRenderer p in posts) { p.gameObject.SetActive(false); freePosts.Push(p); }
            posts.Clear(); postZ.Clear(); postPassed.Clear(); nextPost = 1; postCount = 0;
        }

        // ---------------------------------------------------------------- gate

        /// <summary>The stage gate standing across the road at screen row <paramref name="footY"/> (or rushing past when <paramref name="passing"/> &gt; 0).</summary>
        public void SetGate(bool visible, float footY, float alpha, float passing, float near = 0f)
        {
            gate.enabled = visible && gate.sprite != null;
            gateGlow.enabled = gateVeil.enabled = gate.enabled;
            if (!gate.enabled) return;
            // passing: the arch sweeps right past the camera (its pillars and top fly out over the screen edges)
            float z = passing > 0 ? Mathf.Lerp(0, zCam - .3f, 1f - (1f - passing) * (1f - passing)) : ZForY(footY);
            float width = roadHalf * 2f * 1.3f;
            float s = width / (gate.sprite.rect.width / gate.sprite.pixelsPerUnit);
            float h = gate.sprite.rect.height / gate.sprite.pixelsPerUnit * s;
            gate.transform.localPosition = new Vector3(0, h * .5f, z);
            gate.transform.localScale = new Vector3(s, s, 1);
            float fade = passing > .75f ? 1f - (passing - .75f) / .25f : 1f;
            gate.color = new Color(1, 1, 1, alpha * fade);
            // the closer it gets, the brighter the halo and the veil (rainbow drift, pulse on the beat from outside)
            Color hue = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * .35f, 1f), .45f, 1f);
            float gs = h * 1.5f / (ArtLibrary.Glow.rect.height / ArtLibrary.Glow.pixelsPerUnit);
            gateGlow.transform.localPosition = new Vector3(0, h * .5f, z + .05f);
            gateGlow.transform.localScale = new Vector3(gs * 1.2f, gs, 1);
            gateGlow.color = new Color(hue.r, hue.g, hue.b, alpha * fade * (.25f + .45f * near));
            float vs = h * .62f / (ArtLibrary.Glow.rect.height / ArtLibrary.Glow.pixelsPerUnit);
            gateVeil.transform.localPosition = new Vector3(0, h * .4f, z + .01f);
            gateVeil.transform.localScale = new Vector3(vs * .9f, vs, 1);
            gateVeil.color = new Color(1f, 1f, 1f, alpha * fade * (.2f + .5f * near));
        }

        // ---------------------------------------------------------------- boss

        /// <summary>
        /// Places the boss standing on the road at screen row <paramref name="footY"/>; <paramref name="lift"/> raises it
        /// (screen-height fractions; negative = still under the road, hidden by the road's depth).
        /// </summary>
        public void PlaceBoss(bool visible, Sprite sprite, float footY, float lift, float heightFrac, Vector2 scale, float rotation, Color color,
                              Color glowColor, float shadowAlpha, bool stars, float starsRotation)
        {
            bossBody.enabled = bossGlow.enabled = bossShadow.enabled = visible && sprite != null;
            bossStars.enabled = visible && stars;
            if (bossCracks != null && !bossBody.enabled) bossCracks.enabled = false;
            if (!bossBody.enabled) return;
            bossBody.sprite = sprite;
            float z = ZForY(footY);
            float unit = WorldPerHeight(z);
            float h = heightFrac * unit;
            float baseH = sprite.rect.height / sprite.pixelsPerUnit;
            // keep every pose inside the corridor (owner round 9: its hands vanished into the walls). The walls stand at
            // ±1.25 road half-widths; a little headroom is left for the pulses and swells.
            float room = roadHalf * 1.25f * 2f * .92f;
            if (bossFitAspect > 0 && h * bossFitAspect > room) h = room / bossFitAspect;
            float s = h / baseH;
            float y = lift * unit + h * .5f * scale.y;
            bossBody.transform.localPosition = new Vector3(0, y, z);
            bossBody.transform.localScale = new Vector3(s * scale.x, s * scale.y, 1);
            bossBody.transform.localRotation = Quaternion.Euler(0, 0, rotation);
            bossBody.color = color;
            if (bossCracks != null)
            {
                bossCracks.sprite = sprite;
                bossCracks.color = new Color(1, 1, 1, color.a);
            }
            float gs = h * 1.7f / (ArtLibrary.Glow.rect.height / ArtLibrary.Glow.pixelsPerUnit);
            bossGlow.transform.localPosition = new Vector3(0, y, z + .05f);
            bossGlow.transform.localScale = new Vector3(gs * 1.3f, gs, 1);
            bossGlow.color = glowColor;
            bossShadow.transform.localPosition = new Vector3(0, .002f, z);
            bossShadow.transform.localRotation = Quaternion.Euler(90, 0, 0);
            float shadowW = h * .9f * Mathf.Lerp(.5f, 1f, Mathf.Clamp01(1f + lift * 3f));
            bossShadow.transform.localScale = new Vector3(shadowW, shadowW * .35f, 1);
            shadowMat.color = new Color(0, 0, 0, shadowAlpha);
            if (bossStars.enabled)
            {
                bossStars.transform.localPosition = new Vector3(0, baseH * .55f, -.01f);
                bossStars.transform.localScale = Vector3.one * (baseH * .45f / (bossStars.sprite.rect.height / bossStars.sprite.pixelsPerUnit));
                bossStars.transform.localRotation = Quaternion.Euler(0, 0, starsRotation);
                bossStars.color = UiKit.Gold;
            }
        }

        /// <summary>0 = intact, 1 = cracked all over; <paramref name="glow"/> makes light pour out of the cracks (just before it bursts).</summary>
        public void SetBossWear(float amount, float glow = 0f)
        {
            if (bossCracks == null) return;
            bossCracks.enabled = bossBody.enabled && amount > .01f;
            crackMat.SetFloat("_Amount", amount);
            crackMat.SetColor("_CrackColor", Color.Lerp(CrackDark, CrackHot, glow));
        }

        /// <summary>Remembers the widest pose of this boss (opaque width / height) so it always fits between the walls.</summary>
        public void SetBossKind(string id)
        {
            bossFitAspect = 0;
            foreach (string pose in new[] { "", "_attack", "_hurt", "_rage" })
            {
                Sprite s = ArtLibrary.Get(id + pose);
                if (s != null) bossFitAspect = Mathf.Max(bossFitAspect, OpaqueAspect(s));
            }
        }

        static float OpaqueAspect(Sprite s)
        {
            // the tight sprite mesh hugs the opaque pixels (the texture itself is not CPU-readable)
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (Vector2 v in s.vertices) { minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); }
            float h = s.rect.height / s.pixelsPerUnit;
            return maxX > minX ? (maxX - minX) / h : s.rect.width / s.rect.height;
        }

        // ---------------------------------------------------------------- K.O. shatter

        sealed class Shard { public Transform T; public MeshRenderer R; public Mesh M; public Vector3 V, Axis, Scale; public float Spin, Life; public int Bounces; }
        Color shardInside = Color.white;
        readonly List<Shard> shards = new List<Shard>();
        readonly Stack<Shard> freeShards = new Stack<Shard>();
        float shatterAt = -9;

        /// <summary>
        /// The boss bursts: its sprite is cut along a jittered grid into ~40 triangular pieces that fly out in 3D, tumble,
        /// bounce on the road and fade; a ring races out over the road and a flash fills the middle. The first 0.1 s the
        /// pieces hang in the air (hit-stop), then they move in slow motion for a moment.
        /// </summary>
        public void Shatter(Sprite sprite, Color inside)
        {
            if (sprite == null || bossBody.sprite == null) return;
            foreach (Shard old in shards) { old.T.gameObject.SetActive(false); freeShards.Push(old); }
            shards.Clear();
            shardInside = inside;
            shardMat.mainTexture = sprite.texture;
            Transform body = bossBody.transform;
            Vector3 bodyScale = body.localScale;
            float height = sprite.bounds.size.y * bodyScale.y;
            Bounds b = sprite.bounds;
            Rect tr = sprite.textureRect;
            Vector2 uvMin = new Vector2(tr.xMin / sprite.texture.width, tr.yMin / sprite.texture.height);
            Vector2 uvMax = new Vector2(tr.xMax / sprite.texture.width, tr.yMax / sprite.texture.height);
            const int cols = 5, rows = 6; // ~60 pieces: smaller chunks read as "shattered"
            var grid = new Vector2[cols + 1, rows + 1];
            for (int i = 0; i <= cols; i++)
                for (int j = 0; j <= rows; j++)
                {
                    var g = new Vector2((float)i / cols, (float)j / rows);
                    if (i > 0 && i < cols) g.x += Random.Range(-.3f, .3f) / cols;
                    if (j > 0 && j < rows) g.y += Random.Range(-.3f, .3f) / rows;
                    grid[i, j] = g;
                }
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    Vector2 a = grid[i, j], c = grid[i + 1, j], d = grid[i + 1, j + 1], e = grid[i, j + 1];
                    bool flip = Random.value < .5f;
                    AddShard(flip ? new[] { a, c, d } : new[] { a, c, e }, b, uvMin, uvMax, body, height);
                    AddShard(flip ? new[] { a, d, e } : new[] { c, d, e }, b, uvMin, uvMax, body, height);
                }
            shatterAt = Time.unscaledTime;
            // shockwave ring at its feet and a flash at its chest
            shockAt = flashAt = shatterAt; shockSize = height;
            shock.transform.localPosition = new Vector3(0, .01f, body.localPosition.z);
            shock.transform.localRotation = Quaternion.Euler(90, 0, 0);
            bossFlash.transform.localPosition = body.localPosition + new Vector3(0, 0, -.05f);
            bossFlash.enabled = shock.enabled = true;
        }

        void AddShard(Vector2[] tri, Bounds b, Vector2 uvMin, Vector2 uvMax, Transform body, float height)
        {
            Shard s = freeShards.Count > 0 ? freeShards.Pop() : NewShard();
            s.T.gameObject.SetActive(true);
            var local = new Vector3[3];
            var uv = new Vector2[3];
            Vector3 centre = Vector3.zero;
            for (int k = 0; k < 3; k++)
            {
                local[k] = new Vector3(Mathf.Lerp(b.min.x, b.max.x, tri[k].x), Mathf.Lerp(b.min.y, b.max.y, tri[k].y), 0);
                uv[k] = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, tri[k].x), Mathf.Lerp(uvMin.y, uvMax.y, tri[k].y));
                centre += local[k] / 3f;
            }
            for (int k = 0; k < 3; k++) local[k] -= centre;
            // a solid chunk (owner round 10: the pieces were paper-thin): picture on the front and the back, the boss's
            // inside colour on the three broken side walls (only where the picture is solid along that edge)
            float half = height * .035f / Mathf.Max(.001f, body.localScale.y);
            Vector3 front = new Vector3(0, 0, -half), back = new Vector3(0, 0, half);
            var verts = new List<Vector3>(18); var uvs = new List<Vector2>(18); var side = new List<Vector2>(18); var cols = new List<Color>(18); var tris = new List<int>(24);
            Color backShade = new Color(.85f, .85f, .85f), wall = shardInside * .95f;
            for (int k = 0; k < 3; k++) { verts.Add(local[k] + front); uvs.Add(uv[k]); side.Add(Vector2.zero); cols.Add(Color.white); }
            for (int k = 0; k < 3; k++) { verts.Add(local[k] + back); uvs.Add(uv[k]); side.Add(Vector2.zero); cols.Add(backShade); }
            tris.AddRange(new[] { 0, 1, 2, 5, 4, 3 });
            for (int k = 0; k < 3; k++)
            {
                int n = (k + 1) % 3, i0 = verts.Count;
                Color shade = wall * (k == 0 ? 1f : k == 1 ? .9f : .8f); shade.a = 1;
                verts.Add(local[k] + front); verts.Add(local[n] + front); verts.Add(local[n] + back); verts.Add(local[k] + back);
                uvs.Add(uv[k]); uvs.Add(uv[n]); uvs.Add(uv[n]); uvs.Add(uv[k]);
                for (int q = 0; q < 4; q++) { side.Add(Vector2.right); cols.Add(shade); }
                tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            }
            s.M.Clear();
            s.M.SetVertices(verts); s.M.SetUVs(0, uvs); s.M.SetUVs(1, side); s.M.SetColors(cols); s.M.SetTriangles(tris, 0);
            s.M.RecalculateNormals(); // every face has its own vertices, so the normals are flat per face
            s.M.RecalculateBounds();
            s.T.localPosition = world.InverseTransformPoint(body.TransformPoint(centre));
            s.T.localRotation = body.localRotation;
            // depth scales like the width (the body's own z scale is 1, which made the pieces five times too thick)
            s.T.localScale = s.Scale = new Vector3(body.localScale.x, body.localScale.y, Mathf.Abs(body.localScale.x));
            // outward from the middle of the body, a little up and toward the camera
            Vector2 dir = new Vector2(centre.x / Mathf.Max(.01f, b.extents.x), centre.y / Mathf.Max(.01f, b.extents.y));
            float speed = height * Random.Range(1.1f, 2.2f);
            s.V = new Vector3(dir.x * speed, height * Random.Range(.8f, 1.8f) + dir.y * speed * .5f, -height * Random.Range(0f, .45f)); // mostly sideways and up: pieces must not cover the screen
            s.Axis = Random.onUnitSphere; s.Spin = Random.Range(240f, 720f);
            s.Life = Random.Range(1.5f, 2.1f); s.Bounces = 0;
            shards.Add(s);
        }

        Shard NewShard()
        {
            var go = new GameObject("Shard");
            go.transform.SetParent(world, false);
            var m = new Mesh();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = shardMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return new Shard { T = go.transform, R = r, M = m };
        }

        void Update()
        {
            UpdateChops();
            if (shards.Count == 0 && !shock.enabled && !bossFlash.enabled) return;
            float now = Time.unscaledTime, age = now - shatterAt;
            // hit-stop, then slow motion that eases back to full speed
            float dt = age < .1f ? 0f : Mathf.Min(Time.unscaledDeltaTime, .05f) * Mathf.Lerp(.35f, 1f, Mathf.Clamp01((age - .1f) / .45f));
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                Shard s = shards[i];
                if (age > s.Life) { s.T.gameObject.SetActive(false); freeShards.Push(s); shards.RemoveAt(i); continue; }
                s.V.y -= shockSize * 5.5f * dt;
                Vector3 p = s.T.localPosition + s.V * dt;
                if (p.y < shockSize * .03f && s.V.y < 0)
                {
                    // land on the road and bounce, losing energy
                    p.y = shockSize * .03f;
                    s.V = new Vector3(s.V.x * .55f, -s.V.y * (s.Bounces == 0 ? .38f : .2f), s.V.z * .55f);
                    s.Spin *= .5f; s.Bounces++;
                }
                s.T.localPosition = p;
                s.T.localRotation = Quaternion.AngleAxis(s.Spin * dt, s.Axis) * s.T.localRotation;
                // solid pieces shrink away at the end instead of fading (they are alpha-tested, not blended)
                float fade = Mathf.Clamp01((s.Life - age) / .45f);
                s.T.localScale = s.Scale * Mathf.SmoothStep(0f, 1f, fade);
            }
            float k = Mathf.Clamp01((now - shockAt) / .55f);
            shock.enabled = k < 1f;
            if (shock.enabled)
            {
                float w = shockSize * Mathf.Lerp(.4f, 4.5f, 1f - (1f - k) * (1f - k));
                shock.transform.localScale = new Vector3(w, w * .55f, 1);
                shockMat.color = new Color(1f, .9f, .6f, (1f - k) * .85f);
            }
            float f = Mathf.Clamp01((now - flashAt) / .35f);
            bossFlash.enabled = f < 1f;
            if (bossFlash.enabled)
            {
                float gs = shockSize * Mathf.Lerp(1.2f, 3.2f, f) / (ArtLibrary.Glow.rect.height / ArtLibrary.Glow.pixelsPerUnit);
                bossFlash.transform.localScale = new Vector3(gs, gs, 1);
                bossFlash.color = new Color(1f, .97f, .85f, (1f - f) * .95f);
            }
        }

        /// <summary>
        /// Branching crack lines: R = line strength (1 at the core), G = when it appears (0..1). Several start points
        /// spread out one after another, so the damage grows from a few chips into cracks all over.
        /// </summary>
        static Texture2D MakeCracks(int size)
        {
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 255, 0, 255);
            var rnd = new System.Random(11);
            const int seeds = 6;
            for (int s = 0; s < seeds; s++)
            {
                float start = s / (float)seeds * .75f;
                // start points over the middle of the body (sprites are centred, the edges are mostly transparent)
                var p = new Vector2(Mathf.Lerp(.28f, .72f, (float)rnd.NextDouble()), Mathf.Lerp(.25f, .75f, (float)rnd.NextDouble())) * size;
                float a0 = (float)rnd.NextDouble() * 360f;
                int arms = 2 + rnd.Next(2);
                for (int arm = 0; arm < arms; arm++) Walk(px, size, rnd, p, a0 + arm * (360f / arms) + (float)rnd.NextDouble() * 40f, 9, start, .25f / 9, 0);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "boss_cracks" };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static void Walk(Color32[] px, int size, System.Random rnd, Vector2 p, float angle, int steps, float order, float orderStep, int depth)
        {
            for (int i = 0; i < steps; i++)
            {
                angle += ((float)rnd.NextDouble() - .5f) * 50f; // jagged but going somewhere: cracks, not twigs
                float a = angle * Mathf.Deg2Rad;
                Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(12f, 22f, (float)rnd.NextDouble());
                float width = Mathf.Lerp(2.4f, 1.1f, (float)i / steps) * (depth == 0 ? 1f : .7f);
                Stamp(px, size, p, q, width, order);
                p = q; order += orderStep;
                if (depth < 1 && rnd.NextDouble() < .16) Walk(px, size, rnd, p, angle + (rnd.NextDouble() < .5 ? -55f : 55f), steps / 2, order, orderStep, depth + 1);
            }
        }

        static void Stamp(Color32[] px, int size, Vector2 a, Vector2 b, float radius, float order)
        {
            int n = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f) + 1;
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(order * 255f), 0, 254);
            int r = Mathf.CeilToInt(radius);
            for (int k = 0; k <= n; k++)
            {
                Vector2 c = Vector2.Lerp(a, b, (float)k / n);
                int cx = Mathf.RoundToInt(c.x), cy = Mathf.RoundToInt(c.y);
                for (int y = cy - r; y <= cy + r; y++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (x < 0 || y < 0 || x >= size || y >= size) continue;
                        float d = Vector2.Distance(new Vector2(x, y), c) / radius;
                        if (d >= 1f) continue;
                        int i = y * size + x;
                        byte v = (byte)Mathf.RoundToInt((1f - d) * 255f);
                        if (v > px[i].r) px[i].r = v;
                        if (g < px[i].g) px[i].g = g;
                    }
            }
        }

        /// <summary>Screen position (frame fractions) of the boss's chest, for comets and damage numbers.</summary>
        public Vector2 BossCenter()
        {
            if (!bossBody.enabled) return new Vector2(.5f, Perspective.HorizonY);
            return ToFrame(bossBody.transform.localPosition);
        }

        /// <summary>Screen height (frame fraction) the boss currently covers.</summary>
        public float BossScreenHeight()
        {
            if (!bossBody.enabled) return .2f;
            float h = bossBody.transform.localScale.y * bossBody.sprite.rect.height / bossBody.sprite.pixelsPerUnit;
            return h / WorldPerHeight(bossBody.transform.localPosition.z);
        }
    }
}
