#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Takes the screenshots for an update post by putting the real game in each situation and capturing the game view.
/// Unlike the other command-line tools this one needs a window (the game has to render), so do NOT use -batchmode:
///   Unity -projectPath PROJECT -executeMethod ScreenshotTour.Run -shotsOut /path/to/folder
/// The editor opens for about a minute, writes the PNG files and quits by itself. The project must not be open already.
///
/// Each shot sets up its situation the same way BatchChecks does (teleport the player, call the game's own methods),
/// waits a moment for the real code to react, then uses DebugShot to capture the frame including the UI.
/// The images contain black bars when the game view is not 9:16; crop them afterwards (tools/crop_shots.py).
/// To change what is photographed, edit <see cref="Tour"/>.
/// </summary>
public static class ScreenshotTour
{
    const int SuperSize = 3; // 3x the game view resolution keeps pixel art crisp

    static IEnumerator routine;
    static double waitUntil;
    static string outDir;
    static bool oldEnabled;
    static EnterPlayModeOptions oldOptions;

    static string Arg(string name, string fallback)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return fallback;
    }

    /// <summary>Entry point for -executeMethod.</summary>
    public static void Run()
    {
        outDir = Arg("-shotsOut", Path.Combine(Path.GetTempPath(), "pixel-hopper-shots"));
        Directory.CreateDirectory(outDir);
        string scene = EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(scene);

        oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        Game.AutoPlay = false;
        routine = Tour();
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Game.Instance == null) return;
        if (EditorApplication.timeSinceStartup < waitUntil) return;
        if (!routine.MoveNext()) { Finish(); return; }
        if (routine.Current is float seconds) waitUntil = EditorApplication.timeSinceStartup + seconds;
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Debug.Log("ScreenshotTour: done, files in " + outDir);
        EditorApplication.ExitPlaymode();
        EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
        EditorSettings.enterPlayModeOptions = oldOptions;
        EditorApplication.Exit(0);
    }

    static FieldInfo Field(string name) => typeof(Game).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    static void SetLives(int n) => Field("lives").SetValue(Game.Instance, n);
    static void SetField(string name, object value) => Field(name).SetValue(Game.Instance, value);

    static void Teleport(PlayerController p, Vector2 pos)
    {
        var rb = p.GetComponent<Rigidbody2D>();
        rb.position = pos;
        rb.linearVelocity = Vector2.zero;
        p.transform.position = pos;
    }

    static void Shot(string name) => DebugShot.Capture(Path.Combine(outDir, name + ".png"), SuperSize);

    static IEnumerator Tour()
    {
        var game = Game.Instance;
        yield return 2.0f; // let the title stage build and the game view settle

        // 1. Title screen with the new prompt.
        Shot("01_title");
        yield return 1.0f;

        // 2. A heart rest area in stage 2, with the hero standing a few tiles before the heart.
        game.DebugStage(1, false);
        yield return 0.6f;
        SetLives(2);
        var player = game.Player;
        var heart = Object.FindAnyObjectByType<HeartPickup>();
        Teleport(player, new Vector2(heart.transform.position.x - 2.2f, 3.05f));
        player.Freeze(); // keep the hero still and safe while the camera catches up
        yield return 1.6f;
        Shot("02_bonus_heart");
        yield return 1.0f;

        // 3. Collecting it: lives go from 2 to 3 and the heart bursts.
        player.Unfreeze();
        Teleport(player, heart.transform.position + Vector3.down * 0.3f);
        yield return 0.12f;
        Shot("03_heart_collected");
        yield return 1.0f;

        // 4. Stage clear panel with the heart bonus.
        game.DebugStage(2, false);
        yield return 0.6f;
        SetLives(2);
        SetField("stageCoins", 36);   // show a believable coin count on the panel
        SetField("totalCoins", 71);
        game.LevelComplete();
        yield return 0.9f;
        Shot("04_stage_clear_heart");
        yield return 1.0f;

        // 5. Game over: the third life is lost, next run starts at stage 1.
        game.DebugStage(4, false);
        yield return 0.6f;
        SetLives(1);
        game.PlayerDied(game.Player);
        // The prompt blinks twice per second on unscaled time; wait for a frame where it is visible.
        while (Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 != 0) yield return null;
        yield return null;
        Shot("05_game_over");
        yield return 1.0f;

        // 6. The left wall: after running forward the hero walks back until the left edge of the screen stops them.
        game.DebugStage(0, false);
        yield return 0.6f;
        SetLives(3);
        SetField("totalCoins", 12);
        player = game.Player;
        Teleport(player, new Vector2(14f, 3.05f));
        player.bot = true;
        player.botMove = 0f;
        yield return 1.6f;
        player.botMove = -1f;
        yield return 2.6f; // the hero is now pressed against the left edge of the screen
        Shot("06_left_edge_wall");
        yield return 0.5f;
    }
}
#endif
