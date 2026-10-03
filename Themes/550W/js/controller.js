import {CSS_550C,BOOT_MARKUP,APP_MARKUP} from './mother-assets.js';
import {createShow} from './mother-show.js';
import {AudioKit} from './audio.js';
import {createIntro,introSettings} from './intro.js';

// Authored windows, SVGs and nine phases come from the unchanged upstream mother.
const mode=document.body.dataset.mode;
const post=message=>{window.chrome?.webview?.postMessage(message);window.dispatchEvent(new CustomEvent('550w-event',{detail:message}));};
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,Math.max(0,ms)));
let startFps;let session,settings,audio,mother,started=0,finished=false,active=false,hudVisible=false,choreographyComplete=false,intro,handoffInProgress=false,phase='PREPARING',watchdog,frameId;
let target={processDetected:false,windowDetected:false,ready:false,state:'initializing'};
const rows=new Map(),events=[],modules=[];
const host=document.createElement('div');host.className='controller-host';document.querySelector('#screen').append(host);
const shadow=host.attachShadow({mode:'open'}),style=document.createElement('style');style.textContent=CSS_550C;shadow.append(style);
const custom=document.createElement('link');custom.rel='stylesheet';custom.href=new URL('../css/controller.css',import.meta.url);shadow.append(custom);
const stage=document.createElement('div');stage.className='dsh550c-stage';stage.innerHTML='<div class="composition-layer">'+BOOT_MARKUP+APP_MARKUP+'</div>';shadow.append(stage);const composition=stage.querySelector('.composition-layer');
const $=s=>stage.querySelector(s),scale=ms=>ms*(settings?.durationMultiplier||1);
if(mode==='boot')$('#boot').style.visibility='hidden';
const event=(type,detail='')=>{events.push({type,detail,at:Math.round(performance.now()-started)});post({type,detail});};
let core,eye;if(mode==='boot')composition.classList.add('intro-active');else{core=document.createElement('div');core.className='mechanical-core';eye=document.createElement('img');eye.alt='';eye.src=new URL('../assets/core.png',import.meta.url);eye.crossOrigin='anonymous';core.append(eye);composition.append(core);}
function resize(){const s=Math.min(innerWidth/1920,innerHeight/1080);host.style.width='1920px';host.style.height='1080px';host.style.transform=`translate(-50%,-50%) scale(${s})`;}
window.addEventListener('resize',resize);resize();
function setPhase(value){phase=value;if($('#b-stage'))$('#b-stage').textContent=value;}
// Exactly eight existing text rows. Their DOM, dimensions and positions are retained.
const slots=[['cpu','CPU CORE'],['memory','MEMORY'],['gpu','GPU'],['vram','VRAM TOTAL'],['dns','DNS'],['external','EXTERNAL ROUTE'],['vpn','VPN ADAPTER'],['target','TARGET AI']];
function paintRows(){const existing=$('#teleBody').querySelectorAll('.dt');slots.forEach(([key,label],i)=>{const e=existing[i];if(!e)return;e.dataset.real='true';const r=rows.get(key);e.querySelector('.k').textContent=label;const v=e.querySelector('.v');v.textContent=r?.value||'PENDING';v.title=r?.detail||r?.value||'';v.className='v '+(r?.status||'waiting');});}
function diagnosticSound(row){if(mode!=='boot'||!hudVisible||!['online','stable','ready','detected'].includes(row.status))return;audio?.play('CHECK_OK','diagnostic:'+row.key,.22);if(row.key==='external')audio?.speak('NETWORK');}
function diagnostic(row){rows.set(row.key,row);paintRows();diagnosticSound(row);}
function updateTarget(value){target=value;rows.set('target',{key:'target',value:value.ready?'INTERFACE READY':value.processDetected?'AI PROCESS DETECTED':'WAITING FOR PROCESS',status:value.ready?'ready':'waiting'});paintRows();if(active&&value.processDetected){audio?.play('PROCESS_DETECTED');audio?.speak('PROCESS');}}
function authorEvent(type,detail){
  if(finished)return;event(type,detail);
  if(type==='MULTIWINDOW_ACTIVE')startFps?.();
  if(type==='LOGO_BEGIN')audio.play('CORE_EXPAND');
  if(type==='HUD_VISIBLE'){hudVisible=true;core?.classList.add('released');audio.play('HUD_SCAN');event('BOOT_INTRO_FINISHED');paintRows();for(const row of rows.values())if(row.key!=='target')diagnosticSound(row);updateTarget(target);}
  if(type==='MAJOR_MODULE'){modules.push({name:detail,at:Math.round(performance.now()-started)});if(['ACCESS AUTHORIZATION','OVERRIDE CONTROLLER','OVERRIDE SUMMARY'].includes(detail))audio.play('CHECK_OK','module:'+detail,.38);}
  if(type==='CHOREOGRAPHY_COMPLETE')choreographyComplete=true;
}
function timers(){clearTimeout(watchdog);watchdog=setTimeout(()=>{if(!finished){event('WATCHDOG');finish();}},mode==='boot'?settings.bootWatchdogMs:settings.shutdownWatchdogMs);let frames=0,last=performance.now();const frame=now=>{if(finished)return;frames++;if(now-last>=1000){if($('#b-fps'))$('#b-fps').textContent=Math.round(frames*1000/(now-last));frames=0;last=now;}frameId=requestAnimationFrame(frame);};startFps=()=>{last=performance.now();frameId=requestAnimationFrame(frame);};if(!intro)startFps();}
async function boot(){
  active=true;timers();setPhase('PARTICLE_RESIDUE');audio.speak('BOOT');
  let originalChoreography;await intro.play(()=>{originalChoreography=mother.start();});if(finished)return;
  await originalChoreography;if(finished)return;
  let waiting=false;while(!finished){const elapsed=performance.now()-started+(session.hostElapsedMs||0);if(choreographyComplete&&elapsed>=session.minimumBootTimeMs&&(!settings.waitForReady||target.ready))break;if(!waiting){waiting=true;setPhase('WAITING');$('#final').textContent='INTERFACE INITIALIZING';$('#final').classList.add('show');event('WAITING_LOOP_READY');}await sleep(120);}
  if(finished)return;setPhase('SYSTEM READY');$('#final').textContent=settings.waitForReady||target.ready?'SYSTEM READY':'INTERFACE HANDOFF';$('#final').classList.add('show');event('TARGET_READY');audio.play('TARGET_READY');if(target.ready)audio.speak('READY');
  // ReadyMs controls the visible hold. Optional speech cannot add a hidden time floor.
  await sleep(scale(settings.readyMs));if(finished)return;audio.play('UNLOCK');event('INTERFACE_HANDOFF');await handoff();
}
async function shutdown(){
  if(active||finished)return;active=true;started=performance.now();timers();core.classList.remove('released','expanding','charged');core.style.opacity='0';
  setPhase('SHUTDOWN_BEGIN');event('SHUTDOWN_BEGIN');audio.play('SHUTDOWN_BEGIN');audio.speak('SHUTDOWN');
  const total=scale(settings.shutdownMs),peripherals=['#w-code','#w-tele','#w-node'];
  for(let i=0;i<3;i++){if(finished)return;$(peripherals[i]).animate([{opacity:1},{opacity:.12}],{duration:total*.14,fill:'forwards'});audio.play('RELAY_OFF','relay:'+i,.35);event('MODULE_OFFLINE',peripherals[i]);if(i===1)audio.speak('NETWORK_CLOSED');await sleep(total*.12);}
  if(finished)return;setPhase('CORE STANDBY');audio.speak('STANDBY');const app=$('#app');app.style.transformOrigin='50% 50%';
  const collapse=app.animate([{transform:'scale(1)',opacity:1},{transform:'scale(.12)',opacity:0}],{duration:total*.26,easing:'cubic-bezier(.55,0,.2,1)',fill:'forwards'});
  core.animate([{opacity:0,transform:'translate(-50%,-50%) scale(.88)'},{opacity:1,transform:'translate(-50%,-50%) scale(1)'}],{duration:total*.2,fill:'forwards'});event('HUD_OFFLINE');await collapse.finished;if(finished)return;
  event('CRT_COLLAPSE');audio.play('CRT_COLLAPSE');await core.animate([{transform:'translate(-50%,-50%) scale(1,1)',opacity:1},{transform:'translate(-50%,-50%) scale(1,.012)',opacity:.85}],{duration:total*.16,fill:'forwards'}).finished;
  if(finished)return;const point=document.createElement('div');point.className='power-point';stage.append(point);core.animate([{transform:'translate(-50%,-50%) scale(1,.012)',opacity:.85},{transform:'translate(-50%,-50%) scale(.01,.012)',opacity:0}],{duration:total*.09,fill:'forwards'});
  await sleep(total*.09);if(finished)return;event('POWER_OFF');audio.play('POWER_OFF');audio.speak('POWER_OFF');await point.animate([{opacity:1,transform:'translate(-50%,-50%) scale(1)'},{opacity:0,transform:'translate(-50%,-50%) scale(.1)'}],{duration:total*.13,fill:'forwards'}).finished;await sleep(total*.04);if(!finished)finish();
}
async function handoff(){
  if(finished||handoffInProgress)return;handoffInProgress=true;setPhase('INTERFACE_HANDOFF');
  const compressMs=scale(settings.handoffCompressMs),fadeMs=scale(settings.fadeMs);
  event('HANDOFF_BEGIN',{compressMs,fadeMs});
  // Keep the last actual terminal pixels as the moving surface. All outer
  // backgrounds become transparent, so this cannot become a shrinking black mask.
  document.documentElement.classList.add('handoff');host.style.background='transparent';stage.classList.add('handoff');
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));if(finished)return;
  const compress=composition.animate([{transform:'scaleX(1)'},{transform:'scaleX(0)'}],{duration:compressMs,easing:'cubic-bezier(.4,0,.2,1)',fill:'forwards'});
  const fade=composition.animate([{opacity:1},{opacity:0}],{duration:fadeMs,easing:'ease-in',fill:'forwards'});
  await Promise.all([compress.finished,fade.finished]);if(!finished)finish(true);
}
function finish(handoffComplete=false){if(finished)return;finished=true;active=false;mother?.cancel();intro?.dispose();clearTimeout(watchdog);cancelAnimationFrame(frameId);events.push({type:'REQUEST_FADE',detail:{handoffComplete},at:Math.round(performance.now()-started)});post({type:'REQUEST_FADE',handoffComplete});audio?.dispose();if(!window.chrome?.webview){if(handoffComplete)post({type:'ANIMATION_FINISHED'});else host.animate([{opacity:1},{opacity:0}],{duration:scale(settings?.fadeMs||300),fill:'forwards'}).finished.then(()=>post({type:'ANIMATION_FINISHED'}));}}

