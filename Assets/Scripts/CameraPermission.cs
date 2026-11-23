using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CuidAR
{
    /// <summary>
    /// Camera permission on iOS and Android.
    ///
    /// iOS: uses a tiny native plugin (Assets/Plugins/iOS/CuidARCameraPermission.mm) because Unity strips its
    /// own camera permission code when the project doesn't use WebCamTexture, and then
    /// Application.RequestUserAuthorization answers "denied" without asking. iOS only lists the app under
    /// Settings > Camera after it has asked once, and shows the prompt only once; after a denial the user
    /// must enable the camera in Settings (see <see cref="OpenAppSettings"/>).
    /// The prompt text comes from Player Settings > Camera Usage Description (NSCameraUsageDescription).
    /// </summary>
    public static class CameraPermission
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int CuidAR_GetCameraStatus();
        [DllImport("__Internal")] static extern void CuidAR_RequestCamera();
        const int k_NotDetermined = 0;
        const int k_Authorized = 3;
#endif

        public static bool IsGranted
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_IOS
                return CuidAR_GetCameraStatus() == k_Authorized;
#elif UNITY_ANDROID
                return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
                return true;
#endif
            }
        }

        /// <summary>Shows the system prompt if the user hasn't answered it yet, and waits for the answer.</summary>
        public static IEnumerator Request()
        {
            if (IsGranted)
                yield break;
#if UNITY_IOS && !UNITY_EDITOR
            if (CuidAR_GetCameraStatus() != k_NotDetermined)
                yield break; // already denied/restricted: only Settings can change it
            CuidAR_RequestCamera();
            while (CuidAR_GetCameraStatus() == k_NotDetermined)
                yield return null;
#elif UNITY_ANDROID && !UNITY_EDITOR
            bool answered = false;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered = true;
            callbacks.PermissionDenied += _ => answered = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => answered = true;
            Permission.RequestUserPermission(Permission.Camera, callbacks);
            while (!answered)
                yield return null;
#endif
        }

        /// <summary>Opens this app's page in the system Settings.</summary>
        public static void OpenAppSettings()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Application.OpenURL("app-settings:");
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var uriClass = new AndroidJavaClass("android.net.Uri");
            using var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null);
            using var intent = new AndroidJavaObject("android.content.Intent",
                "android.settings.APPLICATION_DETAILS_SETTINGS", uri);
            activity.Call("startActivity", intent);
#else
            Debug.Log("[CuidAR] OpenAppSettings: only available on the phone.");
#endif
        }
    }
}
