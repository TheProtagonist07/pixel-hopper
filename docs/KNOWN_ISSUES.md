# Known issues

Keep this list current. When something is fixed, change its status, note the version, and add a line to `CHANGELOG.md`.

Status: **Open**, **Planned** (scheduled), **Fixed (vX.Y.Z)**, **Won't fix**.

| ID | Issue | Severity | Status | Notes / fix |
|---|---|---|---|---|
| KI-001 | No sound on iPhone when the silent switch is on | Low | Open | Not a bug, iOS mutes web audio in silent mode. Optional fix: set `navigator.audioSession.type = 'playback'` in `Assets/WebGLTemplates/PixelHopper/index.html`, rebuild, re-upload. Mention in the itch description meanwhile. |
| KI-002 | Embed frame (540 x 960) is taller than many laptop screens, so the lower part of the game is below the fold | Low | Accepted | A smaller 405 x 720 frame was tried (v1.0.2) but it cropped the cover image and showed more purple page background, so the viewport stays **540 x 960**. Players on small laptops use the fullscreen button. Revisit if players report it. |
| KI-003 | Description typo: "3 lives per stage , instant retry" (stray space) | Trivial | Open | Edit the itch page text. |
| KI-004 | First Space press can be ignored until the game has keyboard focus (itch.io embed) | Low | Fixed (v1.1.0) | The title prompt now says "CLICK OR PRESS SPACE TO START"; a click focuses the game and starts it. (A separate hint box added in v1.0.2 was removed in v1.1.0 because it duplicated the prompt.) |
| KI-005 | Stage 10 is hard (test bot needed 6 deaths in v1.0.0); other stages are fine | Design | Planned | Bonus hearts added in v1.1.0 help; wait for player feedback, then tune. |
| KI-007 | The player could walk back through ground the camera had already left behind | Medium | Fixed (v1.1.0) | One-way camera and a wall at the left edge of the screen. |
| KI-008 | Jumping over the top of the finish pole did not complete the stage | High | Fixed (v1.1.0) | The flag trigger is now a full-height line (1 x 8 tiles). |
| KI-009 | Lives refilled at every stage and game over only retried the same stage | Design | Changed (v1.1.0) | Lives are per run (3, max 5), +1 per cleared stage, bonus hearts in stages 2 to 10, game over restarts at stage 1. |
| KI-006 | Enemies at stage 1 are absent by design, so the first stage may feel empty | Design | Fixed (v1.0.1) | Stage 1 now has one slow enemy as its third section (`Levels.Build`). |

## Player-reported bugs
Add here as reports come in (itch comments, social). Include device, browser and steps.

| ID | Report | Device / browser | Status |
|---|---|---|---|
| | | | |

## Editor-only noise (not game bugs)
- Console shows `NoSubscription` errors from `generators.ai.unity.com` — Unity AI (Muse) tools without a subscription. Harmless.
- Recompiling scripts while in Play mode throws NullReferenceExceptions in `Game` / `Backdrop` — the domain reload drops runtime-built objects. Stop Play mode before editing scripts.
- FMOD "failed to switch back to normal output" warnings come from the Mac audio device changing.
