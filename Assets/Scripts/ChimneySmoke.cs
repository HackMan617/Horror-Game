using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Emits soft smoke puffs that rise from the chimney mouth, sway, swell and fade out.
/// Puffs come from a small fixed pool so it's allocation-free after Start, and they rise in
/// WORLD space — that way the emitter can sit on a billboarding chimney without the column of
/// smoke swinging sideways as the chimney turns to face the camera. Each puff is itself a
/// billboard sprite, so the smoke always presents its soft round face.
/// </summary>
public class ChimneySmoke : MonoBehaviour
{
    public Sprite puffSprite;
    public Material material;            // unlit, alpha-blended (Sprites/Default) so per-puff alpha fades
    public int poolSize = 6;
    public float emitInterval = 0.6f;    // seconds between puffs
    public float riseSpeed = 0.7f;       // world units/second upward
    public float driftAmount = 0.3f;     // sideways sway amplitude near the top
    public float life = 3.2f;            // seconds a puff lives
    public float startScale = 0.25f;
    public float endScale = 1.15f;
    public Color tint = new Color(0.82f, 0.82f, 0.86f, 0.65f);
    [Tooltip("Keep at 0 so puffs distance-sort with the trees/player billboards (also order 0) and get " +
             "occluded by nearer ones. A higher value draws the smoke on top of everything (clips through).")]
    public int sortingOrder = 0;

    class Puff
    {
        public Transform tr;
        public SpriteRenderer sr;
        public float age;
        public float phase;
        public float driftDir;
        public bool alive;
    }

    readonly List<Puff> _pool = new List<Puff>();
    float _timer;

    void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("SmokePuff");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = puffSprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(tint.r, tint.g, tint.b, 0f);
            go.AddComponent<Billboard>();
            go.SetActive(false);
            _pool.Add(new Puff { tr = go.transform, sr = sr, alive = false });
        }
        _timer = emitInterval;   // first puff almost immediately
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= emitInterval) { _timer -= emitInterval; Emit(); }

        Vector3 basePos = transform.position;
        for (int i = 0; i < _pool.Count; i++)
        {
            var p = _pool[i];
            if (!p.alive) continue;
            p.age += Time.deltaTime;
            float u = p.age / life;
            if (u >= 1f) { p.alive = false; p.tr.gameObject.SetActive(false); continue; }

            float sway = Mathf.Sin((u * 2.5f + p.phase) * Mathf.PI * 2f) * driftAmount * u;
            p.tr.position = basePos + new Vector3(sway + p.driftDir * u, u * riseSpeed * life, 0f);
            float s = Mathf.Lerp(startScale, endScale, u);
            p.tr.localScale = new Vector3(s, s, s);

            float fade = Mathf.Clamp01(u * 5f) * (1f - u);   // quick fade-in, long tapering fade-out
            p.sr.color = new Color(tint.r, tint.g, tint.b, tint.a * fade);
        }
    }

    void Emit()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i].alive) continue;
            var p = _pool[i];
            p.age = 0f;
            p.alive = true;
            p.phase = Random.value;
            p.driftDir = Random.Range(-0.15f, 0.15f);
            p.tr.gameObject.SetActive(true);
            p.tr.position = transform.position;
            p.tr.localScale = Vector3.one * startScale;
            p.sr.color = new Color(tint.r, tint.g, tint.b, 0f);
            return;
        }
    }
}
