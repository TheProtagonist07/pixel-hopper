using UnityEngine;

[DefaultExecutionOrder(100)]
public class Backdrop : MonoBehaviour
{
    const int LayerWidthPx = 512;
    const float Ppu = 32f;

    class Layer
    {
        public SpriteRenderer[] copies;
        public float parallax;
    }

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
    float nextFlash;
    float flashClock = -1f;

    public static Backdrop Create(Transform parent, Camera cam, StageTheme theme, int seed)
    {
        var go = new GameObject("Backdrop");
        go.transform.SetParent(parent);
        var b = go.AddComponent<Backdrop>();
        b.Init(cam, theme, seed);
        return b;
    }

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

    static Sprite ToSprite(Texture2D tex, Vector2 pivot)
    {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, Ppu, 0, SpriteMeshType.FullRect);
    }

    void Init(Camera camera, StageTheme t, int seed)
    {
        cam = camera;
        theme = t;

        sky = NewSprite("Sky", ToSprite(MakeSky(t.SkyTop, t.SkyBottom), new Vector2(0.5f, 0.5f)), -100);

        if (t.Stars)
        {
            var tex = MakeStars(seed);
            var sprite = ToSprite(tex, new Vector2(0.5f, 0.5f));
            starCopies = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++) starCopies[i] = NewSprite("Stars", sprite, -95);
        }

        if (t.Body != Celestial.None)
        {
            var bodySr = NewSprite("Body", ToSprite(MakeBody(t.Body, t.BodyColor), new Vector2(0.5f, 0.5f)), -90);
            body = bodySr.transform;
        }

        bool snow = t.Name == "Frozen Peaks" || t.Name == "Sunny Meadow";
        layers = new[]
        {
            MakeLayerObject("FarLayer", t.FarShape, t.FarColor, seed + 1, 11, 5.6f, 2.4f, snow, 0.16f, -80),
            MakeLayerObject("NearLayer", t.NearShape, t.NearColor, seed + 2, 8, 4.0f, 1.2f, false, 0.36f, -70),
        };

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

        if (t.Lightning)
        {
            flash = NewSprite("Flash", PixelArt.Dot, 80, new Color(1f, 1f, 1f, 0f));
            nextFlash = Time.time + 3f;
        }

        AmbientParticles.Create(transform, cam, t.Ambient, seed);
    }

    Layer MakeLayerObject(string name, LayerShape shape, Color color, int seed, int heightTiles, float baseTiles, float ampTiles, bool snow, float parallax, int order)
    {
        var tex = MakeLayer(shape, color, seed, LayerWidthPx, heightTiles * 32, baseTiles, ampTiles, snow);
        var sprite = ToSprite(tex, new Vector2(0.5f, 0f));
        var layer = new Layer { parallax = parallax, copies = new SpriteRenderer[3] };
        for (int i = 0; i < 3; i++) layer.copies[i] = NewSprite(name, sprite, order);
        return layer;
    }

    static float Snap(float v) => Mathf.Round(v * Ppu) / Ppu;

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 cp = cam.transform.position;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;

        sky.transform.position = new Vector3(cp.x, cp.y, 0f);
        sky.transform.localScale = new Vector3((halfW * 2f + 1f) / (8f / Ppu), (halfH * 2f + 1f) / (128f / Ppu), 1f);

        float layerWidth = LayerWidthPx / Ppu;
        foreach (var layer in layers)
        {
            float rootX = cp.x * (1f - layer.parallax);
            float n = Mathf.Round((cp.x - rootX) / layerWidth);
            for (int k = -1; k <= 1; k++)
                layer.copies[k + 1].transform.position = new Vector3(Snap(rootX + (n + k) * layerWidth), 0f, 0f);
        }

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

        if (body != null)
            body.position = new Vector3(Snap(cp.x + halfW * 0.5f), Snap(cp.y + halfH * 0.5f), 0f);

        float span = halfW * 2f + 8f;
        foreach (var c in clouds)
        {
            float rel = Mathf.Repeat(c.baseX + Time.time * c.drift - cp.x * c.parallax, span) - span * 0.5f;
            var pos = c.t.position;
            c.t.position = new Vector3(Snap(cp.x + rel), pos.y, 0f);
        }

        if (flash != null) UpdateLightning(cp, halfW, halfH);
    }

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
        flash.transform.localScale = new Vector3(halfW * 2f + 1f, halfH * 2f + 1f, 1f) / 0.0625f;
        flash.color = new Color(0.85f, 0.85f, 1f, a);
    }

    // ---------- procedural textures ----------

    static readonly int[,] Bayer =
    {
        { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 }
    };

    public static Texture2D MakeSky(Color top, Color bottom)
    {
        const int w = 8, h = 128, steps = 20;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1);
            float v = Mathf.Pow(t, 1.1f) * steps;
            int lo = Mathf.FloorToInt(v);
            float frac = v - lo;
            for (int x = 0; x < w; x++)
            {
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

    static int Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }

    static float Wrap(float d, float w) => Mathf.Min(Mathf.Abs(d), w - Mathf.Abs(d));

    static float ShapeHeight(LayerShape shape, float x, float W, float s)
    {
        float tau = Mathf.PI * 2f;
        switch (shape)
        {
            case LayerShape.Mountains:
            {
                float a = 1f - Mathf.Abs(Mathf.Sin(tau * 3f * x / W + s));
                float b = 1f - Mathf.Abs(Mathf.Sin(tau * 7f * x / W + s * 2.3f));
                return Mathf.Pow(a, 1.5f) * 1.5f - 0.55f + 0.22f * b * b;
            }
            case LayerShape.Dunes:
                return 0.6f * Mathf.Sin(tau * 2f * x / W + s) + 0.25f * Mathf.Sin(tau * 3f * x / W + s * 1.7f) - 0.1f;
            case LayerShape.Spires:
            {
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
                return 0.55f * Mathf.Sin(tau * 2f * x / W + s) + 0.3f * Mathf.Sin(tau * 5f * x / W + s * 2.1f) + 0.15f * Mathf.Sin(tau * 11f * x / W + s * 3.3f);
        }
    }

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
        float snowLine = maxH * 0.74f;
        for (int x = 0; x < W; x++)
        {
            int hl = heights[(x - 3 + W) % W], hr = heights[(x + 3) % W];
            float slope = (hr - hl) / 6f;
            float lit = 1f + Mathf.Clamp(slope * 0.14f, -0.3f, 0.3f);
            for (int y = 0; y < heights[x]; y++)
            {
                float t = y / (float)heights[x];
                float faceLight = 1f + (lit - 1f) * Mathf.Exp(-(heights[x] - y) / 30f);
                Color c = col * faceLight * Mathf.Lerp(0.74f, 1.02f, t);
                if (y >= heights[x] - 2) c *= 1.22f;
                else if (y >= heights[x] - 5) c *= 1.07f;
                if (Hash(x, y) % 100 < 6) c *= 0.94f;

                if (snow && shape == LayerShape.Mountains && y > snowLine + (Hash(x / 3, 1) % 7))
                    c = Color.Lerp(c, new Color(1f, 1f, 1f), 0.86f) * Mathf.Clamp(lit, 0.82f, 1.1f);

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

    static void Plot(Color32[] px, int w, int h, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        c.a = 1f;
        px[y * w + x] = c;
    }

    public static Texture2D MakeBody(Celestial kind, Color color)
    {
        var p = new PixelPainter(48, 48);
        if (kind == Celestial.Sun)
        {
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
        return p.Bake(false, 0);
    }
}

public class AmbientParticles : MonoBehaviour
{
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

    public static void Create(Transform parent, Camera cam, AmbientKind kind, int seed)
    {
        if (kind == AmbientKind.None) return;
        var go = new GameObject("Ambient " + kind);
        go.transform.SetParent(parent);
        var a = go.AddComponent<AmbientParticles>();
        a.Init(cam, kind, seed);
    }

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
            sr.sortingOrder = k == AmbientKind.Fireflies ? 5 : 40;

            var p = new Particle
            {
                t = go.transform,
                sr = sr,
                pos = new Vector2((float)(rng.NextDouble() * 2 - 1) * (halfW + 1f), (float)(rng.NextDouble() * 2 - 1) * (halfH + 1f)),
                phase = (float)rng.NextDouble() * 6.28f,
            };

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
            float s = p.size / 0.0625f;
            go.transform.localScale = kind == AmbientKind.Rain ? new Vector3(s, s * 9f, 1f) : new Vector3(s, s, 1f);
            if (kind == AmbientKind.Rain) go.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            items[i] = p;
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 cp = cam.transform.position;
        float halfH = cam.orthographicSize + 1f, halfW = cam.orthographicSize * cam.aspect + 1f;
        float dt = Time.deltaTime, time = Time.time;

        foreach (var p in items)
        {
            Vector2 v = p.vel;
            if (kind == AmbientKind.Snow || kind == AmbientKind.Leaves) v.x += Mathf.Sin(time * 1.6f + p.phase) * 0.5f;
            if (kind == AmbientKind.Fireflies) v += new Vector2(Mathf.Sin(time * 0.9f + p.phase), Mathf.Cos(time * 1.3f + p.phase)) * 0.5f;
            p.pos += v * dt;

            if (p.pos.y < -halfH) p.pos.y += halfH * 2f;
            if (p.pos.y > halfH) p.pos.y -= halfH * 2f;
            if (p.pos.x < -halfW) p.pos.x += halfW * 2f;
            if (p.pos.x > halfW) p.pos.x -= halfW * 2f;

            p.t.position = new Vector3(cp.x + p.pos.x, cp.y + p.pos.y, 0f);

            var c = p.sr.color;
            if (kind == AmbientKind.Fireflies) c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(time * 2.4f + p.phase));
            else if (kind == AmbientKind.Embers) c.a = 0.55f + 0.45f * Mathf.Sin(time * 8f + p.phase);
            p.sr.color = c;
        }
    }
}
