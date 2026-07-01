using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a flat ground mesh from grass_tiles.png. Each cell is cropped to the tile's TOP
/// grass band (the sheet is side-view — grass over a dirt strip — so we drop the strip to
/// avoid stripes). Dirt is clustered into plentiful patches with Perlin noise, and a sparse
/// scatter of cells are animated accents (orange/white flowers, swaying grass) that cycle
/// their frames at runtime. Sits just above the base ground; rebuilt from Build() so tuning
/// the mix only needs a re-Play.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GroundTiler : MonoBehaviour
{
    public Material material;
    public float worldSize = 80f;        // square footprint (matches the yard)
    public float tileWorldSize = 2f;     // world size of one tile cell
    public int seed = 12345;

    [Header("Atlas layout (grass_tiles.png = 8x3 of 16px tiles)")]
    public int atlasCols = 8;
    public int atlasRows = 3;
    public int tilePixels = 16;
    public int grassBandTopPixels = 10;  // crop each tile to its top N px (drops the side-view dirt strip)

    [Header("Dirt patches (Perlin-clustered)")]
    public float dirtNoiseScale = 0.16f;
    [Range(0f, 1f)] public float dirtThreshold = 0.58f;   // lower = more dirt

    [Header("Animated accents on the grass")]
    [Range(0f, 1f)] public float accentChance = 0.05f;    // fraction of grass cells that get a flower/tuft
    public float animFps = 3.5f;

    // Atlas cells (col,row); row 0 = top.
    static readonly Vector2Int[] GrassVariants = {
        new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(3,0),
        new Vector2Int(0,2), new Vector2Int(1,2), new Vector2Int(2,2), new Vector2Int(3,2),
    };
    static readonly Vector2Int[] DirtVariants = { new Vector2Int(5,0), new Vector2Int(6,0) };
    static readonly Vector2Int[] OrangeFlower = { new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(3,1) };
    static readonly Vector2Int[] WhiteFlower  = { new Vector2Int(4,2), new Vector2Int(5,2), new Vector2Int(6,2), new Vector2Int(7,2) };
    static readonly Vector2Int[] GrassTuft    = { new Vector2Int(4,1), new Vector2Int(5,1), new Vector2Int(6,1), new Vector2Int(7,1) };

    struct AnimCell { public int vBase; public Vector2Int[] frames; public float phase; public int last; }

    Mesh _mesh;
    Vector2[] _uv;
    readonly List<AnimCell> _anim = new List<AnimCell>();

    void Start() { Build(); }

    public void Build()
    {
        _anim.Clear();
        int n = Mathf.Max(1, Mathf.RoundToInt(worldSize / Mathf.Max(0.01f, tileWorldSize)));
        float half = worldSize * 0.5f;
        float ts = worldSize / n;
        float h = ts * 0.5f;

        int cells = n * n;
        var verts = new Vector3[cells * 4];
        _uv = new Vector2[cells * 4];
        var tris = new int[cells * 6];

        int vi = 0, ti = 0;
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        {
            float cx = -half + (i + 0.5f) * ts;
            float cz = -half + (j + 0.5f) * ts;
            int b = vi;

            verts[b + 0] = new Vector3(cx - h, 0f, cz - h);
            verts[b + 1] = new Vector3(cx + h, 0f, cz - h);
            verts[b + 2] = new Vector3(cx + h, 0f, cz + h);
            verts[b + 3] = new Vector3(cx - h, 0f, cz + h);

            Vector2Int cell;
            float dirtN = Mathf.PerlinNoise((i + seed * 0.13f) * dirtNoiseScale, (j - seed * 0.07f) * dirtNoiseScale);
            if (dirtN > dirtThreshold)
            {
                cell = DirtVariants[Mod(Hash(i, j, 71), DirtVariants.Length)];
            }
            else if (Hash01(i, j, 137) < accentChance)
            {
                int kind = Mod(Hash(i, j, 251), 3);
                Vector2Int[] frames = kind == 0 ? OrangeFlower : (kind == 1 ? WhiteFlower : GrassTuft);
                cell = frames[0];
                _anim.Add(new AnimCell { vBase = b, frames = frames, phase = Hash01(i, j, 991) * frames.Length, last = 0 });
            }
            else
            {
                cell = GrassVariants[Mod(Hash(i, j, 17), GrassVariants.Length)];
            }
            SetCellUV(b, cell);

            tris[ti + 0] = b; tris[ti + 1] = b + 2; tris[ti + 2] = b + 1;   // both faces up (+Y)
            tris[ti + 3] = b; tris[ti + 4] = b + 3; tris[ti + 5] = b + 2;

            vi += 4; ti += 6;
        }

        _mesh = new Mesh { name = "GroundTiles" };
        _mesh.indexFormat = verts.Length > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.vertices = verts;
        _mesh.uv = _uv;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    void Update()
    {
        if (_anim.Count == 0 || _mesh == null || _uv == null) return;
        float t = Time.time * Mathf.Max(0f, animFps);
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

    void SetCellUV(int vBase, Vector2Int cell)
    {
        Rect r = TileBandUV(cell);
        _uv[vBase + 0] = new Vector2(r.xMin, r.yMin);
        _uv[vBase + 1] = new Vector2(r.xMax, r.yMin);
        _uv[vBase + 2] = new Vector2(r.xMax, r.yMax);
        _uv[vBase + 3] = new Vector2(r.xMin, r.yMax);
    }

    // Top-band crop of atlas cell (col,row): keeps the grass part, drops the side-view dirt strip.
    Rect TileBandUV(Vector2Int cell)
    {
        float aw = atlasCols * tilePixels, ah = atlasRows * tilePixels, tp = tilePixels;
        float u0 = cell.x * tp / aw, u1 = (cell.x + 1) * tp / aw;
        float vTop = 1f - cell.y * tp / ah;
        float band = Mathf.Clamp(grassBandTopPixels, 1, tilePixels) / ah;
        float vBot = vTop - band;
        float eu = 0.5f / aw, ev = 0.5f / ah;
        return Rect.MinMaxRect(u0 + eu, vBot + ev, u1 - eu, vTop - ev);
    }

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    int Hash(int i, int j, int salt)
    {
        unchecked
        {
            uint hh = (uint)(i * 73856093) ^ (uint)(j * 19349663) ^ (uint)(salt * 83492791) ^ (uint)(seed * 1274126177);
            hh ^= hh >> 13; hh *= 0x85ebca6b; hh ^= hh >> 16;
            return (int)(hh & 0x7fffffff);
        }
    }

    float Hash01(int i, int j, int salt) => (Hash(i, j, salt) % 100000) / 100000f;
}
