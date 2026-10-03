const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),images=path.join(root,'docs/images-v4');fs.mkdirSync(images,{recursive:true});
const results=[];let browser,server;
const baseSession=(kind='boot')=>({kind,displayName:'Fixture AI',demo:true,minimumBootTimeMs:4000,settings:{durationMultiplier:1,colorProfile:'movie_red',systemLabel:'550W',centerRevealMs:900,readyMs:600,shutdownMs:2600,fadeMs:300,bootWatchdogMs:24000,shutdownWatchdogMs:5000,waitForReady:true,allowEscape:true,allowClickSkip:false},audio:{soundEnabled:true,soundVolume:0,voiceEnabled:false,voiceVolume:0,muted:false,bootVoice:true,shutdownVoice:true,phrases:{READY:'{app} interface ready',SHUTDOWN:'正在终止人工智能会话。'}}});
async function run(name,test){try{const detail=await test();results.push({name,pass:true,...detail});console.log('PASS '+name);}catch(e){results.push({name,pass:false,error:e.stack});console.log('FAIL '+name+' '+e.message);}fs.writeFileSync(path.join(root,'docs/web-test-v4-results.json'),JSON.stringify(results,null,2));}
const state=p=>p.evaluate(()=>Controller550W.getState());
const send=(p,m)=>p.evaluate(value=>Controller550W.receive(value),m);
async function page(kind='boot',session=baseSession(kind),options={}){const p=await browser.newPage({viewport:{width:1920,height:1080},...options});p.errors=[];p.on('pageerror',e=>p.errors.push(e.message));await p.goto(url+'/Themes/550W/'+kind+'.html?controlled');await p.waitForFunction(()=>window.Controller550W);await send(p,{type:'init',session});return p;}
let url;
(async()=>{
 server=http.createServer((req,res)=>{let file=path.resolve(root,'.'+decodeURIComponent(req.url.split('?')[0]));if(!file.startsWith(root+path.sep)||!fs.existsSync(file)){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.html')?'text/html':file.endsWith('.json')?'application/json':file.endsWith('.png')?'image/png':file.endsWith('.wav')?'audio/wav':'application/octet-stream');fs.createReadStream(file).pipe(res);});await new Promise(r=>server.listen(0,'127.0.0.1',r));url='http://127.0.0.1:'+server.address().port;
 browser=await chromium.launch({headless:true,executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--autoplay-policy=no-user-gesture-required']});

 await run('V4 focused visual chain, original vectors, preserved HUD and transparent handoff',async()=>{
   const s=baseSession();s.settings.coreHoldMs=1000;s.settings.logoHoldMs=1100;s.settings.handoffCompressMs=700;s.settings.fadeMs=700;
   const p=await page('boot',s);await send(p,{type:'target',target:{processDetected:true,windowDetected:true,ready:true}});
   await p.evaluate(()=>{window.samples=[];window.sampleTimer=setInterval(()=>{const s=Controller550W.getState();if(s.intro)samples.push({...s.intro});},10);});
   const wait=phase=>p.waitForFunction(x=>Controller550W.getState().intro?.phase===x,phase,{timeout:20000,polling:5});
   await wait('CORE_SCANNING');await p.screenshot({path:path.join(images,'01-scanning.png')});
   await wait('CORE_REVEAL');await p.waitForTimeout(310);await p.screenshot({path:path.join(images,'02-loading.png')});
   await wait('CORE_HOLD');
   const core=await p.evaluate(()=>{const sh=document.querySelector('.controller-host').shadowRoot,c=sh.querySelector('.mechanical-core'),label=sh.querySelector('.core-status'),a=c.getBoundingClientRect(),b=label.getBoundingClientRect();return{gap:b.top-a.bottom,chinese:getComputedStyle(label).fontSize,english:getComputedStyle(label.querySelector('small')).fontSize,label:label.textContent,lensOpacity:getComputedStyle(sh.querySelector('.core-lens-shade')).opacity,imageFilter:getComputedStyle(c.querySelector('img')).filter,backgrounds:[document.documentElement,document.body,document.querySelector('#screen'),document.querySelector('.controller-host'),sh.querySelector('.dsh550c-stage'),sh.querySelector('.composition-layer')].map(x=>getComputedStyle(x).backgroundColor),crt:getComputedStyle(sh.querySelector('.composition-layer'),'::before').display};});
   assert.equal(core.gap,100);assert.equal(core.chinese,'12px');assert.equal(core.english,'9px');assert.equal(core.lensOpacity,'0');assert.equal(core.imageFilter,'brightness(1)');assert.equal(core.crt,'none');assert(core.backgrounds.every(x=>x==='rgb(0, 0, 0)'));assert(core.label.includes('MOSS 核心接入中')&&core.label.includes('550C JOINT CONTROL NODE'));
   await p.setViewportSize({width:1920,height:1200});await p.screenshot({path:path.join(images,'03-core-letterbox.png')});await p.setViewportSize({width:1920,height:1080});
   await wait('CORE_PARTICLES');await p.waitForTimeout(200);await p.screenshot({path:path.join(images,'04-core-particles.png')});
   await wait('LOGO_ASSEMBLING');await p.waitForTimeout(450);await p.screenshot({path:path.join(images,'05-550c-assembling.png')});
   await wait('LOGO_HOLD');
   const logo=await p.evaluate(async()=>{const {BOOT_MARKUP}=await import('./js/mother-assets.js'),t=document.createElement('template');t.innerHTML=BOOT_MARKUP;const a=t.content.querySelector('svg'),b=document.querySelector('.controller-host').shadowRoot.querySelector('.intro-logo svg');const paths=n=>[...n.querySelectorAll('path')].map(p=>({d:p.getAttribute('d'),cls:p.getAttribute('class')}));return{expected:paths(a),actual:paths(b),viewBox:b.getAttribute('viewBox'),filter:b.querySelector('feGaussianBlur').getAttribute('stdDeviation'),colors:[...b.querySelectorAll('path')].map(p=>getComputedStyle(p).fill),aria:b.parentElement.getAttribute('aria-label')};});
   assert.deepEqual(logo.actual,logo.expected);assert.equal(logo.viewBox,'0 0 800 230');assert.equal(logo.filter,'2.2');assert.equal(logo.aria,'550C');assert(logo.colors.includes('rgb(255, 45, 45)')&&logo.colors.includes('rgb(255, 255, 255)'));
   await p.setViewportSize({width:2560,height:1080});await p.screenshot({path:path.join(images,'06-550c-pillarbox.png')});await p.setViewportSize({width:1920,height:1080});
   await wait('LOGO_BURST');await p.waitForTimeout(680);await p.screenshot({path:path.join(images,'07-logo-burst.png')});
   await wait('MULTIWINDOW_ACTIVE');await p.waitForTimeout(500);await p.screenshot({path:path.join(images,'08-original-hud.png')});
   await p.waitForFunction(()=>Controller550W.getState().handoffInProgress,{},{timeout:20000,polling:5});await p.waitForTimeout(170);
   const handoff=await p.evaluate(()=>{const sh=document.querySelector('.controller-host').shadowRoot,c=sh.querySelector('.composition-layer'),style=getComputedStyle(c);return{scale:new DOMMatrix(style.transform).a,opacity:Number(style.opacity),body:getComputedStyle(document.body).backgroundColor};});assert(handoff.scale>0&&handoff.scale<1);assert(handoff.opacity>0&&handoff.opacity<1);assert.equal(handoff.body,'rgba(0, 0, 0, 0)');
   await p.waitForFunction(()=>Controller550W.getState().finished);const end=await state(p),samples=await p.evaluate(()=>{clearInterval(sampleTimer);return window.samples;});assert(!samples.some(x=>x.coreVisible&&x.logoVisible));assert.equal(end.modules.length,9);assert.equal(end.nodes,47);assert(end.intro.disposed);assert(!end.events.some(x=>['FATAL','WATCHDOG'].includes(x.type)));assert.equal(p.errors.length,0);
   const phases=['CORE_SCANNING','CORE_REVEAL','CORE_ILLUMINATED','CORE_LABEL','CORE_HOLD','CORE_PARTICLES','PARTICLE_RESIDUE','LOGO_ASSEMBLING','LOGO_HOLD','LOGO_BURST','MULTIWINDOW_REVEAL','MULTIWINDOW_ACTIVE'];assert.deepEqual(end.events.filter(x=>phases.includes(x.type)).map(x=>x.type),phases);
   await p.close();return{core,logoPaths:logo.actual.length,originalVectorPathsExact:true,overlapSamples:samples.length,phases,handoff,originalWindows:end.modules.length,originalNodes:end.nodes,errors:p.errors,desktopWindowsShown:0};
 });
 const failures=results.filter(x=>!x.pass);console.log(JSON.stringify({passed:results.length-failures.length,total:results.length}));if(failures.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{await browser?.close();server?.close();});
