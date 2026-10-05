#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Renders the game's logo, itch.io cover and app icon from the game's own procedural art.
public static class CoverArt
{
    const string OutDir = "/Users/theprotagonist/Claude/ProjectIdeas/IndieGames/pixel-hopper/branding";

    class Canvas
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public Canvas(int w, int h)
        {
            W = w;
            H = h;
            Px = new Color32[w * h];
        }

        public void Blit(Color32[] src, int sw, int sh, int dx, int dy, int scale = 1, Color? tint = null)
        {
            for (int sy = 0; sy < sh * scale; sy++)
            {
                int y = dy + sy;
                if (y < 0 || y >= H) continue;
                for (int sx = 0; sx < sw * scale; sx++)
                {
                    int x = dx + sx;
                    if (x < 0 || x >= W) continue;
                    Color32 s = src[(sy / scale) * sw + sx / scale];
                    if (s.a == 0) continue;
                    if (tint.HasValue)
                    {
                        var t = tint.Value;
                        s = new Color32((byte)(s.r * t.r), (byte)(s.g * t.g), (byte)(s.b * t.b), (byte)(s.a * t.a));
                        if (s.a == 0) continue;
                    }
                    if (s.a == 255) { Px[y * W + x] = s; continue; }
                    Color32 d = Px[y * W + x];
                    float a = s.a / 255f;
                    Px[y * W + x] = new Color32(
                        (byte)(s.r * a + d.r * (1f - a)), (byte)(s.g * a + d.g * (1f - a)), (byte)(s.b * a + d.b * (1f - a)), 255);
                }
            }
        }

        public void Blit(Texture2D tex, int dx, int dy, int scale = 1, Color? tint = null) => Blit(tex.GetPixels32(), tex.width, tex.height, dx, dy, scale, tint);

        public void Blit(Sprite sprite, int dx, int dy, int scale = 1, Color? tint = null)
        {
            Rect r = sprite.textureRect;
            var c = sprite.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            var px = new Color32[c.Length];
            for (int i = 0; i < c.Length; i++) px[i] = c[i];
            Blit(px, (int)r.width, (int)r.height, dx, dy, scale, tint);
        }

        public void FillRect(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(H, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(W, x1); x++)
                    Px[y * W + x] = c;
        }

        // Blends the bottom rows of the canvas into a solid color so the image melts into a page background.
        public void FadeToSolid(int rows, Color32 solid)
        {
            for (int y = 0; y < rows && y < H; y++)
            {
                float a = 1f - Mathf.SmoothStep(0f, 1f, y / (float)rows);
                for (int x = 0; x < W; x++)
                {
                    Color32 p = Px[y * W + x];
                    Px[y * W + x] = new Color32(
                        (byte)Mathf.Lerp(p.r, solid.r, a), (byte)Mathf.Lerp(p.g, solid.g, a), (byte)Mathf.Lerp(p.b, solid.b, a), 255);
                }
            }
        }

        public void Sky(Color top, Color bottom)
        {
            var sky = Backdrop.MakeSky(top, bottom).GetPixels32();
            for (int y = 0; y < H; y++)
            {
                int row = Mathf.Clamp(Mathf.FloorToInt(y / (float)H * 128f), 0, 127);
                for (int x = 0; x < W; x++) Px[y * W + x] = sky[row * 8 + x % 8];
            }
        }

