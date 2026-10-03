import { CSS_550C, BOOT_MARKUP, APP_MARKUP, ENHANCE_CSS } from './ported-assets.js';
import { AudioKit } from './audio.js';

const mode=document.body.dataset.mode;
const post=message=>{window.chrome?.webview?.postMessage(message);window.dispatchEvent(new CustomEvent('550w-event',{detail:message}));};
let session,settings,audio,started=0,finished=false,hudAt=0,clockTimer,frameId;
let target={name:'AI',processDetected:false,windowDetected:false,ready:false,state:'initializing'};
const rows=new Map(),seen=new Set(),events=[];
const pendingDiagnostics=[];
const host=document.createElement('div');host.className='controller-host';host.style.visibility='hidden';
document.querySelector('#screen').appendChild(host);const shadow=host.attachShadow({mode:'open'});
const style=document.createElement('style');style.textContent=CSS_550C+ENHANCE_CSS;shadow.appendChild(style);
const custom=document.createElement('link');custom.rel='stylesheet';custom.href=new URL('../css/debug.css',import.meta.url);shadow.appendChild(custom);
const stage=document.createElement('div');stage.className='dsh550c-stage';stage.innerHTML=BOOT_MARKUP+APP_MARKUP;shadow.appendChild(stage);
const $=selector=>stage.querySelector(selector);
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,Math.max(0,ms)));
const scale=ms=>Math.round(ms*(settings?.durationMultiplier||1));
const traceEvent=type=>{events.push({type,at:Math.round(performance.now()-started)});post({type});};
const text=(selector,value)=>{const e=$(selector);if(e)e.textContent=value;};
const make=(tag,cls,value)=>{const e=document.createElement(tag);if(cls)e.className=cls;if(value!==undefined)e.textContent=value;return e;};
function line(message,cls='sys') {
  const e=make('div','ln '+cls,message);e.dataset.ts=new Date().toLocaleTimeString('en-GB',{hour12:false});$('#terminal').appendChild(e);
  while($('#terminal').childElementCount>100)$('#terminal').firstChild.remove();$('#terminal').scrollTop=$('#terminal').scrollHeight;
}
function trace(row){const e=make('div','trace');e.append(make('strong','',row.label),make('div','',row.value));if(row.detail)e.append(make('span','detail',row.detail));$('#codeBody').prepend(e);while($('#codeBody').childElementCount>35)$('#codeBody').lastChild.remove();}
const definitions=[['cpu','CPU CORE'],['memory','MEMORY ARRAY'],['gpu','GRAPHICS PROCESSOR'],['vram','DEDICATED VRAM'],['local','LOCAL NETWORK'],['dns','DNS RESOLUTION'],['domestic','DOMESTIC ROUTE'],['external','EXTERNAL ROUTE'],['vpn','VPN ADAPTER'],['process','AI PROCESS'],['window','AI INTERFACE']];
function setup() {
  const bootText=$('#bootText');bootText.replaceChildren(document.createTextNode(settings.systemLabel+' SYSTEM BOOT'),make('span','cursor'));
  if(settings.systemLabel==='550W')$('#cee').innerHTML='<path class="white" d="M558 35h24l15 128 24-111h24l25 111 15-128h24l-24 178h-28l-24-108-23 108h-28z"/>';
  else if(settings.systemLabel!=='550C'){$('#logo').replaceChildren();const svg=$('#boot svg');const label=document.createElementNS('http://www.w3.org/2000/svg','text');label.setAttribute('x','400');label.setAttribute('y','168');label.setAttribute('text-anchor','middle');label.setAttribute('font-size','130');label.setAttribute('font-family','Consolas,monospace');label.setAttribute('fill','white');label.textContent=settings.systemLabel;svg.append(label);}
  $('#hud-top').replaceChildren(make('span','brand','◢ '+settings.systemLabel+' // AI CONTROLLER'),make('span','sep','│'),make('span','item',session.demo?'DEMO / 模拟自检':'SYSTEM INITIALIZATION'),make('span','spacer'),make('span','item'));
  const clock=make('b','','00:00:00');clock.id='h-time';$('#hud-top').lastChild.append('TIME ',clock);
  $('#teleBody').replaceChildren();
  for(let i=0;i<definitions.length;i++){
    const [key,label]=definitions[i];if([0,4,9].includes(i))$('#teleBody').append(make('div','sect',i===0?'COMPUTE ARRAY':i===4?'NETWORK ROUTES':'TARGET INTERFACE'));
    const e=make('div','dt');e.dataset.key=key;e.append(make('span','k',label),make('span','v waiting','PENDING'));$('#teleBody').append(e);
  }
  $('#w-tele .w-head').replaceChildren(make('span','led'),'SYSTEM DIAGNOSTICS');
  $('#w-main .w-head').replaceChildren(make('span','led'),settings.systemLabel+' CORE TERMINAL');
  $('#w-code .w-head').replaceChildren(make('span','led'),'LIVE STATUS FEED');
  $('#w-node .w-head').replaceChildren(make('span','led'),'MODULE MATRIX');
  $('#mainBody').replaceChildren();const identity=make('div','identity');identity.append(make('div','eyebrow','ARTIFICIAL INTELLIGENCE WORKSTATION'),make('div','system-label',settings.systemLabel),make('div','subtitle',mode==='boot'?'SYSTEM INITIALIZATION':'SHUTDOWN SEQUENCE'));
  const block=make('div','target-block');const name=make('div','target-name',session.displayName);name.id='target-name';const state=make('div','target-state waiting','INITIALIZING');state.id='target-state';block.append(name,state);
  const terminal=make('div');terminal.id='terminal';$('#mainBody').append(identity,block,terminal);
  $('#codeBody').replaceChildren();$('#nodeGrid').replaceChildren();
  for(const [key,label]of definitions.filter(x=>!['vram','domestic'].includes(x[0]))){const e=make('div','module');e.dataset.module=key;e.append(make('span','mark','◈'),make('span','',key.toUpperCase()),make('span','status','PENDING'));$('#nodeGrid').append(e);}
  const scanner=make('div','scanner');$('#w-main').append(scanner);
  const wait=make('div','wait-strip');wait.append('INTERFACE ',make('b','','INITIALIZING'));wait.id='wait-strip';$('#w-main').append(wait);
  $('#hud-bot').replaceChildren(make('span','item','STAGE '),make('span','item'),make('span','spacer'),make('div','prog'),make('span','pct','0%'),make('span','spacer'),make('span','item'));
  const phase=make('b','','CORE');phase.id='b-stage';$('#hud-bot').firstChild.append(phase);
  const elapsed=make('b','','0.0s');elapsed.id='b-elapsed';$('#hud-bot').children[1].append('ELAPSED ',elapsed);
  const fill=make('div','fill');fill.id='b-fill';$('#hud-bot .prog').append(fill);$('#hud-bot .pct').id='b-pct';
  const fps=make('b','','—');fps.id='b-fps';$('#hud-bot').lastChild.append('FPS ',fps);
  $('#final').replaceChildren(make('span','final-label',settings.systemLabel),document.createTextNode('SYSTEM READY'),make('span','final-app',session.displayName+' INTERFACE INITIALIZED'));
  const hint=make('div','skip-hint',settings.allowEscape?'ESC / 跳过':'');if(settings.allowClickSkip)hint.append(' · CLICK / 跳过');stage.append(hint);
  clockTimer=setInterval(()=>{text('#h-time',new Date().toLocaleTimeString('en-GB',{hour12:false}));text('#b-elapsed',((performance.now()-started)/1000).toFixed(1)+'s');},1000);
  let frames=0,last=performance.now();const frame=now=>{if(finished)return;frames++;if(now-last>=1000){text('#b-fps',Math.round(frames*1000/(now-last)));last=now;frames=0;}frameId=requestAnimationFrame(frame);};frameId=requestAnimationFrame(frame);
}
function diagnostic(row){
  rows.set(row.key,row);const e=$(`[data-key="${row.key}"]`);if(e){e.querySelector('.k').textContent=row.label;const value=e.querySelector('.v');value.textContent=row.value;value.className='v '+row.status;}
  const module=$(`[data-module="${row.key}"]`);const good=['online','stable','ready','detected'].includes(row.status);if(module){module.classList.toggle('ready',good);module.querySelector('.status').textContent=row.status.toUpperCase();}
  if(!seen.has(row.key+':'+row.status)){seen.add(row.key+':'+row.status);trace(row);if(!['process','window'].includes(row.key)){line(row.label+' .... '+row.value,good?'ok':'inf');if(good)audio?.play('CHECK_OK','check:'+row.key,.35);}}
  if(row.key==='external'&&good)audio?.speak('NETWORK');
}
function updateTarget(value){
  target=value;diagnostic({key:'process',label:session?.displayName.toUpperCase()+' PROCESS',value:value.processDetected?'DETECTED':'WAITING',status:value.processDetected?'detected':'waiting'});
  diagnostic({key:'window',label:'AI INTERFACE',value:value.ready?'RESPONSIVE / READY':value.windowDetected?'INITIALIZING':'WAITING FOR WINDOW',status:value.ready?'ready':'waiting'});
  if(value.processDetected&&hudAt>0){audio?.play('PROCESS_DETECTED');audio?.speak('PROCESS');}
}
function phase(value){text('#b-stage',value);text('#target-state',value==='WAITING'?'INTERFACE INITIALIZING · WAITING':value==='READY'?'INTERFACE READY':value);$('#target-state').classList.toggle('waiting',value==='WAITING');text('#wait-strip b',value==='READY'?'READY':value);}
async function intro(){
  phase('INITIALIZING');audio.speak('BOOT');
  host.style.clipPath='circle(3px at 50% 50%)';host.style.visibility='visible';post({type:'VISUAL_READY'});
  const energy=make('div','core-energy');stage.append(energy);
  audio.play('CORE_EXPAND');const expansion=host.animate([{clipPath:'circle(3px at 50% 50%)'},{clipPath:'circle(75vmax at 50% 50%)'}],{duration:scale(settings.centerRevealMs),easing:'cubic-bezier(.18,.68,.12,1)',fill:'forwards'});
  const paths=Array.from($('#logo').querySelectorAll('path')).sort((a,b)=>a.getBBox().x-b.getBBox().x);
  for(let i=0;i<paths.length;i++)paths[i].animate([{opacity:.06,clipPath:'inset(0 100% 0 0)'},{opacity:1,clipPath:'inset(0 0 0 0)'}],{duration:scale(470),delay:scale(60+i*65),fill:'forwards',easing:'cubic-bezier(.3,0,.2,1)'});
  energy.animate([{transform:'scale(.1)',opacity:1},{transform:'scale(4)',opacity:0}],{duration:scale(settings.centerRevealMs),fill:'forwards'});
  $('#bootText').classList.add('show');await expansion.finished;if(finished)return;
  host.style.clipPath='none';expansion.cancel();energy.remove();await sleep(Math.max(0,scale(1050-settings.centerRevealMs)));if(finished)return;
  $('#boot').classList.add('fade');$('#app').classList.add('visible');audio.play('HUD_SCAN');hudAt=performance.now();
  await sleep(scale(230));$('#boot').remove();phase('DIAGNOSTICS');traceEvent('BOOT_INTRO_FINISHED');
  for(const row of pendingDiagnostics.splice(0))diagnostic(row);updateTarget(target);
}
async function boot(){
  await intro();if(finished)return;line('PROCESS MONITOR .... ONLINE');line('检测窗口可见性、响应状态与稳定时间。','inf');
  let waiting=false;
  while(!finished){
    const elapsed=performance.now()-started;
    const minimum=Math.max(scale(session.minimumBootTimeMs),hudAt-started+scale(settings.diagnosticsMinimumMs));
    const p=Math.min(94,elapsed/minimum*90);$('#b-fill').style.width=p+'%';text('#b-pct',Math.round(p)+'%');
    if(elapsed>=minimum&&(!settings.waitForReady||target.ready))break;
    if(elapsed>=minimum&&!waiting){waiting=true;phase('WAITING');traceEvent('WAITING_LOOP_READY');line('AI WINDOW .... WAITING FOR RESPONSIVE INTERFACE','warn');}
    await sleep(120);
  }
  if(finished)return;
  phase('READY');$('#b-fill').style.width='100%';text('#b-pct','100%');line(settings.waitForReady?'AI INTERFACE .... READY':'最短动画完成，等待窗口选项已关闭。','ok');
  if(!settings.waitForReady&&!target.ready){$('#final').replaceChildren(make('span','final-label',settings.systemLabel),document.createTextNode('HANDOFF'),make('span','final-app',session.displayName+' · WINDOW MAY STILL BE LOADING'));}
  $('#final').classList.add('show');$('#app').classList.add('ready');traceEvent('TARGET_READY');audio.play('TARGET_READY');
  await Promise.all([sleep(scale(settings.readyMs)),target.ready?audio.speak('READY'):Promise.resolve()]);
  if(finished)return;audio.play('UNLOCK');traceEvent('INTERFACE_HANDOFF');finish();
}
async function shutdown(){
  $('#boot').remove();host.style.visibility='visible';post({type:'VISUAL_READY'});$('#app').classList.add('visible');
  host.animate([{opacity:0},{opacity:1}],{duration:110,fill:'forwards'});phase('SHUTDOWN');traceEvent('SHUTDOWN_BEGIN');audio.play('SHUTDOWN_BEGIN');audio.speak('SHUTDOWN');
  const total=scale(settings.shutdownMs), beats=[['AI INTERFACE','OFFLINE'],['TARGET MONITOR','DETACHED'],['NETWORK MONITOR','RELEASED'],['COMPUTE MODULES','STANDBY'],['PROCESS MONITOR','STANDBY'],[settings.systemLabel+' CORE','SHUTTING DOWN']];
  const core=make('div','shutdown-core');core.append(make('strong','',settings.systemLabel),make('span','','CORE ENTERING STANDBY'));stage.append(core);
  for(let i=0;i<beats.length;i++){
    if(finished)return;const[label,value]=beats[i];line(label+' .... '+value,i===0?'warn':'sys');trace({label,value,status:'offline'});audio.play('RELAY_OFF','relay:'+i,.45);
    if(i===2)audio.speak('NETWORK_CLOSED');if(i===4)audio.speak('STANDBY');
    $('#b-fill').style.width=(1-(i+1)/beats.length)*100+'%';text('#b-pct',Math.round((1-(i+1)/beats.length)*100)+'%');
    await sleep(total*.085);
  }
  core.classList.add('show');$('#app').classList.add('closing');traceEvent('HUD_OFFLINE');await sleep(total*.15);if(finished)return;
  core.animate([{opacity:1,transform:'translate(-50%,-50%) scale(1)'},{opacity:0,transform:'translate(-50%,-50%) scale(.96)'}],{duration:total*.12,fill:'forwards'});
  const crt=make('div','crt-line');stage.append(crt);audio.play('CRT_COLLAPSE');traceEvent('CRT_COLLAPSE');
  await crt.animate([{opacity:.1,transform:'scaleY(1) scaleX(1)'},{opacity:1,transform:'scaleY(.0025) scaleX(.78)'}],{duration:total*.19,easing:'cubic-bezier(.65,0,.2,1)',fill:'forwards'}).finished;if(finished)return;
  const point=make('div','power-point');stage.append(point);crt.animate([{opacity:1,transform:'scaleY(.0025) scaleX(.78)'},{opacity:0,transform:'scaleY(.0025) scaleX(.002)'}],{duration:total*.09,fill:'forwards'});
  point.animate([{opacity:1,transform:'scale(1.2)'},{opacity:0,transform:'scale(.1)'}],{duration:total*.12,fill:'forwards'});audio.play('POWER_OFF');traceEvent('POWER_OFF');
  await Promise.all([sleep(total*.12),audio.speak('POWER_OFF')]);if(!finished)finish();
}
function finish(){if(finished)return;finished=true;clearInterval(clockTimer);cancelAnimationFrame(frameId);traceEvent('REQUEST_FADE');if(!window.chrome?.webview){host.animate([{opacity:1},{opacity:0}],{duration:scale(settings.fadeMs),fill:'forwards'}).finished.then(()=>{audio.dispose();post({type:'ANIMATION_FINISHED'});});}else setTimeout(()=>audio.dispose(),scale(settings.fadeMs)+50);}
async function audioTest(){ $('#boot').remove();host.style.visibility='visible';$('#app').classList.add('visible');post({type:'VISUAL_READY'});phase('AUDIO TEST');line('当前设置声音预览。','ok');if(session.audioTest==='voice')await audio.speak('READY');else{for(const key of ['CORE_EXPAND','HUD_SCAN','CHECK_OK','PROCESS_DETECTED','TARGET_READY','UNLOCK']){audio.play(key);await sleep(450);}}await sleep(400);finish(); }
async function init(value){
  if(session)return;session=value;settings=value.settings;started=performance.now();if(settings.colorProfile!=='amber')host.dataset.scheme=settings.colorProfile;setup();
  const theme=await fetch(new URL('../theme.json',import.meta.url)).then(r=>r.json());audio=new AudioKit(value,theme,post);updateTarget(target);
  const watchdog=setTimeout(()=>{if(!finished){traceEvent('WATCHDOG');finish();}},mode==='boot'?settings.bootWatchdogMs:settings.shutdownWatchdogMs);
  window.addEventListener('pagehide',()=>{clearTimeout(watchdog);audio.dispose();clearInterval(clockTimer);cancelAnimationFrame(frameId);},{once:true});
  window.addEventListener('keydown',e=>{if(e.key==='Escape'&&settings.allowEscape){traceEvent('USER_SKIP');finish();}});host.addEventListener('click',()=>{if(settings.allowClickSkip){traceEvent('USER_SKIP');finish();}});
  if(value.audioTest)await audioTest();else if(mode==='boot')await boot();else await shutdown();
}
function receive(message){try{if(message.type==='init')init(message.session).catch(fatal);else if(message.type==='diagnostics'){if(audio&&hudAt>0)diagnostic(message.row);else pendingDiagnostics.push(message.row);}else if(message.type==='target'){target=message.target;if(session)updateTarget(target);}else if(message.type==='VOICE_DONE')audio?.voiceDone(message.id);}catch(e){fatal(e);}}
function fatal(error){console.error(error);post({type:'FATAL'});if(settings)finish();else host.remove();}
window.addEventListener('error',e=>fatal(e.error||e.message));window.addEventListener('unhandledrejection',e=>fatal(e.reason));
window.chrome?.webview?.addEventListener('message',e=>receive(e.data));
window.Controller550W={receive,getState:()=>({phase:$('#b-stage')?.textContent,finished,target,rows:Array.from(rows.values()),events,audioEvents:audio?.events||[],elapsed:performance.now()-started})};
post({type:'PAGE_READY'});
if(!window.chrome?.webview){
 const params=new URLSearchParams(location.search);const delay=Number(params.get('readyAfter')||1400);
 const demo={kind:mode,displayName:'DEMO AI',demo:true,minimumBootTimeMs:3500,settings:{durationMultiplier:1,colorProfile:params.get('color')||'amber',systemLabel:'550W',centerRevealMs:700,diagnosticsMinimumMs:1400,readyMs:600,shutdownMs:2200,fadeMs:300,bootWatchdogMs:20000,shutdownWatchdogMs:5000,waitForReady:true,allowEscape:true,allowClickSkip:false},audio:{soundEnabled:true,soundVolume:15,voiceEnabled:false,voiceVolume:60,muted:false,bootVoice:true,shutdownVoice:true}};
 receive({type:'init',session:demo});setTimeout(()=>{for(const[key,label]of definitions.slice(0,9))receive({type:'diagnostics',row:{key,label,value:key==='cpu'?'21% · DEMO':key==='memory'?'15.0 / 32.0 GB · DEMO':key==='external'?'STABLE · 112 ms · 3/3 · DEMO':'ONLINE · DEMO',status:key==='external'?'stable':'online'}});receive({type:'target',target:{name:'DEMO AI',processDetected:true,windowDetected:true,ready:false}});},300);
 setTimeout(()=>receive({type:'target',target:{name:'DEMO AI',processDetected:true,windowDetected:true,ready:true}}),delay);
}

