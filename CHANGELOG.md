# Changelog

All notable changes to Pixel Hopper. Newest first. Update this file with every fix or feature.

## [Unreleased]
- (nothing yet)

## [1.0.2] - 2026-10-06
Update 2: **Desktop first run.** Fewer players stuck on a game that "does not respond" or does not fit their screen.

### What changed (player-facing)
- A yellow hint, "CLICK THE GAME TO PLAY", now appears over the game whenever the game does not have keyboard focus, and disappears as soon as the player clicks it. On itch.io the first key press used to go to the page instead of the game, so the game looked frozen; now the player is told what to do. The click that removes the hint also starts the game from the title screen.
- itch.io page setting (not code, done on the itch.io edit page): set the embed viewport to **405 x 720** instead of 540 x 960. It is the same 9:16 shape but 25% smaller, so it fits on most laptop screens. Players on small laptops can still use the fullscreen button.

### Code changes
- `Assets/WebGLTemplates/PixelHopper/index.html` (the only game file changed): a new `#focus-hint` element with its CSS (centred, pulsing, `pointer-events: none` so clicks pass through to the game), and a few lines of JavaScript, `updateFocusHint()`, which shows the hint while the game is running and `document.hasFocus()` is false. It re-checks on `focus`, `blur`, `visibilitychange`, `pointerdown` and a 500 ms timer (iframes do not always fire focus events).
- No C# code changed. `ProjectSettings/ProjectSettings.asset`: version number 1.0.1 to 1.0.2.

### Verified
- Tested inside a cross-origin iframe on a stand-in "itch page": the game loads, the hint appears when the page outside the game is clicked, and disappears when the game is clicked.
- The original problem (first Space press ignored) could **not** be reproduced in the test browser, where Space worked at once, so the real itch.io behaviour should be checked after upload (steps in `docs/TESTING.md`).

## [1.0.1] - 2026-10-06
Update 1: **Stage 1 gets its first enemy.**

### What changed (player-facing)
- Stage 1 now has one slow enemy, placed as the third section of the stage, after the player has practised jumping over a gap and a wall. Before this, enemies first appeared in stage 2 even though the help dialog already says "stomp enemies".
- Because that section replaced another one, stage 1 has 24 coins instead of 32 and is one tile shorter (67 tiles instead of 68). Stages 2 to 10 are exactly the same as in 1.0.0 (checked with the new stage report).

### Code changes
- `Assets/Scripts/Levels.cs`: in `Levels.Build`, one new line forces the third chunk of stage 1 to be the existing `Kind.Enemies` chunk, with an explanatory comment. No new enemy code was needed because the enemy type and the chunk already existed.
- `Assets/Editor/BatchBot.cs` (new, editor only): runs the test bot from the command line without opening the editor (`BatchBot.Run`) and prints a per-stage report of width, coins, enemies and spikes (`BatchBot.Report`). Used to prove stage 1 is still beatable and that no other stage changed.
- `Assets/Editor/ReleaseBuild.cs` (new, editor only): builds the WebGL release with the correct settings in one repeatable command, so every update is built the same way.
- `ProjectSettings/ProjectSettings.asset`: version number 1.0.0 to 1.0.1.

### Verified
- Test bot clears stage 1 with 0 deaths.
- Stage report: only stage 1 differs from 1.0.0 (enemies 0 to 1, coins 32 to 24, width 68 to 67).
- WebGL build (custom template, 11.6 MB zip) loads in a browser without console errors and plays through stage 1 into stage 2.
### Documentation
- Added full reference documentation in `docs/` (overview, architecture, scripts, level design, art, audio, UI, testing, WebGL and itch.io, branding, decisions, glossary) with an index at `docs/README.md`
- Added doc comments and "why" comments to every C# script, the editor branding tool and the WebGL page template (no code changes)
- Added `tools/verify_comment_only.py` to prove comment-only edits leave the code unchanged

## [1.0.0] - 2026-10-05
First public release on itch.io (WebGL).

### Added
- 10 stages generated from hand-tuned chunks (gaps, walls, stairs, spikes, enemies, platforms, spike runs, gauntlets), with difficulty rising per stage
- 10 world themes: Sunny Meadow, Cloud Hills, Sunset Run, Autumn Trail, Deep Cave, Frozen Peaks, Dusty Desert, Midnight, Lava Fields, Final Storm
- Portrait 9:16 view with look-ahead camera and pillarbox bars on wide screens
- Shaded pixel-art hero, enemy, coins (spinning), tiles, spikes, flag and scenery props
- Parallax backdrops, sun/moon, stars, clouds, ambient particles (snow, leaves, embers, fireflies, rain)
- Procedural chiptune music and sound effects for jump, coin, stomp, hurt, steps, landing, stage clear, game over
- Pixel font, 3D logo, HUD, title screen, stage banners, stage-clear and game-over panels
- "How to play" dialog with OK and Skip buttons
- Keyboard, gamepad and touch controls
- Custom WebGL page template with loading bar; gzip build with decompression fallback

### Known at release
- See [docs/KNOWN_ISSUES.md](docs/KNOWN_ISSUES.md)