        public Texture2D Upscale(int factor)
        {
            var tex = new Texture2D(W * factor, H * factor, TextureFormat.RGBA32, false);
            var big = new Color32[W * factor * H * factor];
            for (int y = 0; y < H * factor; y++)
                for (int x = 0; x < W * factor; x++)
                    big[y * W * factor + x] = Px[(y / factor) * W + x / factor];
            tex.SetPixels32(big);
            tex.Apply();
            return tex;
        }
    }

    static Texture2D BuildLogo()
    {
        var colors = new Color[]
        {
            new Color32(255, 90, 90, 255), new Color32(255, 160, 50, 255), new Color32(255, 220, 60, 255), new Color32(110, 220, 90, 255), new Color32(60, 200, 230, 255),
            new Color32(90, 140, 255, 255), new Color32(170, 110, 255, 255), new Color32(255, 110, 200, 255), new Color32(255, 90, 90, 255), new Color32(255, 160, 50, 255), new Color32(255, 220, 60, 255),
        };
        return PixelFont.RenderLogo(new[] { "PIXEL", "HOPPER" }, colors);
    }

    static void Save(Texture2D tex, string name)
    {
        File.WriteAllBytes(Path.Combine(OutDir, name), tex.EncodeToPNG());
    }

    [MenuItem("Tools/Pixel Hopper/Generate Branding")]
    public static string Generate()
    {
        Directory.CreateDirectory(OutDir);
        var theme = Levels.ThemeFor(0);

        // ---- logo (transparent) ----
        var logo = BuildLogo();
        var logoCanvas = new Canvas(logo.width, logo.height);
        logoCanvas.Blit(logo, 0, 0);
        Save(logoCanvas.Upscale(2), "logo.png");

        // ---- cover 630x500 ----
        var c = new Canvas(315, 250);
        c.Sky(theme.SkyTop, theme.SkyBottom);
        c.Blit(Backdrop.MakeBody(theme.Body, theme.BodyColor), 196, 150, 2);
        c.Blit(PixelArt.Cloud, 8, 190);
        c.Blit(PixelArt.Cloud, 214, 132);
        c.Blit(Backdrop.MakeLayer(theme.FarShape, theme.FarColor, 6, 512, 352, 5.6f, 2.4f, true), -70, -46);
        c.Blit(Backdrop.MakeLayer(theme.NearShape, theme.NearColor, 7, 512, 256, 4.0f, 1.2f, false), -200, -46);
        for (int x = 0; x < c.W; x += 32)
        {
            c.Blit(PixelArt.Grass, x, 18);
            c.Blit(PixelArt.Dirt, x, -14);
        }
        c.Blit(PixelArt.Decor("bush"), 6, 49);
        c.Blit(PixelArt.Decor("flowers"), 268, 49);
        c.Blit(PixelArt.Decor("flowers"), 150, 49);
        c.Blit(PixelArt.Shadow, 62, 42, 2);
        c.Blit(PixelArt.Shadow, 207, 42, 2);
        c.Blit(PixelArt.HeroWalkA, 62, 48, 2);
        c.Blit(PixelArt.EnemyA, 206, 48, 2);
        c.Blit(PixelArt.CoinFrame(0), 120, 56, 2);
        c.Blit(PixelArt.CoinFrame(1), 158, 74, 2);
        c.Blit(logo, (c.W - logo.width) / 2, c.H - 6 - logo.height);
        Save(c.Upscale(2), "cover_630x500.png");

        // ---- icon 512x512 ----
        var icon = new Canvas(256, 256);
        icon.Sky(theme.SkyTop, theme.SkyBottom);
        icon.Blit(Backdrop.MakeBody(theme.Body, theme.BodyColor), 150, 150, 2);
        icon.Blit(Backdrop.MakeLayer(theme.NearShape, theme.NearColor, 7, 512, 256, 4.0f, 1.2f, false), -150, -30);
        for (int x = 0; x < icon.W; x += 32)
        {
            icon.Blit(PixelArt.Grass, x, 10);
            icon.Blit(PixelArt.Dirt, x, -22);
        }
        icon.Blit(PixelArt.Shadow, 86, 30, 3);
        icon.Blit(PixelArt.HeroIdle, 72, 38, 4);
        icon.Blit(PixelArt.CoinFrame(0), 24, 150, 3);
        icon.Blit(PixelArt.CoinFrame(1), 176, 170, 3);
        Save(icon.Upscale(2), "icon_512.png");

        AssetDatabase.Refresh();
        return OutDir;
    }

    [MenuItem("Tools/Pixel Hopper/Generate Page Art")]
    public static string GeneratePageArt()
    {
        Directory.CreateDirectory(OutDir);
        string pageDir = Path.Combine(Path.GetDirectoryName(OutDir), "page");
        Directory.CreateDirectory(pageDir);
        var solid = new Color32(19, 10, 40, 255);

        // ---- page background: dusk landscape that fades into the page color (2560x1100) ----
        var bg = new Canvas(640, 275);
        bg.Sky(new Color(0.06f, 0.03f, 0.2f), new Color(0.98f, 0.5f, 0.38f));
        var stars = Backdrop.MakeStars(7);
        bg.Blit(stars, 0, 275 - stars.height);
        bg.Blit(stars, 512, 275 - stars.height);
        bg.Blit(Backdrop.MakeBody(Celestial.Sun, new Color(1f, 0.62f, 0.3f)), 38, 84, 2);
        var cloudTint = new Color(1f, 0.74f, 0.84f, 0.92f);
        bg.Blit(PixelArt.Cloud, 30, 214, 2, cloudTint);
        bg.Blit(PixelArt.Cloud, 285, 236, 1, cloudTint);
        bg.Blit(PixelArt.Cloud, 470, 196, 2, cloudTint);
        bg.Blit(PixelArt.Cloud, 120, 160, 1, cloudTint);
        var far = Backdrop.MakeLayer(LayerShape.Mountains, new Color(0.42f, 0.18f, 0.55f), 11, 512, 6 * 32, 2.0f, 1.7f, false);
        var near = Backdrop.MakeLayer(LayerShape.Hills, new Color(0.22f, 0.09f, 0.36f), 12, 512, 4 * 32, 1.5f, 0.55f, false);
        bg.Blit(far, 0, 50); bg.Blit(far, 512, 50);
        bg.Blit(near, -40, 30); bg.Blit(near, 472, 30);
        bg.FillRect(0, 0, bg.W, 34, new Color32(43, 18, 71, 255));
        var pine = new Color(0.14f, 0.07f, 0.26f, 1f);
        int[] pineX = { 548, 584, 612, 566, 600 };
        int[] pineScale = { 2, 3, 2, 2, 2 };
        int[] pineY = { 30, 28, 32, 22, 20 };
        for (int i = 0; i < pineX.Length; i++)
            bg.Blit(PixelArt.Decor("pine"), pineX[i], pineY[i], pineScale[i], pine);
        bg.Blit(PixelArt.Decor("pine"), 2, 30, 2, pine);
        bg.Blit(PixelArt.Decor("pine"), 22, 24, 2, pine);
        bg.FadeToSolid(120, solid);
        Save(bg.Upscale(4), "../page/page_background_2560x1100.png");

        // ---- environment banner: a journey across the game's worlds, no title or characters (1920x560) ----
        string oldBanner = Path.Combine(OutDir, "../page/banner_1920x560.png");
        if (File.Exists(oldBanner)) File.Delete(oldBanner);

        int[] worlds = { 0, 6, 2, 7, 4, 8 };
        const int bw = 640, bh = 140;
        float segW = bw / (float)worlds.Length;
        var segments = new Canvas[worlds.Length];
        for (int i = 0; i < worlds.Length; i++)
        {
            segments[i] = new Canvas(bw, bh);
            RenderWorld(segments[i], Levels.ThemeFor(worlds[i]), 40 + i * 7, Mathf.RoundToInt(i * segW), Mathf.RoundToInt((i + 1) * segW), i, i == worlds.Length - 1);
        }

        var banner = new Canvas(bw, bh);
        int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        for (int y = 0; y < bh; y++)
        {
            for (int x = 0; x < bw; x++)
            {
                float c = (x + 0.5f) / segW - 0.5f;
                int i0 = Mathf.FloorToInt(c);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.6f, c - i0));
                float threshold = (bayer[y % 4, x % 4] + 0.5f) / 16f;
                int pick = Mathf.Clamp(t > threshold ? i0 + 1 : i0, 0, worlds.Length - 1);
                banner.Px[y * bw + x] = segments[pick].Px[y * bw + x];
            }
        }
        Save(banner.Upscale(3), "../page/banner_environment_1920x420.png");

        AssetDatabase.Refresh();
        return pageDir;
    }

    [MenuItem("Tools/Pixel Hopper/Generate Embed Background")]
    public static string GenerateEmbedBackground()
    {
        string pageDir = Path.Combine(Path.GetDirectoryName(OutDir), "page");
        Directory.CreateDirectory(pageDir);

        // Portrait launch screen shown behind itch's "Run game" button (540x960 frame, supplied at 2x).
        // The middle band is kept clear for the button.
        string oldEmbed = Path.Combine(OutDir, "../page/embed_background_960x960.png");
        if (File.Exists(oldEmbed)) File.Delete(oldEmbed);

        var theme = Levels.ThemeFor(0);
        var c = new Canvas(270, 480);
        c.Sky(theme.SkyTop, theme.SkyBottom);
        c.Blit(Backdrop.MakeBody(theme.Body, theme.BodyColor), 196, 214, 2);
        c.Blit(PixelArt.Cloud, 12, 300, 1);
        c.Blit(PixelArt.Cloud, 160, 150, 1);
        c.Blit(Backdrop.MakeLayer(theme.FarShape, theme.FarColor, 6, 512, 352, 5.6f, 2.4f, true), -60, -52);
        c.Blit(Backdrop.MakeLayer(theme.NearShape, theme.NearColor, 7, 512, 256, 4.0f, 1.2f, false), -150, -22);
        for (int x = 0; x < c.W; x += 32)
        {
            c.Blit(PixelArt.Grass, x, 48);
            c.Blit(PixelArt.Dirt, x, 16);
            c.Blit(PixelArt.Dirt, x, -16);
        }
        c.Blit(PixelArt.Decor("bush"), 4, 79, 2);
        c.Blit(PixelArt.Decor("flowers"), 118, 79, 2);
        c.Blit(PixelArt.Shadow, 30, 68, 3);
        c.Blit(PixelArt.Shadow, 154, 68, 3);
        c.Blit(PixelArt.HeroWalkA, 30, 77, 3);
        c.Blit(PixelArt.EnemyA, 154, 77, 3);
        var logo = BuildLogo();
        c.Blit(logo, (c.W - logo.width) / 2, c.H - 34 - logo.height);

        Save(c.Upscale(4), "../page/embed_cover_1080x1920.png");
        AssetDatabase.Refresh();
        return pageDir;
    }

    static void RenderWorld(Canvas c, StageTheme t, int seed, int x0, int x1, int index, bool lastWorld)
    {
        var rng = new System.Random(seed);
        c.Sky(t.SkyTop, t.SkyBottom);
        if (t.Stars)
        {
            var stars = Backdrop.MakeStars(seed);
            c.Blit(stars, 0, c.H - stars.height);
            c.Blit(stars, 512, c.H - stars.height);
        }
        if (t.Body != Celestial.None)
            c.Blit(Backdrop.MakeBody(t.Body, t.BodyColor), (x0 + x1) / 2 - 24 + rng.Next(-10, 10), 82 + rng.Next(0, 14), 1);
        c.Blit(PixelArt.Cloud, x0 + rng.Next(0, 30), 96 + rng.Next(0, 22), 1, t.CloudTint);

        bool snow = t.Name == "Frozen Peaks" || t.Name == "Sunny Meadow";
        var far = Backdrop.MakeLayer(t.FarShape, t.FarColor, seed + 1, 512, 352, 5.6f, 2.4f, snow);
        var near = Backdrop.MakeLayer(t.NearShape, t.NearColor, seed + 2, 512, 256, 4.0f, 1.2f, false);
        int farX = -rng.Next(0, 300), nearX = -rng.Next(0, 300);
        c.Blit(far, farX, -128); c.Blit(far, farX + 512, -128);
        c.Blit(near, nearX, -98); c.Blit(near, nearX + 512, -98);

        for (int x = 0; x < c.W; x += 32) c.Blit(PixelArt.Grass, x, -6, 1, t.Tint);

        int span = x1 - x0;
        // one piece of scenery on the left of each world
        // pieces stay inside the clean middle of each world (the outer 10% on each side dissolves into the neighbour)
        if (t.Decor.Length > 0)
            c.Blit(PixelArt.Decor(t.Decor[rng.Next(t.Decor.Length)]), x0 + 12, 25, 1, t.Tint);

        // one level piece: brick stack on even worlds, spikes on odd worlds, the goal flag on the last
        int px = x0 + 58;
        if (lastWorld)
        {
            c.Blit(PixelArt.Flag, px, 25, 1);
            c.Blit(PixelArt.CoinFrame(index), x0 + 28, 64, 1);
        }
        else if (index % 2 == 0)
        {
            c.Blit(PixelArt.Brick, px, 25, 1, t.Tint);
            c.Blit(PixelArt.Brick, px, 57, 1, t.Tint);
            c.Blit(PixelArt.CoinFrame(index), px + 4, 94, 1);
            c.Blit(PixelArt.CoinFrame(index + 1), x0 + 26, 68, 1);
        }
        else
        {
            c.Blit(PixelArt.Spikes, px, 25, 1, t.Tint);
            c.Blit(PixelArt.CoinFrame(index), px + 4, 62, 1);
            c.Blit(PixelArt.CoinFrame(index + 1), x0 + 26, 74, 1);
        }
    }
}
#endif
