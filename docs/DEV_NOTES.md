# Dev notes

Practical notes for building, testing and shipping Pixel Hopper. This is the short checklist version; the detailed pages are
[WEBGL_BUILD_AND_ITCH.md](WEBGL_BUILD_AND_ITCH.md), [TESTING.md](TESTING.md) and [BRANDING_AND_PAGE_ART.md](BRANDING_AND_PAGE_ART.md). The full index is [README.md](README.md).

## Editor tools (Tools > Pixel Hopper)
Defined in `Assets/Editor/CoverArt.cs`.
- **Generate Branding** — logo, 630x500 itch cover, 512 app icon
- **Generate Page Art** — itch page background and the environment banner
- **Generate Embed Background** — portrait launch screen for itch's "Run game" frame

Outputs go to `~/Claude/ProjectIdeas/IndieGames/pixel-hopper/` (branding/, page/). The itch embed cover must be exactly 540 x 960.

## Testing
- Editor-only test bot (`AutoPlayer.cs`, compiled only in the editor) plays the stages to check they can be completed. Set `Game.AutoPlay = true` and `Game.DebugStartStage` (from an editor script), then enter Play mode. Re-run after changing physics or level generation.
- Stage 1 to 10 were all completed by the bot at v1.0.0.
- Screenshots and clips use the editor-only `DebugShot` and `DebugRecorder` helpers (capture at end of frame so UI is included).

## Building for WebGL
1. Switch the build target to WebGL.
2. WebGL player settings: Gzip compression with decompression fallback, no exception support, hashed file names, Medium code stripping, template `PROJECT:PixelHopper`, splash screen off.
3. Build to `.../pixel-hopper/build/webgl` and zip `index.html` plus `Build/` into `pixel-hopper-itch.zip` (index.html must be at the zip root).

Gotchas:
- Setting build options through a wrapper tool can reset the WebGL template/splash to defaults. Reliable approach: set the template and call `BuildPipeline.BuildPlayer` in the same editor call, then check the built `index.html`.
- PlayerSettings edits made right before a recompile can be lost. Set them again just before the build.
- Unity does not recompile edits while the editor window is unfocused; force it with `AssetDatabase.Refresh` + `CompilationPipeline.RequestScriptCompilation`.
- Play mode freezes when the editor is unfocused unless `Application.runInBackground = true`.
- IMGUI colors look washed out in this linear-color project, so all UI uses a uGUI canvas (`UiCanvas` in `PixelFont.cs`).

## itch.io page
- Kind: HTML, play in browser, viewport 540 x 960 (see KI-002 for the height problem), fullscreen button on, mobile friendly on, autostart off.
- Theme: BG `#130a28`, BG 2 `#1c0f3d`, text `#ece6ff`, link `#ffd84a`; background image no repeat, top-center.
- **Embed BG** is the launch screen shown behind the "Run game" button, not the strips beside the game.
- Page copy lives in `docs/itch-page-copy.md`.

## Releasing an update
Every update has **one focus point**. Work on a branch named `update/<version>-<topic>`.
1. Make the one change. Keep the diff small and explain it in comments.
2. Check it: compile, run the bot (`BatchBot.Run`) and, for level changes, the stage report (`BatchBot.Report`) before and after. Commands are in `docs/TESTING.md`.
3. Update `CHANGELOG.md` with two lists: **What changed** (player-facing) and **Code changes** (file by file). Update `docs/KNOWN_ISSUES.md`, `docs/ROADMAP.md` and any doc that describes the changed code.
4. Bump the version (Project Settings > Player > Version, field `bundleVersion`).
5. Build with `ReleaseBuild.Build` (`docs/WEBGL_BUILD_AND_ITCH.md`), zip with `index.html` at the zip root, play it once in a browser.
6. Commit on the branch, merge into `main`, tag `vX.Y.Z`, push `main` and the tag.
7. On itch.io upload the zip to the same file slot and paste the update note (text in `docs/itch-page-copy.md`).
