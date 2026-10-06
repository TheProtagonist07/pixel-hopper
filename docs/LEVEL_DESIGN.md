# Level design

All stage content comes from `Assets/Scripts/Levels.cs`. There are no level files: each stage is generated
by code, **the same way every time** (fixed random seeds), so every player sees the same 10 stages.

## The tile grid

A stage is a `LevelGrid`: a 2D array of characters. Coordinates are in tiles, `(0,0)` at the bottom-left,
x to the right, y upward. One tile is one world unit (32 pixels). The grid is always 15 tiles tall, which equals the
camera's view height, so the camera never scrolls vertically.

| Char | Meaning | Created as |
|---|---|---|
| `.` | empty air | nothing |
| `#` | solid ground (grass on top, dirt below) | tile sprite + merged collider |
| `B` | solid brick block | tile sprite + merged collider |
| `c` | coin | `Coin` object |
| `^` | spikes | `Hazard` object |
| `e` | enemy start (feet at the bottom of the cell) | `Enemy` object |

Start and exit are stored separately as tile positions. Ground is 3 tiles thick (rows 0 to 2), so the walking surface is row 3
(`Surface`).

## The chunk generator

Each stage is assembled from **chunks**: short, hand-designed pieces (a gap, a wall, a spike field...) that
each **start and end on flat ground**. Laying them left to right guarantees there is always solid footing to take off from
and to land on, which is why every generated stage can be finished.

```
Build(stage):
  rng = Random(7001 + index * 977)            # fixed seed per stage
  opening: 11 tiles of ground, start on tile 3, 4 coins to walk through
  repeat (5 + stage) times:
      pick a chunk type unlocked at this stage
      stage 1 only: chunk 3 is always an enemy chunk (one slow enemy)
      after 3 non-flat chunks in a row, force a flat "breather"
      avoid the same hazard twice in a row (up to 3 re-rolls)
      emit the chunk, advance x
  finish: 12 tiles of ground, 4 coins, goal flag, crop the grid
```

The grid is allocated 600 tiles wide (far more than needed; stage 10 is about 220) and cropped to the used width at the end.

### Chunk catalogue

Legend in the pictures: `#` ground, `_` pit, `^` spikes, `B` brick, `e` enemy, `c` coins above.

| Chunk | Looks like | Unlocks | Notes |
|---|---|---|---|
| Flat | `#####` to `########` | stage 1 | 5 to 8 tiles; 70% coins on the ground, 30% coins in the air |
| Gap | `###__###` | stage 1 | 2 wide; 2 to 3 wide from stage 3; coin arc marks the jump |
| Wall | `####B####` | stage 1 | 1 tile wide; height 1 (stage 1-2), up to 2 (3-5), up to 3 (6+) |
| Enemies | `#e##e##` | stage 2 | 1 enemy; up to 2 from stage 4 (random); +1 from stage 7; spaced 4 tiles apart |
| Spikes | `###^^###` | stage 2 | 2 spikes; 2 to 3 from stage 6 |
| Stairs | up 1-2(-3), plateau of 3, down | stage 3 | 2 steps; 3 steps from stage 6 |
| Platform | floating 3-wide brick over ground | stage 4 | low (1 above surface) before stage 5, then 2 above; an enemy patrols below from stage 6 |
| Stone gap | `###___BBB__###` | stage 5 | 3-wide pit, floating stone level with the ground, then 2-3 pit |
| Gap landing | `###___########e` | stage 6 | 3-wide pit then an enemy waiting at the far end |
| Spike run | `###^^####^^###` | stage 7 | two spike fields 4 tiles apart; land between and jump again |
| Gauntlet | `###^^##e##^^###` | stage 8 | spikes, patrolling enemy, spikes |

### Difficulty by stage

| Stage | World | Chunks | Enemy speed (tiles/s) | New in this stage |
|---|---|---|---|---|
| 1 | Sunny Meadow | 6 | 1.50 | flat, gap, wall, plus one guaranteed enemy as chunk 3 (since v1.0.1) |
| 2 | Cloud Hills | 7 | 1.64 | enemies, spikes |
| 3 | Sunset Run | 8 | 1.78 | stairs, gaps up to 3 wide, walls up to 2 |
| 4 | Autumn Trail | 9 | 1.92 | floating platform, 1 to 2 enemies |
| 5 | Deep Cave | 10 | 2.06 | stone gap, higher platform |
| 6 | Frozen Peaks | 11 | 2.20 | gap landing, walls up to 3, 3 steps, wider spikes |
| 7 | Dusty Desert | 12 | 2.34 | spike run, extra enemy per enemy chunk |
| 8 | Midnight | 13 | 2.48 | gauntlet |
| 9 | Lava Fields | 14 | 2.62 | (everything combined) |
| 10 | Final Storm | 15 | 2.76 | (everything combined, longest) |

Formulas: chunks = `5 + stage`, enemy speed = `1.5 + 0.14 * (stage - 1)`.

## What the hero can do (why the chunk sizes are what they are)

From `PlayerController`: jump launch speed 15 at gravity scale 3 gives an apex of about 3.8 tiles; with run speed 5.5 a full jump
covers about 5 tiles horizontally (a little more than that when falling to a lower platform). Chunks are kept comfortably
inside that: pits are at most 3 wide, walls and platforms at most 3 high. Coyote time (0.1 s) and jump buffering (0.12 s)
add forgiveness. If you change jump physics, **re-run the test bot** ([TESTING.md](TESTING.md)).

## The 10 worlds (themes)

Each stage has a `StageTheme` (list `Themes` in `Levels.cs`, stage N uses entry N-1).

| # | Name | Sky | Scenery (far / near) | Extras |
|---|---|---|---|---|
| 1 | Sunny Meadow | blue | mountains with snow caps / hills | sun; bushes, flowers |
| 2 | Cloud Hills | light blue | hills / hills | sun; mushrooms |
| 3 | Sunset Run | purple to orange | mountains / hills | orange sun, pink clouds; rocks |
| 4 | Autumn Trail | orange to cream | hills / hills | falling leaves |
| 5 | Deep Cave | deep indigo | spires / spires | fireflies; crystals |
| 6 | Frozen Peaks | icy blue | snowy mountains / mountains | snowfall; pines |
| 7 | Dusty Desert | amber | dunes / dunes | cacti |
| 8 | Midnight | navy to purple | mountains / hills | moon, stars, fireflies |
| 9 | Lava Fields | dark red to orange | volcanoes (lava glow) / mountains | rising embers; lava rocks |
| 10 | Final Storm | purple storm | spires / mountains | rain and lightning flashes; crystals |

A theme also has a `Tint` multiplied onto ground, brick and scenery so one set of tile sprites fits every palette.

## How to change things

| I want to... | Do this |
|---|---|
| Reshuffle all stages | Change the seed formula `7001 + index * 977` in `Levels.Build` |
| Make stages longer or shorter | Change `chunks = 5 + stage` |
| Make enemies faster or slower | Change `EnemySpeed = 1.5f + 0.14f * index` |
| Move or remove the stage 1 enemy | Edit the line `if (stage == 1 && i == 2) kind = Kind.Enemies;` in `Levels.Build` (done in v1.0.1 for KI-006) |
| Add a new world | Add a `StageTheme` to `Themes` and raise `Levels.Count` |
| Add a new chunk type | Add it to the `Kind` enum, `Pick` (unlock rule) and `Emit` (its layout). Keep it starting and ending on ground. |
| Add a hand-built stage | Build a `LevelGrid` manually with the `Add...` helpers and return it from `Build` for that index |

After any change: run the bot over all stages and watch the death counts.
