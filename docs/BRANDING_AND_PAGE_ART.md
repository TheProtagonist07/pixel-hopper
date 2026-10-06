# Branding and page art

All marketing images are rendered by an **editor tool** (`Assets/Editor/CoverArt.cs`) from the game's own art code, so they match the game and can be
regenerated after any art change with one click. No image editor is involved.

## Running the tool

In the Unity menu bar: **Tools > Pixel Hopper**

| Menu item | Output | Folder |
|---|---|---|
| Generate Branding | `logo.png` (transparent), `cover_630x500.png`, `icon_512.png` | `branding/` |
| Generate Page Art | `page_background_2560x1100.png`, `banner_environment_1920x420.png` | `page/` |
| Generate Embed Background | `embed_cover_1080x1920.png` (2x of the 540 x 960 frame) | `page/` |

The same menu items can be run from an editor script with `EditorApplication.ExecuteMenuItem("Tools/Pixel Hopper/Generate Branding")`.

**Where files go:** `OutDir` in `CoverArt.cs` is `marketing/branding` at the project root (next to `Assets`), and page art goes to the sibling `marketing/page`.
Unity only imports `Assets`, `Packages` and `ProjectSettings`, so these images are not imported into the game, but they live in the same folder and Git repository as the code
(see [PROJECT_LAYOUT.md](PROJECT_LAYOUT.md)). The path is computed from the project folder, so renaming or moving the project does not break it.

The tool is wrapped in `#if UNITY_EDITOR`, so it is never part of a build.

## How it works

A tiny software canvas class (`CoverArt.Canvas`) composes pictures at the game's **native low resolution** (for example 315 x 250), one pixel at a time:

| Canvas method | Purpose |
|---|---|
| `Sky(top, bottom)` | Fill with the game's dithered sky gradient |
| `Blit(texture / sprite / pixels, x, y, scale, tint)` | Draw with alpha blending, whole-number scaling and an optional tint; clipped at the edges |
| `FillRect` | Solid rectangle |
| `FadeToSolid(rows, colour)` | Smoothly blend the bottom rows into a colour (used to melt the page background into the page) |
| `Upscale(factor)` | Enlarge by a whole number with nearest-neighbour, so pixels stay square |

Pictures are composed back to front (sky, sun, clouds, far scenery, near scenery, ground, props, characters, logo), then upscaled and saved as PNG.
They reuse the real game code: `Backdrop.MakeSky/MakeLayer/MakeStars/MakeBody`, `PixelArt` sprites, `PixelFont.RenderLogo`, `Levels.ThemeFor`.

## The images

| Image | Size | Used for | Description |
|---|---|---|---|
| `logo.png` | about 2x logo size | thumbnails, social | Rainbow 3D "PIXEL HOPPER" lettering, transparent |
| `cover_630x500.png` | 630 x 500 | itch.io cover | Sunny Meadow scene with hero, enemy, coins, logo at the top |
| `icon_512.png` | 512 x 512 | app icon, avatar | Hero in front of the sun on grass |
| `page_background_2560x1100.png` | 2560 x 1100 | itch page background | Purple dusk landscape with stars, sun and pines; fades into the page colour `#130a28` |
| `banner_environment_1920x420.png` | 1920 x 420 | itch page banner | Six of the game's worlds side by side, blended with dithering; **no title and no characters**, on purpose, so it contrasts with the game window |
| `embed_cover_1080x1920.png` | 1080 x 1920 | itch "Embed BG" launch screen | Portrait scene with logo; the middle band is left clear for itch's "Run game" button |

### Banner details

`RenderWorld` paints one world (sky, stars or sun/moon, cloud, two scenery layers, grass, one prop, one level element with coins) into its own canvas.
Six worlds (stages 1, 7, 3, 8, 5 and 9: meadow, desert, sunset, midnight, cave, lava) are rendered, and the banner takes a vertical slice of each.
Between slices there is a 20% blend zone resolved with **ordered dithering** (the same Bayer matrix as the sky), so one world melts into the next in a retro way. The last world shows the goal flag.

## Design rules for page art

- Brand colours: page background `#130a28`, secondary `#1c0f3d`, text `#ece6ff`, link/accent `#ffd84a`.
- The page should *frame* the game, not repeat it: environments and mood on the page, characters and logo inside the game window.
- Keep the embed cover at exactly 540 x 960 (or an exact 2x) so it fits the frame without cropping.
- Whole-number scales only. Never resize pixel art with smoothing.

## Editing tips

| I want to... | Do this |
|---|---|
| Change a cover scene | Edit the `Blit` coordinates in `Generate` (origin is bottom-left) |
| Show different worlds in the banner | Change the `worlds` array in `GeneratePageArt` (stage indices) |
| Use a different page colour | Change `solid` in `GeneratePageArt` and the itch theme colours together |
| Regenerate after changing art | Run the three menu items again, upload the new images to itch.io |