async function audioTest(){active=true;timers();mother.preview();core?.classList.add('released');setPhase('AUDIO TEST');if(session.audioTest==='voice')await audio.speak('READY');else for(const key of ['CORE_EXPAND','HUD_SCAN','CHECK_OK','PROCESS_DETECTED','TARGET_READY','UNLOCK']){audio.play(key);await sleep(450);}await sleep(400);finish();}
async function init(value){
  if(session)return;session=value;settings={...value.settings,...introSettings(value.settings)};started=performance.now();
  const clockStarted=performance.now(),initialElapsed=Math.max(0,(value.particleElapsedMs||0)+(value.particleClockSentUnixMs?Date.now()-value.particleClockSentUnixMs:0));
  const introElapsed=()=>initialElapsed+performance.now()-clockStarted;
  host.dataset.scheme=settings.colorProfile||'movie_red';if(mode==='boot')$('#boot').style.opacity='0';
  if(eye){if(value.coreImagePath)eye.src='https://audio.550w.local/core';eye.onerror=()=>{eye.onerror=null;eye.src=new URL('../assets/core.png',import.meta.url);};}
  $('#bootText').firstChild.textContent=settings.systemLabel+' SYSTEM BOOT';paintRows();
  mother=createShow(composition,{mode:'full',cancelled:'CANCELLED',holdForTarget:true,skipIntro:true,multiplier:settings.durationMultiplier,event:authorEvent});
  const themePromise=fetch(new URL('../theme.json',import.meta.url)).then(r=>r.ok?r.json():{}).catch(()=>({}));
  audio=new AudioKit(value,{},post);updateTarget(target);
  const audioReady=themePromise.then(theme=>{audio.theme=theme;return audio.preload();});void audioReady;
  const cssReady=new Promise(resolve=>{if(custom.sheet)resolve();else{custom.onload=resolve;custom.onerror=resolve;}});
  if(mode==='boot'&&!value.audioTest&&!value.preload){
    mother.prepare();intro=createIntro({stage:composition,settings,scale,event:authorEvent,finished:()=>finished,elapsed:introElapsed});
    // Preparation runs while the native adapter keeps the exact same intro clock moving.
    // Neither audio, font warmup nor visual-commit adds a new idle/hold interval.
    void document.fonts.ready;
    const warmCover=document.createElement('div');warmCover.style.cssText='position:absolute;inset:0;background:#000;z-index:9998;pointer-events:none';composition.append(warmCover);
    const warmApp=$('#app');warmApp.style.transition='none';warmApp.classList.add('visible');warmApp.style.opacity='.01';
    requestAnimationFrame(()=>requestAnimationFrame(()=>{if(['IDLE','PARTICLE_RESIDUE','LOGO_ASSEMBLING','LOGO_HOLD','LOGO_BURST'].includes(intro.getState().phase))warmApp.style.opacity='0';warmCover.remove();}));
    const running=boot().catch(fatal);
    await Promise.all([intro.firstFrame,cssReady]);
    await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));if(finished)return;
    post({type:'VISUAL_READY'});await running;return;
  }
  await cssReady;if(eye)await Promise.race([eye.decode().catch(()=>{}),sleep(500)]);
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));post({type:'VISUAL_READY'});
  if(value.preload){mother.preview();core?.classList.add('released');paintRows();await Promise.race([audioReady,sleep(1500)]);event('PRELOAD_READY');return;}
  if(value.audioTest)await audioTest();else{mother.preview();paintRows();await shutdown();}
}

