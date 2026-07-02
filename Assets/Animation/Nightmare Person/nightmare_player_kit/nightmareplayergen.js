// Nightmare-player generator  —  "it's wearing you".
// A doppelganger of the player sprite: same red shirt, same jeans, same skin —
// so it reads as YOU first, then wrong. Recolour drains the palette toward waxy
// death (identity kept), then a per-frame grotesque pass bakes: eye-sockets torn
// too wide with sunken blackened rings pulling the face open, a big HOLLOW void
// mouth, a burst belly with very pronounced intestines spilling + dragging, and a
// protruding twisted spine. A contortion WARP then wrenches the whole thing.
// Frames are otherwise the 5-frame player layout (idle 0-1, walk 2-4) so it drops
// straight into the existing animation system; the horror is the timing (playback
// the game drives) + a scale-punch "flash" reveal.
//
//   eval(await readFile('nightmareplayergen.js'));
//   window.__npReadImage = readImage; window.__npSave = saveFile; window.__npCanvas = createCanvas;
//   await window.NightmarePlayer.build('character_sprite_sheet.png', {dir:'wrung', tier:'you'});
//
// dir : 'wrung'  (twisted / broken — head cocked hard, spine wrenched out one side)
//     | 'strung' (marionette — head lolled, body thinned/hung, puppet strings)
// tier: 'base'   (doppelganger, not-quite-right — restrained, uncanny)
//     | 'you'    ("it's wearing YOU" — gore turned up, the jump-scare)

