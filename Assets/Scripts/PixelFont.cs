using System.Collections.Generic;
using UnityEngine;

public static class PixelFont
{
    public const int GlyphW = 5;
    public const int GlyphH = 7;

    static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
    {
        ['A'] = "01110/10001/10001/11111/10001/10001/10001",
        ['B'] = "11110/10001/10001/11110/10001/10001/11110",
        ['C'] = "01110/10001/10000/10000/10000/10001/01110",
        ['D'] = "11110/10001/10001/10001/10001/10001/11110",
        ['E'] = "11111/10000/10000/11110/10000/10000/11111",
        ['F'] = "11111/10000/10000/11110/10000/10000/10000",
        ['G'] = "01110/10001/10000/10111/10001/10001/01111",
        ['H'] = "10001/10001/10001/11111/10001/10001/10001",
        ['I'] = "01110/00100/00100/00100/00100/00100/01110",
        ['J'] = "00111/00010/00010/00010/00010/10010/01100",
        ['K'] = "10001/10010/10100/11000/10100/10010/10001",
        ['L'] = "10000/10000/10000/10000/10000/10000/11111",
        ['M'] = "10001/11011/10101/10101/10001/10001/10001",
        ['N'] = "10001/11001/10101/10011/10001/10001/10001",
        ['O'] = "01110/10001/10001/10001/10001/10001/01110",
        ['P'] = "11110/10001/10001/11110/10000/10000/10000",
        ['Q'] = "01110/10001/10001/10001/10101/10010/01101",
        ['R'] = "11110/10001/10001/11110/10100/10010/10001",
        ['S'] = "01111/10000/10000/01110/00001/00001/11110",
        ['T'] = "11111/00100/00100/00100/00100/00100/00100",
        ['U'] = "10001/10001/10001/10001/10001/10001/01110",
        ['V'] = "10001/10001/10001/10001/10001/01010/00100",
        ['W'] = "10001/10001/10001/10101/10101/11011/10001",
        ['X'] = "10001/10001/01010/00100/01010/10001/10001",
        ['Y'] = "10001/10001/01010/00100/00100/00100/00100",
        ['Z'] = "11111/00001/00010/00100/01000/10000/11111",
        ['0'] = "01110/10001/10011/10101/11001/10001/01110",
        ['1'] = "00100/01100/00100/00100/00100/00100/01110",
        ['2'] = "01110/10001/00001/00010/00100/01000/11111",
        ['3'] = "11110/00001/00001/01110/00001/00001/11110",
        ['4'] = "00010/00110/01010/10010/11111/00010/00010",
        ['5'] = "11111/10000/11110/00001/00001/10001/01110",
        ['6'] = "00110/01000/10000/11110/10001/10001/01110",
        ['7'] = "11111/00001/00010/00100/01000/01000/01000",
        ['8'] = "01110/10001/10001/01110/10001/10001/01110",
        ['9'] = "01110/10001/10001/01111/00001/00010/01100",
        [' '] = "00000/00000/00000/00000/00000/00000/00000",
        ['.'] = "00000/00000/00000/00000/00000/01100/01100",
        [','] = "00000/00000/00000/00000/01100/00100/01000",
        ['!'] = "00100/00100/00100/00100/00100/00000/00100",
        ['?'] = "01110/10001/00001/00010/00100/00000/00100",
        [':'] = "00000/01100/01100/00000/01100/01100/00000",
        ['-'] = "00000/00000/00000/11111/00000/00000/00000",
        ['/'] = "00001/00001/00010/00100/01000/10000/10000",
        ['<'] = "00010/00100/01000/10000/01000/00100/00010",
        ['>'] = "01000/00100/00010/00001/00010/00100/01000",
        ['+'] = "00000/00100/00100/11111/00100/00100/00000",
        ['&'] = "01100/10010/10100/01000/10101/10010/01101",
        ['='] = "00000/00000/11111/00000/11111/00000/00000",
        ['\''] = "00100/00100/01000/00000/00000/00000/00000",
        ['('] = "00010/00100/01000/01000/01000/00100/00010",
        [')'] = "01000/00100/00010/00010/00010/00100/01000",
        ['%'] = "11001/11010/00010/00100/01000/01011/10011",
    };

