#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Runs the test bot (AutoPlayer) from the command line with no editor window, then exits with a result code.
/// Use it to prove stages can still be completed after a change, for example in a release checklist.
///
/// Command line (the project must not be open in the Unity editor):
///   Unity -batchmode -nographics -projectPath PROJECT -executeMethod BatchBot.Run -botStage 0 -botStages 1 -logFile -
/// Arguments (all optional): -botStage = first stage to play (0 = stage 1, default 0), -botStages = how many stages
/// to play in a row (default 1), -botSpeed = Time.timeScale while playing (default 4, higher is faster but less
/// like real play), -botLimit = real-time seconds before giving up (default 600), -botOut = file to write the result.
/// Exit code 0 means every requested stage was cleared with no timeout; 1 means a stage timed out or the run hit the limit.
///
/// How it works: it opens the game scene, turns on Game.AutoPlay, enters Play mode with domain reload disabled (so the
/// static state and this script's update hook survive the switch) and watches Game.DebugLog until the requested number of
/// CLEAR lines has appeared. The editor setting for fast play mode is restored when it finishes.
/// </summary>
public static class BatchBot
{
    static int wantedClears;
    static float startedAt;
    static float limit;
    static string outPath;
    static bool oldOptionsEnabled;
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
        int first = int.Parse(Arg("-botStage", "0"));
        wantedClears = int.Parse(Arg("-botStages", "1"));
        float speed = float.Parse(Arg("-botSpeed", "4"));
        limit = float.Parse(Arg("-botLimit", "600"));
        outPath = Arg("-botOut", "");

        // Open the scene that contains the Game object (the only scene in the project).
        string scene = EditorBuildSettings.scenes.Length > 0 ? EditorBuildSettings.scenes[0].path : "Assets/Scenes/SampleScene.unity";
        EditorSceneManager.OpenScene(scene);

        // Remember the editor's play-mode settings, then skip the domain reload so our static fields and hooks survive.
        oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

        Game.AutoPlay = true;
        Game.DebugStartStage = first;
        Game.DebugLog.Clear();
        Time.timeScale = 1f;
        startedAt = (float)EditorApplication.timeSinceStartup;

        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Poll;
        EditorApplication.EnterPlaymode();
        sPlaySpeed = speed;
    }

    static float sPlaySpeed;

    /// <summary>
    /// Entry point for -executeMethod BatchBot.Report: prints one line per stage with its width in tiles and how many
    /// coins, enemies and spike tiles it contains, then exits. Runs without Play mode, so it takes only a few seconds.
    /// Use it to see exactly what a change to the level generator did (run it before and after and compare).
    /// </summary>
    public static void Report()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Levels.Count; i++)
        {
            var g = Levels.Build(i);
            int coins = 0, enemies = 0, spikes = 0;
            for (int x = 0; x < g.Width; x++)
            {
                for (int y = 0; y < g.Height; y++)
                {
                    char c = g[x, y];
                    if (c == 'c') coins++;
                    else if (c == 'e') enemies++;
                    else if (c == '^') spikes++;
                }
            }
            sb.AppendLine($"stage {i + 1,2}: width={g.Width,3} coins={coins,3} enemies={enemies,2} spikes={spikes,2} enemySpeed={g.EnemySpeed:F2}");
        }
        string text = sb.ToString();
        Debug.Log(text);
        string path = Arg("-botOut", "");
        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, text);
        EditorApplication.Exit(0);
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        // Speed the game up once Play mode has started (Time.timeScale resets when Play mode begins).
        if (change == PlayModeStateChange.EnteredPlayMode) Time.timeScale = sPlaySpeed;
    }

    // Called every editor frame: looks for the finish condition.
    static void Poll()
    {
        if (!EditorApplication.isPlaying) return;

        int clears = 0;
        bool timeout = false;
        foreach (var line in Game.DebugLog)
        {
            if (line.Contains("CLEAR")) clears++;
            if (line.Contains("TIMEOUT")) timeout = true;
        }
        bool allDone = clears >= wantedClears;
        bool overLimit = EditorApplication.timeSinceStartup - startedAt > limit;
        if (!allDone && !timeout && !overLimit) return;

        EditorApplication.update -= Poll;
        EditorApplication.playModeStateChanged -= OnPlayMode;

        var sb = new StringBuilder();
        foreach (var line in Game.DebugLog) sb.AppendLine(line);
        bool ok = allDone && !timeout;
        sb.AppendLine(ok ? "BOT RESULT: PASS" : "BOT RESULT: FAIL (" + (timeout ? "stage timeout" : "run limit reached") + ")");
        Debug.Log(sb.ToString());
        if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, sb.ToString());

        Time.timeScale = 1f;
        Game.AutoPlay = false;
        EditorApplication.ExitPlaymode();
        EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
        EditorSettings.enterPlayModeOptions = oldOptions;
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
#endif