window.NightmarePlayer = (function(){

  // ---- source palette (the healthy player master) ----
  const SRC = {
    skinB:'240,184,144', skinS:'192,122,76',
    hairB:'156,90,38',   hairS:'94,52,16',
    shirtB:'216,48,48',  shirtS:'152,32,24',
    pantsB:'58,91,208',  pantsS:'38,57,140',
    feet:'74,74,74',     out:'0,0,0',
  };

  // ---- nightmare palette per tier (kept RECOGNISABLE — still red shirt / blue jeans / skin) ----
  const PAL = {
    base: {
      skinB:'#c8ad93', skinS:'#977a60',   // waxy, drained, cool
      hairB:'#6a4626', hairS:'#3a2612',   // greasy, matted
      shirtB:'#a8302a', shirtS:'#651a15',  // dirtied red — still clearly their shirt
      pantsB:'#34497e', pantsS:'#222f52',  // grimy blue
      feet:'#333336',   out:'#0b0a0a',
    },
    you: {
      skinB:'#c0b096', skinS:'#897c62',   // drained + waxy, but light enough that the black orbits + grin read
      hairB:'#4f3620', hairS:'#271a0e',
      shirtB:'#8c2820', shirtS:'#4e1310',  // blood-darkened red
      pantsB:'#2b3b66', pantsS:'#1a2542',
      feet:'#2a2a2c',   out:'#0a0909',
    },
  };

  // gore accent colours
  const C = {
    sock:'#080607', ring:'#3a2630', ring2:'#4a3238', tear:'#43201a', blood:'#5a1512', weep:'#2a0f0c',
    mouthVoid:'#0a0708', mouthRim:'#2a191a', mouthLip:'#3a2622', toothDim:'#9a8d74',
    cavity:'#180a0a', woundRim:'#3a1210', tornRed:'#c0342a', tornRed2:'#e0584a',
    gutDark:'#3a1414', gutMid:'#6e2b28', gutHi:'#a2453f', gutWet:'#c88079', gutSh:'#25100f',
    boneHi:'#d6cbb0', bone:'#ab9c80', boneSh:'#6d6250', boneGap:'#130e0c',
    string:'#242430', stringHi:'#54545e',
  };

  const hx=(h)=>{h=h.replace('#','');return [parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];};
  const rgb=(s)=>s.split(',').map(Number);

  function buildMap(tier){
    const p = PAL[tier]; const m = {};
    m[SRC.skinB]=hx(p.skinB);  m[SRC.skinS]=hx(p.skinS);
    m[SRC.hairB]=hx(p.hairB);  m[SRC.hairS]=hx(p.hairS);
    m[SRC.shirtB]=hx(p.shirtB);m[SRC.shirtS]=hx(p.shirtS);
    m[SRC.pantsB]=hx(p.pantsB);m[SRC.pantsS]=hx(p.pantsS);
    m[SRC.feet]=hx(p.feet);    m[SRC.out]=hx(p.out);
    return m;
  }

  // deterministic hash 0..1 (stable gore across rebuilds)
  function rnd(a,b,c){let h=(a*374761393+b*668265263+c*2246822519)>>>0;h=((h^(h>>>15))*2246822519)>>>0;return ((h^(h>>>13))>>>0)/4294967296;}

  async function build(srcFile, opts){
    opts = opts||{}; const dir = opts.dir||'wrung', tier = opts.tier||'you';
    const isYou = tier==='you';
    const img = await window.__npReadImage(srcFile);
    const W=img.width, H=img.height;
    const src = window.__npCanvas(W,H); const sctx=src.getContext('2d'); sctx.drawImage(img,0,0);
    const S = sctx.getImageData(0,0,W,H).data;             // source pixels (read-only)
    const map = buildMap(tier);
    const skinKeys = new Set([SRC.skinB,SRC.skinS]);
    const nFrames = Math.floor(W/32);

    const out = window.__npCanvas(W,H); const octx=out.getContext('2d');
    const O = octx.createImageData(W,H); const OD=O.data;   // output pixels

    const sidx=(x,y)=>(y*W+x)*4;

    for(let f=0; f<nFrames; f++){
      const ox=f*32;
      // ---- per-frame working buffer (32x32 RGBA), start empty ----
      const B = new Uint8ClampedArray(32*32*4);
      const bi=(x,y)=>(y*32+x)*4;
      const setB=(x,y,rgbArr,a)=>{ if(x<0||x>31||y<0||y>31) return; const i=bi(x,y); B[i]=rgbArr[0];B[i+1]=rgbArr[1];B[i+2]=rgbArr[2];B[i+3]=(a==null?255:a); };
      const putB=(x,y,hex)=>setB(x,y,hx(hex));
      const getKeyB=(x,y)=>{ if(x<0||x>31||y<0||y>31) return null; const i=bi(x,y); if(B[i+3]<128) return null; return B[i]+','+B[i+1]+','+B[i+2]; };
      const opaqueB=(x,y)=>{ if(x<0||x>31||y<0||y>31) return false; return B[bi(x,y)+3]>=128; };

      // ---- 1. recolour source frame into B ----
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){
        const i=sidx(ox+x,y); if(S[i+3]<128) continue;
        const k=S[i]+','+S[i+1]+','+S[i+2];
        const rep=map[k]||[S[i],S[i+1],S[i+2]];
        setB(x,y,rep,255);
      }

      // ---- detect eyes (black flanked by skin) from SOURCE ----
      const eyePx=[];
      for(let y=0;y<32;y++) for(let x=1;x<31;x++){
        const i=sidx(ox+x,y); if(S[i+3]<128) continue;
        if(!(S[i]===0&&S[i+1]===0&&S[i+2]===0)) continue;
        const l=sidx(ox+x-1,y), r=sidx(ox+x+1,y);
        const kl=S[l]+','+S[l+1]+','+S[l+2], kr=S[r]+','+S[r+1]+','+S[r+2];
        if(S[l+3]>128&&skinKeys.has(kl)&&S[r+3]>128&&skinKeys.has(kr)) eyePx.push([x,y]);
      }
      // cluster into eyes
      const clusters=[];
      for(const [ex,ey] of eyePx){
        let cl=clusters.find(c=>Math.abs(c.cx-ex)<=2&&Math.abs(c.cy-ey)<=2);
        if(!cl){cl={xs:[],ys:[]};clusters.push(cl);}
        cl.xs.push(ex);cl.ys.push(ey);
        cl.cx=Math.round(cl.xs.reduce((a,b)=>a+b,0)/cl.xs.length);
        cl.cy=Math.round(cl.ys.reduce((a,b)=>a+b,0)/cl.ys.length);
      }
      clusters.sort((a,b)=>a.cx-b.cx);
      const eyeY = clusters.length?Math.round(clusters.reduce((a,c)=>a+c.cy,0)/clusters.length):7;
      const midX = clusters.length?Math.round(clusters.reduce((a,c)=>a+c.cx,0)/clusters.length):16;
      const phase = (f/nFrames)*Math.PI*2;

      // ================= 2. GORE PASS (on B, unwarped) =================

      // -- 2a-pre. pale face MASK + lower MUZZLE: a bone-pale field so the black orbits and the long grin
      //    read as holes cut into skin (the Billy-mask contrast), not a dark smudge --
      {
        const skB = map[SRC.skinB].join(','), skS = map[SRC.skinS].join(','), outK = map[SRC.out].join(',');
        const isSkinB  =(x,y)=>{ const k=getKeyB(x,y); return k===skB||k===skS; };
        const isSkinJaw=(x,y)=>{ const k=getKeyB(x,y); return k===skB||k===skS||k===outK; };
        const skinHi = hx(isYou?'#dcceb5':'#d3ba9f');
        const mask=[];
        for(let dx=-3;dx<=3;dx++){ mask.push([midX+dx,eyeY-2],[midX+dx,eyeY-1]); }   // forehead + brow ridge
        mask.push([midX-1,eyeY],[midX,eyeY],[midX-1,eyeY+1],[midX,eyeY+1]);            // bright bridge BETWEEN the orbits
        for(const [x,y] of mask){ if(isSkinB(x,y)) setB(x,y,skinHi); }
        // pale muzzle under the orbits (rows eyeY+3..+5) so the long toothless grin sits IN skin, not shadow
        const mw = isYou?5:4;
        for(let y=eyeY+3;y<=eyeY+5;y++) for(let dx=-mw;dx<=mw;dx++){ if(isSkinJaw(midX+dx,y)) setB(midX+dx,y,skinHi); }
      }

      // -- 2a. eyes: two DISTINCT gaping orbits + a subtle sunken ring right under each (muzzle stays pale) --
      for(const cl of clusters){
        const ex=cl.cx, ey=cl.cy;
        const outer = ex<midX ? -1 : 1;   // orbits tear toward the temples, never across the bridge
        const hole = isYou
          ? [[0,0],[outer,0],[outer*2,0],[0,1],[outer,1]]   // torn wide toward the temple
          : [[0,0],[0,1]];                                  // sunken, dead
        for(const [dx,dy] of hole){ if(opaqueB(ex+dx,ey+dy)) putB(ex+dx,ey+dy, C.sock); }
        putB(ex,ey+2,C.ring);                                // one bruise pixel directly beneath — no dark band
        if(isYou) putB(ex+outer*2,ey,C.tear);                // a single torn nick at the temple
      }

      // -- 2b. the smile: an EXTRA-LONG, lipless, TOOTHLESS grin — a huge upturned curve stretched ear-to-ear,
      //    hollow, no teeth, corners hooked up. the core of the uncanny read --
      {
        // curve offset from eyeY per dx: ends ride HIGH (eyeY+3), sweeping LOW to the centre (eyeY+5) => upturned
        const off = isYou
          ? { '-5':3,'-4':3,'-3':4,'-2':4,'-1':5,'0':5,'1':5,'2':4,'3':4,'4':3,'5':3 }
          : { '-4':3,'-3':4,'-2':4,'-1':5,'0':5,'1':5,'2':4,'3':4,'4':3 };
        const span = isYou?5:4;
        for(let dx=-span;dx<=span;dx++){
          const y0 = eyeY + off[String(dx)];
          if(opaqueB(midX+dx,y0)) putB(midX+dx,y0, C.mouthVoid);
          if(isYou && opaqueB(midX+dx,y0+1)) putB(midX+dx,y0+1, C.mouthVoid);   // 2px thick = a big open grin
        }
        if(isYou){ putB(midX-span,eyeY+2,C.mouthRim); putB(midX+span,eyeY+2,C.mouthRim); }  // tips hooked up into the cheeks
      }

      // -- 2c. protruding twisted spine --
      if(dir==='wrung'){
        // wrenched out the LEFT side (head cocks right) — knobby vertebrae punched through
        const col = 10, verts = isYou?[15,17,19,21]:[16,19];
        for(const vy of verts){
          putB(col-1,vy,C.boneGap); putB(col-2,vy,C.boneGap);   // dark socket it juts from
          putB(col,vy,C.bone); putB(col-1,vy,C.boneHi);
          putB(col,vy+1,C.boneSh);
          if(isYou){ putB(col-2,vy,C.bone); putB(col-2,vy-1,C.boneHi); }
        }
        if(isYou){ // a longer stretch of exposed backbone curving down
          putB(9,13,C.boneHi); putB(10,13,C.bone); putB(9,23,C.bone); putB(10,23,C.boneSh);
        }
      } else {
        // strung: the stretched throat splits, a couple neck vertebrae showing through
        putB(midX,12,C.boneHi); putB(midX,13,C.bone); putB(midX,14,C.boneSh);
        if(isYou){ putB(midX-1,13,C.boneGap); putB(midX+1,13,C.boneGap); putB(midX,15,C.bone); }
      }

      // -- 2d. burst belly + very pronounced spilling / dragging intestines --
      {
        const wx = midX;                         // wound centred on the belly
        const wyTop = isYou ? 18 : 20;
        const wyBot = 22;
        // open the cavity in the shirt, leaving a ragged torn-red rim
        for(let y=wyTop;y<=wyBot;y++) for(let x=wx-3;x<=wx+3;x++){
          if(!opaqueB(x,y)) continue;
          const edge = (x<=wx-3||x>=wx+3||y===wyTop);
          putB(x,y, edge ? (rnd(f,x,y)<0.5?C.tornRed:C.woundRim) : C.cavity);
        }
        if(isYou){ putB(wx-3,wyTop,C.tornRed2); putB(wx+3,wyTop+1,C.tornRed2); }

        // the spill: a tangle of loops cascading out and down, dragging to one side
        const dragSide = dir==='wrung' ? 1 : 0;      // wrung: drags toward the lean; strung: hangs straight
        const n = isYou?6:3;
        const amp = isYou?2:1;
        const drawLoop=(cx,cy,wet)=>{
          putB(cx-1,cy-1,C.gutMid); putB(cx,cy-1,C.gutHi); putB(cx+1,cy-1,C.gutMid);
          putB(cx-1,cy,  C.gutMid); putB(cx,cy,  C.gutDark);putB(cx+1,cy,  C.gutMid);
          putB(cx-1,cy+1,C.gutDark);putB(cx,cy+1,C.gutMid); putB(cx+1,cy+1,C.gutSh);
          if(wet) putB(cx,cy-1,C.gutWet);
        };
        let lastX=wx, lastY=wyBot;
        for(let i=0;i<n;i++){
          const t=i;
          const sway = Math.round(Math.sin(t*0.95 + phase)*amp);
          const cx = wx + sway + Math.round(dragSide * t*0.7);
          const cy = wyBot + 1 + t*2;
          drawLoop(cx, cy, i%2===0);
          lastX=cx; lastY=cy;
          if(isYou && rnd(f,cx,cy)>0.55) putB(cx, cy+2, C.blood);  // weight-drips
        }
        // dragging tail pooling along the ground
        if(isYou){
          const tx = dragSide? 4 : 0;
          for(let k=1;k<=4;k++){ putB(lastX+ (dragSide?k:(k%2?-1:1)*Math.ceil(k/2)), Math.min(31,lastY+1+ (dragSide?0:0)), k<3?C.gutMid:C.gutDark); }
          putB(lastX+ (dragSide?5:0), Math.min(31,lastY+1), C.gutSh);
        } else {
          putB(lastX, Math.min(31,lastY+2), C.gutDark);
        }
      }

      // ================= 3. CONTORTION WARP (inverse-sample B -> Bw) =================
      const Bw = new Uint8ClampedArray(32*32*4);
      const sample=(sx,sy)=>{ const rx=Math.round(sx), ry=Math.round(sy); if(rx<0||rx>31||ry<0||ry>31) return null; const i=bi(rx,ry); if(B[i+3]<128) return null; return [B[i],B[i+1],B[i+2]]; };
      const HEAD_MAX = 12;
      let ang, pivX=16, pivY=12, headDY=0, squash=1;
      if(dir==='wrung'){ ang = 0.15; pivX=16; pivY=13; headDY=0; squash=1; }   // head cocked to the shoulder, head kept intact
      else            { ang = -0.12; pivX=16; pivY=13; headDY=1; squash=1.10; } // head lolls, body thinned (strung)
      const ca=Math.cos(ang), sa=Math.sin(ang);
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){
        let sx, sy;
        if(y<=HEAD_MAX){
          // rotate head band about the neck pivot
          const dx=x-pivX, dy=y-pivY;
          sx = pivX + dx*ca + dy*sa;
          sy = pivY - dx*sa + dy*ca - headDY;
        } else if(dir==='wrung'){
          // torso counter-twist: nudge upper torso opposite the head
          sx = x + (y<=21 ? (y-12)*0.14 : 0);
          sy = y;
        } else {
          // strung: thin the whole body (sample wider -> renders narrower/taller-looking)
          sx = 16 + (x-16)*squash;
          sy = y;
        }
        const c=sample(sx,sy);
        if(c){ const i=bi(x,y); Bw[i]=c[0];Bw[i+1]=c[1];Bw[i+2]=c[2];Bw[i+3]=255; }
      }

      // ---- 4. post-warp overlay: marionette strings (strung only) ----
      if(dir==='strung'){
        const setW=(x,y,hex)=>{ if(x<0||x>31||y<0||y>31) return; const c=hx(hex); const i=bi(x,y); Bw[i]=c[0];Bw[i+1]=c[1];Bw[i+2]=c[2];Bw[i+3]=255; };
        const jitter=(x)=> x + (rnd(f,x,0)<0.5?0:0);
        // strings from the top of the cell down to crown + both hands, dashed = thin thread
        const anchors = isYou ? [[midX,1,7],[10,0,20],[21,0,20]] : [[midX,1,6],[11,0,19],[20,0,19]];
        for(const [axx,ay0,ay1] of anchors){
          for(let y=ay0;y<=ay1;y++){
            if(y%2===0) continue;                 // dashed
            const wob = Math.round(Math.sin(y*0.6+phase)*0.4);
            setW(jitter(axx)+wob, y, (y%4===1)?C.stringHi:C.string);
          }
        }
      }

      // ---- 5. blit warped frame into the output sheet ----
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){
        const bi_=bi(x,y); if(Bw[bi_+3]<128) continue;
        const oi=((y)*W+(ox+x))*4;
        OD[oi]=Bw[bi_];OD[oi+1]=Bw[bi_+1];OD[oi+2]=Bw[bi_+2];OD[oi+3]=255;
      }
    }

    octx.putImageData(O,0,0);
    const base = srcFile.replace('.png','');
    const outName = `${base}_nm_${dir}_${tier}.png`;
    await window.__npSave(outName, out);
    // 8x preview blow-up (do not ship)
    const up=window.__npCanvas(W*8,H*8); const ux=up.getContext('2d'); ux.imageSmoothingEnabled=false; ux.drawImage(out,0,0,W*8,H*8);
    await window.__npSave(outName.replace('.png','-8x.png'), up);
    return outName;
  }

  return { build };
})();
