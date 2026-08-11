# Player Inventory — implementation & handoff (Unity 2D)

A Minecraft-style **selection screen** (no bottom hotbar): a key opens a centered modal of **5 slots**,
each stack caps at **12**. The player moves a highlighted cursor between slots and drops the selected
item. In the **nightmare** realm certain items (axe, key) are **corrupted + locked** — glitched sprite,
padlock overlay, cursor skips them, can't be selected or dropped.

Prototype: `Canvas-3.dc.html` (live, interactive — the source of truth for feel and visuals).

---

## 1 · Item icons — `item_icons.png`

**16 × 16 px** cells, one row, 4 items → **64 × 16 px**. Order:

| Index | Rect | Item | Locks in nightmare |
|---|---|---|---|
| 0 | `(0,0,16,16)`  | LOGS | no |
| 1 | `(16,0,16,16)` | AXE  | **yes** |
| 2 | `(32,0,16,16)` | NOTE | no |
| 3 | `(48,0,16,16)` | KEY  | **yes** |

Import: **Sprite Mode Multiple · Grid By Cell Count 4 × 1 · Point (no filter) · Compression None ·
PPU 16 · Pivot Center.** (`item_icons-8x.png` is a preview blow-up — do not ship.)

The **corrupted/locked** look (glitch slices + cold tint + padlock) is generated at runtime in the
prototype, not baked. In Unity do the same with a material/overlay so one clean icon serves both
realms — see `InventoryUI` (`ApplyCorruption`) below.

---

## 2 · Data model — `InventoryModel.cs`

Five slots, stack cap 12, add/drop with the same spill rules as the prototype (top up a matching
non-full stack first, then a free slot, else refuse).

```csharp
using System;
using UnityEngine;

[Serializable] public enum ItemType { None, Log, Axe, Note, Key }

[Serializable]
public struct ItemDef {
    public ItemType type;
    public string name;
    [TextArea] public string description;
    public int iconIndex;        // column in item_icons.png (0..3)
    public bool locksInNightmare;
}

[Serializable] public struct Slot { public ItemType type; public int count; public bool Empty => type == ItemType.None || count <= 0; }

public class InventoryModel : MonoBehaviour
{
    public const int SLOTS = 5;
    public int maxStack = 12;

    public ItemDef[] defs = new ItemDef[] {
        new ItemDef{ type=ItemType.Log,  name="LOGS", iconIndex=0, locksInNightmare=false, description="Split pine, still cold from the truck bed. Fuel for the fire." },
        new ItemDef{ type=ItemType.Axe,  name="AXE",  iconIndex=1, locksInNightmare=true,  description="Worn hickory haft, bitten edge. Chops wood — and more." },
        new ItemDef{ type=ItemType.Note, name="NOTE", iconIndex=2, locksInNightmare=false, description="A scrap of paper, the ink run soft at the folds." },
        new ItemDef{ type=ItemType.Key,  name="KEY",  iconIndex=3, locksInNightmare=true,  description="Rust-toothed and heavy. It opens something you have not found." },
    };

    public Slot[] slots = new Slot[SLOTS];
    public event Action Changed;

    public ItemDef Def(ItemType t) { foreach (var d in defs) if (d.type == t) return d; return default; }
    public int UsedSlots { get { int n=0; foreach (var s in slots) if (!s.Empty) n++; return n; } }

    /// Returns true if the pickup was (fully) accepted; message explains a refusal.
    public bool TryAdd(ItemType t, out string message)
    {
        for (int i = 0; i < SLOTS; i++)                       // top up an existing stack
            if (slots[i].type == t && slots[i].count < maxStack) { slots[i].count++; message = "picked up " + Def(t).name.ToLower(); Changed?.Invoke(); return true; }
        for (int i = 0; i < SLOTS; i++)                       // else a free slot
            if (slots[i].Empty) { slots[i] = new Slot{ type=t, count=1 }; message = "picked up " + Def(t).name.ToLower(); Changed?.Invoke(); return true; }
        bool maxed = false; foreach (var s in slots) if (s.type == t) maxed = true;
        message = maxed ? Def(t).name.ToLower() + " stack is full (" + maxStack + ")" : "inventory full — no free slot";
        return false;
    }

    public void DropOne(int i)
    {
        if (i < 0 || i >= SLOTS || slots[i].Empty) return;
        slots[i].count--; if (slots[i].count <= 0) slots[i] = new Slot();
        Changed?.Invoke();
    }
}
```

---

## 3 · Modal + selection + nightmare lock — `InventoryUI.cs`

Toggle with **E** (Esc closes). Cursor: **←→ / A D** move (skips locked), **1–5** jump, click/hover
select, **Q** drop. In the nightmare, `locksInNightmare` items are glitch-tinted, padlocked, and
unselectable. Wire `slotButtons`/`slotIcons`/`slotCounts`/`selectHighlights`/`lockOverlays` to a row
of 5 slot prefabs, and slice `iconSprites` from `item_icons.png` (4 sprites in index order).

