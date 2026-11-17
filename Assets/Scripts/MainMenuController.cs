using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string arSceneName = "ARScene";
    [SerializeField] private GameObject deniedMessage;

    private void Start()
    {
        if (deniedMessage != null)
            deniedMessage.SetActive(false);
    }

    // Hooked to the Start button's OnClick
    public void OnStartPressed()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            LoadAR();
            return;
        }

        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => LoadAR();
        callbacks.PermissionDenied += _ => ShowDenied();
        callbacks.PermissionDeniedAndDontAskAgain += _ => ShowDenied();
        Permission.RequestUserPermission(Permission.Camera, callbacks);
#elif UNITY_IOS && !UNITY_EDITOR
        StartCoroutine(RequestCameraIOS());
#else
        LoadAR();
#endif
    }

#if UNITY_IOS
    // iOS shows the system prompt (text from Player Settings > Camera Usage Description) only once;
    // after a denial the user has to enable the camera in Settings.
    private IEnumerator RequestCameraIOS()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            LoadAR();
        else
            ShowDenied();
    }
#endif

    private void ShowDenied()
    {
        if (deniedMessage != null)
            deniedMessage.SetActive(true);
    }

    private void LoadAR() => SceneManager.LoadScene(arSceneName);
}
