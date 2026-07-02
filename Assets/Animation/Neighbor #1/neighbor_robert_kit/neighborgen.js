// Neighbor generator — "Robert Abernathy", the worn-out-Steve-Jobs technology lunatic.
// A heavy-set middle-aged man: balding grey, round wire glasses, black turtleneck under
// grubby work coveralls, yellow rubber gloves, hedge shears in hand. Friendly, brilliant,
// and VISIBLY off — stiff, a smile held a beat too long.
//
// Authors a front-facing 32-px character sheet matching the player grammar, extended with a
// speak pair:  idle 0-1 · walk 2-4 · speak 5-6   (7 frames -> 224x32).
// Two forms, swapped on the dread flag:
//   'home'      — the daytime neighbor (uncanny but human)
//   'nightmare' — DIFFERENT creepy than the player: no gore, he STRETCHES. Too tall, too thin,
//                 long neck + limbs, glasses gone to blank glare, and in the speak frames the
//                 mouth opens WAY too wide (the jaw drops down the throat into a black void).
//
// Palette is region-keyed (recolour-compatible) so the engine can swap garment/skin colours —
// or roll a fresh random assortment every load (see NEIGHBOR_ROBERT.md).
//
//   eval(await readFile('neighborgen.js'));
//   window.__nbReadImage=readImage; window.__nbSave=saveFile; window.__nbCanvas=createCanvas;
//   await window.Neighbor.build({form:'home'});      // -> neighbor_robert_front.png (+ -8x)
//   await window.Neighbor.build({form:'nightmare'}); // -> neighbor_robert_front_nightmare.png

