# WebGL build and itch.io

How the browser version is built, packaged and published. Short checklist version: [DEV_NOTES.md](DEV_NOTES.md).

## Build settings (Project Settings > Player > WebGL, and Build Profiles)

| Setting | Value | Why |
|---|---|---|
| Build target | WebGL | Browser game |
| Compression format | Gzip, with **decompression fallback** on | itch.io's hosting does not set the `Content-Encoding` header Unity needs for pre-compressed files; the fallback lets Unity's loader decompress in JavaScript. Gzip also builds faster than Brotli. |
| Exception support | None | Smaller and faster build; the game does not rely on exceptions |
| Name files as hashes | On | Browser cache is refreshed whenever the build changes |
| Code stripping | Medium | Smaller wasm without the risks of High |
| WebGL template | `PROJECT:PixelHopper` (the folder `Assets/WebGLTemplates/PixelHopper`) | Custom branded loader and input handling |
| Splash screen | Off | Cleaner start |
| Colour space | Linear | Matches the rest of the project (see [ART_PIPELINE.md](ART_PIPELINE.md)) |

Result at v1.0.0: about 11 MB zipped. Most of it is the Unity engine's code; the game's own data is tiny because it has no imported assets.

## The page template (`index.html`)

Unity copies `Assets/WebGLTemplates/PixelHopper/index.html` to the build and fills in the file names through template variables
(`DATA_FILENAME`, `FRAMEWORK_FILENAME`, `LOADER_FILENAME`, `CODE_FILENAME`, `PRODUCT_NAME`...). What it does:

- Shows a purple loading screen with the title and a green striped progress bar fed by Unity's progress callback.
- Creates the game on a full-window canvas with `image-rendering: pixelated`.
- Blocks page scrolling (Space and arrow keys), the context menu, text selection, pinch zoom and pull-to-refresh, so it feels like an app.
- Focuses the canvas on any click or tap so keyboard input works.
- Caps the device pixel ratio at 2 (phones report 3, which costs speed for no visible gain with pixel art).
- Shows a red error strip if Unity reports an error or fails to start.

The comments in the file describe each part.

## Building

**Recommended: one command.** `Assets/Editor/ReleaseBuild.cs` applies all the settings above and builds in the same call
(this avoids the settings being reset between steps). Close the project in Unity first, then:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -nographics -projectPath ~/pixel-hopper -executeMethod ReleaseBuild.Build \
  -buildOut ~/Claude/ProjectIdeas/IndieGames/pixel-hopper/build/v1.0.1 -logFile /tmp/build.log
cd ~/Claude/ProjectIdeas/IndieGames/pixel-hopper/build/v1.0.1 && zip -r ../pixel-hopper-v1.0.1-itch.zip index.html Build
```

It takes about two to three minutes. The log line `ReleaseBuild: Succeeded` confirms it. The same build is also in the menu:
**Tools > Pixel Hopper > Build WebGL** (asks for an output folder).

Manual steps, if you prefer the editor:

1. Switch to WebGL, check the settings above.
2. Build into a folder (the project used `.../pixel-hopper/build/webgl`). The output is `index.html` and a `Build/` folder with four files
   (`*.loader.js`, `*.framework.js.unityweb`, `*.data.unityweb`, `*.wasm.unityweb`).
3. **Check the built `index.html`** really is the custom template (look for the "PIXEL HOPPER" loader). If it is Unity's default page, the template setting did not stick.
4. Zip `index.html` and `Build/` so that **`index.html` is at the root of the zip**, not inside a folder:
   ```bash
   cd path/to/build/webgl
   zip -r ../pixel-hopper-itch.zip index.html Build
   ```
5. Test locally before uploading: serve the folder over HTTP (opening the file directly will not work) and play a full run, ideally on a phone too.

### Gotchas learned the hard way

- Build wrapper tools can reset the WebGL template and splash settings. Reliable approach: set the template and call
  `BuildPipeline.BuildPlayer` in the **same** editor call, then check the built `index.html`.
- PlayerSettings changes made right before a recompile can be lost. Set them again just before building.
- Unity does not recompile edited scripts while the editor window is unfocused. Force it with `AssetDatabase.Refresh` and
  `CompilationPipeline.RequestScriptCompilation`.
- Play mode freezes when the editor is unfocused unless `Application.runInBackground = true` (the game sets it).
- A delayed callback (`EditorApplication.delayCall`) may not fire while Unity is unfocused; call `BuildPlayer` directly instead.

## itch.io page setup

| Setting | Value |
|---|---|
| Kind of project | HTML |
| Upload | The zip, marked "This file will be played in the browser" |
| Embed options | Click-to-launch, **viewport 405 x 720** (same 9:16 shape as the old 540 x 960, 25% smaller so it fits most laptops; see KI-002), fullscreen button on, mobile friendly on, autostart off |
| Visibility | Public once happy (it was set to Draft first) |
| Page theme | Background `#130a28`, secondary background `#1c0f3d`, text `#ece6ff`, link `#ffd84a`; background image set to the generated page background, no repeat, top centre |
| Banner | The generated environment banner |
| Cover image | `cover_630x500.png` |
| Embed background | The portrait launch screen (see the note below) |

**What "Embed BG" is:** the image shown inside the game frame *before* the player clicks "Run game". It is **not** the area to the left and
right of the running game (those are the black bars the game itself draws, or the page background). The launch screen is generated at 540 x 960
(exported at 2x, 1080 x 1920) so it fits the frame exactly. See [BRANDING_AND_PAGE_ART.md](BRANDING_AND_PAGE_ART.md).

Page text, tags and descriptions: [itch-page-copy.md](itch-page-copy.md).

## Releasing an update

1. Fix or add the feature and test it (bot plus a manual run, [TESTING.md](TESTING.md)).
2. Update [../CHANGELOG.md](../CHANGELOG.md), [KNOWN_ISSUES.md](KNOWN_ISSUES.md) and [ROADMAP.md](ROADMAP.md).
3. Bump the version (Project Settings > Player > Version).
4. Build, check the built `index.html`, zip.
5. On itch.io, upload the new zip to the **same file slot** and add a short changelog note on the page.
6. Commit and push.

## Known web-specific issues

| Issue | Where documented |
|---|---|
| No sound on iPhone when the silent switch is on | KI-001. Optional fix: `navigator.audioSession.type = 'playback'` in the template |
| Embed frame taller than many laptop screens (use viewport 405 x 720) | KI-002 |
| First Space press ignored until the page has focus (title prompt says "CLICK OR PRESS SPACE") | KI-004 |