function receive(m){try{if(m.type==='visual-committed'){}else if(m.type==='init')init(m.session).catch(fatal);else if(m.type==='diagnostics')diagnostic(m.row);else if(m.type==='target')updateTarget(m.target);else if(m.type==='VOICE_DONE')audio?.voiceDone(m.id,m.url);else if(m.type==='activate')shutdown().catch(fatal);else if(m.type==='cancel')finish();}catch(e){fatal(e);}}
function fatal(e){if(e==='CANCELLED')return;console.error(e);post({type:'FATAL'});finish();}
window.addEventListener('keydown',e=>{if(e.key==='Escape'&&settings?.allowEscape&&!session?.preload){event('USER_SKIP');finish();}});host.addEventListener('click',()=>{if(settings?.allowClickSkip&&!session?.preload){event('USER_SKIP');finish();}});window.addEventListener('error',e=>fatal(e.error||e.message));window.addEventListener('unhandledrejection',e=>fatal(e.reason));window.addEventListener('pagehide',()=>{mother?.cancel();intro?.dispose();audio?.dispose();clearTimeout(watchdog);cancelAnimationFrame(frameId);},{once:true});
window.chrome?.webview?.addEventListener('message',e=>receive(e.data));window.Controller550W={receive,getState:()=>({phase,finished,choreographyComplete,intro:intro?.getState(),handoffInProgress,target,rows:[...rows.values()],events,modules,audioEvents:audio?.events||[],elapsed:performance.now()-started,canvas:{width:1920,height:1080,scale:Math.min(innerWidth/1920,innerHeight/1080)},nodes:$('#nodeGrid')?.childElementCount,slots:$('#teleBody').querySelectorAll('.dt').length})};post({type:'PAGE_READY'});
if(!window.chrome?.webview){const p=new URLSearchParams(location.search);if(!p.has('controlled')){const demo={kind:mode,displayName:'DEMO AI',demo:true,minimumBootTimeMs:4000,preload:p.has('preload'),settings:{durationMultiplier:Number(p.get('speed')||1),colorProfile:p.get('color')||'movie_red',systemLabel:'550W',centerRevealMs:900,readyMs:600,shutdownMs:2600,fadeMs:300,bootWatchdogMs:25000,shutdownWatchdogMs:5000,waitForReady:true,allowEscape:true,allowClickSkip:false},audio:{soundEnabled:true,soundVolume:15,voiceEnabled:false,voiceVolume:60,muted:false,bootVoice:true,shutdownVoice:true}};receive({type:'init',session:demo});setTimeout(()=>{for(const [key,label]of slots.slice(0,7))diagnostic({key,label,value:key==='cpu'?'21% · DEMO':key==='memory'?'15 / 32 GB · DEMO':key==='external'?'112 ms · 3/3 · DEMO':'ONLINE · DEMO',status:'online'});updateTarget({processDetected:true,windowDetected:true,ready:false});},300);setTimeout(()=>updateTarget({processDetected:true,windowDetected:true,ready:true}),Number(p.get('readyAfter')||1400));}}
