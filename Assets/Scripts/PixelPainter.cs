using UnityEngine;

/// <summary>
/// Draws a pixel-art sprite from simple shapes (ellipses, rounded rectangles, single pixels) and then LIGHTS it
/// like a 3D object, which is what gives the characters and props their shaded, slightly 3D look without hand-drawing
/// every shade.
///
/// How it works:
///  1. Every shape gives each pixel it covers a base color and a surface NORMAL (the direction that pixel faces).
///     An ellipse behaves like a squashed sphere, so pixels near its edge face sideways and pixels in the middle
///     face the viewer; a rounded rectangle bevels its edges the same way.
///  2. When the sprite is baked, each pixel's normal is compared with one fixed light direction coming from the
///     top-left (<see cref="Light"/>). The brightness is then snapped to four bands (deep shadow, shade, base,
///     highlight) instead of being smooth, which is what keeps it looking like pixel art rather than a soft gradient.
///     Shadows are tinted slightly purple and highlights slightly warm, as is common in pixel art.
///  3. Optional "specular" shines add a white glint where the surface faces the light and the viewer (eyes, coins, gems).
///  4. A dark outline is drawn around the silhouette.
///
/// Coordinates are in pixels with the origin at the TOP-left and y growing DOWN (easier to author than Unity's y-up);
/// <see cref="Bake"/> flips the image into Unity's texture layout. Shapes drawn later paint over earlier ones, so
/// draw back-to-front.
///
/// Example: <c>var p = new PixelPainter(16, 16); p.Ellipse(8, 8, 6, 6, Color.red); Texture2D tex = p.Bake();</c>
/// </summary>
public class PixelPainter
{
    // One painted pixel: whether it is covered, its base color, its surface normal and how shiny it is.
    struct Cell
    {
        public bool on;
        public Color col;
        public Vector3 n;
        public float spec;
    }

    // Direction TOWARD the light, in painter coordinates (x right, y down, z toward the viewer).
    // Negative x and y mean the light comes from the upper left; positive z means it also shines toward the viewer.
    static readonly Vector3 Light = new Vector3(-0.55f, -0.65f, 0.55f).normalized;

    // Halfway between the light direction and the viewer direction; surfaces whose normal lines up with it
    // reflect the light straight into the viewer's eye, which is where the specular glint appears.
    static readonly Vector3 HalfVector = (Light + Vector3.forward).normalized;

    /// <summary>Canvas width and height in pixels (before the outline padding added by <see cref="Bake"/>).</summary>
    public readonly int W, H;

    /// <summary>Color mixed into the silhouette outline (a very dark purple by default).</summary>
    public Color OutlineColor = new Color32(26, 16, 44, 255);
    readonly Cell[] cells;

    public PixelPainter(int w, int h)
    {
        W = w;
        H = h;
        cells = new Cell[w * h];
    }

