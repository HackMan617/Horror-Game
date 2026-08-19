// BathroomShell.cs
// The bathroom's own surfaces — hex penny tile underfoot, beadboard wainscot, the cap rail, damp
// plaster above it. The sibling of CabinInterior's surface list, kept separate for one reason: the
// rest of the house is chinked log, and CabinInterior repaints everything it owns with the log
// texture. The washroom is a different grade and has to stay one.
//
// Same mechanism, though. Every skin is a thin box laid on the inside face of a wall, and the tile
// it carries plus how many times that tile repeats across it live in a MaterialPropertyBlock — so
// one material asset dresses a 5.6m plaster band and a 0.35m cap rail, each at its true 16px-per-
// metre scale, with no per-wall material assets and no stretched pixels.
//
// The cap rail is the reason offsets exist here: the wainscotCap cell carries the rail in its TOP
// band over beadboard below, so the rail strip shows the top ~35% of the cell (tiling.y 0.35,
// offset.y 0.65) rather than squashing the whole cell into a 35cm box.
//
// [ExecuteAlways] because property blocks are a runtime-only override that Unity never serialises —
// without this the room would sit in the Scene view with every tile stretched to a single stop.

using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class BathroomShell : MonoBehaviour
{
    [System.Serializable]
    public class Skin
    {
        public MeshRenderer renderer;
        public Texture2D tile;
        [Tooltip("Cells across / cells up. One cell is one metre of 16px art, so this is just the " +
                 "face's size in metres.")]
        public Vector2 tiling = Vector2.one;
        [Tooltip("Where in the cell the strip starts — the cap rail takes the top of its cell.")]
        public Vector2 offset = Vector2.zero;
    }

    public List<Skin> skins = new List<Skin>();

    MaterialPropertyBlock _mpb;

    static readonly int BaseMap    = Shader.PropertyToID("_BaseMap");
    static readonly int MainTex    = Shader.PropertyToID("_MainTex");
    static readonly int BaseMapST  = Shader.PropertyToID("_BaseMap_ST");
    static readonly int MainTexST  = Shader.PropertyToID("_MainTex_ST");

    void Awake() { Apply(); }

#if UNITY_EDITOR
    // Re-paint while the room is being dressed by eye, so tiling edits show up immediately.
    void OnValidate() { if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); }; }
#endif

    public void Apply()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        foreach (var s in skins)
        {
            if (s == null || s.renderer == null || s.tile == null) continue;
            var st = new Vector4(s.tiling.x, s.tiling.y, s.offset.x, s.offset.y);
            s.renderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(BaseMap, s.tile);
            _mpb.SetTexture(MainTex, s.tile);
            _mpb.SetVector(BaseMapST, st);
            _mpb.SetVector(MainTexST, st);
            s.renderer.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>Register one skin with the tile it carries and how many cells fit across it.</summary>
    public void Register(MeshRenderer r, Texture2D tile, Vector2 tiling, Vector2 offset = default)
    {
        if (r == null) return;
        skins.Add(new Skin { renderer = r, tile = tile, tiling = tiling, offset = offset });
    }
}
