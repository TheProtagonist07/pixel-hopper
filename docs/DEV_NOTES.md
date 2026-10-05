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
1. Fix or add the feature, test it (bot + a manual run).
2. Update `CHANGELOG.md`, `docs/KNOWN_ISSUES.md` and `docs/ROADMAP.md`.
3. Bump the version in Project Settings.
4. Build WebGL, zip, upload to the same itch file slot, add a changelog note on the page.
5. Commit and push.
