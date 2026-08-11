// truckgen.js — the roadside arrival vehicle: a rounded 1940s–50s off-road wagon/truck,
// bulbous fenders, whitewalls gone muddy, glossy-but-ruined paint, snow + dirt caked from
// mountain driving. Authored as 2.5D ELEVATION sprites (same low storybook angle as the
// player/neighbors), 64×32 per cell (4×2 world tiles), 7 frames per direction:
//
//   0–3  ROLL  — wheels spin (parked/idle base; also the driving loop). Body identical.
//   4–6  DOOR  — the near door swings open (climb-out). Back views drop the tailgate.
//
// 8-way facing from FIVE authored views, the other three are horizontal mirrors:
//   front (S) · back (N) · side (E, faces right) · front3q (SE) · back3q (NE)
//   left(W)=flip side · SW=flip front3q · NW=flip back3q
//
// Two forms share one palette:
//   home      — faded teal paint, cream hardtop, warm rust, headlights glow.
//   nightmare — same shapes pushed COLD + drained: dead grey headlights, deeper rust,
//               grime crawls higher, glass goes black. (horror is in the idle TIMING —
//               driven at playback, see ROADSIDE.md — not baked here.)
//
//   eval(await readFile('truckgen.js'));
//   window.__tkSave=saveFile; window.__tkCanvas=createCanvas;
//   await window.Truck.build({view:'side', form:'home'});   // -> truck_side.png (+ -8x)

