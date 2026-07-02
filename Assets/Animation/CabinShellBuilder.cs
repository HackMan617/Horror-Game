// CabinShellBuilder.cs
// ---------------------------------------------------------------------------
// Adds the geometry that flat facade tiles can't provide: notched LOG CORNER
// POSTS at every vertical edge (so walls visibly interlock from any angle) and
// a closed GABLE ROOF (two slopes + ridge cap + gable-end triangles + eave
// fascia). It reads the size of an existing House object and builds a "shell"
// of quads UV-mapped into house_tiles.png (192x144, 24px tiles, 8x6 grid).
//
// Two ways to run it:
//   1) Add this component to (or next to) your House, set "House Target", then
//      use the component's context menu  >  "Build / Rebuild Shell".
//   2) Select the House in the Hierarchy and use the menu
//      Tools > Cabin > Add Corners + Roof to Selection.
//
// Re-running is safe: it deletes the previous generated child ("__CabinShell")
// first. Nothing else in your scene is touched.
//
// URP: assign a material that uses house_tiles.png. If you leave it empty the
// builder creates a "Universal Render Pipeline/Unlit" material for you and
// loads the texture from Resources/house_tiles (see README).
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class CabinShellBuilder : MonoBehaviour
{
    [Header("Source size")]
    [Tooltip("The House object whose bounds define the cabin footprint & wall height. " +
             "Leave empty to use this GameObject.")]
    public Transform houseTarget;
    [Tooltip("Override the auto-measured size (X = width, Y = wall height, Z = depth). " +
             "Leave at 0 to measure from the renderers on House Target.")]
    public Vector3 sizeOverride = Vector3.zero;

    [Header("Atlas material")]
    [Tooltip("Material that samples house_tiles.png. If empty, one is created at build time.")]
    public Material atlasMaterial;
    [Tooltip("Atlas is 192x144 with 24px tiles. Change only if you re-export at a different size.")]
    public int atlasWidth = 192, atlasHeight = 144, tilePx = 24;

    [Header("Look")]
    [Tooltip("World size that one 24px tile should cover. Smaller = denser logs/shingles.")]
    public float worldUnitsPerTile = 0.5f;
    [Tooltip("Square cross-section of each corner post, in world units.")]
    public float cornerPostSize = 0.32f;
    [Tooltip("How far the corner post pokes out past the wall faces.")]
    public float cornerOverhang = 0.06f;

    [Header("Gable roof")]
    [Tooltip("If true the ridge runs along Z (front & back are the triangular gable ends). " +
             "If false the ridge runs along X.")]
    public bool ridgeAlongZ = true;
    [Tooltip("Height of the ridge above the top of the walls.")]
    public float ridgeHeight = 1.4f;
    [Tooltip("How far the roof overhangs the eave (long) sides.")]
    public float eaveOverhang = 0.35f;
    [Tooltip("How far the roof overhangs the gable (end) walls.")]
    public float gableOverhang = 0.3f;
    [Tooltip("Lifts the whole roof up a hair to avoid z-fighting with an old painted-on roof.")]
    public float roofLift = 0.02f;
    public bool buildEaveFascia = true;

    [Header("Roof style")]
    [Tooltip("Gable = player house & neighbor A. Hipped = neighbor B (4 slopes). Saltbox = neighbor C (asymmetric).")]
    public RoofStyle roofStyle = RoofStyle.Gable;
    [Tooltip("Hipped only: how far the ridge is shortened at each end, as a fraction of the half-length (0 = pyramid-ish, 0.5 = long ridge).")]
    [Range(0f, 0.9f)] public float hipInset = 0.4f;
    [Tooltip("Saltbox only: how far the ridge is pushed toward the FRONT, as a fraction of the half-depth. Front slope gets short & steep, back slope long & shallow.")]
    [Range(0f, 0.8f)] public float saltboxRidgeOffset = 0.4f;

    public enum RoofStyle { Gable, Hipped, Saltbox }

    // ---- Atlas tile coordinates (col,row) in the 8x6 grid -------------------
    static readonly Vector2Int T_WALL      = new Vector2Int(0, 0); // wallA
    static readonly Vector2Int T_CORNERLOG = new Vector2Int(6, 1); // NEW notched corner
    static readonly Vector2Int T_ROOF      = new Vector2Int(0, 1); // roofF (shingles)
    static readonly Vector2Int T_RIDGE     = new Vector2Int(7, 1); // NEW ridge cap
    static readonly Vector2Int T_EAVE      = new Vector2Int(3, 1); // eave fascia

    const string SHELL_NAME = "__CabinShell";

    // ===== Build entry points ===============================================
    [ContextMenu("Build / Rebuild Shell")]
    public void Build()
    {
        var src = houseTarget != null ? houseTarget : transform;

        // size & centre of the footprint
        Vector3 size, centre;
        if (sizeOverride != Vector3.zero)
        {
            size = sizeOverride;
            centre = src.position;
        }
        else
        {
            Bounds b = MeasureBounds(src.gameObject);
            size = b.size;
            centre = b.center;
        }

        float hx = size.x * 0.5f, hz = size.z * 0.5f;
        float top = centre.y + size.y * 0.5f;          // top of the walls
        float bottom = centre.y - size.y * 0.5f;

        // fresh shell, parented next to the house with identity transform so we
        // can build directly in world space.
        var oldT = src.Find(SHELL_NAME);
        if (oldT) DestroyImmediate(oldT.gameObject);
        var shell = new GameObject(SHELL_NAME);
        shell.transform.SetParent(src, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localRotation = Quaternion.identity;
        shell.transform.localScale = Vector3.one;

        var mb = new MeshBuilder(this);

        BuildCornerPosts(mb, centre, hx, hz, top, bottom);
        switch (roofStyle)
        {
            case RoofStyle.Hipped:  BuildHippedRoof(mb, centre, hx, hz, top);  break;
            case RoofStyle.Saltbox: BuildSaltboxRoof(mb, centre, hx, hz, top); break;
            default:                BuildGableRoof(mb, centre, hx, hz, top);   break;
        }

        // convert world-space verts into shell-local (shell shares src's transform)
        var mesh = mb.ToMesh(src);
        var mf = shell.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = shell.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ResolveMaterial();

        Debug.Log($"[CabinShell] Built {mesh.vertexCount} verts. Size {size}, centre {centre}.", shell);
    }

    // ===== Corner posts =====================================================
    void BuildCornerPosts(MeshBuilder mb, Vector3 c, float hx, float hz, float top, float bottom)
    {
        float h = cornerPostSize * 0.5f + cornerOverhang;   // post reaches this far from the edge
        float height = top - bottom;
        int nv = Mathf.Max(1, Mathf.RoundToInt(height / worldUnitsPerTile));

        Vector2Int[] corners =
        {
            new Vector2Int( 1,  1), new Vector2Int( 1, -1),
            new Vector2Int(-1, -1), new Vector2Int(-1,  1),
        };
        foreach (var s in corners)
        {
            float ex = c.x + s.x * hx;      // the wall edge
            float ez = c.z + s.y * hz;
            float x0 = ex - h, x1 = ex + h;
            float z0 = ez - h, z1 = ez + h;
            Vector3 bA = new Vector3(x0, bottom, z0);
            Vector3 bB = new Vector3(x1, bottom, z0);
            Vector3 bC = new Vector3(x1, bottom, z1);
            Vector3 bD = new Vector3(x0, bottom, z1);
            Vector3 up = new Vector3(0, height, 0);
            // four vertical faces of the post (1 tile wide, nv tall)
            mb.TiledQuad(bA, bB, bA + up, T_CORNERLOG, 1, nv);
            mb.TiledQuad(bB, bC, bB + up, T_CORNERLOG, 1, nv);
            mb.TiledQuad(bC, bD, bC + up, T_CORNERLOG, 1, nv);
            mb.TiledQuad(bD, bA, bD + up, T_CORNERLOG, 1, nv);
            // small top cap
            mb.Quad(bA + up, bB + up, bD + up, T_CORNERLOG);
        }
    }

    // ===== Gable roof =======================================================
    void BuildGableRoof(MeshBuilder mb, Vector3 c, float hx, float hz, float top)
    {
        top += roofLift;
        float ridgeY = top + ridgeHeight;

        if (ridgeAlongZ)
        {
            // ridge runs front<->back (Z). Slopes face +X and -X.
            float rx = hx + eaveOverhang;          // eave reaches this far out in X
            float z0 = c.z - hz - gableOverhang;   // roof length in Z (with gable overhang)
            float z1 = c.z + hz + gableOverhang;
            float slopeLen = Mathf.Sqrt(rx * rx + ridgeHeight * ridgeHeight);
            int nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / worldUnitsPerTile));
            int ns = Mathf.Max(1, Mathf.RoundToInt(slopeLen / worldUnitsPerTile));

            Vector3 ridgeF = new Vector3(c.x, ridgeY, z0);
            Vector3 ridgeB = new Vector3(c.x, ridgeY, z1);
            Vector3 eaveLF = new Vector3(c.x - rx, top, z0);
            Vector3 eaveLB = new Vector3(c.x - rx, top, z1);
            Vector3 eaveRF = new Vector3(c.x + rx, top, z0);
            Vector3 eaveRB = new Vector3(c.x + rx, top, z1);

            // slopes (origin, +u along Z, +v up the slope toward ridge)
            mb.TiledQuad(eaveLF, eaveLB, ridgeF, T_ROOF, nz, ns);   // -X slope
            mb.TiledQuad(eaveRB, eaveRF, ridgeB, T_ROOF, nz, ns);   // +X slope (wound the other way)

            // ridge cap (thin strip straddling the ridge)
            float cap = Mathf.Max(worldUnitsPerTile, cornerPostSize) * 0.5f;
            Vector3 capL0 = new Vector3(c.x - cap, ridgeY, z0);
            Vector3 capL1 = new Vector3(c.x - cap, ridgeY, z1);
            Vector3 capR0 = new Vector3(c.x + cap, ridgeY, z0);
            mb.TiledQuad(capL0, capL1, new Vector3(c.x + cap, ridgeY, z0), T_RIDGE, nz, 1);

            // gable-end triangles (front & back), filled with wall logs
            Vector3 wLF = new Vector3(c.x - hx, top, c.z - hz);
            Vector3 wRF = new Vector3(c.x + hx, top, c.z - hz);
            Vector3 peakF = new Vector3(c.x, ridgeY, c.z - hz);
            mb.Tri(wLF, wRF, peakF, T_WALL);
            Vector3 wLB = new Vector3(c.x - hx, top, c.z + hz);
            Vector3 wRB = new Vector3(c.x + hx, top, c.z + hz);
            Vector3 peakB = new Vector3(c.x, ridgeY, c.z + hz);
            mb.Tri(wRB, wLB, peakB, T_WALL);

            if (buildEaveFascia)
            {
                float fascia = worldUnitsPerTile * 0.5f;
                mb.TiledQuad(eaveLF, eaveLB, eaveLF + Vector3.down * fascia, T_EAVE, nz, 1);
                mb.TiledQuad(eaveRB, eaveRF, eaveRB + Vector3.down * fascia, T_EAVE, nz, 1);
            }
        }
        else
        {
            // ridge runs left<->right (X). Slopes face +Z and -Z.
            float rz = hz + eaveOverhang;
            float x0 = c.x - hx - gableOverhang;
            float x1 = c.x + hx + gableOverhang;
            float slopeLen = Mathf.Sqrt(rz * rz + ridgeHeight * ridgeHeight);
            int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / worldUnitsPerTile));
            int ns = Mathf.Max(1, Mathf.RoundToInt(slopeLen / worldUnitsPerTile));

            Vector3 ridgeL = new Vector3(x0, ridgeY, c.z);
            Vector3 ridgeR = new Vector3(x1, ridgeY, c.z);
            Vector3 eaveFL = new Vector3(x0, top, c.z - rz);
            Vector3 eaveFR = new Vector3(x1, top, c.z - rz);
            Vector3 eaveBL = new Vector3(x0, top, c.z + rz);
            Vector3 eaveBR = new Vector3(x1, top, c.z + rz);

            mb.TiledQuad(eaveBL, eaveBR, ridgeL, T_ROOF, nx, ns);   // +Z slope
            mb.TiledQuad(eaveFR, eaveFL, ridgeR, T_ROOF, nx, ns);   // -Z slope

            float cap = Mathf.Max(worldUnitsPerTile, cornerPostSize) * 0.5f;
            Vector3 capF = new Vector3(x0, ridgeY, c.z - cap);
            mb.TiledQuad(new Vector3(x0, ridgeY, c.z - cap), new Vector3(x1, ridgeY, c.z - cap),
                         new Vector3(x0, ridgeY, c.z + cap), T_RIDGE, nx, 1);

            Vector3 wFL = new Vector3(c.x - hx, top, c.z - hz);
            Vector3 wFR = new Vector3(c.x - hx, top, c.z + hz);
            Vector3 peakL = new Vector3(c.x - hx, ridgeY, c.z);
            mb.Tri(wFR, wFL, peakL, T_WALL);
            Vector3 wBL = new Vector3(c.x + hx, top, c.z - hz);
            Vector3 wBR = new Vector3(c.x + hx, top, c.z + hz);
            Vector3 peakR = new Vector3(c.x + hx, ridgeY, c.z);
            mb.Tri(wBL, wBR, peakR, T_WALL);

            if (buildEaveFascia)
            {
                float fascia = worldUnitsPerTile * 0.5f;
                mb.TiledQuad(eaveBL, eaveBR, eaveBL + Vector3.down * fascia, T_EAVE, nx, 1);
                mb.TiledQuad(eaveFR, eaveFL, eaveFR + Vector3.down * fascia, T_EAVE, nx, 1);
            }
        }
    }

    // ===== Hipped roof (neighbor B) — four slopes to a shortened centered ridge =====
    void BuildHippedRoof(MeshBuilder mb, Vector3 c, float hx, float hz, float top)
    {
        top += roofLift; float ridgeY = top + ridgeHeight;
        float rx = hx + eaveOverhang, rz = hz + eaveOverhang;
        float ridgeHalf = Mathf.Max(0f, hx * (1f - hipInset));      // ridge along X, centered, shortened
        Vector3 ridgeL = new Vector3(c.x - ridgeHalf, ridgeY, c.z);
        Vector3 ridgeR = new Vector3(c.x + ridgeHalf, ridgeY, c.z);
        Vector3 eFL = new Vector3(c.x - rx, top, c.z - rz), eFR = new Vector3(c.x + rx, top, c.z - rz);
        Vector3 eBL = new Vector3(c.x - rx, top, c.z + rz), eBR = new Vector3(c.x + rx, top, c.z + rz);
        mb.Poly4(eFL, eFR, ridgeR, ridgeL, T_ROOF);                 // front slope (-Z), trapezoid
        mb.Poly4(eBR, eBL, ridgeL, ridgeR, T_ROOF);                 // back slope (+Z), trapezoid
        mb.Tri(eBL, eFL, ridgeL, T_ROOF);                           // -X hip end
        mb.Tri(eFR, eBR, ridgeR, T_ROOF);                           // +X hip end
        float cap = Mathf.Max(worldUnitsPerTile, cornerPostSize) * 0.5f;
        mb.Poly4(new Vector3(c.x - ridgeHalf, ridgeY, c.z - cap), new Vector3(c.x + ridgeHalf, ridgeY, c.z - cap),
                 new Vector3(c.x + ridgeHalf, ridgeY, c.z + cap), new Vector3(c.x - ridgeHalf, ridgeY, c.z + cap), T_RIDGE);
        if (buildEaveFascia)
        {
            float f = worldUnitsPerTile * 0.5f; int nx = Mathf.Max(1, Mathf.RoundToInt((2 * rx) / worldUnitsPerTile));
            mb.TiledQuad(eFL, eFR, eFL + Vector3.down * f, T_EAVE, nx, 1);
            mb.TiledQuad(eBR, eBL, eBR + Vector3.down * f, T_EAVE, nx, 1);
        }
    }

    // ===== Saltbox roof (neighbor C) — ridge pushed forward; short steep front, long shallow back =====
    void BuildSaltboxRoof(MeshBuilder mb, Vector3 c, float hx, float hz, float top)
    {
        top += roofLift; float ridgeY = top + ridgeHeight;
        float rx = hx + eaveOverhang;
        float zr = c.z - hz * saltboxRidgeOffset;                   // ridge toward the front (-Z)
        float zF = c.z - hz - gableOverhang, zB = c.z + hz + gableOverhang;
        Vector3 ridgeL = new Vector3(c.x - rx, ridgeY, zr), ridgeR = new Vector3(c.x + rx, ridgeY, zr);
        Vector3 eFL = new Vector3(c.x - rx, top, zF), eFR = new Vector3(c.x + rx, top, zF);
        Vector3 eBL = new Vector3(c.x - rx, top, zB), eBR = new Vector3(c.x + rx, top, zB);
        mb.Poly4(eFL, eFR, ridgeR, ridgeL, T_ROOF);                 // front slope (short/steep)
        mb.Poly4(eBR, eBL, ridgeL, ridgeR, T_ROOF);                 // back slope (long/shallow)
        mb.Tri(new Vector3(c.x - hx, top, c.z + hz), new Vector3(c.x - hx, top, c.z - hz), new Vector3(c.x - hx, ridgeY, zr), T_WALL);
        mb.Tri(new Vector3(c.x + hx, top, c.z - hz), new Vector3(c.x + hx, top, c.z + hz), new Vector3(c.x + hx, ridgeY, zr), T_WALL);
        float cap = Mathf.Max(worldUnitsPerTile, cornerPostSize) * 0.5f;
        mb.Poly4(new Vector3(c.x - rx, ridgeY, zr - cap), new Vector3(c.x + rx, ridgeY, zr - cap),
                 new Vector3(c.x + rx, ridgeY, zr + cap), new Vector3(c.x - rx, ridgeY, zr + cap), T_RIDGE);
        if (buildEaveFascia)
        {
            float f = worldUnitsPerTile * 0.5f; int nx = Mathf.Max(1, Mathf.RoundToInt((2 * rx) / worldUnitsPerTile));
            mb.TiledQuad(eFL, eFR, eFL + Vector3.down * f, T_EAVE, nx, 1);
            mb.TiledQuad(eBR, eBL, eBR + Vector3.down * f, T_EAVE, nx, 1);
        }
    }

    // ===== helpers ==========================================================
    static Bounds MeasureBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                // ignore a previously-built shell so re-measuring stays stable
                if (rends[i].gameObject.name == SHELL_NAME) continue;
                b.Encapsulate(rends[i].bounds);
            }
            return b;
        }
        var col = go.GetComponentInChildren<Collider>();
        if (col) return col.bounds;
        return new Bounds(go.transform.position, Vector3.one);
    }

    Material ResolveMaterial()
    {
        if (atlasMaterial) return atlasMaterial;
        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
        var m = new Material(sh) { name = "CabinAtlas (auto)" };
        var tex = Resources.Load<Texture2D>("house_tiles");
        if (tex)
        {
            tex.filterMode = FilterMode.Point;
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        }
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0); // render both faces, winding-proof
        atlasMaterial = m;
        return m;
    }

    Rect TileUV(Vector2Int t)
    {
        float u0 = (t.x * tilePx) / (float)atlasWidth;
        float u1 = ((t.x + 1) * tilePx) / (float)atlasWidth;
        float v1 = 1f - (t.y * tilePx) / (float)atlasHeight;       // Unity UV origin = bottom-left
        float v0 = 1f - ((t.y + 1) * tilePx) / (float)atlasHeight;
        // inset a half-texel to avoid bleeding neighbouring tiles
        float eu = 0.5f / atlasWidth, ev = 0.5f / atlasHeight;
        return Rect.MinMaxRect(u0 + eu, v0 + ev, u1 - eu, v1 - ev);
    }

    // small mesh accumulator -------------------------------------------------
    class MeshBuilder
    {
        readonly CabinShellBuilder o;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> tri = new List<int>();
        public MeshBuilder(CabinShellBuilder owner) { o = owner; }

        // a = origin, b = a + uDir end, d = a + vDir end. Fills nu x nv tiles.
        public void TiledQuad(Vector3 a, Vector3 b, Vector3 d, Vector2Int tile, int nu, int nv)
        {
            Rect r = o.TileUV(tile);
            Vector3 c = b + (d - a);
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                float u0 = i / (float)nu, u1 = (i + 1) / (float)nu;
                float w0 = j / (float)nv, w1 = (j + 1) / (float)nv;
                Vector3 p00 = Bilerp(a, b, d, c, u0, w0);
                Vector3 p10 = Bilerp(a, b, d, c, u1, w0);
                Vector3 p11 = Bilerp(a, b, d, c, u1, w1);
                Vector3 p01 = Bilerp(a, b, d, c, u0, w1);
                AddQuad(p00, p10, p11, p01, r);
            }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector2Int tile)
        {
            Rect r = o.TileUV(tile);
            AddQuad(a, b, b + (d - a), d, r);
        }

        // arbitrary 4-point quad (a=bottom-left, b=bottom-right, c=top-right, d=top-left), one tile.
        public void Poly4(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2Int tile)
        {
            Rect r = o.TileUV(tile);
            AddQuad(a, b, c, d, r);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 peak, Vector2Int tile)
        {
            Rect r = o.TileUV(tile);
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(peak);
            uv.Add(new Vector2(r.xMin, r.yMin));
            uv.Add(new Vector2(r.xMax, r.yMin));
            uv.Add(new Vector2((r.xMin + r.xMax) * 0.5f, r.yMax));
            tri.Add(s); tri.Add(s + 1); tri.Add(s + 2);
        }

        void AddQuad(Vector3 p00, Vector3 p10, Vector3 p11, Vector3 p01, Rect r)
        {
            int s = v.Count;
            v.Add(p00); v.Add(p10); v.Add(p11); v.Add(p01);
            uv.Add(new Vector2(r.xMin, r.yMin));
            uv.Add(new Vector2(r.xMax, r.yMin));
            uv.Add(new Vector2(r.xMax, r.yMax));
            uv.Add(new Vector2(r.xMin, r.yMax));
            tri.Add(s); tri.Add(s + 1); tri.Add(s + 2);
            tri.Add(s); tri.Add(s + 2); tri.Add(s + 3);
        }

        static Vector3 Bilerp(Vector3 a, Vector3 b, Vector3 d, Vector3 c, float u, float w)
            => a * (1 - u) * (1 - w) + b * u * (1 - w) + d * (1 - u) * w + c * u * w;

        public Mesh ToMesh(Transform space)
        {
            // bake world-space verts into the shell's local space
            for (int i = 0; i < v.Count; i++) v[i] = space.InverseTransformPoint(v[i]);
            var m = new Mesh { name = "CabinShellMesh" };
            m.indexFormat = v.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Cabin/Add Corners + Roof to Selection")]
    static void BuildFromMenu()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("[CabinShell] Select your House object first."); return; }
        var b = go.GetComponent<CabinShellBuilder>();
        if (b == null) b = go.AddComponent<CabinShellBuilder>();
        b.Build();
        EditorUtility.SetDirty(go);
    }
#endif
}
