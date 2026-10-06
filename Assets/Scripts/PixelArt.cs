using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's sprite library. Every sprite is DRAWN IN CODE at startup (no image files in the project):
/// characters, coins, props and the heart icon use <see cref="PixelPainter"/> for shaded shapes; ground, brick,
/// spikes, clouds and the shadow are computed pixel by pixel.
///
/// All sprites are packed into ONE texture atlas. Sprites that share a texture can be drawn together in few draw calls,
/// which matters for WebGL and phones (a stage has hundreds of tile sprites). The atlas is built lazily the first time
/// any sprite is requested, so the first access costs a few milliseconds.
///
/// Scale: 32 pixels = 1 world unit = 1 tile (see <see cref="PPU"/>). The hero is about 26 x 36 pixels (0.8 x 1.1 tiles).
///
/// To add a new sprite:
///  1. Write a function that returns a Texture2D (paint it with PixelPainter or fill pixels in a loop).
///  2. Add it to the entry list in <see cref="Build"/> with a unique key and a pivot (use Feet for things that stand on
///     the ground, Mid for centred things).
///  3. Expose it with a property that calls <see cref="Get"/> (like HeroIdle below).
/// </summary>
public static class PixelArt
{
    /// <summary>Pixels per world unit. 32 pixels make one tile, so one art pixel is 1/32 of a tile.</summary>
    public const float PPU = 32f;

    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    static Texture2D atlas;

    // A sprite waiting to be packed: its key, its own small texture, and where its pivot is (in pixels).
    class Entry
    {
        public string key;
        public Texture2D tex;
        public Vector2 pivotPx;
    }

    // Keys of the four coin frames, in animation order.
    static readonly string[] CoinKeys = { "coin_0", "coin_1", "coin_2", "coin_3" };
    static Sprite[] coinFrames;

    // ---- Sprite accessors (each looks the sprite up by key, building the atlas on first use) ----

    /// <summary>Hero standing still.</summary>
    public static Sprite HeroIdle => Get("hero_idle");
    /// <summary>Hero walk frame A (alternates with B while running).</summary>
    public static Sprite HeroWalkA => Get("hero_walk_a");
    /// <summary>Hero walk frame B.</summary>
    public static Sprite HeroWalkB => Get("hero_walk_b");
    /// <summary>Hero in the air (legs tucked, arm up).</summary>
    public static Sprite HeroJump => Get("hero_jump");
    /// <summary>Enemy walk frame A (alternates with B).</summary>
    public static Sprite EnemyA => Get("enemy_a");
    /// <summary>Enemy walk frame B.</summary>
    public static Sprite EnemyB => Get("enemy_b");
    /// <summary>Spiky enemy walk frame A (a red shell with white spikes; cannot be stomped).</summary>
    public static Sprite SpikyA => Get("spiky_a");
    /// <summary>Spiky enemy walk frame B.</summary>
    public static Sprite SpikyB => Get("spiky_b");
    /// <summary>Flying enemy wings-up frame.</summary>
    public static Sprite FlyerA => Get("flyer_a");
    /// <summary>Flying enemy wings-down frame.</summary>
    public static Sprite FlyerB => Get("flyer_b");
    /// <summary>The blaster in the hero's hand and in the HUD (facing right).</summary>
    public static Sprite Gun => Get("gun");
    /// <summary>The blaster pickup: the gun inside a glowing aura.</summary>
    public static Sprite GunPickup => Get("gun_pickup");
    /// <summary>A blaster shot (a small glowing orb, facing right).</summary>
    public static Sprite Bullet => Get("bullet");
    /// <summary>Plain dirt tile (32 x 32).</summary>
    public static Sprite Dirt => Get("dirt");
    /// <summary>Dirt tile with a grass top.</summary>
    public static Sprite Grass => Get("grass");
    /// <summary>Brick block tile.</summary>
    public static Sprite Brick => Get("brick");
    /// <summary>Two metal spikes on a base plate (one tile).</summary>
    public static Sprite Spikes => Get("spikes");
    /// <summary>Goal flag: a pole with a red pennant, 1 tile wide and 3 tiles tall.</summary>
    public static Sprite Flag => Get("flag");
    /// <summary>A fluffy cloud (3 tiles wide).</summary>
    public static Sprite Cloud => Get("cloud");
    /// <summary>Soft dark ellipse used as a ground shadow under characters.</summary>
    public static Sprite Shadow => Get("shadow");
    /// <summary>Heart icon for the lives counter in the HUD.</summary>
    public static Sprite Heart => Get("heart");
    /// <summary>A 2 x 2 white square, scaled and tinted for particles and screen flashes.</summary>
    public static Sprite Dot => Get("dot");

    /// <summary>
    /// A scenery prop. Kinds: bush, flowers, pine, cactus, crystal, rock, lavarock, mushroom.
    /// An unknown kind falls back to "rock" inside <see cref="DecorTexture"/>.
    /// </summary>
    public static Sprite Decor(string kind) => Get("decor_" + kind);

    /// <summary>
    /// One frame of the spinning coin. Any integer works (it wraps), so callers can pass a rising time value.
    /// The four frames are the coin seen at 100%, 72%, 20% and 72% of its width, which fakes a rotation around
    /// the vertical axis.
    /// </summary>
    public static Sprite CoinFrame(int index)
    {
        if (coinFrames == null || coinFrames[0] == null)
        {
            coinFrames = new Sprite[CoinKeys.Length];
            for (int i = 0; i < CoinKeys.Length; i++) coinFrames[i] = Get(CoinKeys[i]);
        }
        return coinFrames[((index % CoinKeys.Length) + CoinKeys.Length) % CoinKeys.Length];
    }

    /// <summary>Number of coin animation frames (4).</summary>
    public static int CoinFrameCount => CoinKeys.Length;

