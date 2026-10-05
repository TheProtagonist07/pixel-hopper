using UnityEngine;

/// <summary>
/// The animated background of one stage: sky gradient, stars, sun or moon, two parallax scenery layers, drifting
/// clouds, optional lightning flashes and ambient particles (see <see cref="AmbientParticles"/>).
/// Everything is generated from the stage's <see cref="StageTheme"/>; there are no background image files.
///
/// Layers, from back to front (the sorting order is the number at the end of each line):
///   Sky (-100) / Stars (-95) / Sun or moon (-90) / Far scenery (-80) / Near scenery (-70) / Clouds (-60..-54) /
///   Lightning flash (80) / Ambient particles (5 for fireflies, otherwise 40). The game world is drawn above all of this.
///
/// Parallax: a layer with parallax factor p moves at (1 - p) times the camera speed, so a small p means a layer that
/// follows the camera almost completely (it looks far away). Scenery textures are 512 pixels wide and seamless;
/// each layer uses three copies placed side by side, and every frame the copies are re-positioned around the camera,
/// which gives an endless scrolling background from a single small texture.
///
/// Pixel snapping: positions are rounded to whole texture pixels (<see cref="Snap"/>) so the pixel art never shimmers
/// when the camera moves by fractions of a pixel.
///
/// DefaultExecutionOrder(100) makes this script run after the camera-follow script, so it always reads the camera's
/// final position for the frame (otherwise the background would lag one frame behind and jitter).
/// </summary>
[DefaultExecutionOrder(100)]
public class Backdrop : MonoBehaviour
{
    // Width of one scenery texture in pixels, and sprite pixels per world unit (matches the game sprites: 32).
    const int LayerWidthPx = 512;
    const float Ppu = 32f;

    // A parallax scenery layer: three copies of the same texture and how strongly it follows the camera.
    class Layer
    {
        public SpriteRenderer[] copies;
        public float parallax;
    }

    // One cloud: where it started horizontally, how much it follows the camera, and how fast it drifts on its own.
    class CloudInfo
    {
        public Transform t;
        public float baseX, parallax, drift;
    }

    Camera cam;
    StageTheme theme;
    SpriteRenderer sky;
    SpriteRenderer starsRenderer;
    Transform body;
    SpriteRenderer[] starCopies;
    SpriteRenderer flash;
    Layer[] layers;
    CloudInfo[] clouds;
    float nextFlash;      // time of the next lightning strike
    float flashClock = -1f; // seconds since the current flash started; negative while no flash is running

    /// <summary>
    /// Builds the backdrop for a stage and parents it to <paramref name="parent"/> (so it is destroyed with the level).
    /// </summary>
    /// <param name="cam">The game camera that the backdrop follows.</param>
    /// <param name="theme">Colors and scenery style of this stage.</param>
    /// <param name="seed">Level seed; the same seed always produces the same scenery.</param>
    public static Backdrop Create(Transform parent, Camera cam, StageTheme theme, int seed)
    {
        var go = new GameObject("Backdrop");
        go.transform.SetParent(parent);
        var b = go.AddComponent<Backdrop>();
        b.Init(cam, theme, seed);
        return b;
    }

    // Creates a child object with a SpriteRenderer.
    SpriteRenderer NewSprite(string name, Sprite sprite, int order, Color? color = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.color = color ?? Color.white;
        return sr;
    }

