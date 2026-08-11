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
    return {W,H,cx,top,base,ground:H-8,tiers,gap:!!gap,pine:!!pine,kind:'conifer',mass:opts.mass||0,overgrown:!!opts.overgrown};
  }

  const SPECS = {
    spruce: conifer({W:96,H:196,cx:48,top:12,base:158,N:12,wMax:41,wPow:1.18,
                     thick0:5,thick1:15,droop0:5,droop1:11,mass:0.12}),
    pine:   conifer({W:108,H:184,cx:54,top:12,base:150,N:9,wMax:44,round:true,
                     thick0:6,thick1:13,droop0:10,droop1:19,gap:true,pine:true,mass:0.18}),
    // overgrown giant conifer — huge, many tiers, boughs sag heavy, hung with moss
    bigconifer: conifer({W:130,H:262,cx:65,top:14,base:226,N:16,wMax:56,wPow:1.22,
                     thick0:6,thick1:21,droop0:10,droop1:26,mass:0.8,overgrown:true}),
    snag:   {W:84,H:184,cx:42,top:10,base:150,ground:176,kind:'snag',
             stubs:[{y:44,dir:1,len:22,drop:9},{y:62,dir:-1,len:26,drop:12},
                    {y:82,dir:1,len:18,drop:8},{y:96,dir:-1,len:20,drop:14},
                    {y:118,dir:1,len:14,drop:10}],mass:0.4},
    ridge:  {W:44,H:70,cx:22,top:6,base:56,ground:62,kind:'ridge',N:9,wMax:17,mass:0},
    // GARGANTUAN bare oak — wide fractal branching, no leaves, massive buttressed furrowed trunk
    bareoak: {W:170,H:262,cx:85,ground:254,kind:'bare',mass:1,
              splitY:126,baseHW:16,topHW:6,
              mains:[{a:-1.02,len:80,th:8},{a:-0.42,len:98,th:9},{a:0.32,len:94,th:9},
                     {a:1.05,len:74,th:7},{a:-0.06,len:76,th:6}]},
    // overgrown broadleaf — massive dark sagging leaf-dome, hung with vines/moss
    broadleaf: {W:190,H:236,cx:95,ground:228,kind:'broadleaf',mass:0.85,
                trunkTop:132,trunkHW:10,crownCY:86,crownRX:84,crownRY:70},
  };

  // ---------------------------------------------------------------- motion
  // returns horizontal shift (logical px) for a tier at height-fraction hf (0 top..1 base)
  function motion(mode, f, hf, seed, mass){
    mass = mass||0;
    const ph = 2*Math.PI*f/FRAMES;
    const light = 1 - mass;                                       // how much quick, sharp detail survives
    if(mode==='sway'){
      // DREAD gust — builds slow, then surges; the crown whips and shudders, and the whole
      // tree lurches one way at the peak like something is leaning on it. Seamless over 12f.
      // GIANTS (high mass) move slower & heavier: the fast tremor/whip is damped and a deep,
      // low creak (the fundamental) takes over — a huge groaning mass, not a fluttering twig.
      const raw  = 0.5 - 0.5*Math.cos(ph);
      const gust = Math.pow(raw, 1.7 + mass*1.2);                  // heavier = lingers still far longer
      const lag  = hf*2.15;
      const whip   = (5.2*(1-hf*0.12))*Math.sin(ph - lag + seed*0.6)*gust*(0.4+0.6*light);
      const twist  = 1.7*gust*Math.sin(2*ph + seed*1.3)*light;
      const tremor = 0.75*gust*(1-hf*0.35)*Math.sin(6*ph + seed*3)*light*light;
      const lurch  = 0.95*gust*(0.5+hf*0.5)*(0.6+0.4*light);
      const creak  = mass*3.6*(0.3+hf*0.7)*Math.sin(ph + seed*0.4)*Math.pow(raw,0.85); // slow deep groan
      return whip + twist + tremor + lurch + creak;
    }
    // idle: uneasy breathing shimmer with a micro-twitch; giants add a slow heavy settle
    return 0.9*(0.4+hf*0.6)*Math.sin(ph + hf*2.1 + seed)*(0.5+0.5*light)
         + 0.34*hf*Math.sin(3*ph + seed*2.0)*light
         + mass*0.8*(0.3+hf*0.7)*Math.sin(ph + seed*0.3);
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
      const dx=motion(mode,f,1-t.fr,i*0.7,S.mass);
      // crisp leader spike at the very top
      if(i===0){ const spike={...t,w:Math.max(1,Math.round(t.w*0.5)),thick:t.thick+3}; drawBough(px,cx,spike,dx,f,T,i,snow); }
      else drawBough(px,cx,t,dx,f,T,i,snow);
    }
    // overgrown: shaggy moss/vine strands dangling off the mid boughs, swaying at the tips
    if(S.overgrown){
      for(let i=0;i<9;i++){
        const tt=S.tiers[4 + (i*2)%(S.tiers.length-5)]; if(!tt) continue;
        const side=(i%2)?1:-1;
        const x=cx+side*Math.round(tt.w*0.72);
        const y=Math.round(tt.y+tt.droop*0.6);
        const vl=6+Math.round(hash(i,4)*16);
        const vs=motion(mode,f,1,i*1.3,S.mass)*0.4;
        for(let k=0;k<vl;k++){
          const c = k>vl-3 ? T.rimSoft : mix(T.needleShadow,T.trunkDark,0.35);
          px(Math.round(x+vs*(k/vl)), y+k, c);
        }
      }
    }
  }

  // ---------------------------------------------------------------- recursive bare branch
  function drawBranch(px,x0,y0,ang,len,th,depth,T,f,mode,mass){
    const steps=Math.max(2,Math.round(len));
    const bend = motion(mode,f,Math.min(1,0.35+depth*0.18),depth*1.7,mass)*0.02*(depth+1);
    let x=x0,y=y0;
    for(let k=0;k<steps;k++){
      const kfr=k/steps;
      const a = ang + bend*kfr;                    // tips whip; base stays planted
      x += Math.sin(a); y -= Math.cos(a);
      const thK = Math.max(0.6, th*(1-kfr*0.82));
      const pr = Math.max(0, Math.round(thK));
      const cxp=Math.cos(a), cyp=Math.sin(a);      // perpendicular across the limb
      for(let o=-pr;o<=pr;o++){
        const bx=Math.round(x+cxp*o), by=Math.round(y+cyp*o);
        const lit=(cxp*o)<0;                       // toward the sun (left)
        const edge=Math.abs(o)>=pr;
        let c = depth<=1 ? (lit?T.trunkMid:T.trunkDark) : (lit?T.snagMid:T.trunkDark);
        if(edge && lit && thK>1.2) c=T.trunkHi;
        if(edge && !lit) c=T.trunkDark;
        px(bx,by,c);
      }
      if(thK>1.6 && hash(k,depth)>0.5) px(Math.round(x-Math.abs(cxp)*pr), by(y), T.rimSoft, 0.6);
    }
    function by(v){ return Math.round(v); }
    if(depth<4 && th>1.6){
      const n = depth<2?3:2;
      for(let i=0;i<n;i++){
        const h=hash(x0*1.3+depth*7+i*3, y0*0.9+i*2);
        const spread=(i-(n-1)/2)*(0.55+0.12*depth)+(h-0.5)*0.4;
        drawBranch(px,x,y,ang+bend+spread,len*(0.62+h*0.14),th*0.6,depth+1,T,f,mode,mass);
      }
    } else {
      // clawing bare twigs at the tip
      const n=3+Math.round(hash(x,y)*2);
      for(let i=0;i<n;i++){
        const h=hash(x*2+i,y*2+depth);
        const tang=ang+bend+(i-(n-1)/2)*0.5+(h-0.5)*0.45;
        let tx=x,ty=y; const tl=5+Math.round(h*7);
        for(let k=0;k<tl;k++){ tx+=Math.sin(tang); ty-=Math.cos(tang);
          const lit=Math.sin(tang)<0;
          px(Math.round(tx),Math.round(ty), k>tl-2?T.rimSoft:(lit?T.snagMid:T.trunkDark)); }
      }
    }
  }

  // ---------------------------------------------------------------- GARGANTUAN bare oak
  function drawBareOak(px,S,f,T,mode,snow){
    const cx=S.cx, gy=S.ground, splitY=S.splitY, baseHW=S.baseHW, topHW=S.topHW, rootZone=36;
    drawGround(px,cx,gy,T,Math.round(baseHW*2.6));
    const hwAt=(y)=>{ const fr=(gy-y)/(gy-splitY); let hw=topHW+(baseHW-topHW)*Math.pow(1-fr,1.5);
      if(y>gy-rootZone){ const rz=(y-(gy-rootZone))/rootZone; hw+=rz*rz*11; } return hw; };
    const leanAt=(y)=>{ const fr=Math.max(0,(gy-y)/(gy-splitY)); return Math.round(motion(mode,f,fr,2,S.mass)*0.45*fr); };
    // buttress roots first (behind trunk)
    for(const dir of [-1,-0.5,0.5,1]){
      const rl=15+Math.round(hash(dir*7,0)*9);
      const rx0=cx+dir*baseHW*0.5, ry0=gy-rootZone+8;
      for(let k=0;k<rl;k++){ const kf=k/rl, w=Math.max(1,Math.round(4.5*(1-kf)));
        const xx=rx0+dir*k*1.15, yy=ry0+k*1.5;
        for(let o=-w;o<=w;o++){ const t=(o+w)/(2*w||1); px(Math.round(xx+o),Math.round(yy), t<0.4?T.trunkMid:T.trunkDark); }
      }
    }
    // massive furrowed trunk
    for(let y=gy-1;y>=splitY;y--){
      const hw=Math.max(1,Math.round(hwAt(y))), lean=leanAt(y);
      for(let x=-hw;x<=hw;x++){
        const t=(x+hw)/(2*hw||1);
        let c=t<0.3?T.trunkHi:t<0.62?T.trunkMid:T.trunkDark;
        const groove=Math.sin(x*1.5+Math.sin(y*0.05))*Math.sin(x*0.6);   // deep vertical furrows
        if(groove>0.55) c=T.trunkDark;
        const n=hash(x*3.1,y*1.3); if(n>0.9)c=T.trunkDark; else if(n<0.08)c=T.trunkHi;
        px(cx+x+lean,y,c);
      }
      if(hash(0,y)>0.84) px(cx-hw+lean,y,T.rimSoft);                      // lit spine catches
    }
    // knots & a hollow scar
    for(let i=0;i<3;i++){ const ky=splitY+28+i*38, kx=cx+(i%2?5:-5);
      for(let a=0;a<6.28;a+=0.4) px(Math.round(kx+Math.cos(a)*3),Math.round(ky+Math.sin(a)*2.4),T.trunkDark);
      px(kx,ky-3,T.trunkHi); px(kx,ky,T.trunkDark); }
    const sy=splitY+66; for(let y=sy;y<sy+24;y++){ const w=Math.round(3*Math.sin((y-sy)/24*Math.PI));
      for(let x=-w;x<=w;x++) px(cx+x-4,y,T.trunkDark); if(w>0) px(cx-4-w,y,T.trunkHi); }
    // fractal bare crown
    const bx=cx+leanAt(splitY);
    for(const m of S.mains) drawBranch(px,bx,splitY,m.a,m.len,m.th,1,T,f,mode,S.mass);
    // snow settles along the upper limbs
    if(snow){ for(let i=0;i<60;i++){ const a=hash(i,3)*6.28, r=hash(i,7)*82;
      const x=cx+Math.cos(a)*r, y=splitY-6-Math.abs(Math.sin(a))*80*hash(i,9);
      if(y>4) px(Math.round(x),Math.round(y), hash(i,11)>0.5?SNOW.mid:SNOW.low); } }
  }

  // ---------------------------------------------------------------- overgrown broadleaf
  function drawBroadleaf(px,S,f,T,mode,snow){
    const cx=S.cx, gy=S.ground;
    drawGround(px,cx,gy,T,Math.round(S.crownRX*0.9));
    const shift=Math.round(motion(mode,f,1,0,S.mass)*0.7);
    // stout trunk
    for(let y=gy-1;y>=S.trunkTop;y--){
      const fr=(gy-y)/(gy-S.trunkTop);
      let hw=Math.max(1,Math.round(S.trunkHW*(1-fr*0.42) + (y>gy-22?((y-(gy-22))/22)*5:0)));
      for(let x=-hw;x<=hw;x++){ const t=(x+hw)/(2*hw||1);
        let c=t<0.3?T.trunkHi:t<0.62?T.trunkMid:T.trunkDark;
        const n=hash(x*3.1,y*1.3); if(n>0.88)c=T.trunkDark; else if(n<0.09)c=T.trunkHi;
        px(cx+x,y,c); }
    }
    for(const m of [{a:-0.55,len:44,th:4},{a:0.32,len:48,th:4},{a:-0.05,len:40,th:4}])
      drawBranch(px,cx,S.trunkTop,m.a,m.len,m.th,2,T,f,mode,S.mass);
    // sagging dome of leaf clumps
    const clumps=[];
    for(let v=-1;v<=1.02;v+=0.15) for(let u=-1;u<=1.02;u+=0.13){
      if(u*u+v*v>1) continue; if(hash(u*13.3+7,v*11.1+3)<0.30) continue;
      clumps.push({x:cx+u*S.crownRX+shift, y:S.crownCY+v*S.crownRY+(v>0?v*v*12:0), u, v, r:5+hash(u*3,v*3)*4});
    }
    clumps.sort((a,b)=>a.y-b.y);                                          // paint top clumps first
    for(const cl of clumps){
      const r=cl.r;
      for(let dy=-r;dy<=r;dy++) for(let dx=-r;dx<=r;dx++){
        if(dx*dx+dy*dy>r*r) continue;
        const ny=dy/r;
        let c = ny<-0.2 ? (cl.u<0?T.needleHi:T.needleLight) : ny>0.45 ? T.needleDark : T.needleMid;
        const n=hash((cl.x+dx)*1.7,(cl.y+dy)*1.3);
        if(n>0.86)c=mix(c,T.needleHi,0.4); else if(n<0.12)c=mix(c,T.needleDark,0.5);
        px(Math.round(cl.x+dx),Math.round(cl.y+dy),c);
      }
      if(cl.v<-0.2) px(Math.round(cl.x-r*0.6),Math.round(cl.y-r*0.7),T.rim);
    }
    // backlit rim along the whole crown top
    for(let a=-1;a<=1;a+=0.045){ const x=cx+a*S.crownRX*0.92+shift, y=S.crownCY-Math.sqrt(Math.max(0,1-a*a))*S.crownRY;
      if(hash(x*1.1,y)>0.32) px(Math.round(x),Math.round(y-1),T.rim); }
    // vines / moss dangling from the underside, swaying at the tips
    for(let i=0;i<8;i++){ const a=hash(i,2)*2-1; const x=cx+a*S.crownRX*0.72+shift;
      let y=S.crownCY+Math.sqrt(Math.max(0,1-a*a))*S.crownRY*0.9;
      const vl=8+Math.round(hash(i,5)*18), vs=motion(mode,f,1,i,S.mass)*0.5;
      for(let k=0;k<vl;k++) px(Math.round(x+vs*(k/vl)), Math.round(y+k), k>vl-3?T.rimSoft:mix(T.needleShadow,T.trunkDark,0.4)); }
    // snow caps on the up-facing clumps
    if(snow){ for(const cl of clumps){ if(cl.v<-0.1){ for(let dx=-cl.r;dx<=cl.r;dx++){
      if(hash(cl.x+dx,cl.y)>0.5) px(Math.round(cl.x+dx),Math.round(cl.y-cl.r*0.8),cl.u<0?SNOW.hi:SNOW.mid); } } } }
  }

  function drawSnagTree(px,S,f,T,mode,snow){
    const cx=S.cx;
    drawGround(px,cx,S.ground,T,16);
    // leaning dead trunk
    for(let y=S.top;y<=S.ground-1;y++){
      const fr=(y-S.top)/(S.ground-S.top);
      const lean=Math.round(motion(mode,f,1-fr,3,S.mass)*0.5 + Math.sin(fr*3)*1.5*(1-fr));
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
      const swayx=Math.round(motion(mode,f,1-(st.y-S.top)/(S.ground-S.top),st.y,S.mass)*0.6);
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
        const swayx=Math.round(motion(mode,f,1-(st.y-S.top)/(S.ground-S.top),st.y,S.mass)*0.6);
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
    const dx=motion(mode,f,1,0,S.mass)*0.5;
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
    else if(S.kind==='bare') drawBareOak(px,S,f,T,mode,snow);
    else if(S.kind==='broadleaf') drawBroadleaf(px,S,f,T,mode,snow);
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
