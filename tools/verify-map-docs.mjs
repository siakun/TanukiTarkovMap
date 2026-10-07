#!/usr/bin/env node
/**
 * Independent visual oracle: run the collected source's renderer in a disposable
 * headless browser and compare every layer with the generated SVG. No source code
 * enters resources or the application. Dynamic bindings are discovered by semantic
 * signatures, not a minified name or bundle hash; discovery ambiguity fails closed.
 * Usage: --input <collection> --resources <candidate> --report <new-report-directory>
 */
import { readFile, writeFile, mkdir, mkdtemp, access } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { configuredMaps, requireValue, checkResources, sha256 } from './resource-bundle.mjs';
import { parseScreenshot, gamePositionToMapPosition, gameDirectionToMapDirection } from '../viewer/coords.js';

function arrows(source) {
  const result=[];
  for(const match of source.matchAll(/([\w$]+)=(\([^)]*\)|[\w$]+)=>\{/g)) {
    let depth=1,quote='',escaped=false,index=match.index+match[0].length;
    for(;index<source.length&&depth;index++) {
      const char=source[index];
      if(quote){if(!escaped&&char===quote)quote='';escaped=!escaped&&char==='\\';if(char!=='\\')escaped=false;continue;}
      if(['"',"'",'`'].includes(char)){quote=char;continue;}
      if(char==='{')depth++;else if(char==='}')depth--;
    }
    if(!depth)result.push({name:match[1],source:source.slice(match.index,index),start:match.index,end:index});
  }
  return result;
}
export function referenceRenderer(source,symbolSource) {
  const functions=arrows(source);
  const pick=predicate=>{const found=functions.filter(item=>predicate(item.source));requireValue(found.length===1,'Reference renderer signature changed or ambiguous');return found[0];};
  const draw=pick(s=>s.includes('.stairsProfile')&&s.includes('kind==="stamp"')&&s.includes('strokeStyle')&&!s.includes('setup('));
  const composite=pick(s=>s.includes('flattenOpaqueGroups')&&s.includes('.drawImage(')&&s.length<6000);
  const outline=pick(s=>s.includes('.outline.width')&&s.includes('.mapSize')&&s.length<3000);
  const passes=pick(s=>s.includes('level-context-backdrop')&&s.length<3000);
  const contexts=pick(s=>s.includes('level-context:')&&s.length<3000);
  const backdrop=pick(s=>s.includes('role==="floor"')&&s.includes('role==="empty"')&&s.includes('.every(')&&s.length<1500);
  const order=pick(s=>s.includes('.compositeGroups?.[')&&s.includes('.flatMap(')&&s.length<2000);
  const clip=pick(s=>s.includes('.getTransform()')&&s.includes('.clearRect(')&&s.includes('Math.ceil')&&s.length<2000);
  const palette=/([\w$]+)=\{building:\{label:/.exec(source)?.[1];
  const start=/([\w$]+)="#[0-9a-f]+",[\w$]+="#[0-9a-f]+",[\w$]+="#[0-9a-f]+",[\w$]+=1e3,[\w$]+=\{building:/.exec(source);
  const group=/([\w$]+)=[\w$]+=>[\w$]+\.renderGroup\?[\w$]+\.renderGroup:/.exec(source)?.[1];
  const symbolCall=/globalAlpha=[\w$]+,([\w$]+)\([\w$]+,[\w$]+,[\w$]+,[\w$]+,[\w$]+,[\w$]+\),[\w$]+\.restore/.exec(source)?.[1];
  requireValue(start&&palette&&group&&symbolCall,'Missing reference bindings');
  const glyphName=/\.stroke\(new Path2D\(([\w$]+)\)\)/.exec(symbolSource)?.[1];
  const glyph=new RegExp(`${glyphName}=("[^"]+")`).exec(symbolSource)?.[1];
  const radiusName=/\.scale\([\w$]+\/([\w$]+),[\w$]+\/\1\)/.exec(symbolSource)?.[1];
  const radius=new RegExp(`(?:const |,)${radiusName}=([0-9.]+)`).exec(symbolSource)?.[1];
  requireValue(glyph&&radius,'Missing source stop vector');
  const code=source.slice(start.index,outline.end);
  return `const ${symbolCall}=(ctx,x,y,rx,ry=rx,angle=0)=>{ctx.save();ctx.translate(x,y);ctx.rotate(angle);ctx.scale(rx/${radius},ry/${radius});ctx.strokeStyle='#000';ctx.lineWidth=1;ctx.lineJoin='miter';ctx.setLineDash([]);ctx.stroke(new Path2D(${glyph}));ctx.restore();};
    const ${code};const ${backdrop.source};const ${passes.source};const ${contexts.source};
    window.sourceRenderer={draw:${draw.name},composite:${composite.name},outline:${outline.name},passes:${passes.name},contexts:${contexts.name},order:${order.name},clip:${clip.name},roles:${palette},group:${group}};`;
}

class Cdp {
  constructor(url){this.socket=new WebSocket(url);this.pending=new Map();this.serial=0;this.ready=new Promise((resolve,reject)=>{this.socket.onopen=resolve;this.socket.onerror=reject;});this.socket.onmessage=e=>{const reply=JSON.parse(e.data),pending=this.pending.get(reply.id);if(pending){this.pending.delete(reply.id);reply.error?pending.reject(new Error(reply.error.message)):pending.resolve(reply.result);}};}
  async send(method,params={}){await this.ready;const id=++this.serial;return new Promise((resolve,reject)=>{this.pending.set(id,{resolve,reject});this.socket.send(JSON.stringify({id,method,params}));});}
  async evaluate(expression){const result=await this.send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(result.exceptionDetails)throw new Error(result.exceptionDetails.exception?.description||result.exceptionDetails.text);return result.result.value;}
  close(){this.socket.close();}
}
async function browser() {
  let executable;
  for(const candidate of [process.env.CHROME_PATH,'C:/Program Files/Google/Chrome/Application/chrome.exe','C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe']) {
    if(!candidate)continue;try{await access(candidate);executable=candidate;break;}catch{}
  }
  requireValue(executable,'No headless Chromium found');
  const profile=await mkdtemp(path.join(os.tmpdir(),'tanuki-geometry-'));
  const child=spawn(executable,['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--remote-debugging-port=0',`--user-data-dir=${profile}`,'about:blank'],{stdio:'ignore'});
  let port;const deadline=Date.now()+20000;
  while(Date.now()<deadline){try{port=Number((await readFile(path.join(profile,'DevToolsActivePort'),'utf8')).split('\n')[0]);break;}catch{await new Promise(resolve=>setTimeout(resolve,100));}}
  requireValue(port&&port!==9222,'Disposable browser did not start');
  const targets=await(await fetch(`http://127.0.0.1:${port}/json`)).json();const target=targets.find(t=>t.type==='page');
  return {process:child,cdp:new Cdp(target.webSocketDebuggerUrl),profile};
}

async function compareLayer(mapId,level) {
  const [snapshot,meta,svgText]=await Promise.all([fetch(`/source/${mapId}.json`).then(r=>r.json()),fetch(`/resources/maps/${mapId}/meta.json`).then(r=>r.json()),fetch(`/resources/maps/${mapId}/map.svg`).then(r=>r.text())]);
  const doc=snapshot.bundle.doc,r=window.sourceRenderer,size=meta.size;
  for(const [layer,shapes]of Object.entries(doc.layers))shapes.forEach((shape,index)=>shape.__testIdentity=`${layer}:${index}`);
  const scale=Math.min(1,1400/Math.max(size.width,size.height)),width=Math.ceil(size.width*scale),height=Math.ceil(size.height*scale);
  const reference=document.createElement('canvas');reference.width=width;reference.height=height;const ctx=reference.getContext('2d',{willReadFrequently:true});ctx.scale(scale,scale);
  const own=doc.layers[level],contexts=r.contexts(doc.levelContexts??{},level,doc.layers,doc.groupOrder,size,doc.sharedGroups??{});
  const passes=r.passes(own,contexts,doc.groupOrder,size,r.roles[doc.bgFill]?.color);
  if(doc.bgFill){ctx.fillStyle=r.roles[doc.bgFill].color;ctx.fillRect(1000,1000,size.width-2000,size.height-2000);}
  for(const pass of passes)r.composite(ctx,pass,{wallWidths:doc.wallWidths,mapSize:size,flattenOpaqueGroups:true});
  for(const shape of own.filter(s=>r.group(s)==='zone'&&!s.compositeGroups?.length))r.outline(ctx,shape,{mapSize:size});
  // The live MapLayers draw path applies this source helper only to Customs. It
  // clears device pixels outside the transformed padded rectangle. Run that helper
  // unchanged, independently of the converter's map-coordinate SVG clipPath.
  if(mapId==='customs')r.clip(ctx,size);
  const parsed=new DOMParser().parseFromString(svgText,'image/svg+xml');
  if(parsed.querySelector('parsererror'))throw new Error('Generated SVG is not well formed');
  for(const entry of meta.levels)parsed.getElementById(entry.terrainGroupId).setAttribute('display',entry.id===level?'inline':'none');
  const sourceOrder=passes.flat().map(shape=>shape.__testIdentity).filter(Boolean);
  const svgOrder=[...parsed.getElementById(level).querySelectorAll('[data-source-layer]')].map(element=>`${element.getAttribute('data-source-layer')}:${element.getAttribute('data-source-index')}`);
  if(JSON.stringify(sourceOrder)!==JSON.stringify(svgOrder))throw new Error(`${mapId}/${level}: source shape membership or painter order differs`);
  const blob=new Blob([new XMLSerializer().serializeToString(parsed)],{type:'image/svg+xml'}),url=URL.createObjectURL(blob),image=new Image();
  await new Promise((resolve,reject)=>{image.onload=resolve;image.onerror=reject;image.src=url;});
  const actual=document.createElement('canvas');actual.width=width;actual.height=height;const ac=actual.getContext('2d',{willReadFrequently:true});ac.drawImage(image,0,0,size.width*scale,size.height*scale);URL.revokeObjectURL(url);
  const a=ctx.getImageData(0,0,width,height).data,b=ac.getImageData(0,0,width,height).data;
  let occupied=0,changed=0,large=0,total=0,alpha=0,interiorTotal=0,interiorLarge=0,clipBoundary=0;
  for(let index=0;index<a.length;index+=4){if(a[index+3]||b[index+3])occupied++;let difference=0,sum=0;for(let ch=0;ch<4;ch++){const delta=Math.abs(a[index+ch]-b[index+ch]);difference=Math.max(difference,delta);total+=delta;sum+=delta;}if(difference>8)changed++;if(difference>64)large++;alpha+=Math.abs(a[index+3]-b[index+3]);
    const x=(index/4)%width,y=Math.floor(index/4/width),edge=mapId==='customs'&&[Math.abs(x-1000*scale),Math.abs(x-(size.width-1000)*scale),Math.abs(y-1000*scale),Math.abs(y-(size.height-1000)*scale)].some(n=>n<=2);
    if(edge){if(difference>8)clipBoundary++;}else{interiorTotal+=sum;if(difference>64)interiorLarge++;}
  }
  // SVG and Canvas antialias paths differently. Significant filled-area differences
  // remain visible in largeDifference; the paired PNGs preserve reviewable evidence.
  return {mapId,level,width,height,occupied,changed,large,meanError:total/(occupied*4),alphaError:alpha/occupied,
    changedFraction:changed/occupied,largeFraction:large/occupied,interiorMean:interiorTotal/(occupied*4),interiorLargeFraction:interiorLarge/occupied,clipBoundary,shapeOrderMatch:true,
    reference:reference.toDataURL('image/png'),actual:actual.toDataURL('image/png')};
}

export async function verifyDocuments(input,resources,report) {
  const manifest=await checkResources(resources),maps=await configuredMaps();await mkdir(report,{recursive:true});
  const first=JSON.parse(await readFile(path.join(input,`${maps[0]}.json`),'utf8'));
  const renderer=await readFile(path.join(input,first.renderer.file),'utf8'),symbols=await readFile(path.join(input,first.symbols.file),'utf8');
  requireValue(sha256(renderer)===first.renderer.sha256&&sha256(symbols)===first.symbols.sha256,'Reference source hash mismatch');
  const oracle=referenceRenderer(renderer,symbols);
  const server=createServer(async(req,res)=>{try{
    let body,type='text/html';
    if(req.url==='/')body='<html><body><script src="/reference.js"></script></body></html>';
    else if(req.url==='/reference.js'){body=oracle;type='text/javascript';}
    else {const match=/^\/(source|resources)\/([a-zA-Z0-9_./-]+)$/.exec(req.url);if(!match||match[2].split('/').includes('..'))throw new Error('Invalid path');body=await readFile(path.join(match[1]==='source'?input:resources,match[2]));type=match[2].endsWith('.svg')?'image/svg+xml':'application/json';}
    res.writeHead(200,{'Content-Type':type,'Content-Security-Policy':"default-src 'self'; script-src 'self' 'unsafe-eval'; img-src 'self' blob:; connect-src 'self'"});res.end(body);
  }catch(error){res.writeHead(404);res.end(error.message);}});
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const url=`http://127.0.0.1:${server.address().port}/`;
  let chrome;const results=[],coordinateChecks=[];
  try {
    chrome=await browser();
    await chrome.cdp.send('Page.enable');await chrome.cdp.send('Page.navigate',{url});
    for(let i=0;i<100;i++){if(await chrome.cdp.evaluate('!!window.sourceRenderer'))break;await new Promise(r=>setTimeout(r,50));}
    requireValue(await chrome.cdp.evaluate('!!window.sourceRenderer'),'Reference renderer failed to load');
    for(const mapId of maps){const snapshot=JSON.parse(await readFile(path.join(input,`${mapId}.json`),'utf8'));
      requireValue(snapshot.renderer.sha256===first.renderer.sha256&&snapshot.symbols.sha256===first.symbols.sha256,'Mixed renderer or symbol revisions require separate reference sessions');
      const meta=JSON.parse(await readFile(path.join(resources,'maps',mapId,'meta.json'),'utf8')),markers=JSON.parse(await readFile(path.join(resources,'maps',mapId,'markers.json'),'utf8'));
      requireValue(meta.source.documentHash===snapshot.source.hash,`${mapId}: candidate source revision mismatch`);
      requireValue(meta.source.documentSha256===sha256(JSON.stringify(snapshot.bundle))
        && markers.source.markerSha256===sha256(JSON.stringify(snapshot.markers))
        && meta.source.rendererSha256===snapshot.renderer.sha256&&meta.source.settingsSha256===snapshot.entry.sha256,
      `${mapId}: candidate source evidence differs`);
      requireValue(meta.levels.length===snapshot.levels.length&&snapshot.levels.every(level=>meta.levels.some(l=>l.id===level.id&&l.sourceLevel===level.sourceLevel&&l.defaultVisible===level.defaultVisible)),`${mapId}: source levels differ`);
      requireValue(markers.markers.length===snapshot.markers.length,`${mapId}: marker count differs`);
      requireValue(snapshot.coordinateChecks?.length>=4&&snapshot.directionObservations?.checks?.length>=7,`${mapId}: missing live coordinate/direction observations`);
      for(const check of snapshot.coordinateChecks)requireValue(JSON.stringify(gamePositionToMapPosition(check.game.x,check.game.y,meta.transform))===JSON.stringify(check.map),`${mapId}: viewer coordinate transform differs from live source`);
      for(const check of snapshot.directionObservations.checks){const actual=parseScreenshot(check.filename),expected=check.result;
        requireValue(actual.x===expected.x&&actual.y===expected.y&&actual.z===expected.z&&Math.abs(actual.look-expected.look)<1e-9,`${mapId}: viewer screenshot parser differs from live source`);
        const expectedAngle=((expected.look+270-meta.transform.rotate)%360+360)%360;
        requireValue(Math.abs(gameDirectionToMapDirection(actual.look,meta.transform)-expectedAngle)<1e-9,`${mapId}: viewer screen direction differs from source formula`);}
      coordinateChecks.push({mapId,positions:snapshot.coordinateChecks.length,directions:snapshot.directionObservations.checks.length,sourceParser:snapshot.directionObservations.parserSource,matched:true});
      const expectedFactions={'Transition':['pmc'],'PMC Extraction':['pmc'],'Scav Extraction':['scav'],'Co-Op Extraction':['pmc','scav']};
      for(const source of snapshot.markers){const actual=markers.markers.find(marker=>marker.id===source.id),type=markers.categories.flatMap(category=>category.subtypes).find(subtype=>subtype.id===actual?.subtype);
        requireValue(actual&&actual.name===source.name&&JSON.stringify(actual.position)===JSON.stringify(source.position)&&meta.levels.find(level=>level.id===actual.levelId)?.sourceLevel===source.sourceLevel
          &&type?.sourceSubtype===source.sourceSubtype&&JSON.stringify(type.factions)===JSON.stringify(expectedFactions[source.sourceSubtype]),`${mapId}: source marker UID/name/faction/level/coordinate differs: ${source.id}`);}
      for(const level of Object.keys(snapshot.bundle.doc.layers)) {
        const result=await chrome.cdp.evaluate(`(${compareLayer.toString()})(${JSON.stringify(mapId)},${JSON.stringify(level)})`);
        for(const kind of ['reference','actual']){await writeFile(path.join(report,`${mapId}-${level}-${kind}.png`),Buffer.from(result[kind].split(',')[1],'base64'));delete result[kind];}
        results.push(result);console.log(`${mapId}/${level}: mean ${result.meanError.toFixed(4)}, significant ${(result.largeFraction*100).toFixed(3)}%`);
      }
    }
    await writeFile(path.join(report,'comparison.json'),JSON.stringify({collectedAt:manifest.collectedAt,sourceRendererSha256:first.renderer.sha256,coordinateChecks,results},null,2)+'\n');
    requireValue(results.every(r=>r.interiorMean<.25&&r.interiorLargeFraction<.001),'Visual differences exceed the acceptance threshold; inspect paired images');
    return results;
  }finally{chrome?.cdp.close();chrome?.process.kill();await new Promise(resolve=>server.close(resolve));}
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try{const args=process.argv.slice(2),get=key=>args[args.indexOf(key)+1];requireValue(['--input','--resources','--report'].every(key=>args.includes(key)),'Usage: --input <collection> --resources <candidate> --report <directory>');await verifyDocuments(path.resolve(get('--input')),path.resolve(get('--resources')),path.resolve(get('--report')));}
  catch(error){console.error(error.stack);process.exitCode=1;}
}
