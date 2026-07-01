// Long-hair (female) variant generator.
// Paints mid-back long hair onto the existing 32x32 / 5-frame master sheets,
// using the EXACT source hair colors (base 156,90,38 / shadow 94,52,16 / black
// outline) so the Character Creator's runtime recolor still maps every hair
// option onto it. Head is fixed at cols 11-20; per-frame vertical bob handled
// via detected dy. Front = face-framing side curtains; Back = full hair sheet.
//
// Usage (run_script):
//   eval(await readFile('longhairgen.js'));
//   await window.LongHairGen.build();   // writes the 4 PNGs

window.LongHairGen = (function(){
  const HAIR = [156,90,38], HSH = [94,52,16], INK = [0,0,0];
  const COL = { K:INK, H:HAIR, h:HSH };

  function frameDy(d, W, fx, baseTop){
    // detect topmost hair row in this frame to follow the walk bob
    const isHair=(i)=>{const r=d[i],g=d[i+1],b=d[i+2];return d[i+3]>150 && ((r===156&&g===90&&b===38)||(r===94&&g===52&&b===16));};
    for(let y=0;y<14;y++) for(let x=0;x<32;x++){ const i=((y)*W + fx+x)*4; if(isHair(i)) return y-baseTop; }
    return 0;
  }

  // paint one logical pixel (col,row) into frame fx with vertical offset dy
  function mkPaint(out, W, H, fx, dy){
    return (col,row,kind)=>{
      const x = fx+col, y = row+dy;
      if(x<0||x>=W||y<0||y>=H||col<0||col>31) return;
      const c = COL[kind]; const i=(y*W+x)*4;
      out[i]=c[0]; out[i+1]=c[1]; out[i+2]=c[2]; out[i+3]=255;
    };
  }

  // FRONT: face-framing side curtains falling to mid-back. Drawn for the LEFT
  // half then mirrored about the centre (col c -> 31-c).
  function drawFront(paint){
    const put=(col,row,kind)=>{ paint(col,row,kind); paint(31-col,row,kind); };
    // crown link into the existing cap (cap is cols 11-20, outline col 10/21)
    put(9,3,'K'); put(10,3,'H');
    put(8,4,'K'); put(9,4,'H'); put(10,4,'H'); put(11,4,'h');
    // full-volume curtain, rows 5-15: outer outline col7, base 8-10, shadow 11
    for(let r=5;r<=15;r++){
      put(7,r,'K'); put(8,r,'H'); put(9,r,'H'); put(10,r,'H'); put(11,r,'h');
    }
    // subtle inner strand for texture (one clean vertical, not speckles)
    for(let r=7;r<=14;r++){ paint(9,r,'h'); paint(31-9,r,'h'); }
    // taper the bottom to a soft point (shoulder / upper-back length)
    put(7,16,'K'); put(8,16,'H'); put(9,16,'H'); put(10,16,'H'); put(11,16,'h');
    put(8,17,'K'); put(9,17,'H'); put(10,17,'H'); put(11,17,'h');
    put(9,18,'K'); put(10,18,'H'); put(11,18,'h');
    put(10,19,'K'); put(11,19,'h');
  }

  // BACK: long hair flowing from the head down the back to a mid-back point.
  // Connects under the crown, widens over the shoulders, tapers to a point.
  function drawBack(paint){
    const put=(col,row,kind)=>{ paint(col,row,kind); paint(31-col,row,kind); };
    // row 11 connects to the head bottom (head is cols 11-18 here)
    for(let c=10;c<=15;c++) put(c,11,'H');
    // rows 12-17 full back sheet: outline col8, base 9-15 (mirror -> 16-22,23)
    for(let r=12;r<=17;r++){
      put(8,r,'K');
      for(let c=9;c<=15;c++) put(c,r,'H');
    }
    // two faint vertical strand shadows for texture
    for(let r=12;r<=16;r++){ put(10,r,'h'); }
    // short centre parting near the nape only
    for(let r=12;r<=15;r++){ paint(15,r,'h'); paint(16,r,'h'); }
    // inner side shadow against the outline
    for(let r=13;r<=17;r++){ put(9,r,'h'); }
    // taper to a soft upper-back point
    put(8,18,'K'); for(let c=9;c<=15;c++) put(c,18,'H'); put(9,18,'h');
    put(9,19,'K'); for(let c=10;c<=15;c++) put(c,19,'H');
    put(10,20,'K'); for(let c=11;c<=15;c++) put(c,20,'H');
    put(11,21,'K'); for(let c=12;c<=15;c++) put(c,21,'H');
  }

  async function buildOne(srcFile, outFile, which){
    const img = await window.__lhReadImage(srcFile);
    const W=img.width, H=img.height, frames=Math.floor(W/32);
    const c=document.createElement('canvas'); c.width=W; c.height=H;
    const ctx=c.getContext('2d'); ctx.drawImage(img,0,0);
    const id=ctx.getImageData(0,0,W,H); const out=id.data;
    for(let f=0; f<frames; f++){
      const fx=f*32;
      const dy=frameDy(out, W, fx, 2);
      const paint=mkPaint(out, W, H, fx, dy);
      if(which==='front') drawFront(paint); else drawBack(paint);
    }
    ctx.putImageData(id,0,0);
    await window.__lhSave(outFile, c);
    // 8x preview
    const up=document.createElement('canvas'); up.width=W*8; up.height=H*8;
    const ux=up.getContext('2d'); ux.imageSmoothingEnabled=false; ux.drawImage(c,0,0,W*8,H*8);
    await window.__lhSave(outFile.replace('.png','-8x.png'), up);
    return {W,H,frames};
  }

  async function build(){
    const a=await buildOne('character_sprite_sheet.png','character_sprite_sheet_long.png','front');
    const b=await buildOne('character_sprite_sheet_back.png','character_sprite_sheet_back_long.png','back');
    return {front:a, back:b};
  }

  return { build, drawFront, drawBack };
})();
