// Spruce tree sprite-sheet generator.
// 6x2 grid, 80x152 per cell, 12-frame seamless sway loop.
// Motion: branch tiers flutter independently (out of phase) + needle-edge
// shimmer + an occasional needle that sheds and drifts out of frame.
// Trunk stays planted, ground shadow static. Green + snow-dusted seasons.
// Evaluated via eval() (sets window.TreeGen) or imported as a global script.
window.TreeGen = (function(){
  const FRAME_W = 80, FRAME_H = 152, COLS = 6, ROWS = 2, FRAMES = 12, PX = 2;
  const LW = FRAME_W / PX, LH = FRAME_H / PX;   // 40 x 76 logical
  const CX = 20;                                 // trunk axis (logical)

  // deep, slightly cool greens — reads as an unsettling dense stand
  const GREEN = {
    hi:'#79ad6b', light:'#4f8a4d', mid:'#387140', shadow:'#265230', dark:'#173a22',
    tip:'#8cc079',
    trunkL:'#6a4a2b', trunkM:'#4c3420', trunkD:'#2c1c10',
    needle:'#3f5f2e',
    ground:'rgba(8,13,10,0.26)',
  };
  // winter: colder, desaturated needles under snow load
  const WINTER = {
    hi:'#5d8462', light:'#436f49', mid:'#335d3a', shadow:'#244828', dark:'#16331e',
    tip:'#6fa06f',
    snowHi:'#eef4f7', snowMid:'#cddae2', snowSh:'#a6b7c4',
    trunkL:'#5a4127', trunkM:'#3f2f1c', trunkD:'#26180d',
    flake:'#eef5f9',
    ground:'rgba(176,194,206,0.20)',
  };

  // tiers top->bottom. amp = sway (logical px), phase staggers the ripple.
  const TIERS = [
    {top:2,  shelf:17, neck:0,   edge:5,    amp:1.7, phase:0.0},
    {top:12, shelf:26, neck:3,   edge:8,    amp:1.35,phase:0.55},
    {top:21, shelf:35, neck:5,   edge:11,   amp:1.05,phase:1.1},
    {top:30, shelf:45, neck:7,   edge:13.5, amp:0.78,phase:1.65},
    {top:39, shelf:55, neck:9,   edge:16,   amp:0.52,phase:2.2},
    {top:48, shelf:65, neck:11,  edge:18,   amp:0.30,phase:2.75},
  ];

  function hash(x,y){ const s=Math.sin(x*127.1+y*311.7)*43758.5453; return s-Math.floor(s); }
  function halfWAt(t,y){ if(y<t.top||y>t.shelf) return -1; const fr=(y-t.top)/(t.shelf-t.top); return t.neck+(t.edge-t.neck)*Math.pow(fr,0.82); }

  function blk(ctx,ox,oy,lx,ly,c){ if(lx<0||lx>=LW||ly<0||ly>=LH) return; ctx.fillStyle=c; ctx.fillRect(ox+lx*PX, oy+ly*PX, PX, PX); }

  function drawGround(ctx,ox,oy,P){
    const rx=14, ry=3.2, cy=72;
    for(let y=-4;y<=4;y++) for(let x=-rx;x<=rx;x++){
      if((x*x)/(rx*rx)+(y*y)/(ry*ry) <= 1) blk(ctx,ox,oy,CX+x,cy+y,P.ground);
    }
  }

  function drawTrunk(ctx,ox,oy,P){
    for(let y=63;y<=71;y++){
      blk(ctx,ox,oy,CX-1,y,P.trunkL);
      blk(ctx,ox,oy,CX,  y,P.trunkM);
      blk(ctx,ox,oy,CX+1,y,P.trunkD);
    }
    blk(ctx,ox,oy,CX,65,P.trunkD);
    blk(ctx,ox,oy,CX-1,68,P.trunkM);
    blk(ctx,ox,oy,CX-2,71,P.trunkD);
    blk(ctx,ox,oy,CX+2,71,P.trunkD);
  }

  function drawTier(ctx,ox,oy,t,dx,f,P,winter,idx){
    const cx = CX + dx;
    for(let y=t.top; y<=t.shelf; y++){
      const hw = halfWAt(t,y); if(hw<0) continue;
      let hwR = Math.round(hw);
      if(idx===0){ const ry=y-t.top; if(ry<2) hwR=0; else if(ry<5) hwR=1; }  // crisp leader spike
      const above = halfWAt(t,y-1);
      for(let x=-hwR; x<=hwR; x++){
        const lx = cx + x;
        const tnorm = (x+hwR)/(2*hwR||1);
        const d = (hash(lx*3.1, y*1.7)-0.5)*0.07;   // dithered zone edges kill the vertical seam
        let c = tnorm < 0.30+d ? P.light : tnorm < 0.60+d ? P.mid : P.shadow;
        const isTop = !(above>=0 && Math.round(above)>=Math.abs(x));
        if(isTop) c = (x < hwR*0.2) ? P.hi : (x < hwR*0.55 ? P.light : P.mid);
        if(x===-hwR && !isTop) c = P.hi;            // crisp lit rim
        if(x===hwR  && !isTop) c = P.dark;          // crisp shadow rim
        if(y===t.shelf) c = P.dark;
        if(!isTop && x>-hwR && x<hwR && y<t.shelf){
          const h = hash(lx, y);
          if(h>0.975) c = P.hi; else if(h<0.025) c = P.dark;   // sparse needle texture
        }
        // snow load on lit upper rim
        if(winter && isTop && x < hwR*0.55){ c = (x < hwR*0.1) ? P.snowHi : P.snowMid; }
        blk(ctx,ox,oy,lx,y,c);
      }
      // needle fringe along the shelf, flickering per frame (the "alive" shimmer)
      if(y===t.shelf){
        for(let x=-hwR; x<=hwR; x++){
          const lx=cx+x;
          if(hash(lx*1.7+idx*9, 50) <= 0.58) continue;
          const fl = hash(lx+idx*3, f*7+3);
          let tip = fl>0.66 ? P.tip : fl>0.33 ? P.shadow : P.mid;
          if(x>0) tip = P.shadow;
          blk(ctx,ox,oy,lx,y+1,tip);
        }
      }
    }
    // snow caps sitting on the shelf shoulders
    if(winter){
      for(let x=-Math.round(t.edge); x<=Math.round(t.edge); x++){
        if(hash(x*2.3+idx*5, 11) > 0.74) blk(ctx,ox,oy,cx+x, t.shelf-1, x<0?P.snowHi:P.snowSh);
      }
    }
  }

  function drawParticles(ctx,ox,oy,f,P,winter){
    const set = winter ? [
      {f0:0,f1:6, x:24,y:24, vx:0.5, vy:2.0, wob:2.0, ph:0.0},
      {f0:3,f1:10,x:14,y:30, vx:-0.3,vy:1.9, wob:2.4, ph:1.2},
      {f0:6,f1:12,x:27,y:36, vx:0.6, vy:1.7, wob:1.8, ph:2.0},
    ] : [
      {f0:1,f1:6,  x:26,y:30, vx:0.9, vy:2.5, wob:1.4, ph:0.0},
      {f0:7,f1:12, x:14,y:40, vx:0.6, vy:2.3, wob:1.7, ph:1.1},
    ];
    for(const n of set){
      if(f < n.f0 || f >= n.f1) continue;
      const age = f - n.f0, span = n.f1 - n.f0;
      const x = Math.round(n.x + n.vx*age + n.wob*Math.sin(age*0.9 + n.ph));
      const y = Math.round(n.y + n.vy*age);
      if(y >= 70) continue;                          // gone before the ground — stays clean
      const a = Math.sin(Math.PI * (age+0.5)/(span+1)); // fade in + out -> seamless
      ctx.globalAlpha = Math.max(0, a);
      blk(ctx,ox,oy,x,y, winter?P.flake:P.needle);
      if(winter) ctx.globalAlpha = a*0.5, blk(ctx,ox,oy,x,y-1,P.snowMid);
      ctx.globalAlpha = 1;
    }
  }

  function drawTree(ctx, ox, oy, f, winter){
    const P = winter ? WINTER : GREEN;
    const ph = 2*Math.PI*f/FRAMES;
    drawGround(ctx,ox,oy,P);
    drawTrunk(ctx,ox,oy,P);
    for(let i=TIERS.length-1;i>=0;i--){
      const t=TIERS[i];
      const dx = Math.round(t.amp*Math.sin(ph + t.phase));
      drawTier(ctx,ox,oy,t,dx,f,P,winter,i);
    }
    drawParticles(ctx,ox,oy,f,P,winter);
  }

  function renderSheet(ctx, winter){
    ctx.clearRect(0,0,FRAME_W*COLS,FRAME_H*ROWS);
    for(let f=0; f<FRAMES; f++){
      drawTree(ctx, (f%COLS)*FRAME_W, Math.floor(f/COLS)*FRAME_H, f, winter);
    }
  }

  return { FRAME_W, FRAME_H, COLS, ROWS, FRAMES, PX, GREEN, WINTER, drawTree, renderSheet };
})();
if (typeof module !== 'undefined') module.exports = window.TreeGen;
