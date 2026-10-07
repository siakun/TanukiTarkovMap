#!/usr/bin/env node
/**
 * Build an entire resource candidate from a completed collection. Nothing is written
 * to the destination until all maps pass conversion and structure checks. Existing
 * directories are never overwritten; replacing resources/ remains a separate reviewed step.
 * Usage: node tools/build-map-resources.mjs --input <collection> --out <new-candidate>
 */
import { readFile, writeFile, mkdir, mkdtemp, rename, rm, access } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { convertDocument, CONVERTER_VERSION } from './map-doc-svg.mjs';
import { configuredMaps, requireValue, sha256, checkResources, safeRelative } from './resource-bundle.mjs';

export const markerCategories = [{id:'extraction',label:'추출구',sourceCategory:'Extractions',defaultVisible:true,subtypes:[
  {id:'transit',label:'Transit',sourceSubtype:'Transition',factions:['pmc']},
  {id:'pmc',label:'PMC',sourceSubtype:'PMC Extraction',factions:['pmc']},
  {id:'scav',label:'SCAV',sourceSubtype:'Scav Extraction',factions:['scav']},
  {id:'co-op',label:'Co-Op',sourceSubtype:'Co-Op Extraction',factions:['pmc','scav']},
]}];
export function mapPosition(x,y,transform) {
  const round=n=>Math.round(n*10000)/10000;
  if(transform.rotate) {const angle=-transform.rotate*Math.PI/180,c=Math.cos(angle),s=Math.sin(angle);[x,y]=[round(x*c-y*s),round(x*s+y*c)];}
  return {x:round(transform.xOffset-x*transform.ratio),y:round(transform.yOffset-y*transform.ratio)};
}
export function parseSettings(source,mapId) {
  const n='(-?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:e[+-]?\\d+)?)',b='(true|false|![01])';
  const pattern=new RegExp(`(?:[,{])(?:"${mapId}"|${mapId}):\\{size:\\{width:${n},height:${n}\\},zoom:${n},minZoom:${n},maxZoom:${n},transform:\\{rotate:${n},xOffset:${n},yOffset:${n},invertX:${b},invertY:${b},ratio:${n}\\}\\}`,'g');
  const matches=[...source.matchAll(pattern)];requireValue(matches.length===1,`${mapId}: settings discovery failed or ambiguous`);
  const [width,height,zoom,minZoom,maxZoom,rotate,xOffset,yOffset,invertX,invertY,ratio]=matches[0].slice(1);
  return {size:{width:+width,height:+height},zoom:+zoom,minZoom:+minZoom,maxZoom:+maxZoom,
    transform:{rotate:+rotate,xOffset:+xOffset,yOffset:+yOffset,invertX:['true','!0'].includes(invertX),invertY:['true','!0'].includes(invertY),ratio:+ratio}};
}
export function normalizeSnapshot(snapshot,entrySource) {
  const mapId=snapshot.mapId,doc=snapshot.bundle?.doc,settings=parseSettings(entrySource,mapId);
  requireValue(doc?.map===mapId&&snapshot.source?.hash&&snapshot.collectedAt,`${mapId}: incomplete source evidence`);
  requireValue(JSON.stringify(settings.size)===JSON.stringify(snapshot.settings.size),`${mapId}: observed map size mismatch`);
  for(const [key,value]of Object.entries(snapshot.settings.transform))requireValue(settings.transform[key]===value,`${mapId}: observed transform mismatch: ${key}`);
  requireValue(snapshot.coordinateChecks?.length>=4,`${mapId}: missing independent coordinate observations`);
  for(const check of snapshot.coordinateChecks)requireValue(JSON.stringify(mapPosition(check.game.x,check.game.y,settings.transform))===JSON.stringify(check.map),`${mapId}: coordinate conversion mismatch`);
  // The site selects the level from the player's height: zones first, then the level height range.
  // Keep those ranges so the local viewer can switch levels the same way.
  const levels=snapshot.levels.map(level=>{const config=doc.heightConfig?.[level.id]??{};
    return {...level,terrainGroupId:level.id,...(config.height?.length===2?{height:config.height}:{}),...(config.zones?.length?{zones:config.zones}:{})};});
  requireValue(JSON.stringify(levels.map(l=>l.id).sort())===JSON.stringify(Object.keys(doc.layers).sort()),`${mapId}: document/page layer mismatch`);
  const levelMap=new Map(levels.map(level=>[level.sourceLevel,level.id])),counts={};
  const markers=snapshot.markers.map(marker=>{
    const subtype=markerCategories[0].subtypes.find(type=>type.sourceSubtype===marker.sourceSubtype);
    requireValue(marker.sourceCategory==='Extractions'&&subtype,`${mapId}: unknown extraction subtype ${marker.sourceSubtype}`);
    requireValue(levelMap.has(marker.sourceLevel),`${mapId}: marker refers to missing source level`);
    const position=mapPosition(marker.geometry.x,marker.geometry.y,settings.transform);
    requireValue(JSON.stringify(position)===JSON.stringify(marker.position),`${mapId}: marker coordinate differs from live page: ${marker.id}`);
    counts[marker.sourceSubtype]=(counts[marker.sourceSubtype]||0)+1;
    return {id:marker.id,category:'extraction',subtype:subtype.id,name:marker.name,levelId:levelMap.get(marker.sourceLevel),position};
  }).sort((a,b)=>a.id.localeCompare(b.id,'en'));
  for(const [subtype,count]of Object.entries(snapshot.categories.Extractions??{}))requireValue((counts[subtype]??0)===count,`${mapId}: extraction listing mismatch: ${subtype}`);
  for(const [subtype,count]of Object.entries(counts))requireValue(snapshot.categories.Extractions?.[subtype]===count,`${mapId}: extraction missing from listing`);
  const source={site:'https://tarkov-market.com',page:snapshot.source.page,collectedAt:snapshot.collectedAt,
    documentUrl:snapshot.source.url,documentHash:snapshot.source.hash,documentSha256:sha256(JSON.stringify(snapshot.bundle)),
    rendererUrl:snapshot.renderer.url,rendererSha256:snapshot.renderer.sha256,settingsUrl:snapshot.entry.url,settingsSha256:snapshot.entry.sha256,converterVersion:CONVERTER_VERSION};
  return {
    svg:convertDocument(doc,settings.size),
    meta:{schemaVersion:1,mapId,title:snapshot.title,...settings,levels,source:{...source,
      coordinateFormula:{rotation:'-transform.rotate degrees',mapX:'xOffset - rotatedX * ratio',mapY:'yOffset - rotatedY * ratio',rounding:'4 decimal places'},
      directionFormula:{directionX:'2 * (quaternion.x * quaternion.z + quaternion.w * quaternion.y)',directionZ:'1 - 2 * (quaternion.x^2 + quaternion.y^2)',screenDegrees:'normalize(gameDegrees + 270 - transform.rotate)'},
      // Game -> map pairs computed by the site's own gamePosToMapPos. verify-viewer.mjs compares the viewer with them.
      coordinateChecks:snapshot.coordinateChecks}},
    markers:{schemaVersion:1,mapId,coordinateSpace:'map',factions:[{id:'pmc',label:'PMC',defaultSelected:true},{id:'scav',label:'SCAV',defaultSelected:false}],
      categories:markerCategories,markers,source:{...source,listedCounts:{extraction:Object.fromEntries(markerCategories[0].subtypes.map(type=>[type.id,counts[type.sourceSubtype]??0]))},mapPosition:'Observed page gamePosToMapPos(geometry.x, geometry.y)',
        markerSha256:sha256(JSON.stringify(snapshot.markers))}},
  };
}
export async function buildCandidate(input,output,options={}) {
  input=path.resolve(input);output=path.resolve(output);
  let exists=true;try{await access(output);}catch{exists=false;}requireValue(!exists,'Candidate destination already exists');
  const maps=options.expectedMaps??await configuredMaps(),collection=JSON.parse(await readFile(path.join(input,'collection.json'),'utf8'));
  requireValue(collection.adapterVersion===1&&Array.isArray(collection.maps),'Unsupported collection');
  requireValue(collection.maps.length===maps.length&&new Set(collection.maps.map(m=>m.mapId)).size===maps.length
    && maps.every(map=>collection.maps.some(m=>m.mapId===map)),'Incomplete source collection');
  const parent=path.dirname(output);await mkdir(parent,{recursive:true});
  const staging=await mkdtemp(path.join(parent,'.tanuki-candidate-'));
  try {
    const provenance={};let collectedAt='',screenshotChecks=[];
    for(const mapId of maps) {
      const entry=collection.maps.find(item=>item.mapId===mapId);safeRelative(entry.file);
      const snapshot=JSON.parse(await readFile(path.join(input,entry.file),'utf8'));
      requireValue(snapshot.mapId===mapId&&snapshot.source.hash===entry.sourceHash,`${mapId}: source collection identity mismatch`);
      safeRelative(snapshot.entry.file);safeRelative(snapshot.renderer.file);
      const entrySource=await readFile(path.join(input,snapshot.entry.file),'utf8'),renderer=await readFile(path.join(input,snapshot.renderer.file),'utf8');
      requireValue(sha256(entrySource)===snapshot.entry.sha256&&sha256(renderer)===snapshot.renderer.sha256,`${mapId}: source module integrity mismatch`);
      requireValue(snapshot.symbols?.file,`${mapId}: missing vector symbol evidence`);safeRelative(snapshot.symbols.file);
      requireValue(sha256(await readFile(path.join(input,snapshot.symbols.file)))===snapshot.symbols.sha256,`${mapId}: vector symbol evidence mismatch`);
      const normalized=normalizeSnapshot(snapshot,entrySource),directory=path.join(staging,'maps',mapId);await mkdir(directory,{recursive:true});
      await writeFile(path.join(directory,'map.svg'),normalized.svg);
      await writeFile(path.join(directory,'meta.json'),JSON.stringify(normalized.meta,null,2)+'\n');
      await writeFile(path.join(directory,'markers.json'),JSON.stringify(normalized.markers,null,2)+'\n');
      provenance[mapId]={documentHash:snapshot.source.hash,documentSha256:normalized.meta.source.documentSha256,markerSha256:normalized.markers.source.markerSha256};
      if(snapshot.collectedAt>collectedAt)collectedAt=snapshot.collectedAt;
      // The site's screenshot parser output for fixed filenames. It is the same on every map page.
      if(!screenshotChecks.length&&snapshot.directionObservations?.checks)screenshotChecks=snapshot.directionObservations.checks.map(({filename,result})=>({filename,result}));
    }
    await writeFile(path.join(staging,'manifest.json'),JSON.stringify({schemaVersion:1,source:'https://tarkov-market.com',collectedAt,maps,converterVersion:CONVERTER_VERSION,sourceRevisions:provenance,screenshotChecks},null,2)+'\n');
    const manifest=await checkResources(staging,{expectedMaps:maps});
    await rename(staging,output);return manifest;
  } catch(error) {
    // This generated sibling is the only path cleanup owns. Existing bundles have
    // never been opened for writing, including when the last map fails conversion.
    requireValue(path.dirname(staging)===parent&&path.basename(staging).startsWith('.tanuki-candidate-'),'Invalid staging cleanup path');
    await rm(staging,{recursive:true,force:true});throw error;
  }
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {const args=process.argv.slice(2),value=key=>args[args.indexOf(key)+1];requireValue(args.includes('--input')&&args.includes('--out'),'Usage: --input <collection> --out <new-candidate>');
    const manifest=await buildCandidate(value('--input'),value('--out'));console.log(`${manifest.maps.length} maps, collected ${manifest.collectedAt}`);
  } catch(error){console.error(error.stack);process.exitCode=1;}
}
