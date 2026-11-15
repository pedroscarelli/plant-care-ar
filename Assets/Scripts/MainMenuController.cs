using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string arSceneName = "ARScene";
    [SerializeField] private GameObject deniedMessage;

    // Hook this to the Start button's OnClick
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
        callbacks.PermissionDenied += _ => deniedMessage.SetActive(true);
        callbacks.PermissionDeniedAndDontAskAgain += _ => deniedMessage.SetActive(true);
        Permission.RequestUserPermission(Permission.Camera, callbacks);
#else
        LoadAR();
#endif
    }

    private void LoadAR() => SceneManager.LoadScene(arSceneName);
}