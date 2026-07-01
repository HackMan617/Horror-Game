// Nightmare-dog generator.
// Drains a dog sheet to a corpse palette, gives it a dim red eye-glow, opens a
// clipped gut wound with a couple of hanging entrails, and strips the floating
// hearts from the excited row. 6x4 grid, 32x32 frames. The animation FRAMES are
// untouched (the "unnatural" tail-wag / stutter-pant / lurch is a PLAYBACK feel
// the game drives) — this is the art pass.
//
//   eval(await readFile('nightmaredoggen.js'));
//   await window.NightmareDog.build('dog_cream.png','ash');

window.NightmareDog = (function(){
  const FUR = {
    cream:     ['247,240,223','236,224,200','200,184,150','168,154,126'], // hi, base, sh, deep
    chocolate: ['154,100,64','122,74,44','90,52,32','63,36,22'],
    apricot:   ['238,195,146','217,154,92','180,118,60','156,98,48'],
  };
  // drained corpse ramps (hi, base, sh, deep)
  const CORPSE = {
    cream:     ['#b8b6b0','#9a988f','#6a685f','#44423b'], // ash grey
    chocolate: ['#7c6e60','#5a5048','#3c352d','#26201a'], // cold muddy grey-brown
    apricot:   ['#b0a486','#8c8064','#5e563d','#3c3626'], // sallow sickly grey-tan
  };
  // shared (non-fur) remaps
  const SHARED = {
    '28,20,16':'#1a1416',   // outline -> cold near-black
    '36,26,20':'#241c1e',   // 2nd outline
    '58,91,208':'#4a6080',  // collar blue -> drained dim
    '38,57,140':'#34465c',  // collar shadow
    '232,200,74':'#8c7c44', // tag gold -> tarnished
    '255,111,147':'#7a3a48',// tongue -> sickly drained
    '232,127,147':'#5e2c38',
    '200,95,118':'#46202a',
  };
  const PINK = new Set(['255,111,147','232,127,147','200,95,118','255,192,207']);
  const FURSET = (which)=> new Set(FUR[which]);

  function hx(h){h=h.replace('#','');return [parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];}

  function buildMap(which){
    const m={};
    const ramp=CORPSE[which], fur=FUR[which];
    for(let i=0;i<4;i++) m[fur[i]]=hx(ramp[i]);
    for(const k in SHARED) m[k]=hx(SHARED[k]);
    return m;
  }

  async function build(srcFile, which){
    const img = await window.__ndReadImage(srcFile);
    const W=img.width,H=img.height;
    const c=document.createElement('canvas'); c.width=W; c.height=H;
    const ctx=c.getContext('2d'); ctx.drawImage(img,0,0);
    const id=ctx.getImageData(0,0,W,H); const d=id.data;
    const map=buildMap(which);
    const furSet=FURSET(which);

    const cols=Math.floor(W/32), rows=Math.floor(H/32);
    const px=(x,y)=>(y*W+x)*4;
    const opaque=(x,y)=> x>=0&&x<W&&y>=0&&y<H && d[px(x,y)+3]>150;
    const set=(x,y,rgb,a)=>{ if(x<0||x>=W||y<0||y>=H)return; const i=px(x,y); d[i]=rgb[0];d[i+1]=rgb[1];d[i+2]=rgb[2];d[i+3]=(a==null?255:a); };
    const clear=(x,y)=>{ if(x<0||x>=W||y<0||y>=H)return; d[px(x,y)+3]=0; };

    // ---- pass 1: recolor + strip hearts ----
    for(let cy=0;cy<rows;cy++) for(let cx=0;cx<cols;cx++){
      const ox=cx*32, oy=cy*32;
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){
        const i=px(ox+x,oy+y); if(d[i+3]<150) continue;
        const k=d[i]+','+d[i+1]+','+d[i+2];
        // strip floating hearts: pink in the upper region of the excited row
        if(cy===3 && y<12 && PINK.has(k)){ d[i+3]=0; continue; }
        const rep=map[k];
        if(rep){ d[i]=rep[0]; d[i+1]=rep[1]; d[i+2]=rep[2]; }
      }
    }

    // ---- pass 2: per-frame overlays (eyes + wound) ----
    const ROSE='#d8362a', GLOWDK='#6e1414';       // eye glow
    const RIM='#220810', MID='#6e1822', HI='#9c2a32', DRK='#3a0e16'; // viscera
    for(let cy=0;cy<rows;cy++) for(let cx=0;cx<cols;cx++){
      const ox=cx*32, oy=cy*32;
      // locate eye catchlights (still pure white at this point)
      const eyes=[];
      let fminX=99,fmaxX=-1,fminY=99,fmaxY=-1;
      for(let y=0;y<32;y++) for(let x=0;x<32;x++){
        const i=px(ox+x,oy+y); if(d[i+3]<150) continue;
        if(d[i]===255&&d[i+1]===255&&d[i+2]===255) eyes.push([x,y]);
        if(d[i+3]>150){fminX=Math.min(fminX,x);fmaxX=Math.max(fmaxX,x);fminY=Math.min(fminY,y);fmaxY=Math.max(fmaxY,y);}
      }
      // red eye glow: white -> bright red core, dark ring around it dim red
      for(const [ex,ey] of eyes){
        set(ox+ex,oy+ey,hx(ROSE));
        for(const [dx,dy] of [[-1,0],[1,0],[0,-1],[0,1]]){
          const x=ox+ex+dx,y=oy+ey+dy,i=px(x,y);
          if(d[i+3]>150 && d[i]<60 && d[i+1]<50 && d[i+2]<55) set(x,y,hx(GLOWDK));
        }
      }
      // gut wound: lower-body, clipped to opaque pixels, away from the eyes
      if(fmaxX<0) continue;
      const cxw = ox + Math.round((fminX+fmaxX)/2) - (cy<3?0:0);
      const cyw = oy + Math.round(fminY + (fmaxY-fminY)*0.66);
      const farFromEye=(x,y)=> eyes.every(([ex,ey])=> Math.abs((ox+ex)-x)+Math.abs((oy+ey)-y) > 4);
      const woundPix=[
        [-1,-1,RIM],[0,-1,RIM],[1,-1,RIM],
        [-2,0,RIM],[-1,0,MID],[0,0,HI],[1,0,MID],[2,0,RIM],
        [-1,1,DRK],[0,1,MID],[1,1,HI],[2,1,RIM],
        [0,2,MID],[1,2,RIM],
      ];
      for(const [dx,dy,col] of woundPix){
        const x=cxw+dx, y=cyw+dy;
        if(opaque(x,y) && farFromEye(x,y)) set(x,y,hx(col));
      }
      // a couple of hanging entrails below the body bottom near the wound x
      let bottomY=-1;
      for(let y=31;y>=0;y--){ if(opaque(cxw,oy+y)){ bottomY=oy+y; break; } }
      if(bottomY>0){
        set(cxw,bottomY+1,hx(MID)); set(cxw,bottomY+2,hx(DRK));
        set(cxw+1,bottomY+1,hx(DRK));
      }
    }

    ctx.putImageData(id,0,0);
    const out = srcFile.replace('.png','_nightmare.png');
    await window.__ndSave(out, c);
    const up=document.createElement('canvas'); up.width=W*8; up.height=H*8;
    const ux=up.getContext('2d'); ux.imageSmoothingEnabled=false; ux.drawImage(c,0,0,W*8,H*8);
    await window.__ndSave(out.replace('.png','-8x.png'), up);
    return out;
  }

  return { build };
})();