    static readonly Dictionary<char, bool[,]> masks = new Dictionary<char, bool[,]>();
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    static bool[,] Mask(char ch)
    {
        ch = char.ToUpperInvariant(ch);
        if (!Glyphs.ContainsKey(ch)) ch = '?';
        if (!masks.TryGetValue(ch, out var m))
        {
            m = new bool[GlyphW, GlyphH];
            string[] rows = Glyphs[ch].Split('/');
            for (int y = 0; y < GlyphH; y++)
                for (int x = 0; x < GlyphW; x++)
                    m[x, y] = rows[y][x] == '1';
            masks[ch] = m;
        }
        return m;
    }

    public static int LineWidth(string line) => line.Length == 0 ? 0 : line.Length * (GlyphW + 1) - 1;

    // ---------- small UI text ----------

    public static Texture2D Render(string text, Color fill, Color outline, bool shadow = true)
    {
        string key = text + "|" + ColorUtility.ToHtmlStringRGB(fill) + "|" + ColorUtility.ToHtmlStringRGB(outline) + "|" + shadow;
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        if (cache.Count > 300)
        {
            foreach (var t in cache.Values) if (t != null) Object.Destroy(t);
            cache.Clear();
        }

        string[] lines = text.Split('\n');
        int lineStep = GlyphH + 2;
        int maxW = 0;
        foreach (var l in lines) maxW = Mathf.Max(maxW, LineWidth(l));
        int w = maxW + 4, h = lines.Length * lineStep - 2 + 4;

        var grid = new Color[w, h];
        var filled = new bool[w, h];
        for (int li = 0; li < lines.Length; li++)
        {
            int x0 = 2 + (maxW - LineWidth(lines[li])) / 2;
            int y0 = 2 + li * lineStep;
            for (int ci = 0; ci < lines[li].Length; ci++)
            {
                var m = Mask(lines[li][ci]);
                for (int gy = 0; gy < GlyphH; gy++)
                {
                    for (int gx = 0; gx < GlyphW; gx++)
                    {
                        if (!m[gx, gy]) continue;
                        int px = x0 + ci * (GlyphW + 1) + gx, py = y0 + gy;
                        float shade = gy <= 1 ? 1.16f : gy >= 6 ? 0.78f : 1f;
                        Color c = fill * shade;
                        c.a = 1f;
                        grid[px, py] = c;
                        filled[px, py] = true;
                    }
                }
            }
        }

        var result = new Color[w, h];
        var outlined = new bool[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (filled[x, y]) { result[x, y] = grid[x, y]; continue; }
                for (int dy = -1; dy <= 1 && !outlined[x, y]; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (x + dx >= 0 && y + dy >= 0 && x + dx < w && y + dy < h && filled[x + dx, y + dy]) { outlined[x, y] = true; break; }
                if (outlined[x, y]) result[x, y] = outline;
            }
        }
        if (shadow)
        {
            for (int y = 1; y < h; y++)
                for (int x = 1; x < w; x++)
                    if (!filled[x, y] && !outlined[x, y] && (filled[x - 1, y - 1] || outlined[x - 1, y - 1]))
                        result[x, y] = new Color(0f, 0f, 0f, 0.4f);
        }

        var tex = ToTexture(result, w, h);
        cache[key] = tex;
        return tex;
    }