    /// <summary>
    /// Returns the sprite with the given key, building (or rebuilding) the whole atlas if needed.
    /// The rebuild path also covers the case where Unity destroyed the atlas (for example after leaving Play mode).
    /// </summary>
    public static Sprite Get(string key)
    {
        if (atlas == null || !sprites.ContainsKey(key) || sprites[key] == null) Build();
        return sprites[key];
    }

    // Creates every sprite texture, packs them into one atlas texture, and creates a Sprite for each.
    static void Build()
    {
        sprites.Clear();
        coinFrames = null;

        var entries = new List<Entry>();
        void Add(string key, Texture2D tex, Vector2 pivotPx) => entries.Add(new Entry { key = key, tex = tex, pivotPx = pivotPx });

        // Pivots, in pixels from the bottom-left of the texture:
        //  Feet = bottom centre, 1 px up. The extra pixel keeps the outline row below the pivot, so the visible feet
        //         line up with the ground.
        //  Mid  = centre of the sprite.
        Vector2 Feet(Texture2D t) => new Vector2(t.width * 0.5f, 1f);
        Vector2 Mid(Texture2D t) => new Vector2(t.width * 0.5f, t.height * 0.5f);

        // Hero: 4 poses (idle, walk A, walk B, jump).
        for (int pose = 0; pose < 4; pose++)
        {
            var t = Hero(pose);
            Add(new[] { "hero_idle", "hero_walk_a", "hero_walk_b", "hero_jump" }[pose], t, Feet(t));
        }
        // Enemy: 2 walk frames.
        for (int f = 0; f < 2; f++)
        {
            var t = Enemy(f);
            Add(f == 0 ? "enemy_a" : "enemy_b", t, Feet(t));
        }
        // Spiky and flying enemies, 2 frames each (feet pivot like the walker, so Game places all enemies the same way).
        for (int f = 0; f < 2; f++)
        {
            var t = Spiky(f);
            Add(f == 0 ? "spiky_a" : "spiky_b", t, Feet(t));
            var fl = Flyer(f);
            Add(f == 0 ? "flyer_a" : "flyer_b", fl, Feet(fl));
        }
        // Blaster weapon sprites.
        var gunTex = GunTexture(false);
        Add("gun", gunTex, Mid(gunTex));
        var gunPickupTex = GunTexture(true);
        Add("gun_pickup", gunPickupTex, Mid(gunPickupTex));
        var bulletTex = BulletTexture();
        Add("bullet", bulletTex, Mid(bulletTex));
        // Coin: 4 frames; the number is the fraction of full width shown.
        float[] widths = { 1f, 0.72f, 0.2f, 0.72f };
        for (int i = 0; i < CoinKeys.Length; i++)
        {
            var t = Coin(widths[i]);
            Add(CoinKeys[i], t, Mid(t));
        }

        // Tiles and other single sprites.
        var dirt = TopDown(32, 32, (x, y) => DirtColor(x, y));
        Add("dirt", dirt, Mid(dirt));
        var grass = TopDown(32, 32, (x, y) => GrassColor(x, y));
        Add("grass", grass, Mid(grass));
        var brick = TopDown(32, 32, (x, y) => BrickColor(x, y));
        Add("brick", brick, Mid(brick));
        var spikes = Spikes32();
        Add("spikes", spikes, Mid(spikes));
        var flag = Flag96();
        Add("flag", flag, Feet(flag));
        var cloud = Cloud96();
        Add("cloud", cloud, Mid(cloud));
        var shadow = Shadow28();
        Add("shadow", shadow, Mid(shadow));
        var heart = Heart15();
        Add("heart", heart, Mid(heart));
        var dot = TopDown(2, 2, (x, y) => Color.white);
        Add("dot", dot, Mid(dot));

        // Scenery props.
        foreach (var kind in new[] { "bush", "flowers", "pine", "cactus", "crystal", "rock", "lavarock", "mushroom" })
        {
            var t = DecorTexture(kind);
            Add("decor_" + kind, t, Feet(t));
        }

        // Pack all the small textures into a single atlas (2 px padding between them, at most 2048 x 2048).
        var sources = new Texture2D[entries.Count];
        for (int i = 0; i < sources.Length; i++) sources[i] = entries[i].tex;

        atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelAtlas" };
        Rect[] uv = atlas.PackTextures(sources, 2, 2048, false);
        atlas.filterMode = FilterMode.Point; // PackTextures resets the filter, so set it again for crisp pixels

        // Create a Sprite for each entry from its rectangle in the atlas, then free the individual textures.
        for (int i = 0; i < entries.Count; i++)
        {
            Rect r = uv[i];
            var px = new Rect(Mathf.Round(r.x * atlas.width), Mathf.Round(r.y * atlas.height), entries[i].tex.width, entries[i].tex.height);
            var pivot = new Vector2(entries[i].pivotPx.x / px.width, entries[i].pivotPx.y / px.height);
            var s = Sprite.Create(atlas, px, pivot, PPU, 0, SpriteMeshType.FullRect);
            s.name = entries[i].key;
            sprites[entries[i].key] = s;
            // Destroy cannot be used in edit mode (the branding tools build sprites outside Play mode).
            if (Application.isPlaying) UnityEngine.Object.Destroy(entries[i].tex);
            else UnityEngine.Object.DestroyImmediate(entries[i].tex);
        }
    }

    // ---------- helpers ----------

    // Builds a texture by evaluating f(x, y) for every pixel. y = 0 is the TOP row (easier to think about);
    // the rows are flipped when stored because Unity textures start at the bottom.
    static Texture2D TopDown(int w, int h, Func<int, int, Color> f)
    {
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = f(x, y);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // A fast deterministic pseudo-random number from two integers (a 2D hash). Used for texture speckle so the
    // result is identical on every run and every device, with no random state to manage.
    static int Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }

