# Decision log

Why things are the way they are. Format: **decision, reason, alternatives rejected, consequences**. Add a new entry whenever you make a choice a future reader
might question.

## D-01 Everything is procedural: no imported art, audio or fonts
- **Why:** tiny download, no licences to track, one consistent style, the whole project is plain text that can be reviewed and changed by code. It also let the visual style be
  changed in minutes (for example "more vibrant, more 3D") without redrawing anything.
- **Rejected:** asset packs (licence and style mismatch), hand-drawn sprites (slow to iterate).
- **Consequences:** art is limited to what the painter can express; startup spends a few milliseconds generating sprites and audio.

## D-02 Portrait 9:16 with a narrow 15-tile-tall view
- **Why:** it is the shape of a phone held upright, the likely audience on itch.io and later in app stores. A narrow view (about 8.4 tiles wide) makes stages harder and
  keeps sprites large.
- **Rejected:** landscape (more room, but wrong for phones and less distinctive).
- **Consequences:** wide windows show black bars; the camera needs look-ahead because the player cannot see far ahead (see D-09).

## D-03 Deterministic chunk-based level generator instead of level files
- **Why:** unlimited variety with no level editor, difficulty that scales by formula, and every stage is **guaranteed completable**: chunks start and end on flat ground and are
  sized inside the hero's jump reach. Fixed seeds mean every player sees identical stages, so stages can be tuned and compared.
- **Rejected:** hand-built tile maps (slow for 10 stages), fully random generation (can create impossible gaps).
- **Consequences:** stages feel "assembled"; to get a signature set-piece, add a new chunk type or a hand-built grid ([LEVEL_DESIGN.md](LEVEL_DESIGN.md)).

## D-04 Scene contains one Game object; everything else is created in code
- **Why:** no prefab or inspector wiring to break, trivially reproducible from a clean checkout, easy to read and review.
- **Rejected:** prefabs for player, enemy and coin.
- **Consequences:** artists cannot tweak objects visually in the editor; every object builds itself in `Awake`.

## D-05 UI is immediate mode on a uGUI RawImage canvas, not IMGUI and not prefabs
- **Why:** the IMGUI (`OnGUI`) version looked **washed out** in Linear colour space and did not match the world's colours. A canvas goes through the normal colour pipeline.
  Immediate-mode code keeps layout as simple arithmetic and avoids UI objects to maintain.
- **Rejected:** `OnGUI` (colour problem), UI Toolkit and prefab-based uGUI (heavier for a tiny game).
- **Consequences:** no built-in text layout, so the project has its own pixel font and `UiKit`.

## D-06 Own pixel font with a generated 3D logo
- **Why:** perfectly crisp text at integer scales on every device, no font import settings, and the logo reuses the same glyphs so it always matches.
- **Rejected:** TextMeshPro with a pixel font (import and filtering complexity, extra download).

## D-07 PixelPainter lighting with four tone bands
- **Why:** feedback said characters and icons looked flat; lighting normals gives a 3D feel from simple shapes, and snapping to four bands keeps it recognisably pixel art
  instead of smooth shading.
- **Rejected:** hand-placed highlight pixels (do not scale to many sprites), smooth lit sprites (not pixel art).

## D-08 One sprite atlas built at startup
- **Why:** hundreds of tile sprites per stage; a shared texture means few draw calls, which matters on WebGL and phones.
- **Consequences:** the atlas is rebuilt if Unity destroys it (for example leaving Play mode); `PixelArt.Get` handles that.

## D-09 Camera: look-ahead, exponential smoothing, snapped to the pixel grid
- **Why:** the narrow view needs look-ahead so hazards are visible in time; exponential smoothing is framerate independent; snapping to 1/32 unit prevents shimmering sprites.
- **Consequences:** the camera and backdrops always move in whole-pixel steps.

## D-10 Forgiving player physics (coyote time, jump buffer, variable jump, heavier fall gravity)
- **Why:** standard platformer feel techniques that make controls feel fair, especially on touch screens.
- **Consequences:** physics constants in `PlayerController` are tuned together with the chunk sizes in `Levels.cs`. Change one, re-test the other ([TESTING.md](TESTING.md)).

