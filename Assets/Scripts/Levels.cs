using System;
using System.Collections.Generic;
using UnityEngine;

// Everything about stage content: the theme of each world, the tile grid a stage is built on, and the generator
// that assembles the 10 stages. Game.cs reads a LevelGrid and turns it into GameObjects; nothing here touches the scene.

/// <summary>Silhouette shape of a parallax backdrop layer (see Backdrop.cs for how each shape is drawn).</summary>
public enum LayerShape { Hills, Mountains, Dunes, Spires, Volcano }

/// <summary>Kind of ambient particle drifting across the screen (see AmbientParticles in Backdrop.cs).</summary>
public enum AmbientKind { None, Snow, Leaves, Embers, Fireflies, Rain }

/// <summary>Which sky body (if any) is drawn in the backdrop.</summary>
public enum Celestial { None, Sun, Moon }

/// <summary>
/// The look of one world: sky gradient, mountain colors and shapes, sun or moon, particles, scenery and tint.
/// One theme is attached to each stage. To add a world, add an entry to <see cref="Levels.Themes"/>.
/// </summary>
public class StageTheme
{
    /// <summary>Display name shown on the stage banner.</summary>
    public string Name;

    /// <summary>Sky gradient: color at the top of the screen and at the horizon.</summary>
    public Color SkyTop, SkyBottom;

    /// <summary>Colors of the far (slow) and near (faster) parallax layers.</summary>
    public Color FarColor, NearColor;

    /// <summary>Silhouette shape of the far and near layers.</summary>
    public LayerShape FarShape, NearShape;

    /// <summary>Sun, moon or nothing, and its color.</summary>
    public Celestial Body;
    public Color BodyColor = new Color(1f, 0.92f, 0.5f);

    /// <summary>Draw a field of twinkling stars (night worlds).</summary>
    public bool Stars;

    /// <summary>Particles that drift across the screen (snow, leaves, embers, fireflies, rain).</summary>
    public AmbientKind Ambient;

    /// <summary>Multiplied onto ground and brick tiles and scenery so one tile sprite fits every world's palette.</summary>
    public Color Tint = Color.white;

    /// <summary>Multiplied onto the cloud sprites.</summary>
    public Color CloudTint = Color.white;

    /// <summary>Flash the screen now and then (final storm).</summary>
    public bool Lightning;

    /// <summary>Names of scenery props to scatter on the ground (see PixelArt.Decor).</summary>
    public string[] Decor = new string[0];
}

/// <summary>
/// A stage as a 2D grid of tiles. Coordinates are in tiles with (0,0) at the bottom-left; x grows right, y grows up.
/// Tile legend (one character per cell):
///   '.'  empty air          '#'  solid ground (grass on top, dirt below)    'B'  solid brick block
///   'c'  coin               '^'  spikes (hurt the player)                   'e'  enemy start position
/// Start and Exit are stored separately as tile positions.
/// The generator in <see cref="Levels"/> fills the grid through the Add... helpers below; Game.BuildLevel reads it back.
/// </summary>
public class LevelGrid
{
    /// <summary>Width in tiles. Changes when <see cref="Crop"/> trims the grid to the finished level.</summary>
    public int Width { get; private set; }

    /// <summary>Height in tiles (always <see cref="Levels.GridHeight"/>, which equals the camera's view height).</summary>
    public readonly int Height;

    /// <summary>Tile where the player spawns (standing on the ground).</summary>
    public Vector2Int Start;

    /// <summary>Tile where the goal flag stands.</summary>
    public Vector2Int Exit;

    /// <summary>Enemy walking speed for this stage in tiles per second (rises with the stage number).</summary>
    public float EnemySpeed = 2f;

    /// <summary>Visual theme of this stage.</summary>
    public StageTheme Theme;

    char[,] cells;

