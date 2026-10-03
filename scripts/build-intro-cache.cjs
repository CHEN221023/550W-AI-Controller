// Build cache of the existing upstream SVG, never a screen capture or a redesigned logo.
// Run with Node and Playwright installed; EDGE_PATH may select the local Chromium executable.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {pathToFileURL}=require('url'),{chromium}=require('playwright');
const root=path.resolve(__dirname,'..'),assets=path.join(root,'Themes/550W/assets');
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
(async()=>{
 const {BOOT_MARKUP}=await import(pathToFileURL(path.join(root,'Themes/550W/js/mother-assets.js')).href);
 const browser=await chromium.launch({headless:true,executablePath:process.env.EDGE_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 try{
  const page=await browser.newPage();
  const result=await page.evaluate(async markup=>{
   const template=document.createElement('template');template.innerHTML=markup;
   const svg=template.content.querySelector('svg').cloneNode(true);
   svg.querySelectorAll('[id]').forEach(node=>{node.id='intro-'+node.id;});
   svg.querySelector('[filter]').setAttribute('filter','url(#intro-despike)');
   svg.querySelectorAll('path').forEach(p=>p.setAttribute('fill',p.classList.contains('red')?'#ff2d2d':'#ffffff'));
   const source=new XMLSerializer().serializeToString(svg),image=new Image();
   image.src='data:image/svg+xml;charset=utf-8,'+encodeURIComponent(source);await image.decode();
   const canvas=document.createElement('canvas');canvas.width=800;canvas.height=230;
   const ctx=canvas.getContext('2d',{willReadFrequently:true});ctx.drawImage(image,0,0,800,230);
   const pixels=ctx.getImageData(0,0,800,230).data,targets=[];
   for(let y=0;y<230;y+=3)for(let x=0;x<800;x+=3){const n=(y*800+x)*4;if(pixels[n+3]>80)targets.push({x:x+560,y:y+425,red:pixels[n]>pixels[n+1]*1.5});}
   return{source,png:canvas.toDataURL('image/png').split(',')[1],targets};
  },BOOT_MARKUP);
  if(result.targets.length<100)throw new Error('Original logo cache has too few sampled targets');
  const png=Buffer.from(result.png,'base64'),json=JSON.stringify(result.targets);
  fs.mkdirSync(assets,{recursive:true});
  fs.writeFileSync(path.join(assets,'intro-logo.png'),png);
  fs.writeFileSync(path.join(assets,'intro-logo-targets.json'),json+'\n');
  const meta={source:'../js/mother-assets.js:BOOT_MARKUP',sourceMarkupSha256:hash(BOOT_MARKUP),renderedSvgSha256:hash(result.source),pngSha256:hash(png),targetsSha256:hash(json+'\n'),width:800,height:230,targetStep:3,targetCount:result.targets.length,coordinates:'logical 1920x1080; SVG offset x=560,y=425',renderer:await browser.version()};
  fs.writeFileSync(path.join(assets,'intro-logo-cache.json'),JSON.stringify(meta,null,2)+'\n');
  console.log(JSON.stringify(meta));
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