```csharp
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public InventoryModel model;
    public GameObject panelRoot;         // the centered modal (dim bg + panel); toggled by E
    public GameObject closedHint;        // "PRESS E — INVENTORY" shown when closed

    [Header("Realm")]
    public bool nightmare = false;

    [Header("5 slot widgets, in order")]
    public Button[]  slotButtons  = new Button[5];
    public Image[]   slotIcons     = new Image[5];
    public Text[]    slotCounts    = new Text[5];
    public GameObject[] selectHighlights = new GameObject[5];   // the pulsing corner-highlight frame
    public GameObject[] lockOverlays      = new GameObject[5];  // padlock badge

    [Header("Icons sliced from item_icons.png (index 0..3)")]
    public Sprite[] iconSprites = new Sprite[4];

    [Header("Detail pane")]
    public Text selName, selDesc;
    public Button dropButton;

    [Header("Corruption look")]
    public Material glitchMaterial;      // optional: cold-tint + glitch shader for locked icons
    public Color coldTint = new Color(0.60f, 0.69f, 0.63f);

    int sel = 0;

    void OnEnable()  { if (model != null) model.Changed += Refresh; }
    void OnDisable() { if (model != null) model.Changed -= Refresh; }
    void Start() {
        for (int i = 0; i < 5; i++) { int c = i; if (slotButtons[i]) slotButtons[i].onClick.AddListener(() => Pick(c)); }
        if (dropButton) dropButton.onClick.AddListener(DropSelected);
        SetOpen(false); Refresh();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) { SetOpen(!panelRoot.activeSelf); return; }
        if (!panelRoot.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { SetOpen(false); return; }
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  Move(-1);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Move(+1);
        for (int n = 0; n < 5; n++) if (Input.GetKeyDown(KeyCode.Alpha1 + n)) Pick(n);
        if (Input.GetKeyDown(KeyCode.Q)) DropSelected();
    }

    bool Locked(int i) {
        var s = model.slots[i];
        return !s.Empty && nightmare && model.Def(s.type).locksInNightmare;
    }

    public void SetOpen(bool open) {
        panelRoot.SetActive(open);
        if (closedHint) closedHint.SetActive(!open);
        if (open && Locked(sel)) Move(+1);
        Refresh();
    }
    public void SetNightmare(bool nm) { nightmare = nm; if (Locked(sel)) Move(+1); Refresh(); }

    void Move(int dir) { for (int n = 0; n < 5; n++) { sel = (sel + dir + 5) % 5; if (!Locked(sel)) break; } Refresh(); }
    void Pick(int i) { if (Locked(i)) { /* play reject flash/sfx */ return; } sel = i; Refresh(); }
    void DropSelected() { if (!Locked(sel)) model.DropOne(sel); }

    void Refresh()
    {
        for (int i = 0; i < 5; i++)
        {
            var s = model.slots[i]; bool locked = Locked(i);
            if (slotIcons[i]) {
                slotIcons[i].enabled = !s.Empty;
                if (!s.Empty) { slotIcons[i].sprite = iconSprites[model.Def(s.type).iconIndex]; ApplyCorruption(slotIcons[i], locked); }
            }
            if (slotCounts[i]) { bool show = !s.Empty && s.count > 1; slotCounts[i].enabled = show; if (show) { slotCounts[i].text = s.count.ToString(); slotCounts[i].color = (s.count >= model.maxStack) ? new Color(0.91f,0.63f,0.18f) : Color.white; } }
            if (selectHighlights[i]) selectHighlights[i].SetActive(i == sel);
            if (lockOverlays[i]) lockOverlays[i].SetActive(locked);
        }
        var cur = model.slots[sel]; bool curLock = Locked(sel);
        if (selName) selName.text = cur.Empty ? "— empty slot —" : (curLock ? model.Def(cur.type).name + "  ▮ LOCKED" : model.Def(cur.type).name);
        if (selDesc) selDesc.text = cur.Empty ? "Nothing here yet. Pick something up." : (curLock ? "Corrupted in the nightmare. You cannot bring yourself to touch it." : model.Def(cur.type).description);
        if (dropButton) dropButton.interactable = !cur.Empty && !curLock;
    }

    void ApplyCorruption(Image img, bool locked)
    {
        if (locked) { img.color = coldTint; if (glitchMaterial) img.material = glitchMaterial; }
        else { img.color = nightmare ? Color.Lerp(Color.white, coldTint, 0.5f) : Color.white; img.material = null; }
    }
}
```

### Notes
- **Corner highlights / box styles.** The prototype offers three frame looks (riveted / beveled /
  glow). In Unity make the slot a prefab: a background `Image`, four small corner `Image`s (the
  "little highlights"), and a `selectHighlight` child that pulses (animate its alpha/scale). Swap the
  prefab's sprites for the three styles.
- **Nightmare corruption** is a look, not data: keep one clean `item_icons.png` and drive the glitch
  via `glitchMaterial` + `coldTint`. Locking is decided by `ItemDef.locksInNightmare` (axe, key).
- **Stack cap** lives in `InventoryModel.maxStack` (12). `TryAdd` returns false + a message when a
  stack is maxed or all five slots are taken — surface it as a pickup toast, exactly like the demo.
- Pause player input while `panelRoot` is open if the modal should be modal.
