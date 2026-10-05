using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The game director: the one script that is put in the scene by hand; everything else is created from here in code.
/// It owns the game state machine (title, help, playing, stage clear, game over, won), the lives and coin counters,
/// the cameras, the building of each stage from its level grid, and all of the on-screen UI (drawn each frame in
/// <see cref="DrawUi"/> using <see cref="UiKit"/>).
///
/// Flow of a session:
///  1. Awake: create the sound system. Start: create the cameras, build stage 1 as the title-screen backdrop, hide the player.
///  2. Title: press Space, tap or click. <see cref="BeginGame"/> starts music and stage 1; the first time ever, the help dialog opens.
///  3. Playing: PlayerController and the other scripts run themselves. They call back into this class through
///     <see cref="AddCoin"/>, <see cref="PlayerDied"/> and <see cref="LevelComplete"/>.
///  4. LevelComplete: 2.6 seconds of celebration, then <see cref="AdvanceStage"/> builds the next stage, or shows the win screen after stage 10.
///  5. PlayerDied: lose a life and respawn; at zero lives the game is over and a tap retries the CURRENT stage.
///
/// Execution order -100 makes this run before every other script, so a stage is built before its objects first update.
///
/// Most of the code inside #if UNITY_EDITOR sections only exists in the Unity editor (test bot and debug helpers);
/// it is not part of the WebGL build.
/// </summary>
[DefaultExecutionOrder(-100)]
public class Game : MonoBehaviour
{
    /// <summary>The single Game in the scene (set in Awake) so other scripts can reach the director.</summary>
    public static Game Instance { get; private set; }

    /// <summary>World y below which the player counts as having fallen into a pit.</summary>
    public const float KillY = -4f;

    /// <summary>Title used on the logo and in the UI.</summary>
    public const string GameName = "PIXEL HOPPER";

    // How many world tiles tall the camera view is. 15 tiles on a 9:16 view means the player sees 8.4 tiles
    // across: a deliberately narrow view that makes stages harder and keeps the art large on phones.
    const float ViewTilesHigh = 15f;
    const float PortraitAspect = 9f / 16f;
    const int LivesPerStage = 3;
    const float BannerSeconds = 2.2f;   // how long the "STAGE n" banner stays at the start of a stage

    // PlayerPrefs key remembering that the player pressed SKIP, so the help dialog no longer opens on start.
    const string HelpSkippedKey = "pixelhopper_help_skipped";

    // Title: before the game starts (a stage runs behind the logo). Help: the paused how-to-play dialog.
    // Playing: normal play. StageClear: celebration before the next stage. GameOver: out of lives. Won: stage 10 finished.
    enum State { Title, Help, Playing, StageClear, GameOver, Won }

    int levelIndex;                     // zero-based index of the current stage
    int lives = LivesPerStage;
    int totalCoins;                     // coins collected in the whole run so far
    int coinsAtStageStart;              // total at the start of this stage; a retry after game over restores it
    int stageCoins;                     // coins collected in this stage (shown on the clear panel)
    int stageCoinTotal;                 // coins available in this stage
    float levelStartTime;
    float lastCoinTime = -10f;
    int coinStreak;                     // coins grabbed in quick succession; raises the pitch of the coin sound
    bool helpSeenThisSession;
    float advanceAt;                    // time at which the stage-clear celebration ends
    State state = State.Title;

    Camera cam;
    CameraFollow follow;
    Transform levelRoot;                // parent of everything belonging to the current stage; destroyed to unload it
    PlayerController player;
    Vector3 spawnPoint;
    StageTheme theme;
    Texture2D logo;                     // title logo texture, rendered once on first use

    /// <summary>The player of the current stage.</summary>
    public PlayerController Player => player;

    /// <summary>
    /// The screen rectangle (in pixels, origin bottom-left as Screen uses it) that the 9:16 game view occupies.
    /// It is smaller than the window on wide screens, leaving black bars at the sides.
    /// </summary>
    public Rect ViewRect { get; private set; } = new Rect(0, 0, 9, 16);

#if UNITY_EDITOR
    // Test-bot controls (see AutoPlayer): AutoPlay makes the game play itself starting at DebugStartStage,
    // and DebugLog collects one line per stage result and per death.
    public static bool AutoPlay;
    public static int DebugStartStage;
    public static readonly List<string> DebugLog = new List<string>();
    int stageDeaths;
#endif

#if UNITY_EDITOR
    // Shortcuts used from editor scripts to jump to screens for screenshots.
    public void DebugBegin() => BeginGame();
    public void DebugStage(int index, bool reveal = true) { StartLevel(index); if (!reveal) levelStartTime -= 10f; }
    public void DebugClear() { state = State.StageClear; }
    public void DebugOver() { state = State.GameOver; }
    public void DebugWon() { state = State.Won; }
#endif

