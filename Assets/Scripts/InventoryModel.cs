using System;
using UnityEngine;

/// <summary>Everything the player can carry. Index order matches the columns of item_icons.png.</summary>
[Serializable] public enum ItemType { None, Log, Axe, Note, Key }

[Serializable]
public struct ItemDef
{
    public ItemType type;
    public string name;
    [TextArea] public string description;
    public int iconIndex;          // column in item_icons.png (0..3)
    public bool locksInNightmare;  // corrupted + unselectable in the nightmare realm
}

[Serializable]
public struct Slot
{
    public ItemType type;
    public int count;
    public bool Empty => type == ItemType.None || count <= 0;
}

/// <summary>
/// The player's inventory (Assets/Animation/INVENTORY.md): five slots, twelve to a stack, with the
/// handoff's spill rules — top up a matching non-full stack first, then take a free slot, else refuse.
///
/// The handoff had this as a MonoBehaviour, but carried state in this game has to survive the walk from
/// the woods into the cabin, which is a scene load. So it is static, exactly like the wood count and the
/// felled-tree registry it replaces: it persists across scenes and clears when the game restarts.
/// <see cref="InventoryUI"/> is the view and owns nothing.
///
/// This is the single source of truth for the things it holds. <see cref="LogPickup.Wood"/> now reads
/// its log count rather than keeping a parallel tally, so a log you pick up is the same log the
/// <see cref="Fireplace"/> burns and the same log you see in the slots.
/// </summary>
public static class InventoryModel
{
    public const int Slots = 5;
    public const int MaxStack = 12;

    public static readonly ItemDef[] Defs =
    {
        new ItemDef { type = ItemType.Log,  name = "LOGS", iconIndex = 0, locksInNightmare = false,
                      description = "Split pine, still cold from the truck bed. Fuel for the fire." },
        new ItemDef { type = ItemType.Axe,  name = "AXE",  iconIndex = 1, locksInNightmare = true,
                      description = "Worn hickory haft, bitten edge. Chops wood - and more." },
        new ItemDef { type = ItemType.Note, name = "NOTE", iconIndex = 2, locksInNightmare = false,
                      description = "A scrap of paper, the ink run soft at the folds." },
        new ItemDef { type = ItemType.Key,  name = "KEY",  iconIndex = 3, locksInNightmare = true,
                      description = "Rust-toothed and heavy. It opens something you have not found." },
    };

    static readonly Slot[] _slots = new Slot[Slots];

    /// <summary>Raised whenever the contents change, so the open UI can repaint.</summary>
    public static event Action Changed;

    public static Slot At(int i) => (i >= 0 && i < Slots) ? _slots[i] : default;

    public static ItemDef Def(ItemType t)
    {
        foreach (var d in Defs) if (d.type == t) return d;
        return default;
    }

    public static int UsedSlots
    {
        get { int n = 0; foreach (var s in _slots) if (!s.Empty) n++; return n; }
    }

    /// <summary>How many of an item the player is carrying, counted across every stack.</summary>
    public static int Count(ItemType t)
    {
        int n = 0;
        foreach (var s in _slots) if (s.type == t) n += s.count;
        return n;
    }

    /// <summary>
    /// Take one. False means it did not fit and <paramref name="message"/> says why — the caller should
    /// leave the pickup in the world rather than quietly eating it.
    /// </summary>
    public static bool TryAdd(ItemType t, out string message)
    {
        if (t == ItemType.None) { message = ""; return false; }

        for (int i = 0; i < Slots; i++)                     // top up an existing stack
            if (_slots[i].type == t && _slots[i].count < MaxStack)
            {
                _slots[i].count++;
                message = "picked up " + Def(t).name.ToLower();
                Changed?.Invoke();
                return true;
            }

        for (int i = 0; i < Slots; i++)                     // else a free slot
            if (_slots[i].Empty)
            {
                _slots[i] = new Slot { type = t, count = 1 };
                message = "picked up " + Def(t).name.ToLower();
                Changed?.Invoke();
                return true;
            }

        bool maxed = false;
        foreach (var s in _slots) if (s.type == t) maxed = true;
        message = maxed ? Def(t).name.ToLower() + " stack is full (" + MaxStack + ")"
                        : "inventory full - no free slot";
        return false;
    }

    /// <summary>Drop one from a specific slot (what the UI's Q / DROP does).</summary>
    public static void DropOne(int i)
    {
        if (i < 0 || i >= Slots || _slots[i].Empty) return;
        _slots[i].count--;
        if (_slots[i].count <= 0) _slots[i] = new Slot();
        Changed?.Invoke();
    }

    /// <summary>
    /// Spend one of an item wherever it sits — for the game systems that consume rather than the UI
    /// (the hearth burning a log, the stump taking the axe back). Emptier stacks go first so the
    /// slots tidy themselves up instead of leaving a trail of ones.
    /// </summary>
    public static bool RemoveOne(ItemType t)
    {
        int best = -1;
        for (int i = 0; i < Slots; i++)
            if (_slots[i].type == t && !_slots[i].Empty &&
                (best < 0 || _slots[i].count < _slots[best].count)) best = i;
        if (best < 0) return false;
        DropOne(best);
        return true;
    }

    /// <summary>Wipe the lot — a new game starts with empty hands.</summary>
    public static void Clear()
    {
        for (int i = 0; i < Slots; i++) _slots[i] = new Slot();
        Changed?.Invoke();
    }
}
