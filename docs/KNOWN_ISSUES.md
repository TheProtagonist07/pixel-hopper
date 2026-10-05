# Known issues

Keep this list current. When something is fixed, change its status, note the version, and add a line to `CHANGELOG.md`.

Status: **Open**, **Planned** (scheduled), **Fixed (vX.Y.Z)**, **Won't fix**.

| ID | Issue | Severity | Status | Notes / fix |
|---|---|---|---|---|
| KI-001 | No sound on iPhone when the silent switch is on | Low | Open | Not a bug, iOS mutes web audio in silent mode. Optional fix: set `navigator.audioSession.type = 'playback'` in `Assets/WebGLTemplates/PixelHopper/index.html`, rebuild, re-upload. Mention in the itch description meanwhile. |
| KI-002 | Embed frame (540 x 960) is taller than many laptop screens, so the lower part of the game is below the fold | Medium | Open | itch setting: use "Click to launch in fullscreen" if offered, or lower the viewport to 405 x 720 (same 9:16 shape). |
| KI-003 | Description typo: "3 lives per stage , instant retry" (stray space) | Trivial | Open | Edit the itch page text. |
| KI-004 | First Space press can be ignored until the game canvas has keyboard focus | Low | Open | Players click first; consider showing "Click to focus" hint. |
| KI-005 | Stage 10 is hard (test bot needed 6 deaths); other stages are fine | Design | Planned | Wait for player feedback, then tune chunk mix or lives. |
| KI-006 | Enemies at stage 1 are absent by design, so the first stage may feel empty | Design | Planned | Consider a single slow enemy in stage 1. |

## Player-reported bugs
Add here as reports come in (itch comments, social). Include device, browser and steps.

| ID | Report | Device / browser | Status |
|---|---|---|---|
| | | | |

## Editor-only noise (not game bugs)
- Console shows `NoSubscription` errors from `generators.ai.unity.com` — Unity AI (Muse) tools without a subscription. Harmless.
- Recompiling scripts while in Play mode throws NullReferenceExceptions in `Game` / `Backdrop` — the domain reload drops runtime-built objects. Stop Play mode before editing scripts.
- FMOD "failed to switch back to normal output" warnings come from the Mac audio device changing.
