# Pixel Hopper documentation

Welcome. This folder explains **everything** about the project: what it is, how it is built, why it is built that way,
and how to change, test, build and ship it. If you only read one thing, read [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md).

## Reading paths

| I want to... | Read |
|---|---|
| Explain the project to someone in 5 minutes | [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) |
| Understand how the code fits together | [ARCHITECTURE.md](ARCHITECTURE.md), then [SCRIPTS.md](SCRIPTS.md) |
| Find out what a specific script or class does | [SCRIPTS.md](SCRIPTS.md) (and the comments inside the script) |
| Add or change a stage, enemy placement or world | [LEVEL_DESIGN.md](LEVEL_DESIGN.md) |
| Draw a new sprite or change the look | [ART_PIPELINE.md](ART_PIPELINE.md) |
| Change sounds or music | [AUDIO.md](AUDIO.md) |
| Change the HUD, dialogs, fonts or logo | [UI_SYSTEM.md](UI_SYSTEM.md) |
| Check that I did not break the game | [TESTING.md](TESTING.md) |
| Build the web version and publish on itch.io | [WEBGL_BUILD_AND_ITCH.md](WEBGL_BUILD_AND_ITCH.md) |
| Regenerate the logo, cover and page art | [BRANDING_AND_PAGE_ART.md](BRANDING_AND_PAGE_ART.md) |
| Find where a file lives, or what is backed up | [PROJECT_LAYOUT.md](PROJECT_LAYOUT.md) |
| Know why something was done a certain way | [DECISIONS.md](DECISIONS.md) |
| Look up a term (PPU, parallax, coyote time, 9-slice...) | [GLOSSARY.md](GLOSSARY.md) |
| See what is planned or broken | [ROADMAP.md](ROADMAP.md), [KNOWN_ISSUES.md](KNOWN_ISSUES.md) |
| Practical build and release checklist | [DEV_NOTES.md](DEV_NOTES.md) |
| Reuse the store page text | [itch-page-copy.md](itch-page-copy.md) |

## All documents

| File | Contents |
|---|---|
| [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) | The pitch, the numbers, the tech stack, what is unusual about this project |
| [ARCHITECTURE.md](ARCHITECTURE.md) | State machine, startup flow, how a stage is built, runtime object tree, diagrams |
| [SCRIPTS.md](SCRIPTS.md) | One section per C# file: purpose, main members, who calls it |
| [LEVEL_DESIGN.md](LEVEL_DESIGN.md) | Tile legend, chunk generator, difficulty table, the 10 worlds, jump reach |
| [ART_PIPELINE.md](ART_PIPELINE.md) | PixelPainter lighting model, sprite atlas, backdrops, how to add art |
| [AUDIO.md](AUDIO.md) | Synthesised sound effects and the music loop |
| [UI_SYSTEM.md](UI_SYSTEM.md) | Pixel font, UiKit, UiCanvas, layout rules, touch and keyboard input |
| [TESTING.md](TESTING.md) | The test bot, screenshot and video helpers, manual test checklist |
| [WEBGL_BUILD_AND_ITCH.md](WEBGL_BUILD_AND_ITCH.md) | Build settings, the page template, zip packaging, itch.io page setup |
| [BRANDING_AND_PAGE_ART.md](BRANDING_AND_PAGE_ART.md) | The CoverArt editor tool and every image it produces |
| [PROJECT_LAYOUT.md](PROJECT_LAYOUT.md) | Folder map, what is in Git, backups |
| [DECISIONS.md](DECISIONS.md) | Design decision log: what, why, and what was rejected |
| [GLOSSARY.md](GLOSSARY.md) | Short definitions of the technical terms used everywhere |
| [ROADMAP.md](ROADMAP.md) | Planned updates, wish list, mobile store plan |
| [KNOWN_ISSUES.md](KNOWN_ISSUES.md) | Open bugs and their status |
| [DEV_NOTES.md](DEV_NOTES.md) | Short practical notes for building and releasing |
| [itch-page-copy.md](itch-page-copy.md) | Text, tags and settings for the itch.io page |

## Documentation rules (keep these true)

1. **Code comments explain the code.** Every script starts with a summary of its purpose, and non-obvious lines say *why*.
2. **These files explain the system.** They describe how scripts work together and why choices were made.
3. **Every change updates the docs.** Fixing a bug: update [KNOWN_ISSUES.md](KNOWN_ISSUES.md) and [../CHANGELOG.md](../CHANGELOG.md).
   Adding a feature: update the relevant doc here, [ROADMAP.md](ROADMAP.md), and the changelog.
4. **A comment-only commit must not change code.** Before committing comment edits, compare the code with comments
   stripped (see [TESTING.md](TESTING.md), "Verifying a comment-only change").
