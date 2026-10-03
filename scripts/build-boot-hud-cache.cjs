// Native bridge for a cold WebView: cache the existing HUD's initial frame.
// No new layout, timing, or runtime preload path. Rebuild only when mother assets change.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {pathToFileURL}=require('url'),{chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),theme=path.join(root,'Themes/550W');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
(async()=>{
 const {CSS_550C,APP_MARKUP,BOOT_MARKUP}=await import(pathToFileURL(path.join(theme,'js/mother-assets.js')).href);
 const {createShow}=await import(pathToFileURL(path.join(theme,'js/mother-show.js')).href);
 const custom=fs.readFileSync(path.join(theme,'css/controller.css'),'utf8');
 const browser=await chromium.launch({headless:true,executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const images={};
 try{
  const page=await browser.newPage({viewport:{width:1920,height:1080},deviceScaleFactor:1});
  for(const scheme of ['movie_red','amber','green','cyan','white']){
   await page.setContent('<!doctype html><html style="background:#000"><body style="margin:0;background:#000"></body></html>');
   await page.evaluate(async data=>{
    const host=document.createElement('div');host.dataset.scheme=data.scheme;document.body.append(host);
    const shadow=host.attachShadow({mode:'open'}),style=document.createElement('style');style.textContent=data.css+'\n'+data.custom;shadow.append(style);
    const stage=document.createElement('div');stage.className='dsh550c-stage';stage.innerHTML='<div class="composition-layer">'+data.boot+data.app+'</div>';shadow.append(stage);
    const composition=stage.firstElementChild;composition.querySelector('#boot').style.display='none';
    const show=(0,eval)('('+data.show+')')(composition,{mode:'full',skipIntro:true});show.prepare();
    const slots=[['cpu','CPU CORE'],['memory','MEMORY'],['gpu','GPU'],['vram','VRAM TOTAL'],['dns','DNS'],['external','EXTERNAL ROUTE'],['vpn','VPN ADAPTER'],['target','TARGET AI']];
    const rows=composition.querySelector('#teleBody').querySelectorAll('.dt');
    slots.forEach(([key,label],i)=>{const e=rows[i];e.dataset.real='true';e.querySelector('.k').textContent=label;const v=e.querySelector('.v');v.textContent=key==='target'?'WAITING FOR PROCESS':'PENDING';v.className='v waiting';});
    const app=composition.querySelector('#app');app.style.transition='none';app.classList.add('visible');
    await document.fonts.ready;await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
    // Capture one deterministic first frame, preserving all element-specific animations in runtime source.
    shadow.getAnimations().forEach(a=>{a.pause();a.currentTime=0;});
   },{scheme,css:CSS_550C,custom,boot:BOOT_MARKUP,app:APP_MARKUP,show:createShow.toString()});
   const name='boot-hud-'+scheme+'.png',file=path.join(theme,'assets',name);
   await page.screenshot({path:file});images[name]=hash(fs.readFileSync(file));
  }
 }finally{await browser.close();}
 const meta={source:'Existing APP_MARKUP, CSS_550C, createShow.prepare and controller.css; initial HUD only',width:1920,height:1080,
  motherAssetsSha256:hash(fs.readFileSync(path.join(theme,'js/mother-assets.js'))),motherShowSha256:hash(fs.readFileSync(path.join(theme,'js/mother-show.js'))),controllerCssSha256:hash(Buffer.from(custom)),images};
 fs.writeFileSync(path.join(theme,'assets/boot-hud-cache.json'),JSON.stringify(meta,null,2)+'\n');console.log(JSON.stringify(meta,null,2));
})().catch(e=>{console.error(e);process.exitCode=1;});