window.Neighbor = (function(){

  const FRAMES = 7;                 // idle 0-1, walk 2-4, speak 5-6
  const CELL = 32;

  // ---- region-keyed palettes (base + shade per region) ----
  const PAL = {
    home: {
      out:'#150f12',
      skin:'#e3b78e',  skinSh:'#bf885d',
      hair:'#b8b0a6',  hairSh:'#877e73',   // salt-grey, balding
      stub:'#a99a86',                       // grey stubble tint on the jaw
      frame:'#20242b', lens:'#cfe3e8', eye:'#20242b',
      neck:'#2b2f36',  neckSh:'#181b20',    // black turtleneck (the Jobs rollneck)
      cov:'#5f7488',   covSh:'#42525f',     // worn denim-slate coveralls
      strap:'#3c4855', buckle:'#c9a24a',
      glove:'#d8c24e', gloveSh:'#a6923a',   // yellow rubber gloves
      boot:'#3a332c',  bootSh:'#241f1a',
      metal:'#c9ced6', metalSh:'#8b929c', handle:'#8a2f2f',
      void:'#1a0f10',
    },
    nightmare: {
      out:'#0b090b',
      skin:'#b7b79a',  skinSh:'#81816a',    // waxy grey-green, drained
      hair:'#8e877e',  hairSh:'#584f4a',
      stub:'#6f6759',
      frame:'#141519', lens:'#b7c7ca', eye:'#b7c7ca',  // blank glare — nothing behind
      neck:'#191b1f',  neckSh:'#0d0f12',
      cov:'#49535e',   covSh:'#2f363f',
      strap:'#2b333b', buckle:'#7c6a34',
      glove:'#9a8c3e', gloveSh:'#6c6230',
      boot:'#2a251f',  bootSh:'#161209',
      metal:'#aeb4bd', metalSh:'#6e757f', handle:'#5c2626',
      void:'#080608',
    },
  };

  // ---- per-form armature ----
  const CFG = {
    home: {
      cx:16, headTop:4, faceTop:5, headBot:11, headL:12, headR:19,
      earY:8, neckTop:12, neckBot:13, shoulderY:13,
      torsoTop:14, torsoBot:23, bellyTop:18, bellyBot:22, torsoHalf:5, bellyHalf:5,
      armTop:14, armBot:22, hipY:24, legTop:25, legBot:29, footY:30,
      mouthY:11, stretch:0,
    },
    nightmare: {
      cx:16, headTop:1, faceTop:2, headBot:8, headL:13, headR:18,
      earY:5, neckTop:9, neckBot:14, shoulderY:14,
      torsoTop:15, torsoBot:24, bellyTop:19, bellyBot:22, torsoHalf:3, bellyHalf:3,
      armTop:15, armBot:27, hipY:25, legTop:26, legBot:31, footY:31,
      mouthY:6, stretch:1,
    },
  };

  const hx=(h)=>{h=h.replace('#','');return [parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];};
  function rnd(a,b,c){let h=(a*374761393+b*668265263+c*2246822519)>>>0;h=((h^(h>>>15))*2246822519)>>>0;return ((h^(h>>>13))>>>0)/4294967296;}

  function build(opts){
    opts = opts||{}; const form = opts.form||'home';
    const P = PAL[form], K = CFG[form];
    const W = CELL*FRAMES, H = CELL;
    const cv = window.__nbCanvas(W,H); const ctx = cv.getContext('2d');
    const O = ctx.createImageData(W,H); const OD = O.data;

    for(let f=0; f<FRAMES; f++){
      const B = new Uint8ClampedArray(CELL*CELL*4);
      const bi=(x,y)=>(y*CELL+x)*4;
      const set=(x,y,rgb,a)=>{ if(x<0||x>31||y<0||y>31) return; const i=bi(x,y); B[i]=rgb[0];B[i+1]=rgb[1];B[i+2]=rgb[2];B[i+3]=(a==null?255:a); };
      const put=(x,y,hex)=>set(x,y,hx(hex));
      const fill=(x0,x1,y0,y1,hex)=>{ const c=hx(hex); for(let y=y0;y<=y1;y++) for(let x=x0;x<=x1;x++) set(x,y,c); };
      const on=(x,y)=>{ if(x<0||x>31||y<0||y>31) return false; return B[bi(x,y)+3]>=128; };
      const keyAt=(x,y)=>{ if(!on(x,y)) return null; const i=bi(x,y); return B[i]+','+B[i+1]+','+B[i+2]; };

      // ---- frame motion ----
      const isWalk = f>=2 && f<=4;
      const isSpeak = f>=5;
      // idle: tiny 1px "held too long" breath on frame 1; walk: bob on contact frame
      let bob = 0, legL = 0, legR = 0, armSwing = 0;
      if(f===1) bob = 0;                       // idle stays eerily still
      if(isWalk){
        const ph = f-2;                        // 0,1,2
        if(ph===0){ legL=-1; legR=1; armSwing=1; }
        else if(ph===1){ bob=1; }              // passing/contact dips
        else { legL=1; legR=-1; armSwing=-1; }
      }
      const B0 = bob;                          // upper-body vertical offset

      // =========================== BODY ===========================
      const cx=K.cx;

      // -- legs / boots (with a readable front-walk stride) --
      const legTopY=K.legTop, legBotY=K.legBot, footY=K.footY;
      // per-leg foot lift: f2 left plants / right lifts, f4 mirrored, f3 both plant (body rises)
      let lLift=0, rLift=0;
      if(isWalk){ if(f===2){ rLift=1; } else if(f===4){ lLift=1; } }
      // left leg
      fill(cx-3, cx-1, legTopY, legBotY-1-lLift, P.cov);
      // right leg
      fill(cx+1, cx+3, legTopY, legBotY-1-rLift, P.cov);
      // leg shading (inner seam) + a knee crease on the lifted leg
      for(let y=legTopY;y<=legBotY-1;y++){ put(cx-1,y, P.covSh); put(cx+1,y,P.covSh); }
      // boots
      fill(cx-3, cx-1, footY-1-lLift, footY-lLift, P.boot);
      fill(cx+1, cx+3, footY-1-rLift, footY-rLift, P.boot);
      for(let x=cx-3;x<=cx-1;x++) put(x, footY-lLift, P.bootSh);
      for(let x=cx+1;x<=cx+3;x++) put(x, footY-rLift, P.bootSh);

      // -- torso: heavy-set coveralls (home) / thin (nightmare). belly bulges in the middle --
      for(let y=K.torsoTop; y<=K.torsoBot; y++){
        const yy = y + B0;
        const belly = (y>=K.bellyTop && y<=K.bellyBot);
        const half = belly ? K.bellyHalf : K.torsoHalf;
        fill(cx-half, cx+half, yy, yy, P.cov);
        put(cx-half, yy, P.covSh); put(cx+half, yy, P.covSh);   // side shade
      }
      // hips join
      fill(cx-K.torsoHalf, cx+K.torsoHalf, K.hipY+B0, K.hipY+B0, P.cov);

      // -- black turtleneck: collar + a wedge at the upper chest above the bib --
      fill(cx-2, cx+2, K.neckTop+B0, K.neckBot+B0, P.neck);
      put(cx-2, K.neckBot+B0, P.neckSh); put(cx+2, K.neckBot+B0, P.neckSh);
      // chest wedge (turtleneck showing above the coverall bib)
      for(let k=0;k<3;k++){ fill(cx-1-k+2, cx+1+k-2, K.shoulderY+1+k+B0, K.shoulderY+1+k+B0, P.neck); }
      put(cx, K.shoulderY+1+B0, P.neckSh);

      // -- coverall straps over the shoulders + brass buckles --
      const strapY0=K.shoulderY+B0, strapY1=K.bellyTop+B0;
      for(let y=strapY0; y<=strapY1; y++){ put(cx-3, y, P.strap); put(cx+3, y, P.strap); }
      put(cx-3, strapY0+2, P.buckle); put(cx+3, strapY0+2, P.buckle);

      // -- arms: coverall sleeves down the sides, gloved hands at the ends --
      const aTop=K.armTop+B0, aBot=K.armBot+B0;
      const swL = (isWalk? armSwing : 0), swR = (isWalk? -armSwing : 0);
      const axL = cx - (K.torsoHalf+1), axR = cx + (K.torsoHalf+1);
      // left arm
      for(let y=aTop; y<=aBot+swL; y++){ put(axL, y, P.cov); put(axL-1, y, P.covSh); }
      // right arm
      for(let y=aTop; y<=aBot+swR; y++){ put(axR, y, P.cov); put(axR+1, y, P.covSh); }
      // gloves (hands)
      const glLy=aBot+swL, glRy=aBot+swR;
      const fingLen = form==='nightmare' ? 3 : 1;   // nightmare = long thin fingers
      for(let k=0;k<=fingLen;k++){ put(axL-1, glLy+1+k, P.glove); put(axL, glLy+1+k, k? P.gloveSh:P.glove); }
      for(let k=0;k<=fingLen;k++){ put(axR, glRy+1+k, k? P.gloveSh:P.glove); put(axR+1, glRy+1+k, P.glove); }

      // -- hedge shears held in the right glove: two thin blades rising blade-up beside him --
      {
        const bx = axR+1;                       // blades ride just outside the right hand
        const tipY = (form==='nightmare'? 9 : 12) + B0;
        const baseY = glRy+1;
        for(let y=tipY; y<=baseY; y++){
          put(bx+1, y, P.metal); put(bx+2, y, y%3? P.metal : P.metalSh);
        }
        // the two blades split near the pivot
        put(bx+1, baseY-1, P.metalSh); put(bx+2, baseY-1, P.metal);
        put(bx+1, baseY, P.handle); put(bx+2, baseY, P.handle);   // handle/pivot at the hand
        put(bx+2, tipY, P.metalSh);                                // tip
      }

      // =========================== HEAD ===========================
      const hy = B0;                            // head follows body bob
      const hl=K.headL, hr=K.headR, htop=K.headTop+hy, hbot=K.headBot+hy;

      // skull / face fill (skin)
      for(let y=htop; y<=hbot; y++){
        let l=hl, r=hr;
        if(y===htop){ l+=2; r-=2; }             // round the crown (dome)
        else if(y===htop+1){ l+=1; r-=1; }
        if(y===hbot){ l+=1; r-=1; }             // round the chin
        fill(l, r, y, y, P.skin);
      }
      // balding dome: bare scalp on top rows, grey hair only on the sides + a fringe over the ears
      for(let y=K.faceTop+hy; y<=hbot; y++){
        // side hair
        if(y>=K.earY+hy-2){ put(hl, y, P.hair); put(hr, y, P.hair); put(hl+ (y%2?0:0), y, P.hair); }
      }
      // a thin grey fringe wrapping the back of the skull sides
      put(hl, K.earY+hy-2, P.hairSh); put(hr, K.earY+hy-2, P.hairSh);
      // ears
      put(hl-1, K.earY+hy, P.skin); put(hr+1, K.earY+hy, P.skin);
      put(hl-1, K.earY+hy+1, P.skinSh); put(hr+1, K.earY+hy+1, P.skinSh);

      // grey stubble tint on the jaw SIDES only (leave the mouth row clear so the smile reads)
      for(let y=hbot-2; y<=hbot; y++) for(let x=hl+1; x<=hr-1; x++){
        if(x>=cx-2 && x<=cx+2 && y>=K.mouthY+hy-1) continue;   // keep the mouth zone clean
        if(on(x,y) && rnd(f*7+x, y, form==='nightmare'?9:3) > 0.5) put(x,y, P.stub);
      }

      // ---- round wire glasses (the signature) ----
      const gy = form==='nightmare' ? htop+2 : htop+3;   // eye line
      const lcx = cx-2, rcx = cx+2;                       // two round lenses
      if(form!=='nightmare'){
        // lens fills
        put(lcx-1,gy,P.lens); put(lcx,gy,P.lens); put(rcx,gy,P.lens); put(rcx+1,gy,P.lens);
        put(lcx-1,gy+1,P.lens); put(lcx,gy+1,P.lens); put(rcx,gy+1,P.lens); put(rcx+1,gy+1,P.lens);
        // round frames
        put(lcx-1,gy-1,P.frame); put(lcx,gy-1,P.frame); put(rcx,gy-1,P.frame); put(rcx+1,gy-1,P.frame);
        put(lcx-2,gy,P.frame); put(lcx+1,gy,P.frame); put(rcx-1,gy,P.frame); put(rcx+2,gy,P.frame);
        put(lcx-2,gy+1,P.frame); put(lcx+1,gy+1,P.frame); put(rcx-1,gy+1,P.frame); put(rcx+2,gy+1,P.frame);
        put(lcx-1,gy+2,P.frame); put(lcx,gy+2,P.frame); put(rcx,gy+2,P.frame); put(rcx+1,gy+2,P.frame);
        // bridge + eyes behind
        put(cx-1,gy,P.frame); put(cx,gy,P.frame);
        put(lcx,gy,P.eye); put(rcx+1,gy,P.eye);            // pupils
      } else {
        // nightmare: blank glare discs, cracked frame — no eyes
        put(lcx-1,gy,P.lens); put(lcx,gy,P.lens); put(rcx,gy,P.lens); put(rcx+1,gy,P.lens);
        put(lcx-1,gy+1,P.lens); put(lcx,gy+1,P.lens); put(rcx,gy+1,P.lens); put(rcx+1,gy+1,P.lens);
        put(cx-1,gy,P.frame); put(cx,gy,P.frame);
        put(lcx-2,gy,P.frame); put(rcx+2,gy,P.frame);
        put(lcx,gy-1,P.frame); put(rcx+1,gy-1,P.frame);
      }
      // nose
      put(cx, gy+2, P.skinSh); put(cx, gy+3, P.skinSh);

      // ---- mouth: normal = a wide smile held a beat too long; speak = flap; nightmare speak = huge gape ----
      const my = K.mouthY + hy;
      const wide = (f===1) ? 1 : 0;             // idle-B: the smile stretches 1px wider — held too long
      if(form!=='nightmare'){
        if(f===6){
          // speak open: a dark oval, corners still hooked up
          fill(cx-1, cx+1, my, my+1, P.void);
          put(cx-2,my-1,P.skinSh); put(cx+2,my-1,P.skinSh);   // upturned corners
        } else {
          // a wide smile held a beat too long: flat center, corners hooked UP
          fill(cx-1-wide, cx+1+wide, my, my, P.void);
          put(cx-2-wide, my-1, P.void); put(cx+2+wide, my-1, P.void);   // corners rise = the grin
          put(cx-3-wide, my-1, P.skinSh); put(cx+3+wide, my-1, P.skinSh);
        }
      } else {
        if(f===6){
          // NIGHTMARE speak: the jaw unhinges — a black void gapes down the throat
          fill(cx-1, cx+1, my, my+7, P.void);
          put(cx-2,my,P.void); put(cx+2,my,P.void);
          put(cx-2,my+1,P.void); put(cx+2,my+1,P.void);
        } else {
          // even at rest: a too-wide rictus
          fill(cx-2, cx+2, my, my, P.void);
          put(cx-1,my+1,P.void); put(cx,my+1,P.void); put(cx+1,my+1,P.void);
        }
      }

      // =========================== OUTLINE PASS ===========================
      // wrap the silhouette in a dark outline (player-matching flat look)
      const outline = hx(P.out);
      const snapshot = B.slice();
      const wasOn=(x,y)=>{ if(x<0||x>31||y<0||y>31) return false; return snapshot[(y*CELL+x)*4+3]>=128; };
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){
        if(wasOn(x,y)) continue;
        if(wasOn(x-1,y)||wasOn(x+1,y)||wasOn(x,y-1)||wasOn(x,y+1)) set(x,y,outline);
      }

      // ---- blit frame -> sheet ----
      const ox=f*CELL;
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){
        const i=bi(x,y); if(B[i+3]<128) continue;
        const oi=((y)*W+(ox+x))*4;
        OD[oi]=B[i];OD[oi+1]=B[i+1];OD[oi+2]=B[i+2];OD[oi+3]=255;
      }
    }

    ctx.putImageData(O,0,0);
    const name = form==='nightmare' ? 'neighbor_robert_front_nightmare.png' : 'neighbor_robert_front.png';
    // NOTE: caller saves; return the canvas + name so build can save via helpers
    return { name, canvas: cv, W, H };
  }

  async function buildAndSave(opts){
    const r = build(opts);
    await window.__nbSave(r.name, r.canvas);
    const up=window.__nbCanvas(r.W*8, r.H*8); const ux=up.getContext('2d'); ux.imageSmoothingEnabled=false; ux.drawImage(r.canvas,0,0,r.W*8,r.H*8);
    await window.__nbSave(r.name.replace('.png','-8x.png'), up);
    return r.name;
  }

  return { build: buildAndSave, PAL, CFG };
})();
