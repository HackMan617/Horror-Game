using System.Collections.Generic;
using UnityEngine;
using Game.Interior;

/// <summary>
/// The cabin's interior as one addressable room (see Assets/Animation/Interior Atlas/cabin_interior_handoff/
/// INTERIOR.md). It owns two jobs the two-storey interior needs and nothing else was doing:
///
/// <para><b>1 · The realm swap.</b> Every structural surface — plank floors, chinked log walls, the
/// plaster ceiling, the stair treads — is registered here with its Cold Dusk texture and its Nightmare
/// twin. <see cref="SetNightmare"/> repaints all of them through a MaterialPropertyBlock (so they can
/// share one material asset and still carry their own tiling), kills the warm hearth/lamp lights,
/// bleeds the moonlight red and turns the warp FX on. Assign <see cref="nightmare"/> and the room
/// flips itself the moment the player sleeps in the bed upstairs.</para>
///
/// <para><b>2 · The dread broadcast.</b> <see cref="DreadDirector"/> is the game's single dread value,
/// but the interior's flickering pieces (<see cref="InteriorObject"/> furniture, <see cref="InteriorProp"/>
/// decor and stairs, the <see cref="Bed"/>) each carry their own inspector-set DreadProgress that nothing
/// was ever driving — so indoors they sat at 0 forever and never went wrong. This pumps the director's
/// value into all of them each frame, which is what makes ' [ ' / ' ] ' visibly rot the cabin.</para>
/// </summary>
// Runs in edit mode too: the per-renderer tiling lives in a MaterialPropertyBlock (so one wall
// material can dress a 20m log wall and a 2m stair riser), and property blocks are a runtime-only
// override — without this the room would sit in the Scene view with every texture stretched to a
// single stop, and you could not dress it by eye.
[ExecuteAlways]
public class CabinInterior : MonoBehaviour
{
    public enum Surface { Wall, Floor, Ceiling }

    [System.Serializable]
    public class Tiled
    {
        public MeshRenderer renderer;
        public Surface surface = Surface.Wall;
        [Tooltip("Texture repeats across this renderer — kept per-renderer so one material can dress a " +
                 "20m wall and a 2m stair tread.")]
        public Vector2 tiling = Vector2.one;
    }

    [Header("Structural surfaces")]
    public List<Tiled> surfaces = new List<Tiled>();

    [Header("Cold Dusk tiles")]
    public Texture2D wallDay, floorDay, ceilingDay;
    [Header("Nightmare tiles")]
    public Texture2D wallNight, floorNight, ceilingNight;

    [Header("Lights")]
    [Tooltip("Warm accents (hearth, bedside lamp) — extinguished in the nightmare.")]
    public Light[] warmLights;
    [Tooltip("The cool shafts outside the windows — they bleed red in the nightmare.")]
    public Light[] moonLights;
    public Color moonDay = new Color(0.59f, 0.70f, 0.88f);
    public Color moonNight = new Color(0.60f, 0.15f, 0.13f);
    [Tooltip("Bleeding decals, ceiling drips — off in the waking house.")]
    public GameObject warpFX;

    [Header("Dread")]
    [Tooltip("Push DreadDirector's value into the room's furniture, decor and the bed every frame.")]
    public bool broadcastDread = true;
    [Tooltip("Past this the structure itself swaps to its rotted twin (the pieces flicker in gradually below it).")]
    [Range(0f, 1f)] public float nightmareThreshold = 0.75f;
    [Tooltip("Sleeping in the bed flips the room. Leave empty to drive the swap from dread alone.")]
    public NightmareController nightmare;

    MaterialPropertyBlock _mpb;
    InteriorObject[] _furniture;
    InteriorProp[] _props;
    Bed[] _beds;
    bool _nm, _applied;

    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int MainTex = Shader.PropertyToID("_MainTex");
    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        Rescan();
        Apply(false, true);
    }

    /// <summary>Re-collect the room's dread-driven pieces (call after spawning furniture at runtime).</summary>
    public void Rescan()
    {
        _furniture = FindObjectsByType<InteriorObject>(FindObjectsInactive.Include);
        _props     = FindObjectsByType<InteriorProp>(FindObjectsInactive.Include);
        _beds      = FindObjectsByType<Bed>(FindObjectsInactive.Include);
    }

#if UNITY_EDITOR
    // Re-paint while dressing the room in the editor, so tiling edits show up immediately.
    void OnValidate() { if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(_nm, true); }; }
#endif

    void Update()
    {
        if (!Application.isPlaying) return;   // edit mode only needs the surfaces painted, done in Awake

        float dread = DreadDirector.Value01;

        if (broadcastDread)
        {
            if (_furniture != null) foreach (var f in _furniture) if (f != null) f.DreadProgress = dread;
            if (_props != null)     foreach (var p in _props)     if (p != null) p.DreadProgress = dread;
            if (_beds != null)      foreach (var b in _beds)      if (b != null) b.DreadProgress = dread;
        }

        // Either the slow climb of dread or the hard cut of falling asleep tips the house over.
        bool want = dread >= nightmareThreshold || (nightmare != null && nightmare.IsNightmare);
        if (want != _nm) Apply(want, false);
    }

    /// <summary>Flip the whole interior between the waking Cold Dusk house and its rotted twin.</summary>
    public void SetNightmare(bool nm) => Apply(nm, false);
    public bool IsNightmare => _nm;

    void Apply(bool nm, bool force)
    {
        if (_applied && !force && nm == _nm) return;
        _nm = nm; _applied = true;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        foreach (var s in surfaces)
        {
            if (s == null || s.renderer == null) continue;
            Texture2D tex = TextureFor(s.surface, nm);
            if (tex == null) continue;
            var st = new Vector4(s.tiling.x, s.tiling.y, 0f, 0f);
            s.renderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(BaseMap, tex);
            _mpb.SetTexture(MainTex, tex);
            _mpb.SetVector(BaseMapST, st);
            _mpb.SetVector(MainTexST, st);
            s.renderer.SetPropertyBlock(_mpb);
        }

        if (warmLights != null) foreach (var l in warmLights) if (l != null) l.enabled = !nm;
        if (moonLights != null) foreach (var l in moonLights) if (l != null) l.color = nm ? moonNight : moonDay;
        if (warpFX != null) warpFX.SetActive(nm);
    }

    Texture2D TextureFor(Surface s, bool nm)
    {
        switch (s)
        {
            case Surface.Floor:   return (nm && floorNight   != null) ? floorNight   : floorDay;
            case Surface.Ceiling: return (nm && ceilingNight != null) ? ceilingNight : ceilingDay;
            default:              return (nm && wallNight    != null) ? wallNight    : wallDay;
        }
    }

    /// <summary>Register a structural renderer with its surface kind and repeat count (used by the builder).</summary>
    public void Register(MeshRenderer r, Surface surface, Vector2 tiling)
    {
        if (r == null) return;
        surfaces.Add(new Tiled { renderer = r, surface = surface, tiling = tiling });
    }
}
