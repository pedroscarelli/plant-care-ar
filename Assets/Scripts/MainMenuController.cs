using System.Collections;
using CuidAR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string arSceneName = "ARScene";
    [SerializeField] private GameObject deniedMessage;

    private bool m_Requesting;

    private void Start()
    {
        if (deniedMessage != null)
            deniedMessage.SetActive(false);

        // Background, title and illustration (built in code, see MainMenuStyle).
        Button startButton = null;
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.name == "StartButton")
                startButton = button;
        var canvas = startButton != null ? startButton.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas != null)
            MainMenuStyle.Apply(canvas.rootCanvas, startButton);
    }

    // Hooked to the Start button's OnClick
    public void OnStartPressed()
    {
        if (!m_Requesting)
            StartCoroutine(RequestAndLoad());
    }

    private IEnumerator RequestAndLoad()
    {
        m_Requesting = true;
        // Shows the system prompt the first time (iOS uses a native plugin, see CameraPermission).
        yield return CameraPermission.Request();
        m_Requesting = false;

        if (CameraPermission.IsGranted)
            SceneManager.LoadScene(arSceneName);
        else
            ShowDenied();
    }

    private void ShowDenied()
    {
        if (deniedMessage == null)
            return;
        deniedMessage.SetActive(true);
        // Tapping the message opens this app's page in the system Settings (camera toggle).
        if (deniedMessage.GetComponent<Button>() == null)
        {
            deniedMessage.AddComponent<Button>().onClick.AddListener(CameraPermission.OpenAppSettings);
            foreach (var graphic in deniedMessage.GetComponentsInChildren<Graphic>())
                graphic.raycastTarget = true;
        }
    }

    // Coming back from Settings with the camera allowed: hide the message.
    private void OnApplicationFocus(bool focus)
    {
        if (focus && deniedMessage != null && deniedMessage.activeSelf && CameraPermission.IsGranted)
            deniedMessage.SetActive(false);
    }
}
