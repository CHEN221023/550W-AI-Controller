const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),themeRoot=path.join(root,'Themes'),out=path.join(root,'docs','images');fs.mkdirSync(out,{recursive:true});
const report=[];
const server=http.createServer((req,res)=>{
 const p=path.resolve(themeRoot,'.'+decodeURIComponent(new URL(req.url,'http://local').pathname));
 if(!p.startsWith(themeRoot+path.sep)||!fs.existsSync(p)||!fs.statSync(p).isFile()){res.writeHead(404);res.end();return;}
 res.setHeader('Content-Type',p.endsWith('.js')?'text/javascript':p.endsWith('.css')?'text/css':p.endsWith('.json')?'application/json':p.endsWith('.wav')?'audio/wav':'text/html');fs.createReadStream(p).pipe(res);
});
const settings={durationMultiplier:1,colorProfile:'amber',systemLabel:'550W',centerRevealMs:700,diagnosticsMinimumMs:1400,readyMs:600,shutdownMs:2200,fadeMs:300,bootWatchdogMs:20000,shutdownWatchdogMs:5000,waitForReady:true,allowEscape:true,allowClickSkip:false};
const session=(kind='boot',overrides={})=>({kind,displayName:'ChatGPT / TEST FIXTURE',demo:true,minimumBootTimeMs:3500,settings:{...settings,...overrides},audio:{soundEnabled:true,soundVolume:0,voiceEnabled:false,voiceVolume:60,muted:false,bootVoice:true,shutdownVoice:true}});
async function run(){
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const port=server.address().port;
 const executable=process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
 const browser=await chromium.launch({executablePath:executable,headless:true,args:['--autoplay-policy=no-user-gesture-required']});
 const create=async(kind,overrides={},audioOverrides={})=>{
  const page=await browser.newPage({viewport:{width:1600,height:900}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>{window.chrome=window.chrome||{};window.chrome.webview={postMessage:m=>{window.testEvents=window.testEvents||[];window.testEvents.push(m);if(m.type==='VOICE_REQUEST')setTimeout(()=>window.Controller550W.receive({type:'VOICE_DONE',id:m.id}),20);},addEventListener:()=>{}};});
  await page.goto(`http://127.0.0.1:${port}/550W/${kind}.html`);await page.waitForFunction(()=>!!window.Controller550W);
  const value=session(kind,overrides);Object.assign(value.audio,audioOverrides);
  await page.evaluate(s=>window.Controller550W.receive({type:'init',session:s}),value);
  await page.waitForTimeout(100);return {page,errors};
 };
 const target=(page,ready)=>page.evaluate(ready=>window.Controller550W.receive({type:'target',target:{name:'Test AI',processDetected:true,windowDetected:true,ready}}),ready);
 for(const seconds of [2,5,15]){
  const {page,errors}=await create('boot');await target(page,false);
  await page.waitForTimeout(seconds*1000-100);if(seconds>3){assert.equal((await page.evaluate(()=>window.Controller550W.getState())).phase,'WAITING');}
  if(seconds===5){await page.screenshot({path:path.join(out,'boot-waiting.png')});}
  await target(page,true);await page.waitForFunction(()=>window.Controller550W.getState().finished,{},{timeout:7000});
  const state=await page.evaluate(()=>window.Controller550W.getState());assert.equal(errors.length,0,JSON.stringify(errors));assert.equal(state.events.filter(x=>x.type==='TARGET_READY').length,1);assert(state.elapsed>=3500);
  report.push({test:`ready-after-${seconds}s`,pass:true,elapsed:Math.round(state.elapsed),events:state.events});await page.close();
 }
 {
  const {page,errors}=await create('boot');await page.evaluate(()=>{for(const key of ['cpu','memory','gpu','local','dns','external'])for(let i=0;i<5;i++)window.Controller550W.receive({type:'diagnostics',row:{key,label:key.toUpperCase(),value:'ONLINE · TEST',status:key==='external'?'stable':'online'}});});
  await page.waitForTimeout(1900);let state=await page.evaluate(()=>window.Controller550W.getState());
  assert.equal(state.audioEvents.filter(x=>x.key==='CHECK_OK').length,6);assert.equal(errors.length,0);await target(page,true);
  await page.waitForFunction(()=>window.Controller550W.getState().events.some(x=>x.type==='TARGET_READY'));await page.waitForTimeout(450);await page.screenshot({path:path.join(out,'boot-ready.png')});
  await page.waitForFunction(()=>window.Controller550W.getState().finished);state=await page.evaluate(()=>window.Controller550W.getState());assert.equal(state.audioEvents.filter(x=>x.key==='TARGET_READY').length,1);
  report.push({test:'diagnostic-sounds-once',pass:true,audioEvents:state.audioEvents});await page.close();
 }
 for(const color of ['amber','green','cyan','white']){
  const {page,errors}=await create('boot',{colorProfile:color});await page.waitForTimeout(1600);await page.screenshot({path:path.join(out,`boot-${color}.png`)});assert.equal(errors.length,0);report.push({test:`palette-${color}`,pass:true});await page.close();
 }
 {
  const {page,errors}=await create('shutdown');await page.waitForTimeout(900);await page.screenshot({path:path.join(out,'shutdown.png')});await page.waitForFunction(()=>window.Controller550W.getState().finished);
  const state=await page.evaluate(()=>window.Controller550W.getState());assert.deepEqual(state.events.filter(x=>['SHUTDOWN_BEGIN','HUD_OFFLINE','CRT_COLLAPSE','POWER_OFF'].includes(x.type)).map(x=>x.type),['SHUTDOWN_BEGIN','HUD_OFFLINE','CRT_COLLAPSE','POWER_OFF']);assert.equal(state.audioEvents.filter(x=>x.key==='RELAY_OFF').length,6);assert.equal(errors.length,0);report.push({test:'independent-shutdown-audio',pass:true,events:state.events,audioEvents:state.audioEvents});await page.close();
 }
 {
  const {page}=await create('boot');await page.waitForTimeout(300);await page.keyboard.press('Escape');await page.waitForFunction(()=>window.Controller550W.getState().finished);report.push({test:'escape-during-intro',pass:true});await page.close();
 }
 {
  const {page}=await create('boot');await page.waitForTimeout(1500);await page.mouse.click(800,450);assert.equal((await page.evaluate(()=>window.Controller550W.getState())).finished,false);report.push({test:'click-skip-default-disabled',pass:true});await page.close();
 }
 {
  const {page}=await create('boot',{allowClickSkip:true});await page.waitForTimeout(1500);await page.mouse.click(800,450);await page.waitForFunction(()=>window.Controller550W.getState().finished);report.push({test:'click-skip-enabled',pass:true});await page.close();
 }
 {
  const {page}=await create('boot',{bootWatchdogMs:1100});await page.waitForFunction(()=>window.Controller550W.getState().finished);assert((await page.evaluate(()=>window.Controller550W.getState())).events.some(x=>x.type==='WATCHDOG'));report.push({test:'javascript-watchdog',pass:true});await page.close();
 }
 {
  const {page}=await create('boot');await page.evaluate(()=>window.dispatchEvent(new ErrorEvent('error',{message:'intentional fixture error'})));await page.waitForFunction(()=>window.Controller550W.getState().finished);assert(await page.evaluate(()=>window.testEvents.some(x=>x.type==='FATAL')));report.push({test:'javascript-failure-releases-overlay',pass:true});await page.close();
 }
 {
  const page=await browser.newPage({viewport:{width:1280,height:720},deviceScaleFactor:1.25});await page.goto(`http://127.0.0.1:${port}/550W/boot.html?readyAfter=2000`);await page.waitForFunction(()=>window.Controller550W?.getState().finished,{},{timeout:7000});report.push({test:'browser-125-percent-dpi',pass:true});await page.close();
 }
 for(const [name,audioOverrides,sfx,voice] of [
  ['mute-overrides-all',{muted:true,voiceEnabled:true},false,false],
  ['voice-independent-from-sfx',{soundEnabled:false,voiceEnabled:true},false,true],
  ['sfx-independent-from-voice',{soundEnabled:true,voiceEnabled:false},true,false],
  ['boot-voice-toggle',{soundEnabled:false,voiceEnabled:true,bootVoice:false},false,false]
 ]){
  const {page,errors}=await create('boot',{},audioOverrides);await target(page,true);await page.waitForFunction(()=>window.Controller550W.getState().finished);
  const state=await page.evaluate(()=>window.Controller550W.getState());const requests=await page.evaluate(()=>window.testEvents.filter(x=>x.type==='VOICE_REQUEST'));
  assert.equal(state.audioEvents.length>0,sfx);assert.equal(requests.length>0,voice);assert.equal(errors.length,0);report.push({test:name,pass:true,soundEvents:state.audioEvents.length,voiceRequests:requests.length});await page.close();
 }
 {
  const {page,errors}=await create('boot',{}, {audioFiles:{POWER_ON:'missing.wav',CORE_EXPAND:'missing.ogg',TARGET_READY:'missing.mp3'},voiceEnabled:true,voiceFiles:{READY:'missing.wav'}});await target(page,true);await page.waitForFunction(()=>window.Controller550W.getState().finished,{},{timeout:8000});assert.equal(errors.length,0);report.push({test:'missing-custom-audio-and-voice-silent',pass:true});await page.close();
 }
 {
  const {page,errors}=await create('shutdown',{}, {soundEnabled:false,voiceEnabled:true,shutdownVoice:false});await page.waitForFunction(()=>window.Controller550W.getState().finished);assert.equal(await page.evaluate(()=>window.testEvents.filter(x=>x.type==='VOICE_REQUEST').length),0);assert.equal(errors.length,0);report.push({test:'shutdown-voice-toggle',pass:true});await page.close();
 }
 await browser.close();fs.writeFileSync(path.join(root,'docs','web-test-results.json'),JSON.stringify(report,null,2));console.log(JSON.stringify({passed:report.length,report}));
}
run().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>server.close());
