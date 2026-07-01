// House tile generator — shared by home & nightmare sheets.
// Evaluated via eval(await readFile('tilegen.js')); returns {P, PN, renderSheet, CELL, COLS, ROWS, LAYOUT}.
(function(){
  const CELL=24, COLS=8, ROWS=6;

  // HOME — warm, cozy, lived-in
  const P = {
    wl:'#bd8246', wm:'#9a6531', wd:'#6f4420', grv:'#3f2811', grain:'#7a4d24',
    beam:'#4a2d13', beamD:'#2a1809',
    sl:'#8f857a', sm:'#695f55', sd:'#473d34', sgrv:'#2c261f',
    rl:'#a4543c', rm:'#7e3d2d', rd:'#56261a', redge:'#341610', rhi:'#c07354',
    glass:'#28303a', glassD:'#161c22',
    gw1:'#ffe9b0', gw2:'#f3c062', gw3:'#d4882e', frame:'#3a2412',
    fl1:'#fff2c8', fl2:'#ffd060', fl3:'#ff8f2e',
    sm1:'#d2cdc2', sm2:'#a8a399', sm3:'#7f7a71',
    sil:'#150f0a',
    brk:'#9a5240', brickD:'#5e2f22', mor:'#c2a184',
  };
  // NIGHTMARE — dead of night, home invasion. Cold, dark, realistic.
  const PN = {
    wl:'#454338', wm:'#27241d', wd:'#16140f', grv:'#0b0a07', grain:'#201d17',
    beam:'#15130e', beamD:'#080705',
    sl:'#484852', sm:'#26262c', sd:'#181820', sgrv:'#0b0b10',
    rl:'#3a3a46', rm:'#22222a', rd:'#141419', redge:'#08080b', rhi:'#464654',
    glass:'#10141a', glassD:'#070a0e',
    gw1:'#e6d49a', gw2:'#a98f50', gw3:'#5e4e28', frame:'#14120c',  // dim sodium bulb
    fl1:'#dfeaf0', fl2:'#9fb4be', fl3:'#5a6e78',                    // cold flashlight
    sm1:'#3a3e44', sm2:'#23262b', sm3:'#15171b',                    // faint cold wisp
    sil:'#000000',
    moon:'#5a626e', refl:'#26303c', dark:'#040506',
    brk:'#2a2628', brickD:'#121011', mor:'#3e3a3c',
  };

  function api(ctx,ox,oy){return{
    r:(x,y,w,h,c)=>{ctx.fillStyle=c;ctx.fillRect(ox+x,oy+y,w,h);},
    p:(x,y,c)=>{ctx.fillStyle=c;ctx.fillRect(ox+x,oy+y,1,1);},
    clr:(x,y,w=1,h=1)=>ctx.clearRect(ox+x,oy+y,w,h),
  };}
  const clipRect=(a,c,x,y,w,h)=>{let x0=Math.max(x,4),x1=Math.min(x+w,20),y0=Math.max(y,4),y1=Math.min(y+h,20);if(x1>x0&&y1>y0)a.r(x0,y0,x1-x0,y1-y0,c);};

  // ---- structural (shared geometry; palette carries the mood) ----
  function wallA({r,p},P,f,night){for(let py=0;py<24;py+=6){r(0,py,24,6,P.wm);r(0,py,24,1,P.wl);r(0,py+4,24,1,P.wd);r(0,py+5,24,1,P.grv);for(const gx of[3,9,15,21])p(gx,py+2,P.grain);p(7,py+3,P.wd);p(17,py+2,P.wd);p(12,py+3,P.grain);}if(night){/*moonlit top rim*/}}
  function wallB(a,P,f,night){wallA(a,P,f,night);const{r,p}=a;r(13,8,5,5,P.wd);r(14,9,3,3,P.grv);p(15,10,P.beamD);r(12,9,1,3,P.grain);}
  function postV({r,p},P){r(0,0,24,24,P.beam);r(0,0,2,24,P.beamD);r(22,0,2,24,P.beamD);r(2,0,1,24,P.grain);r(21,0,1,24,P.grain);for(const py of[4,12,20]){r(9,py,5,3,P.beamD);p(10,py+1,P.grain);p(12,py+1,P.grain);}for(let py=0;py<24;py+=3){p(7,py,P.beamD);p(16,py+1,P.beamD);}}
  function beamH({r,p},P){r(0,0,24,24,P.beam);r(0,0,24,2,P.beamD);r(0,22,24,2,P.beamD);for(let px=0;px<24;px+=3){p(px,7,P.grain);p(px+1,15,P.beamD);}for(const px of[4,12,20]){r(px,10,3,4,P.beamD);p(px+1,11,P.grain);}}
  function foundation({r,p},P){r(0,0,24,24,P.sm);r(0,7,24,1,P.sgrv);r(0,15,24,1,P.sgrv);r(0,23,24,1,P.sgrv);r(11,0,1,8,P.sgrv);r(5,8,1,8,P.sgrv);r(17,8,1,8,P.sgrv);r(11,16,1,8,P.sgrv);r(0,0,24,1,P.sl);r(0,8,24,1,P.sl);r(0,16,24,1,P.sl);for(const[sx,sy]of[[3,3],[8,11],[19,4],[14,19],[6,18],[20,12]])p(sx,sy,P.sd);for(const[sx,sy]of[[15,2],[2,10],[9,13],[18,17]])p(sx,sy,P.sl);}

  function doorTop({r,p},P,f,night){
    if(night){ // ajar — black gap, door swung inward
      r(0,0,24,24,P.frame);
      r(2,0,20,24,P.dark);                 // interior darkness
      const dx=6; r(2+dx,0,20-dx,24,P.wm); r(2+dx,0,20-dx,1,P.wd);
      for(const ddx of[12,17])r(ddx,1,1,23,P.wd);
      r(9+dx-2,4,11-dx+2,9,P.wd); r(10+dx-2,5,9-dx+2,7,P.wm);
      r(2+dx,0,1,24,P.moon);               // moonlit edge of the open door
      p(3,2,P.moon);                        // faint light catching the jamb
      return;
    }
    r(0,0,24,24,P.frame);r(2,0,20,24,P.wm);r(2,0,20,1,P.wd);for(const dx of[7,12,17])r(dx,1,1,23,P.wd);r(5,4,14,9,P.wd);r(6,5,12,7,P.wl);r(9,6,6,4,P.glass);r(11,6,1,4,P.frame);
  }
  function doorBottom({r,p},P,f,night){
    if(night){
      r(0,0,24,24,P.frame);
      r(2,0,20,24,P.dark);
      const dx=6; r(2+dx,0,20-dx,22,P.wm);
      for(const ddx of[12,17])r(ddx,0,1,22,P.wd);
      r(9+dx-2,3,11-dx+2,15,P.wd); r(10+dx-2,4,9-dx+2,13,P.wm);
      r(10+dx,10,2,3,P.beamD);
      r(2+dx,0,1,22,P.moon);
      r(0,22,24,2,P.beamD);
      r(2,21,dx,1,P.moon);                 // sliver of light on the threshold through the gap
      return;
    }
    r(0,0,24,24,P.frame);r(2,0,20,24,P.wm);for(const dx of[7,12,17])r(dx,0,1,22,P.wd);r(5,3,14,15,P.wd);r(6,4,12,13,P.wl);r(16,10,2,3,P.beamD);p(17,11,P.gw2);r(0,22,24,2,P.beamD);
  }
  function windowStatic({r,p},P,f,night){
    r(0,0,24,24,P.frame);r(3,3,18,18,P.glassD);r(11,3,2,18,P.frame);r(3,11,18,2,P.frame);r(0,21,24,3,P.beam);r(0,21,24,1,P.beamD);
    if(night){ r(5,5,5,1,P.refl);r(6,6,3,1,P.refl);p(5,6,P.refl);p(14,15,P.refl); }  // cold moonlight reflection
    else { r(5,5,4,1,P.glass);p(5,6,P.glass); }
  }
  function roofFill({r,p},P){r(0,0,24,24,P.rm);for(let ry=0;ry<24;ry+=8){r(0,ry,24,1,P.rd);r(0,ry+1,24,2,P.rl);const off=((ry/8)%2)?0:6;for(let sx=off;sx<24;sx+=12){r(sx,ry+1,1,7,P.rd);}for(let sx=off+6;sx<24;sx+=12){p(sx,ry+3,P.rhi);}}}
  function roofSlopeL(a,P){const{p,clr}=a;roofFill(a,P);for(let y=0;y<24;y++)for(let x=0;x<24;x++)if(x+y<22)clr(x,y);for(let y=0;y<24;y++){const x=22-y;if(x>=0&&x<24){p(x,y,P.redge);if(x+1<24)p(x+1,y,P.rhi);}}}
  function roofSlopeR(a,P){const{p,clr}=a;roofFill(a,P);for(let y=0;y<24;y++)for(let x=0;x<24;x++)if(x>y+1)clr(x,y);for(let y=0;y<24;y++){const x=y+1;if(x>=0&&x<24){p(x,y,P.redge);if(x-1>=0)p(x-1,y,P.rhi);}}}
  function roofEave({r,p},P){r(0,0,24,9,P.rm);r(0,0,24,1,P.rd);r(0,1,24,2,P.rl);for(let sx=0;sx<24;sx+=12){r(sx,1,1,7,P.rd);}r(0,7,24,2,P.redge);r(0,9,24,2,P.beamD);}
  function chimney({r,p},P){r(5,0,14,24,P.brk);for(let by=0;by<24;by+=5){r(5,by+4,14,1,P.mor);}for(let by=0;by<24;by+=5){const off=((by/5)%2)?8:5;for(let bx=off;bx<19;bx+=7){r(bx,by,1,4,P.mor);}}r(5,0,1,24,P.brickD);r(18,0,1,24,P.brickD);for(const[bx,by]of[[8,2],[14,8],[10,18]])p(bx,by,P.brickD);}
  function chimneyTop({r,p,clr},P){for(let y=0;y<6;y++)for(let x=0;x<24;x++)clr(x,y);r(5,6,14,18,P.brk);for(let by=8;by<24;by+=5)r(5,by,14,1,P.mor);r(5,6,1,18,P.brickD);r(18,6,1,18,P.brickD);r(3,3,18,4,P.brickD);r(3,3,18,1,P.brk);r(8,0,8,3,P.beamD);}

  // ---- animated ----
  function winLit({r},P){r(0,0,24,24,P.frame);r(3,3,18,18,P.gw3);r(4,4,16,15,P.gw2);r(5,5,14,8,P.gw1);r(11,3,2,18,P.frame);r(3,11,18,2,P.frame);r(0,21,24,3,P.beam);r(0,21,24,1,P.beamD);}

  // winSil: HOME = someone walks past a warm window. NIGHT = the one lit room, an intruder standing inside (backlit).
  function winSil(a,P,f,night){
    if(night){
      const {r}=a;
      r(0,0,24,24,P.frame);
      r(3,3,18,18,P.gw3); r(5,4,14,13,P.gw2); r(13,4,6,7,P.gw1);  // dim light, brightest by an off-screen lamp
      r(3,3,4,18,'#0c0a07');                                       // curtain edge (dark)
      r(11,3,2,18,P.frame); r(3,11,18,2,P.frame);
      r(0,21,24,3,P.beam); r(0,21,24,1,P.beamD);
      // backlit human figure, standing too still; slow turn toward the glass
      const sway=[0,0,0,1,1,0][f];
      const step=[0,0,0,0,1,1][f];          // drifts a hair closer late in the loop
      const cx=11+sway;
      const turning = f>=3;                 // narrows to a profile as it turns
      const sw = turning? 4 : 5;            // shoulder width
      clipRect(a,P.sil, cx-Math.floor(sw/2), 9-step, sw, 8+step);   // torso
      clipRect(a,P.sil, cx-1, 6-step, 3, 3);                         // head
      clipRect(a,P.sil, cx-Math.floor(sw/2), 17, 2, 3);             // legs
      clipRect(a,P.sil, cx+Math.ceil(sw/2)-2, 17, 2, 3);
      return;
    }
    winLit(a,P);
    if(f===0)return;
    const xs=[null,5,9,12,15,19][f];
    clipRect(a,P.sil,xs-1,6,3,3);clipRect(a,P.sil,xs-2,9,5,7);
    if(f%2){clipRect(a,P.sil,xs-2,16,2,3);clipRect(a,P.sil,xs+1,16,2,4);}else{clipRect(a,P.sil,xs-2,16,2,4);clipRect(a,P.sil,xs+1,16,2,3);}
  }

  // winCandle: HOME = lamp flicker. NIGHT = a flashlight sweeps a pitch-black room (someone searching).
  function winCandle(a,P,f,night){
    const{r,p}=a;
    if(night){
      r(0,0,24,24,P.frame); r(3,3,18,18,P.glassD);
      r(11,3,2,18,P.frame); r(3,11,18,2,P.frame);
      p(5,5,P.refl);p(6,6,P.refl);                          // moon glint
      const bx=[6,11,15,18][f], by=[14,12,13,10][f];
      r(bx-2,by-2,5,5,P.fl3); r(bx-1,by-1,3,3,P.fl2); p(bx,by,P.fl1);  // cold beam
      if(f===2){ r(bx+2,by-1,2,4,'#0c0e10'); }              // a shape brushes past the beam
      r(0,21,24,3,P.beam); r(0,21,24,1,P.beamD);
      return;
    }
    r(0,0,24,24,P.frame);r(3,3,18,18,P.glassD);const g=[6,7,6,8][f];const cx=14,cy=11;r(cx-g,cy-3,g*2,9,P.gw3);r(cx-g+1,cy-2,g*2-2,7,P.gw2);r(cx-2,cy-2,4,5,P.gw1);r(11,3,2,18,P.frame);r(3,11,18,2,P.frame);r(13,15,3,5,P.beamD);r(14,12,1,3,P.gw1);const fr=[[0,0],[1,-1],[-1,0],[0,-1]][f];p(14+fr[0],10+fr[1],P.fl1);p(14+fr[0],9+fr[1],P.fl2);p(14+fr[0],8+fr[1],P.fl3);r(0,21,24,3,P.beam);r(0,21,24,1,P.beamD);
  }

  // smoke: HOME = chimney puffs. NIGHT = the fire is out; only a faint cold wisp, almost nothing.
  function smoke(a,P,f,night){
    if(night){ const shift=(f*4)%24; let y=((14-shift)%24+24)%24; if(y>3&&y<19){ const cx=10+Math.round(Math.sin((24-y)/24*Math.PI*2)*2); a.p(cx,y,P.sm2); a.p(cx, y-1, P.sm3); } return; }
    const shift=(f*4)%24;for(const b of[18,6]){let y=((b-shift)%24+24)%24;const sz=y>16?4:y>9?3:2;const col=y>15?P.sm1:y>8?P.sm2:P.sm3;const cx=9+Math.round(Math.sin((24-y)/24*Math.PI*2)*2);a.r(cx-(sz>>1),y-(sz>>1),sz,sz,col);if(sz>=3){a.p(cx-(sz>>1)-1,y,col);a.p(cx+Math.ceil(sz/2),y,col);}}
  }

  // loosePlank: HOME = a board creaks. NIGHT = a human shadow slides across the outside wall.
  function loosePlank(a,P,f,night){
    if(night){
      wallA(a,P,f,true);
      const {r,p}=a;
      const sx=[-3,4,11,17][f];               // shadow drifts across
      const sh='rgba(0,0,0,0.62)', ed='rgba(0,0,0,0.30)';   // translucent: wall grain shows through
      const blob=(x,y,w,h,c)=>{for(let yy=y;yy<y+h;yy++)for(let xx=x;xx<x+w;xx++)if(xx>=0&&xx<24&&yy>=0&&yy<24)p(xx,yy,c);};
      // soft outer falloff
      blob(sx-4,4,11,17,ed);
      // core human-ish shadow (head + shoulders + taper)
      blob(sx-1,5,3,3,sh);            // head
      blob(sx-3,9,7,5,sh);           // shoulders
      blob(sx-2,14,5,6,sh);          // body taper
      return;
    }
    wallA(a,P);const{r,p,clr}=a;const dy=[0,-1,0,1][f],dx=[0,1,0,-1][f];for(let y=12;y<18;y++)clr(0,y,24,1);if(dy<0)r(0,17,24,1,P.grv);const py=12+dy;r(0,py,24,6,P.wm);r(0,py,24,1,P.wl);r(0,py+4,24,1,P.wd);r(0,py+5,24,1,P.grv);for(const gx of[3,9,15,21])p((gx+dx+24)%24,py+2,P.grain);const nailx=[19,20,19,18][f];r(nailx,py+2,2,2,P.beamD);p(nailx,py+2,P.grain);
  }

  const LAYOUT=[
    ['wallA',wallA,0,0,1],['wallB',wallB,1,0,1],['postV',postV,2,0,1],['beamH',beamH,3,0,1],
    ['foundation',foundation,4,0,1],['doorTop',doorTop,5,0,1],['doorBottom',doorBottom,6,0,1],['window',windowStatic,7,0,1],
    ['roofFill',roofFill,0,1,1],['roofSlopeL',roofSlopeL,1,1,1],['roofSlopeR',roofSlopeR,2,1,1],['roofEave',roofEave,3,1,1],
    ['chimney',chimney,4,1,1],['chimneyTop',chimneyTop,5,1,1],
    ['winSil',winSil,0,2,6],['winCandle',winCandle,0,3,4],['smoke',smoke,0,4,6],['loosePlank',loosePlank,0,5,4],
  ];

  function renderSheet(ctx, palette, night){
    for(const [name,fn,c,r,frames] of LAYOUT){
      for(let f=0; f<frames; f++){
        fn(api(ctx,(c+f)*CELL, r*CELL), palette, f, night);
      }
    }
  }

  return {P, PN, renderSheet, CELL, COLS, ROWS, LAYOUT};
})()