    public LevelGrid(int width, int height)
    {
        Width = width;
        Height = height;
        cells = new char[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = '.';
    }

    /// <summary>True if the tile coordinates lie inside the grid.</summary>
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Reads a cell; anything outside the grid reads as empty air.</summary>
    public char this[int x, int y] => InBounds(x, y) ? cells[x, y] : '.';

    /// <summary>True for tiles that block movement (ground and bricks).</summary>
    public bool IsSolid(int x, int y) { char c = this[x, y]; return c == '#' || c == 'B'; }

    // Writes a cell, silently ignoring out-of-range coordinates so chunks near the edge cannot crash the generator.
    void Set(int x, int y, char c) { if (InBounds(x, y)) cells[x, y] = c; }

    /// <summary>
    /// Trims the grid to the given width. The generator allocates a generous 600-wide grid, fills it,
    /// then crops to what it actually used.
    /// </summary>
    public void Crop(int width)
    {
        var next = new char[width, Height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < Height; y++)
                next[x, y] = cells[x, y];
        cells = next;
        Width = width;
    }

    /// <summary>Fills columns x0..x1 (inclusive) with solid ground, from the bottom up to the given thickness.</summary>
    public void AddGround(int x0, int x1, int thickness = 3)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = 0; y < thickness; y++)
                Set(x, y, '#');
    }

    /// <summary>Adds a w-by-h rectangle of brick blocks with its bottom-left tile at (x, y).</summary>
    public void AddBlock(int x, int y, int w = 1, int h = 1)
    {
        for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++)
                Set(x + i, y + j, 'B');
    }

    /// <summary>Places one coin in a tile.</summary>
    public void AddCoin(int x, int y) => Set(x, y, 'c');

    /// <summary>Places a horizontal row of coins from x0 to x1 (inclusive) at row y.</summary>
    public void AddCoins(int x0, int x1, int y) { for (int x = x0; x <= x1; x++) Set(x, y, 'c'); }

    /// <summary>Places an enemy; (x, y) is the tile the enemy stands in (its feet are at the bottom of that tile).</summary>
    public void AddEnemy(int x, int y) => Set(x, y, 'e');

    /// <summary>Places spikes from x0 to x1 (inclusive) in row y (default: standing on the ground).</summary>
    public void AddSpikes(int x0, int x1, int y = 3) { for (int x = x0; x <= x1; x++) Set(x, y, '^'); }

    public void SetStart(int x, int y) => Start = new Vector2Int(x, y);
    public void SetExit(int x, int y) => Exit = new Vector2Int(x, y);

    /// <summary>Counts the coins in the stage (shown as "coins collected / total" on the stage-clear panel).</summary>
    public int CountCoins()
    {
        int n = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (cells[x, y] == 'c') n++;
        return n;
    }
}

/// <summary>
/// The stage generator and the list of world themes.
///
/// Every stage is built from "chunks": short hand-designed pieces of level (a gap, a wall, a spike field, ...)
/// that each start and end on flat ground. Chunks are laid out left to right, so the player always has solid
/// footing to take off from and land on, which is what makes every generated stage completable.
/// The generator is DETERMINISTIC: each stage uses a fixed random seed, so a stage looks identical every time
/// it is played and across all players. Change the seed formula in <see cref="Build"/> to reshuffle the stages.
///
/// Difficulty scales with the stage number (1 to 10):
///   - number of chunks = 5 + stage (6 chunks in stage 1, 15 in stage 10), so later stages are longer;
///   - enemy speed = 1.5 + 0.14 * (stage - 1) tiles per second;
///   - new chunk types unlock over time (see <see cref="Pick"/>), and existing ones get wider or taller.
///
/// Reach of the hero, which the chunk sizes are tuned against (see PlayerController): jump apex about 3.8 tiles,
/// horizontal range of a full jump about 5 tiles at running speed. So gaps are at most 3 tiles wide (comfortable),
/// and walls and platforms at most 3 tiles high.
/// </summary>
public static class Levels
{
    /// <summary>Number of stages in the game.</summary>
    public const int Count = 10;

    /// <summary>Stage height in tiles. Equals the camera's view height (15), so the camera never scrolls vertically.</summary>
    public const int GridHeight = 15;

    // Row index of the tile just above the ground; the hero's feet rest on y = Surface.
    // Ground fills rows 0 to 2 (3 tiles thick), so the walking surface is at y = 3.
    const int Surface = 3;

    // The building blocks the generator can place. Each is implemented in Emit.
    enum Kind { Flat, Gap, StoneGap, Wall, Stairs, Spikes, Enemies, Platform, SpikeRun, Gauntlet, GapLanding }

