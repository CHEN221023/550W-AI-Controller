// Start with the existing central particle population; preserve the upstream 550C transition.
export const INTRO_DEFAULTS = Object.freeze({particleResidueMs:350,particleToLogoMs:1200,logoHoldMs:450,logoBurstMs:900,particleCount:700,particleSize:2.2,particleSpread:1,multiWindowRevealMs:850,handoffCompressMs:420});
const clamp=(n,min,max)=>Math.min(max,Math.max(min,n));
export function introSettings(value={}){
  const result={...INTRO_DEFAULTS};for(const key of Object.keys(result))if(Number.isFinite(Number(value[key])))result[key]=Number(value[key]);
  if(value.particleToLogoMs===undefined&&Number.isFinite(Number(value.logoRevealMs)))result.particleToLogoMs=result.particleResidueMs+Number(value.logoRevealMs);
  if(result.particleToLogoMs<=result.particleResidueMs)throw new RangeError('粒子 → 550C 总时长必须大于中央粒子待机时长。');
  for(const key of Object.keys(result))if(key.endsWith('Ms'))result[key]=clamp(result[key],0,key==='particleToLogoMs'?20000:10000);
  result.particleCount=Math.round(clamp(result.particleCount,100,2400));result.particleSize=clamp(result.particleSize,.6,7);result.particleSpread=clamp(result.particleSpread,.35,2);return result;
}
export function createIntro({stage,settings,scale,event,finished,elapsed}){
  const cfg=introSettings(settings),canvas=document.createElement('canvas');canvas.className='intro-particles';canvas.width=1920;canvas.height=1080;canvas.setAttribute('aria-hidden','true');stage.append(canvas);const ctx=canvas.getContext('2d',{alpha:true});
  // Build cache of the unchanged upstream SVG, shared with the native first-frame adapter.
  const logo=document.createElement('div');logo.className='intro-logo';logo.setAttribute('aria-label','550C');stage.append(logo);
  const logoRaster=document.createElement('canvas');logoRaster.width=800;logoRaster.height=230;logoRaster.className='intro-logo-raster';logo.append(logoRaster);
  const logoImage=new Image();logoImage.src=new URL('../assets/intro-logo.png',import.meta.url);
  let particles=[],groups=[],prepared,raf=0,resolveFrame,disposed=false,particlePhase='IDLE',coverage={left:960,top:540,right:960,bottom:540};
  const dirty={left:0,top:0,right:1920,bottom:1080};
  const rnd=i=>{const n=Math.sin((i+1)*127.1+311.7)*43758.5453123;return n-Math.floor(n);};
  const color=getComputedStyle(stage.getRootNode().host).getPropertyValue('--amber').trim()||'#e05030';
  const ease=t=>1-Math.pow(1-t,3),mix=(a,b,t)=>a+(b-a)*t;
  async function initialize(){
    const [targets]=await Promise.all([fetch(new URL('../assets/intro-logo-targets.json',import.meta.url)).then(r=>{if(!r.ok)throw Error('550C cache unavailable');return r.json();}),logoImage.decode()]);
    if(disposed||finished())return;logoRaster.getContext('2d').drawImage(logoImage,0,0,800,230);
    particles=Array.from({length:cfg.particleCount},(_,i)=>{const angle=rnd(i)*Math.PI*2,radius=35+Math.sqrt(rnd(i+451))*155,target=targets[Math.floor(rnd(i+83)*targets.length)];return{midX:960+Math.cos(angle)*radius,midY:540+Math.sin(angle)*radius*.67,logoX:target.x,logoY:target.y,logoRed:target.red,burstX:960+(rnd(i+177)*2-1)*1050*cfg.particleSpread,burstY:540+(rnd(i+290)*2-1)*620*cfg.particleSpread,seed:rnd(i+944),size:cfg.particleSize*(.6+rnd(i+630)*.8)};});
    groups=[particles,particles.filter(p=>p.logoRed),particles.filter(p=>!p.logoRed)];

  }
  const idleX=(p,ms)=>p.midX+Math.sin(p.seed*9)*5+1.5*(Math.sin(ms/1000*.9+p.seed*9)-Math.sin(p.seed*9));
  const idleY=(p,ms)=>p.midY+Math.cos(p.seed*9)*4+1.2*(Math.cos(ms/1000*.75+p.seed*7)-Math.cos(p.seed*7));
  let frameReady;const firstFrame=new Promise(resolve=>{frameReady=resolve;});
  function draw(mode,t){
    ctx.clearRect(dirty.left,dirty.top,dirty.right-dirty.left,dirty.bottom-dirty.top);
    coverage.left=1920;coverage.top=1080;coverage.right=0;coverage.bottom=0;
    const logoMode=mode==='reveal'||mode==='burst'||mode==='fade',e=mode==='fade'?1+t*.08:ease(t);
    const alpha=mode==='fade'?1-t:mode==='reveal'?1-clamp((t-.45)/.55,0,1)*.82:1;
    for(const p of particles){
      p.fragment=false;p.renderSize=p.size;
      if(mode==='residue'){p.rx=idleX(p,t);p.ry=idleY(p,t);}
      else if(mode==='reveal'){p.rx=mix(idleX(p,scale(cfg.particleResidueMs)),p.logoX,e);p.ry=mix(idleY(p,scale(cfg.particleResidueMs)),p.logoY,e);}
      else{p.rx=mix(p.logoX,p.burstX,e);p.ry=mix(p.logoY,p.burstY,e);}
      const pad=p.renderSize*3+2;coverage.left=Math.min(coverage.left,p.rx-pad);coverage.top=Math.min(coverage.top,p.ry-pad);coverage.right=Math.max(coverage.right,p.rx+pad);coverage.bottom=Math.max(coverage.bottom,p.ry+pad);
    }
    // Batch bodies/glows by the two original logo colors. No per-particle arrays,
    // strings, canvas resize, texture upload or path parsing during a transition.
    for(let g=logoMode?1:0;g<(logoMode?3:1);g++){
      ctx.fillStyle=g===0?color:g===1?'#ff2d2d':'#ffffff';ctx.globalAlpha=alpha;ctx.beginPath();
      for(const p of groups[g])if(!p.fragment)ctx.rect(p.rx,p.ry,p.renderSize,p.renderSize);ctx.fill();
      ctx.globalAlpha=alpha*.13;ctx.beginPath();for(const p of groups[g])if(!p.fragment&&p.seed>.86)ctx.rect(p.rx-p.renderSize*2,p.ry-p.renderSize*2,p.renderSize*5,p.renderSize*5);ctx.fill();
    }
    ctx.globalAlpha=1;dirty.left=Math.max(0,Math.floor(coverage.left));dirty.top=Math.max(0,Math.floor(coverage.top));dirty.right=Math.min(1920,Math.ceil(coverage.right));dirty.bottom=Math.min(1080,Math.ceil(coverage.bottom));
  }
  function phase(name){particlePhase=name;event(name);}
  function prepare(){return prepared??=(async()=>{await initialize();if(disposed||finished())return;draw('residue',0);draw('burst',0);ctx.getImageData(0,0,1,1);ctx.clearRect(0,0,1920,1080);})();}
  function play(onWindows=()=>{}){
    return prepare().then(()=>new Promise(resolve=>{
      if(disposed||finished()){resolve();return;}resolveFrame=resolve;
      // Only active phases: no zero-duration or hidden robot states.
      const beats=[
        ['PARTICLE_RESIDUE',cfg.particleResidueMs],['LOGO_ASSEMBLING',cfg.particleToLogoMs-cfg.particleResidueMs],['LOGO_HOLD',cfg.logoHoldMs],['LOGO_BURST',cfg.logoBurstMs],['MULTIWINDOW_REVEAL',cfg.multiWindowRevealMs]
      ];let cursor=0;for(const b of beats){b.push(cursor);cursor+=scale(b[1]);b.push(cursor);}
      const app=stage.querySelector('#app');let index=-1,start;
      function enter(i){
        phase(beats[i][0]);
        if(i===0){draw('residue',0);canvas.style.opacity='1';}
        if(i===1){event('LOGO_BEGIN');logo.style.visibility='visible';}
        if(i===2){canvas.style.opacity='0';logo.classList.add('complete');}
        if(i===3){draw('burst',0);canvas.style.opacity='1';}
        if(i===4){logo.style.visibility='hidden';app.style.opacity='0';stage.classList.remove('intro-active');onWindows();}
      }
      function paint(i,t){
        switch(i){
          case 0:draw('residue',t*scale(cfg.particleResidueMs));break;
          case 1:draw('reveal',t);logoRaster.style.opacity=String(clamp((t-.3)/.7,0,1));break;
          case 3:draw('burst',t);logo.style.opacity=String(1-clamp(t*4,0,1));break;
          case 4:app.style.opacity=String(ease(t));draw('fade',t);break;
        }
      }
      function tick(now){
        if(disposed||finished()){resolveFrame=null;resolve();return;}
        // Continue the native first-visible clock. Preparation never restarts the idle interval.
        if(start===undefined)start=now;
        const time=elapsed?Math.max(0,elapsed()):now-start;
        while(index+1<beats.length&&time>=beats[index+1][2]){if(index>=0)paint(index,1);index++;enter(index);}
        const beat=beats[index],duration=beat[3]-beat[2];paint(index,duration<=0?1:clamp((time-beat[2])/duration,0,1));frameReady();
        if(time>=cursor){app.style.removeProperty('opacity');canvas.style.opacity='0';ctx.clearRect(0,0,1920,1080);phase('MULTIWINDOW_ACTIVE');resolveFrame=null;resolve();}
        else raf=requestAnimationFrame(tick);
      }
      raf=requestAnimationFrame(tick);
    }));
  }
  function dispose(){if(disposed)return;disposed=true;cancelAnimationFrame(raf);frameReady();resolveFrame?.();resolveFrame=null;ctx.clearRect(0,0,1920,1080);canvas.style.opacity='0';}
  return{prepare,firstFrame,play,dispose,getState:()=>({phase:particlePhase,count:particles.length,coverage,disposed,coreVisible:false,logoVisible:getComputedStyle(logo).visibility!=='hidden'&&Number(getComputedStyle(logo).opacity)>.01,config:cfg})};
}
