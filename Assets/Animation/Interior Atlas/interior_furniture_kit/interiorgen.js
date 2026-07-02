// Interior furniture generator — the cozy living room that is the LAST comfort before the
// Nightmare Realm. Old-school-but-a-little-modern: warm woods, soft fabric, inviting light —
// authored to sit in the 2.5D oblique lounge (16px grid), matching interior_tiles_{dusk,lavender}.
//
// Renders a transparent atlas (propsgen.js pattern) in THREE palettes:
//   'dusk'      — cool teal-grey room; warm rust furniture (cozy against the cold)
//   'lavender'  — faded-purple room; dusty-rose furniture
//   'nightmare' — the dream-rot pass: sofas sag & stain, the TV dies to static + a face,
//                 cabinets/shelves gape to black. This FLICKERS in over the day sprite as the
//                 player realizes they never woke up (drive the flicker on the dread flag).
//
// Objects (living-room set): sofa, couch(loveseat), armchair, coffeeTable, tv(+stand),
// bookshelf, rug, floorLamp — plus a couchDog "occupied" combo (the dog asleep on the sofa).
// Interactive objects carry USE frames (tv off→on→static, lamp off→on); the rest are 1 frame.
//
//   eval(await readFile('interiorgen.js'));
//   window.__inReadImage=readImage; window.__inSave=saveFile; window.__inCanvas=createCanvas;
//   await window.Interior.build();   // -> interior_furniture_{dusk,lavender,nightmare}.png (+ -8x)

