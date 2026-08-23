using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // Loads scene by name (recommended)
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // Optional: quit button (works in build, not editor)
    public void QuitGame()
    {
        Application.Quit();
    }
}