# Pixel Hopper

A tiny portrait pixel platformer in 10 stages. Stomp enemies, dodge spikes and pits, grab coins, reach the flag.

**Play it:** https://the-protagonist07.itch.io/pixel-hopper

- Unity 6 (6000.6), URP, new Input System, 2D physics
- Portrait 9:16 with a narrow view and camera look-ahead
- 10 generated stages with 10 themes (sky, parallax mountains, scenery, ambient particles)
- All art, music and sound effects are generated in code (no imported asset packs)
- Plays in the browser (WebGL) on desktop and phone

## Controls
| Device | Move | Jump |
|---|---|---|
| Keyboard | A / D or arrows | Space, W or Up (hold = higher) |
| Gamepad | Left stick / D-pad | A / bottom button |
| Touch | Left side of screen | Right side of screen |

`M` mutes, `H` opens the help dialog.

## Project layout
| Path | What it is |
|---|---|
| `Assets/Scripts/Game.cs` | Game state, level building, HUD, title and help dialog |
| `Assets/Scripts/Levels.cs` | Stage generator (chunk based) and the 10 world themes |
| `Assets/Scripts/PlayerController.cs`, `Enemy.cs`, `Pickups.cs` | Gameplay objects |
| `Assets/Scripts/PixelPainter.cs`, `PixelArt.cs` | Procedural, shaded pixel-art sprites |
| `Assets/Scripts/PixelFont.cs` | Pixel font, 3D logo, UI kit |
| `Assets/Scripts/Backdrop.cs` | Sky, parallax layers, ambient particles |
| `Assets/Scripts/Sfx.cs` | Procedural chiptune music and sound effects |
| `Assets/WebGLTemplates/PixelHopper/` | Custom page template for the WebGL build |
| `Assets/Editor/CoverArt.cs` | Editor tools that render the logo, cover, page art and embed cover |
| `docs/` | Roadmap, known issues, dev notes, itch page copy |

See `docs/DEV_NOTES.md` for how to build and ship.

## Docs
- [CHANGELOG.md](CHANGELOG.md) — what changed in each version
- [docs/ROADMAP.md](docs/ROADMAP.md) — planned upgrades and the path to mobile stores
- [docs/KNOWN_ISSUES.md](docs/KNOWN_ISSUES.md) — open bugs and fixes
- [docs/DEV_NOTES.md](docs/DEV_NOTES.md) — build, test and release notes

## Credits
Made with Unity. All art, music and sound effects are generated in code.

License: not chosen yet.