    static Color Rgb(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

    // The 10 worlds, in play order. Stage N uses Themes[N - 1].
    static readonly StageTheme[] Themes =
    {
        new StageTheme
        {
            Name = "Sunny Meadow", SkyTop = Rgb(40, 140, 255), SkyBottom = Rgb(170, 232, 255),
            FarColor = Rgb(120, 205, 170), NearColor = Rgb(66, 190, 96), FarShape = LayerShape.Mountains, NearShape = LayerShape.Hills,
            Body = Celestial.Sun, Decor = new[] { "bush", "flowers", "flowers", "bush" },
        },
        new StageTheme
        {
            Name = "Cloud Hills", SkyTop = Rgb(70, 160, 255), SkyBottom = Rgb(210, 240, 255),
            FarColor = Rgb(150, 214, 200), NearColor = Rgb(96, 196, 150), FarShape = LayerShape.Hills, NearShape = LayerShape.Hills,
            Body = Celestial.Sun, Tint = new Color(0.94f, 1f, 0.96f), Decor = new[] { "bush", "flowers", "mushroom" },
        },
        new StageTheme
        {
            Name = "Sunset Run", SkyTop = Rgb(110, 60, 200), SkyBottom = Rgb(255, 176, 96),
            FarColor = Rgb(196, 90, 150), NearColor = Rgb(120, 52, 120), FarShape = LayerShape.Mountains, NearShape = LayerShape.Hills,
            Body = Celestial.Sun, BodyColor = Rgb(255, 150, 70), Tint = new Color(1f, 0.86f, 0.84f), CloudTint = Rgb(255, 196, 190),
            Decor = new[] { "bush", "flowers", "rock" },
        },
        new StageTheme
        {
            Name = "Autumn Trail", SkyTop = Rgb(255, 140, 56), SkyBottom = Rgb(255, 232, 150),
            FarColor = Rgb(214, 112, 52), NearColor = Rgb(168, 76, 40), FarShape = LayerShape.Hills, NearShape = LayerShape.Hills,
            Body = Celestial.Sun, Ambient = AmbientKind.Leaves, Tint = new Color(1f, 0.82f, 0.58f), CloudTint = Rgb(255, 240, 210),
            Decor = new[] { "bush", "mushroom", "rock" },
        },
        new StageTheme
        {
            Name = "Deep Cave", SkyTop = Rgb(24, 20, 78), SkyBottom = Rgb(86, 56, 160),
            FarColor = Rgb(100, 78, 200), NearColor = Rgb(56, 44, 130), FarShape = LayerShape.Spires, NearShape = LayerShape.Spires,
            Ambient = AmbientKind.Fireflies, Tint = new Color(0.78f, 0.82f, 1f), CloudTint = Rgb(120, 100, 200),
            Decor = new[] { "crystal", "mushroom", "crystal", "rock" },
        },
        new StageTheme
        {
            Name = "Frozen Peaks", SkyTop = Rgb(96, 188, 255), SkyBottom = Rgb(232, 250, 255),
            FarColor = Rgb(190, 216, 248), NearColor = Rgb(150, 190, 236), FarShape = LayerShape.Mountains, NearShape = LayerShape.Mountains,
            Body = Celestial.Sun, BodyColor = Rgb(255, 250, 220), Ambient = AmbientKind.Snow, Tint = new Color(0.84f, 0.94f, 1f), CloudTint = Color.white,
            Decor = new[] { "pine", "rock", "pine", "pine" },
        },
        new StageTheme
        {
            Name = "Dusty Desert", SkyTop = Rgb(255, 160, 50), SkyBottom = Rgb(255, 238, 176),
            FarColor = Rgb(244, 192, 108), NearColor = Rgb(224, 150, 76), FarShape = LayerShape.Dunes, NearShape = LayerShape.Dunes,
            Body = Celestial.Sun, BodyColor = Rgb(255, 250, 200), Tint = new Color(1f, 0.92f, 0.64f), CloudTint = Rgb(255, 244, 220),
            Decor = new[] { "cactus", "rock", "cactus" },
        },
        new StageTheme
        {
            Name = "Midnight", SkyTop = Rgb(6, 8, 52), SkyBottom = Rgb(70, 44, 140),
            FarColor = Rgb(40, 52, 130), NearColor = Rgb(24, 32, 90), FarShape = LayerShape.Mountains, NearShape = LayerShape.Hills,
            Body = Celestial.Moon, BodyColor = Rgb(236, 240, 255), Stars = true, Ambient = AmbientKind.Fireflies,
            Tint = new Color(0.6f, 0.66f, 0.92f), CloudTint = Rgb(90, 96, 170), Decor = new[] { "bush", "mushroom", "rock" },
        },
        new StageTheme
        {
            Name = "Lava Fields", SkyTop = Rgb(60, 8, 26), SkyBottom = Rgb(240, 84, 36),
            FarColor = Rgb(130, 34, 40), NearColor = Rgb(84, 24, 32), FarShape = LayerShape.Volcano, NearShape = LayerShape.Mountains,
            Ambient = AmbientKind.Embers, Tint = new Color(1f, 0.62f, 0.5f), CloudTint = Rgb(150, 56, 50),
            Decor = new[] { "lavarock", "rock", "lavarock" },
        },
        new StageTheme
        {
            Name = "Final Storm", SkyTop = Rgb(18, 8, 56), SkyBottom = Rgb(110, 50, 170),
            FarColor = Rgb(70, 36, 130), NearColor = Rgb(44, 22, 90), FarShape = LayerShape.Spires, NearShape = LayerShape.Mountains,
            Ambient = AmbientKind.Rain, Lightning = true, Tint = new Color(0.76f, 0.6f, 0.96f), CloudTint = Rgb(96, 70, 150),
            Decor = new[] { "crystal", "rock", "crystal" },
        },
    };

    /// <summary>Returns the theme of a stage by zero-based index (used by the branding art tools too).</summary>
    public static StageTheme ThemeFor(int index) => Themes[index];

    /// <summary>
    /// Generates one complete stage.
    /// </summary>
    /// <param name="index">Zero-based stage index (0 = stage 1).</param>
    /// <returns>A finished, cropped <see cref="LevelGrid"/> with start, exit, theme and enemy speed set.</returns>
    public static LevelGrid Build(int index)
    {
        int stage = index + 1;

        // Fixed seed per stage => the same stage every time. 977 spreads the seeds so neighbouring stages differ.
        var rng = new System.Random(7001 + index * 977);

        // Allocate far more width than ever needed (stage 10 is about 220 tiles); Crop trims it at the end.
        var g = new LevelGrid(600, GridHeight)
        {
            Theme = Themes[index],
            EnemySpeed = 1.5f + 0.14f * index
        };

        // Opening: 11 tiles of flat ground, the player starts on tile 3, with a row of coins to walk through.
        g.AddGround(0, 10);
        g.SetStart(3, Surface);
        g.AddCoins(6, 9, Surface);
        int x = 11; // next free column; each chunk starts here and returns the new value

        int chunks = 5 + stage;
        Kind last = Kind.Flat;
        int hazardRun = 0; // number of non-flat chunks in a row
        for (int i = 0; i < chunks; i++)
        {
            // After 3 hazardous chunks in a row, force a breather (flat ground with coins).
            Kind kind = hazardRun >= 3 ? Kind.Flat : Pick(stage, rng);

            // Avoid repeating the same hazard twice in a row (up to 3 re-rolls); flat repeats are fine.
            for (int tries = 0; tries < 3 && kind == last && kind != Kind.Flat; tries++)
                kind = Pick(stage, rng);

            hazardRun = kind == Kind.Flat ? 0 : hazardRun + 1;
            x = Emit(g, kind, x, stage, rng);
            last = kind;
        }

        // Finish: flat ground with a few coins and the goal flag, then trim the grid.
        g.AddGround(x, x + 11);
        g.AddCoins(x + 1, x + 4, Surface);
        g.SetExit(x + 8, Surface);
        g.Crop(x + 13);
        return g;
    }

    // Chooses a random chunk type from the ones unlocked at this stage. Flat, Gap and Wall are always available;
    // new types are introduced gradually so each stage teaches or combines something.
    //   stage 1: flat, gap, wall            stage 5: + stone gap
    //   stage 2: + enemies, spikes          stage 6: + gap with an enemy waiting at the landing
    //   stage 3: + stairs                   stage 7: + spike run (two spike fields in a row)
    //   stage 4: + floating platform        stage 8: + gauntlet (spikes, enemy, spikes)
    static Kind Pick(int stage, System.Random rng)
    {
        var options = new List<Kind> { Kind.Flat, Kind.Gap, Kind.Wall };
        if (stage >= 2) { options.Add(Kind.Enemies); options.Add(Kind.Spikes); }
        if (stage >= 3) options.Add(Kind.Stairs);
        if (stage >= 4) options.Add(Kind.Platform);
        if (stage >= 5) options.Add(Kind.StoneGap);
        if (stage >= 6) options.Add(Kind.GapLanding);
        if (stage >= 7) options.Add(Kind.SpikeRun);
        if (stage >= 8) options.Add(Kind.Gauntlet);
        return options[rng.Next(options.Count)];
    }

    // Writes one chunk into the grid starting at column x and returns the first free column after it.
    // Every chunk begins and ends with solid ground so chunks can be glued together in any order.
    // In the layouts below: '#' ground, '^' spikes, 'B' bricks, 'e' enemy, 'c' coins, '_' gap.
    static int Emit(LevelGrid g, Kind kind, int x, int stage, System.Random rng)
    {
        switch (kind)
        {
            // FLAT: 5 to 8 tiles of ground. 70%: coins to walk through; 30%: coins in the air to jump for.
            case Kind.Flat:
            {
                int len = rng.Next(5, 9);
                g.AddGround(x, x + len - 1);
                if (rng.NextDouble() < 0.7) g.AddCoins(x + 1, x + len - 2, Surface);
                else g.AddCoins(x + 2, x + len - 3, Surface + 2);
                return x + len;
            }

            // GAP: ###___### a pit 2 tiles wide (stage 1-2) or 2 to 3 wide (stage 3+) with 3 flat tiles on each
            // side. A coin arc over the pit marks the jump path.
            case Kind.Gap:
            {
                int w = stage >= 3 ? rng.Next(2, 4) : 2;
                g.AddGround(x, x + 2);
                int gx = x + 3;
                g.AddGround(gx + w, gx + w + 2);
                g.AddCoins(gx, gx + w - 1, Surface + 3);
                g.AddCoin(gx - 1, Surface + 2);
                g.AddCoin(gx + w, Surface + 2);
                return gx + w + 3;
            }

            // STONE GAP (stage 5+): ###___BBB__### a long pit crossed by a 3-wide floating brick stone whose top is level
            // with the ground. The first gap is always 3 wide, the second 2 to 3.
            case Kind.StoneGap:
            {
                int a = 3, b = rng.Next(2, 4);
                g.AddGround(x, x + 2);
                int sx = x + 3 + a;
                g.AddBlock(sx, Surface - 1, 3, 1);
                g.AddCoins(sx, sx + 2, Surface);
                g.AddCoins(x + 3, x + 2 + a, Surface + 3);
                g.AddCoins(sx + 3, sx + 2 + b, Surface + 3);
                int end = sx + 3 + b;
                g.AddGround(end, end + 2);
                return end + 3;
            }

            // WALL: a one-tile-wide brick wall in the middle of 9 flat tiles, to jump over.
            // Height grows with the stage: 1 tile (stages 1-2), up to 2 (3-5), up to 3 (6+).
            case Kind.Wall:
            {
                int maxH = Mathf.Min(3, 1 + stage / 3);
                int h = rng.Next(1, maxH + 1);
                g.AddGround(x, x + 8);
                g.AddBlock(x + 4, Surface, 1, h);
                g.AddCoin(x + 4, Surface + h + 1);
                g.AddCoin(x + 3, Surface + h);
                g.AddCoin(x + 5, Surface + h);
                return x + 9;
            }

            // STAIRS (stage 3+): steps up (1, 2 [, 3] tiles high), a 3-wide plateau with coins on top, steps down.
            // 2 steps normally, 3 steps from stage 6. Ground runs underneath the whole chunk.
            case Kind.Stairs:
            {
                int n = stage >= 6 ? 3 : 2;
                int a = x + 2;
                for (int i = 0; i < n; i++) g.AddBlock(a + i, Surface, 1, i + 1);
                int px = a + n;
                for (int j = 0; j < 3; j++)
                {
                    g.AddBlock(px + j, Surface, 1, n);
                    g.AddCoin(px + j, Surface + n);
                }
                int dx = px + 3;
                for (int i = 0; i < n - 1; i++) g.AddBlock(dx + i, Surface, 1, n - 1 - i);
                int end = dx + (n - 1);
                g.AddGround(x, end + 1);
                return end + 2;
            }

            // SPIKES (stage 2+): ###^^### a field of 2 spikes (2 to 3 from stage 6) with 3 flat tiles each side.
            // A coin arc above shows the jump.
            case Kind.Spikes:
            {
                int w = stage >= 6 ? rng.Next(2, 4) : 2;
                g.AddGround(x, x + w + 5);
                g.AddSpikes(x + 3, x + 2 + w);
                g.AddCoins(x + 3, x + 2 + w, Surface + 3);
                g.AddCoin(x + 2, Surface + 2);
                g.AddCoin(x + 3 + w, Surface + 2);
                return x + w + 6;
            }

            // ENEMIES (stage 2+): flat ground with 1 enemy (2 from stage 4 by chance, +1 from stage 7), spaced 4 tiles
            // apart. The ground gets longer with more enemies so there is room to stomp and bounce.
            case Kind.Enemies:
            {
                int n = 1 + (stage >= 4 ? rng.Next(0, 2) : 0) + (stage >= 7 ? 1 : 0);
                int len = 6 + n * 4;
                g.AddGround(x, x + len - 1);
                g.AddCoins(x + 1, x + 3, Surface);
                for (int i = 0; i < n; i++) g.AddEnemy(x + 5 + i * 4, Surface);
                return x + len;
            }

            // PLATFORM (stage 4+): a floating 3-wide brick platform with coins on top, over 12 flat tiles.
            // Low (2 tiles above the walking surface to stand on) before stage 5, higher (3 tiles) after.
            // From stage 6 an enemy patrols underneath the high platform.
            case Kind.Platform:
            {
                int py = stage >= 5 ? Surface + 2 : Surface + 1;
                g.AddGround(x, x + 11);
                g.AddBlock(x + 4, py, 3, 1);
                g.AddCoins(x + 4, x + 6, py + 1);
                if (stage >= 6 && py == Surface + 2) g.AddEnemy(x + 5, Surface);
                return x + 12;
            }

            // SPIKE RUN (stage 7+): ###^^####^^### two spike fields separated by 4 flat tiles; the player must land
            // between them and jump again right away.
            case Kind.SpikeRun:
            {
                g.AddGround(x, x + 13);
                g.AddSpikes(x + 3, x + 4);
                g.AddSpikes(x + 9, x + 10);
                g.AddCoins(x + 3, x + 4, Surface + 3);
                g.AddCoins(x + 9, x + 10, Surface + 3);
                g.AddCoins(x + 6, x + 7, Surface);
                return x + 14;
            }

            // GAUNTLET (stage 8+): ###^^##e##^^### spikes, an enemy patrolling the middle, spikes again.
            case Kind.Gauntlet:
            {
                g.AddGround(x, x + 14);
                g.AddSpikes(x + 3, x + 4);
                g.AddSpikes(x + 10, x + 11);
                g.AddEnemy(x + 7, Surface);
                g.AddCoins(x + 3, x + 4, Surface + 3);
                g.AddCoins(x + 10, x + 11, Surface + 3);
                return x + 15;
            }

            // GAP LANDING (stage 6+): ###___########e a 3-wide pit followed by 8 flat tiles with an enemy waiting
            // near the far end, so the player has to land and immediately deal with it.
            case Kind.GapLanding:
            {
                g.AddGround(x, x + 2);
                int gx = x + 3;
                g.AddGround(gx + 3, gx + 10);
                g.AddCoins(gx, gx + 2, Surface + 3);
                g.AddEnemy(gx + 9, Surface);
                return gx + 11;
            }
        }
        return x;
    }
}
