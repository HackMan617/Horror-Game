// NIGHTMARE REVEAL generator — 64px hi-bit horror sheets of the WRUNG nightmare player.
// A significant style upgrade from the 32px rig sheets (which stay in place): semi-real
// proportions, rich ramps, wet specular gore, sickly rim light, deep blacks — still pixel art.
// The body is rotting FROM THE INSIDE: withered skin, decay mottle, the mask slipping.
//
//   eval(await readFile('nightmarerevealgen.js'));
//   await window.NightmareReveal.buildAll({ createCanvas, saveFile });
//
// Sheets (64×64 cell, 10 cols × 4 rows = 640×256), front-facing, WRUNG:
//   sprites/nm_reveal_male_base.png    sprites/nm_reveal_male_you.png
//   sprites/nm_reveal_female_base.png  sprites/nm_reveal_female_you.png  (+ *-3x previews)
// Rows: 0 FLASH (lunge at camera) · 1 SCREAM (jaw unhinges) · 2 SNAP (it notices you) · 3 CRAWL (loop)
//
// RECOLOR KEYS — these EXACT colors are the customization remap targets (viewer/engine swaps
// them to the player's drained palette). Never alpha-blended; everything else is fixed gore.
window.NightmareReveal = (function () {
  const W = 64, H = 64, COLS = 10, ROWS = 4;
  const CLIPS = ['flash', 'scream', 'snap', 'crawl'];

  const KEY = {
    skinHi: '#ffe0c4', skin: '#f0b890', skinSh: '#c07a4c', skinDp: '#8f5432',
    hairHi: '#c98a4a', hair: '#9c5a26', hairSh: '#5e3410',
    shirtHi: '#f06a5a', shirt: '#d83030', shirtSh: '#982018',
    pantsHi: '#6a8ae8', pants: '#3a5bd0', pantsSh: '#26398c',
    eye: '#00e2ff',
  };
  const G = {
    outline: '#060404',
    rot1: '#b0a878', rot2: '#8a8a5e', rot3: '#6a6a46', bruise: '#6a5a68', vein: '#4a5a46',
    bone: '#e8e2cc', boneHi: '#f6f2e2', boneSh: '#b8b096', boneDp: '#8a8270',
    gutHi: '#e88a7a', gut: '#b04038', gutSh: '#8a2c26', gutDp: '#5a1a16', gutBk: '#3a0f0c',
    blood: '#9c1c14', bloodDk: '#5a0e0a', bloodDry: '#3a1410', spec: '#e05a4a',
    wireHi: '#c8cdd4', wire: '#8a8f97', wireSh: '#4a4e55', barb: '#26282c',
    char: '#2c2420', charDk: '#191411', ember: '#ff9a3a', emberHi: '#ffd27a', emberDk: '#b04a1a',
    maw: '#150a0c', mawDp: '#070406', teeth: '#d8ccb0', teethSh: '#a89878',
    weep: '#44525c', weepWet: '#7a92a2',
    rim: '#7fae8e', shoe: '#2e2a26', shoeHi: '#4a443c',
  };
  const M = { NONE: 0, SKIN: 1, HAIR: 2, SHIRT: 3, PANTS: 4, GORE: 5, BONE: 6, WIRE: 7, SHOE: 8, FX: 9 };

  // ---------------- frame buffers ----------------
  let COL, MAT;
  function clear() { COL = new Array(W * H).fill(null); MAT = new Uint8Array(W * H); }
  function plot(x, y, c, m) {
    x = Math.round(x); y = Math.round(y);
    if (x < 0 || x >= W || y < 0 || y >= H || !c) return;
    COL[y * W + x] = c; MAT[y * W + x] = m == null ? M.GORE : m;
  }
  function at(x, y) { return (x < 0 || x >= W || y < 0 || y >= H) ? null : COL[y * W + x]; }
  function matAt(x, y) { return (x < 0 || x >= W || y < 0 || y >= H) ? M.NONE : MAT[y * W + x]; }
  function hash(a, b, c) { let h = (a * 374761393 + b * 668265263 + (c || 0) * 2246822519) >>> 0; h = ((h ^ (h >>> 15)) * 2246822519) >>> 0; return ((h ^ (h >>> 13)) >>> 0) / 4294967296; }
  const ez = t => t < 0 ? 0 : t > 1 ? 1 : t * t * (3 - 2 * t);
  const L = (a, b, t) => a + (b - a) * t;

  // ---------------- material ramps (shade s: -1 lit .. +1 shadow) ----------------
  function rampSkin(s) { return s < -0.45 ? KEY.skinHi : s < 0.05 ? KEY.skin : s < 0.5 ? KEY.skinSh : KEY.skinDp; }
  function rampHair(s) { return s < -0.5 ? KEY.hairHi : s < 0.25 ? KEY.hair : KEY.hairSh; }
  function rampShirt(s) { return s < -0.55 ? KEY.shirtHi : s < 0.2 ? KEY.shirt : KEY.shirtSh; }
  function rampPants(s) { return s < -0.55 ? KEY.pantsHi : s < 0.2 ? KEY.pants : KEY.pantsSh; }
  function rampBone(s) { return s < -0.5 ? G.boneHi : s < 0.1 ? G.bone : s < 0.6 ? G.boneSh : G.boneDp; }
  function rampGut(s) { return s < -0.6 ? G.gutHi : s < 0 ? G.gut : s < 0.55 ? G.gutSh : G.gutDp; }
  const RAMPS = { skin: [rampSkin, M.SKIN], hair: [rampHair, M.HAIR], shirt: [rampShirt, M.SHIRT], pants: [rampPants, M.PANTS], bone: [rampBone, M.BONE], gut: [rampGut, M.GORE] };

  // light from upper-left
  const LX = -0.62, LY = -0.78;

  // ---------------- primitives ----------------
  // capsule limb from (x0,y0) to (x1,y1), radius r
  function limb(x0, y0, x1, y1, r, matKey, seed) {
    const [ramp, mid] = RAMPS[matKey];
    const dx = x1 - x0, dy = y1 - y0, len2 = dx * dx + dy * dy || 1;
    const minx = Math.floor(Math.min(x0, x1) - r - 1), maxx = Math.ceil(Math.max(x0, x1) + r + 1);
    const miny = Math.floor(Math.min(y0, y1) - r - 1), maxy = Math.ceil(Math.max(y0, y1) + r + 1);
    const nl = Math.sqrt(len2); const ux = dx / nl, uy = dy / nl; let nx = -uy, ny = ux;
    if (nx * LX + ny * LY > 0) { nx = -nx; ny = -ny; }          // nx,ny points toward the light
    for (let y = miny; y <= maxy; y++) for (let x = minx; x <= maxx; x++) {
      let t = ((x - x0) * dx + (y - y0) * dy) / len2; t = t < 0 ? 0 : t > 1 ? 1 : t;
      const px_ = x0 + t * dx, py_ = y0 + t * dy;
      const ddx = x - px_, ddy = y - py_; const d = Math.sqrt(ddx * ddx + ddy * ddy);
      if (d > r) continue;
      const side = (ddx * nx + ddy * ny) / r;                   // + toward light
      let s = -side * 0.9 + (d / r) * 0.25 - 0.1;               // lit side bright, edges curve away
      s += (hash(x, y, seed || 0) - 0.5) * 0.10;                // subtle grain
      plot(x, y, ramp(s), mid);
    }
  }
  // shaded ellipse blob
  function blob(cx, cy, rx, ry, matKey, seed) {
    const [ramp, mid] = RAMPS[matKey];
    for (let y = Math.floor(cy - ry); y <= Math.ceil(cy + ry); y++)
      for (let x = Math.floor(cx - rx); x <= Math.ceil(cx + rx); x++) {
        const nx = (x - cx) / rx, ny = (y - cy) / ry; const d = nx * nx + ny * ny;
        if (d > 1) continue;
        let s = (nx * -LX + ny * -LY) * -1;                     // normal · light
        s = -s * 0.95 + d * 0.3 - 0.12;
        s += (hash(x, y, seed || 0) - 0.5) * 0.10;
        plot(x, y, ramp(s), mid);
      }
  }
  function line(x0, y0, x1, y1, c, m) {
    const n = Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0), 1);
    for (let i = 0; i <= n; i++) plot(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n, c, m);
  }

  // ---------------- post passes ----------------
  let HEADPOS = null; // [x,y,r] of the last head drawn — rot lesions avoid the face
  function rotPass(amt, seed) { // coherent decay LESIONS — rotting from the inside, in readable patches
    if (amt <= 0) return;
    const n = 3 + Math.round(amt * 3);
    for (let i = 0; i < n; i++) {
      let ax = 18 + Math.floor(hash(i, seed, 1) * 30), ay = 26 + Math.floor(hash(i, seed, 2) * 28);
      if (HEADPOS) { const dx = ax - HEADPOS[0], dy = ay - HEADPOS[1]; if (dx * dx + dy * dy < HEADPOS[2] * HEADPOS[2]) continue; }
      let fx = -1, fy = -1;
      for (let r = 0; r < 6 && fx < 0; r++) for (let dy = -r; dy <= r && fx < 0; dy++) for (let dx = -r; dx <= r && fx < 0; dx++) {
        if (matAt(ax + dx, ay + dy) === M.SKIN) { fx = ax + dx; fy = ay + dy; }
      }
      if (fx < 0) continue;
      const rad = 1.6 + hash(i, seed, 3) * 2.2;
      for (let dy = -4; dy <= 4; dy++) for (let dx = -4; dx <= 4; dx++) {
        const x = fx + dx, y = fy + dy;
        if (matAt(x, y) !== M.SKIN) continue;
        const d = Math.sqrt(dx * dx + dy * dy) + (hash(x, y, seed) - 0.5) * 1.2;   // ragged edge
        if (d > rad) continue;
        const t = d / rad;
        plot(x, y, t < 0.35 ? G.rot3 : t < 0.7 ? G.rot2 : (hash(x, y, 4) > 0.3 ? G.rot1 : G.bruise), M.GORE);
      }
    }
    // one deliberate cheek lesion at full rot
    if (amt > 0.55 && HEADPOS) {
      const cx = HEADPOS[0] - HEADPOS[2] * 0.55, cy = HEADPOS[1] + HEADPOS[2] * 0.45;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if (matAt(cx + dx, cy + dy) === M.SKIN && hash(dx, dy, seed) > 0.25) plot(cx + dx, cy + dy, dx === 0 && dy === 0 ? G.rot3 : G.rot2, M.GORE);
    }
    // very sparse threadveins
    for (let y = 26; y < H; y++) for (let x = 0; x < W; x++) { if (MAT[y * W + x] === M.SKIN && hash(x, y, 99) > 0.996) plot(x, y, G.vein, M.GORE); }
  }
  function outlinePass() {
    const edges = [];
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      if (!COL[y * W + x]) continue;
      if (matAt(x - 1, y) === M.NONE || matAt(x + 1, y) === M.NONE || matAt(x, y - 1) === M.NONE || matAt(x, y + 1) === M.NONE) edges.push([x, y]);
    }
    for (const [x, y] of edges) { COL[y * W + x] = G.outline; MAT[y * W + x] = M.FX; }
    // sickly rim light — sparse, lower-right silhouette only
    for (const [x, y] of edges) {
      const openR = matAt(x + 1, y) === M.NONE, openB = matAt(x, y + 1) === M.NONE;
      if (openB && (openR || x > 34) && hash(x, y, 5) > 0.85) { COL[y * W + x] = G.rim; }
    }
  }
  function emberCracks(cx, cy, amt, phase, seed) { // ember-lit fissures (torso)
    if (amt <= 0) return;
    const n = 2 + Math.round(amt * 2);
    for (let i = 0; i < n; i++) {
      let x = cx + Math.round((hash(i, seed, 1) - 0.5) * 10), y = cy + Math.round((hash(i, seed, 2) - 0.5) * 8);
      const len = 3 + Math.round(hash(i, seed, 3) * 3);
      for (let k = 0; k < len; k++) {
        if (matAt(x, y) !== M.NONE) {
          plot(x, y, G.char, M.FX);
          const hot = 0.55 + 0.45 * Math.sin(phase * 2 + i * 1.9 + k);
          if (hot > 0.62) plot(x, y, k === (len >> 1) ? G.emberHi : G.ember, M.FX);
          else if (hot > 0.3) plot(x, y, G.emberDk, M.FX);
        }
        x += hash(i, k, seed) > 0.5 ? 1 : 0; y += 1;
      }
    }
  }

  // ---------------- shared pieces ----------------
  function claw(hx, hy, len, spread, seed, big, up) { // too-long clawed fingers, charred tips
    const n = big ? 5 : 4;
    const baseA = up ? -Math.PI * 0.5 : Math.PI * 0.5;
    for (let i = 0; i < n; i++) {
      const a = (-0.5 + i / (n - 1)) * (0.9 + spread * 0.8) + baseA; // fan down (or up when gripping)
      const fl = len * (0.75 + hash(i, seed, 4) * 0.5);
      const ex = hx + Math.cos(a) * fl * 0.6, ey = hy + Math.sin(a) * fl;
      limb(hx, hy, ex, ey, big ? 1.2 : 0.8, 'skin', seed + i);
      plot(ex, ey, G.char, M.GORE); plot(ex, ey + (up ? -1 : 1), G.charDk, M.GORE); // charred claw tip
    }
  }
  function wireBand(x0, y0, x1, y1, phase, blood, seed) {
    const n = Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0), 1);
    for (let i = 0; i <= n; i++) {
      const t = i / n;
      const x = x0 + (x1 - x0) * t + Math.round(Math.sin(t * 9 + phase) * 0.6);
      const y = y0 + (y1 - y0) * t;
      if (matAt(x, y) === M.NONE) continue;
      plot(x, y, (i % 3 === 1) ? G.wireHi : ((i % 3 === 2) ? G.wireSh : G.wire), M.WIRE);
      plot(x, y + 1, G.charDk, M.FX);                           // constant drop shadow — the band reads
      if (i % 5 === 2) { plot(x, y - 1, G.barb, M.WIRE); plot(x + 1, y, G.barb, M.WIRE); plot(x - 1, y - 1, G.barb, M.WIRE); if (blood && hash(i, seed, 6) > 0.4) plot(x, y + 2, G.blood, M.GORE); }
    }
  }
  function gutRope(pts, r, phase, seed, drips) { // segmented rope of wet loops — reads as intestine, not tube
    let li = 0;
    for (let p = 0; p < pts.length - 1; p++) {
      const [ax, ay] = pts[p], [bx, by] = pts[p + 1];
      const n = Math.max(Math.abs(bx - ax), Math.abs(by - ay), 1);
      for (let i = 0; i <= n; i += 2.2) {
        const t = i / n; li++;
        const x = ax + (bx - ax) * t + Math.sin((p * 3 + t * 5) + phase) * 0.9;
        const y = ay + (by - ay) * t;
        const rr = r * (0.75 + hash(li, seed, 2) * 0.5);
        blob(x, y, rr, rr * 0.85, 'gut', seed + li * 7);
        plot(x, y + rr, G.gutBk, M.GORE);                       // per-loop underside crease
        if (li % 2 === 0) plot(x - rr * 0.5, y - rr * 0.5, G.spec, M.GORE); // wet glint
      }
    }
    // weight drips
    for (let p = 0; p < pts.length; p++) {
      const [x, y] = pts[p];
      if (drips && hash(p, seed, 9) > 0.5) { const dl = 1 + Math.round(hash(p, seed, 10) * 2 + Math.sin(phase + p) * 0.8); for (let k = 1; k <= dl; k++) plot(x, y + r + k, k === dl ? G.bloodDk : G.blood, M.GORE); }
    }
  }

  // head + ruined face. tilt: rad (positive = cocked toward its left / our right)
  function head(cx, cy, r, tilt, o) {
    HEADPOS = [cx, cy, r];
    const ca = Math.cos(tilt), sa = Math.sin(tilt);
    const RX = (dx, dy) => cx + dx * ca - dy * sa, RY = (dx, dy) => cy + dx * sa + dy * ca;
    // skull
    blob(cx, cy, r, r * 1.06, 'skin', 11);
    // gaunt cheek hollows (withered)
    plot(RX(-r * 0.55, r * 0.25), RY(-r * 0.55, r * 0.25), KEY.skinDp, M.SKIN);
    plot(RX(r * 0.55, r * 0.3), RY(r * 0.55, r * 0.3), KEY.skinDp, M.SKIN);
    // hair — a real cap that survives close-ups
    if (o.hairLong) {
      for (let a = -2.0; a <= 2.0; a += 0.11) {
        if (Math.cos(a) < -0.55) continue;
        const hx = cx + Math.sin(a + tilt) * r * 0.94, hy = cy - Math.cos(a + tilt) * r * 0.94;
        limb(hx, hy, hx + sa * 1.5, hy + 1.8, 1.7, 'hair', Math.round(a * 10));
      }
      // side falls HANG BY GRAVITY (straight down from the head edge), matted \u2014 dead hair
      const aLx = RX(-r * 0.9, 0), aLy = RY(-r * 0.9, 0);
      const aRx = RX(r * 0.9, 0), aRy = RY(r * 0.9, 0);
      const swayL = sa * 1.6, swayR = sa * 1.6;
      limb(aLx, aLy, aLx + swayL - 0.6, aLy + r * 2.4, 1.9, 'hair', 21);
      limb(aRx, aRy, aRx + swayR + 0.6, aRy + r * 2.2, 1.8, 'hair', 22);
      line(aLx - 1, aLy + r * 1.1, aLx + swayL - 2, aLy + r * 2.6, KEY.hairSh, M.HAIR);
      line(aRx + 1, aRy + r * 1.2, aRx + swayR + 2, aRy + r * 2.4, KEY.hairSh, M.HAIR);
    } else {
      for (let a = -1.9; a <= 1.9; a += 0.11) {
        if (Math.cos(a) < 0.05) continue;                        // short cap — crown + slight sides
        const hx = cx + Math.sin(a + tilt) * r * 0.94, hy = cy - Math.cos(a + tilt) * r * 0.94;
        const fringe = Math.cos(a) > 0.55 && hash(Math.round(a * 20), 3, 7) > 0.5 ? 1 : 0;
        limb(hx, hy, hx, hy + 1.5 + fringe, 1.6, 'hair', Math.round(a * 10));
      }
    }
    // withered scalp patch (hair fallen out — decay)
    if (o.rot > 0.4) { const px_ = RX(r * 0.3, -r * 0.8), py_ = RY(r * 0.3, -r * 0.8); plot(px_, py_, G.rot2, M.GORE); plot(px_ + 1, py_, G.rot1, M.GORE); plot(px_, py_ + 1, G.rot3, M.GORE); }

    // ---- face ----
    const exL = RX(-r * 0.42, -r * 0.08), eyL = RY(-r * 0.42, -r * 0.08);
    const exR = RX(r * 0.42, 0.02 * r), eyR = RY(r * 0.42, 0.02 * r);
    // sunken weeping orbit voids — bold 3×2 sockets
    for (const [ex, ey, side] of [[exL, eyL, -1], [exR, eyR, 1]]) {
      for (let dx = -1; dx <= 1; dx++) { plot(ex + dx, ey, G.mawDp, M.GORE); plot(ex + dx, ey + 1, dx === 0 ? G.mawDp : G.maw, M.GORE); }
      plot(ex - side, ey - 1, G.bruise, M.GORE); plot(ex, ey - 1, G.bruise, M.GORE);   // bruised sunken lid
      plot(ex + side, ey - 2, KEY.skinDp, M.SKIN); plot(ex, ey - 2, KEY.skinDp, M.SKIN); // pinched brow
      if (o.pinEye) plot(ex, ey, KEY.eye, M.FX);                                       // pin-light: it sees you
      const wl = 1 + Math.round(o.weep * 2.5);
      for (let k = 1; k <= wl; k++) plot(ex, ey + 2 + k, k === wl ? G.weepWet : G.weep, M.GORE);
    }
    // nose hollow
    plot(RX(0, r * 0.26), RY(0, r * 0.26), KEY.skinDp, M.SKIN); plot(RX(-0.08 * r, r * 0.34), RY(-0.08 * r, r * 0.34), G.bruise, M.GORE);
    // mouth — grimace, or unhinged maw when jawOpen
    const mx = RX(0, r * 0.55), my = RY(0, r * 0.55);
    if (o.jawOpen > 0.5) {
      const jo = o.jawOpen;
      for (let j = 0; j < jo; j++) {
        const wgt = 1 - Math.abs(j / jo - 0.4) * 1.1;
        const hw = Math.max(1, Math.round(3.1 * wgt + 0.4));
        for (let dx = -hw; dx <= hw; dx++) plot(mx + dx, my + j, (Math.abs(dx) === hw) ? G.maw : G.mawDp, M.GORE);
      }
      for (let dx = -2; dx <= 2; dx++) plot(mx + dx, my, dx % 2 ? G.teethSh : G.teeth, M.GORE);   // upper teeth row
      // dislocated jaw flap hanging below the maw
      limb(mx - 2.2, my + jo + 0.5, mx + 2.2, my + jo + 0.5, 1.5, 'skin', 31);
      for (let dx = -1; dx <= 1; dx++) plot(mx + dx, my + jo - 0.5, G.teethSh, M.GORE);           // lower teeth
      // stretched cheek tears
      plot(mx - 4, my + 1, G.gutDp, M.GORE); plot(mx + 4, my + 2, G.gutDp, M.GORE);
      plot(mx - 1, my + Math.round(jo * 0.5), G.spec, M.GORE);                                   // wet throat glint
    } else {
      // pleading grimace — corners dragged down, 2px deep
      plot(mx - 2, my + 1, G.charDk, M.GORE); plot(mx - 1, my, G.mawDp, M.GORE); plot(mx, my, G.mawDp, M.GORE); plot(mx + 1, my, G.mawDp, M.GORE); plot(mx + 2, my + 1, G.charDk, M.GORE);
      plot(mx, my + 1, G.maw, M.GORE);
      if (o.tier === 'you') plot(mx - 1, my + 1, G.blood, M.GORE);                               // bloodied lip
    }
    // ---- the slipping mask seam (down our-right cheek) ----
    const peel = o.maskPeel || 0;
    let sx = RX(r * 0.15, -r * 0.85), sy = RY(r * 0.15, -r * 0.85);
    const tx = RX(r * 0.75, r * 0.55), ty = RY(r * 0.75, r * 0.55);
    const segs = 6;
    for (let i = 0; i <= segs; i++) {
      const t = i / segs;
      const x = L(sx, tx, t) + (hash(i, 3, 1) > 0.5 ? 1 : 0), y = L(sy, ty, t);
      if (matAt(x, y) === M.NONE) continue;
      plot(x, y, G.charDk, M.GORE);                                    // the seam
      if (peel > 0.15) {                                               // lifted edge + wet under-flesh
        plot(x - 1, y, KEY.skinHi, M.SKIN);                            // mask edge catching light (inner side)
        const gap = Math.round(peel * 2);
        for (let g = 1; g <= gap; g++) plot(x + g, y, g === 1 ? G.gutSh : G.gutDp, M.GORE);  // wet flesh toward the edge
        if (peel > 0.6 && i % 2 === 0) plot(x + 1, y + 1, G.spec, M.GORE);
      }
    }
    // hook in the cheek pulling skin (you tier)
    if (o.hooks) {
      const hx = RX(-r * 0.7, r * 0.35), hy = RY(-r * 0.7, r * 0.35);
      plot(hx, hy, G.wireHi, M.WIRE); plot(hx - 1, hy + 1, G.wire, M.WIRE); plot(hx - 1, hy - 1, G.wire, M.WIRE);
      plot(hx + 1, hy, KEY.skinDp, M.SKIN); plot(hx, hy + 1, G.blood, M.GORE);
    }
  }

  // ---------------- STANDING painter (flash / scream / snap) ----------------
  function standing(o) {
    const s = o.s || 1, lean = o.leanX || 0, dy = o.dy || 0, ay = o.anchorY == null ? 60 : o.anchorY;
    const X = (x) => (x - 32) * s + 32 + lean + (o.tx || 0);
    const Y = (y) => (y - ay) * s + ay + dy + (o.ty || 0);
    const R = (r) => r * s;
    const hunch = o.hunch || 0;

    // — spine knobs punched out the LEFT flank (behind torso) —
    if (o.tier === 'you') for (let i = 0; i < 4; i++) {
      const bx = X(24 - i * 0.4 - hunch), by = Y(27 + i * 2.6);
      blob(bx, by, R(1.3), R(1.1), 'bone', 41 + i);
      plot(bx - R(1.5), by, G.gutDp, M.GORE); plot(bx, by + R(1.4), G.gutBk, M.GORE);   // torn rim
    }
    // — legs (buckled) + shoes —
    limb(X(29), Y(42), X(27.5), Y(50), R(2.2), 'pants', 1); limb(X(27.5), Y(50), X(27), Y(56.5), R(1.9), 'pants', 2);
    limb(X(35), Y(42), X(36.5), Y(50), R(2.2), 'pants', 3); limb(X(36.5), Y(50), X(37), Y(56.5), R(1.9), 'pants', 4);
    for (const lx of [27, 37]) { const bx = X(lx), by = Y(57.5);
      for (let yy = -1; yy <= 1; yy++) for (let xx = -2; xx <= 2; xx++) plot(bx + xx, by + yy, (yy < 0 ? G.shoeHi : G.shoe), M.SHOE); }
    // — pelvis —
    limb(X(29), Y(41), X(35), Y(41), R(2.6), 'pants', 5);
    // — dragging gut trail on the ground (you tier) —
    if (o.tier === 'you' && o.gutAmt > 0) {
      gutRope([[X(33.5), Y(40)], [X(37 + o.gutSway), Y(46)], [X(40.5 + o.gutSway * 1.4), Y(52)], [X(43.5 + o.gutSway * 1.8), Y(56)]], R(1.45), o.phase, 51, true);
      // smeared blood where it drags
      for (let i = 0; i < 5; i++) plot(X(41 + i * 2 + o.gutSway), Y(56.5 + (i % 2)), i % 2 ? G.bloodDry : G.bloodDk, M.GORE);
    }
    // — torso (hunched, twisted) —
    limb(X(30 - hunch), Y(29), X(31), Y(41), R(4.6), 'shirt', 6);
    limb(X(29 - hunch * 1.4), Y(26.5), X(35), Y(28), R(3.4), 'shirt', 7);          // shoulders line (right raised)
    // torn hem strands
    plot(X(28), Y(40), KEY.shirtSh, M.SHIRT); plot(X(27), Y(41.5), KEY.shirtSh, M.SHIRT);
    // — burst belly + clutching arm (you) / seep (base) —
    if (o.tier === 'you') {
      // wound
      blob(X(32.5), Y(37.5), R(3.6), R(2.6), 'gut', 61);
      plot(X(31), Y(36), G.spec, M.GORE); plot(X(34), Y(38.5), G.gutBk, M.GORE);
      for (let a = 0; a < 6; a++) { const an = a / 6 * 6.28; plot(X(32.5) + Math.cos(an) * R(3.4), Y(37.5) + Math.sin(an) * R(2.7), G.gutBk, M.GORE); } // torn rim
      // gut loops boiling at the wound
      gutRope([[X(30.5), Y(37)], [X(33.5 + o.gutSway * 0.5), Y(38.5)], [X(31.5), Y(39.5)]], R(1.2), o.phase, 63, false);
    } else {
      plot(X(32), Y(37), G.bloodDk, M.GORE); plot(X(33), Y(38), G.bloodDry, M.GORE); plot(X(31.5), Y(38.5), G.bloodDry, M.GORE);
    }
    // — RIGHT arm clutches the wound —
    limb(X(35.5), Y(27.5), X(37.5), Y(33), R(1.9), 'shirt', 8);                    // sleeve
    limb(X(37.5), Y(33), X(34), Y(37.5 - (o.tier === 'you' ? 0 : 0.5)), R(1.6), 'skin', 9);   // forearm
    blob(X(33.5), Y(37.6), R(1.7), R(1.4), 'skin', 10);                            // hand on belly
    if (o.tier === 'you') plot(X(33), Y(38.5), G.blood, M.GORE);                   // blood through fingers
    // — LEFT arm: hangs / reaches / rises (clip-driven) —
    const A = o.armL; // {sx,sy,ex,ey,hx,hy, clawLen, clawSpread, big}
    limb(X(A.sx), Y(A.sy), X(A.ex), Y(A.ey), R(1.9), 'shirt', 12);
    limb(X(A.ex), Y(A.ey), X(A.hx), Y(A.hy), R(1.5), 'skin', 13);
    if (o.tier === 'you' || o.clip === 'flash') claw(X(A.hx), Y(A.hy), R(A.clawLen || 4), A.clawSpread || 0.3, 17, A.big, A.up);
    else blob(X(A.hx), Y(A.hy + 1), R(1.4), R(1.6), 'skin', 18);
    // nail through the reaching forearm
    if (o.tier === 'you') { const nx = X((A.ex + A.hx) / 2), ny = Y((A.ey + A.hy) / 2); plot(nx, ny, G.wireSh, M.WIRE); plot(nx, ny - 1, G.wireHi, M.WIRE); plot(nx + 1, ny + 1, G.blood, M.GORE); }
    // — barbed wire binding the torso —
    wireBand(X(26 - hunch), Y(30), X(37), Y(35), o.phase, o.tier === 'you', 71);
    if (o.wireTight > 0.5) wireBand(X(27 - hunch), Y(34), X(36), Y(38.5), o.phase + 2, o.tier === 'you', 72);
    // — neck (wrung — stretched to the cocked side) —
    limb(X(31.5), Y(26), X(o.hx - 1), Y(o.hy + 4), R(1.7), 'skin', 19);
    if (o.tier === 'you') { plot(X(32.5), Y(24.5), G.rot2, M.GORE); plot(X(33.5), Y(23.5), G.bruise, M.GORE); } // bruised throat
    // — head (cocked hard) —
    head(X(o.hx), Y(o.hy), R(7.2), o.headTilt, o);
    // — ember cracks on the torso —
    emberCracks(X(30), Y(33), o.embers || 0, o.phase, 81);
  }

  // ---------------- CRAWL painter (prone, loops) ----------------
  function crawl(o) {
    const p = o.phase;
    const pull = Math.sin(p);                       // -1..1 crawl cycle
    const bodyBob = Math.abs(Math.sin(p)) * 1.2;
    const gy = 55;                                  // ground line
    // — blood smear trail (loops: pattern shifts one period over the row) —
    const off = (o.f / COLS) * 8;
    for (let x = 26; x < 62; x++) {
      const n = hash(Math.round(x + off), 3, 91);
      if (n > 0.55) plot(x, gy + 2 + (n > 0.8 ? 1 : 0), n > 0.75 ? G.bloodDk : G.bloodDry, M.GORE);
    }
    // handprints ahead
    for (let i = 0; i < 2; i++) { const hx = 10 + i * 6 + Math.round(hash(i, 7, 2) * 3); plot(hx, gy + 1, G.bloodDry, M.GORE); plot(hx + 1, gy + 1, G.bloodDk, M.GORE); }
    // — dead legs trailing —
    limb(44, gy - 4, 52, gy - 2 + Math.sin(p) * 0.8, 2.1, 'pants', 1);
    limb(52, gy - 2, 59, gy - 1 + Math.sin(p + 0.6) * 0.6, 1.8, 'pants', 2);
    for (let xx = 0; xx <= 3; xx++) plot(58 + xx, gy - 1, xx < 2 ? G.shoeHi : G.shoe, M.SHOE);
    limb(46, gy - 3, 55, gy - 0.5, 1.8, 'pants', 3);                       // other leg flopped
    // — guts dragging under the belly —
    if (o.tier === 'you') gutRope([[38, gy - 3], [43, gy - 1 + Math.sin(p + 1) * 0.7], [49, gy], [55, gy + 1]], 1.4, p, 95, true);
    // — torso low on elbows, chest raised —
    limb(40, gy - 5, 30, gy - 8 - bodyBob, 4.2, 'shirt', 4);
    limb(33, gy - 8 - bodyBob, 27, gy - 9 - bodyBob, 3.2, 'shirt', 5);
    // spine knobs through the shirt (you)
    if (o.tier === 'you') for (let i = 0; i < 3; i++) { const bx = 34 + i * 3, by = gy - 11 - bodyBob + i * 0.8; blob(bx, by, 1.1, 0.9, 'bone', 41 + i); plot(bx, by + 1.4, G.gutDp, M.GORE); }
    // — crawl arms: alternating reach/pull —
    const reach = ez((pull + 1) / 2);               // 0..1
    const aFx = 20 - reach * 7, aFy = gy - 1;       // planted forward hand
    limb(28, gy - 9 - bodyBob, 23, gy - 5, 1.8, 'shirt', 6);
    limb(23, gy - 5, aFx, aFy, 1.4, 'skin', 7);
    claw(aFx, aFy, 3.4, 0.7, 23, false);
    const bFx = 24 + (1 - reach) * 6, bFy = gy - 2; // other arm mid-pull
    limb(29, gy - 8 - bodyBob, 27, gy - 4, 1.7, 'shirt', 8);
    limb(27, gy - 4, bFx, bFy, 1.3, 'skin', 9);
    blob(bFx, bFy, 1.3, 1.1, 'skin', 10);
    // — head: raised at you, wrung tilt, bobbing with the pull —
    const hy = gy - 14 - bodyBob - reach * 1.5;
    head(24, hy, 6.4, 0.5 + Math.sin(p) * 0.08, o);
    // wire trailing off the shoulder
    wireBand(30, gy - 10 - bodyBob, 40, gy - 6, p, o.tier === 'you', 73);
    emberCracks(34, gy - 7, (o.embers || 0) * 0.8, p, 83);
  }

  // ---------------- clip schedules ----------------
  function frameParams(clip, f, tier, body) {
    const u = f / (COLS - 1), phase = 2 * Math.PI * f / COLS;
    const base = {
      clip, f, tier, phase,
      hairLong: body === 'female',
      rot: tier === 'you' ? 0.62 : 0.34,
      weep: tier === 'you' ? 1 : 0.6,
      maskPeel: tier === 'you' ? 0.5 : 0.18,
      hooks: tier === 'you',
      embers: tier === 'you' ? 0.8 : 0,
      wireTight: tier === 'you' ? 1 : 0.3,
      gutAmt: tier === 'you' ? 1 : 0,
      gutSway: Math.sin(phase) * 1.1,
      hx: 36.5, hy: 18.5, headTilt: 0.62,          // wrung: cocked hard to its left
      hunch: 1.2, jawOpen: 0, pinEye: false,
      s: 1, leanX: 0, dy: 0, tx: 0, ty: 0, anchorY: 60,
      armL: { sx: 24.5, sy: 28, ex: 21.5, ey: 34.5, hx: 20.5, hy: 41, clawLen: 4, clawSpread: 0.25 },
    };
    if (clip === 'flash') {
      const a = ez(Math.min(1, u / 0.28)) * (u < 0.28 ? 1 : 0);
      const l = ez(Math.max(0, (u - 0.28) / 0.72));
      base.anchorY = 34;                            // scale around the chest — the FACE stays in frame
      base.s = 1 + l * 1.35;
      base.dy = a * 2 + l * 12;
      base.leanX = -l * 2;
      base.hunch = 1.2 + a * 1.4 - l * 0.8;
      base.jawOpen = l * 5.5;
      base.maskPeel = base.maskPeel + l * 0.5;
      base.pinEye = l > 0.35;
      base.tx = (l > 0.55 ? (hash(f, 1, 2) - 0.5) * 3.4 * l : 0);
      base.ty = (l > 0.55 ? (hash(f, 3, 4) - 0.5) * 3.4 * l : 0);
      base.embers = (base.embers || 0.3) + l * 0.4;
      // the claw arm THROWS forward at the camera — huge, foreshortened, ending beside the lens
      base.armL = l < 0.2
        ? base.armL
        : { sx: 24.5, sy: 28, ex: L(21.5, 23.5, l), ey: L(34.5, 33, l), hx: L(20.5, 25, l), hy: L(41, 35.5, l), clawLen: 4 + l * 4.5, clawSpread: 0.25 + l * 0.95, big: l > 0.5 };
    } else if (clip === 'scream') {
      const e = ez(Math.min(1, u * 1.25));
      base.jawOpen = e * 7;
      base.headTilt = 0.62 - e * 0.34;               // rights itself as it howls
      base.hy = 18.5 - e * 1.5;
      base.maskPeel = base.maskPeel + e * 0.5;
      base.weep = 1;
      base.wireTight = 1;
      base.tx = (u > 0.75 ? (f % 2 ? 0.8 : -0.8) : 0);
      // hands rise up toward the skull
      base.armL = { sx: 24.5, sy: 28, ex: L(21.5, 23, e), ey: L(34.5, 27, e), hx: L(20.5, 28, e), hy: L(41, 23, e), clawLen: 3, clawSpread: 0.25, up: e > 0.4 };
      base.embers = (base.embers || 0) * (1 + e * 0.3);
    } else if (clip === 'snap') {
      // 0-3 dropped/lolling · 4-5 SNAP with overshoot · 6-9 dead stare + pin-lights
      if (f <= 3) { base.hx = 31.5; base.hy = 24.5 + f * 0.4; base.headTilt = 1.35; base.weep *= 0.7; }
      else if (f === 4) { base.hx = 36; base.hy = 17.5; base.headTilt = -0.3; }     // overshoot
      else if (f === 5) { base.hx = 36.5; base.hy = 18.2; base.headTilt = 0.5; }
      else { base.hx = 36.5; base.hy = 18.5; base.headTilt = 0.58; base.pinEye = true; base.tx = (f % 2 ? 0.6 : -0.6); }
    } else if (clip === 'crawl') { /* handled by crawl painter */ }
    return base;
  }

  // ---------------- cell / sheet ----------------
  function drawCell(ctx, ox, oy, clip, f, tier, body) {
    clear();
    const o = frameParams(clip, f, tier, body);
    if (clip === 'crawl') crawl(o); else standing(o);
    rotPass(o.rot, f * 3 + (clip === 'crawl' ? 1 : 0));
    outlinePass();
    // blit
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const c = COL[y * W + x]; if (!c) continue;
      ctx.fillStyle = c; ctx.fillRect(ox + x, oy + y, 1, 1);
    }
  }
  function buildSheet(mk, body, tier) {
    const cv = mk(W * COLS, H * ROWS);
    const g = cv.getContext('2d'); g.imageSmoothingEnabled = false;
    CLIPS.forEach((clip, r) => { for (let f = 0; f < COLS; f++) drawCell(g, f * W, r * H, clip, f, tier, body); });
    return cv;
  }

  async function buildAll({ createCanvas, saveFile, only, preview = 3 }) {
    const mk = (w, h) => createCanvas(w, h);
    const out = [];
    for (const body of ['male', 'female']) for (const tier of ['base', 'you']) {
      if (only && only !== body && only !== tier && only !== body + '_' + tier) continue;
      const cv = buildSheet(mk, body, tier);
      const path = 'sprites/nm_reveal_' + body + '_' + tier;
      await saveFile(path + '.png', cv);
      if (preview) { const big = mk(cv.width * preview, cv.height * preview); const bg = big.getContext('2d'); bg.imageSmoothingEnabled = false; bg.drawImage(cv, 0, 0, big.width, big.height); await saveFile(path + '-' + preview + 'x.png', big); }
      out.push(path + '.png');
    }
    return { W, H, COLS, ROWS, clips: CLIPS, files: out, keys: KEY };
  }

  return { buildAll, buildSheet, drawCell, frameParams, W, H, COLS, ROWS, CLIPS, KEY };
})();
