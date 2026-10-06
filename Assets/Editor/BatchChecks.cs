#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Automated behaviour checks for game rules, run from the command line with no editor window:
///   Unity -batchmode -nographics -projectPath PROJECT -executeMethod BatchChecks.Run -checksOut /tmp/checks.txt -logFile -
/// Exit code 0 = every check passed, 1 = at least one failed. The project must not be open in the editor.
///
/// Each check puts the game in a known situation (by teleporting the player or calling the same methods the game calls),
/// waits for the real game code to react, and tests the outcome. Checks:
///  - flag: jumping over the top of the finish pole still completes the stage
///  - camera: the camera does not scroll back, and the left edge of the screen blocks the player
///  - lives: three deaths end the run and the next run starts at stage 1 with 3 lives
///  - carry-over: lives carry from stage to stage and clearing a stage gives one heart
///  - hearts: a heart pickup gives a life, and is left alone when the player has the maximum
/// The checks only read private game state through reflection, so they also run on older versions of the code
/// (this is how a fix is proven: the check fails on the old code and passes on the new code).
/// Add a new check by adding a block to <see cref="Checks"/> that ends with a call to <see cref="Report"/>.
/// </summary>
public static class BatchChecks
{
    static IEnumerator routine;
    static double waitUntil;
    static string outPath;
    static readonly StringBuilder log = new StringBuilder();
    static int failures;
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
        outPath = Arg("-checksOut", "");
        string scene = EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(scene);

        oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        Game.AutoPlay = false;
        routine = Checks();
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    // Advances the checks one step per editor frame, honouring the real-time waits the checks ask for (a yielded float).
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
        log.AppendLine(failures == 0 ? "CHECKS RESULT: PASS" : "CHECKS RESULT: FAIL (" + failures + " failed)");
        Debug.Log(log.ToString());
        if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, log.ToString());
        EditorApplication.ExitPlaymode();
        EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
        EditorSettings.enterPlayModeOptions = oldOptions;
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static void Report(string name, bool pass, string detail = "")
    {
        if (!pass) failures++;
        log.AppendLine((pass ? "PASS  " : "FAIL  ") + name + (detail.Length > 0 ? "   [" + detail + "]" : ""));
    }

    // ---- helpers that read or set private Game state by name ----
    static FieldInfo Field(string name) => typeof(Game).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    static int Lives() => (int)Field("lives").GetValue(Game.Instance);
    static void SetLives(int n) => Field("lives").SetValue(Game.Instance, n);
    static string State() => Field("state").GetValue(Game.Instance).ToString();
    static int LevelIndex() => (int)Field("levelIndex").GetValue(Game.Instance);

    static void Teleport(PlayerController p, Vector2 pos)
    {
        var rb = p.GetComponent<Rigidbody2D>();
        rb.position = pos;
        rb.linearVelocity = Vector2.zero;
        p.transform.position = pos;
    }

    static float CameraLeftEdge()
    {
        var cam = Camera.main;
        return cam.transform.position.x - cam.orthographicSize * cam.aspect;
    }

    static IEnumerator Checks()
    {
        var game = Game.Instance;
        yield return 1.0f; // let Game.Start finish building the title stage

        // ---------- flag: coming down on the pole from high up must still finish the stage ----------
        game.DebugStage(0);
        yield return 0.5f;
        Game.DebugLog.Clear();
        var exit = Object.FindFirstObjectByType<LevelExit>();
        var player = game.Player;
        // Recreate a real jump over the pole. A full jump peaks about 3.8 tiles up, and crossing the pole's 1 tile wide column
        // takes about 0.3 s, so the feet stay between 3.5 and 3.8 tiles up the whole time. That is above the old 3 tile tall
        // trigger and well inside the new one. Start just as the hero enters the column: feet 3.5 up, rising at 4.3 tiles per
        // second (the apex is then reached in the middle of the crossing), running right at full speed.
        Teleport(player, new Vector2(exit.transform.position.x - 0.85f, exit.transform.position.y + 3.5f));
        player.bot = true;
        player.botMove = 1f;
        player.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(PlayerController.MoveSpeed, 4.3f);
        yield return 1.5f;
        bool cleared = false;
        foreach (var line in Game.DebugLog) if (line.Contains("CLEAR")) cleared = true;
        Report("flag: jumping over the top of the pole still completes the stage", cleared);

        // ---------- camera: never scrolls back, left edge blocks the player ----------
        game.DebugStage(0);
        yield return 0.5f;
        player = game.Player;
        Teleport(player, new Vector2(12f, 3.05f));
        player.bot = true;
        player.botMove = 0f;
        yield return 1.5f; // camera catches up
        float camBefore = Camera.main.transform.position.x;
        player.botMove = -1f; // walk back
        yield return 3.0f;
        float camAfter = Camera.main.transform.position.x;
        float px = player.transform.position.x;
        float edge = CameraLeftEdge();
        Report("camera: the camera does not scroll back", camAfter >= camBefore - 0.05f, "camera x " + camBefore.ToString("F2") + " -> " + camAfter.ToString("F2"));
        Report("camera: the player can still walk back inside the screen", px < 12f - 1f, "player x " + px.ToString("F2"));
        Report("camera: the left edge of the screen blocks the player", px >= edge - 0.05f, "player x " + px.ToString("F2") + ", left edge " + edge.ToString("F2"));

        // ---------- lives: three deaths end the run, the next run starts at stage 1 ----------
        game.DebugStage(4);
        yield return 0.5f;
        SetLives(3);
        player = game.Player;
        game.PlayerDied(player);
        yield return 0.2f;
        Report("lives: first death leaves 2 lives", Lives() == 2, "lives " + Lives());
        game.PlayerDied(player);
        yield return 0.2f;
        game.PlayerDied(player);
        yield return 0.2f;
        Report("lives: the third death is game over", State() == "GameOver", "state " + State());
        typeof(Game).GetMethod("OnConfirm", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
        yield return 0.5f;
        Report("lives: after game over the run restarts at stage 1 with 3 lives", LevelIndex() == 0 && Lives() == 3 && State() == "Playing",
            "stage " + (LevelIndex() + 1) + ", lives " + Lives() + ", state " + State());

        // ---------- carry-over: lives carry between stages, a cleared stage gives one heart ----------
        game.DebugStage(0);
        yield return 0.5f;
        SetLives(3);
        player = game.Player;
        game.PlayerDied(player);
        yield return 0.2f;
        game.PlayerDied(player);
        yield return 0.2f;
        game.LevelComplete();
        yield return 3.4f; // celebration, then the next stage is built
        Report("carry-over: 3 lives, 2 deaths, stage cleared = 2 lives in the next stage", LevelIndex() == 1 && Lives() == 2,
            "stage " + (LevelIndex() + 1) + ", lives " + Lives());

        // ---------- hearts: pickup gives a life, and is kept when already at the maximum ----------
        var heartType = typeof(Game).Assembly.GetType("HeartPickup");
        if (heartType == null)
        {
            Report("hearts: a heart pickup exists in the game", false, "HeartPickup type not found");
        }
        else
        {
            game.DebugStage(1);
            yield return 0.5f;
            SetLives(2);
            player = game.Player;
            var heart = Object.FindFirstObjectByType(heartType) as Component;
            Report("hearts: stage 2 contains a bonus heart", heart != null);
            if (heart != null)
            {
                Teleport(player, heart.transform.position + Vector3.down * 0.3f);
                yield return 0.5f;
                Report("hearts: touching a heart gives one life", Lives() == 3, "lives " + Lives());

                game.DebugStage(1);
                yield return 0.5f;
                SetLives(5);
                player = game.Player;
                heart = Object.FindFirstObjectByType(heartType) as Component;
                Teleport(player, heart.transform.position + Vector3.down * 0.3f);
                yield return 0.5f;
                Report("hearts: with 5 lives the heart gives nothing and stays", Lives() == 5 && Object.FindFirstObjectByType(heartType) != null, "lives " + Lives());
            }
        }
    }
}
#endif
