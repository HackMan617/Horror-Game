using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lays path_cobble.png as a flat cobblestone road over the grass. The route is authored as a set
/// of grid cells; each cell auto-picks its tile from which of its four neighbours are also in the
/// set — a "line autotile" (see Assets/Animation/README.md, section 2). Wide runs and junctions
/// (3+ connected neighbours) fill with the solid 4-way tile so a double-wide road reads as one
/// paved ribbon, single-width runs use the directional straights/elbows, and cells listed in
/// <see cref="houseCells"/> render the threshold tile (path meeting a building). Vertical straights
/// listed in <see cref="puddleCells"/> animate as puddles.
///
/// Sits just above the grass and below upright props; rebuilt from Build() so tweaking a route only
/// needs a re-Play. Grid convention: +x = East, +z = North (the cabin is to the north).
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PathTiler : MonoBehaviour
{
    public Material material;
    public float tileWorldSize = 1f;     // 16 px tile -> 1 world unit (the game's 16 px grid)
    public float pathY = 0.04f;          // just above grass (0.02), below leaves (0.06)

    [Header("Atlas layout (path_cobble.png = 8x2 of 16px tiles)")]
    public int atlasCols = 8;
    public int atlasRows = 2;
    public int tilePixels = 16;

    [Header("Route (grid cells; +x = East, +z = North)")]
    public List<Vector2Int> cells = new List<Vector2Int>();
    public List<Vector2Int> houseCells = new List<Vector2Int>();    // cells that meet a building -> threshold
    public List<Vector2Int> puddleCells = new List<Vector2Int>();   // vertical straights drawn as animated puddles
    public float puddleFrameMs = 170f;   // README: puddleV 4 frames @ 170 ms loop
    [Range(0f, 1f)] public float mossChance = 0.12f;                // fraction of solid fill tiles drawn mossy

    // Atlas cells (col,row); row 0 = top. Names/positions mirror the README routing table.
    static readonly Vector2Int StraightV = new Vector2Int(0, 0), StraightH = new Vector2Int(1, 0);
    static readonly Vector2Int ElbowNE = new Vector2Int(2, 0), ElbowNW = new Vector2Int(3, 0);
    static readonly Vector2Int ElbowSE = new Vector2Int(4, 0), ElbowSW = new Vector2Int(5, 0);
    static readonly Vector2Int Threshold = new Vector2Int(6, 0), Full = new Vector2Int(7, 0);
    static readonly Vector2Int FullMoss = new Vector2Int(7, 1);
    static readonly Vector2Int[] Puddle =
        { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) };

    struct AnimCell { public int vBase; public Vector2Int[] frames; public float phase; public int last; }

    Mesh _mesh;
    Vector2[] _uv;
    readonly List<AnimCell> _anim = new List<AnimCell>();

    void Start() { Build(); }

    public void Build()
    {
        _anim.Clear();
        if (cells == null || cells.Count == 0) { GetComponent<MeshFilter>().sharedMesh = null; return; }

        var set = new HashSet<Vector2Int>(cells);
        var houses = new HashSet<Vector2Int>(houseCells ?? new List<Vector2Int>());
        var puddle = new HashSet<Vector2Int>(puddleCells ?? new List<Vector2Int>());

        int cnt = cells.Count;
        var verts = new Vector3[cnt * 4];
        _uv = new Vector2[cnt * 4];
        var tris = new int[cnt * 6];
        float h = tileWorldSize * 0.5f;
        int vi = 0, ti = 0;

        foreach (var c in cells)
        {
            float cx = c.x * tileWorldSize;
            float cz = c.y * tileWorldSize;
            int b = vi;

            verts[b + 0] = new Vector3(cx - h, pathY, cz - h);
            verts[b + 1] = new Vector3(cx + h, pathY, cz - h);
            verts[b + 2] = new Vector3(cx + h, pathY, cz + h);
            verts[b + 3] = new Vector3(cx - h, pathY, cz + h);

            bool n = set.Contains(new Vector2Int(c.x, c.y + 1));
            bool e = set.Contains(new Vector2Int(c.x + 1, c.y));
            bool s = set.Contains(new Vector2Int(c.x, c.y - 1));
            bool w = set.Contains(new Vector2Int(c.x - 1, c.y));

            Vector2Int tile = houses.Contains(c) ? Threshold : PickTile(n, e, s, w);
            if (tile == Full && Hash01(c) < mossChance) tile = FullMoss;

            if (tile == StraightV && puddle.Contains(c))
            {
                SetCellUV(b, Puddle[0]);
                _anim.Add(new AnimCell { vBase = b, frames = Puddle, phase = Mathf.Abs(c.x * 7 + c.y * 13) % Puddle.Length, last = 0 });
            }
            else
            {
                SetCellUV(b, tile);
            }

            tris[ti + 0] = b; tris[ti + 1] = b + 2; tris[ti + 2] = b + 1;   // faces up (+Y)
            tris[ti + 3] = b; tris[ti + 4] = b + 3; tris[ti + 5] = b + 2;

            vi += 4; ti += 6;
        }

        _mesh = new Mesh { name = "PathTiles" };
        _mesh.vertices = verts;
        _mesh.uv = _uv;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    // Line autotile: solid fill for wide runs/junctions (3+ sides), directional straights/elbows for
    // single-width runs, and a straight stub for a lone end. Threshold/house ends are handled by the
    // caller via houseCells.
    static Vector2Int PickTile(bool n, bool e, bool s, bool w)
    {
        int c = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);
        if (c >= 3) return Full;                   // double-wide body & crossings -> one paved surface
        if (c == 2)
        {
            if (n && s) return StraightV;
            if (e && w) return StraightH;
            if (n && e) return ElbowNE;
            if (n && w) return ElbowNW;
            if (s && e) return ElbowSE;
            return ElbowSW;                         // s && w
        }
        if (c == 1) return (n || s) ? StraightV : StraightH;   // stub continues its run
        return Full;                                // isolated stone
    }

    void Update()
    {
        if (_anim.Count == 0 || _mesh == null || _uv == null) return;
        float fps = 1000f / Mathf.Max(1f, puddleFrameMs);
        float t = Time.time * fps;
        bool changed = false;
        for (int k = 0; k < _anim.Count; k++)
        {
            var a = _anim[k];
            int frame = Mod((int)(t + a.phase), a.frames.Length);
            if (frame != a.last)
            {
                SetCellUV(a.vBase, a.frames[frame]);
                a.last = frame;
                _anim[k] = a;
                changed = true;
            }
        }
        if (changed) _mesh.uv = _uv;
    }

    void SetCellUV(int b, Vector2Int cell)
    {
        Rect r = TileUV(cell);
        _uv[b + 0] = new Vector2(r.xMin, r.yMin);
        _uv[b + 1] = new Vector2(r.xMax, r.yMin);
        _uv[b + 2] = new Vector2(r.xMax, r.yMax);
        _uv[b + 3] = new Vector2(r.xMin, r.yMax);
    }

    // UV rect for atlas cell (col,row) with a half-texel inset (row 0 = top).
    Rect TileUV(Vector2Int cell)
    {
        float aw = atlasCols * tilePixels, ah = atlasRows * tilePixels, tp = tilePixels;
        float u0 = cell.x * tp / aw, u1 = (cell.x + 1) * tp / aw;
        float vTop = 1f - cell.y * tp / ah, vBot = 1f - (cell.y + 1) * tp / ah;
        float eu = 0.5f / aw, ev = 0.5f / ah;
        return Rect.MinMaxRect(u0 + eu, vBot + ev, u1 - eu, vTop - ev);
    }

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    // Deterministic 0..1 hash of a cell (stable moss pattern across rebuilds).
    static float Hash01(Vector2Int c)
    {
        unchecked
        {
            uint h = (uint)(c.x * 73856093) ^ (uint)(c.y * 19349663);
            h ^= h >> 13; h *= 0x85ebca6b; h ^= h >> 16;
            return (h & 0xffffff) / (float)0x1000000;
        }
    }
}
