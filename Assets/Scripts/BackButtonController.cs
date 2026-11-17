using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class BackButtonController : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenu";

    // Hooked to the Back button's OnClick
    public void OnBackPressed()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    void Update()
    {
        // The project uses only the new Input System (the old Input class throws).
        // On Android the system back button arrives as Escape.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            OnBackPressed();
    }
}
