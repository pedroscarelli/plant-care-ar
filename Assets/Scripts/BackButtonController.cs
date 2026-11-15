using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButtonController : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenu";

    // Hook this to the Back button's OnClick
    public void OnBackPressed()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) // Android back button
            OnBackPressed();
    }
}