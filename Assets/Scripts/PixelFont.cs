using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A tiny built-in pixel font (5 x 7 pixel glyphs) plus a generator for the chunky 3D title logo.
/// There is no font file: every letter is stored as 7 rows of 5 bits and rendered to a texture on demand.
/// Text is therefore always crisp, always the same style on every device, and costs nothing to download.
///
/// Supported characters: A-Z (lowercase is converted to uppercase), 0-9, space and . , ! ? : - / &lt; &gt; + &amp; = ' ( ) %.
/// Anything else is drawn as a question mark. To add a character, add a line to <see cref="Glyphs"/>.
///
/// Layers in this file:
///  - <see cref="PixelFont"/>: turns a string into a texture (small outlined UI text, or the big 3D logo).
///  - <see cref="UiKit"/>: immediate-mode helpers to draw text, sprites, panels and buttons at integer pixel scales.
///  - <see cref="UiCanvas"/>: the screen-space canvas that the helpers draw on.
/// </summary>
public static class PixelFont
{
    /// <summary>Glyph width in pixels.</summary>
    public const int GlyphW = 5;

    /// <summary>Glyph height in pixels.</summary>
    public const int GlyphH = 7;

    // Each glyph is seven rows separated by '/', each row five characters of '1' (pixel) or '0' (empty).
    // Example, the letter A:   01110 / 10001 / 10001 / 11111 / 10001 / 10001 / 10001
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

    // Parsed glyph bitmaps, created on first use.
    static readonly Dictionary<char, bool[,]> masks = new Dictionary<char, bool[,]>();

    // Rendered text textures, keyed by text + colors + shadow flag, so the same string is only drawn once.
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    // Returns the bitmap of a character (indexed [x, y], y = 0 at the top), parsing and caching it on first use.
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

    /// <summary>
    /// Width in glyph pixels of one line of text: 5 pixels per character plus 1 pixel of spacing between characters.
    /// </summary>
    public static int LineWidth(string line) => line.Length == 0 ? 0 : line.Length * (GlyphW + 1) - 1;

    // ---------- small UI text ----------

