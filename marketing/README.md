# Marketing

Everything that promotes Pixel Hopper. Images are generated from the game's own art by the editor tool `CoverArt` ([docs/BRANDING_AND_PAGE_ART.md](../docs/BRANDING_AND_PAGE_ART.md)),
so they can be regenerated after any art change.

| Folder | Contents |
|---|---|
| `branding/` | `logo.png` (transparent), `cover_630x500.png` (itch cover), `icon_512.png` |
| `page/` | `page_background_2560x1100.png`, `banner_environment_1920x420.png`, `embed_cover_540x960.png` (launch screen behind "Run game") |
| `promo/` | `pixel-hopper-clip.mp4` and `.gif` (promo clip for social posts) |
| `screenshots/` | `v1.0.0-polish/` (look of the first release) and `vX.Y.Z/` for each update (made by `ScreenshotTour`, see [docs/TESTING.md](../docs/TESTING.md)) |

## Brand
- Colours: page background `#130a28`, secondary `#1c0f3d`, text `#ece6ff`, accent `#ffd84a`.
- Style: retro pixel art, rainbow 3D "PIXEL HOPPER" logo, portrait 9:16.
- The itch embed viewport is 540 x 960 (matches the launch cover).

## Text
- Page description, tags, launch post drafts and **update notes**: [docs/itch-page-copy.md](../docs/itch-page-copy.md).

## Posting status
- Launch posts (Reddit, X) are **paused** by the owner while the game is improved in small updates. Each update gets an itch.io devlog post with its screenshots.
- A suggested time for a first social push is a Saturday (#screenshotsaturday on X), after a few updates.

## For each update
1. Take screenshots: `ScreenshotTour` then `tools/crop_shots.py` into `screenshots/vX.Y.Z/`.
2. Paste the update note from [docs/itch-page-copy.md](../docs/itch-page-copy.md) into an itch.io devlog post and attach 3 to 4 of the screenshots.
3. Add a row to [management/release-log.md](../management/release-log.md).
