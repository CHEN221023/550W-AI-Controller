const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert'),crypto=require('crypto');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),baseline=path.resolve(process.argv[2]||'work/v44-before/Themes/550W'),images=path.join(root,'docs/images-v44');fs.mkdirSync(images,{recursive:true});
let browser,server,url;const results=[];
async function test(name,fn){try{results.push({name,pass:true,...await fn()});console.log('PASS '+name);}catch(e){results.push({name,pass:false,error:e.stack});console.error(e);}fs.writeFileSync(path.join(root,'docs/web-test-v44-results.json'),JSON.stringify(results,null,2));}
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
async function fixture(old){const p=await browser.newPage({viewport:{width:1920,height:1080}});await p.goto(url+'/fixture');await p.evaluate(async old=>{
 const prefix=old?'/baseline/':'/Themes/550W/',{createIntro}=await import(prefix+'js/intro.js');document.documentElement.style.background='#000';document.body.style.cssText='margin:0;background:#000;overflow:hidden';
 const host=document.createElement('div');host.style.cssText='width:1920px;height:1080px;--amber:#e05030';document.body.append(host);const sh=host.attachShadow({mode:'open'}),link=document.createElement('link');link.rel='stylesheet';link.href=prefix+'css/controller.css';sh.append(link);await new Promise(r=>link.onload=r);const stage=document.createElement('div');stage.className='dsh550c-stage intro-active';stage.innerHTML='<div id="app"></div>';sh.append(stage);
 let core,eye;if(false){core=document.createElement('div');core.className='mechanical-core boot-core';eye=new Image();eye.src='/Themes/550W/assets/core.png';core.append(eye);stage.append(core);await eye.decode();}
 const settings={coreHoldMs:650,coreDissolveMs:650,particleResidueMs:350,logoRevealMs:850,logoHoldMs:450,logoBurstMs:900,multiWindowRevealMs:850,particleCount:700,particleSize:2.2,particleSpread:1};const events=[];const intro=createIntro({stage,core,eye,settings,scale:x=>x,event:e=>events.push(e),finished:()=>false});await intro.prepare();let next;window.requestAnimationFrame=fn=>(next=fn,1);window.cancelAnimationFrame=()=>{next=null;};window.f={intro,stage,events,step:t=>{const fn=next;next=null;fn(t);},image:()=>stage.querySelector('.intro-particles').toDataURL()};intro.play();await Promise.resolve();f.step(0);
 },old);return p;}
