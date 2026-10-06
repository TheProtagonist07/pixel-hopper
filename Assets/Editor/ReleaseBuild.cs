#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the WebGL release with the exact settings the itch.io version needs, so every update is built the same way.
/// Run it from the menu (Tools > Pixel Hopper > Build WebGL) or from the command line:
///   Unity -batchmode -nographics -projectPath PROJECT -executeMethod ReleaseBuild.Build -buildOut /path/to/folder -quit -logFile -
/// The settings are applied and the build is started in the same call on purpose: setting them in one step and building
/// in another lets Unity reset them (the template and splash screen silently went back to defaults before).
/// After the build, zip the folder so index.html is at the root of the zip, and upload that zip to itch.io (docs/WEBGL_BUILD_AND_ITCH.md).
/// </summary>
public static class ReleaseBuild
{
    const string Template = "PROJECT:PixelHopper";

    static string Arg(string name, string fallback)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return fallback;
    }

    [MenuItem("Tools/Pixel Hopper/Build WebGL")]
    public static void BuildFromMenu()
    {
        string folder = EditorUtility.SaveFolderPanel("Build WebGL into folder", "", "webgl");
        if (!string.IsNullOrEmpty(folder)) BuildTo(folder);
    }

    /// <summary>Command-line entry point. Exit code 0 on success, 1 on failure.</summary>
    public static void Build()
    {
        string folder = Arg("-buildOut", "");
        if (string.IsNullOrEmpty(folder))
        {
            Debug.LogError("ReleaseBuild: pass -buildOut /path/to/output/folder");
            EditorApplication.Exit(1);
            return;
        }
        bool ok = BuildTo(folder);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool BuildTo(string folder)
    {
        // Release settings (see docs/WEBGL_BUILD_AND_ITCH.md for the reason behind each one).
        PlayerSettings.WebGL.template = Template;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = folder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("ReleaseBuild: " + report.summary.result + ", " + (report.summary.totalSize / (1024 * 1024)) + " MB uncompressed output, " + report.summary.totalTime);
        return report.summary.result == BuildResult.Succeeded;
    }
}
#endif
