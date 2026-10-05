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

## 2. Screenshot and video helpers

Also in `AutoPlayer.cs` (editor only):

| Helper | Use |
|---|---|
| `DebugShot.Capture(path, superSize)` | One PNG of the game view **including the UI**. `superSize` 3 gives crisp pixel art. |
| `DebugRecorder.Record(dir, seconds, fps, superSize)` | Numbered PNG frames `0000.png`... and a `done.txt`. Turn them into a clip with ffmpeg. |

Why not the standard screenshot tool? It can miss the UI overlay; these wait for the end of the frame, after all rendering.
The image has black bars if the game view is wider than 9:16; crop them afterwards.

`Game` has shortcuts for reaching specific screens quickly (editor only): `DebugStage(index)`, `DebugClear()`, `DebugOver()`, `DebugWon()`, `DebugBegin()`.

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
| Game over | After 3 lives: panel, tap retries the same stage with the coin total it started with |
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
