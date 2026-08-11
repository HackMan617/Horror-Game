using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The inventory selection screen (Assets/Animation/INVENTORY.md): a centred modal of five slots with a
/// highlighted cursor you move between them, a detail pane, and a drop action. In the nightmare realm the
/// items flagged <see cref="ItemDef.locksInNightmare"/> (axe, key) go cold and padlocked, the cursor skips
/// them, and they cannot be selected or dropped.
///
/// Two deliberate departures from the handoff:
/// <list type="bullet">
/// <item><b>I opens it, not E.</b> E is this game's universal interact key — the axe, the stump, the
/// hearth, the truck door, the bed, the note and the partner all read it — so binding the inventory to
/// it would have opened the modal every time you touched anything.</item>
/// <item><b>It builds its own canvas</b> instead of expecting five wired slot prefabs, the same way
/// <see cref="DialogUI"/> and <see cref="CockpitController"/> do here. Nothing to hook up by hand and
/// nothing to go stale when a scene is regenerated.</item>
/// </list>
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    /// <summary>True while the modal is up — the pause menu checks this so Escape closes one thing.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Escape was spent closing the inventory this frame; GameManager must not also pause.</summary>
    public static bool ConsumedEscapeThisFrame { get; private set; }

    [Header("Icons sliced from item_icons.png, in index order (LOGS, AXE, NOTE, KEY)")]
    public Sprite[] iconSprites = new Sprite[4];

    [Header("Realm")]
    [Tooltip("Driven from NightmareController when one is in the scene; set by hand to preview the look.")]
    public bool nightmare = false;
    public Color coldTint = new Color(0.60f, 0.69f, 0.63f);

    [Header("Testing")]
    [Tooltip("Seed a NOTE and a KEY on first run so the slots have something in them to try the screen " +
             "with. Logs and the axe arrive from the real world pickups; these two have no source yet.")]
    public bool seedPlaceholders = true;

    const int N = InventoryModel.Slots;

    // --- built UI ---
    Font _font;
    GameObject _panelRoot;
    Image[] _slotBg = new Image[N];
    Image[] _slotIcon = new Image[N];
    Text[] _slotCount = new Text[N];
    Image[] _slotSelect = new Image[N];      // ring drawn BEHIND the slot, so it frames without tinting it
    Image[] _lockOverlay = new Image[N];
    Text _selName, _selDesc, _hint;
    Button _dropButton;
    Sprite _quad, _padlock;

    int _sel;
    float _pulse;
    static bool _seeded;          // session-wide: a fresh scene must not hand out fresh test items
    NightmareController _realm;
    float _realmScan;
    Behaviour _playerCtrl;
    CursorLockMode _cursor0;
    bool _cursorHidden0;

    void Awake()
    {
        Instance = this;
        _font = Resources.Load<Font>("HerculesPixelFontRegular");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _quad = SolidSprite();
        _padlock = PadlockSprite();
        BuildUI();
    }

    void OnEnable() { InventoryModel.Changed += Refresh; }
    void OnDisable() { InventoryModel.Changed -= Refresh; }

    void Start()
    {
        // One-time so re-entering a scene doesn't keep handing out fresh test items.
        if (seedPlaceholders && !_seeded && InventoryModel.UsedSlots == 0)
        {
            _seeded = true;
            InventoryModel.TryAdd(ItemType.Note, out _);
            InventoryModel.TryAdd(ItemType.Key, out _);
        }
        SetOpen(false);
    }

    void Update()
    {
        ConsumedEscapeThisFrame = false;

        // Follow the realm if this scene has a nightmare director. Looked up on a slow timer rather than
        // every frame — the nightmare arrives mid-scene, so it can't just be resolved once at Start.
        if (_realm == null && (_realmScan -= Time.unscaledDeltaTime) <= 0f)
        {
            _realmScan = 0.5f;
            _realm = FindAnyObjectByType<NightmareController>();
        }
        if (_realm != null && _realm.IsNightmare != nightmare) SetNightmare(_realm.IsNightmare);

        bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;

        if (!IsOpen)
        {
            if (!paused && TogglePressed()) SetOpen(true);
            return;
        }

        if (EscapePressed() || TogglePressed())
        {
            ConsumedEscapeThisFrame = true;   // so the pause menu doesn't open behind us
            SetOpen(false);
            return;
        }

        if (LeftPressed()) Move(-1);
        if (RightPressed()) Move(+1);
        for (int n = 0; n < N; n++) if (DigitPressed(n)) Pick(n);
        if (DropPressed()) DropSelected();

        // The cursor frame breathes so it reads as "here you are" at a glance.
        _pulse += Time.unscaledDeltaTime * 3.2f;
        float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_pulse));
        for (int i = 0; i < N; i++)
            if (_slotSelect[i] != null && _slotSelect[i].enabled)
            {
                var c = _slotSelect[i].color; c.a = a; _slotSelect[i].color = c;
            }
    }

    // ---------------------------------------------------------------- state

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (_panelRoot != null) _panelRoot.SetActive(open);

        // Modal: the player shouldn't walk, look or swing an axe while reading their pockets.
        if (open)
        {
            var pgo = GameObject.Find("Player");
            _playerCtrl = pgo != null ? pgo.GetComponent<PlayerController3D>() : null;
            if (_playerCtrl != null) _playerCtrl.enabled = false;
            _cursor0 = Cursor.lockState; _cursorHidden0 = !Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (Locked(_sel)) Move(+1);
        }
        else
        {
            if (_playerCtrl != null) { _playerCtrl.enabled = true; _playerCtrl = null; }
            Cursor.lockState = _cursor0; Cursor.visible = !_cursorHidden0;
        }
        Refresh();
    }

    public void SetNightmare(bool nm)
    {
        nightmare = nm;
        if (Locked(_sel)) Move(+1);
        Refresh();
    }

    bool Locked(int i)
    {
        var s = InventoryModel.At(i);
        return !s.Empty && nightmare && InventoryModel.Def(s.type).locksInNightmare;
    }

    void Move(int dir)
    {
        for (int n = 0; n < N; n++)
        {
            _sel = (_sel + dir + N) % N;
            if (!Locked(_sel)) break;      // corrupted slots are skipped over, never landed on
        }
        Refresh();
    }

    void Pick(int i)
    {
        if (i < 0 || i >= N || Locked(i)) return;
        _sel = i;
        Refresh();
    }

    void DropSelected()
    {
        if (Locked(_sel)) return;
        var s = InventoryModel.At(_sel);
        if (s.Empty) return;
        string name = InventoryModel.Def(s.type).name.ToLower();
        InventoryModel.DropOne(_sel);
        if (DialogUI.Instance != null) DialogUI.Instance.ShowDialog("dropped " + name, 1.2f);
    }

    // ---------------------------------------------------------------- paint

    void Refresh()
    {
        if (_panelRoot == null) return;

        for (int i = 0; i < N; i++)
        {
            var s = InventoryModel.At(i);
            bool locked = Locked(i);

            if (_slotIcon[i] != null)
            {
                bool show = !s.Empty;
                _slotIcon[i].enabled = show;
                if (show)
                {
                    int idx = InventoryModel.Def(s.type).iconIndex;
                    _slotIcon[i].sprite = (iconSprites != null && idx < iconSprites.Length) ? iconSprites[idx] : null;
                    // Corruption is a LOOK, not data: one clean icon serves both realms.
                    _slotIcon[i].color = locked ? coldTint
                                       : (nightmare ? Color.Lerp(Color.white, coldTint, 0.5f) : Color.white);
                }
            }

            if (_slotCount[i] != null)
            {
                bool show = !s.Empty && s.count > 1;
                _slotCount[i].enabled = show;
                if (show)
                {
                    _slotCount[i].text = s.count.ToString();
                    _slotCount[i].color = s.count >= InventoryModel.MaxStack
                                        ? new Color(0.91f, 0.63f, 0.18f) : Color.white;
                }
            }

            if (_slotSelect[i] != null) _slotSelect[i].enabled = (i == _sel);
            if (_lockOverlay[i] != null) _lockOverlay[i].enabled = locked;
            if (_slotBg[i] != null)
                _slotBg[i].color = locked ? new Color(0.10f, 0.13f, 0.12f, 1f)
                                          : new Color(0.11f, 0.11f, 0.14f, 1f);
        }

        var cur = InventoryModel.At(_sel);
        bool curLock = Locked(_sel);
        if (_selName != null)
            _selName.text = cur.Empty ? "- empty slot -"
                          : (curLock ? InventoryModel.Def(cur.type).name + "   LOCKED"
                                     : InventoryModel.Def(cur.type).name);
        if (_selDesc != null)
            _selDesc.text = cur.Empty ? "Nothing here yet. Pick something up."
                          : (curLock ? "Corrupted in the nightmare. You cannot bring yourself to touch it."
                                     : InventoryModel.Def(cur.type).description);
        if (_dropButton != null) _dropButton.interactable = !cur.Empty && !curLock;
        if (_hint != null)
            _hint.text = "A / D  move     1-5  jump     Q  drop     I  close"
                       + (nightmare ? "     - some resist -" : "");
    }

    // ---------------------------------------------------------------- build

    void BuildUI()
    {
        var canvasGo = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;                      // over the prompts (200), under the pause menu (1000)
        var sc = canvasGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;

        _panelRoot = new GameObject("Panel", typeof(RectTransform));
        _panelRoot.transform.SetParent(canvasGo.transform, false);
        Stretch(_panelRoot.GetComponent<RectTransform>());

        // dim the world behind the modal
        var dim = MakeImage(_panelRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);

        var board = MakeImage(_panelRoot.transform, "Board", new Color(0.07f, 0.07f, 0.09f, 0.98f));
        Place(board.rectTransform, Vector2.zero, new Vector2(1180f, 700f));
        var edge = board.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.55f, 0.48f, 0.36f, 0.9f);
        edge.effectDistance = new Vector2(4f, -4f);

        var title = MakeText(board.transform, new Vector2(0f, 286f), new Vector2(900f, 70f), 46, TextAnchor.MiddleCenter);
        title.text = "INVENTORY";
        title.color = new Color(0.87f, 0.82f, 0.66f);

        // ---- the five slots ----
        const float slot = 168f, gap = 26f;
        float rowW = N * slot + (N - 1) * gap;
        for (int i = 0; i < N; i++)
        {
            float x = -rowW * 0.5f + slot * 0.5f + i * (slot + gap);

            // The cursor ring goes in FIRST so it draws behind the slot: an Outline on the slot itself
            // re-draws the whole panel four times at an offset, which bled its colour through the fill and
            // left the selected slot looking stained rather than framed.
            _slotSelect[i] = MakeImage(board.transform, "Select" + i, new Color(0.95f, 0.80f, 0.35f, 1f));
            Place(_slotSelect[i].rectTransform, new Vector2(x, 120f), new Vector2(slot + 14f, slot + 14f));
            _slotSelect[i].raycastTarget = false;
            _slotSelect[i].enabled = false;

            // Opaque, so the ring behind it reads as a border and never tints the slot.
            _slotBg[i] = MakeImage(board.transform, "Slot" + i, new Color(0.11f, 0.11f, 0.14f, 1f));
            Place(_slotBg[i].rectTransform, new Vector2(x, 120f), new Vector2(slot, slot));

            // the "little highlights" from the handoff: four corner ticks so an empty slot still reads as a slot
            var corner = new Color(0.45f, 0.41f, 0.32f, 0.85f);
            for (int c = 0; c < 4; c++)
            {
                var t = MakeImage(_slotBg[i].transform, "Corner" + c, corner);
                float cx = ((c & 1) == 0 ? -1f : 1f) * (slot * 0.5f - 13f);
                float cy = ((c & 2) == 0 ? -1f : 1f) * (slot * 0.5f - 13f);
                Place(t.rectTransform, new Vector2(cx, cy), new Vector2(16f, 16f));
            }

            _slotIcon[i] = MakeImage(_slotBg[i].transform, "Icon", Color.white);
            Place(_slotIcon[i].rectTransform, Vector2.zero, new Vector2(104f, 104f));
            _slotIcon[i].sprite = null;
            _slotIcon[i].enabled = false;
            _slotIcon[i].raycastTarget = false;

            // Kept clear of the slot edge — at the old offset the stack count hung half outside the frame.
            _slotCount[i] = MakeText(_slotBg[i].transform, new Vector2(-8f, -52f), new Vector2(140f, 38f), 32, TextAnchor.MiddleRight);
            _slotCount[i].enabled = false;

            _lockOverlay[i] = MakeImage(_slotBg[i].transform, "Lock", new Color(0.85f, 0.86f, 0.90f, 0.95f));
            _lockOverlay[i].sprite = _padlock;
            Place(_lockOverlay[i].rectTransform, new Vector2(-46f, 46f), new Vector2(44f, 44f));
            _lockOverlay[i].enabled = false;
            _lockOverlay[i].raycastTarget = false;

            // clicking a slot selects it, same as the keys
            int captured = i;
            var btn = _slotBg[i].gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => Pick(captured));
        }

        // ---- detail pane ----
        _selName = MakeText(board.transform, new Vector2(0f, -20f), new Vector2(1000f, 60f), 40, TextAnchor.MiddleCenter);
        _selName.color = new Color(0.93f, 0.90f, 0.80f);
        _selDesc = MakeText(board.transform, new Vector2(0f, -100f), new Vector2(980f, 110f), 30, TextAnchor.UpperCenter);
        _selDesc.color = new Color(0.72f, 0.70f, 0.64f);

        var dropGo = MakeImage(board.transform, "Drop", new Color(0.28f, 0.14f, 0.14f, 0.95f));
        Place(dropGo.rectTransform, new Vector2(0f, -212f), new Vector2(280f, 66f));
        var dropEdge = dropGo.gameObject.AddComponent<Outline>();
        dropEdge.effectColor = new Color(0.62f, 0.34f, 0.30f, 0.9f);
        dropEdge.effectDistance = new Vector2(3f, -3f);
        var dropLabel = MakeText(dropGo.transform, Vector2.zero, new Vector2(280f, 66f), 32, TextAnchor.MiddleCenter);
        dropLabel.text = "DROP  (Q)";
        dropLabel.raycastTarget = false;
        _dropButton = dropGo.gameObject.AddComponent<Button>();
        _dropButton.targetGraphic = dropGo;
        _dropButton.onClick.AddListener(DropSelected);

        _hint = MakeText(board.transform, new Vector2(0f, -292f), new Vector2(1100f, 44f), 26, TextAnchor.MiddleCenter);
        _hint.color = new Color(0.58f, 0.56f, 0.52f);

        _panelRoot.SetActive(false);
    }

    Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = _quad;
        img.color = color;
        return img;
    }

    Text MakeText(Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = _font;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(2f, -2f);
        Place(t.rectTransform, pos, size);
        return t;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // A plain white quad every panel is a tint of — same trick ForestDebris uses for its leaves.
    static Sprite SolidSprite()
    {
        var tex = Texture2D.whiteTexture;
        var s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f);
        s.name = "InventoryQuad";
        return s;
    }

    // A 16x16 padlock badge, drawn in code so the corrupted slots need no extra art.
    static Sprite PadlockSprite()
    {
        const int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                bool body = x >= 3 && x <= 12 && y >= 2 && y <= 9;                 // the lock body
                bool shackleOuter = (x >= 5 && x <= 10) && y >= 9 && y <= 13;
                bool shackleInner = (x >= 7 && x <= 8) && y >= 9 && y <= 12;
                bool shackle = shackleOuter && !shackleInner;
                bool keyhole = x >= 7 && x <= 8 && y >= 4 && y <= 7;               // punched out of the body
                tex.SetPixel(x, y, (body && !keyhole) || shackle ? Color.white : clear);
            }
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 16f);
        s.name = "InventoryPadlock";
        return s;
    }

    // ---------------------------------------------------------------- input
    // Both back-ends, matching every other interactable in the project.

    static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.I);
#endif
    }

    static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    static bool LeftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
#endif
    }

    static bool RightPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
#endif
    }

    static bool DropPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Q);
#endif
    }

    static bool DigitPressed(int n)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (n)
        {
            case 0: return kb.digit1Key.wasPressedThisFrame;
            case 1: return kb.digit2Key.wasPressedThisFrame;
            case 2: return kb.digit3Key.wasPressedThisFrame;
            case 3: return kb.digit4Key.wasPressedThisFrame;
            case 4: return kb.digit5Key.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.Alpha1 + n);
#endif
    }
}
