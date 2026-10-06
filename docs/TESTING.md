# Testing

There are no unit tests. Quality is checked with three things: an automatic **test bot**, helper tools for **screenshots and video**, and a
**manual checklist** on real devices.

## 1. The test bot (`AutoPlayer`)

Defined in `Assets/Scripts/AutoPlayer.cs`, compiled **only in the editor** (`#if UNITY_EDITOR`), so it can never be in a build.

**What it is:** a simple bot that always runs right and decides when to jump by looking ahead with rays and overlap boxes. It jumps at ledges
(no ground ahead), at walls, and when spikes (within 1.2 tiles) or an enemy (within 2 tiles) are in front. It holds jump for 0.7 s for a full jump.
It feeds `PlayerController` through the editor-only fields `bot`, `botMove`, `botJumpHeld`, `botJumpPress` instead of the keyboard.

**What it proves:** that each generated stage *can be completed*. It is not a good player (it gets hit sometimes), so the number of
deaths is a rough difficulty signal.

**How to run it:**
1. Open the project in Unity, open the scene, stay out of Play mode.
2. Set the flags (from any editor script):
   ```csharp
   Game.AutoPlay = true;
   Game.DebugStartStage = 0;   // 0 = stage 1
   ```
3. Enter Play mode. The game starts by itself and plays stage after stage.
4. Read the results in `Game.DebugLog`: one line per stage (`CLEAR time=... deaths=...` or `TIMEOUT`), plus the first 8 deaths of each stage with their coordinates.

In bot mode deaths cost nothing (the bot respawns), and a stage is abandoned after 150 s (logged as TIMEOUT).

**When to run it:** after changing jump physics, enemy speed, or anything in `Levels.cs`.

**Result at v1.0.0:** all 10 stages completed; stage 10 needed about 6 deaths (see KI-005 in [KNOWN_ISSUES.md](KNOWN_ISSUES.md)).

### Running the bot without the editor window (`BatchBot`)

`Assets/Editor/BatchBot.cs` runs the same bot from the command line, which is how an update is checked quickly and repeatably.
Close the project in Unity first (a project can only be open in one editor at a time), then:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity

# Play stage 1 (stage index 0) with the bot at 4x speed; writes the result to a file and exits with code 0 (pass) or 1 (fail)
$UNITY -batchmode -nographics -projectPath ~/pixel-hopper -executeMethod BatchBot.Run \
  -botStage 0 -botStages 1 -botSpeed 4 -botLimit 600 -botOut /tmp/bot.txt -logFile /tmp/bot.log

# Play all ten stages in a row
$UNITY -batchmode -nographics -projectPath ~/pixel-hopper -executeMethod BatchBot.Run \
  -botStage 0 -botStages 10 -botSpeed 4 -botLimit 1800 -botOut /tmp/bot.txt -logFile /tmp/bot.log