    static Texture2D ToTexture(Color[,] topDown, int w, int h)
    {
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = topDown[x, y];
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ---------- chunky 3D logo ----------

    public static Texture2D RenderLogo(string[] lines, Color[] letterColors)
    {
        const int block = 6, extrude = 5, pad = 8, lineGap = 2;
        int maxFp = 0;
        foreach (var l in lines) maxFp = Mathf.Max(maxFp, LineWidth(l));
        int fpHeight = lines.Length * GlyphH + (lines.Length - 1) * lineGap;
        int w = maxFp * block + extrude + pad * 2, h = fpHeight * block + extrude + pad * 2;

        var fpColor = new Color[maxFp, fpHeight];
        var fpOn = new bool[maxFp, fpHeight];
        int letterIndex = 0;
        for (int li = 0; li < lines.Length; li++)
        {
            int x0 = (maxFp - LineWidth(lines[li])) / 2;
            int y0 = li * (GlyphH + lineGap);
            for (int ci = 0; ci < lines[li].Length; ci++)
            {
                if (lines[li][ci] != ' ') letterIndex++;
                var m = Mask(lines[li][ci]);
                Color lc = letterColors[Mathf.Max(0, letterIndex - 1) % letterColors.Length];
                for (int gy = 0; gy < GlyphH; gy++)
                    for (int gx = 0; gx < GlyphW; gx++)
                        if (m[gx, gy])
                        {
                            fpOn[x0 + ci * (GlyphW + 1) + gx, y0 + gy] = true;
                            fpColor[x0 + ci * (GlyphW + 1) + gx, y0 + gy] = lc;
                        }
            }
        }

        bool On(int x, int y) => x >= 0 && y >= 0 && x < maxFp && y < fpHeight && fpOn[x, y];

        var face = new Color[w, h];
        var faceOn = new bool[w, h];
        for (int fy = 0; fy < fpHeight; fy++)
        {
            for (int fx = 0; fx < maxFp; fx++)
            {
                if (!fpOn[fx, fy]) continue;
                for (int by = 0; by < block; by++)
                {
                    for (int bx = 0; bx < block; bx++)
                    {
                        Color c = fpColor[fx, fy];
                        float vgrad = Mathf.Lerp(1.12f, 0.9f, (fy % (GlyphH + lineGap)) / (float)GlyphH);
                        c *= vgrad;
                        if (by == 0 && !On(fx, fy - 1)) c *= 1.35f;
                        else if (bx == 0 && !On(fx - 1, fy)) c *= 1.18f;
                        if (by == block - 1 && !On(fx, fy + 1)) c *= 0.74f;
                        else if (bx == block - 1 && !On(fx + 1, fy)) c *= 0.84f;
                        c.a = 1f;
                        int ax = pad + fx * block + bx, ay = pad + fy * block + by;
                        face[ax, ay] = c;
                        faceOn[ax, ay] = true;
                    }
                }
            }
        }

        var layered = new Color[w, h];
        var layeredOn = new bool[w, h];
        for (int d = extrude; d >= 1; d--)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (faceOn[x, y] && x + d < w && y + d < h)
                    {
                        Color c = face[x, y] * 0.42f;
                        c.a = 1f;
                        layered[x + d, y + d] = c;
                        layeredOn[x + d, y + d] = true;
                    }
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (faceOn[x, y]) { layered[x, y] = face[x, y]; layeredOn[x, y] = true; }

        var final = new Color[w, h];
        Color outline = new Color32(34, 14, 58, 255);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (layeredOn[x, y]) { final[x, y] = layered[x, y]; continue; }
                bool near = false;
                for (int dy = -2; dy <= 2 && !near; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        if (dx * dx + dy * dy <= 5 && x + dx >= 0 && y + dy >= 0 && x + dx < w && y + dy < h && layeredOn[x + dx, y + dy]) { near = true; break; }
                if (near) final[x, y] = outline;
            }
        }
        return ToTexture(final, w, h);
    }
}

// Immediate-mode drawing helpers for OnGUI built on top of PixelFont.
public static class UiKit
{
    static Texture2D panelTex;
    static Texture2D buttonTex;
    const int Border = 9;

    public static readonly Color Ink = new Color32(30, 16, 56, 255);

    public static int K(float u, float factor) => Mathf.Max(1, Mathf.RoundToInt(u * factor));

    public static Vector2 TextSize(string text, int k)
    {
        string[] lines = text.Split('\n');
        int maxW = 0;
        foreach (var l in lines) maxW = Mathf.Max(maxW, PixelFont.LineWidth(l));
        return new Vector2((maxW + 4) * k, (lines.Length * (PixelFont.GlyphH + 2) - 2 + 4) * k);
    }

    public static void Text(Rect area, string text, int k, Color fill, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var tex = PixelFont.Render(text, fill, Ink);
        while (k > 1 && tex.width * k > area.width) k--;
        float w = tex.width * k, h = tex.height * k;
        float x = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.LowerLeft ? area.x
                : anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight ? area.xMax - w
                : area.center.x - w * 0.5f;
        float y = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.UpperCenter || anchor == TextAnchor.UpperRight ? area.y
                : anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight ? area.yMax - h
                : area.center.y - h * 0.5f;
        UiCanvas.Draw(new Rect(Mathf.Round(x), Mathf.Round(y), w, h), tex, new Rect(0, 0, 1, 1), Color.white);
    }

    public static void Sprite(Rect rect, Sprite sprite)
    {
        Rect r = sprite.textureRect;
        var tex = sprite.texture;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        UiCanvas.Draw(rect, tex, uv, Color.white);
    }

    public static void Quad(Rect rect, Color color)
    {
        UiCanvas.Draw(rect, Texture2D.whiteTexture, new Rect(0, 0, 1, 1), color);
    }