window.Interior = (function(){
  const hx=(h)=>{h=h.replace('#','');return [parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];};

  // ---------------- palettes (shared keys so draw fns are palette-agnostic) ----------------
  const RAW = {
    dusk: {
      wood:'#7c5a3a', woodHi:'#9a744c', woodSh:'#5a3f28', woodDk:'#3f2c1c',
      fab:'#a85a3e', fabHi:'#c37a56', fabSh:'#7a3d2a',
      cush:'#d6a65e', cushSh:'#a87838',
      panel:'#39434a', panelHi:'#4f5c64', panelSh:'#232a2f',
      screen:'#20282c', screenOn:'#cfe0dd', screenGlow:'#9fd0c8',
      glow:'#f2ca7a', bulb:'#fff0c8',
      rugA:'#9a5240', rugB:'#dcb890', rugC:'#6f8a80',
      book1:'#a24a3a', book2:'#c8a24e', book3:'#4a7a6a', shelfBack:'#4a382a',
      metal:'#8f8f97', dark:'#0c0d10',
      dogFur:'#e8dcc0', dogFurSh:'#c2b294', dogEar:'#c49a6c', dogNose:'#332822',
      stain:'#3a2a20', blood:'#4a1c16', rot:'#6a7a52',
      contact:'#000000',
    },
    lavender: {
      wood:'#7a584e', woodHi:'#957067', woodSh:'#59403a', woodDk:'#3e2d29',
      fab:'#b06a7c', fabHi:'#c98a99', fabSh:'#82495a',
      cush:'#cc9e50', cushSh:'#9c7434',
      panel:'#3f3a48', panelHi:'#544f6a', panelSh:'#28242f',
      screen:'#221f2a', screenOn:'#e2d2ec', screenGlow:'#c2a2d2',
      glow:'#f0c088', bulb:'#fff2d8',
      rugA:'#8a4a5e', rugB:'#dcb2ba', rugC:'#7a6a9a',
      book1:'#9a4a6a', book2:'#c8a24e', book3:'#6a5a9a', shelfBack:'#463040',
      metal:'#8c8692', dark:'#0b0a0e',
      dogFur:'#e8dcc0', dogFurSh:'#c2b294', dogEar:'#c49a6c', dogNose:'#332822',
      stain:'#38282e', blood:'#4a1c16', rot:'#6a7a52',
      contact:'#000000',
    },
    nightmare: {
      wood:'#4a4438', woodHi:'#5e5748', woodSh:'#332e26', woodDk:'#201c17',
      fab:'#586551', fabHi:'#6e7d64', fabSh:'#3b4536',
      cush:'#6f6247', cushSh:'#453c2a',
      panel:'#2a2e30', panelHi:'#3a4042', panelSh:'#171a1b',
      screen:'#16191a', screenOn:'#8a9088', screenGlow:'#7fb07a',
      glow:'#84d089', bulb:'#c8f0c0',
      rugA:'#4a4436', rugB:'#6a5a4a', rugC:'#4a5648',
      book1:'#4a463c', book2:'#5a4a3a', book3:'#3a4640', shelfBack:'#161009',
      metal:'#5a5a60', dark:'#070709',
      dogFur:'#b7b79a', dogFurSh:'#83836a', dogEar:'#8a8a6e', dogNose:'#0a0a0a',
      stain:'#241d16', blood:'#5a201a', rot:'#7a8a58', rotHi:'#9aaa72', eye:'#b7c7ca',
      contact:'#000000',
    },
  };
  const PAL = {};
  for(const k in RAW){ PAL[k]={}; for(const c in RAW[k]) PAL[k][c]=hx(RAW[k][c]); }

  // ---------------- pixel api (from propsgen.js) ----------------
  function api(ctx,ox,oy){
    const P=(x,y,c,a)=>{ if(x<0||y<0)return; ctx.fillStyle=`rgba(${c[0]},${c[1]},${c[2]},${a==null?1:a})`; ctx.fillRect(ox+x,oy+y,1,1); };
    const R=(x,y,w,h,c,a)=>{ ctx.fillStyle=`rgba(${c[0]},${c[1]},${c[2]},${a==null?1:a})`; ctx.fillRect(ox+x,oy+y,w,h); };
    const line=(x0,y0,x1,y1,c)=>{ x0|=0;y0|=0;x1|=0;y1|=0; let dx=Math.abs(x1-x0),dy=Math.abs(y1-y0),sx=x0<x1?1:-1,sy=y0<y1?1:-1,e=dx-dy; for(;;){ P(x0,y0,c); if(x0===x1&&y0===y1)break; const e2=2*e; if(e2>-dy){e-=dy;x0+=sx;} if(e2<dx){e+=dx;y0+=sy;} } };
    const ell=(cx,cy,rx,ry,c)=>{ for(let y=-ry;y<=ry;y++)for(let x=-rx;x<=rx;x++){ if((x*x)/(rx*rx)+(y*y)/(ry*ry)<=1) P(cx+x,cy+y,c); } };
    return {P,R,line,ell};
  }
  // deterministic speckle
  function rnd(a,b){ let h=(a*374761393+b*668265263)>>>0; h=((h^(h>>>13))*1274126177)>>>0; return ((h^(h>>>16))>>>0)/4294967296; }

  // ================= OBJECTS =================
  // Each fn: (a, f, C, night)  — a=pixel api, f=frame, C=palette colors, night=nightmare pass.

  // -- sofa : plush 3-seat, 48x32, base of the room. dog sleeps here (see couchDog). --
  function sofa(a,f,C,night){
    const {P,R}=a;
    R(2,7,44,11,C.fab); R(2,7,44,1,C.fabHi);                 // backrest
    R(0,11,6,19,C.fab); R(42,11,6,19,C.fab);                 // arms
    R(0,11,2,17,C.fabHi); R(46,11,2,17,C.fabSh); R(0,11,6,2,C.fabHi);
    for(let i=0;i<3;i++){ const x=7+i*12; R(x,17,12,8,C.cush); R(x,17,12,1,C.cushSh); R(x+11,17,1,8,C.fabSh); } // seat cushions
    for(let i=0;i<2;i++){ const x=9+i*15; R(x,9,11,7,C.cush); R(x,9,11,1,C.fabHi); }                          // back pillows
    R(6,25,36,5,C.fabSh);                                    // skirt
    R(6,29,3,2,C.woodDk); R(39,29,3,2,C.woodDk);             // feet
    if(night){
      R(10,20,7,4,C.stain); R(27,19,8,4,C.stain);            // seat stains
      R(2,7,44,1,C.fabSh); P(24,13,C.blood); P(25,14,C.blood); P(16,22,C.blood);
      for(let i=0;i<10;i++){ const x=2+((i*37)%44), y=8+((i*23)%20); if(rnd(x,y)>0.72) P(x,y,C.rot); }
      R(20,17,2,8,C.fabSh);                                  // a torn seam gaps dark
      P(21,20,C.dark); P(21,21,C.dark);
    }
  }

  // -- couch : compact loveseat, 32x32 --
  function couch(a,f,C,night){
    const {P,R}=a;
    R(2,8,28,10,C.fab); R(2,8,28,1,C.fabHi);
    R(0,12,5,18,C.fab); R(27,12,5,18,C.fab);
    R(0,12,2,15,C.fabHi); R(30,12,2,15,C.fabSh); R(0,12,5,2,C.fabHi);
    for(let i=0;i<2;i++){ const x=6+i*11; R(x,17,10,8,C.cush); R(x,17,10,1,C.cushSh); R(x+9,17,1,8,C.fabSh); }
    R(7,10,9,6,C.cush); R(7,10,9,1,C.fabHi);
    R(5,25,22,5,C.fabSh);
    R(6,29,3,2,C.woodDk); R(24,29,3,2,C.woodDk);
    if(night){ R(9,20,7,4,C.stain); P(18,13,C.blood); for(let i=0;i<7;i++){const x=3+((i*29)%26),y=9+((i*19)%18); if(rnd(x,y)>0.7)P(x,y,C.rot);} R(15,17,2,8,C.fabSh); P(15,21,C.dark);}
  }

  // -- armchair : single seat, 32x32 --
  function armchair(a,f,C,night){
    const {P,R}=a;
    R(6,6,20,12,C.fab); R(6,6,20,1,C.fabHi);                 // tall back
    R(2,12,5,18,C.fab); R(25,12,5,18,C.fab);                 // arms
    R(2,12,2,15,C.fabHi); R(28,12,2,15,C.fabSh); R(2,12,5,2,C.fabHi);
    R(8,17,16,8,C.cush); R(8,17,16,1,C.cushSh); R(8,24,16,1,C.fabSh); // seat cushion
    R(7,25,18,5,C.fabSh);                                    // base
    R(8,29,3,2,C.woodDk); R(21,29,3,2,C.woodDk);
    if(night){ R(11,20,9,4,C.stain); P(15,10,C.blood); P(16,11,C.blood); for(let i=0;i<7;i++){const x=6+((i*23)%20),y=7+((i*17)%16); if(rnd(x,y)>0.7)P(x,y,C.rot);} R(15,17,2,8,C.fabSh);}
  }

  // -- coffeeTable : low table w/ top lip + a book, 32x16 --
  function coffeeTable(a,f,C,night){
    const {P,R}=a;
    R(3,6,26,3,C.wood); R(3,6,26,1,C.woodHi);                // top
    R(3,9,26,2,C.woodSh);                                    // apron (oblique lip)
    R(5,11,3,5,C.woodDk); R(24,11,3,5,C.woodDk);             // legs
    // a little book resting on top (cozy)
    if(!night){ R(8,4,7,2,C.book2); R(8,4,7,1,C.fabHi); P(15,5,C.book1); }
    else { R(8,4,7,2,C.stain); R(3,6,26,1,C.woodSh); P(20,7,C.blood); P(12,8,C.rot); R(14,6,2,3,C.dark); } // cracked, wet
  }

  // -- tv : CRT-ish set on a wood stand, 32x32. frames: 0 off, 1 on-a, 2 on-b, 3 static --
  function tv(a,f,C,night){
    const {P,R}=a;
    // stand
    R(2,22,28,6,C.wood); R(2,22,28,1,C.woodHi); R(2,27,28,1,C.woodSh);
    R(4,28,3,3,C.woodDk); R(25,28,3,3,C.woodDk);
    R(9,24,14,3,C.woodSh);                                   // stand shelf line
    // set body
    R(4,4,24,17,C.panel); R(4,4,24,1,C.panelHi); R(4,20,24,1,C.panelSh); R(27,4,1,17,C.panelSh);
    // screen bezel + screen
    R(6,6,16,12,C.panelSh); 
    const on = (f===1||f===2);
    const showFace = night;                                  // nightmare: a face in the static
    if(night){
      // dead channel static + faint pale face on every frame
      for(let y=7;y<17;y++) for(let x=7;x<21;x++){ const s=rnd(x*3+f, y*5+f); P(x,y, s>0.6?C.screenGlow: (s>0.3?C.screen:C.screenOn), s>0.6?0.5:1); }
      if(showFace){ // two hollow eyes + a wide mouth surfacing in the snow
        P(10,10,C.dark); P(11,10,C.dark); P(16,10,C.dark); P(17,10,C.dark);
        R(11,13,6,1,C.dark); P(10,12,C.dark); P(17,12,C.dark);
      }
    } else if(on){
      R(7,7,14,10,C.screenOn);                               // lit picture
      // a simple broadcast shape that shifts between the two on-frames
      const dx = f===1?0:2;
      R(9+dx,9,6,5,C.screenGlow); R(8+dx,13,9,2,C.panelHi);
      P(19,8,C.bulb);
    } else if(f===3){
      for(let y=7;y<17;y++) for(let x=7;x<21;x++){ P(x,y, rnd(x+f,y)>0.5?C.screenGlow:C.screen); } // static
    } else {
      R(7,7,14,10,C.screen); R(8,8,5,3,C.panelSh);           // OFF: dark glass w/ reflection
      P(9,8,C.panelHi);
    }
    // knobs
    P(24,8,C.woodHi); P(24,11,C.woodHi); R(23,7,3,1,C.panelSh);
    P(15,3,C.metal); P(15,2,C.metal); P(9,3,C.metal);        // antenna base
  }

  // -- bookshelf : 3 shelves of books + a couple trinkets, 32x32 --
  function bookshelf(a,f,C,night){
    const {P,R}=a;
    R(1,3,30,28,C.wood); R(1,3,30,1,C.woodHi); R(1,3,1,28,C.woodHi); R(30,3,1,28,C.woodSh);
    for(const sy of [4,13,22]){ R(3,sy,26,8,C.shelfBack); R(3,sy+8,26,1,C.woodSh); }  // recessed bays
    const spineRow=(sy,seed)=>{
      let x=4;
      while(x<28){ const w=1+Math.floor(rnd(x,seed)*2); const c=[C.book1,C.book2,C.book3][(x+seed)%3];
        const h=6+Math.floor(rnd(x+1,seed)*2); R(x,sy+ (8-h)+ (sy? -0:0),w, h, c); R(x,sy+(8-h),w,1, C.fabHi);
        x+=w+ (rnd(x+2,seed)>0.85?1:0); }
    };
    if(!night){ spineRow(4,1); spineRow(13,2); spineRow(22,3); P(25,10,C.book2); }
    else {
      // books slumped, a black GAP where some are missing, a shape inside
      spineRow(4,4);
      R(10,13,12,8,C.dark);                                   // gap to black
      P(13,16,C.eye); P(18,17,C.eye);                         // something waits
      spineRow(22,6);
      for(let i=0;i<8;i++){ const x=4+((i*29)%24), y=5+((i*23)%24); if(rnd(x,y)>0.72) P(x,y,C.rot); }
    }
  }

  // -- rug : floor rug w/ pattern + fringe, 48x16 (draw UNDER furniture) --
  function rug(a,f,C,night){
    const {P,R}=a;
    R(2,3,44,11,C.rugA); R(2,3,44,1,C.rugA);
    R(4,5,40,7,C.rugB);                                       // inner field
    R(6,7,36,3,C.rugA);                                       // center band
    for(let x=8;x<40;x+=4){ P(x,8,C.rugC); P(x+1,8,C.rugC); } // motif dots
    R(4,5,40,1,C.rugC); R(4,11,40,1,C.rugC);                  // border lines
    for(let x=2;x<46;x+=2){ P(x,14,C.rugA); P(x,15,C.rugB); } // fringe
    if(night){ R(14,6,12,5,C.stain); P(30,8,C.blood); P(31,9,C.blood); for(let i=0;i<12;i++){const x=3+((i*41)%42),y=4+((i*17)%9); if(rnd(x,y)>0.7)P(x,y,C.rot);} }
  }

  // -- floorLamp : pole lamp, 16x32. frames: 0 off, 1 on (warm pool) --
  function floorLamp(a,f,C,night){
    const {P,R}=a;
    const on = (f===1);
    // downward light cone FIRST (so the pole/base sit on top of it) — only when lit
    if(on){ for(let y=10;y<27;y++){ const w=Math.round((y-9)*0.5); for(let x=8-w;x<=7+w;x++) P(x,y, C.glow, night?0.11:0.15); } }
    R(7,9,2,19,C.metal); P(7,9,C.woodHi);                    // thin pole
    R(4,28,7,2,C.woodDk); R(5,27,5,1,C.woodSh);              // small base
    // shade (trapezoid)
    R(4,4,8,5,C.fab); R(5,3,6,1,C.fab); R(4,4,8,1,C.fabHi); R(4,8,8,1,C.woodDk);
    if(on){
      R(4,4,8,5, night?C.fabHi:C.bulb); R(4,3,8,1,C.bulb);   // shade glows
      R(6,9,4,2, night?C.glow:C.bulb);                       // bright bulb spill under the shade
    } else {
      R(5,6,6,2,C.fabSh); R(6,9,4,1,C.woodDk);               // unlit
    }
    if(night){ P(4,6,C.stain); P(11,7,C.stain); if(!on) R(7,14,2,5,C.rot,0.4); }
  }

  // -- couchDog : the sofa with the dog curled asleep on it, 48x32. 3 breathing frames. --
  function couchDog(a,f,C,night){
    sofa(a,0,C,night);                                        // the sofa underneath
    const {P,R,ell}=a;
    const br = (f===1)?1:0;                                   // breathing rise
    // a dog curled asleep on the seat (side view, head left, tail wrapping right)
    const bx=16, by=21-br;
    ell(bx,by+1,8,4,C.dogFurSh);                              // under-shadow / body base
    ell(bx,by,8,4,C.dogFur);                                  // body curl
    ell(bx+4,by-1,4,3,C.dogFur);                              // haunch rise (back)
    R(bx-4,by-3,9,1,C.dogFurSh);                              // back line
    // head resting low on the left, snout out
    ell(bx-6,by+1,3,3,C.dogFur);
    R(bx-11,by+1,4,2,C.dogFur); P(bx-11,by+2,C.dogFurSh);     // snout
    P(bx-12,by+1,C.dogNose); P(bx-11,by+1,C.dogNose);         // nose tip
    R(bx-7,by-2,2,3,C.dogEar); P(bx-8,by-1,C.dogEar);         // floppy ear
    ell(bx+7,by-1,2,2,C.dogFur); P(bx+8,by-2,C.dogFurSh);     // curled tail
    P(bx-8,by+4,C.dogFur); P(bx-6,by+4,C.dogFurSh);           // tucked paw
    if(!night){ P(bx-6,by,C.dogNose); }                       // eye closed — peaceful
    else {
      P(bx-6,by,C.eye); P(bx-5,by,C.eye);                     // one eye OPEN, watching you
      R(bx-2,by+3,5,1,C.stain); P(bx+2,by+2,C.blood);
      for(let i=0;i<7;i++){ const x=bx-7+((i*13)%15), y=by-3+((i*7)%7); if(rnd(x,y)>0.64) P(x,y,C.rot); }
    }
  }

  // ---------------- atlas layout [name, fn, x, y, w, h, frames] ----------------
  const LAYOUT = [
    ['sofa',        sofa,         0,  0, 48,32, 1],
    ['couch',       couch,       48,  0, 32,32, 1],
    ['armchair',    armchair,    80,  0, 32,32, 1],
    ['bookshelf',   bookshelf,  112,  0, 32,32, 1],
    ['floorLamp',   floorLamp,  144,  0, 16,32, 2],   // x144..176
    ['coffeeTable', coffeeTable,176,  0, 32,16, 1],   // top-right band
    ['rug',         rug,        176, 16, 48,16, 1],   // under-band (y16)
    ['tv',          tv,           0, 32, 32,32, 4],   // x0..128
    ['couchDog',    couchDog,     0, 64, 48,32, 3],   // x0..144
  ];
  const W = 256, H = 96;

  function renderSheet(ctx, palName){
    const C = PAL[palName], night = (palName==='nightmare');
    ctx.clearRect(0,0,W,H);
    for(const [name,fn,x,y,w,h,frames] of LAYOUT){
      for(let f=0; f<frames; f++) fn(api(ctx, x+f*w, y), f, C, night);
    }
  }

  async function build(){
    const names=[];
    for(const pal of ['dusk','lavender','nightmare']){
      const cv=window.__inCanvas(W,H); const ctx=cv.getContext('2d');
      renderSheet(ctx, pal);
      const name=`interior_furniture_${pal}.png`;
      await window.__inSave(name, cv);
      const up=window.__inCanvas(W*8,H*8); const ux=up.getContext('2d'); ux.imageSmoothingEnabled=false; ux.drawImage(cv,0,0,W*8,H*8);
      await window.__inSave(name.replace('.png','-8x.png'), up);
      names.push(name);
    }
    return names;
  }

  return { build, renderSheet, LAYOUT, PAL, W, H };
})();
