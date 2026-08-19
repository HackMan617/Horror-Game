// Interior window DAY→NIGHT CYCLE — one 48×40 window sprite baked at 16 points around the clock,
// so the view beyond the glass transitions gradually instead of snapping between two sprites.
// Frame 0 = dawn · 4 = mid-morning · 6 = noon · 10 = golden hour · 12 = sunset · 14 = dusk · 15 = night.
//
// Identical geometry to interior_colddusk_window.png / interior_daylight_window.png — curtains
// x=0..5 and x=42..47, wood frame rows 0..4 and 35..39, mid rail rows 19..20, centre mullion
// x=23..24, upper pane rows 5..18, lower pane rows 21..34 — so it drops onto the same renderer.
//
// Everything is interpolated from the KEYS table below: sky bands, haze, sun/moon arc + colour,
// ridge and treeline lighting, star field, glass sheen, sill warmth, curtain tint. Frame 15 lands
// on the existing night palette so the cycle closes on the sprite already in the game.
//
//   eval(await readFile('windowcyclegen.js'));
//   await window.WindowCycle.build({createCanvas, saveFile});
//   // -> interior_window_cycle.png (768×40, 16 frames) + -8x preview

window.WindowCycle = (function () {
  const hx = (h) => { h = h.replace('#', ''); return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)]; };
  const mix = (a, b, t) => [Math.round(a[0] + (b[0] - a[0]) * t), Math.round(a[1] + (b[1] - a[1]) * t), Math.round(a[2] + (b[2] - a[2]) * t)];
  const lerp = (a, b, t) => a + (b - a) * t;
  const rnd = (a, b) => { let h = (a * 374761393 + b * 668265263) >>> 0; h = ((h ^ (h >>> 13)) * 1274126177) >>> 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };

  const F = 16, W = 48, H = 40;
  const GT = 5, GB = 34, GL = 6, GR = 41;     // glass extents

  // ---- keyframes around the clock. t: 0 = dawn … 1 = deep night ----
  const KEYS = [
    { t: 0.00, skyTop: '#33436a', skyMid: '#8a7a94', skyLow: '#e6ab7c', haze: '#f0c69a', hazeA: 0.34,
      ridge: '#4e5464', ridgeLit: '#6e6a78', snow: '#d6c8cc', snowSh: '#a89aa6',
      tree: '#2e3440', treeLit: '#454a52', field: '#5a5a52', fieldLit: '#726e5e',
      curtain: '#6a6a80', curtainHi: '#8e8a9c', curtainSh: '#484a5e',
      sill: '#4a3a24', sillHi: '#7a5c36', warm: '#f0c48a', warmA: 0.40,
      cloud: '#f0c8b0', cloudSh: '#b08a8e', sheen: 0.10, stars: 0.20 },
    { t: 0.18, skyTop: '#5286c4', skyMid: '#9dc0e0', skyLow: '#cfe0ec', haze: '#e8dcc4', hazeA: 0.22,
      ridge: '#5a6a72', ridgeLit: '#7e8c90', snow: '#e4ecf0', snowSh: '#b8c8d2',
      tree: '#3c4a3c', treeLit: '#52644e', field: '#7c8558', fieldLit: '#98a06a',
      curtain: '#8a94a6', curtainHi: '#a8b2c2', curtainSh: '#5e6878',
      sill: '#5a4326', sillHi: '#8a6b3e', warm: '#f0d49a', warmA: 0.30,
      cloud: '#f4f8fc', cloudSh: '#cfdcea', sheen: 0.16, stars: 0.0 },
    { t: 0.40, skyTop: '#6f9cd0', skyMid: '#a6c8e4', skyLow: '#d6e6f0', haze: '#eee4cc', hazeA: 0.16,
      ridge: '#5e6e76', ridgeLit: '#86949a', snow: '#eef4f8', snowSh: '#c2d2dc',
      tree: '#3e4e3e', treeLit: '#587052', field: '#848f5c', fieldLit: '#a4ac72',
      curtain: '#96a0b2', curtainHi: '#b6c0ce', curtainSh: '#68727f',
      sill: '#60482a', sillHi: '#94743f', warm: '#f4dca4', warmA: 0.26,
      cloud: '#ffffff', cloudSh: '#d8e4f0', sheen: 0.20, stars: 0.0 },
    { t: 0.62, skyTop: '#6d92c0', skyMid: '#b0bcd0', skyLow: '#e4d8c8', haze: '#f0d8ae', hazeA: 0.24,
      ridge: '#64686e', ridgeLit: '#90908c', snow: '#eee8e4', snowSh: '#c4bcbc',
      tree: '#3e4a38', treeLit: '#5e6a48', field: '#8a8654', fieldLit: '#aaa468',
      curtain: '#9a9aa4', curtainHi: '#bcb8bc', curtainSh: '#6c6c74',
      sill: '#63482a', sillHi: '#9a7640', warm: '#f6d89a', warmA: 0.30,
      cloud: '#fff4e8', cloudSh: '#d8c8c4', sheen: 0.18, stars: 0.0 },
    { t: 0.78, skyTop: '#4a4a7e', skyMid: '#a86a72', skyLow: '#f0a860', haze: '#f6b070', hazeA: 0.40,
      ridge: '#4e4652', ridgeLit: '#7a5e5e', snow: '#e8c8b4', snowSh: '#b08a86',
      tree: '#2e2c34', treeLit: '#4c4038', field: '#6a5a3e', fieldLit: '#8a744a',
      curtain: '#8a7280', curtainHi: '#b09098', curtainSh: '#5a4a5c',
      sill: '#54381e', sillHi: '#8c5c2e', warm: '#ffbe72', warmA: 0.46,
      cloud: '#f8c090', cloudSh: '#b06a68', sheen: 0.14, stars: 0.06 },
    { t: 0.90, skyTop: '#26304e', skyMid: '#4e4a68', skyLow: '#8a6058', haze: '#a86a52', hazeA: 0.30,
      ridge: '#32384a', ridgeLit: '#4c4e5e', snow: '#b8b0bc', snowSh: '#7e7a8a',
      tree: '#20242e', treeLit: '#32363c', field: '#3e4038', fieldLit: '#525242',
      curtain: '#565e72', curtainHi: '#727a8c', curtainSh: '#3a4052',
      sill: '#3a2c1c', sillHi: '#5e4628', warm: '#c89a70', warmA: 0.30,
      cloud: '#8a7a86', cloudSh: '#5a4e5c', sheen: 0.08, stars: 0.55 },
    { t: 1.00, skyTop: '#1c2742', skyMid: '#2a3550', skyLow: '#38435f', haze: '#38435f', hazeA: 0.14,
      ridge: '#151b30', ridgeLit: '#1e2740', snow: '#5a6478', snowSh: '#3e4658',
      tree: '#10141f', treeLit: '#1a2028', field: '#181c22', fieldLit: '#22262a',
      curtain: '#3a4658', curtainHi: '#4c5463', curtainSh: '#2c3644',
      sill: '#2a1e12', sillHi: '#4a3822', warm: '#8aa0c8', warmA: 0.18,
      cloud: '#3e4a5e', cloudSh: '#28303f', sheen: 0.06, stars: 1.0 },
  ];
  const CKEYS = ['skyTop', 'skyMid', 'skyLow', 'haze', 'ridge', 'ridgeLit', 'snow', 'snowSh', 'tree', 'treeLit', 'field', 'fieldLit', 'curtain', 'curtainHi', 'curtainSh', 'sill', 'sillHi', 'warm', 'cloud', 'cloudSh'];
  const NKEYS = ['hazeA', 'warmA', 'sheen', 'stars'];
  const FRAME = { frame: hx('#2a1e12'), frameHi: hx('#4a3822'), frameSh: hx('#1a1209') };

  function grade(t) {
    let i = 0; while (i < KEYS.length - 2 && t > KEYS[i + 1].t) i++;
    const a = KEYS[i], b = KEYS[i + 1];
    const u = Math.max(0, Math.min(1, (t - a.t) / (b.t - a.t)));
    const g = {};
    for (const k of CKEYS) g[k] = mix(hx(a[k]), hx(b[k]), u);
    for (const k of NKEYS) g[k] = lerp(a[k], b[k], u);
    return g;
  }

  // sun / moon along an arc across the panes
  function sunPos(t) {                                  // visible t < 0.84
    const p = Math.max(0, Math.min(1, t / 0.84));
    return { x: Math.round(lerp(10, 39, p)), y: Math.round(24 - Math.sin(p * Math.PI) * 16), vis: t < 0.84, set: p };
  }
  function moonPos(t) {                                 // rises from t 0.76 → lands on the night sprite's spot
    const p = Math.max(0, Math.min(1, (t - 0.76) / 0.24));
    return { x: Math.round(lerp(38, 31, p)), y: Math.round(lerp(26, 12, p)), vis: t > 0.76, a: p };
  }

  function drawFrame(ctx, ox, t) {
    const g = grade(t);
    const P = (x, y, c, a) => { if (x < 0 || y < 0 || x >= W || y >= H) return; ctx.fillStyle = `rgba(${c[0]},${c[1]},${c[2]},${a == null ? 1 : a})`; ctx.fillRect(ox + x, y, 1, 1); };
    const R = (x, y, w, h, c, a) => { ctx.fillStyle = `rgba(${c[0]},${c[1]},${c[2]},${a == null ? 1 : a})`; ctx.fillRect(ox + x, y, w, h); };
    const inGlass = (x, y) => x >= GL && x <= GR && y >= GT && y <= GB;

    // ---- sky ----
    for (let y = GT; y <= GB; y++) {
      const u = (y - GT) / (GB - GT);
      const col = u < 0.55 ? mix(g.skyTop, g.skyMid, u / 0.55) : mix(g.skyMid, g.skyLow, (u - 0.55) / 0.45);
      R(GL, y, GR - GL + 1, 1, col);
    }
    for (let y = 22; y <= 26; y++) R(GL, y, GR - GL + 1, 1, g.haze, g.hazeA - (y - 22) * 0.04);

    // ---- stars (night end of the cycle) ----
    if (g.stars > 0.02) for (let i = 0; i < 26; i++) {
      const sx = GL + Math.floor(rnd(i * 3 + 1, 7) * (GR - GL)), sy = GT + Math.floor(rnd(i * 5 + 2, 11) * 16);
      const tw = 0.5 + 0.5 * Math.sin(t * 30 + i);
      P(sx, sy, [232, 238, 250], g.stars * (0.35 + 0.45 * tw));
    }

    // ---- sun with bloom ----
    const S = sunPos(t);
    if (S.vis) {
      const warmSun = t > 0.66;                       // reddens as it drops
      const core = warmSun ? [255, 244, 214] : [255, 255, 255];
      const body = warmSun ? mix([255, 246, 216], [255, 176, 96], (t - 0.66) / 0.18) : [255, 246, 216];
      const rim = warmSun ? mix([244, 212, 137], [230, 122, 70], (t - 0.66) / 0.18) : [244, 212, 137];
      const glow = warmSun ? mix([255, 233, 168], [246, 150, 96], (t - 0.66) / 0.18) : [255, 233, 168];
      const r = 4;
      for (let y = -r - 4; y <= r + 4; y++) for (let x = -r - 4; x <= r + 4; x++) {
        const d = Math.hypot(x, y); if (d <= r || d > r + 4.2) continue;
        if (!inGlass(S.x + x, S.y + y)) continue;
        P(S.x + x, S.y + y, glow, 0.30 * (1 - (d - r) / 4.2));
      }
      for (let y = -r; y <= r; y++) for (let x = -r; x <= r; x++) {
        const d = Math.hypot(x, y); if (d > r) continue;
        if (!inGlass(S.x + x, S.y + y)) continue;
        P(S.x + x, S.y + y, d > r - 1.2 ? rim : (x < -1 && y < -1 ? core : body));
      }
    }
    // ---- moon, rising into the night keyframe ----
    const M = moonPos(t);
    if (M.vis) {
      const mo = [244, 236, 207], msh = [216, 203, 160];
      for (let y = -6; y <= 6; y++) for (let x = -6; x <= 6; x++) {
        const d = Math.hypot(x, y); if (d <= 4 || d > 6.5) continue;
        if (!inGlass(M.x + x, M.y + y)) continue;
        P(M.x + x, M.y + y, [200, 214, 240], 0.16 * M.a * (1 - (d - 4) / 2.5));
      }
      for (let y = -4; y <= 4; y++) for (let x = -4; x <= 4; x++) {
        const d = Math.hypot(x, y); if (d > 4) continue;
        if (!inGlass(M.x + x, M.y + y)) continue;
        P(M.x + x, M.y + y, d > 3 ? mix(mo, msh, 0.55) : (x < -1 && y < -1 ? mo : mix(mo, msh, 0.2)), Math.max(0.25, M.a));
      }
    }

    // ---- clouds ----
    const puff = (cx, cy, w) => {
      for (let i = 0; i < w; i++) {
        const h = 1 + Math.round(Math.sin(i / w * Math.PI) * 1.6);
        for (let k = 0; k < h; k++) { if (!inGlass(cx + i, cy - k)) continue; P(cx + i, cy - k, k === h - 1 ? g.cloud : g.cloudSh, 0.85); }
      }
    };
    puff(9, 10, 8); puff(15, 16, 6); puff(28, 22, 7);

    // ---- ridge, snow caps, treeline, field ----
    const ridgeY = (x) => 27 - Math.round(2.2 * Math.sin((x - 6) / 7) + 1.6 * Math.sin((x - 6) / 3.1));
    for (let x = GL; x <= GR; x++) {
      const ry = ridgeY(x);
      for (let y = ry; y <= 30; y++) {
        if (!inGlass(x, y)) continue;
        const lit = rnd(x, y) > 0.42;
        P(x, y, y <= ry + 1 && x % 7 < 3 ? (lit ? g.snow : g.snowSh) : (lit ? g.ridgeLit : g.ridge));
      }
    }
    for (let x = GL; x <= GR; x++) {
      const th = 2 + Math.round(rnd(x * 3, 7) * 2);
      for (let y = 31 - th; y <= 31; y++) if (inGlass(x, y)) P(x, y, rnd(x, y * 2) > 0.5 ? g.treeLit : g.tree);
    }
    for (let y = 32; y <= GB; y++) for (let x = GL; x <= GR; x++) P(x, y, rnd(x * 5, y * 3) > 0.5 ? g.fieldLit : g.field);

    // ---- glass sheen ----
    for (let i = 0; i < 4; i++) { P(GL + 1 + i, GT + 1 + i, [255, 255, 255], g.sheen); P(GL + 2 + i, GT + 1 + i, [255, 255, 255], g.sheen * 0.6); }
    for (let i = 0; i < 3; i++) P(GL + 2 + i, 22 + i, [255, 255, 255], g.sheen * 0.6);

    // ---- muntins + frame ----
    R(23, GT, 2, GB - GT + 1, FRAME.frame); R(23, GT, 1, GB - GT + 1, FRAME.frameHi, 0.25);
    R(GL, 19, GR - GL + 1, 2, FRAME.frame); R(GL, 19, GR - GL + 1, 1, FRAME.frameHi, 0.35);
    R(GL, 0, GR - GL + 1, 5, FRAME.frame);
    R(GL, 0, GR - GL + 1, 1, FRAME.frameHi, 0.5); R(GL, 4, GR - GL + 1, 1, FRAME.frameSh);
    R(GL, 35, GR - GL + 1, 5, FRAME.frame);
    R(GL, 35, GR - GL + 1, 1, g.sillHi); R(GL, 36, GR - GL + 1, 1, g.sill);
    for (let x = GL; x <= GR; x++) if (rnd(x, 37) > 0.7) P(x, 37, FRAME.frameHi, 0.4);
    for (let i = 0; i < 9; i++) P(GL + 2 + i * 4, 35, g.warm, g.warmA);

    // ---- curtains ----
    for (const side of [0, 1]) {
      const x0 = side ? 42 : 0;
      for (let y = 0; y < H; y++) for (let x = 0; x < 6; x++) {
        const fold = (x + (side ? 1 : 0)) % 3;
        let c = fold === 0 ? g.curtainSh : (fold === 1 ? g.curtain : g.curtainHi);
        if (rnd(x0 + x, y * 3) > 0.93) c = g.curtainHi;
        P(x0 + x, y, c);
      }
      const inner = side ? 42 : 5;
      for (let y = 4; y < 36; y++) P(inner, y, g.warm, g.warmA * 0.7);
      R(x0, 0, 6, 2, g.curtainSh); R(x0, 0, 6, 1, g.curtainHi, 0.4);
      R(x0, H - 2, 6, 2, g.curtainSh, 0.7);
    }
  }

  async function build({ createCanvas, saveFile }) {
    const cv = createCanvas(W * F, H); const ctx = cv.getContext('2d'); ctx.imageSmoothingEnabled = false;
    for (let i = 0; i < F; i++) drawFrame(ctx, i * W, i / (F - 1));
    await saveFile('interior_window_cycle.png', cv);
    const up = createCanvas(W * F * 4, H * 4); const u = up.getContext('2d'); u.imageSmoothingEnabled = false; u.drawImage(cv, 0, 0, up.width, up.height);
    await saveFile('interior_window_cycle-4x.png', up);
    return ['interior_window_cycle.png'];
  }
  return { build, F, W, H, KEYS, drawFrame };
})();
if (typeof module !== 'undefined') module.exports = window.WindowCycle;
