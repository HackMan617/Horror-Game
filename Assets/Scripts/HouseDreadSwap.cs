using UnityEngine;

/// <summary>
/// Swaps a solid 3D neighbor house's wall/roof texture home ↔ nightmare on the shared dread flag
/// (same source as the dog, mountain, and Robert). Sets the atlas via a MaterialPropertyBlock on each
/// renderer, so all the houses can share one material asset while showing their own tileset.
/// </summary>
public class HouseDreadSwap : MonoBehaviour
{
    public MeshRenderer[] renderers;
    public Texture2D home;
    public Texture2D night;

    [Range(0f, 1f)] public float DreadProgress = 0f;
    public float nightmareThreshold = 0.5f;

    MaterialPropertyBlock _mpb;
    bool _nightmare;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        Apply(DreadProgress >= nightmareThreshold, true);
    }

    void Update()
    {
        bool want = DreadProgress >= nightmareThreshold;
        if (want != _nightmare) Apply(want, false);
    }

    public void Apply(bool nm, bool force)
    {
        if (!force && nm == _nightmare) return;
        _nightmare = nm;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Texture2D t = (nm && night != null) ? night : home;
        if (t == null || renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetTexture("_BaseMap", t);
            _mpb.SetTexture("_MainTex", t);
            r.SetPropertyBlock(_mpb);
        }
    }
}
