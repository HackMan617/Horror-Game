// High-detail conifer sprite system — reusable across scenes.
//   4 species : spruce (dark spiky spire) · pine (rounder, open boughs) ·
//               snag (dead bare trunk) · ridge (tiny distant silhouette)
//   2 themes  : sunset (warm backlit rim, cool green body) · moody (dark dusk)
//   2 loops   : idle (tiny breathing shimmer) · sway (a wind gust swells + releases)
// Every sprite is 1px pixel-art. drawTree() paints one frame; buildAtlas() caches
// the 12 frames of a (species,theme,mode) as canvases for cheap blitting; renderSheet()
// lays a full sheet (6 cols x 4 rows = idle rows 0-1, sway rows 2-3) for PNG export.
// Evaluated via eval() (sets window.TreeGen) or imported as a global script.
window.TreeGen = (function(){
  const FRAMES = 12, COLS = 6, ROWS = 4;   // rows 0-1 idle, 2-3 sway

  // ---------------------------------------------------------------- utils
  function hash(x,y){ const s=Math.sin(x*127.1+y*311.7)*43758.5453; return s-Math.floor(s); }
  function mix(a,b,t){ // hex lerp
    const pa=[parseInt(a.slice(1,3),16),parseInt(a.slice(3,5),16),parseInt(a.slice(5,7),16)];
    const pb=[parseInt(b.slice(1,3),16),parseInt(b.slice(3,5),16),parseInt(b.slice(5,7),16)];
    const c=pa.map((v,i)=>Math.round(v+(pb[i]-v)*t));
    return '#'+c.map(v=>v.toString(16).padStart(2,'0')).join('');
  }

  // ---------------------------------------------------------------- themes
  const THEMES = {
    sunset: {
      needleHi:'#6f9a55', needleLight:'#4c7a44', needleMid:'#315733', needleShadow:'#1e3a26', needleDark:'#132618',
      rim:'#f3c065', rimHot:'#ffe39a', rimSoft:'#d9924a',                 // warm backlit glow
      trunkHi:'#6a4a2c', trunkMid:'#3f2c1a', trunkDark:'#241610',
      snagHi:'#8a755a', snagMid:'#574836', snagDark:'#2f2619',
      ground:'rgba(26,20,30,0.42)', groundWarm:'rgba(120,74,40,0.16)',
      haze:'#b79a86', hazeDeep:'#8a7182',
      lightDir:-1,                                                        // sun from left
    },
    moody: {
      needleHi:'#79ad6b', needleLight:'#4f8a4d', needleMid:'#2f5a38', needleShadow:'#1c3a25', needleDark:'#112417',
      rim:'#9fd08c', rimHot:'#c4e6a8', rimSoft:'#6f9a63',
      trunkHi:'#5a4127', trunkMid:'#37281a', trunkDark:'#1f150d',
      snagHi:'#5f6558', snagMid:'#3b4038', snagDark:'#222620',
      ground:'rgba(8,13,10,0.5)', groundWarm:'rgba(40,60,44,0.14)',
      haze:'#2a3a30', hazeDeep:'#18261e',
      lightDir:-1,
    },
  };

  // snow load — cool, catches a little of the warm rim on the sunlit side
  const SNOW = { hi:'#eef4fb', mid:'#c7d6e8', low:'#93a6bd', shadow:'#5f7186' };

  // ---------------------------------------------------------------- species geometry
  function conifer(opts){
    const {W,H,cx,top,base,N,wMax,wPow,round,thick0,thick1,droop0,droop1,gap,pine}=opts;
    const tiers=[];
    for(let i=0;i<N;i++){
      const fr=i/(N-1);
      const y = Math.round(top + (base-top)*fr);
      let w;
      if(round) w = 3 + wMax*Math.pow(Math.sin(Math.PI*(0.06+0.9*fr)),0.82); // pine: rounded, narrows top+base
      else      w = 3 + wMax*Math.pow(fr,wPow);                  // spruce: narrow spire → wide base
      const jit = round ? (hash(i*3.3,i*1.7)-0.5)*6 : 0;         // irregular pine crown
      w += jit;
      const thick = thick0 + (thick1-thick0)*fr;
      const droop = droop0 + (droop1-droop0)*fr;
      tiers.push({y, w, thick, droop, fr});
    }
    return {W,H,cx,top,base,ground:H-8,tiers,gap:!!gap,pine:!!pine,kind:'conifer'};
  }

  const SPECS = {
    spruce: conifer({W:96,H:196,cx:48,top:12,base:158,N:12,wMax:41,wPow:1.18,
                     thick0:5,thick1:15,droop0:5,droop1:11}),
    pine:   conifer({W:108,H:184,cx:54,top:12,base:150,N:9,wMax:44,round:true,
                     thick0:6,thick1:13,droop0:10,droop1:19,gap:true,pine:true}),
    snag:   {W:84,H:184,cx:42,top:10,base:150,ground:176,kind:'snag',
             stubs:[{y:44,dir:1,len:22,drop:9},{y:62,dir:-1,len:26,drop:12},
                    {y:82,dir:1,len:18,drop:8},{y:96,dir:-1,len:20,drop:14},
                    {y:118,dir:1,len:14,drop:10}]},
    ridge:  {W:44,H:70,cx:22,top:6,base:56,ground:62,kind:'ridge',N:9,wMax:17},
  };

  // ---------------------------------------------------------------- motion
  // returns horizontal shift (logical px) for a tier at height-fraction hf (0 top..1 base)
  function motion(mode, f, hf, seed){
    const ph = 2*Math.PI*f/FRAMES;
    if(mode==='sway'){
      // DREAD gust — builds slow, then surges; the crown whips and shudders, and the whole
      // tree lurches one way at the peak like something is leaning on it. Seamless over 12f.
      const raw  = 0.5 - 0.5*Math.cos(ph);
      const gust = Math.pow(raw, 1.7);                            // lingers still, then convulses
      const lag  = hf*2.15;                                       // boughs trail hard up the crown
      const whip   = (5.2*(1-hf*0.12))*Math.sin(ph - lag + seed*0.6)*gust;
      const twist  = 1.7*gust*Math.sin(2*ph + seed*1.3);          // asymmetric second-harmonic twist
      const tremor = 0.75*gust*(1-hf*0.35)*Math.sin(6*ph + seed*3);// high-freq shudder
      const lurch  = 0.95*gust*(0.5+hf*0.5);                       // biased lean toward one side
      return whip + twist + tremor + lurch;
    }
    // idle: uneasy breathing shimmer with an occasional micro-twitch (not quite at rest)
    return 0.9*(0.4+hf*0.6)*Math.sin(ph + hf*2.1 + seed)
         + 0.34*hf*Math.sin(3*ph + seed*2.0);
  }

  // ---------------------------------------------------------------- pixel put
  function makePx(ctx,ox,oy,W,H){
    return (x,y,c,a)=>{
      if(x<0||x>=W||y<0||y>=H) return;
      if(a!=null){ ctx.globalAlpha=a; ctx.fillStyle=c; ctx.fillRect(ox+x,oy+y,1,1); ctx.globalAlpha=1; }
      else { ctx.fillStyle=c; ctx.fillRect(ox+x,oy+y,1,1); }
    };
  }

  // ---------------------------------------------------------------- trunk
  function drawTrunk(px,cx,y0,y1,narrow,T){
    const w=narrow?1:2;
    for(let y=y0;y<=y1;y++){
      for(let x=-w;x<=w;x++){
        const t=(x+w)/(2*w||1);
        let c = t<0.34?T.trunkHi : t<0.7?T.trunkMid : T.trunkDark;
        if(hash(x*5.1,y*2.3)>0.86) c=T.trunkDark;                // bark striation
        else if(hash(x*3.7,y*1.9)<0.1) c=T.trunkHi;
        px(cx+x,y,c);
      }
      if(y%9===4) px(cx, y, T.trunkDark);                        // knot rows
    }
  }

  // ---------------------------------------------------------------- one bough shelf
  function drawBough(px,cx,t,dx,f,T,seed,snow){
    const w=Math.max(1,Math.round(t.w));
    const light=T.lightDir; // -1 lit-left
    for(let ix=-w; ix<=w; ix++){
      const ax=Math.abs(ix)/w;
      const topE=Math.round(t.y - t.thick*Math.pow(1-ax,0.75)); // feathered: tapers to a point at the tip
      const botE=Math.round(t.y + t.droop*Math.pow(ax,1.4));    // tips droop down
      const lx=Math.round(cx+ix+dx);
      const lit = (ix*light) < 0;                                // toward the sun
      const span=Math.max(1,botE-topE);
      for(let yy=topE; yy<=botE; yy++){
        const d=(yy-topE)/span;
        let c;
        if(d<0.16) c=lit?T.needleHi:T.needleLight;
        else if(d<0.42) c=lit?T.needleLight:T.needleMid;
        else if(d<0.68) c=T.needleMid;
        else if(d<0.86) c=T.needleShadow;
        else c=T.needleDark;                                     // sub-canopy underside
        const h=hash(lx*2.3+seed*7, yy*1.7);
        if(h>0.93 && d<0.6) c=mix(c,T.needleHi,0.6);             // needle sparkle
        else if(h<0.07) c=mix(c,T.needleDark,0.5);
        px(lx,yy,c);
      }
      // backlit rim: top contour + outer tips catch the sun
      const rimc = ax>0.72 ? T.rim : T.rimSoft;
      if(hash(lx*1.3+seed, f*3+topE)>0.30) px(lx, topE, ax>0.55?T.rim:mix(T.needleHi,T.rim,0.5));
      if(ax>0.82){ // glowing edge where the bough is thin
        const tip=botE;
        px(lx, tip, hash(lx,f)>0.4? T.rimHot : rimc);
      }
    }
    // drooping needle fringe under the shelf tips, flickers per frame (alive)
    for(let ix=-w; ix<=w; ix+=1){
      if(hash(ix*1.9+seed*4, 21)<0.55) continue;
      const ax=Math.abs(ix)/w;
      const botE=Math.round(t.y + t.droop*Math.pow(ax,1.35));
      const lx=Math.round(cx+ix+dx);
      const fl=hash(lx+seed*3, f*7+3);
      if(fl>0.55) px(lx, botE+1, fl>0.8?T.needleShadow:T.needleMid);
    }
    // SNOW LOAD — a chunky cap mounded on the up-facing top contour; heaviest on upper tiers
    if(snow){
      const light=T.lightDir;
      for(let ix=-w; ix<=w; ix++){
        const ax=Math.abs(ix)/w;
        if(ax>0.94) continue;
        const topE=Math.round(t.y - t.thick*Math.pow(1-ax,0.75));
        const lx=Math.round(cx+ix+dx);
        const load=0.55 + 0.45*(1-t.fr);                          // crown holds the most
        if(hash(lx*1.7+seed*5, topE*1.3) > 0.30+0.62*load) continue;
        const lit=(ix*light)<0;
        const cap= lit ? mix(SNOW.hi, T.rimHot, 0.30) : SNOW.hi;   // sunlit snow warms slightly
        const mound = Math.round((1.4 + 2.2*load)*(1-Math.pow(ax,1.5)));  // thicker toward the spine
        px(lx, topE, ax>0.8 ? SNOW.low : cap);                    // base course
        for(let d=1; d<=mound; d++){
          const c = d>=mound ? SNOW.mid : (d===1? cap : (hash(lx, topE-d)>0.5?SNOW.hi:SNOW.mid));
          px(lx, topE-d, c);
        }
        if(ax>0.5) px(lx, topE+1, SNOW.shadow, 0.55);             // shaded lip where snow overhangs
        if(mound>=3 && ax<0.3) px(lx, topE-mound-1, SNOW.hi, 0.8);// bright crest on deep drifts
      }
    }
  }

  // ---------------------------------------------------------------- ground shadow
  function drawGround(px,cx,cy,T,rx){
    const ry=Math.max(2,rx*0.22);
    for(let y=-ry;y<=ry;y++) for(let x=-rx;x<=rx*1.3;x++){   // stretched toward the shade side
      const nx=x/(rx*1.15), ny=y/ry;
      if(nx*nx+ny*ny<=1){ px(cx+x,Math.round(cy+y),T.ground); if(x<0&&hash(x,y)>0.6) px(cx+x,Math.round(cy+y),T.groundWarm); }
    }
  }

  // ---------------------------------------------------------------- species painters
  function drawConiferTree(px,S,f,T,mode,snow){
    const cx=S.cx;
    drawGround(px,cx,S.ground,T,Math.round(S.tiers[S.tiers.length-1].w*0.9));
    if(snow){ const gy=S.ground, gw=Math.round(S.tiers[S.tiers.length-1].w*0.7); for(let x=-gw;x<=gw;x++){ if(hash(x,gy)>0.4) px(cx+x, gy-1, x*T.lightDir<0?SNOW.mid:SNOW.low); } } // snow drift at the base
    drawTrunk(px,cx,S.base-6,S.ground-1,S.W<90,T);
    // top → bottom so lower boughs overlap the ones above
    for(let i=0;i<S.tiers.length;i++){
      const t=S.tiers[i];
      const dx=motion(mode,f,1-t.fr,i*0.7);
      // crisp leader spike at the very top
      if(i===0){ const spike={...t,w:Math.max(1,Math.round(t.w*0.5)),thick:t.thick+3}; drawBough(px,cx,spike,dx,f,T,i,snow); }
      else drawBough(px,cx,t,dx,f,T,i,snow);
    }
  }

  function drawSnagTree(px,S,f,T,mode,snow){
    const cx=S.cx;
    drawGround(px,cx,S.ground,T,16);
    // leaning dead trunk
    for(let y=S.top;y<=S.ground-1;y++){
      const fr=(y-S.top)/(S.ground-S.top);
      const lean=Math.round(motion(mode,f,1-fr,3)*0.5 + Math.sin(fr*3)*1.5*(1-fr));
      const w=Math.round(1+fr*1.8);
      for(let x=-w;x<=w;x++){
        const t=(x+w)/(2*w||1);
        let c=t<0.34?T.snagHi:t<0.7?T.snagMid:T.snagDark;
        if(hash(x*4.3,y*1.5)>0.82) c=T.snagDark;                // cracked bark
        else if(hash(x*2.9,y*2.1)<0.12) c=T.snagHi;
        px(cx+x+lean,y,c);
      }
      if(hash(0,y)>0.9) px(cx+lean, y, T.rimSoft);              // rim catch on the spine
    }
    // broken branch stubs
    for(const st of S.stubs){
      const swayx=Math.round(motion(mode,f,1-(st.y-S.top)/(S.ground-S.top),st.y)*0.6);
      for(let k=0;k<st.len;k++){
        const x=cx+st.dir*(2+k)+swayx;
        const y=Math.round(st.y + st.drop*Math.pow(k/st.len,1.6));
        const t=k/st.len;
        px(x,y, t<0.5?T.snagMid:T.snagDark);
        px(x,y-1, t<0.3?T.snagHi:T.snagMid);
        if(k===st.len-1) px(x, y-1, T.rim);                     // lit broken tip
      }
    }
    // snow clings to the up-facing sides of the dead limbs + a cap on the broken top
    if(snow){
      for(const st of S.stubs){
        const swayx=Math.round(motion(mode,f,1-(st.y-S.top)/(S.ground-S.top),st.y)*0.6);
        for(let k=1;k<st.len;k+=2){ const x=cx+st.dir*(2+k)+swayx; const y=Math.round(st.y + st.drop*Math.pow(k/st.len,1.6)); if(hash(x,y)>0.4) px(x,y-1, SNOW.mid); }
      }
      for(let x=-2;x<=2;x++){ const yy=S.top+(2-Math.abs(x)); px(cx+x,yy, Math.abs(x)<2?SNOW.hi:SNOW.low); }
    }
  }

  function drawRidgeTree(px,S,f,T,mode,snow){
    const cx=S.cx;
    const core=window.TreeGen.mix(T.needleDark,T.hazeDeep,0.35);   // dark silhouette
    const body=window.TreeGen.mix(core,T.haze,0.22);
    const lit=window.TreeGen.mix(core,T.rim,0.18);
    const dx=motion(mode,f,1,0)*0.5;
    // SOLID filled conifer silhouette (no gaps) — a soft dark shape against the far sky
    let prevW=0;
    for(let y=S.top;y<=S.base;y++){
      const fr=(y-S.top)/(S.base-S.top);
      let w=Math.round(1+S.wMax*Math.pow(fr,1.15));
      const jag=(hash(y*2.7,y)>0.68)?1:0;                          // ragged conifer edge
      w=Math.max(prevW-1,w-jag); prevW=w;
      const ox=Math.round(dx*(1-fr));
      for(let x=-w;x<=w;x++){
        const ax=Math.abs(x)/(w||1);
        let c = ax>0.82 ? (x<0?lit:core) : (x<0?body:core);
        if(snow && fr<0.55 && ax<0.55 && hash(y*1.3, x*2.1)>0.62) c = mix(c, SNOW.low, 0.55); // dusting on the far ridge
        px(cx+x+ox, y, c);
      }
    }
    for(let y=S.base;y<S.ground;y++) px(cx,y,core);                 // stub trunk
  }

  function drawTree(ctx, ox, oy, f, opts){
    opts=opts||{};
    const species=opts.species||'spruce';
    const T=THEMES[opts.theme||'sunset'];
    const mode=opts.mode||'sway';
    const S=SPECS[species];
    const px=makePx(ctx,ox,oy,S.W,S.H);
    const snow=!!opts.snow;
    if(S.kind==='snag') drawSnagTree(px,S,f,T,mode,snow);
    else if(S.kind==='ridge') drawRidgeTree(px,S,f,T,mode,snow);
    else drawConiferTree(px,S,f,T,mode,snow);
  }

  // ---------------------------------------------------------------- atlas (cached frames)
  const _atlas={};
  function buildAtlas(species,theme,mode,snow,factory){
    const key=species+'|'+theme+'|'+mode+'|'+(snow?'s':'0');
    if(_atlas[key]) return _atlas[key];
    const S=SPECS[species];
    const mk = factory || ((w,h)=>{ const c=document.createElement('canvas'); c.width=w; c.height=h; return c; });
    const frames=[];
    for(let f=0;f<FRAMES;f++){
      const c=mk(S.W,S.H); const cx=c.getContext('2d'); cx.imageSmoothingEnabled=false;
      drawTree(cx,0,0,f,{species,theme,mode,snow});
      frames.push(c);
    }
    return (_atlas[key]={frames,W:S.W,H:S.H});
  }

  // ---------------------------------------------------------------- sheet (PNG export)
  function renderSheet(ctx, species, theme, snow){
    const S=SPECS[species];
    ctx.clearRect(0,0,S.W*COLS,S.H*ROWS);
    for(let f=0; f<FRAMES; f++){                                 // idle rows 0-1
      drawTree(ctx,(f%COLS)*S.W, Math.floor(f/COLS)*S.H, f, {species,theme,mode:'idle',snow});
    }
    for(let f=0; f<FRAMES; f++){                                 // sway rows 2-3
      drawTree(ctx,(f%COLS)*S.W, (2+Math.floor(f/COLS))*S.H, f, {species,theme,mode:'sway',snow});
    }
  }

  const SPECIES=Object.keys(SPECS);
  return { FRAMES, COLS, ROWS, SPECS, SPECIES, THEMES, mix, drawTree, buildAtlas, renderSheet };
})();
if (typeof module !== 'undefined') module.exports = window.TreeGen;
