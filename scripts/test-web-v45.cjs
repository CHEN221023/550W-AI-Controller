const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert'),crypto=require('crypto');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),baseline=path.resolve(process.argv[2]||'work/v45-before/Themes/550W'),images=path.join(root,'docs/images-v45');fs.mkdirSync(images,{recursive:true});
let browser,server,url;const results=process.env.V45_TEST_FILTER&&fs.existsSync(path.join(root,'docs/web-test-v45-results.json'))?JSON.parse(fs.readFileSync(path.join(root,'docs/web-test-v45-results.json'),'utf8')).filter(x=>!x.name.includes(process.env.V45_TEST_FILTER)):[];
async function test(name,fn){if(process.env.V45_TEST_FILTER&&!name.includes(process.env.V45_TEST_FILTER))return;try{results.push({name,pass:true,...await fn()});console.log('PASS '+name);}catch(e){results.push({name,pass:false,error:e.stack});console.error(e);}fs.writeFileSync(path.join(root,'docs/web-test-v45-results.json'),JSON.stringify(results,null,2));}
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
async function fixture(settings={},old=false,late=0){const p=await browser.newPage({viewport:{width:1920,height:1080}});await p.goto(url+'/fixture');await p.evaluate(async({settings,old,late})=>{
 const prefix=old?'/baseline/':'/Themes/550W/',{createIntro}=await import(prefix+'js/intro.js');document.documentElement.style.background='#000';document.body.style.cssText='margin:0;background:#000;overflow:hidden';
 const host=document.createElement('div');host.style.cssText='width:1920px;height:1080px;--amber:#e05030';document.body.append(host);const sh=host.attachShadow({mode:'open'}),link=document.createElement('link');link.rel='stylesheet';link.href='/Themes/550W/css/controller.css';sh.append(link);await new Promise(r=>link.onload=r);const stage=document.createElement('div');stage.className='dsh550c-stage intro-active';stage.innerHTML='<div id="app"></div>';sh.append(stage);
 const cfg={particleResidueMs:350,particleToLogoMs:1200,logoRevealMs:850,logoHoldMs:450,logoBurstMs:900,multiWindowRevealMs:850,particleCount:700,particleSize:2.2,particleSpread:1,durationMultiplier:1,...settings};
 const events=[];let time=late;const intro=createIntro({stage,settings:cfg,scale:x=>x*cfg.durationMultiplier,event:e=>events.push({type:e,time}),finished:()=>false,elapsed:()=>time});await intro.prepare();let next;window.requestAnimationFrame=fn=>(next=fn,1);window.cancelAnimationFrame=()=>{next=null;};
 window.f={intro,stage,events,step:t=>{time=t;const fn=next;next=null;if(fn)fn(t);},image:()=>stage.querySelector('.intro-particles').toDataURL(),logo:()=>stage.querySelector('.intro-logo-raster').toDataURL()};intro.play();await Promise.resolve();f.step(late);
 },{settings,old,late});return p;}
