using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;

/// <summary>
/// Stops an iOS build that would open without asking for the camera:
/// ARKit must be the XR loader for iOS and the Camera Usage Description must be filled in.
/// </summary>
class IOSBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS)
            return;

        if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.cameraUsageDescription))
            throw new BuildFailedException(
                "[CuidAR] Player Settings > iOS > Camera Usage Description is empty: iOS would never ask for the camera.");

        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
        var loaders = settings != null && settings.Manager != null ? settings.Manager.activeLoaders : null;
        if (loaders == null || !loaders.Any(l => l != null && l.GetType().Name.Contains("ARKit")))
            throw new BuildFailedException(
                "[CuidAR] Apple ARKit is not enabled for iOS: Project Settings > XR Plug-in Management > iOS tab > check Apple ARKit.");
    }
}
