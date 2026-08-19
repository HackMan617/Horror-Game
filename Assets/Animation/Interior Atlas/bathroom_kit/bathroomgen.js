// BATHROOM generator — the cabin's old washroom: hex penny-tile floor, plank wainscot, a
// utilitarian walk-in shower (bare concrete pan, exposed black iron pipe, curtain on a ring),
// plus sink/vanity/toilet/shelves/towels and the door with its hook-and-eye latch.
//
// Same 16px oblique world as interiorgen.js / interiorstructgen.js. Cold Dusk grade only
// (blue moonlight, cool porcelain, warm wood accents) — the nightmare pass ships later, EXCEPT
// the WINDOW, which has a watcher variant baked now: two glowing red eyes outside the glass,
// meant to be shown on rare random frames while the player showers.
//
//   eval(await readFile('bathroomgen.js'));
//   await window.Bath.build({createCanvas, saveFile});
//   // -> bathroom_colddusk.png (+ -4x preview), bathroom_window_watch.png (+ -4x)

window.Bath = (function () {
  const hx = (h) => { h = h.replace('#', ''); return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)]; };
  const RAW = {
    wood: '#5e5343', woodHi: '#7d7360', woodSh: '#3a3226', woodDk: '#241f18', grain: '#4a4032',
    tile: '#8a8f96', tileHi: '#a6aab0', tileSh: '#62666d', grout: '#474b52', tileStain: '#6a6a5e',
    conc: '#6e727a', concHi: '#868a92', concSh: '#4e525a', concDk: '#32363c',
    iron: '#2e3238', ironHi: '#4c525a', ironDk: '#14181c', rust: '#6a4632', rustHi: '#8a6040',
    porc: '#b8bcc2', porcHi: '#d6dade', porcSh: '#8a8e96', porcDk: '#5c606a',
    brass: '#8a7440', brassHi: '#b49c5e', brassDk: '#5a4a26',
    fab: '#7c8794', fabHi: '#98a3b0', fabSh: '#586470', fabDk: '#3c4650',
    water: '#8fb0cc', waterHi: '#c6dcec', waterDk: '#5e7e9a',
    steam: '#aeb8c6', steamHi: '#ccd4de',
    towel: '#8a7f6c', towelHi: '#a89c86', towelSh: '#5e5648',
    glass: '#26313f', glassHi: '#3e4c5c', moon: '#a8c0e4', moonDk: '#7290b8',
    soap: '#9aa08e', bottleA: '#4a6a58', bottleB: '#6a5a48', bottleC: '#7a6a80',
    dark: '#0e1116', black: '#050609', shadow: '#1a1e24',
    eye: '#c8201c', eyeHi: '#ff5a44', eyeGlow: '#7a1210',
  };
  const C = {}; for (const k in RAW) C[k] = hx(RAW[k]);

  function api(ctx, ox, oy) {
    const P = (x, y, c, a) => { ctx.fillStyle = `rgba(${c[0]},${c[1]},${c[2]},${a == null ? 1 : a})`; ctx.fillRect(ox + x, oy + y, 1, 1); };
    const R = (x, y, w, h, c, a) => { ctx.fillStyle = `rgba(${c[0]},${c[1]},${c[2]},${a == null ? 1 : a})`; ctx.fillRect(ox + x, oy + y, w, h); };
    const line = (x0, y0, x1, y1, c) => { x0 |= 0; y0 |= 0; x1 |= 0; y1 |= 0; let dx = Math.abs(x1 - x0), dy = Math.abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, e = dx - dy; for (; ;) { P(x0, y0, c); if (x0 === x1 && y0 === y1) break; const e2 = 2 * e; if (e2 > -dy) { e -= dy; x0 += sx; } if (e2 < dx) { e += dx; y0 += sy; } } };
    const ell = (cx, cy, rx, ry, c, a) => { for (let y = -ry; y <= ry; y++) for (let x = -rx; x <= rx; x++) if ((x * x) / (rx * rx) + (y * y) / (ry * ry) <= 1) P(cx + x, cy + y, c, a); };
    return { P, R, line, ell };
  }
  const rnd = (a, b) => { let h = (a * 374761393 + b * 668265263) >>> 0; h = ((h ^ (h >>> 13)) * 1274126177) >>> 0; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };

  // ============================================================== TILEABLE SURFACES ==
  // hex penny tile: offset rows of small hexes, cool aged cream, dark grout
  function hexFloor(a, worn) {
    const { P, R } = a;
    R(0, 0, 16, 16, C.grout);
    // 16px cell holds a 4-col × 4-row half-offset hex grid (4px pitch), seamless
    for (let ry = 0; ry < 4; ry++) for (let cx = 0; cx < 5; cx++) {
      const off = (ry % 2) ? 2 : 0, x0 = cx * 4 + off - 2, y0 = ry * 4;
      const s = rnd(cx + 1, ry + 3);
      let base = C.tile, hi = C.tileHi, sh = C.tileSh;
      if (s > 0.86) { base = C.tileHi; } else if (s < 0.16) { base = C.tileSh; }
      if (worn && rnd(cx * 3, ry * 5) > 0.72) { base = C.tileStain; hi = C.tileSh; }
      if (worn && rnd(cx * 7 + 2, ry * 2) > 0.90) continue;      // missing tile → grout shows
      // hex: 3px wide body with clipped corners
      for (let y = 0; y < 3; y++) for (let x = 0; x < 3; x++) {
        if ((y === 0 || y === 2) && (x === 0 || x === 2)) continue;
        P(x0 + x, y0 + y, base);
      }
      P(x0 + 1, y0, hi); P(x0 + 1, y0 + 2, sh);
    }
    // faint moonlight sheen upper-left
    for (let y = 0; y < 16; y++) for (let x = 0; x < 16; x++) if (rnd(x * 5, y * 3) > 0.94) P(x, y, C.moon, 0.10);
  }
  // wainscot: vertical beadboard planks, tiles X (and Y for a taller run)
  function wainscotWall(a) {
    const { P, R } = a;
    R(0, 0, 16, 16, C.wood);
    for (let x = 0; x < 16; x += 4) { R(x, 0, 1, 16, C.woodDk); R(x + 1, 0, 1, 16, C.woodHi, 0.5); }  // bead seams
    for (let y = 0; y < 16; y++) for (let x = 0; x < 16; x++) { const s = rnd(x * 3 + 1, y * 7); if (s > 0.90) P(x, y, C.grain, 0.55); else if (s < 0.07) P(x, y, C.woodHi, 0.35); }
    for (let y = 0; y < 16; y++) if (rnd(2, y) > 0.93) P(0, y, C.woodSh);
  }
  // wainscot cap rail — the top course of the wainscot (tiles X only)
  function wainscotCap(a) {
    const { R } = a;
    R(0, 0, 16, 3, C.woodHi); R(0, 3, 16, 1, C.wood); R(0, 4, 16, 1, C.woodSh); R(0, 5, 16, 1, C.woodDk);
    R(0, 6, 16, 10, C.wood);
    for (let x = 0; x < 16; x += 4) R(x, 6, 1, 10, C.woodDk);
    R(0, 0, 16, 1, C.moon, 0.14);
  }
  // plaster above the wainscot (tiles X & Y) — cool, damp-speckled
  function plasterWall(a) {
    const { P, R } = a;
    R(0, 0, 16, 16, C.concHi);
    for (let y = 0; y < 16; y++) for (let x = 0; x < 16; x++) { const s = rnd(x * 2 + 5, y * 3 + 2); if (s > 0.88) P(x, y, C.conc, 0.5); else if (s < 0.10) P(x, y, C.concSh, 0.35); }
    if (true) { for (let i = 0; i < 3; i++) P(4 + i * 4, 6 + i, C.tileStain, 0.35); }   // damp bloom
  }
  // wet floor overlay — a slick sheen to lay over hexFloor near the shower
  function wetFloor(a) {
    const { P } = a;
    for (let y = 0; y < 16; y++) for (let x = 0; x < 16; x++) { const s = rnd(x * 9 + 3, y * 4); if (s > 0.62) P(x, y, C.water, 0.22); if (s > 0.94) P(x, y, C.waterHi, 0.5); }
  }
  function drainGrate(a) {
    const { P, R, ell } = a;
    ell(8, 8, 5, 4, C.concSh); ell(8, 8, 4, 3, C.ironDk);
    for (let i = 0; i < 4; i++) R(5 + i * 2, 6, 1, 5, C.iron);
    R(4, 5, 9, 1, C.ironHi, 0.7); P(4, 8, C.rust); P(12, 9, C.rust);
  }

  // ============================================================== SHOWER ==
  // concrete pan 32×16 — poured slab with a raised lip you step over, slight sheen
  function showerPan(a) {
    const { P, R } = a;
    R(0, 4, 32, 12, C.conc);
    for (let y = 4; y < 16; y++) for (let x = 0; x < 32; x++) { const s = rnd(x * 3, y * 5 + 1); if (s > 0.88) P(x, y, C.concHi, 0.45); else if (s < 0.10) P(x, y, C.concSh, 0.4); }
    R(0, 4, 32, 2, C.concHi);                        // top of lip catches light
    R(0, 6, 32, 1, C.concSh);                        // inside edge of the lip
    R(0, 14, 32, 2, C.concDk);                       // front face shadow
    // sunken basin with the drain
    R(4, 8, 24, 5, C.concSh, 0.5);
    R(14, 10, 4, 3, C.ironDk); R(15, 10, 1, 3, C.iron); R(17, 10, 1, 3, C.iron);
    for (let i = 0; i < 4; i++) P(6 + i * 6, 9, C.tileStain, 0.5);   // old mineral staining
    P(2, 13, C.water, 0.5); P(29, 12, C.water, 0.4);
  }
  // shower back wall 32×48 — full stall height of bare concrete: form seams, damp streaks, rust
  function showerBackWall(a) {
    const { P, R } = a;
    R(0, 0, 32, 48, C.conc);
    for (let y = 0; y < 48; y++) for (let x = 0; x < 32; x++) { const s = rnd(x * 2 + 7, y * 3); if (s > 0.87) P(x, y, C.concHi, 0.45); else if (s < 0.11) P(x, y, C.concSh, 0.4); }
    for (const sy of [11, 23, 35]) { R(0, sy, 32, 1, C.concSh); R(0, sy + 1, 32, 1, C.concHi, 0.6); }   // form-board seams
    for (let i = 0; i < 5; i++) { const x = 3 + i * 6; for (let y = 13; y < 46; y++) if (rnd(x, y) > 0.55) P(x, y, C.tileStain, 0.22); }  // damp streaks
    P(6, 11, C.rust); P(7, 11, C.rustHi); P(25, 23, C.rust); P(13, 35, C.rust);   // tie-hole rust
    R(0, 44, 32, 1, C.concSh, 0.7); R(0, 45, 32, 3, C.concDk, 0.5);   // damp base course
    R(0, 0, 32, 1, C.moon, 0.12);
  }
  // exposed black iron pipe riser 16×32 — threaded couplings, a wall strap, faint rust
  function pipeRiser(a) {
    const { P, R } = a;
    R(7, 0, 3, 32, C.iron); R(7, 0, 1, 32, C.ironHi); R(9, 0, 1, 32, C.ironDk);
    for (const y of [5, 18, 28]) { R(6, y, 5, 3, C.iron); R(6, y, 5, 1, C.ironHi); R(6, y + 2, 5, 1, C.ironDk); }  // couplings
    R(4, 12, 9, 2, C.ironDk); R(4, 12, 9, 1, C.ironHi, 0.6);          // wall strap
    P(8, 22, C.rust); P(8, 23, C.rustHi); P(7, 9, C.rust);
  }
  // shower head 16×16 — 2 frames: [0] off (a single hanging drip) [1] on (spray building)
  function showerHead(a, f) {
    const { P, R } = a;
    R(7, 0, 3, 4, C.iron); R(7, 0, 1, 4, C.ironHi);                  // stub from the pipe
    R(5, 4, 7, 2, C.iron); R(5, 4, 7, 1, C.ironHi);                  // gooseneck elbow
    R(4, 6, 9, 3, C.iron); R(4, 6, 9, 1, C.ironHi); R(4, 8, 9, 1, C.ironDk);   // head body
    R(5, 9, 7, 1, C.brassDk);                                         // brass face plate
    for (let i = 0; i < 4; i++) P(5 + i * 2, 9, C.brass);              // nozzle holes
    P(11, 7, C.rust); P(4, 7, C.rust);
    if (f === 0) { P(8, 11, C.water, 0.8); P(8, 13, C.water, 0.5); }   // leaky drip
    else { for (let i = 0; i < 5; i++) { P(4 + i * 2, 11, C.waterHi, 0.8); P(4 + i * 2, 13, C.water, 0.6); } }
  }
  // valve handle 16×16 — 2 frames: [0] closed (cross vertical) [1] open (turned 45°)
  function valveHandle(a, f) {
    const { P, R, ell } = a;
    R(6, 8, 4, 8, C.iron); R(6, 8, 1, 8, C.ironHi);                   // valve body on the pipe
    R(5, 6, 6, 2, C.brassDk);
    if (f === 0) { R(7, 1, 2, 6, C.brass); R(4, 3, 8, 2, C.brass); R(4, 3, 8, 1, C.brassHi); }
    else { for (let i = 0; i < 5; i++) { P(5 + i, 6 - i, C.brass); P(6 + i, 6 - i, C.brassHi); P(11 - i, 6 - i, C.brass); } }
    ell(8, 5, 1, 1, C.brassHi);
  }
  // curtain rail 48×16 — iron rod, rings, end brackets
  function curtainRail(a) {
    const { P, R } = a;
    R(0, 4, 48, 2, C.iron); R(0, 4, 48, 1, C.ironHi);
    R(0, 2, 3, 6, C.ironDk); R(45, 2, 3, 6, C.ironDk);                // brackets
    for (let i = 0; i < 8; i++) { const x = 3 + i * 6; R(x, 3, 4, 1, C.ironHi); R(x, 6, 4, 1, C.iron); P(x, 4, C.ironHi); P(x + 3, 4, C.ironHi); }
    P(20, 6, C.rust); P(33, 5, C.rust);
  }
  // curtain 48×48 — full stall drop. 4 frames: closed → 1/3 → 2/3 → fully bunched open at the left
  function curtain(a, f) {
    const { P, R } = a;
    const cover = [48, 32, 17, 9][f];         // how much width the fabric spans
    const folds = [8, 6, 4, 3][f];
    R(0, 0, cover, 48, C.fab);
    const pitch = Math.max(2, Math.floor(cover / folds));
    for (let i = 0; i < folds; i++) {
      const x = i * pitch;
      R(x, 0, 1, 48, C.fabSh);                                        // fold valley
      R(x + 1, 0, 1, 48, C.fabHi, 0.7);                               // crest catching moonlight
      for (let y = 6; y < 46; y += 7) P(x, y + (i % 2), C.fabDk, 0.45);  // valley deepens in places
    }
    R(0, 0, cover, 2, C.fabDk);                                       // gathered top hem
    for (let i = 0; i < folds; i++) P(i * pitch + 1, 1, C.ironHi);      // ring grommets
    R(0, 45, cover, 3, C.fabSh); R(0, 47, cover, 1, C.fabDk);         // weighted bottom hem
    for (let y = 2; y < 45; y++) for (let x = 0; x < cover; x++) { const s = rnd(x * 3 + f, y * 5); if (s > 0.94) P(x, y, C.fabHi, 0.25); else if (s < 0.05) P(x, y, C.fabDk, 0.3); }
    for (let i = 0; i < 5; i++) P(2 + i * 5, 38 + (i % 3), C.tileStain, 0.35);   // old mildew spotting at the hem
    if (f > 0) { R(cover, 0, 1, 48, C.fabDk, 0.5); }                    // shadowed leading edge
  }
  // water stream 16×32 — 4 frames of falling water, widening + splash at the base
  function waterStream(a, f) {
    const { P, R } = a;
    for (let i = 0; i < 5; i++) {
      const x = 3 + i * 2 + (i === 2 ? 0 : 0);
      for (let y = 0; y < 28; y++) {
        const s = rnd(x * 7 + f * 13, y * 3 + i);
        if (s > 0.30) P(x + ((y + f) % 3 === 0 ? (i > 2 ? 1 : -1) : 0), y, s > 0.80 ? C.waterHi : C.water, 0.62);
      }
    }
    // splash ring in the pan
    for (let i = 0; i < 7; i++) { const sx = 1 + i * 2, sy = 28 + ((i + f) % 2); P(sx, sy, C.waterHi, 0.7); P(sx, sy + 1, C.water, 0.45); }
    for (let i = 0; i < 3; i++) { const dx = 2 + ((f + i * 3) % 12), dy = 24 - ((f * 2 + i * 5) % 8); P(dx, dy, C.waterHi, 0.8); }   // flung droplets
  }
  // steam 32×32 — 4 frames of wisps rising and dissipating
  function steam(a, f) {
    const { P, ell } = a;
    for (let i = 0; i < 6; i++) {
      const ph = (f + i * 1.6);
      const cx = 5 + i * 4 + Math.round(Math.sin(ph * 0.9) * 2);
      const cy = 26 - ((i * 5 + f * 3) % 26);
      const r = 2 + ((i + f) % 3);
      const al = 0.30 * (cy / 26) + 0.06;
      ell(cx, cy, r + 1, r, C.steam, al * 0.6);
      ell(cx, cy - 1, r, Math.max(1, r - 1), C.steamHi, al);
    }
    for (let i = 0; i < 10; i++) { const x = (i * 7 + f * 3) % 32, y = (i * 11 + f * 5) % 30; P(x, y, C.steam, 0.10); }
  }
  // puddle 16×16 — 3 frames: still, one drip-ripple, ripple spreading
  function puddle(a, f) {
    const { P, ell } = a;
    ell(8, 11, 6, 3, C.waterDk, 0.45); ell(8, 11, 5, 2, C.water, 0.5);
    ell(6, 10, 2, 1, C.waterHi, 0.55);
    if (f === 1) { ell(9, 11, 2, 1, C.waterHi, 0.8); P(9, 6, C.waterHi, 0.9); }
    if (f === 2) { for (let i = 0; i < 2; i++) ell(9, 11, 3 + i * 2, 1 + i, C.waterHi, 0.35 - i * 0.12); }
  }

  // ============================================================== FIXTURES ==
  // pedestal sink 16×32 — porcelain basin on a fluted column, brass tap
  function pedestalSink(a) {
    const { P, R } = a;
    R(2, 8, 12, 4, C.porc); R(2, 8, 12, 1, C.porcHi);                 // basin rim
    R(3, 9, 10, 2, C.porcSh, 0.6);                                     // bowl interior
    R(3, 12, 10, 2, C.porc); R(4, 14, 8, 2, C.porcSh);                // basin underside
    R(6, 16, 4, 12, C.porc); R(6, 16, 1, 12, C.porcHi); R(9, 16, 1, 12, C.porcDk);  // column
    for (let y = 17; y < 28; y += 3) R(6, y, 4, 1, C.porcSh, 0.5);      // fluting
    R(4, 28, 8, 3, C.porc); R(4, 28, 8, 1, C.porcHi); R(4, 30, 8, 1, C.porcDk);     // foot
    R(7, 5, 2, 3, C.brass); R(6, 4, 4, 1, C.brassHi); R(9, 6, 2, 1, C.brass);        // tap + spout
    P(5, 7, C.brass); P(11, 7, C.brass);                               // hot/cold knobs
    P(3, 13, C.rust, 0.6); P(12, 27, C.tileStain, 0.5);                // age
  }
  // mirror 16×16 — 2 frames: [0] clear (reflects the dim room) [1] fogged from the shower
  function mirror(a, f) {
    const { P, R } = a;
    R(0, 0, 16, 16, C.woodSh); R(1, 1, 14, 14, C.woodHi); R(2, 2, 12, 12, C.woodDk);   // wood frame
    R(3, 3, 10, 10, C.glass);
    if (f === 0) {
      R(3, 3, 10, 4, C.glassHi, 0.5); R(4, 4, 3, 2, C.moon, 0.6);        // moonlight glint
      for (let i = 0; i < 4; i++) P(4 + i * 2, 10 + (i % 2), C.glassHi, 0.3);
    } else {
      R(3, 3, 10, 10, C.steam, 0.55);
      for (let y = 4; y < 12; y++) for (let x = 4; x < 12; x++) if (rnd(x * 3, y * 5) > 0.7) P(x, y, C.steamHi, 0.35);
      for (let y = 5; y < 12; y++) P(9, y, C.glass, 0.5);               // a runnel of condensation
      P(10, 11, C.waterHi, 0.6);
    }
  }
  // rustic vanity 32×32 — plank cabinet, two doors, dropped-in basin, brass pulls
  function vanity(a) {
    const { P, R } = a;
    R(0, 6, 32, 4, C.wood); R(0, 6, 32, 1, C.woodHi); R(0, 9, 32, 1, C.woodSh);         // counter slab
    R(0, 10, 32, 20, C.wood);
    for (let x = 0; x < 32; x += 4) R(x, 10, 1, 20, C.woodDk, 0.7);                      // plank seams
    R(2, 12, 12, 16, C.woodSh, 0.6); R(18, 12, 12, 16, C.woodSh, 0.6);                  // door recesses
    R(3, 13, 10, 14, C.wood); R(19, 13, 10, 14, C.wood);
    R(3, 13, 10, 1, C.woodHi, 0.6); R(19, 13, 10, 1, C.woodHi, 0.6);
    R(11, 19, 2, 2, C.brass); R(19, 19, 2, 2, C.brass); P(11, 19, C.brassHi); P(19, 19, C.brassHi);  // pulls
    R(0, 30, 32, 2, C.woodDk);                                                            // toe kick
    // basin sunk into the counter
    R(10, 3, 12, 4, C.porc); R(10, 3, 12, 1, C.porcHi); R(11, 4, 10, 2, C.porcSh, 0.7);
    R(15, 0, 2, 3, C.brass); R(14, 0, 4, 1, C.brassHi); R(17, 1, 2, 1, C.brass);          // tap
    for (let y = 10; y < 30; y++) for (let x = 0; x < 32; x++) { const s = rnd(x * 3 + 2, y * 7); if (s > 0.94) P(x, y, C.grain, 0.4); }
    P(1, 27, C.tileStain, 0.5); P(30, 24, C.tileStain, 0.4);
  }
  // toilet 16×32 — old high-ish tank, bowl, wooden seat lid, brass lever
  function toilet(a) {
    const { P, R } = a;
    R(3, 2, 10, 12, C.porc); R(3, 2, 10, 1, C.porcHi); R(3, 13, 10, 1, C.porcSh);   // tank
    R(3, 2, 1, 12, C.porcHi); R(12, 2, 1, 12, C.porcDk);
    R(2, 0, 12, 2, C.wood); R(2, 0, 12, 1, C.woodHi);                                // wooden tank lid
    R(13, 4, 2, 1, C.brass); P(15, 4, C.brassHi);                                     // flush lever
    R(5, 14, 6, 4, C.porcSh);                                                         // pedestal neck
    R(3, 18, 10, 6, C.porc); R(3, 18, 10, 1, C.porcHi); R(4, 19, 8, 3, C.porcSh, 0.6);  // bowl
    R(2, 17, 12, 2, C.wood); R(2, 17, 12, 1, C.woodHi);                              // wooden seat ring
    R(4, 24, 8, 5, C.porc); R(4, 24, 1, 5, C.porcHi); R(11, 24, 1, 5, C.porcDk);      // base
    R(3, 29, 10, 2, C.porcSh); R(3, 30, 10, 1, C.porcDk);
    P(12, 22, C.tileStain, 0.5); P(4, 28, C.rust, 0.5);
  }
  // open plank shelves 32×16 — two boards on iron brackets with bottles and folded cloth
  function plankShelf(a) {
    const { P, R } = a;
    R(0, 3, 32, 2, C.wood); R(0, 3, 32, 1, C.woodHi); R(0, 5, 32, 1, C.woodSh);      // upper board
    R(0, 13, 32, 2, C.wood); R(0, 13, 32, 1, C.woodHi); R(0, 15, 32, 1, C.woodSh);   // lower board
    for (const x of [3, 27]) { R(x, 5, 2, 8, C.iron); R(x, 5, 1, 8, C.ironHi); R(x, 15, 2, 1, C.iron); }  // brackets
    // bottles / jars on the upper board
    R(7, 0, 3, 3, C.bottleA); P(8, 0, C.bottleA); R(7, 0, 1, 3, C.steamHi, 0.25);
    R(12, 1, 2, 2, C.bottleB); R(16, 0, 3, 3, C.bottleC); R(16, 0, 1, 3, C.steamHi, 0.2);
    R(21, 1, 4, 2, C.towel); R(21, 1, 4, 1, C.towelHi);                              // folded cloth
    // lower board: folded towels
    R(8, 10, 6, 3, C.towel); R(8, 10, 6, 1, C.towelHi); R(8, 12, 6, 1, C.towelSh);
    R(17, 11, 5, 2, C.towelSh); R(17, 11, 5, 1, C.towel);
    for (let x = 0; x < 32; x++) { if (rnd(x, 4) > 0.9) P(x, 4, C.grain, 0.5); if (rnd(x, 14) > 0.9) P(x, 14, C.grain, 0.5); }
  }
  // towel rack 32×16 — iron rod on wood blocks, two hanging towels
  function towelRack(a) {
    const { P, R } = a;
    R(2, 2, 3, 3, C.wood); R(27, 2, 3, 3, C.wood);                                    // mounting blocks
    R(4, 3, 24, 2, C.iron); R(4, 3, 24, 1, C.ironHi);
    // towel A
    R(7, 4, 7, 11, C.towel); R(7, 4, 7, 1, C.towelHi);
    for (let y = 5; y < 15; y += 3) R(7, y, 7, 1, C.towelSh, 0.5);
    R(13, 5, 1, 10, C.towelSh); R(7, 14, 7, 1, C.towelSh);
    // towel B, hung shorter
    R(18, 4, 6, 8, C.towelSh); R(18, 4, 6, 1, C.towel);
    for (let y = 6; y < 12; y += 3) R(18, y, 6, 1, C.towelHi, 0.35);
    P(9, 13, C.tileStain, 0.4); P(21, 10, C.tileStain, 0.35);
  }
  // soap dish + bottles clutter 16×16
  function soapClutter(a) {
    const { P, R, ell } = a;
    R(0, 12, 16, 3, C.wood); R(0, 12, 16, 1, C.woodHi);                              // small ledge
    ell(4, 11, 3, 1, C.porc); P(4, 10, C.porcHi);                                     // soap dish
    ell(4, 10, 2, 1, C.soap); P(3, 10, C.steamHi, 0.6);                               // worn bar of soap
    R(9, 5, 3, 7, C.bottleA); R(9, 5, 1, 7, C.steamHi, 0.25); R(10, 3, 1, 2, C.iron); // tall bottle
    R(13, 8, 2, 4, C.bottleB); P(13, 7, C.brassDk);
    P(7, 11, C.water, 0.6);
  }

  // ============================================================== DOOR ==
  // plank door 32×48 — 4 frames: closed → latch lifted → half open → wide open (oblique swing)
  function door(a, f) {
    const { P, R, line } = a;
    R(0, 0, 32, 48, C.dark, 0);                                                       // (transparent base)
    // door jamb / frame always drawn
    R(0, 0, 3, 48, C.woodSh); R(0, 0, 3, 1, C.woodHi);
    R(29, 0, 3, 48, C.woodSh);
    R(0, 0, 32, 2, C.woodSh); R(0, 0, 32, 1, C.woodHi);
    const dark = C.black;
    if (f === 0 || f === 1) {
      // slab fills the opening
      R(3, 2, 26, 46, C.wood);
      for (let x = 3; x < 29; x += 5) { R(x, 2, 1, 46, C.woodDk); R(x + 1, 2, 1, 46, C.woodHi, 0.35); }
      R(3, 10, 26, 2, C.woodSh); R(3, 36, 26, 2, C.woodSh);                            // ledger battens
      for (let y = 2; y < 48; y++) for (let x = 3; x < 29; x++) { const s = rnd(x * 3, y * 5 + 1); if (s > 0.94) P(x, y, C.grain, 0.45); }
      R(4, 6, 2, 4, C.iron); R(4, 40, 2, 4, C.iron);                                   // strap hinges
      R(24, 22, 3, 3, C.ironDk); P(25, 23, C.brass);                                    // handle
      // hook-and-eye latch: hooked (f0) → lifted clear of the eye (f1)
      R(20, 18, 1, 1, C.brassDk);                                                       // eye plate on the jamb
      if (f === 0) { line(27, 17, 22, 19, C.brass); P(21, 19, C.brassHi); P(21, 20, C.brass); }
      else { line(27, 17, 22, 15, C.brass); P(21, 14, C.brassHi); P(22, 16, C.brass); }
      R(27, 16, 2, 2, C.brassDk);                                                       // hook anchor
    } else {
      // swung inward: dark opening + a foreshortened slab on the hinge side
      R(3, 2, 26, 46, dark);
      for (let y = 2; y < 48; y++) for (let x = 3; x < 29; x++) if (rnd(x * 7, y * 3) > 0.96) P(x, y, C.shadow, 0.6);
      const w = f === 2 ? 14 : 6;
      R(3, 2, w, 46, C.wood);
      for (let x = 3; x < 3 + w; x += 4) R(x, 2, 1, 46, C.woodDk);
      R(3 + w, 2, 1, 46, C.woodHi);                                                     // lit leading edge
      R(3, 2, w, 1, C.woodHi, 0.5);
      R(4, 6, 2, 4, C.iron); R(4, 40, 2, 4, C.iron);
      R(27, 16, 2, 2, C.brassDk); line(27, 17, 27, 22, C.brass);                         // hook hangs free
      // light spill from the room beyond onto the floor
      for (let i = 0; i < 6; i++) P(3 + w + 1 + i, 44 + (i % 3), C.moon, 0.18);
    }
  }

  // ============================================================== WINDOW ==
  // small frosted window 32×32 — plank frame, frosted lower pane, night beyond
  function window_(a, watch, f) {
    const { P, R, ell } = a;
    R(0, 0, 32, 32, C.woodSh); R(1, 1, 30, 30, C.woodHi); R(2, 2, 28, 28, C.woodDk);   // casing
    R(3, 3, 26, 26, C.glass);
    // night sky beyond the clear upper band
    for (let y = 3; y < 12; y++) for (let x = 3; x < 29; x++) { const s = rnd(x * 5, y * 3); P(x, y, s > 0.97 ? C.moon : C.glass, s > 0.97 ? 0.9 : 1); }
    ell(23, 7, 3, 3, C.moonDk); ell(23, 7, 2, 2, C.moon);                               // moon
    for (let i = 0; i < 5; i++) { const x = 4 + i * 5; R(x, 10 - (i % 2), 5, 2, C.dark, 0.8); }  // treeline silhouette
    // frosted lower panes
    R(3, 12, 26, 17, C.steam, 0.42);
    for (let y = 12; y < 29; y++) for (let x = 3; x < 29; x++) if (rnd(x * 3 + 1, y * 7) > 0.72) P(x, y, C.steamHi, 0.28);
    // muntins
    R(15, 3, 2, 26, C.woodDk); R(15, 3, 1, 26, C.wood);
    R(3, 11, 26, 2, C.woodDk); R(3, 11, 26, 1, C.wood);
    R(2, 28, 28, 3, C.wood); R(2, 28, 28, 1, C.woodHi);                                  // sill
    P(5, 30, C.tileStain, 0.5); P(26, 29, C.rust, 0.4);
    // ---- watcher variant: two glowing red eyes outside the frosted glass ----
    if (watch) {
      const lift = [0, 0, 1][f] || 0;                                                    // eyes drift slightly
      const al = [0.55, 0.95, 0.75][f];
      const ex = [10, 20], ey = 18 - lift;
      // a body-shaped darkening pressed against the pane
      for (let y = 13; y < 29; y++) for (let x = 5; x < 27; x++) {
        const d = Math.abs(x - 16) / 11 + Math.abs(y - 22) / 9;
        if (d < 1) P(x, y, C.black, 0.30 * al * (1 - d) + 0.10);
      }
      for (const x of ex) {
        ell(x, ey, 3, 2, C.eyeGlow, 0.30 * al);                                          // bloom through the frost
        ell(x, ey, 2, 1, C.eye, 0.85 * al);
        P(x, ey, C.eyeHi, al); P(x - 1, ey, C.eye, al);
        if (f === 1) { P(x, ey - 1, C.eyeHi, 0.6 * al); }                                // brighter pulse frame
      }
      // breath-fog blooming between the eyes
      if (f !== 0) for (let i = 0; i < 5; i++) P(13 + i, 22 + (i % 2), C.steamHi, 0.35 * al);
    }
  }

  // ============================================================== ATLAS ==
  const A = {
    hexFloor: [0, 0, 16, 16, 1], hexFloorWorn: [16, 0, 16, 16, 1], wainscotWall: [32, 0, 16, 16, 1],
    wainscotCap: [48, 0, 16, 16, 1], plasterWall: [64, 0, 16, 16, 1], wetFloor: [80, 0, 16, 16, 1],
    drainGrate: [96, 0, 16, 16, 1], soapClutter: [112, 0, 16, 16, 1],
    mirror: [128, 0, 16, 16, 2], valveHandle: [160, 0, 16, 16, 2], showerHead: [192, 0, 16, 16, 2],
    plankShelf: [0, 16, 32, 16, 1], towelRack: [32, 16, 32, 16, 1], showerPan: [64, 16, 32, 16, 1],
    puddle: [96, 16, 16, 16, 3], curtainRail: [144, 16, 48, 16, 1],
    pedestalSink: [0, 32, 16, 32, 1], toilet: [16, 32, 16, 32, 1], vanity: [32, 32, 32, 32, 1],
    pipeRiser: [64, 32, 16, 32, 1], waterStream: [80, 32, 16, 32, 4], window: [144, 32, 32, 32, 1],
    steam: [0, 64, 32, 32, 4],
    curtain: [0, 96, 48, 48, 4], showerBackWall: [192, 96, 32, 48, 1], door: [224, 96, 32, 48, 4],
  };
  const AW = [{ windowWatch: [0, 0, 32, 32, 3] }];

  async function build({ createCanvas, saveFile }) {
    const cv = createCanvas(384, 192); const ctx = cv.getContext('2d'); ctx.imageSmoothingEnabled = false;
    const at = (name, fn) => { const [x, y, w, h, n] = A[name]; for (let f = 0; f < n; f++) fn(api(ctx, x + f * w, y), f); };
    at('hexFloor', a => hexFloor(a, false)); at('hexFloorWorn', a => hexFloor(a, true));
    at('wainscotWall', a => wainscotWall(a)); at('wainscotCap', a => wainscotCap(a));
    at('plasterWall', a => plasterWall(a)); at('wetFloor', a => wetFloor(a));
    at('drainGrate', a => drainGrate(a)); at('soapClutter', a => soapClutter(a));
    at('mirror', (a, f) => mirror(a, f)); at('valveHandle', (a, f) => valveHandle(a, f));
    at('showerHead', (a, f) => showerHead(a, f));
    at('plankShelf', a => plankShelf(a)); at('towelRack', a => towelRack(a));
    at('showerPan', a => showerPan(a)); at('puddle', (a, f) => puddle(a, f));
    at('curtainRail', a => curtainRail(a));
    at('pedestalSink', a => pedestalSink(a)); at('toilet', a => toilet(a)); at('vanity', a => vanity(a));
    at('pipeRiser', a => pipeRiser(a)); at('waterStream', (a, f) => waterStream(a, f));
    at('window', a => window_(a, false, 0));
    at('steam', (a, f) => steam(a, f)); at('showerBackWall', a => showerBackWall(a));
    at('curtain', (a, f) => curtain(a, f));
    at('door', (a, f) => door(a, f));
    await saveFile('bathroom_colddusk.png', cv);
    const up = createCanvas(384 * 4, 192 * 4); const u = up.getContext('2d'); u.imageSmoothingEnabled = false; u.drawImage(cv, 0, 0, up.width, up.height);
    await saveFile('bathroom_colddusk-4x.png', up);
    // watcher window sheet: 3 frames of the eyes outside the glass
    const wcv = createCanvas(96, 32); const wc = wcv.getContext('2d'); wc.imageSmoothingEnabled = false;
    for (let f = 0; f < 3; f++) window_(api(wc, f * 32, 0), true, f);
    await saveFile('bathroom_window_watch.png', wcv);
    const wup = createCanvas(96 * 8, 32 * 8); const wu = wup.getContext('2d'); wu.imageSmoothingEnabled = false; wu.drawImage(wcv, 0, 0, wup.width, wup.height);
    await saveFile('bathroom_window_watch-4x.png', wup);
    return ['bathroom_colddusk.png', 'bathroom_window_watch.png'];
  }
  return { build, ATLAS: A, WATCH: AW, RAW };
})();
if (typeof module !== 'undefined') module.exports = window.Bath;
