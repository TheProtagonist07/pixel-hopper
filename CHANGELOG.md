# Changelog

All notable changes to Pixel Hopper. Newest first. Update this file with every fix or feature.

## [Unreleased]
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
