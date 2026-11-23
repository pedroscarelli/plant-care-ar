using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command-line build: exports the Xcode project to plant-care-ar/ (replacing it).
/// Used by Tools/run_ios.sh:
///   Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod CuidARBuild.BuildIOS
/// </summary>
public static class CuidARBuild
{
    public static void BuildIOS()
    {
        var output = Environment.GetEnvironmentVariable("CUIDAR_IOS_OUT");
        if (string.IsNullOrEmpty(output))
            output = "plant-care-ar";

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/ARScene.unity" },
            locationPathName = output,
            target = BuildTarget.iOS,
            options = BuildOptions.None,
        });
        Debug.Log($"[CuidARBuild] {report.summary.result} -> {output}");
        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    /// <summary>
    /// Android APK (ARCore):
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod CuidARBuild.BuildAndroid
    /// Output: Builds/cuidAR.apk (or env CUIDAR_ANDROID_OUT). Install with: adb install -r Builds/cuidAR.apk
    /// </summary>
    public static void BuildAndroid()
    {
        var output = Environment.GetEnvironmentVariable("CUIDAR_ANDROID_OUT");
        if (string.IsNullOrEmpty(output))
            output = "Builds/cuidAR.apk";
        EditorUserBuildSettings.buildAppBundle = false;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/ARScene.unity" },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });
        Debug.Log($"[CuidARBuild] {report.summary.result} -> {output}");
        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
