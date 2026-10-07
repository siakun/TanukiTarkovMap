#!/usr/bin/env node
/**
 * Capture decoded map documents and independent page observations from the live site in a
 * disposable headless Chrome. The site runtime is used only here, never shipped in resources.
 * Export aliases and bundle URLs are discovered from the current page. If discovery
 * is ambiguous the adapter stops; minified function names are not a stable API.
 * Usage: node tools/collect-map-docs.mjs --out <empty-directory> [--maps customs,lab]
 * Then: node tools/build-map-resources.mjs --input <collection> --out <new-candidate>
 * This tool does not drive the application or a user's browser.
 */
import { mkdir, writeFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { launchChrome } from './headless-chrome.mjs';
import { configuredMaps, requireValue, sha256 } from './resource-bundle.mjs';

// This is a format-specific collection adapter. Its structural predicates deliberately
// fail when the site changes rather than finding a plausible but unrelated function.
async function capturePage(mapId) {
  const deadline = Date.now() + 15000;
  while (!document.querySelector('canvas.doc-map-canvas')
    || document.querySelector('#__nuxt')?._vnode?.component?.proxy?.$nuxt?.payload?.state?.$squestsState?.map !== mapId) {
    if (Date.now() > deadline) throw new Error('Map canvas did not become ready');
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  const root = document.querySelector('#__nuxt')?._vnode;
  const state = root?.component?.proxy?.$nuxt?.payload?.state?.$squestsState;
  if (state?.map !== mapId || !Array.isArray(state.markers)) throw new Error('Map state is not ready');
  const pending = [root], seen = new Set(); let left, layers;
  while (pending.length) {
    const node = pending.pop(); if (!node || typeof node !== 'object' || seen.has(node)) continue;
    seen.add(node);
    if (node.component) {
      const component = node.component;
      if (component.type?.__name === 'MapLeftPanel') left = component;
      if (component.type?.__name === 'MapLayers') layers = component;
      pending.push(component.subTree);
    }
    if (Array.isArray(node.children)) pending.push(...node.children);
    if (node.suspense?.activeBranch) pending.push(node.suspense.activeBranch);
  }
  if (!left || !layers) throw new Error('Current map components are missing');
  const resources = [...new Set(performance.getEntriesByType('resource').map(entry => entry.name))]
    .filter(url => url.startsWith(location.origin + '/_nuxt3/') && url.endsWith('.js'));
  let renderer, symbols;
  for (const url of resources) {
    const response = await fetch(url); if (!response.ok) throw new Error(`Module fetch failed: ${response.status}`);
    const text = await response.text();
    if (text.includes('doc-map-canvas') && text.includes('flattenOpaqueGroups')) {
      if (renderer) throw new Error('Ambiguous renderer modules'); renderer = { url, text };
    }
    if(/\.stroke\(new Path2D\([\w$]+\)\)/.test(text)&&/\.scale\([\w$]+\/[\w$]+,[\w$]+\/[\w$]+\)/.test(text)) {
      if(symbols)throw new Error('Ambiguous vector symbol modules');symbols={url,text};
    }
  }
  if (!renderer) throw new Error('Current document renderer was not discovered');
  const aliases = [...new Set([...renderer.text.matchAll(/([\w$]+)\([\w$]+\?\.data\)/g)].map(match => match[1]))];
  const imports = [...renderer.text.matchAll(/import\{([^}]+)\}from"([^"]+)"/g)];
  const decoders = [];
  for (const alias of aliases) for (const imported of imports) for (const binding of imported[1].split(',')) {
    const [exportName, localName = exportName] = binding.trim().split(/\s+as\s+/);
    if (localName === alias) decoders.push({ url: new URL(imported[2], renderer.url).href, exportName });
  }
  if (decoders.length !== 1) throw new Error('Decoder discovery was ambiguous');
  const decoder = decoders[0], module = await import(decoder.url);
  const parsers=Object.values(module).filter(value=>typeof value==='function'&&String(value).includes('.groups')&&String(value).includes('look:'));
  if(parsers.length!==1)throw new Error('Screenshot parser discovery was ambiguous');
  const directionFixtures=[[0,0,0,1],[0,Math.SQRT1_2,0,Math.SQRT1_2],[0,1,0,0],[0,-Math.SQRT1_2,0,Math.SQRT1_2],[0,1,0,1],[0,-3,0,3],[.23,.38,-.09,.7]];
  const directionObservations={parserSource:String(parsers[0]),checks:directionFixtures.map(quaternion=>{
    const filename='2000-01-01[00-00]_125.25, -3.5, -84.875_'+quaternion.join(', ')+'_0.png';
    const result=parsers[0](filename);if(!result||!Number.isFinite(result.look))throw new Error('Source screenshot parser failed fixture');
    return {quaternion,filename,result};
  })};
  const endpoint = new URL('/api/be/map-doc', location.origin); endpoint.search = new URLSearchParams({ map:mapId, bundle:'1' });
  const response = await fetch(endpoint, { cache:'no-store' });
  if (!response.ok || !response.headers.get('content-type')?.includes('json')) throw new Error(`Document HTTP ${response.status}`);
  const encoded = await response.json();
  if (typeof encoded.hash !== 'string' || typeof encoded.data !== 'string') throw new Error('Incomplete document response');
  const bundle = await module[decoder.exportName](encoded.data);
  if (bundle?.doc?.map !== mapId || bundle.doc.version !== 2) throw new Error('Unsupported or wrong document');
  const entryResponse = await fetch(decoder.url); const entry = await entryResponse.text();
  const map = left.props.map;
  const points = [{x:0,y:0},{x:125.25,y:-84.875},{x:-313.625,y:211.5},{x:1000,y:1000}];
  const labels = Object.fromEntries([...document.querySelectorAll('[data-layer]')].map(element =>
    [element.dataset.layer, element.textContent.replace(/\([-\d]+\)/g,'').trim()]));
  return {
    mapId, collectedAt:new Date().toISOString(), source:{ page:location.href, url:endpoint.href, hash:encoded.hash },
    encoded, bundle, renderer, symbols, directionObservations, entry:{url:decoder.url,text:entry}, decoderExport:decoder.exportName,
    title:document.title.replace(/^Map - /,'').replace(/ - Tarkov Market$/,''),
    settings:{size:map.size,transform:map.transform},
    levels:Object.entries(layers.props.visibleLayers).map(([id, value]) => ({id,sourceLevel:value.num,
      label:labels[id] || id, defaultVisible:id === layers.props.modelValue})),
    markers:state.markers.filter(marker => marker.category === 'Extractions').map(marker => ({
      id:marker.uid, sourceCategory:marker.category, sourceSubtype:marker.subCategory,
      name:marker.name ?? '', sourceLevel:marker.level, geometry:marker.geometry,
      position:map.gamePosToMapPos(marker.geometry.x, marker.geometry.y),
    })),
    categories:left.props.categories,
    coordinateChecks:points.map(point => ({game:point,map:map.gamePosToMapPos(point.x,point.y)})),
    coordinateFunction:String(map.gamePosToMapPos),
  };
}

async function collectMap(browser, output, mapId) {
  // A fresh profile has no cache, so the map state can arrive well after the canvas appears.
  // Wait for the same condition capturePage checks before handing over to its short timeout.
  const opened = await browser.navigate(`https://tarkov-market.com/maps/${mapId}`,
    `location.pathname === ${JSON.stringify(`/maps/${mapId}`)} && !!document.querySelector('canvas.doc-map-canvas')
      && document.querySelector('#__nuxt')?._vnode?.component?.proxy?.$nuxt?.payload?.state?.$squestsState?.map === ${JSON.stringify(mapId)}`, 90000);
  requireValue(opened, `${mapId}: map page did not open (blocked by a bot check or the page layout changed)`);
  const snapshot = JSON.parse(await browser.evaluate(`(${capturePage.toString()})(${JSON.stringify(mapId)}).then(JSON.stringify)`, 120000));
  const rendererFile = `renderer-${sha256(snapshot.renderer.text)}.js`;
  const entryFile = `entry-${sha256(snapshot.entry.text)}.js`;
  requireValue(snapshot.symbols,'Source vector symbol module was not collected');
  const symbolFile=`symbols-${sha256(snapshot.symbols.text)}.js`;
  await writeFile(path.join(output, rendererFile), snapshot.renderer.text);
  await writeFile(path.join(output, entryFile), snapshot.entry.text);
  await writeFile(path.join(output, symbolFile), snapshot.symbols.text);
  snapshot.renderer = {url:snapshot.renderer.url,file:rendererFile,sha256:sha256(snapshot.renderer.text)};
  snapshot.entry = {url:snapshot.entry.url,file:entryFile,sha256:sha256(snapshot.entry.text)};
  snapshot.symbols = {url:snapshot.symbols.url,file:symbolFile,sha256:sha256(snapshot.symbols.text)};
  await writeFile(path.join(output, `${mapId}.json`), JSON.stringify(snapshot) + '\n');
  console.log(`${mapId}: document v${snapshot.bundle.doc.version}, ${snapshot.markers.length} extraction markers`);
  return {mapId,sourceHash:snapshot.source.hash,file:`${mapId}.json`};
}

export async function collectDocuments(output, maps) {
  maps ??= await configuredMaps();
  await mkdir(output, { recursive:true });
  requireValue((await readdir(output)).length === 0, 'Collection output must be empty');
  const entries = [];
  const browser = await launchChrome({ realUserAgent:true, width:1280, height:1000 });
  try {
    for (const mapId of maps) entries.push(await collectMap(browser, output, mapId));
  } finally { await browser.close(); }
  await writeFile(path.join(output,'collection.json'), JSON.stringify({adapterVersion:1,maps:entries},null,2)+'\n');
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const args = process.argv.slice(2), value = key => args[args.indexOf(key)+1];
    requireValue(args.includes('--out'), 'Usage: --out <empty-directory> [--maps customs,lab]');
    const maps = args.includes('--maps') ? value('--maps').split(',').filter(Boolean) : undefined;
    await collectDocuments(path.resolve(value('--out')), maps);
  } catch(error) { console.error(error.stack); process.exitCode = 1; }
}
