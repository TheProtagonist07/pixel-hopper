# Art pipeline

The project contains **no image files**. All pictures are generated in code at startup (or in the editor, for promo images).
This page explains how, so you can change the look or add new art.

## Three ways pictures are made

| Technique | Used for | Code |
|---|---|---|
| **Shaded shapes** (`PixelPainter`) | hero, enemy, coin, flag, heart, props, sun, moon, UI panels | `PixelPainter.cs`, `PixelArt.cs` |
| **Per-pixel functions** | ground, dirt, brick, spikes, clouds, shadow, sky, scenery silhouettes, stars | `PixelArt.cs`, `Backdrop.cs` |
| **Font rendering** | text and the 3D logo | `PixelFont.cs` |

## PixelPainter: how flat shapes become shaded 3D-looking sprites

Hand-drawing every shade of every sprite is slow, so the painter does what a 3D renderer does, in miniature.

1. **Paint shapes with a normal.** `Ellipse`, `RoundRect`, `Rect`, `Pixel`, `Custom` set, for each covered pixel, a base
   colour and a **surface normal** (the direction the pixel faces). An ellipse acts like a squashed sphere: pixels in the
   middle face the viewer, pixels near the edge face sideways. A rounded rectangle bevels its edges the same way.
2. **Light it.** On `Bake()`, each normal is compared with one fixed light direction from the **top-left**. The result is snapped to
   **four tone bands** (deep shadow, shade, base, highlight) instead of a smooth gradient, which is what makes it read as
   pixel art. Shadows drift toward purple and highlights toward warm white (a classic pixel-art colour rule).
3. **Specular glints.** Pixels with `spec > 0` facing halfway between the light and the viewer get a white shine
   (eyes, coins, gems, the sun).
4. **Outline.** Every empty pixel next to a painted pixel becomes a dark outline, tinted by the neighbouring colour so a red
   shape gets a dark-red edge instead of a harsh black one.

Drawing order matters: later shapes paint over earlier ones, so draw back to front. Coordinates have the origin at the **top-left**,
y down (easier to author than Unity's y-up); `Bake` flips the result into Unity's layout.

```csharp
var p = new PixelPainter(16, 16);
p.Ellipse(8, 8, 6, 6, Color.red, bulge: 1f, spec: 0.4f);   // a shiny red ball
Texture2D tex = p.Bake();                                   // lit, outlined, point-filtered texture
```

Tuning knobs: the `Light` and `HalfVector` constants (light direction), the tone thresholds and multipliers in `Shade`,
and `OutlineColor`.

## PixelArt: the sprite library and atlas

- Sprites are generated the first time any is requested (`PixelArt.Get`), then **packed into one atlas texture**
  (`Texture2D.PackTextures`, 2 px padding, max 2048 x 2048). One texture means few draw calls, which matters in WebGL and on phones
  (a stage has hundreds of tile sprites).
- **Scale:** 32 pixels = 1 world unit = 1 tile (`PixelArt.PPU`). The hero is about 26 x 36 pixels.
- **Pivots:** `Feet` (bottom centre, 1 px up so the outline row sits below the pivot) for things that stand on the ground;
  `Mid` for centred things.
- **Filtering:** point filtering everywhere, so pixels stay square. Never use bilinear for this art.
- The coin has 4 frames drawn at 100%, 72%, 20% and 72% width, which fakes rotation about the vertical axis.
- Theme `Tint` is multiplied onto tiles and props at runtime, so one grass sprite serves every world.

### Add a new sprite

1. Write a function returning a `Texture2D` (paint with `PixelPainter`, or fill pixels in a loop using `TopDown(w, h, f)`).
2. In `PixelArt.Build`, add it with `Add("my_key", texture, Feet(texture))` or `Mid(texture)`.
3. Expose it: `public static Sprite MySprite => Get("my_key");`.
4. Use it: `spriteRenderer.sprite = PixelArt.MySprite;`.

New scenery prop: add its name to the `foreach` list of decor kinds in `Build`, handle it in `DecorTexture(kind)`, and list
the name in a theme's `Decor` array.

## Backdrops

`Backdrop.cs` builds a stage's background from its `StageTheme`:

| Layer | How it is made |
|---|---|
| Sky | 8 x 128 gradient texture with 20 colour bands and **ordered (Bayer) dithering** between bands, stretched over the screen |
| Stars | 90 random stars in a 512 x 480 texture, tinted white/warm/cool; twinkle by alpha pulsing |
| Sun / moon | `PixelPainter` discs with translucent glow rings; the moon has craters |
| Scenery | Height profile per shape (hills, mountains, dunes, spires, volcano) turned into a lit silhouette; 512 px wide and **seamless**, drawn as three copies that follow the camera at a parallax rate |
| Clouds | 7 sprites with random size, height and drift; bigger clouds follow the camera more |
| Particles | snow, leaves, embers, fireflies, rain: a small fixed set that wraps around the camera |
| Lightning | A white full-screen quad that flickers in a double flash every 4 to 9 seconds (final stage) |

Parallax: a layer with parallax factor *p* moves at `1 - p` of the camera speed. Far layer p=0.16, near layer p=0.36, so both
follow the camera closely and read as distant. All positions are snapped to whole pixels.

The textures are **deterministic** (seeded), so a stage always looks the same.

## Colour and rendering settings that matter

- **Linear colour space** (Project Settings): colours in code (`new Color(...)`) are interpreted in sRGB-like terms by Unity's
  conversions. This is why IMGUI text looked washed out; the uGUI canvas path is colour-correct ([UI_SYSTEM.md](UI_SYSTEM.md)).
- **No anti-aliasing, no HDR, no post-processing** on the game camera (set in `Game.SetupCameras`): they would blur or cost speed.
- **Camera size** is exactly 15 tiles, and the camera snaps to 1/32 unit, so one art pixel is always a whole number of screen pixels
  at the same scale.

## Style rules to keep new art consistent

- Light comes from the top-left everywhere; do not hand-paint highlights on the bottom-right.
- Four tone bands per colour; avoid smooth gradients on sprites (gradients are only for sky and scenery, and they are dithered).
- Dark outline on characters and props; none on glow effects.
- Shadows lean purple, highlights lean warm.
- Keep the palette saturated; the original feedback was "vibrant and 3D", and dull colours looked wrong.
