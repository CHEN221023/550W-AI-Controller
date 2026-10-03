// Narrow continuity check of the unchanged production web choreography.
const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),results=[];let browser,server;
(async()=>{
 server=http.createServer((req,res)=>{const file=path.resolve(root,'.'+decodeURIComponent(req.url.split('?')[0]));if(!file.startsWith(root+path.sep)||!fs.existsSync(file)){res.writeHead(404);return res.end();}res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.html')?'text/html':file.endsWith('.json')?'application/json':'application/octet-stream');fs.createReadStream(file).pipe(res);});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 browser=await chromium.launch({headless:true,executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 for(const late of [0,4400]){
  const page=await browser.newPage({viewport:{width:1920,height:1080}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:'+server.address().port+'/Themes/550W/boot.html?controlled');await page.waitForFunction(()=>window.Controller550W);
  await page.evaluate(late=>{
   window.samples=[];window.handoff=false;window.addEventListener('550w-event',e=>{if(e.detail.type==='HANDOFF_BEGIN')handoff=true;});
   Controller550W.receive({type:'init',session:{kind:'boot',displayName:'Continuity fixture',minimumBootTimeMs:0,particleElapsedMs:late,particleClockSentUnixMs:Date.now(),
    settings:{particleResidueMs:500,particleToLogoMs:1500,logoHoldMs:300,logoBurstMs:500,multiWindowRevealMs:100,durationMultiplier:.75,readyMs:50,fadeMs:120,handoffCompressMs:120,colorProfile:'movie_red',systemLabel:'550W',waitForReady:true,bootWatchdogMs:20000},
    audio:{soundEnabled:false,voiceEnabled:false,muted:true}}});
   Controller550W.receive({type:'target',target:{processDetected:true,windowDetected:true,ready:true}});
   const host=document.querySelector('.controller-host'),shadow=host.shadowRoot,stage=shadow.querySelector('.dsh550c-stage'),composition=shadow.querySelector('.composition-layer'),app=shadow.querySelector('#app');
   function sample(){if(handoff||Controller550W.getState().finished)return;const state=Controller550W.getState();
    samples.push({at:state.elapsed,phase:state.intro?.phase,root:[document.documentElement,document.body,document.querySelector('#screen'),host,stage,composition].map(n=>{const s=getComputedStyle(n);return{opacity:Number(s.opacity),visibility:s.visibility,display:s.display,animation:s.animationName};}),hudOpacity:Number(getComputedStyle(app).opacity)});requestAnimationFrame(sample);
   }requestAnimationFrame(sample);
  },late);
  await page.waitForFunction(()=>Controller550W.getState().finished,null,{timeout:22000});
  const data=await page.evaluate(()=>({state:Controller550W.getState(),samples}));
  assert.equal(errors.length,0);assert(!data.state.events.some(e=>['FATAL','WATCHDOG'].includes(e.type)));assert.equal(data.state.modules.length,9);assert.equal(data.state.nodes,47);
  assert(data.samples.length>100);assert(data.samples.every(f=>f.root.every(s=>s.opacity===1&&s.visibility==='visible'&&s.display!=='none'&&s.animation==='none')),'root/container changed opacity, visibility or blink animation');
  const events=Object.fromEntries(data.state.events.map(e=>[e.type,e.at])),hud=data.samples.filter(f=>['MULTIWINDOW_REVEAL','MULTIWINDOW_ACTIVE'].includes(f.phase));
  const first=hud.find(f=>f.hudOpacity>0);assert(first,'HUD not displayed');assert(first.at-events.MULTIWINDOW_REVEAL<90,'HUD has an extra transition delay');
  assert(hud.every((f,i)=>i===0||f.hudOpacity>=hud[i-1].hudOpacity),'HUD repeatedly hidden during reveal');
  assert(hud.filter(f=>f.phase==='MULTIWINDOW_ACTIVE').every(f=>f.hudOpacity===1),'HUD blinks after reveal');
  results.push({pass:true,nativeElapsedMs:late,rootSamples:data.samples.length,allRootLayersStable:true,hudFirstVisibleAfterRevealMs:Math.round(first.at-events.MULTIWINDOW_REVEAL),hudMonotonicReveal:true,hudWindows:data.state.modules.length,nodes:data.state.nodes,handoffCompleted:data.state.handoffInProgress,errors});
  console.log('PASS web continuity; native elapsed '+late+'ms; '+data.samples.length+' stable root frames');await page.close();
 }
})().catch(e=>{results.push({pass:false,error:e.stack});console.error(e);process.exitCode=1;}).finally(async()=>{await browser?.close();server?.close();fs.writeFileSync(path.join(root,'docs/web-test-v46-results.json'),JSON.stringify(results,null,2)+'\n');});