    void Awake()
    {
        Instance = this;
        // Rays and overlap queries should ignore trigger colliders (coins, spikes, enemy hit boxes) by default;
        // code that needs them (AutoPlayer) turns this on briefly.
        Physics2D.queriesHitTriggers = false;
        Application.runInBackground = true;
#if !UNITY_WEBGL
        // The browser controls the frame rate in WebGL; on desktop we cap at 60.
        Application.targetFrameRate = 60;
#endif
        if (GetComponent<Sfx>() == null) gameObject.AddComponent<Sfx>();
    }

    void Start()
    {
        SetupCameras();
        UpdateViewport();
        // The title screen is drawn on top of a real stage (stage 1) so the background is alive and parallax-scrolling.
        // The player is hidden and frozen until the game starts.
        StartLevel(0);
        player.Freeze();
        player.gameObject.SetActive(false);
        state = State.Title;
    }

    // Two cameras: the main camera draws the game inside a 9:16 rectangle; a second camera that draws nothing
    // and clears the whole window to black provides the letterbox / pillarbox bars around it.
    void SetupCameras()
    {
        var bars = new GameObject("BarsCamera").AddComponent<Camera>();
        bars.transform.SetParent(transform);
        bars.orthographic = true;
        bars.clearFlags = CameraClearFlags.SolidColor;
        bars.backgroundColor = Color.black;
        bars.cullingMask = 0;   // renders no objects, only the black clear color
        bars.depth = -100f;     // drawn first, so the game camera draws over it
        bars.GetUniversalAdditionalCameraData().renderPostProcessing = false;

        cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = ViewTilesHigh / 2f;  // orthographic size is half the visible height in world units
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.allowHDR = false;
        cam.allowMSAA = false;   // anti-aliasing would blur pixel art
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

        follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
    }

    // Fits the largest 9:16 rectangle into the window, centred, and points the game camera at it.
    // Called every frame so resizing the window or rotating the phone works immediately.
    void UpdateViewport()
    {
        float sw = Screen.width, sh = Screen.height;
        float w, h;
        if (sw / sh > PortraitAspect) { h = sh; w = sh * PortraitAspect; }   // window wider than 9:16: pillarbox
        else { w = sw; h = sw / PortraitAspect; }                             // window taller than 9:16: letterbox
        float x = (sw - w) * 0.5f, y = (sh - h) * 0.5f;
        ViewRect = new Rect(x, y, w, h);
        if (cam != null) cam.rect = new Rect(x / sw, y / sh, w / sw, h / sh);  // camera rect is in 0..1 screen fractions
    }

    // ---------- input / state ----------

    void Update()
    {
        UpdateViewport();

        var kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame) Sfx.ToggleMute();

        // After the celebration delay, move on to the next stage.
        if (state == State.StageClear && Time.time >= advanceAt) AdvanceStage();

#if UNITY_EDITOR
        // Bot mode: start by itself and give up on a stage after 150 seconds (logged as a TIMEOUT).
        if (AutoPlay)
        {
            if (state == State.Title) BeginGame();
            if (state == State.Playing && Time.time - levelStartTime > 150f)
            {
                DebugLog.Add($"stage {levelIndex + 1}: TIMEOUT deaths={stageDeaths}");
                AdvanceStage();
            }
            return;
        }
#endif

        if (state == State.Help)
        {
            UpdateHelp(kb);
            return;
        }

        // H re-opens the help dialog during play (the game is paused while it is open).
        if (state == State.Playing && kb != null && kb.hKey.wasPressedThisFrame)
        {
            OpenHelp();
            return;
        }

