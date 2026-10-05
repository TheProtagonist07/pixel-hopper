using System;
using System.Collections.Generic;
using UnityEngine;

public enum LayerShape { Hills, Mountains, Dunes, Spires, Volcano }
public enum AmbientKind { None, Snow, Leaves, Embers, Fireflies, Rain }
public enum Celestial { None, Sun, Moon }

public class StageTheme
{
    public string Name;
    public Color SkyTop, SkyBottom;
    public Color FarColor, NearColor;
    public LayerShape FarShape, NearShape;
    public Celestial Body;
    public Color BodyColor = new Color(1f, 0.92f, 0.5f);
    public bool Stars;
    public AmbientKind Ambient;
    public Color Tint = Color.white;
    public Color CloudTint = Color.white;
    public bool Lightning;
    public string[] Decor = new string[0];
}

public class LevelGrid
{
    public int Width { get; private set; }
    public readonly int Height;
    public Vector2Int Start;
    public Vector2Int Exit;
    public float EnemySpeed = 2f;
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

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public char this[int x, int y] => InBounds(x, y) ? cells[x, y] : '.';
    public bool IsSolid(int x, int y) { char c = this[x, y]; return c == '#' || c == 'B'; }

    void Set(int x, int y, char c) { if (InBounds(x, y)) cells[x, y] = c; }

    public void Crop(int width)
    {
        var next = new char[width, Height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < Height; y++)
                next[x, y] = cells[x, y];
        cells = next;
        Width = width;
    }

    public void AddGround(int x0, int x1, int thickness = 3)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = 0; y < thickness; y++)
                Set(x, y, '#');
    }

    public void AddBlock(int x, int y, int w = 1, int h = 1)
    {
        for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++)
                Set(x + i, y + j, 'B');
    }

    public void AddCoin(int x, int y) => Set(x, y, 'c');
    public void AddCoins(int x0, int x1, int y) { for (int x = x0; x <= x1; x++) Set(x, y, 'c'); }
    public void AddEnemy(int x, int y) => Set(x, y, 'e');
    public void AddSpikes(int x0, int x1, int y = 3) { for (int x = x0; x <= x1; x++) Set(x, y, '^'); }
    public void SetStart(int x, int y) => Start = new Vector2Int(x, y);
    public void SetExit(int x, int y) => Exit = new Vector2Int(x, y);

    public int CountCoins()
    {
        int n = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (cells[x, y] == 'c') n++;
        return n;
    }
}

public static class Levels
{
    public const int Count = 10;
    public const int GridHeight = 15;
    const int Surface = 3;

    enum Kind { Flat, Gap, StoneGap, Wall, Stairs, Spikes, Enemies, Platform, SpikeRun, Gauntlet, GapLanding }

    static Color Rgb(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

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

    public static StageTheme ThemeFor(int index) => Themes[index];

    public static LevelGrid Build(int index)
    {
        int stage = index + 1;
        var rng = new System.Random(7001 + index * 977);
        var g = new LevelGrid(600, GridHeight)
        {
            Theme = Themes[index],
            EnemySpeed = 1.5f + 0.14f * index
        };

        g.AddGround(0, 10);
        g.SetStart(3, Surface);
        g.AddCoins(6, 9, Surface);
        int x = 11;

        int chunks = 5 + stage;
        Kind last = Kind.Flat;
        int hazardRun = 0;
        for (int i = 0; i < chunks; i++)
        {
            Kind kind = hazardRun >= 3 ? Kind.Flat : Pick(stage, rng);
            for (int tries = 0; tries < 3 && kind == last && kind != Kind.Flat; tries++)
                kind = Pick(stage, rng);
            hazardRun = kind == Kind.Flat ? 0 : hazardRun + 1;
            x = Emit(g, kind, x, stage, rng);
            last = kind;
        }

        g.AddGround(x, x + 11);
        g.AddCoins(x + 1, x + 4, Surface);
        g.SetExit(x + 8, Surface);
        g.Crop(x + 13);
        return g;
    }

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

    static int Emit(LevelGrid g, Kind kind, int x, int stage, System.Random rng)
    {
        switch (kind)
        {
            case Kind.Flat:
            {
                int len = rng.Next(5, 9);
                g.AddGround(x, x + len - 1);
                if (rng.NextDouble() < 0.7) g.AddCoins(x + 1, x + len - 2, Surface);
                else g.AddCoins(x + 2, x + len - 3, Surface + 2);
                return x + len;
            }
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
            case Kind.Enemies:
            {
                int n = 1 + (stage >= 4 ? rng.Next(0, 2) : 0) + (stage >= 7 ? 1 : 0);
                int len = 6 + n * 4;
                g.AddGround(x, x + len - 1);
                g.AddCoins(x + 1, x + 3, Surface);
                for (int i = 0; i < n; i++) g.AddEnemy(x + 5 + i * 4, Surface);
                return x + len;
            }
            case Kind.Platform:
            {
                int py = stage >= 5 ? Surface + 2 : Surface + 1;
                g.AddGround(x, x + 11);
                g.AddBlock(x + 4, py, 3, 1);
                g.AddCoins(x + 4, x + 6, py + 1);
                if (stage >= 6 && py == Surface + 2) g.AddEnemy(x + 5, Surface);
                return x + 12;
            }
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