(async()=>{
 server=http.createServer((req,res)=>{const pathname=decodeURIComponent(req.url.split('?')[0]);if(pathname==='/fixture'){res.setHeader('Content-Type','text/html');return res.end('<!doctype html><html><body></body></html>');}let file;if(pathname.startsWith('/baseline/')){const rel=pathname.slice(10);file=path.resolve(baseline,rel);if(!file.startsWith(baseline+path.sep)){res.writeHead(403);return res.end();}if(!fs.existsSync(file))file=path.join(root,'Themes/550W',rel);}else file=path.resolve(root,'.'+pathname);if(!file.startsWith(root+path.sep)&&!file.startsWith(baseline+path.sep)||!fs.existsSync(file)){res.writeHead(404);return res.end();}res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.html')?'text/html':file.endsWith('.json')?'application/json':file.endsWith('.png')?'image/png':'application/octet-stream');fs.createReadStream(file).pipe(res);});await new Promise(r=>server.listen(0,'127.0.0.1',r));url='http://127.0.0.1:'+server.address().port;
 browser=await chromium.launch({headless:true,executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--autoplay-policy=no-user-gesture-required']});
 await test('existing particle visual is pixel-identical from residue through HUD crossfade',async()=>{
  const old=await fixture(true),now=await fixture(false),comparisons=[];
  for(const [phase,t]of [['residue-start',0],['residue-middle',175],['logo-assembling',700],['logo-hold',1200],['logo-burst',2025],['hud-crossfade',2800]]){
   await old.evaluate(t=>f.step(t),t);if(t)await now.evaluate(t=>f.step(t),t);const a=await old.evaluate(()=>f.image()),b=await now.evaluate(()=>f.image());assert.equal(hash(a),hash(b),phase+' particle pixels changed');
   const la=await old.evaluate(()=>f.stage.querySelector('.intro-logo-raster').toDataURL()),lb=await now.evaluate(()=>f.stage.querySelector('.intro-logo-raster').toDataURL());assert.equal(hash(la),hash(lb),'550C changed');comparisons.push({phase,particleSha256:hash(b),logoSha256:hash(lb)});
   if(t===0)await now.screenshot({path:path.join(images,'01-central-particles.png')});if(t===1200)await now.screenshot({path:path.join(images,'02-original-550c.png')});
  }
  const events=await now.evaluate(()=>f.events);assert(!events.some(e=>e.startsWith('CORE_')));assert.equal(events[0],'PARTICLE_RESIDUE');assert.equal(await now.evaluate(()=>f.stage.querySelectorAll('img,.mechanical-core,.core-scanner,.core-status').length),0);await old.close();await now.close();return{comparisons,events};
 });
 await test('web first frame does not wait for slow audio preload',async()=>{
  const p=await browser.newPage({viewport:{width:1920,height:1080}});
  await p.goto(url+'/Themes/550W/boot.html?controlled');await p.waitForFunction(()=>window.Controller550W);
  await p.evaluate(async()=>{
    const {AudioKit}=await import('/Themes/550W/js/audio.js');
    AudioKit.prototype.preload=()=>new Promise(r=>{window.releaseAudio=r;});
    window.chrome={webview:{postMessage:m=>{
      if(m.type==='VISUAL_READY'){
        const sh=document.querySelector('.controller-host').shadowRoot,c=sh.querySelector('.intro-particles');
        const pixels=c.getContext('2d').getImageData(750,380,420,320).data;
        window.firstFrame={at:performance.now()-initAt,opacity:getComputedStyle(c).opacity,colored:Array.from(pixels).filter((v,i)=>i%4===3&&v>0).length,background:getComputedStyle(document.body).backgroundColor};
      }
    }}};
    window.initAt=performance.now();Controller550W.receive({type:'init',session:{kind:'boot',displayName:'Fixture',settings:{durationMultiplier:1,colorProfile:'movie_red',systemLabel:'550W'},audio:{soundEnabled:true}}});
  });
  await p.waitForFunction(()=>window.firstFrame,null,{timeout:1000});const frame=await p.evaluate(()=>firstFrame);
  assert(frame.colored>1500);assert.equal(frame.opacity,'1');assert(frame.at<1000);
  assert.equal(await p.evaluate(()=>Controller550W.getState().intro.phase),'IDLE');
  await p.evaluate(()=>{releaseAudio();Controller550W.receive({type:'cancel'});});await p.close();return frame;
 });
 await test('full boot starts at residue; no robot requests or obsolete sound; existing HUD and handoff complete',async()=>{
  const p=await browser.newPage({viewport:{width:1920,height:1080}}),requests=[],errors=[];p.on('request',r=>requests.push(r.url()));p.on('pageerror',e=>errors.push(e.message));await p.goto(url+'/Themes/550W/boot.html?controlled');await p.waitForFunction(()=>window.Controller550W);
  await p.evaluate(()=>{window.speechSynthesis.speak=u=>setTimeout(()=>u.onend?.(),0);window.bootAudit=[];const capture=()=>{const s=Controller550W.getState(),sh=document.querySelector('.controller-host').shadowRoot;bootAudit.push({phase:s.intro?.phase,removed:sh.querySelectorAll('.mechanical-core,.core-scanner,.core-status').length,background:getComputedStyle(sh.querySelector('.composition-layer')).backgroundColor});if(!s.finished)requestAnimationFrame(capture);};requestAnimationFrame(capture);Controller550W.receive({type:'init',session:{kind:'boot',displayName:'Fixture AI',minimumBootTimeMs:0,coreImagePath:'ignored-old-custom-core.png',settings:{coreHoldMs:10000,coreDissolveMs:10000,durationMultiplier:1,colorProfile:'movie_red',systemLabel:'550W',particleResidueMs:350,logoRevealMs:850,logoHoldMs:450,logoBurstMs:900,multiWindowRevealMs:850,readyMs:50,fadeMs:120,handoffCompressMs:120,shutdownMs:2600,bootWatchdogMs:22000,shutdownWatchdogMs:5000,waitForReady:true,allowEscape:true},audio:{soundEnabled:true,soundVolume:0,voiceEnabled:true,voiceVolume:0,bootVoice:true,shutdownVoice:true,phrases:{BOOT:'系统启动。',CORE:'不得播放',READY:'就绪。'}}}});Controller550W.receive({type:'target',target:{processDetected:true,windowDetected:true,ready:true}});});
  await p.waitForFunction(()=>Controller550W.getState().finished,null,{timeout:24000});const state=await p.evaluate(()=>Controller550W.getState()),audit=await p.evaluate(()=>bootAudit),times=Object.fromEntries(state.events.map(e=>[e.type,e.at]));
  assert.equal(state.events.find(e=>['CORE_SCANNING','CORE_REVEAL','CORE_HOLD','CORE_PARTICLES','PARTICLE_RESIDUE'].includes(e.type)).type,'PARTICLE_RESIDUE');assert(!state.events.some(e=>e.type.startsWith('CORE_')||['FATAL','WATCHDOG'].includes(e.type)));assert(audit.every(a=>a.removed===0));assert(!requests.some(r=>/\/assets\/core\.png|audio\.550w\.local\/core|power_on\.wav/.test(r)),requests.join('\n'));
  const timing=[];for(const [from,to,expected]of [['PARTICLE_RESIDUE','LOGO_ASSEMBLING',350],['LOGO_ASSEMBLING','LOGO_HOLD',850],['LOGO_HOLD','LOGO_BURST',450],['LOGO_BURST','MULTIWINDOW_REVEAL',900],['MULTIWINDOW_REVEAL','MULTIWINDOW_ACTIVE',850]]){const actual=times[to]-times[from];assert(Math.abs(actual-expected)<85,from+': '+actual);timing.push({from,to,expected,actual});}
  assert.equal(state.nodes,47);assert.equal(state.modules.length,9);assert.equal(state.intro.count,700);assert(state.events.some(e=>e.type==='HANDOFF_BEGIN'));assert(!state.audioEvents.some(e=>e.key==='POWER_ON'));assert(state.audioEvents.some(e=>e.key==='CORE_EXPAND'));assert(state.audioEvents.some(e=>e.key==='HUD_SCAN'));assert(!state.audioEvents.some(e=>e.key==='CORE'));assert.equal(errors.length,0);await p.close();return{timing,removedNodes:0,robotRequests:0,obsoleteAudioRequests:0,oldCoreDurationSettingsIgnored:true,particles:700,hudNodes:47,hudWindows:9,errors};
 });
 if(results.some(x=>!x.pass))process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{await browser?.close();server?.close();});
