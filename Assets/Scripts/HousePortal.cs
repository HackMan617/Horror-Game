using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Walk up to the front of the cabin and press E to enter: shows a prompt, plays the
/// door-opening frames from house_tiles.png on the CabinDoor quad, fades to black, then
/// loads the interior scene. Placed on the cabin's front door.
///
/// The door is a single flat mesh whose four UVs map to one 24x24 cell of the 192x144
/// atlas. We animate it by rewriting those UVs through a sequence of atlas cells
/// (closed -> open). Cells are (column,row) coordinates in the same layout
/// HorrorGame3DSetup uses to build the cabin, so the door stays consistent with the walls.
/// </summary>
public class HousePortal : MonoBehaviour
{
    public Transform player;
    public string interiorScene = "Sandbox3D";
    public float range = 3.5f;
    public float fadeDuration = 0.7f;
    public AudioClip openSound;         // played as the door swings open

    [Header("Door open animation (house_tiles.png cells: column,row)")]
    // Windowed double door opening: closed (4,3) -> fully open (7,3).
    // For the single plank door instead, set these to (4,5),(5,5),(6,5),(7,5).
    public Vector2Int[] doorFrames =
    {
        new Vector2Int(4, 3), new Vector2Int(5, 3), new Vector2Int(6, 3), new Vector2Int(7, 3),
    };
    public float openDuration = 0.9f;    // total time to flip through the open frames (slower = creakier)
    public float pauseAfterOpen = 0.25f; // beat on the fully-open frame before the fade

    [Header("Atlas layout (must match house_tiles.png)")]
    public int atlasWidth = 192;
    public int atlasHeight = 144;
    public int tileSize = 24;

    bool _entering;
    float _fade;
    Texture2D _black;
    Mesh _doorMesh;
    Vector2[] _uv;
    AudioSource _audio;

    void Awake()
    {
        _black = new Texture2D(1, 1);
        _black.SetPixel(0, 0, Color.white);
        _black.Apply();

        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;   // 2D: it's a close-range interaction cue
    }

    void Start()
    {
        var mf = GetComponent<MeshFilter>();
        var mesh = mf != null ? mf.mesh : null;   // per-instance copy; safe to rewrite at runtime
        if (mesh != null && mesh.vertexCount == 4)
        {
            _doorMesh = mesh;
            _uv = new Vector2[4];
            if (doorFrames != null && doorFrames.Length > 0)
                SetDoorFrame(doorFrames[0]);       // rest on the closed frame
        }
    }

    void Update()
    {
        if (_entering || player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        Vector3 a = player.position; a.y = 0f;
        Vector3 b = transform.position; b.y = 0f;
        bool inFront = Vector3.Distance(a, b) <= range && player.position.z < transform.position.z;
        if (!inFront) return;

        if (DialogUI.Instance != null) DialogUI.Instance.ShowPrompt("Press E to enter");
        if (EnterPressed()) StartCoroutine(Enter());
    }

    IEnumerator Enter()
    {
        _entering = true;
        if (openSound != null && _audio != null) _audio.PlayOneShot(openSound);   // creak as it swings

        yield return OpenDoor();
        if (pauseAfterOpen > 0f) yield return new WaitForSeconds(pauseAfterOpen);

        float ft = 0f;
        while (ft < fadeDuration)
        {
            ft += Time.deltaTime;
            _fade = Mathf.Clamp01(ft / fadeDuration);
            yield return null;
        }
        _fade = 1f;
        SceneManager.LoadScene(interiorScene);
    }

    // Flip through the door cells (closed -> open) on the door quad's UVs.
    IEnumerator OpenDoor()
    {
        if (_doorMesh == null || doorFrames == null || doorFrames.Length == 0) yield break;
        float per = openDuration / doorFrames.Length;
        for (int i = 0; i < doorFrames.Length; i++)
        {
            SetDoorFrame(doorFrames[i]);
            if (per > 0f) yield return new WaitForSeconds(per);
        }
    }

    // Point the door quad's four UVs at atlas cell (col,row), with the same half-texel
    // inset the cabin mesh uses so the tile doesn't bleed into its neighbours.
    void SetDoorFrame(Vector2Int cell)
    {
        if (_doorMesh == null || _uv == null) return;
        float aw = atlasWidth, ah = atlasHeight, tp = tileSize;
        float u0 = cell.x * tp / aw, u1 = (cell.x + 1) * tp / aw;
        float v1 = 1f - cell.y * tp / ah, v0 = 1f - (cell.y + 1) * tp / ah;
        float eu = 0.5f / aw, ev = 0.5f / ah;
        float xMin = u0 + eu, xMax = u1 - eu, yMin = v0 + ev, yMax = v1 - ev;
        _uv[0] = new Vector2(xMin, yMin);   // bottom-left
        _uv[1] = new Vector2(xMax, yMin);   // bottom-right
        _uv[2] = new Vector2(xMax, yMax);   // top-right
        _uv[3] = new Vector2(xMin, yMax);   // top-left
        _doorMesh.uv = _uv;
    }

    void OnGUI()
    {
        if (_fade <= 0f) return;
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, _fade);
        GUI.depth = -1000;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _black);
        GUI.color = prev;
    }

    bool EnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