        // Title, game-over and win screens all react to the same "confirm" input.
        if (state == State.Title || state == State.GameOver || state == State.Won)
        {
            if (ConfirmPressed(kb)) OnConfirm();
        }
    }

    // True on the frame the player confirms: Space, Enter or R, gamepad A/Start, a mouse click or a screen tap.
    static bool ConfirmPressed(Keyboard kb)
    {
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame)) return true;
        var gp = Gamepad.current;
        if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) return true;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        return TouchControls.AnyPress();
    }

    // Reads a mouse click or touch press this frame and returns its position in "GUI" coordinates:
    // pixels with the origin at the TOP-left (the Input System reports bottom-left, so y is flipped).
    // These match the rectangles that UiKit draws with, so buttons can be hit-tested with Rect.Contains.
    static bool PointerPressed(out Vector2 guiPosition)
    {
        guiPosition = default;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 p = mouse.position.ReadValue();
            guiPosition = new Vector2(p.x, Screen.height - p.y);
            return true;
        }
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 p = touch.primaryTouch.position.ReadValue();
            guiPosition = new Vector2(p.x, Screen.height - p.y);
            return true;
        }
        return false;
    }

    // What "confirm" does in each screen. Game over retries the current stage with the coin total it started with;
    // winning starts a completely new run.
    void OnConfirm()
    {
        switch (state)
        {
            case State.Title:
                BeginGame();
                break;
            case State.GameOver:
                totalCoins = coinsAtStageStart;
                Sfx.Play(Clip.Start);
                StartLevel(levelIndex);
                break;
            case State.Won:
                totalCoins = 0;
                coinsAtStageStart = 0;
                Sfx.Play(Clip.Start);
                StartLevel(0);
                break;
        }
    }

    // Starts a new run from stage 1. The music starts here (the first user interaction), because browsers
    // do not allow audio to play before the user has interacted with the page.
    void BeginGame()
    {
        totalCoins = 0;
        coinsAtStageStart = 0;
        Sfx.Play(Clip.Start);
        Sfx.StartMusic();
#if UNITY_EDITOR
        if (AutoPlay)
        {
            StartLevel(DebugStartStage);
            return;
        }
#endif
        StartLevel(0);
        // Show the how-to-play dialog unless it was already seen in this session or skipped forever in a past one.
        if (!helpSeenThisSession && PlayerPrefs.GetInt(HelpSkippedKey, 0) == 0) OpenHelp();
    }

    // Pauses the game (time scale 0) and shows the help dialog.
    void OpenHelp()
    {
        state = State.Help;
        Time.timeScale = 0f;
        if (player != null) player.Freeze();
    }

    // Closes the help dialog and resumes play. 'skipForever' (the SKIP button or Esc) remembers the choice
    // in PlayerPrefs, which the browser keeps between visits.
    void CloseHelp(bool skipForever)
    {
        if (skipForever)
        {
            PlayerPrefs.SetInt(HelpSkippedKey, 1);
            PlayerPrefs.Save();
        }
        helpSeenThisSession = true;
        Time.timeScale = 1f;
        if (player != null) player.Unfreeze();
        state = State.Playing;
        levelStartTime = Time.time;   // restart the stage banner timer so it shows after the dialog closes
        Sfx.Play(Clip.Start);
    }

    // Handles input while the help dialog is open: Enter, Space or gamepad A closes it; Esc skips it for good;
    // clicking or tapping OK or SKIP does the same. The button rectangles come from HelpLayout, shared with the drawing code.
    void UpdateHelp(Keyboard kb)
    {
        HelpLayout(ViewRect, out _, out Rect ok, out Rect skip);
        if (kb != null)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { CloseHelp(false); return; }
            if (kb.escapeKey.wasPressedThisFrame) { CloseHelp(true); return; }
        }
        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) { CloseHelp(false); return; }
        if (PointerPressed(out Vector2 p))
        {
            if (ok.Contains(p)) CloseHelp(false);
            else if (skip.Contains(p)) CloseHelp(true);
        }
    }

    // ---------- level building ----------

    // Unloads the current stage (if any) and builds stage 'index' (zero-based): resets lives and the stage
    // counters, asks Levels for the grid, and creates all the game objects from it.
    void StartLevel(int index)
    {
        if (levelRoot != null)
        {
            // Deactivate first: Destroy only takes effect at the end of the frame, and the old objects must not
            // keep colliding or updating next to the new stage until then.
            levelRoot.gameObject.SetActive(false);
            Destroy(levelRoot.gameObject);
        }

        levelIndex = index;
        state = State.Playing;
        levelStartTime = Time.time;
        lives = LivesPerStage;
        stageCoins = 0;
        coinsAtStageStart = totalCoins;
        coinStreak = 0;
#if UNITY_EDITOR
        stageDeaths = 0;
#endif

        LevelGrid grid = Levels.Build(index);
        theme = grid.Theme;
        stageCoinTotal = grid.CountCoins();
        cam.backgroundColor = theme.SkyBottom;

        levelRoot = new GameObject("Stage " + (index + 1)).transform;
        BuildLevel(grid);
    }

    // Turns the level grid into game objects. Everything is parented under levelRoot (so one Destroy removes the
    // whole stage), grouped into Tiles (pictures), Solids (invisible colliders), Items and Decor.
    // Grid coordinates: x to the right, y upward, one cell = one world unit, cell (x, y) centred at (x + 0.5, y + 0.5).
    void BuildLevel(LevelGrid g)
    {
        Backdrop.Create(levelRoot, cam, theme, levelIndex * 13 + 5);

        var tiles = NewChild("Tiles");
        var solids = NewChild("Solids");
        var items = NewChild("Items");
        var decor = NewChild("Decor");

        for (int x = 0; x < g.Width; x++)
        {
            for (int y = 0; y < g.Height; y++)
            {
                Vector3 center = new Vector3(x + 0.5f, y + 0.5f, 0f);
                switch (g[x, y])
                {
                    case '#':
                        // Ground that has more ground above it is dirt; the top layer gets the grass-topped tile.
                        AddTile(tiles, center, g.IsSolid(x, y + 1) ? PixelArt.Dirt : PixelArt.Grass);
                        break;
                    case 'B':
                        AddTile(tiles, center, PixelArt.Brick);
                        break;
                    case 'c':
                        NewObject<Coin>("Coin", items, center);
                        break;
                    case '^':
                        NewObject<Hazard>("Spikes", items, center);
                        break;
                    case 'e':
                        // Enemies and the exit are placed by their feet (the bottom of the cell), not the cell centre.
                        NewObject<Enemy>("Enemy", items, new Vector3(x + 0.5f, y, 0f)).speed = g.EnemySpeed;
                        break;
                }
            }
        }

        BuildSolids(g, solids);
        PlaceDecor(g, decor);

        NewObject<LevelExit>("Exit", items, new Vector3(g.Exit.x + 0.5f, g.Exit.y, 0f));

        // The player starts slightly above the ground so it never spawns embedded in a tile.
        spawnPoint = new Vector3(g.Start.x + 0.5f, g.Start.y + 0.05f, 0f);
        var playerObject = new GameObject("Player");
        playerObject.transform.SetParent(levelRoot);
        playerObject.transform.position = spawnPoint;
        player = playerObject.AddComponent<PlayerController>();

#if UNITY_EDITOR
        if (AutoPlay)
        {
            player.bot = true;
            playerObject.AddComponent<AutoPlayer>();
        }
#endif

        follow.SetTarget(player, new Vector2(g.Width, g.Height));
    }

    // Scatters theme decoration (bushes, flowers, cacti and so on) over the grass.
    // A decoration is placed on a ground column when: it is at least 3 cells after the previous one, not near the exit,
    // the cell above the ground is empty, there is no spike, enemy or brick in the three neighbouring columns,
    // and a 30% random roll succeeds. The random numbers are seeded by the stage number, so every stage always looks the same.
    void PlaceDecor(LevelGrid g, Transform parent)
    {
        if (theme.Decor.Length == 0) return;
        var rng = new System.Random(levelIndex * 4241 + 11);
        int lastX = -10;
        for (int x = 5; x < g.Width - 3; x++)
        {
            if (x - lastX < 3 || Mathf.Abs(x - g.Exit.x) < 3) continue;
            if (g[x, 2] != '#' || g[x, 3] != '.') continue;   // ground surface is at row 2; row 3 must be free
            bool clear = true;
            for (int dx = -1; dx <= 1; dx++)
            {
                char c = g[x + dx, 3];
                if (c == '^' || c == 'e' || c == 'B') clear = false;
            }
            if (!clear || rng.NextDouble() > 0.3) continue;

            string kind = theme.Decor[rng.Next(theme.Decor.Length)];
            var go = new GameObject("Decor " + kind);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.3f, 3f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Decor(kind);
            sr.color = theme.Tint;
            sr.sortingOrder = -1;   // behind the player and tiles
            sr.flipX = rng.NextDouble() < 0.5;
            lastX = x;
        }
    }

    // Creates an empty child of the current stage root.
    Transform NewChild(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(levelRoot);
        return t;
    }

    // Creates a child object at a position and adds a component of type T to it
    // (the component sets up its own look and colliders in Awake).
    T NewObject<T>(string name, Transform parent, Vector3 position) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        return go.AddComponent<T>();
    }

    // Creates one visible tile, tinted with the stage colour.
    void AddTile(Transform parent, Vector3 position, Sprite sprite)
    {
        var go = new GameObject("Tile");
        go.transform.SetParent(parent);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = theme.Tint;
    }

    // Builds the collision for all solid cells with as few colliders as possible. A collider per cell would make
    // the physics slow and, worse, cause the player to catch on the seams between neighbouring tiles.
    // Algorithm (greedy rectangle merge): scan row by row; each horizontal run of solid cells is looked up in 'open',
    // the runs from the previous row that have not ended yet. If the identical run (same start and end column)
    // was open, it is extended upward instead of being a new rectangle. Runs that are not continued in the next row
    // are finished and become a rectangle. Dictionary key = (first column, last column), value = row where it began.
    void BuildSolids(LevelGrid g, Transform parent)
    {
        var open = new Dictionary<(int, int), int>();
        var rects = new List<(int x0, int x1, int y0, int y1)>();

        for (int y = 0; y < g.Height; y++)
        {
            var next = new Dictionary<(int, int), int>();
            int x = 0;
            while (x < g.Width)
            {
                if (!g.IsSolid(x, y)) { x++; continue; }
                int start = x;
                while (x < g.Width && g.IsSolid(x, y)) x++;
                var run = (start, x - 1);
                if (open.TryGetValue(run, out int y0))
                {
                    next[run] = y0;      // same run as in the row below: keep growing that rectangle
                    open.Remove(run);
                }
                else
                {
                    next[run] = y;       // a new rectangle starts on this row
                }
            }
            // Runs left in 'open' were not repeated on this row, so their rectangles end on the previous row.
            foreach (var kv in open) rects.Add((kv.Key.Item1, kv.Key.Item2, kv.Value, y - 1));
            open = next;
        }
        foreach (var kv in open) rects.Add((kv.Key.Item1, kv.Key.Item2, kv.Value, g.Height - 1));

        foreach (var r in rects)
            AddBox(parent, new Vector2((r.x0 + r.x1 + 1) / 2f, (r.y0 + r.y1 + 1) / 2f), new Vector2(r.x1 - r.x0 + 1, r.y1 - r.y0 + 1));

        // Invisible walls just outside both ends of the stage so the player cannot walk off the level.
        AddBox(parent, new Vector2(-0.5f, 20f), new Vector2(1f, 60f));
        AddBox(parent, new Vector2(g.Width + 0.5f, 20f), new Vector2(1f, 60f));
    }

    // Creates an invisible box collider.
    void AddBox(Transform parent, Vector2 center, Vector2 size)
    {
        var go = new GameObject("Solid");
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    // ---------- gameplay events ----------

    /// <summary>
    /// Called by a Coin when it is collected. Counts the coin and plays the coin sound.
    /// Coins collected less than 0.8 seconds apart build a streak (up to 8) that raises the pitch by 6% per step,
    /// so a row of coins plays a rising scale.
    /// </summary>
    public void AddCoin()
    {
        totalCoins++;
        stageCoins++;
        coinStreak = Time.time - lastCoinTime < 0.8f ? Mathf.Min(coinStreak + 1, 8) : 0;
        lastCoinTime = Time.time;
        Sfx.Play(Clip.Coin, 1f, 1f + 0.06f * coinStreak);
    }

    /// <summary>
    /// Called by the player (or a hazard) when the player dies. Removes a life and respawns at the stage start;
    /// with no lives left, the game is over (a retry restarts the same stage with 3 lives).
    /// In bot mode (editor only) deaths are only logged and the bot respawns for free.
    /// </summary>
    public void PlayerDied(PlayerController p)
    {
        if (state != State.Playing) return;   // ignore deaths during overlays (for example while the stage-clear celebration plays)
#if UNITY_EDITOR
        stageDeaths++;
        if (AutoPlay)
        {
            if (stageDeaths <= 8) DebugLog.Add($"  stage {levelIndex + 1} death #{stageDeaths} at x={p.transform.position.x:F1} y={p.transform.position.y:F1}");
            p.Respawn(spawnPoint);
            return;
        }
#endif
        lives--;
        follow.Shake(0.3f, 0.28f);
        Fx.Burst(p.transform.position + Vector3.up * 0.5f, new Color(1f, 0.35f, 0.4f, 1f), 14, 4.5f, 0.6f, 6f, 0.18f, 2f);
        if (lives <= 0)
        {
            state = State.GameOver;
            p.Disable();
            Sfx.Play(Clip.GameOver);
            return;
        }
        Sfx.Play(Clip.Hurt);
        p.Respawn(spawnPoint);
    }

    /// <summary>
    /// Called by the exit flag when the player reaches it. Freezes the player, plays the jingle and a gold
    /// burst, and schedules the move to the next stage 2.6 seconds later (see Update).
    /// </summary>
    public void LevelComplete()
    {
        if (state != State.Playing) return;
        state = State.StageClear;
        player.Freeze();
        Sfx.Play(Clip.StageClear);
        Fx.Burst(player.transform.position + Vector3.up, new Color(1f, 0.9f, 0.35f, 1f), 24, 5f, 0.9f, 4f, 0.16f, 2f);
#if UNITY_EDITOR
        DebugLog.Add($"stage {levelIndex + 1}: CLEAR time={Time.time - levelStartTime:F1}s deaths={stageDeaths}");
#endif
        advanceAt = Time.time + 2.6f;
    }

    // Builds the next stage, or shows the win screen if the last stage was just completed.
    void AdvanceStage()
    {
        if (levelIndex + 1 < Levels.Count)
        {
            StartLevel(levelIndex + 1);
        }
        else
        {
            state = State.Won;
#if UNITY_EDITOR
            DebugLog.Add("ALL STAGES CLEARED");
#endif
        }
    }

    // ---------- UI ----------

    static readonly Color Gold = new Color(1f, 0.86f, 0.3f);
    static readonly Color PanelTint = new Color(1f, 1f, 1f, 0.94f);

    // The UI is redrawn every frame, in LateUpdate so it uses the state after all gameplay scripts have run.
    void LateUpdate()
    {
        UiCanvas.Begin();
        DrawUi();
        UiCanvas.End();
    }

    // Chooses what to draw for the current state. All sizes are relative to the 9:16 view so the layout works
    // on any screen size: 'u' is 1% of the view height in pixels; 'pk' is the integer scale of panel borders.
    void DrawUi()
    {
        Rect v = ViewRect;
        float u = v.height / 100f;
        int pk = Mathf.Max(1, Mathf.RoundToInt(u * 0.11f));

        switch (state)
        {
            case State.Title:
                DrawTitle(v, u);
                break;
            case State.Help:
                DrawHud(v, u, pk);
                DrawHelp(v, u, pk);
                break;
            default:
                DrawHud(v, u, pk);
                DrawStateOverlay(v, u, pk);
                // The on-screen touch buttons are only drawn while playing, and only on touch devices.
                if (state == State.Playing && TouchControls.Available) DrawTouchButtons(v, u);
                break;
        }
    }

    // Darkens the whole view so overlay text stands out.
    void Dim(Rect v, float alpha)
    {
        UiKit.Quad(v, new Color(0.04f, 0.02f, 0.1f, alpha));
    }

    // Title screen: the 3D rainbow logo bobbing gently, a tagline, the hero hopping next to an enemy and a row of
    // bouncing coins, a blinking start prompt and a hint line. The animation uses unscaled time so it keeps moving
    // even while Time.timeScale is 0.
    void DrawTitle(Rect v, float u)
    {
        Dim(v, 0.22f);
        if (logo == null)
        {
            // One color per letter of "PIXEL" and "HOPPER" (11 letters): a rainbow that continues across both lines.
            var colors = new[]
            {
                new Color32(255, 90, 90, 255), new Color32(255, 160, 50, 255), new Color32(255, 220, 60, 255), new Color32(110, 220, 90, 255), new Color32(60, 200, 230, 255),
                new Color32(90, 140, 255, 255), new Color32(170, 110, 255, 255), new Color32(255, 110, 200, 255), new Color32(255, 90, 90, 255), new Color32(255, 160, 50, 255), new Color32(255, 220, 60, 255),
            };
            var tinted = new Color[colors.Length];
            for (int i = 0; i < colors.Length; i++) tinted[i] = colors[i];
            logo = PixelFont.RenderLogo(new[] { "PIXEL", "HOPPER" }, tinted);
        }

        // Largest whole-number scale at which the logo fills at most 92% of the view width.
        int scale = Mathf.Max(1, Mathf.FloorToInt(v.width * 0.92f / logo.width));
        float lw = logo.width * scale, lh = logo.height * scale;
        float bob = Mathf.Sin(Time.unscaledTime * 2f) * u * 0.6f;
        UiKit.Image(new Rect(Mathf.Round(v.center.x - lw / 2f), Mathf.Round(v.y + v.height * 0.1f + bob), lw, lh), logo);

        int tk = UiKit.K(u, 0.27f);
        UiKit.Text(new Rect(v.x + u * 2f, v.y + v.height * 0.1f + lh + u * 1.5f, v.width - u * 4f, u * 5), "10 STAGES OF PIXEL PLATFORMING", tk, Color.white);

        // Mascots: the hero hops (|sin| gives a bounce) while walking, the enemy waddles, three coins spin and bob.
        float t = Time.unscaledTime;
        float baseY = v.y + v.height * 0.8f + u * 0.2f;
        float spriteScale = Mathf.Max(2f, u * 0.42f);
        float hop = Mathf.Abs(Mathf.Sin(t * 3f)) * u * 3f;
        UiKit.SpriteAt(new Vector2(v.center.x - u * 14f, baseY - hop), Mathf.FloorToInt(t * 6f) % 2 == 0 ? PixelArt.HeroWalkA : PixelArt.HeroWalkB, spriteScale);
        UiKit.SpriteAt(new Vector2(v.center.x + u * 14f, baseY), Mathf.FloorToInt(t * 4f) % 2 == 0 ? PixelArt.EnemyA : PixelArt.EnemyB, spriteScale);
        for (int i = 0; i < 3; i++)
            UiKit.SpriteAt(new Vector2(v.center.x + (i - 1) * u * 8f, baseY - u * 9f - Mathf.Abs(Mathf.Sin(t * 3f + i * 0.6f)) * u * 1.4f), PixelArt.CoinFrame(Mathf.FloorToInt(t * 7f + i)), spriteScale * 0.9f);

        // The start prompt blinks twice per second and changes wording on touch devices.
        if (Mathf.FloorToInt(t * 2f) % 2 == 0)
        {
            int k = UiKit.K(u, 0.4f);
            UiKit.Text(new Rect(v.x, v.y + v.height * 0.8f, v.width, u * 7), TouchControls.Available ? "TAP TO START" : "PRESS SPACE TO START", k, Gold);
        }
        UiKit.Text(new Rect(v.x, v.yMax - u * 6f, v.width, u * 4f), "M: SOUND ON/OFF   H: HELP", UiKit.K(u, 0.2f), new Color(1f, 1f, 1f, 0.85f));
    }

    // The top bar shown on every screen except the title: lives (heart) on the left, stage number in the
    // middle, coin total (spinning coin) on the right-hand side.
    void DrawHud(Rect v, float u, int pk)
    {
        int k = UiKit.K(u, 0.3f);
        var bar = new Rect(v.x + u * 1.5f, v.y + u * 1.2f, v.width - u * 3f, u * 7.4f);
        UiKit.Panel(bar, pk, PanelTint);

        float iconH = u * 3.6f;
        var heart = PixelArt.Heart;
        float heartW = iconH * heart.textureRect.width / heart.textureRect.height;
        float cy = bar.center.y;
        UiKit.Sprite(new Rect(bar.x + u * 2.6f, cy - iconH / 2f, heartW, iconH), heart);
        UiKit.Text(new Rect(bar.x + u * 2.6f + heartW + u * 0.6f, bar.y, u * 12f, bar.height), "x" + lives, k, Color.white, TextAnchor.MiddleLeft);

        UiKit.Text(bar, "STAGE " + (levelIndex + 1) + "/" + Levels.Count, k, Color.white);

        // Right-align the coin count and put the coin icon just left of the number.
        string coinText = totalCoins.ToString();
        var coin = PixelArt.CoinFrame(0);
        float coinW = iconH * coin.textureRect.width / coin.textureRect.height;
        float textW = UiKit.TextSize(coinText, k).x;
        float right = bar.xMax - u * 2.4f;
        UiKit.Text(new Rect(right - textW, bar.y, textW, bar.height), coinText, k, Gold, TextAnchor.MiddleRight);
        UiKit.Sprite(new Rect(right - textW - coinW - u * 0.4f, cy - iconH / 2f, coinW, iconH), PixelArt.CoinFrame(Mathf.FloorToInt(Time.unscaledTime * 7f)));
    }

    // Banner and panels for the playing, stage-clear, game-over and won states.
    void DrawStateOverlay(Rect v, float u, int pk)
    {
        int big = UiKit.K(u, 0.75f), mid = UiKit.K(u, 0.38f), small = UiKit.K(u, 0.27f);
        var area = new Rect(v.x, v.y + v.height * 0.26f, v.width, v.height * 0.4f);

        switch (state)
        {
            case State.Playing:
                // "STAGE n" plus the theme name for the first 2.2 seconds of a stage.
                if (Time.time - levelStartTime < BannerSeconds)
                {
                    UiKit.Text(new Rect(area.x, area.y, area.width, u * 10f), "STAGE " + (levelIndex + 1), big, Gold);
                    UiKit.Text(new Rect(area.x, area.y + u * 11f, area.width, u * 6f), theme.Name.ToUpper(), mid, Color.white);
                }
                break;
            case State.StageClear:
            {
                Dim(v, 0.25f);
                var panel = new Rect(v.x + u * 6f, area.y - u * 3f, v.width - u * 12f, u * 30f);
                UiKit.Panel(panel, pk, PanelTint);
                UiKit.Text(new Rect(panel.x, panel.y + u * 3f, panel.width, u * 9f), "STAGE " + (levelIndex + 1) + " CLEAR!", mid + 1, Gold);
                UiKit.Text(new Rect(panel.x, panel.y + u * 14f, panel.width, u * 6f), "COINS  " + stageCoins + " / " + stageCoinTotal, mid, Color.white);
                UiKit.Text(new Rect(panel.x, panel.y + u * 21f, panel.width, u * 5f), levelIndex + 1 < Levels.Count ? "GET READY FOR THE NEXT ONE" : "THAT WAS THE LAST STAGE!", small, new Color(1f, 1f, 1f, 0.85f));
                break;
            }
            case State.GameOver:
            {
                Dim(v, 0.4f);
                var panel = new Rect(v.x + u * 6f, area.y - u * 3f, v.width - u * 12f, u * 32f);
                UiKit.Panel(panel, pk, PanelTint);
                UiKit.Text(new Rect(panel.x, panel.y + u * 3f, panel.width, u * 10f), "GAME OVER", big - 1, new Color(1f, 0.45f, 0.5f));
                UiKit.Text(new Rect(panel.x, panel.y + u * 16f, panel.width, u * 5f), "STAGE " + (levelIndex + 1) + " - " + theme.Name.ToUpper(), small, Color.white);
                if (Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0)
                    UiKit.Text(new Rect(panel.x, panel.y + u * 23f, panel.width, u * 6f), TouchControls.Available ? "TAP TO RETRY" : "PRESS SPACE TO RETRY", mid, Gold);
                break;
            }
            case State.Won:
            {
                Dim(v, 0.4f);
                var panel = new Rect(v.x + u * 6f, area.y - u * 6f, v.width - u * 12f, u * 38f);
                UiKit.Panel(panel, pk, PanelTint);
                UiKit.Text(new Rect(panel.x, panel.y + u * 3f, panel.width, u * 10f), "YOU WIN!", big, Gold);
                UiKit.Text(new Rect(panel.x, panel.y + u * 15f, panel.width, u * 5f), "ALL 10 STAGES CLEARED", small, Color.white);
                UiKit.Text(new Rect(panel.x, panel.y + u * 21f, panel.width, u * 6f), totalCoins + " COINS COLLECTED", mid, Color.white);
                if (Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0)
                    UiKit.Text(new Rect(panel.x, panel.y + u * 29f, panel.width, u * 6f), TouchControls.Available ? "TAP TO PLAY AGAIN" : "SPACE TO PLAY AGAIN", mid, Gold);
                break;
            }
        }
    }

    // Computes the help dialog panel and its two buttons (SKIP on the left, OK on the right).
    // Shared by DrawHelp (drawing) and UpdateHelp (click hit-testing) so the two can never disagree.
    void HelpLayout(Rect v, out Rect panel, out Rect ok, out Rect skip)
    {
        float u = v.height / 100f;
        panel = new Rect(v.x + v.width * 0.06f, v.y + v.height * 0.15f, v.width * 0.88f, v.height * 0.68f);
        float bh = u * 8.5f, bw = (panel.width - u * 12f) / 2f;
        float by = panel.yMax - bh - u * 3f;
        skip = new Rect(panel.x + u * 4f, by, bw, bh);
        ok = new Rect(panel.xMax - u * 4f - bw, by, bw, bh);
    }

    // The how-to-play dialog: six rows (controls, stomp, spikes, coins, flag) and the SKIP / OK! buttons.
    // Wording changes between touch and keyboard devices. The label text size is reduced until the longest
    // of the six labels fits the dialog, so every row uses the same size.
    void DrawHelp(Rect v, float u, int pk)
    {
        Dim(v, 0.55f);
        HelpLayout(v, out Rect panel, out Rect ok, out Rect skip);
        UiKit.Panel(panel, pk, PanelTint);

        int title = UiKit.K(u, 0.45f), text = UiKit.K(u, 0.25f), keyK = UiKit.K(u, 0.2f);
        UiKit.Text(new Rect(panel.x, panel.y + u * 2.4f, panel.width, u * 7f), "HOW TO PLAY", title, Gold);

        bool touch = TouchControls.Available;
        float rowY = panel.y + u * 12f, step = u * 7.3f;
        float iconX = panel.x + u * 3.5f, labelX = panel.x + u * 15f;
        float keyH = u * 5.2f;

        string[] allLabels =
        {
            touch ? "TAP LEFT SIDE: MOVE" : "MOVE (OR ARROWS)", touch ? "TAP RIGHT SIDE: JUMP" : "JUMP (HOLD = HIGHER)",
            "STOMP ENEMIES", "AVOID SPIKES AND PITS", "COLLECT ALL THE COINS", "REACH THE FLAG"
        };
        float widest = 1f;
        foreach (var s in allLabels) widest = Mathf.Max(widest, UiKit.TextSize(s, 1).x);
        text = Mathf.Clamp(Mathf.FloorToInt((panel.xMax - labelX - u * 1f) / widest), 1, text);

        // Local helpers for one row: a text label, a key-cap button graphic, and an icon sprite.
        void Label(int row, string s)
        {
            UiKit.Text(new Rect(labelX, rowY + row * step, panel.xMax - labelX - u * 1f, keyH), s, text, Color.white, TextAnchor.MiddleLeft);
        }
        void KeyCap(float x, int row, string s, float w)
        {
            UiKit.Button(new Rect(x, rowY + row * step, w, keyH), s, 1, keyK, new Color(0.62f, 0.64f, 0.8f), false);
        }
        void Icon(int row, Sprite sprite, float height)
        {
            Rect r = sprite.textureRect;
            float w = height * r.width / r.height;
            UiKit.Sprite(new Rect(iconX + (u * 11f - w) / 2f, rowY + row * step + (keyH - height) / 2f, w, height), sprite);
        }

        KeyCap(iconX, 0, touch ? "<" : "A", u * 5f);
        KeyCap(iconX + u * 5.8f, 0, touch ? ">" : "D", u * 5f);
        Label(0, touch ? "TAP LEFT SIDE: MOVE" : "MOVE (OR ARROWS)");

        KeyCap(iconX, 1, touch ? "JUMP" : "SPACE", u * 11f);
        Label(1, touch ? "TAP RIGHT SIDE: JUMP" : "JUMP (HOLD = HIGHER)");

        Icon(2, PixelArt.EnemyA, u * 5.6f);
        Label(2, "STOMP ENEMIES");
        Icon(3, PixelArt.Spikes, u * 5.2f);
        Label(3, "AVOID SPIKES AND PITS");
        Icon(4, PixelArt.CoinFrame(Mathf.FloorToInt(Time.unscaledTime * 7f)), u * 5f);
        Label(4, "COLLECT ALL THE COINS");
        Icon(5, PixelArt.Flag, u * 6.6f);
        Label(5, "REACH THE FLAG");

        UiKit.Button(skip, "SKIP", 1, UiKit.K(u, 0.3f), new Color(0.78f, 0.5f, 0.55f), false);
        UiKit.Button(ok, "OK!", 1, UiKit.K(u, 0.34f), new Color(0.35f, 0.82f, 0.45f), false);
    }

    // Draws the translucent on-screen controls on touch devices: left and right (a quarter of the width each) and
    // JUMP on the right. The touches themselves are read by TouchControls, whose zones are the left quarter (move left),
    // the second quarter (move right) and the whole right half (jump); the drawn JUMP button is just a visual
    // hint inside that half, so tapping anywhere on the right half jumps.
    void DrawTouchButtons(Rect v, float u)
    {
        float h = u * 14f, y = v.yMax - h - u * 3f;
        TouchButton(new Rect(v.x + u * 2, y, v.width * 0.25f - u * 2.5f, h), "<", u);
        TouchButton(new Rect(v.x + v.width * 0.25f + u * 0.5f, y, v.width * 0.25f - u * 2.5f, h), ">", u);
        TouchButton(new Rect(v.x + v.width * 0.55f, y, v.width * 0.43f, h), "JUMP", u);
    }

    void TouchButton(Rect r, string text, float u)
    {
        UiKit.Quad(r, new Color(1f, 1f, 1f, 0.2f));
        UiKit.Text(r, text, UiKit.K(u, 0.4f), new Color(1f, 1f, 1f, 0.9f));
    }
}
