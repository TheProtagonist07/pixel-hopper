# Changelog

All notable changes to Pixel Hopper. Newest first. Update this file with every fix or feature.

## [Unreleased]
- (nothing yet)

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
