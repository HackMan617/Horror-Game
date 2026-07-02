// Neighbor generator — "Robert Abernathy", the worn-out-Steve-Jobs technology lunatic.
// A heavy-set middle-aged man: balding grey, round wire glasses, black turtleneck under
// grubby work coveralls, yellow rubber gloves, hedge shears in hand. Friendly, brilliant,
// and VISIBLY off — stiff, a smile held a beat too long.
//
// Authors 32-px character sheets matching the player grammar, extended with a speak pair:
//   idle 0-1 · walk 2-4 · speak 5-6   (7 frames -> 224x32)
// in three views (front / back / side) × two forms:
//   'home'      — the daytime neighbor (uncanny but human)
//   'nightmare' — DIFFERENT creepy than the player: no gore, he STRETCHES. Too tall, too thin,
//                 long neck + limbs, glasses gone to blank glare, and in the speak frames the
//                 mouth opens WAY too wide (front/side: the jaw unhinges into a black void).
// The SIDE sheet faces right; mirror it (flipX) for the left-facing walk, same as the player.
//
// Palette is region-keyed (recolour-compatible) so the engine can swap garment/skin colours —
// or roll a fresh random assortment every load (see NEIGHBOR_ROBERT.md).
//
//   eval(await readFile('neighborgen.js'));
//   window.__nbReadImage=readImage; window.__nbSave=saveFile; window.__nbCanvas=createCanvas;
//   await window.Neighbor.build({view:'front', form:'home'});   // -> neighbor_robert_front.png
//   await window.Neighbor.build({view:'back',  form:'nightmare'});
//   await window.Neighbor.build({view:'side',  form:'home'});

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

  // ---- per-form armature (vertical layout shared by all views; widths used by front/back) ----
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

  function motionFor(f){
    const isWalk=f>=2&&f<=4, isSpeak=f>=5;
    let bob=0, armSwing=0, lLift=0, rLift=0;
    if(isWalk){ const ph=f-2; if(ph===0){armSwing=1; rLift=1;} else if(ph===1){bob=1;} else {armSwing=-1; lLift=1;} }
    return {isWalk,isSpeak,bob,B0:bob,armSwing,lLift,rLift};
  }

  // ============================ FRONT (faces the camera) ============================
  function paintFront(X){
    const {set,put,fill,on,cx,P,K,form,m,f}=X; const {B0,isWalk,armSwing,lLift,rLift}=m;

    // legs / boots
    const legTopY=K.legTop, legBotY=K.legBot, footY=K.footY;
    fill(cx-3, cx-1, legTopY, legBotY-1-lLift, P.cov);
    fill(cx+1, cx+3, legTopY, legBotY-1-rLift, P.cov);
    for(let y=legTopY;y<=legBotY-1;y++){ put(cx-1,y, P.covSh); put(cx+1,y,P.covSh); }
    fill(cx-3, cx-1, footY-1-lLift, footY-lLift, P.boot);
    fill(cx+1, cx+3, footY-1-rLift, footY-rLift, P.boot);
    for(let x=cx-3;x<=cx-1;x++) put(x, footY-lLift, P.bootSh);
    for(let x=cx+1;x<=cx+3;x++) put(x, footY-rLift, P.bootSh);

    // torso (heavy-set; belly bulges)
    for(let y=K.torsoTop; y<=K.torsoBot; y++){
      const yy=y+B0; const belly=(y>=K.bellyTop&&y<=K.bellyBot); const half=belly?K.bellyHalf:K.torsoHalf;
      fill(cx-half, cx+half, yy, yy, P.cov); put(cx-half,yy,P.covSh); put(cx+half,yy,P.covSh);
    }
    fill(cx-K.torsoHalf, cx+K.torsoHalf, K.hipY+B0, K.hipY+B0, P.cov);

    // turtleneck collar + chest wedge
    fill(cx-2, cx+2, K.neckTop+B0, K.neckBot+B0, P.neck);
    put(cx-2,K.neckBot+B0,P.neckSh); put(cx+2,K.neckBot+B0,P.neckSh);
    for(let k=0;k<3;k++){ fill(cx-1-k+2, cx+1+k-2, K.shoulderY+1+k+B0, K.shoulderY+1+k+B0, P.neck); }
    put(cx,K.shoulderY+1+B0,P.neckSh);

    // straps + buckles
    const strapY0=K.shoulderY+B0, strapY1=K.bellyTop+B0;
    for(let y=strapY0;y<=strapY1;y++){ put(cx-3,y,P.strap); put(cx+3,y,P.strap); }
    put(cx-3,strapY0+2,P.buckle); put(cx+3,strapY0+2,P.buckle);

    // arms + gloves
    const aTop=K.armTop+B0, aBot=K.armBot+B0;
    const swL=(isWalk?armSwing:0), swR=(isWalk?-armSwing:0);
    const axL=cx-(K.torsoHalf+1), axR=cx+(K.torsoHalf+1);
    for(let y=aTop;y<=aBot+swL;y++){ put(axL,y,P.cov); put(axL-1,y,P.covSh); }
    for(let y=aTop;y<=aBot+swR;y++){ put(axR,y,P.cov); put(axR+1,y,P.covSh); }
    const glLy=aBot+swL, glRy=aBot+swR; const fingLen=form==='nightmare'?3:1;
    for(let k=0;k<=fingLen;k++){ put(axL-1,glLy+1+k,P.glove); put(axL,glLy+1+k,k?P.gloveSh:P.glove); }
    for(let k=0;k<=fingLen;k++){ put(axR,glRy+1+k,k?P.gloveSh:P.glove); put(axR+1,glRy+1+k,P.glove); }

    // hedge shears in the right glove
    { const bx=axR+1, tipY=(form==='nightmare'?9:12)+B0, baseY=glRy+1;
      for(let y=tipY;y<=baseY;y++){ put(bx+1,y,P.metal); put(bx+2,y,y%3?P.metal:P.metalSh); }
      put(bx+1,baseY-1,P.metalSh); put(bx+2,baseY-1,P.metal);
      put(bx+1,baseY,P.handle); put(bx+2,baseY,P.handle); put(bx+2,tipY,P.metalSh); }

    // head
    const hy=B0, hl=K.headL, hr=K.headR, htop=K.headTop+hy, hbot=K.headBot+hy;
    for(let y=htop;y<=hbot;y++){ let l=hl,r=hr; if(y===htop){l+=2;r-=2;} else if(y===htop+1){l+=1;r-=1;} if(y===hbot){l+=1;r-=1;} fill(l,r,y,y,P.skin); }
    for(let y=K.faceTop+hy;y<=hbot;y++){ if(y>=K.earY+hy-2){ put(hl,y,P.hair); put(hr,y,P.hair); } }
    put(hl,K.earY+hy-2,P.hairSh); put(hr,K.earY+hy-2,P.hairSh);
    put(hl-1,K.earY+hy,P.skin); put(hr+1,K.earY+hy,P.skin); put(hl-1,K.earY+hy+1,P.skinSh); put(hr+1,K.earY+hy+1,P.skinSh);
    for(let y=hbot-2;y<=hbot;y++) for(let x=hl+1;x<=hr-1;x++){ if(x>=cx-2&&x<=cx+2&&y>=K.mouthY+hy-1) continue; if(on(x,y)&&rnd(f*7+x,y,form==='nightmare'?9:3)>0.5) put(x,y,P.stub); }

    // glasses
    const gy=form==='nightmare'?htop+2:htop+3, lcx=cx-2, rcx=cx+2;
    if(form!=='nightmare'){
      put(lcx-1,gy,P.lens); put(lcx,gy,P.lens); put(rcx,gy,P.lens); put(rcx+1,gy,P.lens);
      put(lcx-1,gy+1,P.lens); put(lcx,gy+1,P.lens); put(rcx,gy+1,P.lens); put(rcx+1,gy+1,P.lens);
      put(lcx-1,gy-1,P.frame); put(lcx,gy-1,P.frame); put(rcx,gy-1,P.frame); put(rcx+1,gy-1,P.frame);
      put(lcx-2,gy,P.frame); put(lcx+1,gy,P.frame); put(rcx-1,gy,P.frame); put(rcx+2,gy,P.frame);
      put(lcx-2,gy+1,P.frame); put(lcx+1,gy+1,P.frame); put(rcx-1,gy+1,P.frame); put(rcx+2,gy+1,P.frame);
      put(lcx-1,gy+2,P.frame); put(lcx,gy+2,P.frame); put(rcx,gy+2,P.frame); put(rcx+1,gy+2,P.frame);
      put(cx-1,gy,P.frame); put(cx,gy,P.frame); put(lcx,gy,P.eye); put(rcx+1,gy,P.eye);
    } else {
      put(lcx-1,gy,P.lens); put(lcx,gy,P.lens); put(rcx,gy,P.lens); put(rcx+1,gy,P.lens);
      put(lcx-1,gy+1,P.lens); put(lcx,gy+1,P.lens); put(rcx,gy+1,P.lens); put(rcx+1,gy+1,P.lens);
      put(cx-1,gy,P.frame); put(cx,gy,P.frame); put(lcx-2,gy,P.frame); put(rcx+2,gy,P.frame);
      put(lcx,gy-1,P.frame); put(rcx+1,gy-1,P.frame);
    }
    put(cx,gy+2,P.skinSh); put(cx,gy+3,P.skinSh);

    // mouth
    const my=K.mouthY+hy, wide=(f===1)?1:0;
    if(form!=='nightmare'){
      if(f===6){ fill(cx-1,cx+1,my,my+1,P.void); put(cx-2,my-1,P.skinSh); put(cx+2,my-1,P.skinSh); }
      else { fill(cx-1-wide,cx+1+wide,my,my,P.void); put(cx-2-wide,my-1,P.void); put(cx+2+wide,my-1,P.void); put(cx-3-wide,my-1,P.skinSh); put(cx+3+wide,my-1,P.skinSh); }
    } else {
      if(f===6){ fill(cx-1,cx+1,my,my+7,P.void); put(cx-2,my,P.void); put(cx+2,my,P.void); put(cx-2,my+1,P.void); put(cx+2,my+1,P.void); }
      else { fill(cx-2,cx+2,my,my,P.void); put(cx-1,my+1,P.void); put(cx,my+1,P.void); put(cx+1,my+1,P.void); }
    }
  }

  // ============================ BACK (faces away) ============================
  function paintBack(X){
    const {set,put,fill,on,cx,P,K,form,m,f}=X; const {B0,isWalk,armSwing,lLift,rLift}=m;

    // legs / boots (heels)
    const legTopY=K.legTop, legBotY=K.legBot, footY=K.footY;
    fill(cx-3, cx-1, legTopY, legBotY-1-lLift, P.cov);
    fill(cx+1, cx+3, legTopY, legBotY-1-rLift, P.cov);
    for(let y=legTopY;y<=legBotY-1;y++){ put(cx-1,y, P.covSh); put(cx+1,y,P.covSh); }
    fill(cx-3, cx-1, footY-1-lLift, footY-lLift, P.boot);
    fill(cx+1, cx+3, footY-1-rLift, footY-rLift, P.boot);
    for(let x=cx-3;x<=cx-1;x++) put(x, footY-lLift, P.bootSh);
    for(let x=cx+1;x<=cx+3;x++) put(x, footY-rLift, P.bootSh);

    // torso (back of the coveralls)
    for(let y=K.torsoTop; y<=K.torsoBot; y++){
      const yy=y+B0; const belly=(y>=K.bellyTop&&y<=K.bellyBot); const half=belly?K.bellyHalf:K.torsoHalf;
      fill(cx-half, cx+half, yy, yy, P.cov); put(cx-half,yy,P.covSh); put(cx+half,yy,P.covSh);
    }
    fill(cx-K.torsoHalf, cx+K.torsoHalf, K.hipY+B0, K.hipY+B0, P.cov);

    // turtleneck collar at the nape
    fill(cx-2, cx+2, K.neckTop+B0, K.neckBot+B0, P.neck);
    put(cx-2,K.neckBot+B0,P.neckSh); put(cx+2,K.neckBot+B0,P.neckSh);

    // overall CROSSBACK — two straps crossing in an X (distinctive back read)
    const sy0=K.shoulderY+B0, sy1=K.bellyTop+B0, steps=Math.max(1,sy1-sy0);
    for(let i=0;i<=steps;i++){ const t=i/steps;
      put(Math.round((cx-3)+6*t), sy0+i, P.strap);   // L shoulder -> R hip
      put(Math.round((cx+3)-6*t), sy0+i, P.strap);   // R shoulder -> L hip
    }
    put(cx-3,sy0,P.buckle); put(cx+3,sy0,P.buckle);
    // a back-panel line where the straps meet
    fill(cx-2,cx+2,sy1,sy1,P.strap);

    // arms + gloves
    const aTop=K.armTop+B0, aBot=K.armBot+B0;
    const swL=(isWalk?armSwing:0), swR=(isWalk?-armSwing:0);
    const axL=cx-(K.torsoHalf+1), axR=cx+(K.torsoHalf+1);
    for(let y=aTop;y<=aBot+swL;y++){ put(axL,y,P.cov); put(axL-1,y,P.covSh); }
    for(let y=aTop;y<=aBot+swR;y++){ put(axR,y,P.cov); put(axR+1,y,P.covSh); }
    const glLy=aBot+swL, glRy=aBot+swR; const fingLen=form==='nightmare'?3:1;
    for(let k=0;k<=fingLen;k++){ put(axL-1,glLy+1+k,P.glove); put(axL,glLy+1+k,k?P.gloveSh:P.glove); }
    for(let k=0;k<=fingLen;k++){ put(axR,glRy+1+k,k?P.gloveSh:P.glove); put(axR+1,glRy+1+k,P.glove); }

    // shears carried on his RIGHT hand (viewer's LEFT from behind)
    { const bx=axL-1, tipY=(form==='nightmare'?9:12)+B0, baseY=glLy+1;
      for(let y=tipY;y<=baseY;y++){ put(bx-1,y,P.metal); put(bx-2,y,y%3?P.metal:P.metalSh); }
      put(bx-1,baseY-1,P.metalSh); put(bx-2,baseY-1,P.metal);
      put(bx-1,baseY,P.handle); put(bx-2,baseY,P.handle); put(bx-2,tipY,P.metalSh); }

    // head from behind — bald crown + horseshoe of grey hair, no face
    const nod=(m.isSpeak&&f===6)?1:0;    // tiny nod while "speaking" (no visible mouth)
    const hy=B0+nod, hl=K.headL, hr=K.headR, htop=K.headTop+hy, hbot=K.headBot+hy;
    for(let y=htop;y<=hbot;y++){ let l=hl,r=hr; if(y===htop){l+=2;r-=2;} else if(y===htop+1){l+=1;r-=1;} if(y===hbot){l+=1;r-=1;} fill(l,r,y,y,P.skin); }
    // hair horseshoe: bald spot up top-center, hair on sides + lower-back of the skull
    for(let y=htop;y<=hbot;y++) for(let x=hl;x<=hr;x++){
      if(!on(x,y)) continue;
      const baldSpot=(y<=K.earY+hy-1)&&(x>=cx-1&&x<=cx+1);
      const edge=(x<=hl+1||x>=hr-1)||(y>=K.earY+hy);
      if(edge && !baldSpot) put(x,y,P.hair);
    }
    for(let x=hl+1;x<=hr-1;x++){ if(on(x,hbot)) put(x,hbot,P.hairSh); }
    // ears peeking at the sides
    put(hl-1,K.earY+hy,P.skin); put(hr+1,K.earY+hy,P.skin);
    put(hl-1,K.earY+hy+1,P.skinSh); put(hr+1,K.earY+hy+1,P.skinSh);
    // a sliver of nape skin above the collar
    put(cx,K.neckTop+B0-1,P.skinSh);
  }

  // ============================ SIDE (profile, faces RIGHT) ============================
  function paintSide(X){
    const {set,put,fill,on,cx,P,K,form,m,f}=X; const {B0,isWalk,isSpeak}=m;
    const nm = form==='nightmare';

    // ---- legs: scissor walk in profile (toe points right / +x) ----
    const legTopY=K.legTop, legBotY=K.legBot, footY=K.footY;
    let nearFwd=0, farFwd=0;
    if(isWalk){ if(f===2){ nearFwd=2; farFwd=-2; } else if(f===4){ nearFwd=-2; farFwd=2; } }
    // far leg (behind — darker)
    fill(cx-1+farFwd, cx+farFwd, legTopY, legBotY-1, P.covSh);
    fill(cx-1+farFwd, cx+farFwd, footY-1, footY, P.bootSh);
    put(cx+1+farFwd, footY, P.bootSh);                 // far toe
    // near leg (front)
    fill(cx+nearFwd, cx+1+nearFwd, legTopY, legBotY-1, P.cov);
    put(cx+nearFwd, legBotY-1, P.covSh);
    fill(cx+nearFwd, cx+1+nearFwd, footY-1, footY, P.boot);
    put(cx+2+nearFwd, footY, P.boot);                  // near toe points forward
    put(cx+2+nearFwd, footY, P.bootSh);

    // ---- torso profile: belly bulges FORWARD (+x), back straight ----
    for(let y=K.torsoTop; y<=K.torsoBot; y++){
      const yy=y+B0; const belly=(y>=K.bellyTop&&y<=K.bellyBot);
      let l=cx-2, r = (y<=K.shoulderY+1) ? cx+2 : (belly? cx+4 : cx+3);
      if(nm){ l=cx-1; r=(y<=K.shoulderY+1)?cx+1:(belly?cx+2:cx+2); }   // thin
      fill(l, r, yy, yy, P.cov);
      put(l, yy, P.covSh);                              // back-edge shade
      put(r, yy, P.covSh);
    }

    // bib panel on the front + a shoulder strap + turtleneck at the collar/chest
    const frontX = nm? cx+2 : cx+3;
    for(let y=K.shoulderY+1+B0; y<=K.bellyTop+B0; y++) put(frontX-1, y, P.strap);   // strap down the chest
    put(frontX-1, K.shoulderY+1+B0, P.buckle);
    fill(cx-1, cx+1, K.neckTop+B0, K.neckBot+B0, P.neck);                            // collar
    put(cx+1, K.neckBot+B0, P.neckSh);

    // ---- near arm hanging in front, gloved, holding shears ----
    const aTop=K.armTop+B0, aBot=K.armBot+B0, armX=frontX;
    let sw=0; if(isWalk){ sw = (f===2?-1:(f===4?1:0)); }   // swings opposite the near leg
    for(let y=aTop;y<=aBot+sw;y++){ put(armX,y,P.cov); put(armX+1,y,P.covSh); }
    const glY=aBot+sw, fingLen=nm?3:1;
    for(let k=0;k<=fingLen;k++){ put(armX,glY+1+k,k?P.gloveSh:P.glove); put(armX+1,glY+1+k,P.glove); }
    // shears rising in front of him
    { const bx=armX+1, tipY=(nm?9:12)+B0, baseY=glY+1;
      for(let y=tipY;y<=baseY;y++){ put(bx+1,y,P.metal); put(bx+2,y,y%3?P.metal:P.metalSh); }
      put(bx+1,baseY,P.handle); put(bx+2,baseY,P.handle); put(bx+2,tipY,P.metalSh); }

    // ---- head profile ----
    const hy=B0, htop=K.headTop+hy, hbot=K.headBot+hy;
    // neck lean: nightmare cranes the head forward (+x) as it rises
    const lean = nm ? 1 : 0;
    const hbx = cx-3+lean, hfx = cx+3+lean;             // head back / front x
    for(let y=htop;y<=hbot;y++){ let l=hbx, r=hfx; if(y===htop){l+=1;} if(y===hbot){l+=1;} fill(l,r,y,y,P.skin); }
    put(hfx+1, htop+Math.round((hbot-htop)*0.5), P.skin);        // nose bump at the front
    put(hfx+1, htop+Math.round((hbot-htop)*0.5)+1, P.skinSh);
    // balding: hair on the back + a low fringe; bald over the top-front
    for(let y=htop;y<=hbot;y++) for(let x=hbx;x<=hfx;x++){
      if(!on(x,y)) continue;
      const backHair=(x<=hbx+1)&&(y>=htop+1);
      const lowFringe=(y>=K.earY+hy+1)&&(x<=cx+lean);
      if(backHair||lowFringe) put(x,y,P.hair);
    }
    put(hbx, hbot, P.hairSh);
    // ear
    put(cx-1+lean, K.earY+hy, P.skinSh);

    // glasses (profile) — one lens at the front, temple arm back to the ear
    const gy=(nm?htop+2:htop+3), ex=cx+2+lean;
    if(!nm){
      put(ex-1,gy,P.lens); put(ex,gy,P.lens); put(ex+1,gy,P.lens);
      put(ex-1,gy+1,P.lens); put(ex,gy+1,P.lens); put(ex+1,gy+1,P.lens);
      put(ex-1,gy-1,P.frame); put(ex,gy-1,P.frame); put(ex+1,gy-1,P.frame);
      put(ex+2,gy,P.frame); put(ex-2,gy,P.frame); put(ex+2,gy+1,P.frame);
      put(ex-1,gy+2,P.frame); put(ex,gy+2,P.frame); put(ex+1,gy+2,P.frame);
      put(ex-2,gy-1,P.frame); put(ex-3,gy-1,P.frame);       // temple arm back
      put(ex,gy,P.eye);
    } else {
      put(ex-1,gy,P.lens); put(ex,gy,P.lens); put(ex+1,gy,P.lens);
      put(ex-1,gy+1,P.lens); put(ex,gy+1,P.lens); put(ex+1,gy+1,P.lens);
      put(ex-2,gy,P.frame); put(ex-3,gy-1,P.frame);
    }

    // stubble on the front jaw
    for(let y=hbot-1;y<=hbot;y++) for(let x=cx+lean;x<=hfx;x++){ if(on(x,y)&&rnd(f*5+x,y,nm?9:3)>0.5) put(x,y,P.stub); }

    // ---- mouth (front of the face) ----
    const my=K.mouthY+hy, mfx=cx+2+lean;
    if(!nm){
      if(f===6){ put(mfx,my,P.void); put(mfx+1,my,P.void); put(mfx,my+1,P.void); }   // small open flap
      else { put(mfx,my,P.void); put(mfx+1,my,P.void); put(mfx+2,my-1,P.skinSh); }   // upturned smile at the front
    } else {
      if(f===6){ // jaw unhinges FORWARD-DOWN into a void
        for(let k=0;k<=6;k++){ const w=Math.min(3, 1+Math.floor(k/2)); fill(mfx-1, mfx-1+w, my+k, my+k, P.void); }
      } else { fill(mfx-1, mfx+2, my, my, P.void); put(mfx, my+1, P.void); }          // too-wide rictus
    }
  }

  const PAINT = { front:paintFront, back:paintBack, side:paintSide };

  function build(opts){
    opts=opts||{}; const form=opts.form||'home', view=opts.view||'front';
    const P=PAL[form], K=CFG[form];
    const W=CELL*FRAMES, H=CELL;
    const cv=window.__nbCanvas(W,H); const ctx=cv.getContext('2d');
    const O=ctx.createImageData(W,H); const OD=O.data;

    for(let f=0; f<FRAMES; f++){
      const B=new Uint8ClampedArray(CELL*CELL*4);
      const bi=(x,y)=>(y*CELL+x)*4;
      const set=(x,y,rgb,a)=>{ if(x<0||x>31||y<0||y>31) return; const i=bi(x,y); B[i]=rgb[0];B[i+1]=rgb[1];B[i+2]=rgb[2];B[i+3]=(a==null?255:a); };
      const put=(x,y,hex)=>set(x,y,hx(hex));
      const fill=(x0,x1,y0,y1,hex)=>{ const c=hx(hex); for(let y=y0;y<=y1;y++) for(let x=x0;x<=x1;x++) set(x,y,c); };
      const on=(x,y)=>{ if(x<0||x>31||y<0||y>31) return false; return B[bi(x,y)+3]>=128; };

      const X={set,put,fill,on,cx:K.cx,P,K,form,m:motionFor(f),f};
      (PAINT[view]||paintFront)(X);

      // outline pass (player-matching flat look)
      const outline=hx(P.out); const snap=B.slice();
      const wasOn=(x,y)=>{ if(x<0||x>31||y<0||y>31) return false; return snap[(y*CELL+x)*4+3]>=128; };
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){ if(wasOn(x,y)) continue; if(wasOn(x-1,y)||wasOn(x+1,y)||wasOn(x,y-1)||wasOn(x,y+1)) set(x,y,outline); }

      // blit
      const ox=f*CELL;
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){ const i=bi(x,y); if(B[i+3]<128) continue; const oi=(y*W+(ox+x))*4; OD[oi]=B[i];OD[oi+1]=B[i+1];OD[oi+2]=B[i+2];OD[oi+3]=255; }
    }

    ctx.putImageData(O,0,0);
    const nmSuffix = form==='nightmare' ? '_nightmare' : '';
    const name = `neighbor_robert_${view}${nmSuffix}.png`;
    return { name, canvas:cv, W, H };
  }

  async function buildAndSave(opts){
    const r=build(opts);
    await window.__nbSave(r.name, r.canvas);
    const up=window.__nbCanvas(r.W*8,r.H*8); const ux=up.getContext('2d'); ux.imageSmoothingEnabled=false; ux.drawImage(r.canvas,0,0,r.W*8,r.H*8);
    await window.__nbSave(r.name.replace('.png','-8x.png'), up);
    return r.name;
  }

  return { build: buildAndSave, PAL, CFG };
})();
