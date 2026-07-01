using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static "far range" backdrop for the exterior (MOUNTAIN_BACKDROP.md). The mountain layers packed
/// in range_backdrop.png are wrapped into concentric rings that surround the play area, so the range
/// reads in every direction under the 360° camera. Far layers sit on larger, taller rings; because
/// they are genuinely far away they parallax by distance as the player walks (no texture-scroll
/// needed). Silhouettes are alpha-clipped, so overlapping rings depth-sort correctly by radius. A
/// single hero peak is placed at one azimuth, and a dusk sky cylinder sits behind everything.
///
/// Meshes are procedural and rebuilt in Build() (run at edit time by the exterior generator and again
/// in Start()), matching GroundTiler/PathTiler. The wanderer, mountain face and fog described in the
/// README are intentionally NOT built here yet — this is the "normal range" pass.
/// </summary>
public class MountainBackdrop : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public string name;
        public Sprite sprite;
        public float radius = 60f;      // ring radius from centre
        public float height = 20f;      // world height, baseline -> top
        public float baseY = 0f;        // bottom of the strip (the horizon line)
        public int copies = 5;          // how many times the seamless strip wraps the ring
        public int segmentsPerCopy = 8; // curve subdivisions per copy
    }

    public Material material;           // alpha-clipped unlit atlas material (shared by rings + hero)
    public Vector3 center = Vector3.zero;
    public Layer[] layers;

    [Header("Hero peak")]
    public Sprite heroSprite;
    public float heroAzimuthDeg = 90f;  // 90° = +Z (north, behind the cabin)
    public float heroRadius = 72f;
    public float heroWidth = 34f;
    public float heroHeight = 30f;
    public float heroBaseY = 0f;

    [Header("Dusk sky")]
    public bool buildSky = true;
    public float skyRadius = 150f;
    public float skyBottomY = -20f;
    public float skyTopY = 260f;        // tall enough that the dusk gradient fills the upward view
    public Color[] skyStops;            // top -> horizon; empty = README dusk palette

    void Start() { Build(); }

    public void Build()
    {
        var kill = new List<GameObject>();
        foreach (Transform t in transform) kill.Add(t.gameObject);
        foreach (var g in kill) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }

        if (buildSky) BuildSky();
        if (layers != null)
            foreach (var L in layers)
                if (L != null && L.sprite != null) BuildRing(L);
        if (heroSprite != null && material != null) BuildHero();
    }

    // One ridge strip wrapped `copies` times around a ring of `radius`, each copy subdivided for curve.
    void BuildRing(Layer L)
    {
        Rect uv = UvRect(L.sprite);
        int copies = Mathf.Max(1, L.copies);
        int spc = Mathf.Max(1, L.segmentsPerCopy);
        int cols = copies * (spc + 1);       // duplicate columns at copy seams (u jumps back to 0)

        var verts = new Vector3[cols * 2];
        var uvs = new Vector2[cols * 2];
        var tris = new int[copies * spc * 6];
        float yb = L.baseY, yt = L.baseY + L.height;
        int ti = 0;

        for (int c = 0; c < copies; c++)
            for (int s = 0; s <= spc; s++)
            {
                int col = c * (spc + 1) + s;
                float global = c * spc + s;
                float f = global / (copies * spc);                 // 0..1 around the ring
                float ang = f * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * L.radius, z = Mathf.Sin(ang) * L.radius;
                float u = uv.xMin + uv.width * (s / (float)spc);
                verts[col * 2 + 0] = new Vector3(x, yb, z);
                verts[col * 2 + 1] = new Vector3(x, yt, z);
                uvs[col * 2 + 0] = new Vector2(u, uv.yMin);
                uvs[col * 2 + 1] = new Vector2(u, uv.yMax);

                if (s < spc)
                {
                    int a = col * 2, b = (col + 1) * 2;
                    tris[ti++] = a; tris[ti++] = a + 1; tris[ti++] = b + 1;
                    tris[ti++] = a; tris[ti++] = b + 1; tris[ti++] = b;
                }
            }

        MakeChild("Ring_" + LayerName(L), verts, uvs, tris, material);
    }

    // The dominant central peak: a single flat quad at one azimuth, tangent to the ring.
    void BuildHero()
    {
        Rect uv = UvRect(heroSprite);
        float ang = heroAzimuthDeg * Mathf.Deg2Rad;
        Vector3 c = new Vector3(Mathf.Cos(ang) * heroRadius, 0f, Mathf.Sin(ang) * heroRadius);
        Vector3 tan = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));   // along the ring
        float hw = heroWidth * 0.5f, yb = heroBaseY, yt = heroBaseY + heroHeight;
        Vector3 l = c - tan * hw, r = c + tan * hw;

        var verts = new[]
        {
            new Vector3(l.x, yb, l.z), new Vector3(l.x, yt, l.z),
            new Vector3(r.x, yt, r.z), new Vector3(r.x, yb, r.z),
        };
        var uvs = new[]
        {
            new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax),
            new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin),
        };
        MakeChild("HeroPeak", verts, uvs, new[] { 0, 1, 2, 0, 2, 3 }, material);
    }

    // A tall cylinder with a vertical dusk gradient, drawn behind the mountains.
    void BuildSky()
    {
        var stops = (skyStops != null && skyStops.Length >= 2) ? skyStops : DefaultSky();
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SkyGradient" };
        for (int y = 0; y < 64; y++)
        {
            float p = (1f - y / 63f) * (stops.Length - 1);        // v=0 horizon .. v=1 top; stops are top->horizon
            int i = Mathf.Clamp(Mathf.FloorToInt(p), 0, stops.Length - 2);
            tex.SetPixel(0, y, Color.Lerp(stops[i], stops[i + 1], p - i));
        }
        tex.Apply();

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_Cull", 0f);        // viewed from inside
        mat.renderQueue = 1900;           // behind the geometry/alpha-clip mountains

        int segs = 48;
        var verts = new Vector3[(segs + 1) * 2];
        var uvs = new Vector2[(segs + 1) * 2];
        var tris = new int[segs * 6];
        int ti = 0;
        for (int i = 0; i <= segs; i++)
        {
            float ang = i / (float)segs * Mathf.PI * 2f;
            float x = Mathf.Cos(ang) * skyRadius, z = Mathf.Sin(ang) * skyRadius;
            verts[i * 2 + 0] = new Vector3(x, skyBottomY, z);
            verts[i * 2 + 1] = new Vector3(x, skyTopY, z);
            uvs[i * 2 + 0] = new Vector2(0f, 0f);
            uvs[i * 2 + 1] = new Vector2(0f, 1f);
            if (i < segs)
            {
                int a = i * 2, b = (i + 1) * 2;
                tris[ti++] = a; tris[ti++] = a + 1; tris[ti++] = b + 1;
                tris[ti++] = a; tris[ti++] = b + 1; tris[ti++] = b;
            }
        }
        MakeChild("SkyDome", verts, uvs, tris, mat);
    }

    GameObject MakeChild(string name, Vector3[] verts, Vector2[] uvs, int[] tris, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = center;
        var m = new Mesh { name = name };
        m.vertices = verts; m.uv = uvs; m.triangles = tris;
        m.RecalculateNormals(); m.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = m;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    // Normalised UV rect of an atlas sprite, inset half a texel so point sampling can't bleed into a
    // neighbouring packed sprite.
    static Rect UvRect(Sprite s)
    {
        var t = s.texture; var r = s.textureRect;
        float iw = 1f / t.width, ih = 1f / t.height;
        return Rect.MinMaxRect((r.xMin + 0.5f) * iw, (r.yMin + 0.5f) * ih, (r.xMax - 0.5f) * iw, (r.yMax - 0.5f) * ih);
    }

    static string LayerName(Layer L) => !string.IsNullOrEmpty(L.name) ? L.name : (L.sprite != null ? L.sprite.name : "layer");

    static Color[] DefaultSky() => new[]
    {
        Hex(0x141e46), Hex(0x232653), Hex(0x3d335e), Hex(0x5c3f61),
        Hex(0x8a4a5c), Hex(0xc65f38), Hex(0xef9f55),
    };
    static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
}
