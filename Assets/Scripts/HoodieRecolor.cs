using UnityEngine;

/// <summary>
/// Repaints the partner's hoodie without touching a texture. The garment is drawn in exactly three
/// tones — base / shadow / highlight — across all three of their sheets, so swapping those three keys
/// recolours the whole jumper and leaves skin, hair, trousers and the cream drawstrings where they are.
/// The swap itself happens in <c>Sprites/PartnerHoodieSwap</c>; this only decides which row of the
/// palette to feed it. Palette and default tones are from
/// Assets/Animation/UpdatedPartner/partner_walk_handoff/PARTNER_WALK.md.
///
/// <para>The colour is picked once, at character creation, and kept in
/// <see cref="CharacterStore"/> — like the dog's breed. Re-rolling it per scene would change their
/// jumper every time you walked through a door.</para>
///
/// <para><b>Colour space.</b> A linear project converts Color material properties behind your back but
/// leaves Vectors alone, so the keys go over as Vectors converted here, into the same space the shader's
/// sampler returns. Get this wrong and the keys quietly stop matching and nothing recolours.</para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class HoodieRecolor : MonoBehaviour
{
    public struct Hoodie
    {
        public string name;
        public Color baseC, shadow, highlight;
        public Hoodie(string n, string b, string s, string h)
        {
            name = n; baseC = Hex(b); shadow = Hex(s); highlight = Hex(h);
        }
    }

    /// <summary>The selectable jumpers. Index 0 (Crimson) and 1 (Forest) are the two sheets as drawn.</summary>
    public static readonly Hoodie[] Palette =
    {
        new Hoodie("Crimson",  "#b0303a", "#7e1c24", "#c84a52"),   // the girl's own tones
        new Hoodie("Forest",   "#566e3a", "#384824", "#6e8a4a"),   // the boy's own tones
        new Hoodie("Cobalt",   "#2f5aa0", "#1f3c72", "#4a7fc8"),
        new Hoodie("Plum",     "#6b3a7a", "#4a2656", "#8a54a0"),
        new Hoodie("Teal",     "#1f7a72", "#12524c", "#3aa096"),
        new Hoodie("Rust",     "#b5622a", "#7e401a", "#d1834a"),
        new Hoodie("Mustard",  "#c79a2e", "#8f6c1a", "#e0b84a"),
        new Hoodie("Slate",    "#445066", "#2c3444", "#64728c"),
        new Hoodie("Rose",     "#c25a7a", "#8f3a54", "#d97e9a"),
        new Hoodie("Charcoal", "#3a3a42", "#24242a", "#54545e"),
    };

    /// <summary>The tones each partner's sheets are actually drawn in — the keys to replace.</summary>
    public static Hoodie DrawnAs(int partner) => Palette[partner == 1 ? 0 : 1];   // 0 = boy, 1 = girl

    [Tooltip("Take the colour from CharacterStore on Start. Off if something else drives Apply().")]
    public bool applyOnStart = true;

    static readonly int PFB = Shader.PropertyToID("_FromBase");
    static readonly int PFS = Shader.PropertyToID("_FromShadow");
    static readonly int PFH = Shader.PropertyToID("_FromHigh");
    static readonly int PTB = Shader.PropertyToID("_ToBase");
    static readonly int PTS = Shader.PropertyToID("_ToShadow");
    static readonly int PTH = Shader.PropertyToID("_ToHigh");

    SpriteRenderer _sr;

    void Awake() => _sr = GetComponent<SpriteRenderer>();

    void Start()
    {
        if (applyOnStart) Apply(CharacterStore.LoadHoodie());
    }

    /// <summary>
    /// Wear palette row <paramref name="index"/>. Anything out of range (including the -1 that means
    /// "never chosen") leaves the sheets showing the colour they were drawn in.
    /// </summary>
    public void Apply(int index)
    {
        if (index < 0 || index >= Palette.Length || _sr == null) return;

        var from = DrawnAs(CharacterStore.LoadPartner());
        var to = Palette[index];
        var mat = _sr.material;                       // a per-partner instance; both may be on screen
        if (mat == null) return;

        mat.SetVector(PFB, V(from.baseC));  mat.SetVector(PFS, V(from.shadow));  mat.SetVector(PFH, V(from.highlight));
        mat.SetVector(PTB, V(to.baseC));    mat.SetVector(PTS, V(to.shadow));    mat.SetVector(PTH, V(to.highlight));
    }

    // Into whichever space the shader's sampler hands back, so the key comparison is exact.
    static Vector4 V(Color c)
    {
        Color v = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
        return new Vector4(v.r, v.g, v.b, 1f);
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
