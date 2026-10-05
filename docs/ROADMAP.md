# Roadmap

Living plan for Pixel Hopper. Tick items off, move them between sections, and note the version in `CHANGELOG.md` when they ship.

## Vision
Start as a free browser game on itch.io, learn from real players, then grow it into a replayable score-chasing game with a community, and test a mobile release. Keep each step small and measure before investing more.

## 1. Next update (v1.0.1) — fixes
- [ ] Fix itch embed height (see KI-002)
- [ ] Fix description typo (KI-003)
- [ ] iPhone silent-mode audio fix (KI-001), optional
- [ ] Triage player-reported bugs (see `docs/KNOWN_ISSUES.md`)

## 2. Gameplay depth (v1.1 – v1.3)
Your list, plus suggestions. Order is a recommendation.

**Score and time (do first, it unlocks everything social)**
- [ ] Score system: points per coin, per stomp, combo multiplier for chained stomps, stage-clear bonus
- [ ] Stage timer with a time bonus and a best-time per stage (stored locally with PlayerPrefs)
- [ ] Results panel at stage clear: score, time, coins, new-best marker
- [ ] Local high-score list

**Difficulty modes**
- [ ] Easy: more lives, slower enemies, wider landing spaces
- [ ] Normal: current balance
- [ ] Hard: 1 life per stage, faster enemies, extra spikes
- [ ] "Slow" option as an accessibility mode (game speed scale)

**More enemies**
- [ ] Flying enemy (sine-wave flight, can be stomped)
- [ ] Spiky enemy that cannot be stomped (must be jumped over)
- [ ] Shooter / projectile enemy for late stages
- [ ] Boss on stage 10

**More characters**
- [ ] Character select on the title screen
- [ ] 2–3 extra heroes with small stat differences (jump height, speed, stomp bounce)
- [ ] Unlock by total coins or stages cleared

**Other ideas**
- [ ] Endless / daily-challenge mode using the chunk generator with a daily seed (great for leaderboards and replay)
- [ ] Checkpoints on long stages
- [ ] Power-ups (shield, double jump, coin magnet)
- [ ] Achievements

## 3. Community and leaderboards
- [ ] Decide on a backend for online scores (Supabase or Firebase are free to start; itch has no built-in score API for HTML builds)
- [ ] Submit score + time per stage, daily-challenge board
- [ ] Anti-cheat basics (server-side sanity checks, replay seed)
- [ ] Share card / screenshot export of a run
- [ ] Discord or itch community post for high scores

## 4. Mobile stores (Android first, then iOS)
Honest view, written so we can decide with data:

**Is it possible?** Yes. Unity builds the same project for Android and iOS. The generated levels and procedural art/audio make content cheap to extend.

**What it takes**
- Android: Google Play developer account (one-time fee, about $25). New personal accounts may need a closed test with testers before going to production, so check Google's current rules. Build an AAB, make an icon, store listing, screenshots, privacy policy URL, content rating.
- iOS: Apple Developer Program (about $99 per year), a Mac with Xcode (you have this), TestFlight beta, App Store review. Apple rejects apps with too little content or polish, so more depth than v1.0 helps.
- Both: safe-area handling for notches, app icon sets, store screenshots, privacy policy, age rating, ad-consent screens if you add ads (GDPR / App Tracking Transparency).

**Revenue, realistically**
- Most mobile games earn very little without strong retention and paid user acquisition. A short 10-stage game on its own will likely earn close to nothing organically.
- Better odds come from replayability (score chasing, daily challenge, characters) plus light monetization: rewarded ads ("continue after losing"), interstitials between stages, a "remove ads" purchase, cosmetic characters.
- Treat the first store release mainly as learning and portfolio value, and let numbers decide whether to invest more.

**Path that limits risk**
1. Add score, timer, modes, more enemies/characters (section 2) and watch itch metrics.
2. Soft-launch on Android (cheaper and faster review) with an ad SDK (Unity LevelPlay or AdMob).
3. Measure day-1 and day-7 retention and revenue per user. Healthy early signs: day-1 retention above ~30%, day-7 above ~8%.
4. Only then spend on iOS and any marketing.

**Technical to-dos for mobile**
- [ ] Android and iOS build targets installed in Unity Hub
- [ ] Touch UI polish and safe areas
- [ ] Performance pass (draw calls, GC) on a mid-range phone
- [ ] Save system (progress, best scores, unlocked characters)
- [ ] Analytics events (stage reached, deaths, quits)
- [ ] Privacy policy page and consent flow

## 5. Housekeeping
- [ ] Choose a license for the repo
- [ ] Rename repo and local folder (currently still "First One")
- [ ] Commit and push game code to GitHub (only the empty template commit is there as of 2026-10-05)
- [ ] Keep `CHANGELOG.md`, `docs/KNOWN_ISSUES.md` and this roadmap updated with every change
