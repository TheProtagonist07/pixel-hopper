using UnityEngine;

// Draws a sprite from simple shapes, then lights it like a 3D object (top-left light, banded tones, outline).
// Coordinates are in pixels with the origin at the top-left (y grows downward).
public class PixelPainter
{
    struct Cell
    {
        public bool on;
        public Color col;
        public Vector3 n;
        public float spec;
    }

    static readonly Vector3 Light = new Vector3(-0.55f, -0.65f, 0.55f).normalized;
    static readonly Vector3 HalfVector = (Light + Vector3.forward).normalized;

    public readonly int W, H;
    public Color OutlineColor = new Color32(26, 16, 44, 255);
    readonly Cell[] cells;

    public PixelPainter(int w, int h)
    {
        W = w;
        H = h;
        cells = new Cell[w * h];
    }

    public void Custom(int x, int y, Color c, Vector3 normal, float spec = 0f)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        cells[y * W + x] = new Cell { on = true, col = c, n = normal.normalized, spec = spec };
    }

    public void Pixel(int x, int y, Color c, float spec = 0f) => Custom(x, y, c, Vector3.forward, spec);

    public void Rect(int x, int y, int w, int h, Color c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                Custom(i, j, c, Vector3.forward);
    }

    public void Ellipse(float cx, float cy, float rx, float ry, Color c, float bulge = 1f, float spec = 0f)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
        {
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float r2 = dx * dx + dy * dy;
                if (r2 > 1f) continue;
                var n = new Vector3(dx * bulge, dy * bulge, Mathf.Sqrt(Mathf.Max(0.12f, 1f - r2)));
                Custom(x, y, c, n, spec);
            }
        }
    }

    public void RoundRect(float x0, float y0, float x1, float y1, float r, Color c, float bulge = 0.8f, float spec = 0f)
    {
        r = Mathf.Max(0.001f, r);
        for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
        {
            for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                if (px < x0 || px > x1 || py < y0 || py > y1) continue;
                float cx = Mathf.Clamp(px, x0 + r, x1 - r), cy = Mathf.Clamp(py, y0 + r, y1 - r);
                if (Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) > r) continue;

                float left = 1f - Mathf.Clamp01((px - x0) / r), right = 1f - Mathf.Clamp01((x1 - px) / r);
                float top = 1f - Mathf.Clamp01((py - y0) / r), bottom = 1f - Mathf.Clamp01((y1 - py) / r);
                var n = new Vector3((right - left) * bulge, (bottom - top) * bulge, 1f);
                Custom(x, y, c, n, spec);
            }
        }
    }

    public Texture2D Bake(bool outline = true, int pad = 1, bool linear = false)
    {
        int tw = W + pad * 2, th = H + pad * 2;
        var px = new Color32[tw * th];
        var shaded = new Color[W * H];

        for (int i = 0; i < cells.Length; i++)
            if (cells[i].on) shaded[i] = Shade(cells[i]);

        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                int sx = x - pad, sy = y - pad;
                Color result = Color.clear;
                if (sx >= 0 && sy >= 0 && sx < W && sy < H && cells[sy * W + sx].on)
                {
                    result = shaded[sy * W + sx];
                }
                else if (outline)
                {
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

    static Color Shade(Cell c)
    {
        float d = Vector3.Dot(c.n, Light);
        int tone = d > 0.88f ? 3 : d > 0.5f ? 2 : d > 0.22f ? 1 : 0;
        float[] mult = { 0.58f, 0.8f, 1f, 1.18f };
        Color col = c.col * mult[tone];
        if (tone < 2) col = Color.Lerp(col, new Color(0.22f, 0.16f, 0.42f), (2 - tone) * 0.08f);
        if (tone == 3) col = Color.Lerp(col, new Color(1f, 0.98f, 0.85f), 0.16f);
        if (c.spec > 0f && Vector3.Dot(c.n, HalfVector) > 0.965f) col = Color.Lerp(col, Color.white, c.spec);
        col.a = c.col.a;
        return col;
    }
}
