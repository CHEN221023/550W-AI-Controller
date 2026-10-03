const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),images=path.join(root,'docs/images-v41');fs.mkdirSync(images,{recursive:true});
const results=[];let browser,server;
const baseSession=(kind='boot')=>({kind,displayName:'Fixture AI',demo:true,minimumBootTimeMs:4000,settings:{durationMultiplier:1,colorProfile:'movie_red',systemLabel:'550W',centerRevealMs:900,readyMs:600,shutdownMs:2600,fadeMs:300,bootWatchdogMs:24000,shutdownWatchdogMs:5000,waitForReady:true,allowEscape:true,allowClickSkip:false},audio:{soundEnabled:true,soundVolume:0,voiceEnabled:false,voiceVolume:0,muted:false,bootVoice:true,shutdownVoice:true,phrases:{READY:'{app} interface ready',SHUTDOWN:'正在终止人工智能会话。'}}});
async function run(name,test){try{const detail=await test();results.push({name,pass:true,...detail});console.log('PASS '+name);}catch(e){results.push({name,pass:false,error:e.stack});console.log('FAIL '+name+' '+e.message);}fs.writeFileSync(path.join(root,'docs/ready-time-v41-results.json'),JSON.stringify(results,null,2));}
const state=p=>p.evaluate(()=>Controller550W.getState());
const send=(p,m)=>p.evaluate(value=>Controller550W.receive(value),m);
async function page(kind='boot',session=baseSession(kind),options={}){const p=await browser.newPage({viewport:{width:1920,height:1080},...options});p.errors=[];p.on('pageerror',e=>p.errors.push(e.message));await p.goto(url+'/Themes/550W/'+kind+'.html?controlled');await p.waitForFunction(()=>window.Controller550W);await send(p,{type:'init',session});return p;}
let url;
(async()=>{
 server=http.createServer((req,res)=>{let file=path.resolve(root,'.'+decodeURIComponent(req.url.split('?')[0]));if(!file.startsWith(root+path.sep)||!fs.existsSync(file)){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.html')?'text/html':file.endsWith('.json')?'application/json':file.endsWith('.png')?'image/png':file.endsWith('.wav')?'audio/wav':'application/octet-stream');fs.createReadStream(file).pipe(res);});await new Promise(r=>server.listen(0,'127.0.0.1',r));url='http://127.0.0.1:'+server.address().port;
 browser=await chromium.launch({headless:true,executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--autoplay-policy=no-user-gesture-required']});



 await run('Ready duration is not extended by optional speech',async()=>{
 const p=await browser.newPage({viewport:{width:1280,height:720}});await p.goto(url+'/Themes/550W/boot.html?controlled');await p.evaluate(()=>Object.defineProperty(window,'speechSynthesis',{value:{speak:u=>{window.readyVoiceStarted=true;setTimeout(()=>u.onend(),1600);},cancel:()=>{window.readyVoiceCancelled=true;}}}));
 const s=baseSession();s.minimumBootTimeMs=0;s.settings.durationMultiplier=.1;s.settings.readyMs=500;s.audio.voiceEnabled=true;await send(p,{type:'init',session:s});await send(p,{type:'target',target:{processDetected:true,windowDetected:true,ready:true}});await p.waitForFunction(()=>Controller550W.getState().finished,{},{timeout:12000});
 const end=await state(p),start=end.events.find(x=>x.type==='TARGET_READY').at,stop=end.events.find(x=>x.type==='INTERFACE_HANDOFF').at;assert(stop-start>=40&&stop-start<200);assert(await p.evaluate(()=>readyVoiceStarted));await p.close();return{expected:50,actual:stop-start,voiceDuration:1600};
 });const failures=results.filter(x=>!x.pass);if(failures.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{await browser?.close();server?.close();});