(async()=>{
 server=http.createServer((req,res)=>{const pathname=decodeURIComponent(req.url.split('?')[0]);if(pathname==='/fixture'){res.setHeader('Content-Type','text/html');return res.end('<!doctype html><html><body></body></html>');}let file;if(pathname.startsWith('/baseline/')){const rel=pathname.slice(10);file=path.resolve(baseline,rel);if(!file.startsWith(baseline+path.sep)){res.writeHead(403);return res.end();}if(!fs.existsSync(file))file=path.join(root,'Themes/550W',rel);}else file=path.resolve(root,'.'+pathname);if(!file.startsWith(root+path.sep)&&!file.startsWith(baseline+path.sep)||!fs.existsSync(file)){res.writeHead(404);return res.end();}res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.html')?'text/html':file.endsWith('.json')?'application/json':file.endsWith('.png')?'image/png':'application/octet-stream');fs.createReadStream(file).pipe(res);});await new Promise(r=>server.listen(0,'127.0.0.1',r));url='http://127.0.0.1:'+server.address().port;
 browser=await chromium.launch({headless:true,executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--autoplay-policy=no-user-gesture-required']});
 await test('three exact deadlines, mild motion, zero hold, multiplier and invalid combinations',async()=>{
  const records=[];
  for(const [idle,total,hold,multiplier]of [[100,400,200,1],[500,1000,450,1],[1000,1800,300,1],[1500,2100,0,1],[500,1000,450,.75],[100,101,0,1]]){
   const p=await fixture({particleResidueMs:idle,particleToLogoMs:total,logoHoldMs:hold,durationMultiplier:multiplier});
   const initial=await p.evaluate(()=>f.image());await p.evaluate(t=>f.step(t),idle*multiplier*.5);const moving=await p.evaluate(()=>f.image());assert.notEqual(hash(initial),hash(moving),'idle remained static');
   await p.evaluate(t=>f.step(t),idle*multiplier-0.01);assert.equal(await p.evaluate(()=>f.intro.getState().phase),'PARTICLE_RESIDUE');
   await p.evaluate(t=>f.step(t),idle*multiplier);assert.equal(await p.evaluate(()=>f.intro.getState().phase),'LOGO_ASSEMBLING');
   await p.evaluate(t=>f.step(t),total*multiplier);assert.equal(await p.evaluate(()=>f.intro.getState().phase),hold?'LOGO_HOLD':'LOGO_BURST');
   assert.equal(await p.evaluate(()=>Number(f.stage.querySelector('.intro-logo-raster').style.opacity)),1,'logo not complete at total deadline');
   if(hold){await p.evaluate(t=>f.step(t),(total+hold)*multiplier-.01);assert.equal(await p.evaluate(()=>f.intro.getState().phase),'LOGO_HOLD');}
   await p.evaluate(t=>f.step(t),(total+hold)*multiplier);assert.equal(await p.evaluate(()=>f.intro.getState().phase),'LOGO_BURST');records.push({idle,total,hold,multiplier,regroupMs:(total-idle)*multiplier,events:await p.evaluate(()=>f.events)});await p.close();
  }
  const p=await browser.newPage();await p.goto(url+'/fixture');const invalid=await p.evaluate(async()=>{const {introSettings}=await import('/Themes/550W/js/intro.js');return [800,1000].map(total=>{try{introSettings({particleResidueMs:1000,particleToLogoMs:total});return false;}catch(e){return e.message.includes('总时长必须大于');}});});assert(invalid.every(Boolean));await p.close();return{records,invalidRejected:invalid};
 });
 await test('late web preparation continues native time instead of restarting idle',async()=>{
  const p=await fixture({particleResidueMs:500,particleToLogoMs:1000,logoHoldMs:450},false,750);assert.equal(await p.evaluate(()=>f.intro.getState().phase),'LOGO_ASSEMBLING');
  await p.evaluate(()=>f.step(1000));assert.equal(await p.evaluate(()=>f.intro.getState().phase),'LOGO_HOLD');await p.evaluate(()=>f.step(1450));assert.equal(await p.evaluate(()=>f.intro.getState().phase),'LOGO_BURST');await p.close();return{lateAttachMs:750,completeAt:1000,burstAt:1450};
 });
 await test('original 550C and later particle burst/HUD fade remain pixel-identical',async()=>{
  const old=await fixture({},true),now=await fixture(),comparisons=[];
  for(const [name,t]of [['initial',0],['logo-complete',1200],['burst',2025],['hud-crossfade',2800]]){
   if(t){await old.evaluate(t=>f.step(t),t);await now.evaluate(t=>f.step(t),t);}
   const a=await old.evaluate(()=>f.image()),b=await now.evaluate(()=>f.image());assert.equal(hash(a),hash(b),name+' particles');assert.equal(hash(await old.evaluate(()=>f.logo())),hash(await now.evaluate(()=>f.logo())),name+' 550C');comparisons.push({name,particleHash:hash(b)});
   if(t===1200)await now.screenshot({path:path.join(images,'550c-preserved.png')});
  }await old.close();await now.close();return{comparisons};
 });
 await test('full boot with blocked audio uses elapsed clock; HUD/handoff complete',async()=>{
  const p=await browser.newPage({viewport:{width:1920,height:1080}}),errors=[];p.on('pageerror',e=>errors.push(e.message));await p.goto(url+'/Themes/550W/boot.html?controlled');await p.waitForFunction(()=>window.Controller550W);
  await p.evaluate(async()=>{const {AudioKit}=await import('/Themes/550W/js/audio.js');AudioKit.prototype.preload=()=>new Promise(()=>{});window.speechSynthesis.speak=u=>u.onend?.();window.visibleFrame=null;window.addEventListener('550w-event',e=>{if(e.detail.type==='VISUAL_READY')visibleFrame=Controller550W.getState().intro.phase;});Controller550W.receive({type:'init',session:{kind:'boot',displayName:'Timing Fixture',minimumBootTimeMs:0,particleElapsedMs:650,particleClockSentUnixMs:Date.now(),settings:{particleResidueMs:500,particleToLogoMs:2000,logoHoldMs:450,durationMultiplier:1,colorProfile:'movie_red',systemLabel:'550W',logoBurstMs:900,multiWindowRevealMs:850,readyMs:50,fadeMs:120,handoffCompressMs:120,bootWatchdogMs:20000,waitForReady:true},audio:{soundEnabled:true,soundVolume:0,voiceEnabled:false}}});Controller550W.receive({type:'target',target:{processDetected:true,windowDetected:true,ready:true}});});
  await p.waitForFunction(()=>Controller550W.getState().finished,null,{timeout:24000});const state=await p.evaluate(()=>Controller550W.getState()),frame=await p.evaluate(()=>visibleFrame),times=Object.fromEntries(state.events.map(e=>[e.type,e.at]));
  assert.equal(frame,'LOGO_ASSEMBLING');assert(Math.abs(times.LOGO_HOLD-1350)<90,JSON.stringify(times));assert(Math.abs(times.LOGO_BURST-1800)<90,JSON.stringify(times));assert(state.events.some(e=>e.type==='HANDOFF_BEGIN'));assert(!state.events.some(e=>['FATAL','WATCHDOG'].includes(e.type)));assert.equal(state.nodes,47);assert.equal(state.modules.length,9);assert.equal(errors.length,0);await p.close();return{firstWebPhase:frame,logoCompleteAfterAttachMs:times.LOGO_HOLD,burstAfterAttachMs:times.LOGO_BURST,nodes:state.nodes,windows:state.modules.length,errors};
 });
 if(results.some(x=>!x.pass))process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{await browser?.close();server?.close();});

