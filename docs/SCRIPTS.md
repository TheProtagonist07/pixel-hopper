# Script reference

One section per C# file in `Assets/`. Each script also begins with a summary comment and explains non-obvious
lines inline, so this page is the map and the scripts are the detail.

| File | Lines (approx.) | Role |
|---|---|---|
| [Game.cs](#gamecs) | 890 | Director: state machine, cameras, stage building, HUD and dialogs |
| [Levels.cs](#levelscs) | 500 | Stage generator, tile grid, 10 world themes |
| [PlayerController.cs](#playercontrollercs) | 400 | Hero movement, jump feel, stomp and hurt |
| [Enemy.cs](#enemycs) | 150 | Patrolling blob enemy |
| [Pickups.cs](#pickupscs) | 110 | Coin, spike hazard, level exit |
| [CameraFollow.cs](#camerafollowcs) | 110 | Look-ahead camera with shake and pixel snapping |
| [Backdrop.cs](#backdropcs) | 620 | Sky, parallax scenery, sun/moon, clouds, lightning, particles |
| [PixelPainter.cs](#pixelpaintercs) | 230 | Shape painter with 3D-style lighting |
| [PixelArt.cs](#pixelartcs) | 690 | Sprite library and atlas |
| [PixelFont.cs](#pixelfontcs) | 570 | Font, 3D logo, UI kit, UI canvas |
| [Sfx.cs](#sfxcs) | 200 | Synthesised sound effects and music |
| [Fx.cs](#fxcs) | 120 | Particle bursts (dust, sparkles, stomp puffs) |
| [TouchControls.cs](#touchcontrolscs) | 70 | Touch zones for phones |
| [AutoPlayer.cs](#autoplayercs-editor-only) | 150 | Editor-only test bot, screenshot and video helpers |
| [Editor/CoverArt.cs](#editorcoverartcs-editor-only) | 420 | Editor-only generator for logo, cover, page art |
| [Editor/BatchBot.cs](#editorbatchbotcs-editor-only) | 120 | Editor-only: runs the test bot and a stage report from the command line |
| [Editor/ReleaseBuild.cs](#editorreleasebuildcs-editor-only) | 80 | Editor-only: builds the WebGL release with the correct settings |
| [WebGLTemplates/PixelHopper/index.html](#webgltemplatespixelhopperindexhtml) | 110 | Web page that hosts the game |

---

## Game.cs
**Class `Game`** (MonoBehaviour, execution order -100). The only component placed in the scene by hand.

| Member | What it does |
|---|---|
| `Instance` | Static access to the one Game. |
| `KillY` | World y below which the player has fallen into a pit (-4). |
| `State` enum | Title, Help, Playing, StageClear, GameOver, Won. |
| `Awake` / `Start` | Create audio; create cameras, viewport and stage 1 as the title backdrop. |
| `SetupCameras` | Main camera setup (orthographic, 15 tiles tall) plus a black "bars" camera. |
| `UpdateViewport` | Fits a 9:16 rectangle in the window every frame and sets the camera rect. `ViewRect` exposes it. |
| `Update` | Mute key, stage-clear timer, help input, confirm input; bot loop in the editor. |
| `ConfirmPressed`, `PointerPressed` | Keyboard, gamepad, mouse and touch input for menus. |
| `BeginGame`, `OnConfirm`, `OpenHelp`, `CloseHelp`, `UpdateHelp` | Menu and dialog logic. |
| `StartLevel`, `BuildLevel` | Unload the old stage and build the new one from a `LevelGrid`. |
| `PlaceDecor` | Scatter bushes, cacti, crystals... with a seeded RNG and spacing rules. |
| `BuildSolids` | Merge solid cells into few rectangular colliders (greedy rectangle merge). |
| `AddCoin`, `PlayerDied`, `LevelComplete`, `AdvanceStage` | Game events called by other scripts. |
| `DrawUi` and the `Draw...` methods | Immediate-mode HUD, title, overlays, help dialog, touch buttons. |
| `HelpLayout` | Single source of truth for the help dialog rectangles (used by drawing and clicking). |
| `#if UNITY_EDITOR` members | `AutoPlay`, `DebugStartStage`, `DebugLog`, `Debug...` shortcuts for tests and screenshots. |

Depends on: `Levels`, `PlayerController`, `Backdrop`, `CameraFollow`, `UiKit`/`UiCanvas`, `Sfx`, `Fx`, `PixelArt`, `TouchControls`.

## Levels.cs
- **Enums** `LayerShape`, `AmbientKind`, `Celestial`: vocabulary for themes.
- **`StageTheme`**: data for one world (sky colors, layer colors and shapes, sun/moon, stars, particles, tints, lightning, decor list).
- **`LevelGrid`**: a 2D grid of characters (`.` air, `#` ground, `B` brick, `c` coin, `^` spikes, `e` enemy), start and exit tile, enemy speed,
  theme. Has `Add...` helpers the generator uses, `IsSolid`, `CountCoins`, `Crop`.
- **`Levels`** (static): `Count` (10 stages), `GridHeight` (15), the 10 `Themes`, `ThemeFor(index)`,
  `Build(index)` (the generator), `Pick` (which chunk types are unlocked at a stage) and `Emit` (draws one chunk).

Details and the difficulty table: [LEVEL_DESIGN.md](LEVEL_DESIGN.md).

## PlayerController.cs
Requires `Rigidbody2D` and `BoxCollider2D` (auto-added). Creates its own `Visual` and `Shadow` children in `Awake`.

| Part | Behaviour |
|---|---|
| Constants | Run speed 5.5, acceleration 60, braking 80, jump velocity 15, stomp bounce 12, coyote 0.1 s, jump buffer 0.12 s, max fall 22. |
| `ReadInput` (Update) | Keyboard (A/D, arrows, Space/W/Up), gamepad (stick, D-pad, south button) and `TouchControls`; the editor bot replaces input. |
| `FixedUpdate` | Ground check, landing detection, acceleration/braking, buffered + coyote jump, variable jump height, heavier fall gravity. |
| `UpdateAnimation` | Idle / walk A-B / jump sprites, facing flip, blinking while invulnerable. |
| `UpdateShadowAndSquash` | Squash and stretch, plus a ground shadow found with a downward ray. |
| `StompOrHurt(enemy)` | Called by an enemy hit box. Stomp = feet above enemy centre and vertical speed at most 2. |
| `Hurt`, `Respawn`, `Freeze`, `Unfreeze`, `Disable` | State changes driven by `Game`. |

## Enemy.cs
Requires `Rigidbody2D` and `BoxCollider2D`. Adds a second, trigger `BoxCollider2D` as the hit box.
Patrols with two short rays (wall ahead, ground ahead) and turns around at walls and ledges; two walk frames;
`Squash()` plays the stomp animation and removes it. Speed is set by `Game` from the stage's `EnemySpeed`.

## Pickups.cs
- **`Coin`**: trigger circle, bobs, spins through 4 frames, calls `Game.AddCoin` and plays a sparkle when touched.
- **`Hazard`**: spike tile with a trigger collider; hurts the player on touch.
- **`LevelExit`**: the flag; a 1 x 3 tile trigger that calls `Game.LevelComplete`.

None are placed by hand: `Game.BuildLevel` creates an empty object and adds the component, which builds its own sprite and collider.

## CameraFollow.cs
Added to the main camera by `Game`. Look-ahead grows with run speed (0.6 to 2.4 tiles), exponential smoothing,
clamped to the level, snapped to 1/32 unit, `Shake(duration, magnitude)` for hits, `SetTarget` for instant placement.

## Backdrop.cs
- **`Backdrop`**: builds the stage's background from its theme and keeps it positioned each frame (`LateUpdate`, order 100).
  Parallax layers use three tiled copies. Static generators `MakeSky`, `MakeLayer`, `MakeStars`, `MakeBody` are
  also used by the branding tool.
- **`AmbientParticles`**: 26 to 46 camera-relative particles (snow, leaves, embers, fireflies, rain) that wrap around the screen.

## PixelPainter.cs
Instance-based painter: `Ellipse`, `RoundRect`, `Rect`, `Pixel`, `Custom` set a colour and a surface normal per pixel;
`Bake` lights them from a fixed top-left light in four tone bands, adds specular glints and an outline, and returns a texture.
Lighting model in [ART_PIPELINE.md](ART_PIPELINE.md).

## PixelArt.cs
Static sprite library. Draws every sprite at first use, packs them into one atlas texture (`PackTextures`), and exposes
properties such as `HeroIdle`, `EnemyA`, `Grass`, `Brick`, `Flag`, `Heart`, `Dot`, `CoinFrame(i)`, `Decor(kind)`.
`PPU` = 32 pixels per unit. Add sprites in `Build` (see [ART_PIPELINE.md](ART_PIPELINE.md)).

## PixelFont.cs
Three classes in one file:
- **`PixelFont`**: 5 x 7 glyph table, `Render` (outlined text texture, cached), `RenderLogo` (3D extruded logo).
- **`UiKit`**: `Text`, `Sprite`, `Quad`, `Image`, `SpriteAt`, `Panel`, `Button`, `K`, `TextSize`; 9-slice panels.
- **`UiCanvas`**: screen-space canvas with a pooled set of `RawImage`s; `Begin`, `Draw`, `End` each frame.

Details: [UI_SYSTEM.md](UI_SYSTEM.md).

## Sfx.cs
`Clip` enum (Jump, Coin, Stomp, Hurt, Step, Land, StageClear, GameOver, Start) and the `Sfx` MonoBehaviour. Synthesises all clips
and the music in `Awake`. Static API: `Play`, `StartMusic`, `ToggleMute`, `Muted`. Details: [AUDIO.md](AUDIO.md).

## Fx.cs
`FxParticle` (a tiny self-destroying particle with velocity, gravity and fade) and static `Fx` helpers
`Burst`, `Dust`, `CoinSparkle`, `StompPuff`. No Unity particle system is used (lighter, and pixel-art friendly).

## TouchControls.cs
Static helper. `Read(view, out move, out jumpPressed, out jumpHeld)` maps touches to three zones of the visible 9:16 view
(left quarter = left, second quarter = right, right half = jump). `AnyPress()` for menus. `Available` becomes true on
mobile platforms or after the first real touch, so desktop touchscreens keep the keyboard hints.

## AutoPlayer.cs (editor only)
The whole file is inside `#if UNITY_EDITOR`; it cannot be in a build. Contains `DebugRecorder` (PNG frame sequence),
`DebugShot` (one screenshot with UI) and `AutoPlayer` (the test bot). See [TESTING.md](TESTING.md).

## Editor/CoverArt.cs (editor only)
Menu tools under Tools > Pixel Hopper that render the logo, itch cover, icon, page background, banner and embed cover from the
game's own art code. See [BRANDING_AND_PAGE_ART.md](BRANDING_AND_PAGE_ART.md).

## Editor/BatchBot.cs (editor only)
Two command-line entry points (see [TESTING.md](TESTING.md)). `BatchBot.Run` opens the game scene, turns on `Game.AutoPlay`, enters Play mode with domain reload disabled
(so static state survives), speeds up time, watches `Game.DebugLog` for `CLEAR` or `TIMEOUT` lines, writes the result and exits with code 0 or 1.
`BatchBot.Report` prints width, coins, enemies and spikes for every stage without entering Play mode.

## Editor/ReleaseBuild.cs (editor only)
Applies the WebGL release settings (template, no splash, gzip with decompression fallback, no exceptions, hashed file names, medium stripping) and calls
`BuildPipeline.BuildPlayer` in the same call. Menu item **Tools > Pixel Hopper > Build WebGL** or `-executeMethod ReleaseBuild.Build -buildOut FOLDER`
([WEBGL_BUILD_AND_ITCH.md](WEBGL_BUILD_AND_ITCH.md)).

## WebGLTemplates/PixelHopper/index.html
Custom page for the web build: loading bar, canvas, keyboard and touch hygiene. Unity fills in file names through template
variables. See [WEBGL_BUILD_AND_ITCH.md](WEBGL_BUILD_AND_ITCH.md).