    // Wraps a texture in a sprite. FullRect avoids Unity's tight-mesh generation, which is slow for big textures.
    static Sprite ToSprite(Texture2D tex, Vector2 pivot)
    {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, Ppu, 0, SpriteMeshType.FullRect);
    }

    // Creates every element of the backdrop according to the theme.
    void Init(Camera camera, StageTheme t, int seed)
    {
        cam = camera;
        theme = t;

        // Sky: a tiny 8 x 128 gradient texture that LateUpdate stretches to cover the whole screen.
        sky = NewSprite("Sky", ToSprite(MakeSky(t.SkyTop, t.SkyBottom), new Vector2(0.5f, 0.5f)), -100);

        // Stars (night themes): three copies of one star texture so they can tile horizontally.
        if (t.Stars)
        {
            var tex = MakeStars(seed);
            var sprite = ToSprite(tex, new Vector2(0.5f, 0.5f));
            starCopies = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++) starCopies[i] = NewSprite("Stars", sprite, -95);
        }

        // Sun or moon, kept in the upper-right quarter of the screen.
        if (t.Body != Celestial.None)
        {
            var bodySr = NewSprite("Body", ToSprite(MakeBody(t.Body, t.BodyColor), new Vector2(0.5f, 0.5f)), -90);
            body = bodySr.transform;
        }

        // Two scenery layers. Far = bigger, slower, fainter; near = smaller and moves faster.
        // Arguments: name, shape, color, seed, texture height in tiles, base height in tiles, wave amplitude in tiles,
        // snow caps, parallax factor, sorting order. Snow caps only appear on the mountains of two themes.
        bool snow = t.Name == "Frozen Peaks" || t.Name == "Sunny Meadow";
        layers = new[]
        {
            MakeLayerObject("FarLayer", t.FarShape, t.FarColor, seed + 1, 11, 5.6f, 2.4f, snow, 0.16f, -80),
            MakeLayerObject("NearLayer", t.NearShape, t.NearColor, seed + 2, 8, 4.0f, 1.2f, false, 0.36f, -70),
        };

        // Seven clouds with random size, height and drift speed (all derived from the seed so a level always looks the
        // same). Bigger clouds follow the camera more (parallax 0.3 to 0.55), which makes them feel closer.
        var cloudSprite = PixelArt.Cloud;
        var rng = new System.Random(seed * 31 + 7);
        clouds = new CloudInfo[7];
        for (int i = 0; i < clouds.Length; i++)
        {
            float scale = 0.6f + (float)rng.NextDouble() * 0.7f;
            var sr = NewSprite("Cloud", cloudSprite, -60 + i, t.CloudTint);
            sr.transform.localScale = Vector3.one * scale;
            clouds[i] = new CloudInfo
            {
                t = sr.transform,
                baseX = (float)rng.NextDouble() * 40f,
                parallax = 0.3f + scale * 0.25f,
                drift = 0.15f + (float)rng.NextDouble() * 0.2f,
            };
            sr.transform.position = new Vector3(0f, 8.5f + (float)rng.NextDouble() * 5.5f, 0f);
        }

        // Storm themes get a full-screen white quad that flashes (starting 3 seconds in, then every 4 to 9 seconds).
        if (t.Lightning)
        {
            flash = NewSprite("Flash", PixelArt.Dot, 80, new Color(1f, 1f, 1f, 0f));
            nextFlash = Time.time + 3f;
        }

        AmbientParticles.Create(transform, cam, t.Ambient, seed);
    }

    // Generates one scenery texture and creates its three side-by-side copies.
    // The pivot is bottom-centre, so the scenery stands on the world's y = 0 line; the texture's height in pixels
    // is heightTiles * 32 (32 pixels = one world tile).
    Layer MakeLayerObject(string name, LayerShape shape, Color color, int seed, int heightTiles, float baseTiles, float ampTiles, bool snow, float parallax, int order)
    {
        var tex = MakeLayer(shape, color, seed, LayerWidthPx, heightTiles * 32, baseTiles, ampTiles, snow);
        var sprite = ToSprite(tex, new Vector2(0.5f, 0f));
        var layer = new Layer { parallax = parallax, copies = new SpriteRenderer[3] };
        for (int i = 0; i < 3; i++) layer.copies[i] = NewSprite(name, sprite, order);
        return layer;
    }

    // Rounds a world coordinate to the nearest whole sprite pixel.
    static float Snap(float v) => Mathf.Round(v * Ppu) / Ppu;

    // Runs after the camera has moved. Re-positions everything so it fills the view.
    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 cp = cam.transform.position;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;

        // Sky: centre on the camera and stretch the 8 x 128 pixel texture to the view size plus a 1 unit margin.
        sky.transform.position = new Vector3(cp.x, cp.y, 0f);
        sky.transform.localScale = new Vector3((halfW * 2f + 1f) / (8f / Ppu), (halfH * 2f + 1f) / (128f / Ppu), 1f);

        // Scenery layers. rootX is where the layer's origin would be if it moved by (1 - parallax) of the camera
        // motion. n is how many whole layer-widths lie between that origin and the camera, so the middle copy is
        // always the one nearest the camera, with one copy on each side to fill the screen edges.
        float layerWidth = LayerWidthPx / Ppu;
        foreach (var layer in layers)
        {
            float rootX = cp.x * (1f - layer.parallax);
            float n = Mathf.Round((cp.x - rootX) / layerWidth);
            for (int k = -1; k <= 1; k++)
                layer.copies[k + 1].transform.position = new Vector3(Snap(rootX + (n + k) * layerWidth), 0f, 0f);
        }

        // Stars: nearly fixed to the camera (parallax 0.04) and gently twinkling by pulsing their opacity.
        if (starCopies != null)
        {
            float w = 512f / Ppu;
            float rootX = cp.x * 0.96f;
            float n = Mathf.Round((cp.x - rootX) / w);
            float alpha = 0.78f + 0.22f * Mathf.Sin(Time.time * 2.2f);
            for (int k = -1; k <= 1; k++)
            {
                starCopies[k + 1].transform.position = new Vector3(Snap(rootX + (n + k) * w), cp.y + 0.2f, 0f);
                starCopies[k + 1].color = new Color(1f, 1f, 1f, alpha);
            }
        }

        // Sun or moon: pinned to the upper-right of the view, so it never scrolls away.
        if (body != null)
            body.position = new Vector3(Snap(cp.x + halfW * 0.5f), Snap(cp.y + halfH * 0.5f), 0f);

        // Clouds: each drifts on its own and lags behind the camera by its parallax factor. Mathf.Repeat wraps the
        // position within one screen width plus 8 units, so a cloud leaving one side re-enters at the other.
        float span = halfW * 2f + 8f;
        foreach (var c in clouds)
        {
            float rel = Mathf.Repeat(c.baseX + Time.time * c.drift - cp.x * c.parallax, span) - span * 0.5f;
            var pos = c.t.position;
            c.t.position = new Vector3(Snap(cp.x + rel), pos.y, 0f);
        }

        if (flash != null) UpdateLightning(cp, halfW, halfH);
    }

    // Lightning: when it is time, run a short flash sequence. In the first 0.07 s the screen is lit strongly, then dark
    // until 0.16 s, then a weaker second flash until 0.3 s (the classic double flicker), and the sequence is over at
    // 0.45 s. The flash is a white quad covering the whole view; its alpha is the brightness.
    void UpdateLightning(Vector3 cp, float halfW, float halfH)
    {
        if (flashClock < 0f && Time.time > nextFlash)
        {
            flashClock = 0f;
            nextFlash = Time.time + Random.Range(4f, 9f);
        }
        float a = 0f;
        if (flashClock >= 0f)
        {
            flashClock += Time.deltaTime;
            float t = flashClock;
            a = (t < 0.07f || (t > 0.16f && t < 0.3f)) ? 0.55f * (t < 0.07f ? 1f : 0.7f) : 0f;
            if (t > 0.45f) flashClock = -1f;
        }
        flash.transform.position = new Vector3(cp.x, cp.y, 0f);
        // The "Dot" sprite is 2 x 2 pixels = 0.0625 world units wide, so divide by that to scale it to the view size.
        flash.transform.localScale = new Vector3(halfW * 2f + 1f, halfH * 2f + 1f, 1f) / 0.0625f;
        flash.color = new Color(0.85f, 0.85f, 1f, a);
    }

    // ---------- procedural textures ----------

    // 4 x 4 Bayer matrix for ordered dithering. Comparing a fractional color level with these thresholds gives a
    // fixed, regular checker-like pattern of the two nearest colors, which looks like classic retro-hardware gradients.
    static readonly int[,] Bayer =
    {
        { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 }
    };

    /// <summary>
    /// Creates the sky gradient: an 8 x 128 texture that fades from <paramref name="top"/> to <paramref name="bottom"/>
    /// in 20 discrete color bands, with ordered (Bayer) dithering between neighbouring bands instead of smooth blending.
    /// It is stretched over the screen in LateUpdate, which makes each texture pixel a large dithered block.
    /// </summary>
    public static Texture2D MakeSky(Color top, Color bottom)
    {
        const int w = 8, h = 128, steps = 20;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1);
            // Gamma 1.1 gives the upper sky slightly longer bands than the horizon.
            float v = Mathf.Pow(t, 1.1f) * steps;
            int lo = Mathf.FloorToInt(v);
            float frac = v - lo;
            for (int x = 0; x < w; x++)
            {
                // Pixels whose Bayer threshold is below the fractional part go up to the next band.
                float threshold = (Bayer[y % 4, x % 4] + 0.5f) / 16f;
                int band = lo + (frac > threshold ? 1 : 0);
                float bt = Mathf.Clamp01(Mathf.Pow(band / (float)steps, 1f / 1.1f));
                Color c = Color.Lerp(top, bottom, bt);
                c.a = 1f;
                px[(h - 1 - y) * w + x] = c;
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Deterministic integer hash of two coordinates (a few multiply/xor-shift rounds). Gives repeatable "random"
    // speckle without using a random-number generator, so a texture is identical every time it is made.
    static int Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }

    // Distance between two positions on a circle of circumference w (the shortest way round).
    // Used so that spires and volcanoes near the texture's edge wrap smoothly to the other side.
    static float Wrap(float d, float w) => Mathf.Min(Mathf.Abs(d), w - Mathf.Abs(d));

    // The height profile of the scenery at column x of a texture W pixels wide, roughly in the range -0.5 to 1.
    // 's' shifts the shape per seed. Every shape repeats exactly over W (sine frequencies are whole numbers per W,
    // and Wrap handles peaks near the edge), so the texture tiles with no visible seam.
    static float ShapeHeight(LayerShape shape, float x, float W, float s)
    {
        float tau = Mathf.PI * 2f;
        switch (shape)
        {
            case LayerShape.Mountains:
            {
                // 1 - |sin| turns smooth waves into sharp peaks; two frequencies (3 and 7 per width) give big peaks
                // plus smaller ridges. Raising to 1.5 makes the valleys wider and the summits pointier.
                float a = 1f - Mathf.Abs(Mathf.Sin(tau * 3f * x / W + s));
                float b = 1f - Mathf.Abs(Mathf.Sin(tau * 7f * x / W + s * 2.3f));
                return Mathf.Pow(a, 1.5f) * 1.5f - 0.55f + 0.22f * b * b;
            }
            case LayerShape.Dunes:
                // Two smooth sine waves: rolling sand dunes.
                return 0.6f * Mathf.Sin(tau * 2f * x / W + s) + 0.25f * Mathf.Sin(tau * 3f * x / W + s * 1.7f) - 0.1f;
            case LayerShape.Spires:
            {
                // Eight tall triangular towers at jittered positions with varying height and width;
                // where towers overlap, the taller one wins (Max).
                float best = -0.5f;
                for (int i = 0; i < 8; i++)
                {
                    float pos = (i + 0.5f + 0.35f * Mathf.Sin(s * (i + 1.3f))) * W / 8f;
                    float height = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(s * 3.1f + i * 2.17f));
                    float halfWidth = 18f + 22f * Mathf.Abs(Mathf.Cos(s * 1.9f + i * 1.3f));
                    float v = height * (1f - Wrap(x - pos, W) / halfWidth) - 0.45f;
                    best = Mathf.Max(best, v);
                }
                return best;
            }
            case LayerShape.Volcano:
            {
                // Two flat-topped cones at fixed places (22% and 70% across the texture). The cone height is capped
                // so the summit is a flat crater rim where MakeLayer paints the lava glow.
                float best = -0.45f;
                float[] centers = { 0.22f, 0.7f };
                float[] widths = { 150f, 120f };
                for (int i = 0; i < 2; i++)
                {
                    float cone = 1.45f * (1f - Wrap(x - centers[i] * W, W) / widths[i]);
                    best = Mathf.Max(best, Mathf.Min(cone, 1.0f) - 0.4f);
                }
                return best;
            }
            default:
                // Rolling hills: three sine waves of different frequency added together (2, 5 and 11 per width).
                return 0.55f * Mathf.Sin(tau * 2f * x / W + s) + 0.3f * Mathf.Sin(tau * 5f * x / W + s * 2.1f) + 0.15f * Mathf.Sin(tau * 11f * x / W + s * 3.3f);
        }
    }

    /// <summary>
    /// Creates one seamless scenery texture (a silhouette of hills, mountains, dunes, spires or volcanoes with a
    /// bottom-left origin, transparent above the skyline).
    ///
    /// For each column the skyline height comes from <see cref="ShapeHeight"/>; the column below it is then filled with
    /// the theme color and lit as if from the left: slopes facing up-left are lighter, slopes facing down-right darker
    /// (compared across the neighbouring columns 3 pixels either side). The colour also darkens toward the bottom, the
    /// top 2 pixels get a bright rim and the next 3 a softer one, and about 6% of pixels are dimmed with the hash noise for texture.
    /// </summary>
    /// <param name="W">Texture width (must stay 512; the shapes are built to tile exactly).</param>
    /// <param name="H">Texture height in pixels (the skyline is clamped below this).</param>
    /// <param name="baseTiles">Average skyline height in world tiles (1 tile = 32 pixels).</param>
    /// <param name="ampTiles">How far the skyline varies around the average, in tiles.</param>
    /// <param name="snow">Paint white snow caps on mountain tops (mountains only).</param>
    public static Texture2D MakeLayer(LayerShape shape, Color col, int seed, int W, int H, float baseTiles, float ampTiles, bool snow)
    {
        float s = seed * 1.37f;
        var heights = new int[W];
        int maxH = 0;
        for (int x = 0; x < W; x++)
        {
            float f = ShapeHeight(shape, x, W, s);
            heights[x] = Mathf.Clamp(Mathf.RoundToInt((baseTiles + ampTiles * f) * 32f), 4, H - 1);
            maxH = Mathf.Max(maxH, heights[x]);
        }

        var px = new Color32[W * H];
        float snowLine = maxH * 0.74f; // snow starts at 74% of the tallest peak
        for (int x = 0; x < W; x++)
        {
            // Slope of the skyline here (pixels of rise per pixel across, measured over 6 columns, wrapping at the edges).
            // A rising skyline to the right means this face points up-left, toward the light.
            int hl = heights[(x - 3 + W) % W], hr = heights[(x + 3) % W];
            float slope = (hr - hl) / 6f;
            float lit = 1f + Mathf.Clamp(slope * 0.14f, -0.3f, 0.3f);
            for (int y = 0; y < heights[x]; y++)
            {
                float t = y / (float)heights[x]; // 0 at the bottom of the column, 1 at the skyline
                // Directional lighting fades out with depth below the surface (30 pixels), so only the outer
                // skin of the shape gets the lit or shaded look.
                float faceLight = 1f + (lit - 1f) * Mathf.Exp(-(heights[x] - y) / 30f);
                Color c = col * faceLight * Mathf.Lerp(0.74f, 1.02f, t);
                if (y >= heights[x] - 2) c *= 1.22f;
                else if (y >= heights[x] - 5) c *= 1.07f;
                if (Hash(x, y) % 100 < 6) c *= 0.94f;

                // Snow cap: above the snow line (with a jagged edge from the hash) blend 86% toward white.
                if (snow && shape == LayerShape.Mountains && y > snowLine + (Hash(x / 3, 1) % 7))
                    c = Color.Lerp(c, new Color(1f, 1f, 1f), 0.86f) * Mathf.Clamp(lit, 0.82f, 1.1f);

                // Volcano crater glow: the top 8 pixels of the tallest parts are painted orange like lava.
                if (shape == LayerShape.Volcano && heights[x] > maxH * 0.9f && y > heights[x] - 8)
                    c = new Color(1f, 0.55f + 0.2f * (Hash(x, 5) % 3) / 3f, 0.16f) * (y > heights[x] - 3 ? 1.1f : 0.95f);

                c.a = 1f;
                px[y * W + x] = c;
            }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Creates the star-field texture (512 x 480): 90 random stars of white, warm or cool tint, every seventh one
    /// larger (a plus-shaped cross). The seed makes the sky identical every time the same stage is built.
    /// </summary>
    public static Texture2D MakeStars(int seed)
    {
        const int w = 512, h = 480;
        var px = new Color32[w * h];
        var rng = new System.Random(seed * 17 + 3);
        for (int i = 0; i < 90; i++)
        {
            int x = rng.Next(4, w - 4), y = rng.Next(4, h - 4);
            bool big = i % 7 == 0;
            Color c = i % 3 == 0 ? new Color(1f, 0.95f, 0.7f) : i % 3 == 1 ? new Color(0.8f, 0.9f, 1f) : Color.white;
            Plot(px, w, h, x, y, c);
            if (big)
            {
                Plot(px, w, h, x - 1, y, c * 0.8f); Plot(px, w, h, x + 1, y, c * 0.8f);
                Plot(px, w, h, x, y - 1, c * 0.8f); Plot(px, w, h, x, y + 1, c * 0.8f);
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Writes one opaque pixel into a pixel array, ignoring coordinates outside the texture.
    static void Plot(Color32[] px, int w, int h, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        c.a = 1f;
        px[y * w + x] = c;
    }

    /// <summary>
    /// Creates the sun or moon (48 x 48 pixels) with the shared <see cref="PixelPainter"/>: a bright disc surrounded
    /// by two translucent glow rings. The moon is shinier and has three small craters.
    /// Also used by the branding tool (CoverArt) for the promotional pictures.
    /// </summary>
    public static Texture2D MakeBody(Celestial kind, Color color)
    {
        var p = new PixelPainter(48, 48);
        if (kind == Celestial.Sun)
        {
            // Outer halo, inner halo, then the disc itself (bulge 0.4 = fairly flat, small specular shine).
            p.Ellipse(24, 24, 23f, 23f, new Color(color.r, color.g, color.b, 0.16f), 0f);
            p.Ellipse(24, 24, 18f, 18f, new Color(color.r, color.g, color.b, 0.26f), 0f);
            p.Ellipse(24, 24, 12.5f, 12.5f, color, 0.4f, 0.35f);
        }
        else
        {
            p.Ellipse(24, 24, 22f, 22f, new Color(color.r, color.g, color.b, 0.12f), 0f);
            p.Ellipse(24, 24, 17f, 17f, new Color(color.r, color.g, color.b, 0.2f), 0f);
            p.Ellipse(24, 24, 12.5f, 12.5f, color, 0.8f, 0.3f);
            Color crater = color * 0.82f;
            crater.a = 1f;
            p.Ellipse(20, 20, 2.8f, 2.4f, crater, 0.6f);
            p.Ellipse(28, 27, 3.4f, 3f, crater, 0.6f);
            p.Ellipse(21, 29, 1.8f, 1.6f, crater, 0.6f);
        }
        return p.Bake(false, 0); // no outline, no padding: the soft glow must fade out, not be framed
    }
}

/// <summary>
/// Small drifting particles that give each stage some atmosphere: falling snow, tumbling autumn leaves, rising embers,
/// glowing fireflies or rain (the kind is chosen by the stage theme, see <see cref="AmbientKind"/>).
///
/// The particles are not world objects. They live in a box around the camera (the view plus a 1 unit margin) and wrap
/// around from one side to the opposite side when they leave it, so a fixed handful of 26 to 46 sprites is enough to
/// fill the screen forever, no matter how far the player travels.
/// </summary>
public class AmbientParticles : MonoBehaviour
{
    // One particle: its transform and renderer, position relative to the camera, velocity, a random phase (so the
    // wobble of different particles is out of step), and its size in world units.
    class Particle
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 pos;
        public Vector2 vel;
        public float phase, size;
    }

    AmbientKind kind;
    Camera cam;
    Particle[] items;

    /// <summary>Creates the particle system for a stage; does nothing for <see cref="AmbientKind.None"/>.</summary>
    public static void Create(Transform parent, Camera cam, AmbientKind kind, int seed)
    {
        if (kind == AmbientKind.None) return;
        var go = new GameObject("Ambient " + kind);
        go.transform.SetParent(parent);
        var a = go.AddComponent<AmbientParticles>();
        a.Init(cam, kind, seed);
    }

    // Creates the particles with random starting positions and the look and motion of the chosen kind.
    void Init(Camera camera, AmbientKind k, int seed)
    {
        cam = camera;
        kind = k;
        var rng = new System.Random(seed + 99);
        int count = k == AmbientKind.Rain ? 46 : k == AmbientKind.Snow ? 42 : 26;
        items = new Particle[count];
        float halfH = cam.orthographicSize, halfW = halfH * Mathf.Max(0.5f, cam.aspect);

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("p");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Dot;
            // Fireflies use a low sorting order (5) so they glow close to the ground behind the foreground;
            // every other kind uses 40 so it drifts in front of the world.
            sr.sortingOrder = k == AmbientKind.Fireflies ? 5 : 40;

            var p = new Particle
            {
                t = go.transform,
                sr = sr,
                pos = new Vector2((float)(rng.NextDouble() * 2 - 1) * (halfW + 1f), (float)(rng.NextDouble() * 2 - 1) * (halfH + 1f)),
                phase = (float)rng.NextDouble() * 6.28f,
            };

            // r is a per-particle random number from 0 to 1, used to vary size, speed and color within one kind.
            // Velocities are in world units per second (negative y = falling).
            float r = (float)rng.NextDouble();
            switch (k)
            {
                case AmbientKind.Snow:
                    p.size = 0.1f + r * 0.1f;
                    p.vel = new Vector2(0.4f, -(1.1f + r * 1.2f));
                    sr.color = new Color(1f, 1f, 1f, 0.9f);
                    break;
                case AmbientKind.Leaves:
                    p.size = 0.15f + r * 0.1f;
                    p.vel = new Vector2(-0.8f - r * 0.6f, -(0.9f + r));
                    sr.color = r < 0.33f ? new Color(1f, 0.55f, 0.15f) : r < 0.66f ? new Color(0.9f, 0.25f, 0.12f) : new Color(1f, 0.82f, 0.25f);
                    break;
                case AmbientKind.Embers:
                    p.size = 0.07f + r * 0.08f;
                    p.vel = new Vector2((r - 0.5f) * 0.6f, 0.9f + r * 1.6f);
                    sr.color = new Color(1f, 0.55f + r * 0.3f, 0.15f, 0.95f);
                    break;
                case AmbientKind.Fireflies:
                    p.size = 0.1f + r * 0.06f;
                    p.vel = new Vector2((r - 0.5f) * 0.8f, ((float)rng.NextDouble() - 0.5f) * 0.6f);
                    sr.color = r < 0.5f ? new Color(0.85f, 1f, 0.4f) : new Color(0.5f, 1f, 0.95f);
                    break;
                case AmbientKind.Rain:
                    p.size = 0.05f;
                    p.vel = new Vector2(-3.2f, -15f - r * 4f);
                    sr.color = new Color(0.78f, 0.86f, 1f, 0.55f);
                    break;
            }
            // The Dot sprite is 0.0625 world units wide, so divide to scale to the wanted size.
            // Rain is stretched 9 times taller and tilted 12 degrees to look like streaks blown by wind.
            float s = p.size / 0.0625f;
            go.transform.localScale = kind == AmbientKind.Rain ? new Vector3(s, s * 9f, 1f) : new Vector3(s, s, 1f);
            if (kind == AmbientKind.Rain) go.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            items[i] = p;
        }
    }

    // Moves every particle, wraps it around the box that surrounds the camera, and animates fades.
    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 cp = cam.transform.position;
        float halfH = cam.orthographicSize + 1f, halfW = cam.orthographicSize * cam.aspect + 1f;
        float dt = Time.deltaTime, time = Time.time;

        foreach (var p in items)
        {
            Vector2 v = p.vel;
            // Snow and leaves sway sideways; fireflies wander in small loops.
            if (kind == AmbientKind.Snow || kind == AmbientKind.Leaves) v.x += Mathf.Sin(time * 1.6f + p.phase) * 0.5f;
            if (kind == AmbientKind.Fireflies) v += new Vector2(Mathf.Sin(time * 0.9f + p.phase), Mathf.Cos(time * 1.3f + p.phase)) * 0.5f;
            p.pos += v * dt;

            // Leave on one side, re-enter on the opposite side.
            if (p.pos.y < -halfH) p.pos.y += halfH * 2f;
            if (p.pos.y > halfH) p.pos.y -= halfH * 2f;
            if (p.pos.x < -halfW) p.pos.x += halfW * 2f;
            if (p.pos.x > halfW) p.pos.x -= halfW * 2f;

            // Positions are relative to the camera, so the particles move with the view.
            p.t.position = new Vector3(cp.x + p.pos.x, cp.y + p.pos.y, 0f);

            // Fireflies pulse slowly; embers flicker quickly.
            var c = p.sr.color;
            if (kind == AmbientKind.Fireflies) c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(time * 2.4f + p.phase));
            else if (kind == AmbientKind.Embers) c.a = 0.55f + 0.45f * Mathf.Sin(time * 8f + p.phase);
            p.sr.color = c;
        }
    }
}