    public static void Image(Rect rect, Texture2D tex)
    {
        UiCanvas.Draw(rect, tex, new Rect(0, 0, 1, 1), Color.white);
    }

    public static void SpriteAt(Vector2 bottomCenter, Sprite sprite, float scale)
    {
        Rect r = sprite.textureRect;
        float w = r.width * scale, h = r.height * scale;
        Sprite(new Rect(bottomCenter.x - w * 0.5f, bottomCenter.y - h, w, h), sprite);
    }

    public static void Panel(Rect rect, int k, Color tint)
    {
        if (panelTex == null) panelTex = BuildPanel(new Color32(70, 60, 150, 255));
        NineSlice(rect, panelTex, k, tint);
    }

    public static void Button(Rect rect, string label, int panelK, int textK, Color tint, bool pressed)
    {
        if (buttonTex == null) buttonTex = BuildPanel(new Color32(236, 236, 240, 255));
        float lip = panelK * 3f;
        Rect face = pressed
            ? new Rect(rect.x, rect.y + lip, rect.width, rect.height - lip)
            : new Rect(rect.x, rect.y, rect.width, rect.height - lip);
        if (!pressed)
        {
            var dark = tint * 0.55f;
            dark.a = tint.a;
            NineSlice(new Rect(rect.x, rect.y + lip, rect.width, rect.height - lip), buttonTex, panelK, dark);
        }
        NineSlice(face, buttonTex, panelK, tint);
        Text(face, label, textK, Color.white);
    }

    static Texture2D BuildPanel(Color baseColor)
    {
        var p = new PixelPainter(24, 24);
        p.RoundRect(0, 0, 24, 24, 8f, baseColor, 0.55f);
        return p.Bake(true, 1);
    }

    static void NineSlice(Rect r, Texture2D tex, int k, Color tint)
    {
        float b = Border * k;
        float cw = Mathf.Max(0f, r.width - 2 * b), ch = Mathf.Max(0f, r.height - 2 * b);
        float t = Border / (float)tex.width, mid = 1f - 2f * t;
        void Part(float x, float y, float w, float h, float u, float v, float uw, float vh) =>
            UiCanvas.Draw(new Rect(x, y, w, h), tex, new Rect(u, v, uw, vh), tint);

        Part(r.x, r.y, b, b, 0, 1 - t, t, t);
        Part(r.x + b, r.y, cw, b, t, 1 - t, mid, t);
        Part(r.xMax - b, r.y, b, b, 1 - t, 1 - t, t, t);
        Part(r.x, r.y + b, b, ch, 0, t, t, mid);
        Part(r.x + b, r.y + b, cw, ch, t, t, mid, mid);
        Part(r.xMax - b, r.y + b, b, ch, 1 - t, t, t, mid);
        Part(r.x, r.yMax - b, b, b, 0, 0, t, t);
        Part(r.x + b, r.yMax - b, cw, b, t, 0, mid, t);
        Part(r.xMax - b, r.yMax - b, b, b, 1 - t, 0, t, t);
    }
}

// Immediate-mode drawing on top of a screen-space uGUI canvas. Rects use top-left screen pixels.
public class UiCanvas : MonoBehaviour
{
    static UiCanvas instance;
    readonly System.Collections.Generic.List<UnityEngine.UI.RawImage> pool = new System.Collections.Generic.List<UnityEngine.UI.RawImage>();
    int used;

    static UiCanvas Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("UI Canvas");
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                instance = go.AddComponent<UiCanvas>();
            }
            return instance;
        }
    }

    public static void Begin() => Instance.used = 0;

    public static void End()
    {
        var u = Instance;
        for (int i = u.used; i < u.pool.Count; i++)
            if (u.pool[i].gameObject.activeSelf) u.pool[i].gameObject.SetActive(false);
    }

    public static void Draw(Rect rect, Texture tex, Rect uv, Color color)
    {
        var u = Instance;
        if (u.used >= u.pool.Count)
        {
            var go = new GameObject("img");
            go.transform.SetParent(u.transform, false);
            var img = go.AddComponent<UnityEngine.UI.RawImage>();
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            u.pool.Add(img);
        }

        var raw = u.pool[u.used++];
        if (!raw.gameObject.activeSelf) raw.gameObject.SetActive(true);
        raw.texture = tex;
        raw.uvRect = uv;
        raw.color = color;
        raw.rectTransform.anchoredPosition = new Vector2(rect.x, Screen.height - rect.yMax);
        raw.rectTransform.sizeDelta = new Vector2(rect.width, rect.height);
    }
}
