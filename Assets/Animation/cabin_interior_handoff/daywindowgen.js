// DAYTIME interior window — the daylit twin of interior_colddusk_window.png.
// Identical 48×40 geometry so it's a straight sprite swap on the same renderer:
//   curtains x=0..5 and x=42..47 · wood frame rows 0..4 and 35..39 · mid rail rows 19..20 ·
//   centre mullion x=23..24 · upper pane rows 5..18 · lower pane rows 21..34.
// Beyond the glass: a high sun with a soft bloom, pale blue sky grading to warm horizon haze,
// a sunlit ridge with snow caps and a treeline, and warm light spilling onto the sill.
//
//   eval(await readFile('daywindowgen.js'));
//   await window.DayWindow.build({createCanvas, saveFile});

window.DayWindow = (function () {
  const hx = (h) => { h = h.replace('#', ''); return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)]; };
  const RAW = {
    frame: '#2a1e12', frameHi: '#4a3822', frameSh: '#1a1209',
    curtain: '#8a94a6', curtainHi: '#a8b2c2', curtainSh: '#5e6878',
    skyTop: '#6f9cd0', skyMid: '#9dc0e0', skyLow: '#c8dcea', haze: '#e8dcc4',
    sun: '#fff6d8', sunCore: '#ffffff', sunGlow: '#ffe9a8', sunRim: '#f4d489',
    ridge: '#5a6a72', ridgeLit: '#7e8c90', snow: '#e4ecf0', snowSh: '#b8c8d2',
    treeline: '#3c4a3c', treelineLit: '#52644e',
    field: '#7c8558', fieldLit: '#98a06a',
    cloud: '#f4f8fc', cloudSh: '#cfdcea',
    sill: '#5a4326', sillHi: '#8a6b3e', warm: '#f0d49a',
  };
  const C = {}; for (const k in RAW) C[k] = hx(RAW[k]);
  const rnd = (a, b) => { let h = (a * 374761393 + b * 668265263) >>> 0; h = ((h ^ (h >>> 13)) * 1274126177) >>> 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
  const mix = (a, b, t) => [Math.round(a[0] + (b[0] - a[0]) * t), Math.round(a[1] + (b[1] - a[1]) * t), Math.round(a[2] + (b[2] - a[2]) * t)];

  function build_({ createCanvas, saveFile }) {
    const W = 48, H = 40;
    const cv = createCanvas(W, H); const ctx = cv.getContext('2d'); ctx.imageSmoothingEnabled = false;
    const P = (x, y, c, a) => { if (x < 0 || y < 0 || x >= W || y >= H) return; ctx.fillStyle = `rgba(${c[0]},${c[1]},${c[2]},${a == null ? 1 : a})`; ctx.fillRect(x, y, 1, 1); };
    const R = (x, y, w, h, c, a) => { ctx.fillStyle = `rgba(${c[0]},${c[1]},${c[2]},${a == null ? 1 : a})`; ctx.fillRect(x, y, w, h); };

    // ---- glass: sky gradient across both panes (rows 5..34, x 6..41) ----
    const GT = 5, GB = 34, GL = 6, GR = 41;
    for (let y = GT; y <= GB; y++) {
      const t = (y - GT) / (GB - GT);
      const col = t < 0.55 ? mix(C.skyTop, C.skyMid, t / 0.55) : mix(C.skyMid, C.skyLow, (t - 0.55) / 0.45);
      R(GL, y, GR - GL + 1, 1, col);
    }
    // horizon haze band just above the ridge
    for (let y = 22; y <= 26; y++) R(GL, y, GR - GL + 1, 1, C.haze, 0.20 - (y - 22) * 0.03);

    // ---- sun: high in the upper-right pane, soft bloom, no hard rays ----
    const sx = 33, sy = 11, sr = 4;
    for (let y = -sr - 4; y <= sr + 4; y++) for (let x = -sr - 4; x <= sr + 4; x++) {
      const d = Math.hypot(x, y); if (d <= sr || d > sr + 4.2) continue;
      const px = sx + x, py = sy + y; if (px < GL || px > GR || py < GT || py > GB) continue;
      P(px, py, C.sunGlow, 0.30 * (1 - (d - sr) / 4.2));
    }
    for (let y = -sr; y <= sr; y++) for (let x = -sr; x <= sr; x++) {
      const d = Math.hypot(x, y); if (d > sr) continue;
      const px = sx + x, py = sy + y; if (px < GL || px > GR || py < GT || py > GB) continue;
      P(px, py, d > sr - 1.2 ? C.sunRim : (x < -1 && y < -1 ? C.sunCore : C.sun));
    }

    // ---- a couple of soft clouds ----
    const puff = (cx, cy, w) => {
      for (let i = 0; i < w; i++) {
        const h = 1 + Math.round(Math.sin(i / w * Math.PI) * 1.6);
        for (let k = 0; k < h; k++) { const px = cx + i, py = cy - k; if (px < GL || px > GR || py < GT || py > GB) continue; P(px, py, k === h - 1 ? C.cloud : C.cloudSh, 0.9); }
      }
    };
    puff(9, 10, 8); puff(15, 16, 6); puff(28, 22, 7);

    // ---- ridge line with snow caps, lower pane ----
    const ridgeY = (x) => 27 - Math.round(2.2 * Math.sin((x - 6) / 7) + 1.6 * Math.sin((x - 6) / 3.1));
    for (let x = GL; x <= GR; x++) {
      const ry = ridgeY(x);
      for (let y = ry; y <= 30; y++) {
        if (y < GT || y > GB) continue;
        const lit = rnd(x, y) > 0.42;                    // sun-facing flank catches light
        P(x, y, y <= ry + 1 && x % 7 < 3 ? (lit ? C.snow : C.snowSh) : (lit ? C.ridgeLit : C.ridge));
      }
    }
    // ---- treeline + sunlit field at the bottom of the lower pane ----
    for (let x = GL; x <= GR; x++) {
      const th = 2 + Math.round(rnd(x * 3, 7) * 2);
      for (let y = 31 - th; y <= 31; y++) if (y >= GT && y <= GB) P(x, y, rnd(x, y * 2) > 0.5 ? C.treelineLit : C.treeline);
    }
    for (let y = 32; y <= GB; y++) for (let x = GL; x <= GR; x++) P(x, y, rnd(x * 5, y * 3) > 0.5 ? C.fieldLit : C.field);

    // ---- glass sheen: a soft glint in the top-left corner of each pane ----
    for (let i = 0; i < 4; i++) { P(GL + 1 + i, GT + 1 + i, C.sunCore, 0.14); P(GL + 2 + i, GT + 1 + i, C.sunCore, 0.09); }
    for (let i = 0; i < 3; i++) P(GL + 2 + i, 22 + i, C.sunCore, 0.10);

    // ---- muntins: centre mullion + mid rail (drawn over the glass) ----
    R(23, GT, 2, GB - GT + 1, C.frame); P(23, GT, C.frameHi); R(23, GT, 1, GB - GT + 1, C.frameHi, 0.25);
    R(GL, 19, GR - GL + 1, 2, C.frame); R(GL, 19, GR - GL + 1, 1, C.frameHi, 0.35);

    // ---- outer frame: top and bottom bands ----
    R(GL, 0, GR - GL + 1, 5, C.frame);
    R(GL, 0, GR - GL + 1, 1, C.frameHi, 0.5); R(GL, 4, GR - GL + 1, 1, C.frameSh);
    R(GL, 35, GR - GL + 1, 5, C.frame);
    R(GL, 35, GR - GL + 1, 1, C.sillHi);                  // sill top catches the daylight
    R(GL, 36, GR - GL + 1, 1, C.sill);
    for (let x = GL; x <= GR; x++) if (rnd(x, 37) > 0.7) P(x, 37, C.frameHi, 0.4);   // grain on the sill face
    for (let i = 0; i < 9; i++) P(GL + 2 + i * 4, 35, C.warm, 0.45);                  // warm spill along the sill

    // ---- curtains, daylit: same cloth, pushed to the sides, sun glowing through ----
    for (const side of [0, 1]) {
      const x0 = side ? 42 : 0;
      R(x0, 0, 6, H, C.curtain);
      for (let y = 0; y < H; y++) {
        // vertical folds + a translucent glow where the sun rakes across the near edge
        for (let x = 0; x < 6; x++) {
          const fold = (x + (side ? 1 : 0)) % 3;
          let c = fold === 0 ? C.curtainSh : (fold === 1 ? C.curtain : C.curtainHi);
          if (rnd(x0 + x, y * 3) > 0.93) c = C.curtainHi;
          P(x0 + x, y, c);
        }
      }
      const inner = side ? 42 : 5;
      for (let y = 4; y < 36; y++) P(inner, y, C.warm, 0.22);       // sunlight bleeding round the edge
      R(x0, 0, 6, 2, C.curtainSh);                                   // rod pocket in shadow
      R(x0, 0, 6, 1, C.curtainHi, 0.4);
      R(x0, H - 2, 6, 2, C.curtainSh, 0.7);                          // weighted hem
    }
    return { cv, W, H };
  }

  async function build({ createCanvas, saveFile }) {
    const { cv, W, H } = build_({ createCanvas, saveFile });
    await saveFile('interior_daylight_window.png', cv);
    const up = createCanvas(W * 8, H * 8); const u = up.getContext('2d'); u.imageSmoothingEnabled = false; u.drawImage(cv, 0, 0, up.width, up.height);
    await saveFile('interior_daylight_window-8x.png', up);
    return ['interior_daylight_window.png'];
  }
  return { build, RAW };
})();
if (typeof module !== 'undefined') module.exports = window.DayWindow;
