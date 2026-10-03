import {readFileSync,writeFileSync,readdirSync,statSync,mkdirSync} from 'node:fs';
import {resolve,relative,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {runInNewContext} from 'node:vm';
import * as mother from '../Themes/550W/js/mother-assets.js';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..'),upstream=resolve(root,'third_party/dsh-550c-boot');
const original=runInNewContext(readFileSync(resolve(upstream,'src/assets.js'),'utf8')+';({CSS_550C,BOOT_MARKUP,APP_MARKUP});');
const hash=b=>createHash('sha256').update(b).digest('hex');
const uniform=original.CSS_550C.replace(/([\d.]+)vw/g,(_,n)=>(Number(n)*19.2)+'px').replace(/([\d.]+)vh/g,(_,n)=>(Number(n)*10.8)+'px');
const evidence={commit:'bddc507d7c717fe7cda440cec83f330e8b3e5004',bootMarkupExact:mother.BOOT_MARKUP===original.BOOT_MARKUP,appMarkupExact:mother.APP_MARKUP===original.APP_MARKUP,cssExactAfterUniformCanvasConversion:mother.CSS_550C===uniform,originalHtmlSha256:hash(readFileSync(resolve(upstream,'assets/550C-source.html'))),files:[]};
function visit(directory){for(const entry of readdirSync(directory)){const path=resolve(directory,entry);if(entry==='.git'||entry==='node_modules')continue;if(statSync(path).isDirectory())visit(path);else evidence.files.push({path:relative(upstream,path).replaceAll('\\','/'),sha256:hash(readFileSync(path))});}}
visit(upstream);mkdirSync(resolve(root,'docs'),{recursive:true});writeFileSync(resolve(root,'docs/source-preservation.json'),JSON.stringify(evidence,null,2));
if(!evidence.bootMarkupExact||!evidence.appMarkupExact||!evidence.cssExactAfterUniformCanvasConversion)throw Error('Original mother preservation differs.');
console.log(`Exact BOOT/APP markup, uniformly converted CSS, ${evidence.files.length} upstream source files recorded.`);