## D-11 Enemies: solid body plus a larger trigger hit box; player and enemy bodies ignore each other
- **Why:** all contact is decided by the stomp rule (feet above enemy centre, not moving up fast), so the player is never shoved sideways or bounced unpredictably by physics.
- **Consequences:** the stomp rule is lenient on purpose; landing on an enemy from the side while falling counts as a stomp.

## D-12 Solid tiles merged into few rectangle colliders
- **Why:** one collider per tile makes the physics slow and the hero can snag on seams between neighbouring tiles.
- **How:** greedy row-by-row rectangle merge in `Game.BuildSolids`.

## D-13 Audio synthesised with a phase accumulator
- **Why:** no files, tiny size, consistent chiptune character; accumulating phase (instead of `sin(frequency * time)`) keeps frequency sweeps smooth without clicks.
- **Consequences:** music starts after the first user input because browsers block earlier audio.

## D-14 WebGL: gzip with decompression fallback, custom page template
- **Why:** itch.io's static hosting cannot set the response headers that pre-compressed Unity builds need, so the fallback is required. Gzip builds faster than Brotli.
  The custom template gives a branded loader and prevents the page from scrolling or selecting text over the game.

## D-15 Lives belong to the run; game over restarts at stage 1 (changed in v1.1.0)
- **Why:** the owner wanted lives that really run out, and a game over that sends you back to the start. To keep ten stages finishable, a cleared stage gives one heart back and stages contain bonus hearts, with a maximum of 5 so lives stay meaningful.
- **Replaced:** v1.0.x gave 3 lives at every stage and game over retried the same stage (forgiving, but lives never mattered).
- **Consequences:** a run is longer to lose and the first stage matters more; stage 10 difficulty (KI-005) now depends on how many hearts the player kept.

## D-19 One-way camera with a wall at the left edge of the screen
- **Why:** players could walk back through ground the camera had left behind, which looked broken. Classic platformers never scroll back and treat the left screen edge as a wall.
- **How:** `CameraFollow` remembers the furthest x it reached and never goes below it; `PlayerController` stops the hero at `CameraLeftEdge`; a respawn re-centres the camera.

## D-16 Marketing images are generated by an editor tool from the game's own art
- **Why:** images always match the game, change with one click, and no graphics software is needed. See [BRANDING_AND_PAGE_ART.md](BRANDING_AND_PAGE_ART.md).

## D-17 One folder for everything: the Unity project root is the Git repo (changed 2026-10-06)
- **Why:** files were kept in two places (the Unity project, and a second folder with builds, marketing and page art), which made them easy to lose track of and left
  the Git history incomplete. Unity only imports `Assets`, `Packages` and `ProjectSettings`, so extra folders next to them are safe. Now the project folder holds code,
  `docs/`, `marketing/`, `management/`, `sales/` and `builds/`, and one repository covers everything.
- **Size control:** `builds/` (about 89 MB, large and regenerable) is not committed; each release zip is also attached to a GitHub Release, which gives an off-machine copy without
  bloating the Git history. Everything else is small and committed (the repository is private), so GitHub is the backup.
- **Consequences:** renaming the project folder requires re-adding it in Unity Hub. See [PROJECT_LAYOUT.md](PROJECT_LAYOUT.md).

## D-18 Documentation lives in the repo and is updated with every change
- **Why:** the project must stay explainable and maintainable months later. Code comments explain code, `docs/` explains the system, `CHANGELOG.md` explains history.
  The helper `tools/verify_comment_only.py` proves comment-only edits did not change code.

## Not decided yet (open questions)

| Question | Notes |
|---|---|
| Score system, timer, and leaderboard design | See [ROADMAP.md](ROADMAP.md); leaderboards need a backend or a service |
| Difficulty modes | Easy, Normal, Hard, and a "slow" accessibility mode are on the roadmap |
| Mobile store release | Possible through Unity (Android first); see the roadmap's store section |
| Licence for the repository | Not chosen yet |