    // Shorthand for a color from 0-255 channel values.
    static Color C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);

    // ---------- tiles ----------

    // Pebbles embedded in the dirt, each as {centre x, centre y, radius x, radius y} in pixels of the 32 x 32 tile.
    // All sit well inside the tile so the dirt tiles line up seamlessly side by side and top to bottom.
    static readonly float[][] Pebbles =
    {
        new[] { 7f, 14f, 3f, 2.2f }, new[] { 22f, 10f, 2.4f, 1.8f }, new[] { 15f, 24f, 3.4f, 2.4f },
        new[] { 27f, 26f, 2.2f, 1.6f }, new[] { 5f, 28f, 2f, 1.5f }, new[] { 23f, 19f, 2f, 1.5f },
    };

    // Color of the dirt tile at pixel (x, y): a vertical brown gradient with light and dark speckle,
    // shaded pebbles lit from the top-left, and a soft drop shadow to the lower right of each pebble.
    static Color DirtColor(int x, int y)
    {
        Color c = Color.Lerp(C(158, 110, 70), C(112, 74, 46), y / 32f);
        int n = Hash(x / 2, y / 2) % 100; // speckle in 2 x 2 pixel blocks
        if (n < 10) c *= 0.88f;
        else if (n > 90) c *= 1.08f;

        foreach (var p in Pebbles)
        {
            float dx = (x + 0.5f - p[0]) / p[2], dy = (y + 0.5f - p[1]) / p[3];
            float sx = (x + 0.5f - (p[0] + 1f)) / p[2], sy = (y + 0.5f - (p[1] + 1f)) / p[3]; // same ellipse shifted 1 px down-right
            if (dx * dx + dy * dy <= 1f)
            {
                // Inside the pebble: light it like a little sphere.
                float z = Mathf.Sqrt(Mathf.Max(0.1f, 1f - (dx * dx + dy * dy)));
                float d = Vector3.Dot(new Vector3(dx, dy, z).normalized, new Vector3(-0.55f, -0.65f, 0.55f).normalized);
                Color pebble = C(180, 142, 102) * Mathf.Lerp(0.72f, 1.28f, Mathf.Clamp01(d));
                pebble.a = 1f;
                return pebble;
            }
            // In the shifted copy but not in the pebble itself: that is its shadow.
            if (sx * sx + sy * sy <= 1f) c *= 0.74f;
        }
        c.a = 1f;
        return c;
    }

    // Color of the grass tile at pixel (x, y): dirt below a wavy grass layer.
    // The grass depth varies along x with exactly three sine periods across the 32 pixels, so tiles repeat seamlessly,
    // plus a little hash noise for irregular blades. Bands from the top: bright rim, light, body with lighter blades,
    // darker underside, darkest edge; just under the grass the dirt is shadowed (ambient occlusion).
    static Color GrassColor(int x, int y)
    {
        int depth = 10 + Mathf.RoundToInt(2f * Mathf.Sin(x * 2f * Mathf.PI * 3f / 32f + 0.8f) + 0.5f * (Hash(x, 3) % 3));
        if (y >= depth)
        {
            Color d = DirtColor(x, y);
            float ao = Mathf.Clamp01((y - depth) / 5f); // 0 right under the grass, 1 five pixels down
            d *= Mathf.Lerp(0.58f, 1f, ao);
            d.a = 1f;
            return d;
        }
        if (y == 0) return C(176, 248, 112);                  // bright top rim
        if (y == 1) return C(136, 230, 94);
        if (y == depth - 1) return C(34, 110, 52);            // darkest bottom edge
        if (y >= depth - 3) return C(52, 160, 64);            // darker underside
        if (Hash(x, 7) % 4 == 0) return C(116, 224, 84);      // lighter blade columns
        return C(92, 206, 72);                                // base grass
    }

    // Color of the brick tile at pixel (x, y). Four rows of bricks, each 8 pixels tall (7 of brick + 1 of mortar) and
    // 16 wide, with alternate rows offset by half a brick. Each brick is bevelled: light top and left edges, dark
    // bottom and right edges, and a little noise.
    static Color BrickColor(int x, int y)
    {
        int row = y / 8, v = y % 8;
        int xx = (x + (row % 2 == 0 ? 0 : 8)) % 16; // position within the brick, shifted on odd rows
        Color mortar = C(190, 160, 142);
        if (v == 7) return mortar * 0.9f + new Color(0, 0, 0, 1) * 0.1f; // horizontal mortar line
        if (xx == 15) return mortar;                                      // vertical mortar line

        Color c = C(222, 98, 54);
        float n = 1f + ((Hash(x, y) % 100) - 50) / 900f; // plus or minus about 5% noise
        c *= n;
        if (v == 0) c *= 1.26f;          // lit top edge
        else if (xx == 0) c *= 1.12f;    // lit left edge
        if (v == 6) c *= 0.72f;          // shadowed bottom edge
        else if (xx == 14) c *= 0.8f;    // shadowed right edge
        c.a = 1f;
        return c;
    }

    // The spikes tile: two cones on a metal base plate. Each cone has a light left half and a darker right half
    // (as if lit from the left), a white centre line and a dark outline added in a second pass.
    static Texture2D Spikes32()
    {
        var px = new Color[32 * 32];
        Color metalLight = C(228, 234, 250), metalDark = C(150, 158, 192), outline = C(44, 48, 82);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                Color c = Color.clear;
                if (y >= 27)
                {
                    // Base plate along the bottom 5 rows with a highlight on top and a shadow on the bottom.
                    c = y == 27 ? C(170, 176, 204) : y == 31 ? C(60, 64, 92) : C(100, 106, 134);
                    if (x == 0 || x == 31) { c *= 0.7f; c.a = 1f; }
                }
                else
                {
                    // Two cones centred at x = 8 and x = 24. Each is a triangle whose half-width grows from 0 at the tip (y = 5)
                    // to 7.5 pixels near the base (y = 27).
                    foreach (float cx in new[] { 8f, 24f })
                    {
                        float half = 7.5f * (y - 5f) / 22f;
                        if (y >= 5 && Mathf.Abs(x + 0.5f - cx) <= half + 0.5f)
                        {
                            bool left = x + 0.5f < cx;
                            c = Color.Lerp(left ? metalLight : metalDark, left ? metalDark : C(96, 102, 140), (y - 5f) / 24f);
                            if (Mathf.Abs(x + 0.5f - cx) < 0.6f) c = Color.white; // bright centre ridge
                        }
                    }
                }
                px[y * 32 + x] = c;
            }
        }

        // Second pass: any empty pixel touching a filled one becomes outline.
        var result = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                Color c = px[y * 32 + x];
                if (c.a < 0.5f)
                {
                    bool near = (x > 0 && px[y * 32 + x - 1].a > 0.5f) || (x < 31 && px[y * 32 + x + 1].a > 0.5f)
                                || (y > 0 && px[(y - 1) * 32 + x].a > 0.5f) || (y < 31 && px[(y + 1) * 32 + x].a > 0.5f);
                    c = near ? outline : Color.clear;
                }
                result[y * 32 + x] = c;
            }
        }
        return TopDown(32, 32, (x, y) => result[y * 32 + x]);
    }

    // The goal flag, 32 x 96 pixels (1 x 3 tiles): stone base, white pole, gold ball on top, and a red pennant.
    // The pennant tapers to a point and has a sine-wave fold; each pixel's normal follows the wave so the folds
    // are shaded like real cloth.
    static Texture2D Flag96()
    {
        var p = new PixelPainter(32, 96);
        p.RoundRect(8, 90, 24, 96, 1.8f, C(116, 122, 150), 0.8f);
        p.RoundRect(14.2f, 10, 17.8f, 92, 1.6f, C(236, 238, 248), 1f, 0.5f);
        p.Ellipse(16, 7, 4.6f, 4.6f, C(255, 214, 64), 1f, 0.9f);
        for (int x = 18; x <= 30; x++)
        {
            float t = (x - 18f) / 12f;                                     // 0 at the pole, 1 at the tip
            float h = Mathf.Lerp(22f, 6f, t);                              // pennant height tapers from 22 to 6 px
            float center = 25f + Mathf.Sin((x - 17f) * 0.55f) * 1.6f;      // centre line waves up and down
            var n = new Vector3(-Mathf.Cos((x - 17f) * 0.55f) * 0.9f, 0f, 1f); // surface tilt follows the wave slope
            for (int y = Mathf.RoundToInt(center - h / 2f); y <= Mathf.RoundToInt(center + h / 2f); y++)
                p.Custom(x, y, C(236, 64, 72), n);
        }
        p.Ellipse(23, 25, 2f, 2f, C(255, 244, 220), 0.3f); // small cream emblem
        return p.Bake();
    }

    // A fluffy cloud 96 x 40 pixels, made of overlapping lit ellipses on a rounded base. No outline, so it stays soft.
    static Texture2D Cloud96()
    {
        var p = new PixelPainter(96, 40);
        Color white = C(255, 255, 255);
        p.RoundRect(10, 24, 88, 36, 6, white, 0.9f);
        p.Ellipse(24, 25, 16, 12, white, 0.9f);
        p.Ellipse(46, 18, 20, 15, white, 0.9f);
        p.Ellipse(68, 24, 17, 13, white, 0.9f);
        p.Ellipse(82, 28, 12, 9, white, 0.9f);
        return p.Bake(false, 0);
    }

    // A soft ground shadow 28 x 8 pixels: an ellipse darkest in the middle (34% opaque) with a lighter rim (18%).
    static Texture2D Shadow28()
    {
        return TopDown(28, 8, (x, y) =>
        {
            float dx = (x + 0.5f - 14f) / 14f, dy = (y + 0.5f - 4f) / 4f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            return new Color(0.05f, 0.03f, 0.1f, r >= 1f ? 0f : (r > 0.6f ? 0.18f : 0.34f));
        });
    }

    // The lives icon, 15 x 14 pixels. A pixel is inside the heart when the classic "heart curve" inequality
    // (x^2 + y^2 - 1)^3 - x^2 y^3 is at most zero (x and y scaled to roughly -1..1). Shaded as a glossy ball.
    static Texture2D Heart15()
    {
        var p = new PixelPainter(15, 14);
        for (int y = 0; y < 14; y++)
        {
            for (int x = 0; x < 15; x++)
            {
                float hx = (x + 0.5f - 7.5f) / 7f, hy = (6.2f - (y + 0.5f)) / 6.2f;
                float v = Mathf.Pow(hx * hx + hy * hy - 1f, 3f) - hx * hx * hy * hy * hy;
                if (v > 0f) continue;
                var n = new Vector3(hx * 0.9f, -hy * 0.7f, Mathf.Sqrt(Mathf.Max(0.15f, 1f - hx * hx * 0.6f)));
                p.Custom(x, y, C(240, 60, 80), n, 0.5f);
            }
        }
        return p.Bake();
    }

    // ---------- characters ----------

    // The hero, 26 x 36 pixels (plus outline): a chibi explorer with a teal hood, orange scarf and blue tunic.
    // Drawn back to front: scarf tail, legs and boots, torso, belt, front arm, scarf, head, hood, face plate,
    // hood trim, bobble, eyes, cheeks and mouth.
    // pose: 0 = idle, 1 = walk A (front leg lifted), 2 = walk B (back leg lifted), 3 = jump (legs tucked, arm raised).
    static Texture2D Hero(int pose)
    {
        var p = new PixelPainter(26, 36);
        Color skin = C(255, 208, 160), hood = C(38, 200, 190), hoodLight = C(130, 236, 224);
        Color scarf = C(255, 150, 50), tunic = C(88, 102, 228), tunicDark = C(66, 78, 192);
        Color belt = C(150, 86, 44), boot = C(134, 78, 44), gold = C(255, 220, 70);

        // Pose parameters: how high each foot is lifted and where the legs stand horizontally.
        bool jump = pose == 3;
        float liftBack = pose == 2 ? 2.2f : (jump ? 2.5f : 0f);
        float liftFront = pose == 1 ? 2.2f : (jump ? 2.5f : 0f);
        float backX = pose == 1 ? 7.5f : pose == 2 ? 10f : jump ? 6.5f : 9f;
        float frontX = pose == 1 ? 15.5f : pose == 2 ? 13f : jump ? 15.5f : 14f;

        // Scarf tail hanging behind (streaming back horizontally when jumping).
        if (jump) p.RoundRect(0.5f, 19.5f, 9f, 23f, 1.7f, scarf, 0.9f);
        else
        {
            float tailTop = pose == 0 ? 21.5f : 20.5f;
            p.RoundRect(4f, tailTop, 9.2f, tailTop + 6.5f, 1.8f, scarf, 0.9f);
        }

        // A leg is a rounded rectangle plus a boot; 'toe' stretches the boot forward (the front foot points right).
        void Leg(float x, float lift, Color leg, float toe)
        {
            p.RoundRect(x, 28.5f, x + 4f, 33f - lift, 1.6f, leg, 0.9f);
            p.RoundRect(x - 0.8f, 31.2f - lift, x + 5f + toe, 35.5f - lift, 2f, boot, 0.9f);
        }
        Leg(backX, liftBack, tunicDark, 0f);
        Leg(frontX, liftFront, tunic, 1.5f);

        // Torso, belt and buckle.
        p.RoundRect(7.5f, 21.4f, 18.5f, 30.4f, 4f, tunic, 0.9f);
        p.RoundRect(7.5f, 27.2f, 18.5f, 29.2f, 0.9f, belt, 0.5f);
        p.Rect(12, 27, 3, 2, gold);

        // Front arm (sleeve and hand); raised when jumping.
        float armY = jump ? 20.5f : 25.6f;
        p.Ellipse(19.2f, armY, 2.4f, 4f, tunic, 0.9f);
        p.Ellipse(19.8f, armY + (jump ? -3.8f : 3.8f), 1.8f, 1.8f, skin, 0.9f);

        // Scarf around the neck.
        p.RoundRect(8f, 19.6f, 18.8f, 23f, 1.6f, scarf, 0.8f);

        // Head: a skin-colored ball, hood side flap, hood top, a flatter lit "face plate" (so the face is not in
        // shadow), a light hood trim across the forehead, and the orange bobble on top.
        p.Ellipse(13f, 12.4f, 9.4f, 8.2f, skin, 0.7f);
        p.Ellipse(4.8f, 13.6f, 3.2f, 5.6f, hood, 0.9f);
        p.Ellipse(13f, 7.4f, 10.2f, 5.8f, hood, 1f);
        p.Ellipse(14.6f, 17f, 7.6f, 4.8f, skin, 0.35f);
        p.RoundRect(4.2f, 11.4f, 22f, 13.6f, 1.1f, hoodLight, 0.5f);
        p.Ellipse(13f, 2.4f, 2.4f, 2.4f, scarf, 1f, 0.6f);

        // Face (the hero faces right): two eyes with a white highlight pixel, pink cheeks and a small mouth.
        Color eye = C(40, 28, 60);
        p.Ellipse(15.2f, 16.6f, 1.7f, 2.5f, eye, 0.8f, 0.4f);
        p.Ellipse(20.4f, 16.6f, 1.7f, 2.5f, eye, 0.8f, 0.4f);
        p.Pixel(14, 15, Color.white);
        p.Pixel(19, 15, Color.white);
        p.Ellipse(13.6f, 19.4f, 1.6f, 0.9f, C(255, 140, 150), 0.3f);
        p.Ellipse(21.6f, 19.4f, 1.6f, 0.9f, C(255, 140, 150), 0.3f);
        p.Rect(17, 20, 2, 1, C(150, 60, 60));

        return p.Bake();
    }

    // The enemy, 28 x 26 pixels: a round purple blob with little orange horns, big angry eyes, fangs and two feet.
    // frame: 0 or 1; the feet swap which one is raised, giving a two-frame waddle.
    static Texture2D Enemy(int frame)
    {
        var p = new PixelPainter(28, 26);
        Color purple = C(170, 86, 212), dark = C(98, 46, 140), horn = C(255, 172, 60);

        // Feet are drawn first so the body overlaps them.
        float leftTop = frame == 0 ? 20.5f : 22f, rightTop = frame == 0 ? 22f : 20.5f;
        p.RoundRect(4.5f, leftTop, 12.5f, 25.5f, 2.2f, dark, 0.9f);
        p.RoundRect(15.5f, rightTop, 23.5f, 25.5f, 2.2f, dark, 0.9f);

        // Three horns, partly hidden behind the body.
        p.Ellipse(8.5f, 4.8f, 1.8f, 3.6f, horn, 0.9f);
        p.Ellipse(14f, 3.2f, 1.8f, 3.6f, horn, 0.9f);
        p.Ellipse(19.5f, 4.8f, 1.8f, 3.6f, horn, 0.9f);

        // Body.
        p.Ellipse(14f, 14f, 12.6f, 10.8f, purple, 1f, 0.2f);

        // Eyes: white balls with dark pupils (with a strong glint).
        p.Ellipse(9.6f, 12f, 3.8f, 4.4f, Color.white, 0.55f, 0.3f);
        p.Ellipse(18.4f, 12f, 3.8f, 4.4f, Color.white, 0.55f, 0.3f);
        Color pupil = C(30, 18, 50);
        p.Ellipse(10.8f, 13f, 1.9f, 2.4f, pupil, 0.9f, 0.9f);
        p.Ellipse(19.2f, 13f, 1.9f, 2.4f, pupil, 0.9f, 0.9f);

        // Angry eyebrows: two slanted lines of 2-pixel-tall steps, mirrored left and right.
        for (int i = 0; i <= 7; i++)
        {
            int y = 6 + Mathf.RoundToInt(i * 0.42f);
            p.Rect(5 + i, y, 1, 2, pupil);
            p.Rect(22 - i, y, 1, 2, pupil);
        }
        // Mouth with two white fangs.
        p.Rect(11, 18, 6, 2, C(70, 24, 80));
        p.Pixel(12, 18, Color.white);
        p.Pixel(15, 18, Color.white);

        return p.Bake();
    }

    // The spiky enemy, 28 x 26 pixels: a red round shell with a crown of five white spikes on top, small angry eyes
    // and two dark feet. Stomping it hurts the player, so it must be avoided or shot (it takes two hits).
    static Texture2D Spiky(int frame)
    {
        var p = new PixelPainter(28, 26);
        Color shell = C(222, 74, 78), dark = C(120, 36, 60), bone = C(250, 244, 226);

        // Feet first, like the walker, so the body overlaps them.
        float leftTop = frame == 0 ? 21f : 22.5f, rightTop = frame == 0 ? 22.5f : 21f;
        p.RoundRect(4.5f, leftTop, 12.5f, 25.5f, 2.2f, dark, 0.9f);
        p.RoundRect(15.5f, rightTop, 23.5f, 25.5f, 2.2f, dark, 0.9f);

        // Five spikes: each is a triangle built from rows that get wider toward the base. Painted before the body so
        // the shell covers their bottoms. Spike k is centred at x = 4.5 + 4.75 k and 8 pixels tall.
        for (int k = 0; k < 5; k++)
        {
            float cx = 4.5f + 4.75f * k;
            float height = k == 2 ? 9f : (k == 1 || k == 3) ? 8f : 6.5f;
            for (int r = 0; r < height; r++)
            {
                float half = 0.6f + r * 0.42f;
                for (int x = Mathf.RoundToInt(cx - half); x <= Mathf.RoundToInt(cx + half); x++)
                    p.Custom(x, 11 - Mathf.RoundToInt(height) + r + 2, bone, new Vector3((x - cx) * 0.25f, -0.2f, 1f), 0.2f);
            }
        }

        // Shell: a big round body with a lighter dome patch for a shiny look.
        p.Ellipse(14f, 16f, 12.6f, 9.6f, shell, 1f, 0.25f);
        p.Ellipse(10f, 12.5f, 4.2f, 2.4f, C(250, 130, 120), 0.5f);

        // Eyes: white with dark pupils, set low so the spikes read as a hairstyle, plus a frown.
        Color pupil = C(30, 18, 50);
        p.Ellipse(9.8f, 16f, 3.0f, 3.4f, Color.white, 0.55f, 0.3f);
        p.Ellipse(18.2f, 16f, 3.0f, 3.4f, Color.white, 0.55f, 0.3f);
        p.Ellipse(10.8f, 16.8f, 1.5f, 1.9f, pupil, 0.9f, 0.9f);
        p.Ellipse(19.2f, 16.8f, 1.5f, 1.9f, pupil, 0.9f, 0.9f);
        for (int i = 0; i <= 5; i++)
        {
            int y = 11 + Mathf.RoundToInt(i * 0.45f);
            p.Rect(6 + i, y, 1, 2, pupil);
            p.Rect(21 - i, y, 1, 2, pupil);
        }
        p.Rect(11, 21, 6, 1, C(70, 24, 60));
        return p.Bake();
    }

    // The flying enemy, 28 x 22 pixels: a round magenta bat with two flapping wings, big eyes and tiny fangs.
    // frame 0 = wings raised, frame 1 = wings lowered. It hovers; there is no foot, the pivot is simply the bottom.
    static Texture2D Flyer(int frame)
    {
        var p = new PixelPainter(28, 22);
        Color body = C(255, 96, 170), wing = C(168, 56, 150), wingLight = C(214, 100, 190), pupil = C(30, 18, 50);

        // Wings behind the body. A wing is a rounded rectangle plus a darker rib line; raised wings point up and out,
        // lowered wings point down and out.
        float wy = frame == 0 ? 1.5f : 8.5f;
        float tilt = frame == 0 ? -1.5f : 1.5f;
        p.RoundRect(0.5f, wy + tilt, 10.5f, wy + 8f + tilt, 3f, wing, 0.9f);
        p.RoundRect(17.5f, wy + tilt, 27.5f, wy + 8f + tilt, 3f, wing, 0.9f);
        p.RoundRect(2f, wy + tilt + 1.2f, 9f, wy + 3.5f + tilt, 1.2f, wingLight, 0.5f);
        p.RoundRect(19f, wy + tilt + 1.2f, 26f, wy + 3.5f + tilt, 1.2f, wingLight, 0.5f);

        // Round body, two little ears, eyes and fangs.
        p.Ellipse(9.2f, 6.8f, 2.0f, 3.2f, body, 0.9f);
        p.Ellipse(18.8f, 6.8f, 2.0f, 3.2f, body, 0.9f);
        p.Ellipse(14f, 13f, 8.4f, 7.4f, body, 1f, 0.35f);
        p.Ellipse(11f, 12f, 2.5f, 3.0f, Color.white, 0.55f, 0.3f);
        p.Ellipse(17f, 12f, 2.5f, 3.0f, Color.white, 0.55f, 0.3f);
        p.Ellipse(11.8f, 12.8f, 1.2f, 1.6f, pupil, 0.9f, 0.9f);
        p.Ellipse(17.8f, 12.8f, 1.2f, 1.6f, pupil, 0.9f, 0.9f);
        p.Rect(12, 17, 4, 1, C(100, 20, 70));
        p.Pixel(12, 18, Color.white);
        p.Pixel(15, 18, Color.white);
        return p.Bake();
    }

    // Draws the blaster with its top-left corner at (ox, oy) in the painter's coordinates: a steel body with a glowing
    // cyan energy cell, a darker barrel with an orange muzzle, and a grip. Faces right.
    static void GunShapes(PixelPainter p, float ox, float oy)
    {
        Color steel = C(160, 170, 210), barrel = C(112, 122, 176), grip = C(76, 82, 120);
        p.RoundRect(ox + 3f, oy + 5.5f, ox + 6.8f, oy + 9.8f, 1f, grip, 0.8f);
        p.RoundRect(ox + 1f, oy + 1.5f, ox + 11.5f, oy + 6.5f, 2f, steel, 0.8f, 0.3f);
        p.RoundRect(ox + 10f, oy + 2.4f, ox + 14.8f, oy + 5.2f, 1f, barrel, 0.7f);
        p.Rect(Mathf.RoundToInt(ox + 14), Mathf.RoundToInt(oy + 3), 1, 2, C(255, 190, 60));
        p.Rect(Mathf.RoundToInt(ox + 4), Mathf.RoundToInt(oy + 3), 4, 2, C(90, 240, 255));
        p.Pixel(Mathf.RoundToInt(ox + 4), Mathf.RoundToInt(oy + 3), Color.white);
    }

    // The gun alone (15 x 10) or inside a soft cyan aura (25 x 22) for the floating pickup.
    static Texture2D GunTexture(bool aura)
    {
        if (!aura)
        {
            var g = new PixelPainter(15, 10);
            GunShapes(g, 0f, 0f);
            return g.Bake();
        }
        var p = new PixelPainter(25, 22);
        p.Ellipse(12.5f, 11f, 12.2f, 10.5f, C(90, 220, 255, 60), 0f);
        p.Ellipse(12.5f, 11f, 9.4f, 8.2f, C(140, 240, 255, 90), 0f);
        GunShapes(p, 5f, 6f);
        return p.Bake(false, 0);
    }

    // A blaster shot, 9 x 5: a cyan orb with a white-hot core and a small trail on the left.
    static Texture2D BulletTexture()
    {
        var p = new PixelPainter(9, 5);
        p.Ellipse(4.5f, 2.5f, 4.4f, 2.4f, C(70, 220, 255), 0.6f, 0.2f);
        p.Ellipse(5.6f, 2.5f, 2.3f, 1.4f, Color.white, 0.2f, 0.5f);
        return p.Bake();
    }

    // One frame of the coin, 22 x 22 pixels. 'w' is the fraction of full width: 1 = facing us, 0.2 = nearly edge-on.
    // A darker gold rim ellipse is drawn first, then (when wide enough to show the face) a lighter inner face and
    // a vertical slot mark whose width scales with the coin.
    static Texture2D Coin(float w)
    {
        var p = new PixelPainter(22, 22);
        float rx = Mathf.Max(1.8f, 10f * w);
        Color rim = C(240, 172, 28), face = C(255, 216, 64), slot = C(206, 142, 18);
        p.Ellipse(11, 11, rx, 10f, rim, 0.95f, 0.7f);
        if (w > 0.4f)
        {
            p.Ellipse(11, 11, rx * 0.72f, 7.4f, face, 0.35f, 0.8f);
            p.RoundRect(11 - rx * 0.13f, 6.2f, 11 + rx * 0.13f, 15.8f, 0.8f, slot, 0.3f);
        }
        return p.Bake();
    }

    // ---------- scenery props ----------

    // Scenery props placed behind the player along the ground (Game.PlaceDecor). Each is a few shaded shapes.
    //   bush      three overlapping green balls with tiny flowers
    //   flowers   three stems with pink, white and yellow blossoms
    //   pine      four stacked green tiers with snow caps on a brown trunk
    //   cactus    a trunk with two arms and a pink flower
    //   crystal   two tall faceted gems (cyan and magenta), flat-shaded per face
    //   lavarock  dark rocks with glowing orange cracks
    //   mushroom  red cap with white spots on a cream stem
    //   rock      two grey boulders (also the fallback for any unknown name)
    static Texture2D DecorTexture(string kind)
    {
        switch (kind)
        {
            case "bush":
            {
                var p = new PixelPainter(40, 24);
                p.Ellipse(10, 16, 9, 7, C(52, 170, 76));
                p.Ellipse(32, 17, 8, 6.5f, C(52, 170, 76));
                p.Ellipse(22, 13, 11, 9.5f, C(72, 192, 88));
                p.Ellipse(14, 10.5f, 1.4f, 1.4f, C(255, 110, 160), 0.3f);
                p.Ellipse(26, 8.5f, 1.4f, 1.4f, C(255, 232, 90), 0.3f);
                p.Ellipse(30, 15.5f, 1.3f, 1.3f, C(255, 110, 160), 0.3f);
                return p.Bake();
            }
            case "flowers":
            {
                var p = new PixelPainter(24, 22);
                Color stem = C(70, 170, 70);
                p.RoundRect(5.5f, 8, 6.6f, 21.5f, 0.5f, stem, 0.6f);
                p.RoundRect(11.5f, 5, 12.6f, 21.5f, 0.5f, stem, 0.6f);
                p.RoundRect(17.5f, 10, 18.6f, 21.5f, 0.5f, stem, 0.6f);
                p.Ellipse(9, 17, 3, 1.5f, stem, 0.8f);
                p.Ellipse(15, 15, 3, 1.5f, stem, 0.8f);
                p.Ellipse(6, 7, 3.4f, 3.4f, C(255, 110, 170), 0.9f);
                p.Ellipse(6, 7, 1.3f, 1.3f, C(255, 224, 80), 0.4f);
                p.Ellipse(12, 4, 3.6f, 3.6f, C(255, 250, 240), 0.9f);
                p.Ellipse(12, 4, 1.4f, 1.4f, C(255, 190, 60), 0.4f);
                p.Ellipse(18, 9, 3.2f, 3.2f, C(255, 200, 60), 0.9f);
                p.Ellipse(18, 9, 1.3f, 1.3f, C(255, 120, 80), 0.4f);
                return p.Bake();
            }
            case "pine":
            {
                var p = new PixelPainter(32, 48);
                p.RoundRect(13.5f, 38, 18.5f, 48, 1f, C(132, 84, 50), 0.8f);
                Color pine = C(34, 150, 112);
                p.Ellipse(16, 34, 12.5f, 6.8f, pine, 0.9f);
                p.Ellipse(16, 26, 10.5f, 6.4f, pine, 0.9f);
                p.Ellipse(16, 18, 8.5f, 6f, pine, 0.9f);
                p.Ellipse(16, 11, 5.5f, 5.4f, pine, 0.9f);
                Color snow = C(240, 250, 255);
                p.Ellipse(16, 30.5f, 8.5f, 3f, snow, 0.8f);
                p.Ellipse(16, 22.5f, 7f, 3f, snow, 0.8f);
                p.Ellipse(16, 14.5f, 5.5f, 2.8f, snow, 0.8f);
                p.Ellipse(16, 8.4f, 3.4f, 2.4f, snow, 0.8f);
                return p.Bake();
            }
            case "cactus":
            {
                var p = new PixelPainter(32, 48);
                Color green = C(72, 178, 98);
                p.RoundRect(3, 20, 11, 26, 3f, green, 0.9f);
                p.RoundRect(3, 14, 8, 24, 2.6f, green, 0.9f);
                p.RoundRect(21, 24, 29, 30, 3f, green, 0.9f);
                p.RoundRect(24, 16, 29, 28, 2.6f, green, 0.9f);
                p.RoundRect(11, 8, 21, 47.5f, 4.5f, green, 0.95f);
                p.Ellipse(16, 7.5f, 2.8f, 2.8f, C(255, 112, 164), 0.9f);
                p.Rect(15, 16, 1, 2, C(255, 250, 200));
                p.Rect(17, 26, 1, 2, C(255, 250, 200));
                p.Rect(14, 34, 1, 2, C(255, 250, 200));
                return p.Bake();
            }
            case "crystal":
            {
                var p = new PixelPainter(30, 40);
                // A crystal is a hexagonal prism seen from the side: pointed top that widens over the first third
                // of its height, then straight sides. The left face is tilted toward the light, the right face away,
                // and a thin centre strip faces the viewer, so each face gets its own flat tone.
                void Crystal(float cx, float baseY, float hw, float h, Color col)
                {
                    float top = baseY - h;
                    for (int y = Mathf.FloorToInt(top); y <= Mathf.CeilToInt(baseY); y++)
                    {
                        float t = (y + 0.5f - top) / h;
                        if (t < 0f || t > 1f) continue;
                        float w = hw * Mathf.Min(1f, t / 0.32f) * (t > 0.85f ? 0.9f : 1f);
                        for (int x = Mathf.FloorToInt(cx - w); x <= Mathf.CeilToInt(cx + w); x++)
                        {
                            float dx = x + 0.5f - cx;
                            if (Mathf.Abs(dx) > w) continue;
                            Vector3 n = Mathf.Abs(dx) < 1f ? new Vector3(0, 0, 1) : dx < 0 ? new Vector3(-0.8f, -0.1f, 0.6f) : new Vector3(0.8f, 0.1f, 0.6f);
                            p.Custom(x, y, col, n, 0.5f);
                        }
                    }
                }
                Crystal(21, 39.5f, 5.4f, 26f, C(236, 112, 255));
                Crystal(10, 39.5f, 6.4f, 34f, C(84, 224, 255));
                return p.Bake();
            }
            case "lavarock":
            {
                var p = new PixelPainter(28, 18);
                p.Ellipse(10, 11, 9, 6.5f, C(96, 70, 84), 0.95f);
                p.Ellipse(19, 10, 8, 7.5f, C(112, 82, 96), 0.95f);
                Color glow = C(255, 150, 40);
                p.Rect(8, 9, 1, 3, glow); p.Rect(9, 12, 2, 1, glow); p.Rect(17, 7, 1, 3, glow); p.Rect(18, 10, 2, 1, glow); p.Rect(21, 11, 1, 2, glow);
                return p.Bake();
            }
            case "mushroom":
            {
                var p = new PixelPainter(22, 24);
                p.RoundRect(8, 12, 14, 22, 2.4f, C(252, 242, 224), 0.9f);
                p.Ellipse(11, 10, 10, 7.5f, C(232, 62, 74), 1f, 0.4f);
                p.Ellipse(6, 9, 1.7f, 1.4f, Color.white, 0.2f);
                p.Ellipse(12, 6, 1.8f, 1.5f, Color.white, 0.2f);
                p.Ellipse(16.5f, 10, 1.6f, 1.3f, Color.white, 0.2f);
                return p.Bake();
            }
            default: // rock
            {
                var p = new PixelPainter(28, 18);
                p.Ellipse(10, 11, 9, 6.5f, C(146, 146, 170), 0.95f);
                p.Ellipse(19, 10, 8, 7.5f, C(164, 164, 186), 0.95f);
                return p.Bake();
            }
        }
    }
}
