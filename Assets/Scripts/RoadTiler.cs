using UnityEngine;

/// <summary>
/// Lays road_tiles.png (roadside_pack) as a flat vehicle road over the grass, the same way
/// <see cref="PathTiler"/> lays the cobble footpath. The road is a straight rectangular strip so it
/// can be authored from a handful of scalar fields instead of a hand-listed cell set: a run of
/// <see cref="length"/> tiles along +Z (north) that is <see cref="width"/> tiles across in X. Each
/// column of the cross-section is auto-assigned a tile — white-shoulder edge tiles on the far left
/// and right, a dashed centre line down the middle, plain lanes in between — so the strip melts into
/// the field at its shoulders and reads as one paved ribbon (see Assets/Animation/Car/roadside_pack/CAR.md,
/// section 2).
///
/// Atlas (road_tiles.png = 8x2 of 16 px tiles): row 0 = ASPHALT, row 1 = DIRT/GRAVEL.
///   col 0 plain · 1 dash/rut · 2 edgeL · 3 edgeR · 4 crack/gravel · 5 patch/mud · 6 snow · 7 transition/rocks.
///
/// Sits just above the grass (below the cobble path, so a junction reads as the footpath crossing on
/// top) and below upright props. Rebuilt from Build(); grid convention matches PathTiler: +x = East,
/// +z = North (the cabin is to the north).
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoadTiler : MonoBehaviour
{
    public Material material;
    public float tileWorldSize = 1f;     // 16 px tile -> 1 world unit (the game's 16 px grid)
    public float roadY = 0.03f;          // above grass (0.02), just below the cobble path (0.04)

    [Header("Atlas layout (road_tiles.png = 8x2 of 16px tiles)")]
    public int atlasCols = 8;
    public int atlasRows = 2;
    public int tilePixels = 16;

    [Header("Road strip (grid cells; +x = East, +z = North)")]
    public int originX = 5;              // min-x column of the strip
    public int originZ = 5;              // min-z (south) row of the strip
    public int width = 5;               // tiles across X (>=2: leftmost=edgeL, rightmost=edgeR)
    public int length = 8;              // tiles along the run (north, +Z)
    public int surfaceRow = 0;          // 0 = asphalt, 1 = dirt/gravel
    public bool dashedCentre = true;    // draw the dashed line / wheel rut down the middle column
    [Range(0f, 1f)] public float wearChance = 0.14f;   // fraction of plain lane tiles worn (crack/patch)
    [Range(0f, 1f)] public float snowChance = 0.08f;   // fraction of plain lane tiles snow-dusted

    // Atlas columns (shared across both surface rows).
    const int C_PLAIN = 0, C_DASH = 1, C_EDGE_L = 2, C_EDGE_R = 3, C_WEAR = 4, C_PATCH = 5, C_SNOW = 6;

    Mesh _mesh;

    void Start() { Build(); }

    public void Build()
    {
        if (width < 1 || length < 1) { GetComponent<MeshFilter>().sharedMesh = null; return; }

        int cnt = width * length;
        var verts = new Vector3[cnt * 4];
        var uv = new Vector2[cnt * 4];
        var tris = new int[cnt * 6];
        float h = tileWorldSize * 0.5f;
        int mid = width / 2;
        int vi = 0, ti = 0;

        for (int j = 0; j < length; j++)
        {
            for (int i = 0; i < width; i++)
            {
                var cell = new Vector2Int(originX + i, originZ + j);
                float cx = cell.x * tileWorldSize;
                float cz = cell.y * tileWorldSize;
                int b = vi;

                verts[b + 0] = new Vector3(cx - h, roadY, cz - h);
                verts[b + 1] = new Vector3(cx + h, roadY, cz - h);
                verts[b + 2] = new Vector3(cx + h, roadY, cz + h);
                verts[b + 3] = new Vector3(cx - h, roadY, cz + h);

                int col = PickColumn(i, mid, cell);
                SetCellUV(uv, b, new Vector2Int(col, surfaceRow));

                tris[ti + 0] = b; tris[ti + 1] = b + 2; tris[ti + 2] = b + 1;   // faces up (+Y)
                tris[ti + 3] = b; tris[ti + 4] = b + 3; tris[ti + 5] = b + 2;

                vi += 4; ti += 6;
            }
        }

        _mesh = new Mesh { name = "RoadTiles" };
        _mesh.vertices = verts;
        _mesh.uv = uv;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;

        // Give the strip a collider (like the cobble Pathway) so a downward ray from the player can
        // name this surface "Road" — that's what FootstepAudio keys the asphalt walking loop off.
        var mc = GetComponent<MeshCollider>();
        if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = null;
        mc.sharedMesh = _mesh;
    }

    int PickColumn(int i, int mid, Vector2Int cell)
    {
        if (width >= 2 && i == 0) return C_EDGE_L;                 // west shoulder
        if (width >= 2 && i == width - 1) return C_EDGE_R;         // east shoulder
        if (dashedCentre && i == mid) return C_DASH;              // centre line / rut
        float r = Hash01(cell);                                    // sparse wear on the plain lanes
        if (r < wearChance) return (Hash01(cell + Vector2Int.one) < 0.5f) ? C_WEAR : C_PATCH;
        if (r > 1f - snowChance) return C_SNOW;
        return C_PLAIN;
    }

    void SetCellUV(Vector2[] uv, int b, Vector2Int cell)
    {
        Rect r = TileUV(cell);
        uv[b + 0] = new Vector2(r.xMin, r.yMin);
        uv[b + 1] = new Vector2(r.xMax, r.yMin);
        uv[b + 2] = new Vector2(r.xMax, r.yMax);
        uv[b + 3] = new Vector2(r.xMin, r.yMax);
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

    // Deterministic 0..1 hash of a cell (stable wear pattern across rebuilds).
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
