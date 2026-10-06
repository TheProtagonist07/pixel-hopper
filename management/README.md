# Management

How Pixel Hopper is run. The plan itself lives in [docs/ROADMAP.md](../docs/ROADMAP.md); history lives in [CHANGELOG.md](../CHANGELOG.md); this folder is the control panel.

## Way of working
- **Small updates, one focus each**, released regularly (see ROADMAP section 0). Each update has two lists in the changelog: what changed for players, and which code changed.
- **Branch and pull request per update**: `update/<version>-<topic>`, merged to `main` through a pull request, tagged `vX.Y.Z`.
- **Checks before shipping**: compile, test bot, rule checks, stage report, build, play the build once ([docs/TESTING.md](../docs/TESTING.md)).
- **Everything in one place**: [docs/PROJECT_LAYOUT.md](../docs/PROJECT_LAYOUT.md).
- Commits and pull requests carry only the owner's name.

## Roles
| Who | Does |
|---|---|
| Owner | product decisions, uploads to itch.io, itch page and posts, testing on real devices, final say on every change |
| Engineering (code and docs) | implements updates, tests, builds, writes changelog and docs |

## Status board
| Item | State |
|---|---|
| Current release | see [release-log.md](release-log.md) |
| Next updates | see ROADMAP section 0 (v1.1.1 iPhone silent-mode sound, then the gun pickup, new enemies, score and timer) |
| Open issues | [docs/KNOWN_ISSUES.md](../docs/KNOWN_ISSUES.md) |
| Parked work | the blaster prototype is on the local branch `feature/blaster` |

## Update checklist
Copy this into the pull request description of each update.
- [ ] One focus point, branch `update/<version>-<topic>`
- [ ] Changelog entry with "What changed" and "Code changes"
- [ ] Docs updated, links checked
- [ ] Version bumped (Project Settings)
- [ ] Bot, rule checks and stage report run
- [ ] Build made with `ReleaseBuild`, played once in a browser
- [ ] Screenshots in `marketing/screenshots/vX.Y.Z/`
- [ ] Pull request merged, tag pushed, zip attached to the GitHub Release
- [ ] Uploaded to itch.io, devlog posted, row added to the release log