    /// <summary>
    /// Renders (and caches) a piece of text as a texture, one texture pixel per glyph pixel.
    /// The texture includes a dark outline and an optional soft drop shadow, so it stays readable on any background.
    /// The text may contain '\n' for several lines; lines are centred on each other.
    /// Draw it scaled by a whole number (see <see cref="UiKit.Text"/>) to keep the pixels crisp.
    /// </summary>
    /// <param name="text">The text (case-insensitive).</param>
    /// <param name="fill">Letter color.</param>
    /// <param name="outline">Color of the 1-pixel outline around the letters.</param>
    /// <param name="shadow">Add a translucent drop shadow one pixel down and to the right.</param>
    public static Texture2D Render(string text, Color fill, Color outline, bool shadow = true)
    {
        string key = text + "|" + ColorUtility.ToHtmlStringRGB(fill) + "|" + ColorUtility.ToHtmlStringRGB(outline) + "|" + shadow;
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        // Counters that change often (coins, lives) would otherwise fill memory with one texture per value.
        // When the cache grows past 300 entries, throw everything away and start over.
        if (cache.Count > 300)
        {
            foreach (var t in cache.Values) if (t != null) Object.Destroy(t);
            cache.Clear();
        }

        // Layout: each line is 7 pixels tall with 2 pixels between lines; 2 pixels of margin all around
        // leave room for the outline and shadow.
        string[] lines = text.Split('\n');
        int lineStep = GlyphH + 2;
        int maxW = 0;
        foreach (var l in lines) maxW = Mathf.Max(maxW, LineWidth(l));
        int w = maxW + 4, h = lines.Length * lineStep - 2 + 4;

        // Step 1: draw the glyph pixels. The top two rows are lightened and the bottom row darkened
        // to give each letter a little 3D shading.
        var grid = new Color[w, h];
        var filled = new bool[w, h];
        for (int li = 0; li < lines.Length; li++)
        {
            int x0 = 2 + (maxW - LineWidth(lines[li])) / 2; // centre this line
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

        // Step 2: outline = any empty pixel with a letter pixel among its 8 neighbours.
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

        // Step 3: drop shadow = an empty pixel whose up-left neighbour is a letter or outline pixel.
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

    // Converts a top-down [x, y] color grid into a Point-filtered texture (flipping rows into Unity's bottom-up layout).
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

    /// <summary>
    /// Renders large chunky 3D lettering for the title logo, for example "PIXEL" over "HOPPER".
    ///
    /// Each font pixel becomes a 6 x 6 block. A block is lightened along an edge only where its neighbouring font pixel
    /// is empty (a bevel on the silhouette, not a grid on every block). The letters are then extruded 5 pixels down and
    /// to the right in a much darker shade of their color (that is the 3D depth), and finally a dark outline about 2
    /// pixels thick is drawn around the whole shape.
    /// </summary>
    /// <param name="lines">Lines of text, centred on each other.</param>
    /// <param name="letterColors">One color per non-space letter, cycled if there are fewer colors than letters.</param>
    /// <returns>A Point-filtered texture; draw it at a whole-number scale.</returns>
    public static Texture2D RenderLogo(string[] lines, Color[] letterColors)
    {
        const int block = 6, extrude = 5, pad = 8, lineGap = 2;
        int maxFp = 0;
        foreach (var l in lines) maxFp = Mathf.Max(maxFp, LineWidth(l));
        int fpHeight = lines.Length * GlyphH + (lines.Length - 1) * lineGap;
        int w = maxFp * block + extrude + pad * 2, h = fpHeight * block + extrude + pad * 2;

        // Step 1: lay the letters out on a grid of "font pixels", remembering which pixel belongs to which letter color.
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

        // Step 2: draw the front face. Each font pixel becomes a block; edges on the silhouette get a highlight
        // (top, left) or a shadow (bottom, right), and a gentle top-to-bottom gradient runs through each line of letters.
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

        // Step 3: extrusion. Copy the face down-right by 1..5 pixels in 42% brightness, farthest copy first so the
        // nearer (and later) copies overwrite it; then draw the face itself on top.
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

        // Step 4: outline. Any empty pixel within a radius of about 2 pixels (dx*dx + dy*dy <= 5) of the shape.
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

/// <summary>
/// Immediate-mode drawing helpers for the game's user interface, built on <see cref="PixelFont"/> and <see cref="UiCanvas"/>.
/// "Immediate mode" means the UI is redrawn from scratch every frame by calling these methods (Game.DrawUi does this);
/// there are no persistent UI objects to create, position or update.
///
/// All rectangles are in screen pixels with the origin at the TOP-LEFT of the screen (so y grows downward).
/// Pixel scale: text and panels are drawn at whole-number scales ("k") so each art pixel becomes a k x k block of
/// screen pixels. <see cref="K"/> turns a size relative to screen height into such a scale.
/// </summary>
public static class UiKit
{
    static Texture2D panelTex;
    static Texture2D buttonTex;

    // Width in pixels of the corner/edge border of the panel textures (the 9-slice margin).
    const int Border = 9;

    /// <summary>Dark purple used as the text outline and as general "ink".</summary>
    public static readonly Color Ink = new Color32(30, 16, 56, 255);

    /// <summary>
    /// Converts a size relative to the screen into a whole-number pixel scale of at least 1.
    /// Typical call: <c>K(u, 0.3f)</c> where u is one percent of the playfield height.
    /// </summary>
    public static int K(float u, float factor) => Mathf.Max(1, Mathf.RoundToInt(u * factor));

    /// <summary>
    /// Size in screen pixels that <paramref name="text"/> will occupy at pixel scale <paramref name="k"/>
    /// (including its outline margin). Use it to right-align text or to place an icon next to it.
    /// </summary>
    public static Vector2 TextSize(string text, int k)
    {
        string[] lines = text.Split('\n');
        int maxW = 0;
        foreach (var l in lines) maxW = Mathf.Max(maxW, PixelFont.LineWidth(l));
        return new Vector2((maxW + 4) * k, (lines.Length * (PixelFont.GlyphH + 2) - 2 + 4) * k);
    }

    /// <summary>
    /// Draws text inside <paramref name="area"/>.
    /// If the text would be wider than the area at scale k, the scale is reduced step by step until it fits,
    /// so labels never spill out of panels on small screens.
    /// </summary>
    /// <param name="area">Box to place the text in.</param>
    /// <param name="k">Desired pixel scale.</param>
    /// <param name="fill">Letter color.</param>
    /// <param name="anchor">Where in the box the text sits (default: centred).</param>
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

    /// <summary>
    /// Draws a sprite from the sprite atlas into <paramref name="rect"/>.
    /// The sprite is a region of the shared atlas texture, so only that region's UV rectangle is drawn.
    /// </summary>
    public static void Sprite(Rect rect, Sprite sprite)
    {
        Rect r = sprite.textureRect;
        var tex = sprite.texture;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        UiCanvas.Draw(rect, tex, uv, Color.white);
    }

    /// <summary>Fills a rectangle with a flat color (used for dimming overlays and touch buttons).</summary>
    public static void Quad(Rect rect, Color color)
    {
        UiCanvas.Draw(rect, Texture2D.whiteTexture, new Rect(0, 0, 1, 1), color);
    }

    /// <summary>Draws a whole texture into a rectangle (used for the logo).</summary>
    public static void Image(Rect rect, Texture2D tex)
    {
        UiCanvas.Draw(rect, tex, new Rect(0, 0, 1, 1), Color.white);
    }

    /// <summary>
    /// Draws a sprite standing on a point: its bottom-centre is placed at <paramref name="bottomCenter"/>,
    /// scaled by <paramref name="scale"/> screen pixels per sprite pixel. Used for the characters on the title screen.
    /// </summary>
    public static void SpriteAt(Vector2 bottomCenter, Sprite sprite, float scale)
    {
        Rect r = sprite.textureRect;
        float w = r.width * scale, h = r.height * scale;
        Sprite(new Rect(bottomCenter.x - w * 0.5f, bottomCenter.y - h, w, h), sprite);
    }

    /// <summary>
    /// Draws a rounded, bevelled purple panel (the HUD bar and dialog backgrounds).
    /// </summary>
    /// <param name="k">Pixel scale of the border (1 is usually right).</param>
    /// <param name="tint">Multiplied with the panel color; use alpha below 1 for translucency.</param>
    public static void Panel(Rect rect, int k, Color tint)
    {
        if (panelTex == null) panelTex = BuildPanel(new Color32(70, 60, 150, 255));
        NineSlice(rect, panelTex, k, tint);
    }

    /// <summary>
    /// Draws a chunky 3D button with a label. When not pressed, a darker "lip" is drawn underneath so the button
    /// looks raised; when pressed, the face moves down onto the lip.
    /// Hit-testing is not done here (see Game.UpdateHelp), this only draws.
    /// </summary>
    /// <param name="panelK">Pixel scale of the button's border.</param>
    /// <param name="textK">Pixel scale of the label text.</param>
    /// <param name="tint">Button color (the base texture is near-white, so the tint decides the color).</param>
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

    // Builds the shared panel/button texture: a 24 x 24 bevelled rounded square with an outline (26 x 26 in total).
    static Texture2D BuildPanel(Color baseColor)
    {
        var p = new PixelPainter(24, 24);
        p.RoundRect(0, 0, 24, 24, 8f, baseColor, 0.55f);
        return p.Bake(true, 1);
    }

    // 9-slice drawing: the texture is cut into 3 x 3 parts. The four corners keep their size, the four edges stretch
    // in one direction, and the centre stretches in both, so a small texture can fill a rectangle of any size
    // without distorting its corners. (Drawn as nine quads on the UI canvas.)
    static void NineSlice(Rect r, Texture2D tex, int k, Color tint)
    {
        float b = Border * k;
        float cw = Mathf.Max(0f, r.width - 2 * b), ch = Mathf.Max(0f, r.height - 2 * b);
        float t = Border / (float)tex.width, mid = 1f - 2f * t; // border and middle as fractions of the texture
        void Part(float x, float y, float w, float h, float u, float v, float uw, float vh) =>
            UiCanvas.Draw(new Rect(x, y, w, h), tex, new Rect(u, v, uw, vh), tint);

        // Top row, middle row, bottom row (UV v runs bottom to top, so the top row uses the high v values).
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

/// <summary>
/// The screen-space canvas that all UI is drawn on, wrapped in a tiny immediate-mode API.
///
/// Usage each frame: <c>UiCanvas.Begin();</c> then any number of <c>UiCanvas.Draw(...)</c> calls (directly, or through
/// <see cref="UiKit"/>), then <c>UiCanvas.End();</c>. Behind the scenes a pool of uGUI RawImage objects is reused:
/// Begin resets the pool counter, each Draw takes the next RawImage (creating one if needed) and sets its texture,
/// UV rectangle, color and position, and End hides the images that were not used this frame. Images are drawn in the
/// order of the Draw calls, so later calls appear on top.
///
/// Why uGUI instead of Unity's old OnGUI/IMGUI system: this project uses the Linear color space, and IMGUI rendered
/// textures washed-out and too light there. A uGUI canvas goes through the normal color pipeline, so UI colors match the
/// sprites in the game world exactly.
/// </summary>
public class UiCanvas : MonoBehaviour
{
    static UiCanvas instance;
    readonly System.Collections.Generic.List<UnityEngine.UI.RawImage> pool = new System.Collections.Generic.List<UnityEngine.UI.RawImage>();
    int used;

    // Creates the canvas object the first time it is needed. ScreenSpaceOverlay draws on top of everything,
    // in screen pixels, regardless of cameras. Sorting order 100 keeps it above any other canvas.
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

    /// <summary>Starts a new frame of UI drawing (resets the image pool counter).</summary>
    public static void Begin() => Instance.used = 0;

    /// <summary>Ends the frame: hides every pooled image that was not used this frame.</summary>
    public static void End()
    {
        var u = Instance;
        for (int i = u.used; i < u.pool.Count; i++)
            if (u.pool[i].gameObject.activeSelf) u.pool[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Draws a textured rectangle.
    /// </summary>
    /// <param name="rect">Destination in screen pixels, origin at the top-left.</param>
    /// <param name="tex">Texture to draw (must have Point filtering to stay crisp).</param>
    /// <param name="uv">Which part of the texture to show, in 0..1 texture coordinates (v runs bottom to top).</param>
    /// <param name="color">Multiplied with the texture (use white for no change, alpha for transparency).</param>
    public static void Draw(Rect rect, Texture tex, Rect uv, Color color)
    {
        var u = Instance;
        if (u.used >= u.pool.Count)
        {
            // Grow the pool by one image. The RectTransform is anchored to the bottom-left of the screen with a
            // bottom-left pivot, so its position can be set directly in screen pixels.
            var go = new GameObject("img");
            go.transform.SetParent(u.transform, false);
            var img = go.AddComponent<UnityEngine.UI.RawImage>();
            img.raycastTarget = false; // purely visual; clicks are read from the input system in Game
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
        // Our rectangles use a top-left origin; the RectTransform uses bottom-left, so flip y.
        raw.rectTransform.anchoredPosition = new Vector2(rect.x, Screen.height - rect.yMax);
        raw.rectTransform.sizeDelta = new Vector2(rect.width, rect.height);
    }
}
