// HoodieRecolor.cs — remap the 3 hoodie tones to a random palette entry.
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HoodieRecolor : MonoBehaviour
{
    [System.Serializable] public struct Hoodie { public string name; public Color baseC, shadow, highlight; }

    [Header("This partner's DEFAULT hoodie tones (the keys to replace)")]
    public Color fromBase = HexC("#b0303a"), fromShadow = HexC("#7e1c24"), fromHigh = HexC("#c84a52");

    [Header("Palette to pick from")]
    public Hoodie[] palette;                 // fill from the table above
    public bool randomizeOnStart = true;

    static readonly int PFB = Shader.PropertyToID("_FromBase");
    static readonly int PFS = Shader.PropertyToID("_FromShadow");
    static readonly int PFH = Shader.PropertyToID("_FromHigh");
    static readonly int PTB = Shader.PropertyToID("_ToBase");
    static readonly int PTS = Shader.PropertyToID("_ToShadow");
    static readonly int PTH = Shader.PropertyToID("_ToHigh");

    SpriteRenderer _sr; MaterialPropertyBlock _mpb;

    void Awake() { _sr = GetComponent<SpriteRenderer>(); _mpb = new MaterialPropertyBlock(); }
    void Start()  { if (randomizeOnStart && palette.Length > 0) Apply(Random.Range(0, palette.Length)); }

    public void Apply(int i)
    {
        var h = palette[i];
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor(PFB, fromBase);   _mpb.SetColor(PFS, fromShadow); _mpb.SetColor(PFH, fromHigh);
        _mpb.SetColor(PTB, h.baseC);    _mpb.SetColor(PTS, h.shadow);   _mpb.SetColor(PTH, h.highlight);
        _sr.SetPropertyBlock(_mpb);
    }

    static Color HexC(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
}