    /// <summary>
    /// Paints one pixel with an explicit surface normal. The other shape methods are built on this;
    /// use it directly for special surfaces (the waving flag cloth, crystal facets, the heart).
    /// Out-of-range coordinates are ignored.
    /// </summary>
    /// <param name="normal">Surface direction (x right, y down, z toward the viewer); it is normalised for you.</param>
    /// <param name="spec">0 = matte; up to 1 = strong white glint.</param>
    public void Custom(int x, int y, Color c, Vector3 normal, float spec = 0f)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        cells[y * W + x] = new Cell { on = true, col = c, n = normal.normalized, spec = spec };
    }

    /// <summary>Paints one flat pixel (facing the viewer).</summary>
    public void Pixel(int x, int y, Color c, float spec = 0f) => Custom(x, y, c, Vector3.forward, spec);

    /// <summary>Paints a flat rectangle (no bevel): good for belts, mouths, eyebrows.</summary>
    public void Rect(int x, int y, int w, int h, Color c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                Custom(i, j, c, Vector3.forward);
    }

    /// <summary>
    /// Paints a shaded ellipse that looks like a ball or dome.
    /// </summary>
    /// <param name="cx">Centre x in pixels.</param>
    /// <param name="cy">Centre y in pixels.</param>
    /// <param name="rx">Horizontal radius.</param>
    /// <param name="ry">Vertical radius.</param>
    /// <param name="bulge">How round it looks: 0 is flat, 1 is a full sphere, 0.3 to 0.5 is a gently curved plate.</param>
    /// <param name="spec">Glint strength (0 to 1).</param>
    public void Ellipse(float cx, float cy, float rx, float ry, Color c, float bulge = 1f, float spec = 0f)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
        {
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                // Position inside the ellipse in the range -1..1 on each axis, sampled at the pixel centre.
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float r2 = dx * dx + dy * dy;
                if (r2 > 1f) continue;

                // For a sphere the normal at (dx, dy) is (dx, dy, sqrt(1 - r^2)). 'bulge' scales the sideways part;
                // the z part has a floor of sqrt(0.12) so the very rim does not become fully edge-on and black.
                var n = new Vector3(dx * bulge, dy * bulge, Mathf.Sqrt(Mathf.Max(0.12f, 1f - r2)));
                Custom(x, y, c, n, spec);
            }
        }
    }

    /// <summary>
    /// Paints a rounded rectangle with bevelled (rounded-over) edges: limbs, torsos, bricks, panels, poles.
    /// </summary>
    /// <param name="x0">Left edge.</param>
    /// <param name="y0">Top edge.</param>
    /// <param name="x1">Right edge.</param>
    /// <param name="y1">Bottom edge.</param>
    /// <param name="r">Corner radius, which is also the width of the bevelled edge.</param>
    /// <param name="bulge">Edge steepness: 0 is flat, 1 is strongly rounded.</param>
    public void RoundRect(float x0, float y0, float x1, float y1, float r, Color c, float bulge = 0.8f, float spec = 0f)
    {
        r = Mathf.Max(0.001f, r);
        for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
        {
            for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                if (px < x0 || px > x1 || py < y0 || py > y1) continue;

                // Cut the corners: a pixel belongs to the shape only if its distance to the inner rectangle is within r.
                float cx = Mathf.Clamp(px, x0 + r, x1 - r), cy = Mathf.Clamp(py, y0 + r, y1 - r);
                if (Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) > r) continue;

                // How deep into each bevelled edge this pixel is (0 in the flat middle, up to 1 right at the edge).
                float left = 1f - Mathf.Clamp01((px - x0) / r), right = 1f - Mathf.Clamp01((x1 - px) / r);
                float top = 1f - Mathf.Clamp01((py - y0) / r), bottom = 1f - Mathf.Clamp01((y1 - py) / r);

                // Edges tilt the normal outward; the middle of the face points at the viewer.
                var n = new Vector3((right - left) * bulge, (bottom - top) * bulge, 1f);
                Custom(x, y, c, n, spec);
            }
        }
    }

    /// <summary>
    /// Lights every painted pixel, adds the outline, and returns the finished texture
    /// (Point filtering and Clamp wrapping, so pixels stay sharp).
    /// </summary>
    /// <param name="outline">Draw the dark silhouette outline.</param>
    /// <param name="pad">Transparent border added on every side so the outline has room (set 0 with outline false).</param>
    /// <param name="linear">Mark the texture as linear (non-sRGB) data. Currently unused; the UI once needed it.</param>
    public Texture2D Bake(bool outline = true, int pad = 1, bool linear = false)
    {
        int tw = W + pad * 2, th = H + pad * 2;
        var px = new Color32[tw * th];
        var shaded = new Color[W * H];

        // Step 1: light every covered pixel.
        for (int i = 0; i < cells.Length; i++)
            if (cells[i].on) shaded[i] = Shade(cells[i]);

        // Step 2: compose the final image including the outline.
        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                int sx = x - pad, sy = y - pad; // position in the painter's coordinates
                Color result = Color.clear;
                if (sx >= 0 && sy >= 0 && sx < W && sy < H && cells[sy * W + sx].on)
                {
                    result = shaded[sy * W + sx];
                }
                else if (outline)
                {
                    // An empty pixel touching a covered one (up, down, left or right) becomes outline.
                    Color neighbor = default;
                    bool found = false;
                    for (int d = 0; d < 4 && !found; d++)
                    {
                        int nx = sx + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = sy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx >= 0 && ny >= 0 && nx < W && ny < H && cells[ny * W + nx].on)
                        {
                            neighbor = shaded[ny * W + nx];
                            found = true;
                        }
                    }
                    if (found)
                    {
                        // Outline = a very dark version of the neighbouring color blended with the fixed outline color,
                        // so a red shape gets a dark-red edge rather than a harsh black one.
                        result = Color.Lerp(neighbor * 0.28f, OutlineColor, 0.55f);
                        result.a = 1f;
                    }
                }
                // Texture rows run bottom-to-top; the painter runs top-to-bottom.
                px[(th - 1 - y) * tw + x] = result;
            }
        }

        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false, linear) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Computes the final color of one pixel from its base color, normal and shininess.
    static Color Shade(Cell c)
    {
        // How directly the surface faces the light: 1 = straight at it, 0 or less = turned away.
        float d = Vector3.Dot(c.n, Light);

        // Snap to four tone bands. A flat surface (normal straight at the viewer) scores about 0.54, which lands in
        // band 2 (the unchanged "base" color); tilting toward the light gives highlights, away gives shade.
        int tone = d > 0.88f ? 3 : d > 0.5f ? 2 : d > 0.22f ? 1 : 0;

        // Brightness multiplier per band: deep shadow, shade, base, highlight.
        float[] mult = { 0.58f, 0.8f, 1f, 1.18f };
        Color col = c.col * mult[tone];

        // Pixel-art colour rule: shadows drift toward purple and highlights toward warm white (not just darker/lighter).
        if (tone < 2) col = Color.Lerp(col, new Color(0.22f, 0.16f, 0.42f), (2 - tone) * 0.08f);
        if (tone == 3) col = Color.Lerp(col, new Color(1f, 0.98f, 0.85f), 0.16f);

        // Specular glint where the surface faces halfway between the light and the viewer.
        if (c.spec > 0f && Vector3.Dot(c.n, HalfVector) > 0.965f) col = Color.Lerp(col, Color.white, c.spec);

        col.a = c.col.a; // multiplying by 'mult' scaled alpha too; restore it
        return col;
    }
}
