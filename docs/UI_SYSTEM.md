# UI system

All on-screen UI (title, HUD, dialogs, touch buttons, logo) is drawn by code from three classes in
`Assets/Scripts/PixelFont.cs`. There are no UI prefabs, fonts or image files.

```
Game.DrawUi()          decides what to draw for the current state
   -> UiKit            helpers: Text, Sprite, Panel, Button, Quad, Image
        -> PixelFont   turns strings into pixel-font textures (and the 3D logo)
        -> UiCanvas    pooled RawImage objects on a screen-space canvas
```

## Immediate mode

"Immediate mode" means the UI is **redrawn from scratch every frame** by calling functions, like drawing on a canvas.
There are no persistent buttons to create, position or update. `Game.LateUpdate` does:

```csharp
UiCanvas.Begin();   // reset the pool counter
DrawUi();           // many UiKit calls, in back-to-front order
UiCanvas.End();     // hide pooled images that were not used this frame
```

Advantages: layout is plain arithmetic, state and UI cannot get out of sync, and nothing needs wiring in the editor.
Cost: a few dozen image updates per frame, which is negligible here.

Hit-testing is separate from drawing. `Game.HelpLayout` returns the dialog rectangles and is used by **both** drawing and
click detection, so the two can never disagree.

## PixelFont

- A built-in **5 x 7 pixel font**. Each glyph is seven rows of five bits stored as a string such as `01110/10001/...`.
- Characters: A-Z (lowercase is uppercased), 0-9, space and `. , ! ? : - / < > + & = ' ( ) %`. Anything else shows `?`.
- `Render(text, fill, outline, shadow)` returns a texture with one pixel per glyph pixel, a 1-pixel dark outline, an optional soft drop shadow,
  and slight shading (lighter top rows, darker bottom row). Results are **cached** by text and colours; the cache is flushed if it passes 300
  entries (changing counters would otherwise leak textures).
- `RenderLogo(lines, letterColors)` makes the big title lettering: each font pixel becomes a 6 x 6 block, edges get bevel highlights only where the
  neighbouring pixel is empty, the letters are extruded 5 pixels down-right in a much darker shade (the 3D depth), and a thick dark outline wraps it all.
  The same function produces the logo on the title screen and in the marketing images.

To add a character: add a line to the `Glyphs` dictionary.

## UiKit

All rectangles are screen pixels with the origin at the **top-left** (y grows downward).

| Method | Purpose |
|---|---|
| `K(u, factor)` | Convert a size relative to the screen into a whole-number pixel scale (minimum 1) |
| `TextSize(text, k)` | Pixel size the text will occupy at scale `k` |
| `Text(rect, text, k, colour, anchor)` | Draw text; **auto-shrinks** the scale while it would overflow the rectangle |
| `Sprite(rect, sprite)` | Draw an atlas sprite |
| `SpriteAt(bottomCentre, sprite, scale)` | Draw a sprite standing on a point (title mascots) |
| `Quad(rect, colour)` | Solid colour (dimming layers, touch buttons) |
| `Image(rect, texture)` | A whole texture (logo) |
| `Panel(rect, k, tint)` | Rounded bevelled panel (HUD bar, dialogs) |
| `Button(rect, label, panelK, textK, tint, pressed)` | Chunky 3D button; raised with a darker lip, or pressed down |

**Integer scaling:** text and panels use whole-number scales so each art pixel is a k x k block of screen pixels and never blurs.

**9-slice panels:** the panel texture is one small rounded square cut into 3 x 3 parts. The corners keep their size, the edges stretch
one way, the middle stretches both ways, so the panel can be any size without distorted corners.

## UiCanvas

A `ScreenSpaceOverlay` canvas (sorting order 100) created on first use. `Draw(rect, texture, uv, colour)` takes the next
`RawImage` from a **pool** (creating one if needed), sets its texture, UV rectangle, colour and position. Because images are used in call order,
later draws appear on top. `End()` hides the unused ones.

### Why uGUI and not IMGUI (`OnGUI`)?

The first version used `OnGUI`. In this project (Linear colour space) IMGUI textures came out **washed out and too light**, and did not match the sprites in the
world. Routing the UI through a normal canvas uses the standard colour pipeline, so UI and world colours match exactly. See [DECISIONS.md](DECISIONS.md).

## Layout rules (responsive by construction)

- The game occupies a **9:16 rectangle** (`Game.ViewRect`) centred in the window. UI is laid out inside that rectangle only.
- `u = ViewRect.height / 100` (one percent of the view height). All sizes are multiples of `u`, so the layout scales with any screen.
- Font scales come from `UiKit.K(u, factor)`; the factors were tuned by eye (0.2 for tiny hints up to 0.75 for the stage banner).
- Long labels never overflow: `Text` shrinks them, and the help dialog picks one shared text scale so the longest label fits.

## Screens

| Screen | Drawn by | Notes |
|---|---|---|
| Title | `DrawTitle` | Logo bobs with a sine wave; hero, enemy and coins animate with `Time.unscaledTime`; start prompt blinks twice per second |
| HUD | `DrawHud` | Hearts (lives) left, `STAGE n/10` centre, spinning coin and total right |
| Stage banner | `DrawStateOverlay` | `STAGE n` and the world name for 2.2 s |
| Stage clear | `DrawStateOverlay` | Panel with coins `x / y` |
| Game over / You win | `DrawStateOverlay` | Panel with a blinking prompt |
| Help dialog | `DrawHelp` | Key caps, icons, six rows, SKIP and OK buttons |
| Touch buttons | `DrawTouchButtons` | Translucent left, right and JUMP hints (only after a real touch, or on mobile) |

## Input for menus

- Confirm: Space, Enter, R, gamepad A or Start, mouse click, or a screen tap (`Game.ConfirmPressed`).
- Help dialog: Enter or Space = OK, Esc = skip forever, or click/tap the buttons.
- `H` opens help during play (the game pauses with `Time.timeScale = 0`), `M` toggles sound.
- Pointer positions are flipped from the Input System's bottom-left origin to the UI's top-left origin (`Game.PointerPressed`).

## Touch controls (gameplay)

Zones are relative to the visible 9:16 view, not the whole window, so black bars do not shift them:

| Zone | Action |
|---|---|
| Left quarter | move left |
| Second quarter | move right |
| Right half | jump (hold for a higher jump) |

Several touches work at once (hold right with one thumb, jump with the other). The touch UI only appears on mobile platforms or after a real touch, so a
laptop with a touchscreen keeps the keyboard hints.
