const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),images=path.join(root,'docs/images-v41');fs.mkdirSync(images,{recursive:true});
const results=[];let browser,server;
const baseSession=(kind='boot')=>({kind,displayName:'Fixture AI',demo:true,minimumBootTimeMs:4000,settings:{durationMultiplier:1,colorProfile:'movie_red',systemLabel:'550W',centerRevealMs:900,readyMs:600,shutdownMs:2600,fadeMs:300,bootWatchdogMs:24000,shutdownWatchdogMs:5000,waitForReady:true,allowEscape:true,allowClickSkip:false},audio:{soundEnabled:true,soundVolume:0,voiceEnabled:false,voiceVolume:0,muted:false,bootVoice:true,shutdownVoice:true,phrases:{READY:'{app} interface ready',SHUTDOWN:'正在终止人工智能会话。'}}});
async function run(name,test){try{const detail=await test();results.push({name,pass:true,...detail});console.log('PASS '+name);}catch(e){results.push({name,pass:false,error:e.stack});console.log('FAIL '+name+' '+e.message);}fs.writeFileSync(path.join(root,'docs/web-test-v41-results.json'),JSON.stringify(results,null,2));}
const state=p=>p.evaluate(()=>Controller550W.getState());
const send=(p,m)=>p.evaluate(value=>Controller550W.receive(value),m);
async function page(kind='boot',session=baseSession(kind),options={}){const p=await browser.newPage({viewport:{width:1920,height:1080},...options});p.errors=[];p.on('pageerror',e=>p.errors.push(e.message));await p.goto(url+'/Themes/550W/'+kind+'.html?controlled');await p.waitForFunction(()=>window.Controller550W);await send(p,{type:'init',session});return p;}
let url;
(async()=>{
 server=http.createServer((req,res)=>{let file=path.resolve(root,'.'+decodeURIComponent(req.url.split('?')[0]));if(!file.startsWith(root+path.sep)||!fs.existsSync(file)){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.html')?'text/html':file.endsWith('.json')?'application/json':file.endsWith('.png')?'image/png':file.endsWith('.wav')?'audio/wav':'application/octet-stream');fs.createReadStream(file).pipe(res);});await new Promise(r=>server.listen(0,'127.0.0.1',r));url='http://127.0.0.1:'+server.address().port;
 browser=await chromium.launch({headless:true,executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--autoplay-policy=no-user-gesture-required']});


 for(const [name,coreMs,partMs,multiplier] of [['short',500,180,1],['long',1600,650,1.5]])await run('V4.1 '+name+' exact durations / synchronous scan / stable layers',async()=>{
  const s=baseSession();Object.assign(s.settings,{coreHoldMs:coreMs,coreDissolveMs:partMs,particleResidueMs:partMs,logoRevealMs:partMs,logoHoldMs:partMs,logoBurstMs:partMs,multiWindowRevealMs:partMs,durationMultiplier:multiplier});s.minimumBootTimeMs=0;
  const p=await page('boot',s);await p.evaluate(()=>{window.audit=[];window.oldCanvases=[];window.sampler=setInterval(()=>{const sh=document.querySelector('.controller-host').shadowRoot,st=Controller550W.getState(),core=sh.querySelector('.boot-core img'),scan=sh.querySelector('.core-main-sweep'),canvas=sh.querySelector('.intro-particles');if(!canvas)return;if(!oldCanvases.includes(canvas))oldCanvases.push(canvas);audit.push({phase:st.intro?.phase,core:st.intro?.coreVisible,logo:st.intro?.logoVisible,clip:core.style.clipPath,scanY:scan?new DOMMatrix(getComputedStyle(scan).transform).f:0,background:getComputedStyle(sh.querySelector('.composition-layer')).backgroundColor,hostOpacity:getComputedStyle(document.querySelector('.controller-host')).opacity,canvasWidth:canvas.width,canvasHeight:canvas.height});},10);});
  await send(p,{type:'target',target:{processDetected:true,windowDetected:true,ready:true}});
  if(name==='long'){
   for(const [phase,delay,file]of [['CORE_SCANNING',200,'01-four-points'],['CORE_REVEAL',350,'02-synchronous-reveal'],['CORE_HOLD',0,'03-core-lit'],['LOGO_HOLD',0,'04-original-550c']]){await p.waitForFunction(x=>Controller550W.getState().intro?.phase===x,phase,{timeout:18000,polling:5});await p.waitForTimeout(delay);await p.screenshot({path:path.join(images,file+'.png')});}
  }
  await p.waitForFunction(()=>Controller550W.getState().intro?.phase==='MULTIWINDOW_ACTIVE',{},{timeout:24000,polling:5});
  const a=await state(p),times=Object.fromEntries(a.events.map(x=>[x.type,x.at])),timings=[];
  for(const [from,to,expected] of [['CORE_SCANNING','CORE_PARTICLES',coreMs],['CORE_PARTICLES','PARTICLE_RESIDUE',partMs],['PARTICLE_RESIDUE','LOGO_ASSEMBLING',partMs],['LOGO_ASSEMBLING','LOGO_HOLD',partMs],['LOGO_HOLD','LOGO_BURST',partMs],['LOGO_BURST','MULTIWINDOW_REVEAL',partMs],['MULTIWINDOW_REVEAL','MULTIWINDOW_ACTIVE',partMs]]){const actual=times[to]-times[from],wanted=expected*multiplier;assert(Math.abs(actual-wanted)<85,from+': '+actual+' vs '+wanted);timings.push({from,to,expected:wanted,actual});}
  const audit=await p.evaluate(()=>{clearInterval(sampler);return{samples:audit,canvases:oldCanvases.length,oldFrames:document.querySelector('.controller-host').shadowRoot.querySelectorAll('.core-locator').length};});
  assert.equal(audit.canvases,1);assert.equal(audit.oldFrames,0);assert(audit.samples.every(x=>x.hostOpacity==='1'&&x.background==='rgb(0, 0, 0)'&&x.canvasWidth===1920&&x.canvasHeight===1080));assert(!audit.samples.some(x=>x.core&&x.logo));
  const reveal=audit.samples.filter(x=>x.phase==='CORE_REVEAL');assert(reveal.length>3);for(const frame of reveal){const m=frame.clip.match(/inset\(0px 0px ([\d.]+)%(?: 0px)?\)/);assert(m,frame.clip);assert(Math.abs(frame.scanY-(1-Number(m[1])/100)*330)<.1,'scan and image mask mismatch');}
  if(name==='short'){await p.waitForFunction(()=>Controller550W.getState().finished,{},{timeout:16000});const end=await state(p);assert.equal(end.nodes,47);assert.equal(end.modules.length,9);assert(!end.events.some(x=>['FATAL','WATCHDOG'].includes(x.type)));}else await p.keyboard.press('Escape');
  assert.equal(p.errors.length,0);await p.close();return{timings,samples:audit.samples.length,scanSamples:reveal.length,persistentParticleCanvases:audit.canvases,background:'opaque black until formal handoff',errors:p.errors};
 });
 const failures=results.filter(x=>!x.pass);console.log(JSON.stringify({passed:results.length-failures.length,total:results.length}));if(failures.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{await browser?.close();server?.close();});
