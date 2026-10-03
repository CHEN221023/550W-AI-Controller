import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { runInNewContext } from 'node:vm';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const upstream = resolve(root, 'third_party/dsh-550c-boot');
const assets = runInNewContext(readFileSync(resolve(upstream, 'src/assets.js'), 'utf8') + ';({CSS_550C,BOOT_MARKUP,APP_MARKUP});');
let enhance = runInNewContext(readFileSync(resolve(upstream, 'src/enhance.js'), 'utf8') + ';ENHANCE_CSS;');
enhance = enhance.slice(0, enhance.indexOf('/* ── 桌面窗口的原生按钮'));
mkdirSync(resolve(root, 'Themes/550W/js'), {recursive:true});
writeFileSync(resolve(root, 'Themes/550W/js/ported-assets.js'), Object.entries({...assets, ENHANCE_CSS: enhance}).map(([key,value])=>`export const ${key} = ${JSON.stringify(value)};`).join('\n')+'\n');
// The original 1920x1080 canvas is scaled uniformly, never reflowed.
const baseCss=assets.CSS_550C.replace(/([\d.]+)vw/g,(_,n)=>(Number(n)*19.2)+'px').replace(/([\d.]+)vh/g,(_,n)=>(Number(n)*10.8)+'px');
writeFileSync(resolve(root,'Themes/550W/js/mother-assets.js'),Object.entries({...assets,CSS_550C:baseCss}).map(([k,v])=>`export const ${k} = ${JSON.stringify(v)};`).join('\n')+'\n');
let show=readFileSync(resolve(upstream,'src/show.js'),'utf8');
show=show.replace('function createShow(stage, options) {','export function createShow(stage, options) {\n  const innerWidth=1920, innerHeight=1080;\n  const timers=new Set();\n  const setInterval=(fn,ms)=>{const id=window.setInterval(fn,ms);timers.add(id);return id;};\n  const clearInterval=id=>{timers.delete(id);window.clearInterval(id);};');
show=show.replace('setTimeout(() => {','setTimeout(() => {');
// Times remain the author's times at Standard. Presets only apply an explicit multiplier.
show=show.replaceAll('}, ms);','}, ms*(options.multiplier||1));');
show=show.replace('    cancelled = true;','    cancelled = true;\n    for(const timer of timers)window.clearInterval(timer);timers.clear();');
show=show.replace('    const totalMs = playBoot();','    options.event?.("LOGO_BEGIN");\n    const totalMs = playBoot();');
show=show.replace('    launched = true;','    launched = true;\n    options.event?.("HUD_VISIBLE");');
show=show.replace('    zCounter++;','    zCounter++;\n    options.event?.("MAJOR_MODULE",opt.name);');
show=show.replace(/function startFps\(\)\{[^\n]*\}/,'function startFps(){} // Frame rate is measured by the outer controller, never randomized.');
show=show.replace('  $("#final").classList.add("show");','  options.event?.("CHOREOGRAPHY_COMPLETE");\n  if(options.holdForTarget)return;\n  $("#final").classList.add("show");');
for(const phase of ['Boot','Auth','Link','Tunnel','Isolate','Rewrite','Verify','Arm','Finish'])show=show.replace(`async function phase${phase}(){`,`async function phase${phase}(){\n  options.event?.("AUTHOR_PHASE","${phase.toUpperCase()}");`);
show=show.replace('  return { start: start, cancel: cancel };',`  function preview(){
    const boot=$("#boot");if(boot)boot.remove();$("#app").classList.add("visible");buildNodes();
    for(let i=0;i<NODE_COUNT;i++)setIcon(i,"done","ONLINE","var(--green)");
    for(let i=0;i<65;i++)pushCodeLine();
    for(const t of ["[550C] 集群状态稳定。","[550C] 所有节点进入安全锁定状态。","[550C] 基站日志写入完成。","AI SESSION TERMINATED","INTERFACE OFFLINE","LINK MONITOR CLOSED","CORE STANDBY"])mainLine(t,"sys");
    setProgress(100,"47 / 47");
  }
  return { start: start, cancel: cancel, preview: preview };`);
show=show.replace('    booting = true;\n',"    booting = true;\n    // The V3 controller supplies its own core/particle/550W intro. The original\n    // multiwindow choreography below remains the sole source of HUD behavior.\n    if (options.skipIntro && mode === 'full') {\n      $(\"#boot\")?.remove();\n      $(\"#app\")?.classList.add(\"visible\");\n      launched = true;\n      options.event?.(\"HUD_VISIBLE\");\n      await run();\n      return;\n    }\n");
show=show.replace('startClock(); startFps(); startLink(); startCode(45); buildNodes();','startClock(); startFps(); startLink(); startCode(45); prepare();').replace('  return { start: start, cancel: cancel, preview: preview };','  let prepared=false;function prepare(){if(prepared)return;prepared=true;buildNodes();}\n  return { start: start, cancel: cancel, preview: preview, prepare: prepare };').replace('$("#boot")?.remove();','$("#boot")?.style.setProperty("display","none");');
writeFileSync(resolve(root,'Themes/550W/js/mother-show.js'),show);
console.log('Preserved original DOM/CSS/SVG and all nine choreography phases; added event hooks and uniform canvas.');
