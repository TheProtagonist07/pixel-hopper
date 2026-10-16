#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Records a promo clip of one stage with a scripted player (<see cref="ClipBot"/>) and keeps the first take in which the hero
/// stomps the enemy, picks up a bonus heart and clears the stage. Needs a window (the game has to render), so do NOT use -batchmode:
///   Unity -projectPath PROJECT -executeMethod ClipRecorder.Run -clipOut /path/to/folder -clipStage 1
/// Arguments: -clipOut = folder for the frames (required), -clipStage = stage index, 0 = stage 1 (default 1, which has an enemy and a heart),
/// -clipFps = frames per second (default 30), -clipSuper = resolution multiplier of the game view (default 2).
/// Result: frames in OUT/frames (PNG, black bars still included), and OUT/result.txt with the outcome of each take.
/// Turn the frames into a video with tools/make_clip.py. The editor opens for a minute or two and quits by itself.
/// </summary>
public static class ClipRecorder
{
    static IEnumerator routine;
    static double waitUntil;
    static string outDir;
    static bool oldEnabled;
    static EnterPlayModeOptions oldOptions;
    static readonly StringBuilder log = new StringBuilder();
    static bool success;

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
        outDir = Arg("-clipOut", "");
        if (string.IsNullOrEmpty(outDir)) { Debug.LogError("ClipRecorder: pass -clipOut /path/to/folder"); EditorApplication.Exit(1); return; }
        Directory.CreateDirectory(outDir);

        string scene = EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(scene);
        oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        Game.AutoPlay = false;
        routine = Record(int.Parse(Arg("-clipStage", "1")), int.Parse(Arg("-clipFps", "30")), int.Parse(Arg("-clipSuper", "2")));
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
        File.WriteAllText(Path.Combine(outDir, "result.txt"), log.ToString() + (success ? "CLIP RESULT: OK\n" : "CLIP RESULT: NO GOOD TAKE\n"));
        Debug.Log(log.ToString());
        Time.captureFramerate = 0;
        EditorApplication.ExitPlaymode();
        EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
        EditorSettings.enterPlayModeOptions = oldOptions;
        EditorApplication.Exit(success ? 0 : 1);
    }

    static string State() => typeof(Game).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Game.Instance).ToString();

    // Each take uses a slightly different landing-time guess for the stomp, because a take is deterministic and repeating it unchanged would give the same result.
    static IEnumerator Record(int stage, int fps, int superSize)
    {
        var game = Game.Instance;
        yield return 2.0f; // let the title stage build
        float[] guesses = { 0.88f, 0.84f, 0.92f, 0.80f, 0.96f };
        for (int take = 0; take < guesses.Length && !success; take++)
        {
            string frames = Path.Combine(outDir, "frames");
            if (Directory.Exists(frames)) Directory.Delete(frames, true);

            game.DebugStage(stage, true); // true = keep the "STAGE n" banner at the start
            yield return 0.3f;
            var player = game.Player;
            var bot = player.gameObject.AddComponent<ClipBot>();
            bot.landingTime = guesses[take];
            var capture = ClipCapture.Begin(frames, fps, superSize);

            // Record until the stage is cleared, plus about 1.5 seconds of the clear panel. Give up after 60 seconds.
            float started = Time.time;
            bool cleared = false;
            float clearedAt = 0f;
            while (Time.time - started < 60f)
            {
                if (!cleared && State() == "StageClear") { cleared = true; clearedAt = Time.time; }
                if (cleared && Time.time - clearedAt > 1.5f) break;
                if (State() == "GameOver") break;
                yield return null;
            }
            capture.Stop();
            yield return 0.5f;

            bool heartTaken = typeof(Game).Assembly.GetType("HeartPickup") != null && Object.FindAnyObjectByType(typeof(Game).Assembly.GetType("HeartPickup")) == null;
            int living = 0;
            foreach (var e in Object.FindObjectsByType<Enemy>()) if (!e.IsDead) living++;
            bool enemyDead = living == 0;
            log.AppendLine($"take {take + 1} (landingTime {guesses[take]:F2}): cleared={cleared} heartTaken={heartTaken} enemiesAlive={living} frames={capture.Frames} seconds={capture.Frames / (float)fps:F1}");
            success = cleared && heartTaken && enemyDead;
        }
    }
}
#endif