```

| Argument | Meaning | Default |
|---|---|---|
| `-botStage` | First stage to play (0 = stage 1) | 0 |
| `-botStages` | How many stages to play in a row | 1 |
| `-botSpeed` | `Time.timeScale` while playing (higher = faster, but less like real play) | 4 |
| `-botLimit` | Real-time seconds before the run is given up as a failure | 600 |
| `-botOut` | File to write the result lines to | none |

### Stage report (`BatchBot.Report`)

Prints one line per stage: width in tiles and how many coins, enemies, spike tiles and bonus hearts it has. It does not enter Play mode, so it takes a few seconds.
Run it **before and after** any change to `Levels.cs` and compare the two outputs; that shows exactly which stages a change touched.

```bash
$UNITY -batchmode -nographics -projectPath ~/pixel-hopper -executeMethod BatchBot.Report -botOut /tmp/report.txt -logFile /tmp/report.log
cat /tmp/report.txt
```

Example (v1.0.1): `stage  1: width= 67 coins= 24 enemies= 1 spikes= 0 enemySpeed=1.50`.

### Rule checks (`BatchChecks`)

`Assets/Editor/BatchChecks.cs` checks game **rules** automatically: it creates a situation (teleports the player, calls the same methods the game calls), waits for the real code
to react, and tests the result. Close the project in Unity first, then:

```bash
$UNITY -batchmode -nographics -projectPath ~/pixel-hopper -executeMethod BatchChecks.Run -checksOut /tmp/checks.txt -logFile /tmp/checks.log
cat /tmp/checks.txt        # one PASS or FAIL line per check, then CHECKS RESULT
```

Checks in v1.1.0: jumping over the flag pole finishes the stage, the camera does not scroll back and the left screen edge blocks the player, three deaths end the run and restart at
stage 1 with 3 lives, lives carry between stages and a cleared stage gives a heart, a heart pickup gives a life and is left alone at 5 lives. A run takes about two minutes.

**Proving a fix:** a good check fails on the old code and passes on the new code. To see it, temporarily put the old versions of the gameplay scripts back
(`git stash push -- Assets/Scripts/...`, run the checks, `git stash pop`). This is how the v1.1.0 fixes were verified (5 checks failed before, all passed after).
To add a check, add a block to `BatchChecks.Checks` that ends with `Report(name, passed, detail)`.

## 2. Screenshot and video helpers

Also in `AutoPlayer.cs` (editor only):

| Helper | Use |
|---|---|
| `DebugShot.Capture(path, superSize)` | One PNG of the game view **including the UI**. `superSize` 3 gives crisp pixel art. |
| `DebugRecorder.Record(dir, seconds, fps, superSize)` | Numbered PNG frames `0000.png`... and a `done.txt`. Turn them into a clip with ffmpeg. |

Why not the standard screenshot tool? It can miss the UI overlay; these wait for the end of the frame, after all rendering.
The image has black bars if the game view is wider than 9:16; crop them afterwards.

`Game` has shortcuts for reaching specific screens quickly (editor only): `DebugStage(index)`, `DebugClear()`, `DebugOver()`, `DebugWon()`, `DebugBegin()`.

## 2b. Testing a web template change in an itch.io-style iframe, without publishing

The itch.io page runs the game in a cross-origin iframe that is created after the player clicks "Run game". A template change can be tested locally
with a stand-in page, no upload needed:

1. Serve the built game folder on one port and a small wrapper page on another (different ports count as different origins):
   `cd build/v1.0.2 && python3 -m http.server 8099` and, in another terminal, a wrapper `index.html` with a "Run game" button that creates
   `<iframe src="http://localhost:8099/index.html">` when clicked.
2. Open the wrapper, click "Run game", wait for the game, and check the change there (loading screen, input, console errors).
3. Click the game: on the title screen the click starts it.

## 2c. Screenshots for an update post (`ScreenshotTour`)

`Assets/Editor/ScreenshotTour.cs` takes the screenshots for an update post from the real game: it builds each situation (a heart in a stage, the stage clear panel, game over, the hero at the
left wall), waits for the game to react and captures the frame, UI included. Unlike the other command-line tools it needs a window, so it is started **without** `-batchmode`
(the editor opens for about a minute, then quits by itself; the project must not already be open):

```bash
$UNITY -projectPath ~/pixel-hopper -executeMethod ScreenshotTour.Run -shotsOut /tmp/shots -logFile /tmp/shots.log
python3 tools/crop_shots.py /tmp/shots marketing/screenshots/vX.Y.Z --width 720    # removes the black bars, saves 720 x 1280 PNGs
```

Edit the `Tour` method to photograph different situations. Reference the images from the changelog entry (`marketing/screenshots/vX.Y.Z/`) and attach them to the itch.io update post.

## 3. Manual test checklist

Run before every release, on **desktop browser and a real phone** (the iPhone sound issue was only found this way).

| Area | Check |
|---|---|
| Loading | Branded loading bar fills, then the title appears; no red error strip |
| Title | Logo animates, prompt blinks, Space/tap starts |
| Help dialog | Opens first time, OK and SKIP work, Esc skips, `H` reopens during play, skip is remembered after reload |
| Controls | Keyboard (A/D/arrows, Space/W/Up), gamepad if available, touch zones on a phone |
| Jump feel | Short tap = small hop, hold = full jump, coyote time works at ledges |
| Enemies | Stomp from above kills and bounces; side contact costs a life |
| Hazards | Spikes hurt; falling into a pit costs a life; respawn blinks for 2 s |
| Coins | Collected with sound; HUD total and stage-clear `x / y` agree |
| Flag | Stage clear panel, then the next stage loads automatically |
| Lives | Start with 3 hearts; they carry between stages, +1 on each stage clear (max 5), bonus hearts in stages 2 to 10 add 1 (a heart is left alone at 5) |
| Game over | After the third life is lost: panel "YOU START OVER AT STAGE 1", tap restarts at stage 1 with 3 lives and 0 coins |
| Camera | The screen never scrolls back; the hero cannot walk past the left edge of the screen; after losing a life the camera returns to the start |
| Flag | Jumping high over the flag pole still completes the stage |
| Win | After stage 10: win screen, tap restarts at stage 1 |
| Audio | Music starts at first interaction, `M` mutes, effects play (iPhone: silent switch off) |
| Layout | Resize the window: 9:16 view stays centred; text never overflows its panel |
| Performance | Smooth on a mid-range phone; no hitching when a stage loads |

Record anything found in [KNOWN_ISSUES.md](KNOWN_ISSUES.md) with device, browser and steps.

## 4. Verifying a comment-only change

When you edit only comments or documentation inside scripts, prove that no code changed:

```bash
python3 tools/verify_comment_only.py Assets/Scripts/Game.cs Assets/Scripts/Sfx.cs
# compare with another revision:
python3 tools/verify_comment_only.py --ref origin/main Assets/Scripts/Game.cs
```

It strips comments (keeping strings) and whitespace, then compares with the same file at `HEAD` (or `--ref`). Exit code 0 means identical code.
The editor cannot check XML-doc syntax for you, so also avoid raw `<`, `>` and `&` inside `///` comment text (write `&lt;`, `&gt;`, `&amp;`);
a mistake only produces a compiler warning, not an error.

## 5. Editor-only noise (not bugs)

See the bottom of [KNOWN_ISSUES.md](KNOWN_ISSUES.md): Unity AI `NoSubscription` errors, errors if you edit scripts during Play mode, and FMOD output warnings.