window.Truck = (function(){
  const W=64, H=32, FRAMES=7;

  const hx=(h)=>{h=h.replace('#','');return [parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];};

  const PAL = {
    home: {
      body:'#3f6b63', bodyHi:'#568c81', bodySh:'#2b4a44', bodyDk:'#1d3632',
      roof:'#c7bd9c', roofHi:'#ded4b2', roofSh:'#9c9276',
      rust:'#7f4e2f', rustHi:'#9c6a40', rustDk:'#552f1c',
      chrome:'#bcc3c7', chromeHi:'#e6ebed', chromeSh:'#7b838a',
      glass:'#33505a', glassHi:'#9fb9c2', glassSh:'#21343b',
      head:'#f4e7a6', headHi:'#fdf8dc', headRim:'#b09a48', headOn:true,
      tail:'#b8452e', tailHi:'#e57044',
      amber:'#e7a02f', amberHi:'#ffce6b',
      tire:'#1b1a19', tireHi:'#34322f', wall:'#c6bca6', wallSh:'#8f8368', mud:'#7a5c37',
      hub:'#9aa1a6', hubHi:'#dbe0e3',
      snow:'#e6ecef', snowSh:'#bcc6cb', dirt:'#6a5236', dirtDk:'#463620',
      out:'#141110', shadow:'#100d0b',
    },
    nightmare: {
      body:'#3b4a47', bodyHi:'#4d5d59', bodySh:'#26302e', bodyDk:'#161d1b',
      roof:'#8d887a', roofHi:'#a29b8a', roofSh:'#696556',
      rust:'#5c3a27', rustHi:'#754f36', rustDk:'#381f14',
      chrome:'#8d969a', chromeHi:'#adb5b8', chromeSh:'#585f63',
      glass:'#1f2a2f', glassHi:'#44565d', glassSh:'#12191d',
      head:'#79857a', headHi:'#8f9a8b', headRim:'#454b3c', headOn:false,
      tail:'#5a2a20', tailHi:'#7a3a2a',
      amber:'#7d6a34', amberHi:'#9c8748',
      tire:'#131211', tireHi:'#26241f', wall:'#8b8577', wallSh:'#5a5344', mud:'#4c3c26',
      hub:'#6a7175', hubHi:'#909799',
      snow:'#c3cbce', snowSh:'#9199a0', dirt:'#463726', dirtDk:'#2c2216',
      out:'#0b0908', shadow:'#080605',
    },
  };

  // deterministic hash noise (stable per-pixel, so grime never shimmers between frames)
  function rnd(a,b,c){let h=((a|0)*374761393+(b|0)*668265263+((c|0)+41)*2246822519)>>>0;h=((h^(h>>>15))*2246822519)>>>0;h=((h^(h>>>13))*3266489917)>>>0;return ((h^(h>>>16))>>>0)/4294967296;}

  function makeBuf(){
    const B=new Uint8ClampedArray(W*H*4);
    const bi=(x,y)=>(y*W+x)*4;
    const set=(x,y,rgb,a)=>{ if(x<0||x>=W||y<0||y>=H) return; const i=bi(x,y); B[i]=rgb[0];B[i+1]=rgb[1];B[i+2]=rgb[2];B[i+3]=(a==null?255:a*255); };
    const put=(x,y,rgb,a)=>set(x,y,rgb,a);
    const fill=(x0,x1,y0,y1,rgb,a)=>{ for(let y=y0;y<=y1;y++) for(let x=x0;x<=x1;x++) set(x,y,rgb,a); };
    const on=(x,y)=>{ if(x<0||x>=W||y<0||y>=H) return false; return B[bi(x,y)+3]>=128; };
    const getA=(x,y)=>{ if(x<0||x>=W||y<0||y>=H) return 0; return B[bi(x,y)+3]; };
    return {B,bi,set,put,fill,on,getA};
  }

  // ---- whitewall wheel seen from the side (muddy, spinning hub) ----
  function wheelSide(D,P,cx,cy,r,phase){
    const {set,put}=D;
    for(let y=-r;y<=r;y++) for(let x=-r;x<=r;x++){
      const d=Math.hypot(x,y); if(d>r+0.3) continue;
      let c;
      if(d>r-1.15) c=P.tire;                    // outer tread
      else if(d>r-2.35) c=P.wall;               // whitewall ring
      else if(d>r-2.9) c=P.wallSh;
      else c=P.hub;                             // hub cap
      set(cx+x,cy+y,c);
    }
    // muddy splatter on the lower whitewall so it isn't pristine
    for(let y=-r;y<=r;y++) for(let x=-r;x<=r;x++){
      const d=Math.hypot(x,y); if(d>r-1.2||d<r-2.9) continue;
      if(y>0 && rnd(cx+x,cy+y,3)>0.62) set(cx+x,cy+y,P.mud);
    }
    // spinning hub: a bright nub + two spokes rotating through 4 phases
    const ang=phase*(Math.PI/2)+0.4;
    put(cx,cy,P.hubHi);
    for(let k=0;k<2;k++){ const a=ang+k*Math.PI; const sx=Math.round(cx+Math.cos(a)*(r-3)), sy=Math.round(cy+Math.sin(a)*(r-3)); put(sx,sy,P.hubHi); }
    // top rim catches light
    put(cx-1,cy-r+1,P.tireHi); put(cx,cy-r+1,P.tireHi);
  }

  // partial tire arc peeking under a front/back view (just the bottom of the wheel)
  function wheelStub(D,P,cx,groundY,halfW,phase){
    const {set}=D;
    for(let y=0;y<=3;y++) for(let x=-halfW;x<=halfW;x++){
      const d=Math.hypot(x,(y-4)); if(d>halfW+0.4||d<halfW-2.2) continue;
      set(cx+x,groundY-3+y,P.tire);
    }
    // tread flicker to imply rotation
    const t=(phase%2)?1:-1;
    set(cx+t,groundY-1,P.tireHi); set(cx-t,groundY-2,P.tireHi);
  }

  const PAINT = {};

  // ================= SIDE (E) — hero profile, faces RIGHT (front at +x) =================
  PAINT.side = function(D,P,fr,form){
    const {put,fill,on,set}=D; const nm=form==='nightmare';
    const rearWx=17, frontWx=47, wy=25, wr=5;                 // wheel centres, ground y30
    const grime=(D.doorPhase!=null); // unused; keep signature simple

    // ---- ground contact shadow ----
    for(let x=8;x<=58;x++){ const e=(x<12||x>54)?1:0; set(x,31,P.shadow, e?0.5:0.8); }

    // ---- body shell (rocker to belt) ----
    // lower body panel between/over wheels
    fill(9,57,20,29,P.body);
    // rocker (dark sill)
    fill(9,57,28,29,P.bodyDk);
    // fender arches over both wheels
    for(const cxw of [rearWx,frontWx]){
      for(let x=-7;x<=7;x++){ const yy=Math.round(wy-Math.sqrt(Math.max(0,49-x*x))-0.5); for(let y=Math.max(17,yy);y<=19;y++) set(cxw+x,y,P.body); }
      // arch lip highlight + rust at the lower lip
      for(let a=-7;a<=7;a++){ const yy=Math.round(wy-Math.sqrt(Math.max(0,54-a*a))); set(cxw+a,yy,P.bodySh); }
    }
    // wheel wells (cut the body away so wheels sit in arches)
    for(const cxw of [rearWx,frontWx]) for(let y=18;y<=29;y++) for(let x=-6;x<=6;x++){ if(Math.hypot(x,y-wy)<=6.2) D.set(cxw+x,y,[0,0,0],0); }

    // ---- upper body / cabin + wagon hardtop ----
    // hood (front, slopes down toward grille at right)
    fill(44,56,14,20,P.body);
    for(let x=44;x<=56;x++){ const drop=Math.round((x-44)*0.18); fill(x,x,13+drop,13+drop,P.body); }
    // cab roof
    fill(28,45,8,10,P.roof);
    fill(29,44,7,7,P.roof);
    // wagon (rear) hardtop — a touch taller/rounded
    fill(9,28,9,11,P.roof);
    fill(10,27,8,8,P.roof);
    // roof front + rear rounding
    put(28,8,P.roof); put(45,10,P.roof); put(9,10,P.roof);
    // belt line under windows
    fill(9,44,16,16,P.bodyHi);

    // windows: cab door window + windshield + rear wagon window
    fill(30,37,10,15,P.glass);                         // door window
    fill(38,44,10,15,P.glass);                         // windshield (raked)
    for(let y=10;y<=15;y++){ put(44+ (y-10>2?1:0),y,P.glass); }
    fill(12,25,10,15,P.glass);                         // rear wagon window
    // window frame + a sky glint across the top of each
    for(const [gx0,gx1] of [[30,37],[38,44],[12,25]]){ for(let x=gx0;x<=gx1;x++) put(x,10,P.glassHi,0.8); put(gx0,15,P.glassSh); }
    // pillars
    fill(37,38,10,15,P.roofSh); fill(26,27,10,16,P.bodySh);

    // ---- grille + front ----
    fill(56,59,15,26,P.chrome);
    for(let y=16;y<=24;y+=2) fill(56,59,y,y,P.chromeSh);   // grille slats
    put(59,15,P.chromeHi);
    // round headlight
    for(let y=-2;y<=2;y++) for(let x=-2;x<=2;x++){ if(Math.hypot(x,y)<=2.2) set(57+x,17+y, P.headOn?P.head:P.head); }
    for(let y=-2;y<=2;y++) for(let x=-2;x<=2;x++){ if(Math.hypot(x,y)>2.2||Math.hypot(x,y)<1.2) continue; set(57+x,17+y,P.headRim); }
    if(P.headOn){ put(56,16,P.headHi); }
    // front bumper
    fill(56,60,26,27,P.chrome); put(60,26,P.chromeSh);
    // amber blinker (small, on the front fender) — lamp lit state handled in DC overlay; draw the lens
    fill(54,55,24,25,nm?P.amber:P.amber);

    // ---- rear / tailgate + bumper + taillight ----
    fill(6,9,17,27,P.body);
    fill(5,9,26,27,P.chrome);                          // rear bumper
    fill(6,7,19,22,P.tail); put(6,19,P.tailHi);        // taillight
    // spare tire strapped to the back
    for(let y=-3;y<=3;y++) for(let x=-3;x<=3;x++){ const d=Math.hypot(x,y); if(d>3.2) continue; set(6+x,22+y, d>2?P.tire:P.hub); }

    // ---- glossy sheen: a bright diagonal streak over the doors/hood (the "glossy but ruined") ----
    for(let x=14;x<=52;x++){ const y=17-Math.round(Math.sin((x-14)/9)*0.6); if(on(x,y)) put(x,y,P.bodyHi,0.7); }

    // ---- door + handle (side) ----
    const dX0=29,dX1=38;
    fill(dX0,dX0,16,27,P.bodySh);                      // door seam (rear)
    put(dX1,16,P.bodySh);
    put(dX1-1,20,P.chromeHi);                          // handle

    // door OPEN animation (front-hinged at +x; rear edge lifts away, cavity at −x side)
    if(fr.door>0){
      const sw=fr.door;                                // 1..3
      // dark interior cavity
      fill(dX0,dX0+2+sw,16,26,P.bodyDk);
      fill(dX0+1,dX0+2+sw,17,20,P.glassSh);            // seat/interior gloom
      // swung door: a lit vertical panel standing out to the left, foreshortened
      const doorX=dX0-1-sw;
      fill(doorX,doorX+1,15,26,P.body);
      fill(doorX,doorX,15,26,P.bodyHi);
      fill(doorX+1,doorX+1,26,26,P.bodyDk);
      // door window on the swung panel
      fill(doorX,doorX+1,15,20,P.glass);
    }

    // ---- grime pass: snow on the roof, dirt up the flanks & behind wheels ----
    grimePass(D,P,{roofY:[7,11], flankY:[20,29], wells:[rearWx,frontWx], wy});
    // wheels last (on top of arches)
    wheelSide(D,P,rearWx,wy,wr,fr.wheel);
    wheelSide(D,P,frontWx,wy,wr,fr.wheel);
  };

  // ================= FRONT (S) — grille to camera =================
  PAINT.front = function(D,P,fr,form){
    const {put,fill,on,set}=D; const cx=32; const nm=form==='nightmare';
    for(let x=20;x<=44;x++) set(x,31,P.shadow,0.7);
    // front wheels peeking
    wheelStub(D,P,23,30,4,fr.wheel);
    wheelStub(D,P,41,30,4,fr.wheel);
    // body / fenders (bulbous at the sides)
    fill(20,44,14,29,P.body);
    fill(20,44,28,29,P.bodyDk);
    // rounded fender bulges
    for(let y=18;y<=27;y++){ const w=Math.round(Math.sin((y-18)/9*Math.PI)*2); fill(18-w? 18: 18,18,y,y,P.body); }
    fill(17,19,18,27,P.body); fill(45,47,18,27,P.body);
    put(17,18,P.bodySh); put(47,18,P.bodySh);
    // cab roof + windshield
    fill(24,40,8,10,P.roof); fill(25,39,7,7,P.roof);
    fill(24,40,16,16,P.bodyHi);
    fill(25,39,10,15,P.glass);                         // split windshield
    fill(31,32,10,15,P.roofSh);                        // centre divider
    for(let x=25;x<=39;x++) put(x,10,P.glassHi,0.8);
    // grille (vertical chrome bars) + emblem
    fill(27,37,17,26,P.chrome);
    for(let x=28;x<=36;x+=2) fill(x,x,17,25,P.chromeSh);
    put(32,15,P.chromeHi);
    // twin round headlights
    for(const hx0 of [24,40]){
      for(let y=-2;y<=2;y++) for(let x=-2;x<=2;x++){ const d=Math.hypot(x,y); if(d>2.3) continue; set(hx0+x,18+y, d>1.3?P.headRim:P.head); }
      if(P.headOn){ put(hx0-1,17,P.headHi); }
    }
    // amber blinkers beside the grille
    fill(21,22,20,21,P.amber); fill(42,43,20,21,P.amber);
    // front bumper
    fill(19,45,27,28,P.chrome); fill(19,45,28,28,P.chromeSh);
    // little sheen on the roof front
    for(let x=25;x<=39;x++) if(on(x,9)) put(x,9,P.roofHi,0.6);
    grimePass(D,P,{roofY:[7,10], flankY:[22,29], wells:[], wy:0, front:true});
  };

  // ================= BACK (N) — tailgate to camera =================
  PAINT.back = function(D,P,fr,form){
    const {put,fill,on,set}=D; const cx=32; const nm=form==='nightmare';
    for(let x=20;x<=44;x++) set(x,31,P.shadow,0.7);
    wheelStub(D,P,23,30,4,fr.wheel);
    wheelStub(D,P,41,30,4,fr.wheel);
    fill(20,44,14,29,P.body);
    fill(20,44,28,29,P.bodyDk);
    fill(17,19,18,27,P.body); fill(45,47,18,27,P.body);
    // roof + rear wagon window
    fill(24,40,8,10,P.roof); fill(25,39,7,7,P.roof);
    fill(24,40,16,16,P.bodyHi);
    fill(25,39,10,15,P.glass);
    for(let x=25;x<=39;x++) put(x,10,P.glassHi,0.8);
    // tailgate seam + handle
    fill(20,44,21,21,P.bodySh);
    fill(31,33,19,20,P.chromeHi);
    // twin taillights
    for(const tx of [23,41]){ fill(tx-1,tx+1,22,25,P.tail); put(tx-1,22,P.tailHi); }
    // rear bumper + spare tire mounted centre
    fill(19,45,27,28,P.chrome); fill(19,45,28,28,P.chromeSh);
    for(let y=-4;y<=4;y++) for(let x=-4;x<=4;x++){ const d=Math.hypot(x,y); if(d>4.2) continue; set(32+x,24+y, d>3?P.tire: d>1.6?P.wall:P.hub); }

    // TAILGATE DROP on door frames (back views open the gate instead of a door)
    if(fr.door>0){
      const drop=fr.door*2;                            // 2..6 px down/out
      fill(21,43,26,26+drop,P.bodyDk);                 // opened cavity
      fill(21,43,26+drop,27+drop,P.body);              // the gate laid down
      fill(21,43,26+drop,26+drop,P.bodyHi);
    }
    grimePass(D,P,{roofY:[7,10], flankY:[22,29], wells:[], wy:0, front:true});
  };

  // ================= FRONT 3/4 (SE) — nose down-right, flank up-left =================
  PAINT.front3q = function(D,P,fr,form){
    const {put,fill,on,set}=D; const nm=form==='nightmare';
    for(let x=8;x<=56;x++) set(x,31,P.shadow, (x<12||x>52)?0.5:0.75);
    const rearWx=18, frontWx=44, wy=25, wr=5;
    // flank body (receding up-left) — slightly higher at the rear
    fill(10,50,19,29,P.body);
    fill(10,50,28,29,P.bodyDk);
    // wheel wells
    for(const cxw of [rearWx,frontWx]) for(let y=18;y<=29;y++) for(let x=-6;x<=6;x++){ if(Math.hypot(x,y-wy)<=6.2) set(cxw+x,y,[0,0,0],0); }
    // hood + front fascia turned toward us (right, lower/larger)
    fill(40,54,14,27,P.body);
    for(let x=40;x<=54;x++){ const drop=Math.round((x-40)*0.12); fill(x,x,13+drop,13+drop,P.body); }
    // roof (cab) trailing up-left
    fill(20,42,8,10,P.roof); fill(21,41,7,7,P.roof);
    fill(10,42,16,16,P.bodyHi);
    // side windows + a bit of windshield wrap
    fill(22,31,10,15,P.glass);                         // door window
    fill(32,40,10,15,P.glass);                         // windshield
    fill(12,20,10,15,P.glass);                         // rear window
    for(const [a,b] of [[22,31],[32,40],[12,20]]) for(let x=a;x<=b;x++) put(x,10,P.glassHi,0.8);
    fill(31,31,10,15,P.roofSh);
    // grille block facing us (right side)
    fill(48,54,16,26,P.chrome);
    for(let y=17;y<=24;y+=2) fill(48,54,y,y,P.chromeSh);
    // two headlights: near (right, big) + far (left of grille, smaller)
    for(let y=-2;y<=2;y++) for(let x=-2;x<=2;x++){ const d=Math.hypot(x,y); if(d>2.3) continue; set(52+x,18+y, d>1.3?P.headRim:P.head); }
    for(let y=-1;y<=1;y++) for(let x=-1;x<=1;x++){ const d=Math.hypot(x,y); if(d>1.4) continue; set(46+x,17+y, d>0.6?P.headRim:P.head); }
    if(P.headOn){ put(50,17,P.headHi); }
    // bumper
    fill(46,55,26,27,P.chrome); put(55,26,P.chromeSh);
    fill(45,46,24,25,P.amber);                         // blinker
    // door + handle + open swing (front3q shows the near door well)
    fill(21,21,16,27,P.bodySh); put(31,20,P.chromeHi);
    if(fr.door>0){ const sw=fr.door; fill(22,24+sw,16,26,P.bodyDk); fill(22,24+sw,17,20,P.glassSh); const dx=21-sw; fill(dx,dx+1,15,26,P.body); put(dx,15,P.bodyHi); fill(dx,dx+1,15,20,P.glass); }
    // sheen
    for(let x=14;x<=48;x++){ const y=17; if(on(x,y)) put(x,y,P.bodyHi,0.6); }
    grimePass(D,P,{roofY:[7,11], flankY:[20,29], wells:[rearWx,frontWx], wy});
    wheelSide(D,P,rearWx,wy,wr-1,fr.wheel);
    wheelSide(D,P,frontWx,wy,wr,fr.wheel);
  };

  // ================= BACK 3/4 (NE) — tail up-right, flank down-left =================
  PAINT.back3q = function(D,P,fr,form){
    const {put,fill,on,set}=D; const nm=form==='nightmare';
    for(let x=8;x<=56;x++) set(x,31,P.shadow,(x<12||x>52)?0.5:0.75);
    const rearWx=20, frontWx=46, wy=25, wr=5;
    fill(10,50,19,29,P.body);
    fill(10,50,28,29,P.bodyDk);
    for(const cxw of [rearWx,frontWx]) for(let y=18;y<=29;y++) for(let x=-6;x<=6;x++){ if(Math.hypot(x,y-wy)<=6.2) set(cxw+x,y,[0,0,0],0); }
    // rear face turned toward us (right)
    fill(42,52,15,27,P.body);
    // roof trailing down-left toward front
    fill(24,46,8,10,P.roof); fill(25,45,7,7,P.roof);
    fill(10,50,16,16,P.bodyHi);
    // windows
    fill(26,35,10,15,P.glass);                         // side window
    fill(14,24,10,15,P.glass);                         // front side window
    fill(43,50,10,15,P.glass);                         // rear window (turned to us)
    for(const [a,b] of [[26,35],[14,24],[43,50]]) for(let x=a;x<=b;x++) put(x,10,P.glassHi,0.8);
    // tailgate + taillights on the rear face
    fill(42,52,21,21,P.bodySh);
    fill(43,44,22,25,P.tail); fill(50,51,22,25,P.tail); put(43,22,P.tailHi);
    // spare tire on the back
    for(let y=-3;y<=3;y++) for(let x=-3;x<=3;x++){ const d=Math.hypot(x,y); if(d>3.2) continue; set(48+x,23+y, d>2.2?P.tire:d>1?P.wall:P.hub); }
    // bumper
    fill(41,53,26,27,P.chrome); put(53,26,P.chromeSh);
    // tailgate drop on door frames
    if(fr.door>0){ const drop=fr.door*2; fill(43,51,26,26+drop,P.bodyDk); fill(43,51,26+drop,27+drop,P.body); fill(43,51,26+drop,26+drop,P.bodyHi); }
    grimePass(D,P,{roofY:[7,11], flankY:[20,29], wells:[rearWx,frontWx], wy});
    wheelSide(D,P,rearWx,wy,wr,fr.wheel);
    wheelSide(D,P,frontWx,wy,wr-1,fr.wheel);
  };

  // ---- wheel seen in the high-rear chase view; `steer` pivots the front pair ----
  function chaseWheel(D,P,cx,cy,r,phase,steer){
    const {set,put}=D;
    for(let y=-r;y<=r;y++) for(let x=-r;x<=r;x++){ const d=Math.hypot(x*1.08,y); if(d>r+0.3) continue;
      let c=(d>r-1)?P.tire:(d>r-1.9?P.tireHi:P.hub); set(cx+x,cy+y,c); }
    if(steer){ const dir=Math.sign(steer);            // a lit, turned face toward the steer side
      for(let y=-r+1;y<=r-1;y++) set(cx+dir*(r-1),cy+y,P.tireHi);
      put(cx+dir*r,cy-r+1,P.chromeHi); put(cx+dir*r,cy,P.wall);
    }
    const ang=phase*(Math.PI/2)+0.3;                  // spin nub through 4 phases
    put(Math.round(cx+Math.cos(ang)*(r-2)), Math.round(cy+Math.sin(ang)*(r-2)), P.hubHi);
    put(cx,cy-r,P.tireHi);
  }

  // ================= CHASE (high rear 3/4) — the third-person DRIVING view =================
  // Camera sits slightly above & behind: near end = rear bumper (bottom, wide), far end = hood
  // (top, narrow). `fr.steer` (-1/0/+1) banks the body and pivots the front wheels into the turn.
  PAINT.chase = function(D,P,fr,form){
    const {put,fill,on,set}=D; const nm=form==='nightmare';
    const s=fr.steer||0, roll=0.20;
    const cxAt=(y)=> Math.round(32 + s*((27-y)*roll));   // front (low y) leans toward the turn
    const halfAt=(y)=>{ const t=Math.max(0,Math.min(1,(y-14)/(29-14))); return Math.round(8+t*7); };
    // banked ground shadow
    for(let x=14;x<=50;x++){ const e=(x<18||x>46)?0.4:0.72; set(x-s,31,P.shadow,e); }
    // body trapezoid (rear near/wide -> front far/narrow)
    for(let y=14;y<=29;y++){ const cx=cxAt(y), hw=halfAt(y);
      for(let x=cx-hw;x<=cx+hw;x++){ let c=P.body; if(y>=28)c=P.bodyDk; else if(x<=cx-hw+1)c=P.bodySh; else if(x>=cx+hw-1)c=P.bodySh; set(x,y,c); } }
    // belt-line sheen
    { const cx=cxAt(16),hw=halfAt(16); fill(cx-hw,cx+hw,16,16,P.bodyHi); }
    // roof (front portion up top, narrower)
    for(let y=9;y<=14;y++){ const cx=cxAt(y), hw=Math.round(6+(y-9)*0.5); fill(cx-hw,cx+hw,y,y,P.roof); }
    { const cx=cxAt(9); for(let x=cx-6;x<=cx+6;x++) put(x,9,P.roofHi,0.7); }
    // rear/back glass (we see the road ahead through it)
    { const cx=cxAt(12),hw=6; fill(cx-hw,cx+hw,10,13,P.glass); for(let x=cx-hw;x<=cx+hw;x++) put(x,10,P.glassHi,0.8); fill(cx-1,cx,10,13,P.roofSh); }
    // tailgate seam + handle
    { const cx=cxAt(21),hw=halfAt(21); fill(cx-hw,cx+hw,21,21,P.bodySh); fill(cx-1,cx+1,19,20,P.chromeHi); }
    // twin taillights
    { const cx=cxAt(26),hw=halfAt(26); for(const tx of [cx-hw+3,cx+hw-3]){ fill(tx-1,tx+1,24,26,P.tail); put(tx-1,24,P.tailHi); } }
    // rear bumper + centre spare tyre
    { const cx=cxAt(28),hw=halfAt(28); fill(cx-hw-1,cx+hw+1,27,28,P.chrome); fill(cx-hw-1,cx+hw+1,28,28,P.chromeSh);
      for(let yy=-3;yy<=3;yy++) for(let xx=-3;xx<=3;xx++){ const d=Math.hypot(xx,yy); if(d>3.2) continue; set(cx+xx,23+yy, d>2.2?P.tire:d>1?P.wall:P.hub); } }
    // front wheels (far/top) — only visible when steering (they peek out as the truck turns);
    // rear wheels (near/bottom) — bigger, roll only
    if(s){ chaseWheel(D,P, cxAt(13)-8, 15, 3, fr.wheel, s);
           chaseWheel(D,P, cxAt(13)+8, 15, 3, fr.wheel, s); }
    chaseWheel(D,P, cxAt(27)-halfAt(27), 25, 4, fr.wheel, 0);
    chaseWheel(D,P, cxAt(27)+halfAt(27), 25, 4, fr.wheel, 0);
    grimePass(D,P,{roofY:[8,12], flankY:[22,29], wells:[], wy:0, front:true});
  };

  const PAINT_FRAMES = { chase:12 };
  function viewFrames(view){ return PAINT_FRAMES[view]||FRAMES; }
  function frameSpec(view,f){
    if(view==='chase'){ const steer=[0,0,0,0,-1,-1,-1,-1,1,1,1,1][f]; return { wheel:f%4, steer, door:0 }; }
    return { wheel: f<4? f : 0, door: f<4? 0 : (f-3) };
  }

  // ---- shared grime: snow settles on the roof, dirt splatters up the lower body ----
  function grimePass(D,P,opt){
    const {on,put,getA}=D;
    // snow on the roof (top-lit edge of whatever is up there)
    const [r0,r1]=opt.roofY;
    for(let x=0;x<W;x++) for(let y=r0;y<=r1;y++){
      if(!on(x,y)) continue;
      if(!on(x,y-1)){ // top edge of a roof span
        if(rnd(x,y,7)>0.35) put(x,y,P.snow,0.9);
        if(rnd(x,y,8)>0.7) put(x,y+1,P.snowSh,0.7);
        break;
      }
    }
    // dirt crawling up the flanks + kicked up behind wheels
    const [f0,f1]=opt.flankY;
    for(let x=0;x<W;x++) for(let y=f1;y>=f0;y--){
      if(!on(x,y)) continue;
      const h=(f1-y); const climb=rnd(x,y,4);
      // heavier near wheels
      let boost=0; for(const cxw of (opt.wells||[])){ const dd=Math.abs(x-cxw); if(dd<9) boost+=(9-dd)/9*0.45; }
      const thresh = 0.62 + h*0.06 - boost*0.9;
      if(climb>thresh){ put(x,y, rnd(x,y,5)>0.7?P.dirtDk:P.dirt, 0.85); }
    }
  }

  function colorize(pal){ const o={}; for(const k in pal){ const v=pal[k]; o[k]=(typeof v==='string'&&v[0]==='#')?hx(v):v; } return o; }

  function build(opts){
    opts=opts||{}; const view=opts.view||'side', form=opts.form||'home';
    const P=colorize(PAL[form]);
    const NF=viewFrames(view);
    const cv=window.__tkCanvas(W*NF, H); const ctx=cv.getContext('2d');
    const O=ctx.createImageData(W*NF,H); const OD=O.data;

    for(let f=0; f<NF; f++){
      const D=makeBuf();
      const fr=frameSpec(view,f);
      (PAINT[view]||PAINT.side)(D,P,fr,form);

      // outline pass (flat dark keyline, player-matching)
      const out=P.out; const snap=D.B.slice();
      const wasOn=(x,y)=>{ if(x<0||x>=W||y<0||y>=H) return false; return snap[(y*W+x)*4+3]>=128; };
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ if(wasOn(x,y)) continue; if(wasOn(x-1,y)||wasOn(x+1,y)||wasOn(x,y-1)||wasOn(x,y+1)) D.set(x,y,out); }

      const ox=f*W;
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=D.bi(x,y); const a=D.B[i+3]; if(a<8) continue; const oi=(y*(W*NF)+(ox+x))*4; OD[oi]=D.B[i];OD[oi+1]=D.B[i+1];OD[oi+2]=D.B[i+2];OD[oi+3]=a; }
    }
    ctx.putImageData(O,0,0);
    const nmSuffix = form==='nightmare' ? '_nightmare' : '';
    return { name:`truck_${view}${nmSuffix}.png`, canvas:cv };
  }

  async function buildAndSave(opts){
    const r=build(opts);
    await window.__tkSave(r.name, r.canvas);
    const up=window.__tkCanvas(W*FRAMES*8, H*8); const ux=up.getContext('2d'); ux.imageSmoothingEnabled=false; ux.drawImage(r.canvas,0,0,W*FRAMES*8,H*8);
    await window.__tkSave(r.name.replace('.png','-8x.png'), up);
    return r.name;
  }

  return { build: buildAndSave, _build: build, PAL, W, H, FRAMES };
})();
