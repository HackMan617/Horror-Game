using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the gameplay HUD (compass + dread detector) alive across scene loads so it follows the
/// player through the door between the exterior and interior, and hides it in non-gameplay scenes
/// (the main menu / character select). Put this on a root "HUD" object whose children are the HUD
/// canvases. A copy re-loaded with a scene self-destructs, so exactly one HUD survives.
/// </summary>
public class PersistentHud : MonoBehaviour
{
    static PersistentHud _instance;

    [Tooltip("Scenes where the HUD stays hidden (menus / character select).")]
    public string[] hideInScenes = { "MainMenu", "CharacterSelect" };

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (_instance == this) { _instance = null; SceneManager.sceneLoaded -= OnSceneLoaded; }
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode) => Apply(s.name);

    void Apply(string sceneName)
    {
        bool show = System.Array.IndexOf(hideInScenes, sceneName) < 0;
        foreach (Transform child in transform)
            child.gameObject.SetActive(show);
    }
}
