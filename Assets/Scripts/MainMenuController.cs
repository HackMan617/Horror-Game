using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Start-screen buttons. Play loads the game environment; Quit exits
/// (stops Play mode in the editor).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Scene loaded by the Play button.")]
    public string gameSceneName = "Sandbox3D";

    public void Play()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
