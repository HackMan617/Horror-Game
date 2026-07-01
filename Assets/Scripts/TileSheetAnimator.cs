using UnityEngine;

/// <summary>
/// Continuously cycles a flat tile-quad through a sequence of atlas cells by rewriting its
/// four UVs — the same trick HousePortal uses for the opening door, but looping. Drives the
/// cabin's lit windows: a shadow figure crossing the panes (sequential) or a candle guttering
/// behind them (flicker). The quad must be a single 4-vertex mesh whose UVs map one 24x24 cell
/// of house_tiles.png (see HorrorGame3DSetup.MakeTileQuad).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class TileSheetAnimator : MonoBehaviour
{
    [Header("Frames as atlas cells (column,row)")]
    public Vector2Int[] frames;
    public float fps = 5f;
    public bool pingPong = false;     // walk the sequence forward then back
    public bool flicker = false;      // pick a random frame each step (candlelight)
    public float startPhase = 0f;     // seconds offset so neighbouring windows aren't in lockstep

    [Header("Atlas layout (must match house_tiles.png)")]
    public int atlasWidth = 192;
    public int atlasHeight = 144;
    public int tileSize = 24;

    Mesh _mesh;
    Vector2[] _uv;
    int _last = -1;
    System.Random _rng;

    void Start()
    {
        var mf = GetComponent<MeshFilter>();
        _mesh = mf != null ? mf.sharedMesh : null;   // MakeTileQuad hands each quad its own mesh
        if (_mesh == null || _mesh.vertexCount != 4 || frames == null || frames.Length == 0)
        {
            enabled = false;
            return;
        }
        _uv = new Vector2[4];
        _rng = new System.Random(transform.position.GetHashCode());   // unique per window; GetInstanceID() is obsolete in Unity 6
        SetFrame(frames[0]);
    }

    void Update()
    {
        if (fps <= 0f) return;
        int step = Mathf.FloorToInt((Time.time + startPhase) * fps);
        int idx;
        if (flicker)
        {
            if (step == _last) return;   // one random pick per fps tick
            _last = step;
            idx = frames.Length == 1 ? 0 : _rng.Next(frames.Length);
        }
        else
        {
            if (pingPong && frames.Length > 1)
            {
                int period = frames.Length * 2 - 2;
                int m = ((step % period) + period) % period;
                idx = m < frames.Length ? m : period - m;
            }
            else
            {
                idx = ((step % frames.Length) + frames.Length) % frames.Length;
            }
            if (idx == _last) return;
            _last = idx;
        }
        SetFrame(frames[idx]);
    }

    // Point the quad's four UVs at atlas cell (col,row) with a half-texel inset, matching the
    // cabin mesh so the tile never bleeds into its neighbours.
    void SetFrame(Vector2Int cell)
    {
        float aw = atlasWidth, ah = atlasHeight, tp = tileSize;
        float u0 = cell.x * tp / aw, u1 = (cell.x + 1) * tp / aw;
        float v1 = 1f - cell.y * tp / ah, v0 = 1f - (cell.y + 1) * tp / ah;
        float eu = 0.5f / aw, ev = 0.5f / ah;
        float xMin = u0 + eu, xMax = u1 - eu, yMin = v0 + ev, yMax = v1 - ev;
        _uv[0] = new Vector2(xMin, yMin);
        _uv[1] = new Vector2(xMax, yMin);
        _uv[2] = new Vector2(xMax, yMax);
        _uv[3] = new Vector2(xMin, yMax);
        _mesh.uv = _uv;
    }
}
