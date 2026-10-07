#!/usr/bin/env node
/**
 * Adversarial inputs for conversion and resource checks. These tests exercise
 * public entrypoints with invalid documents/files, not private implementation state.
 * Usage: node --test tools/test-resource-pipeline.mjs
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, writeFile, readFile, mkdir, rm, cp, readdir } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { convertDocument, validateDocument, wallSegments } from './map-doc-svg.mjs';
import { sha256, checkResources, validateSvg } from './resource-bundle.mjs';
import { buildCandidate } from './build-map-resources.mjs';

const clone=object=>structuredClone(object);
const square=[[1000,1000],[1100,1000],[1100,1100],[1000,1100]];
const document={map:'fixture',version:2,groupOrder:['empty','layout','walls','stairs','accents','room-numbers','zone'],wallWidths:{},bgFill:'',
  layers:{main:[{kind:'poly',role:'building',points:square,holes:[[[1020,1020],[1080,1020],[1080,1080],[1020,1080]]]},
    {kind:'wall',role:'wall',points:[[1000,1120],[1100,1120]],gaps:[{seg:0,t:.5,w:20}]},
    {kind:'rect',role:'stairs-up',rect:[[1120,1000],[1160,1100]]},
    {kind:'ellipse',role:'water',rect:[[1180,1000],[1250,1100]],angle:20},
    {kind:'text',role:'accent',text:{value:'Room',x:1200,y:1140,size:12}}],
    level2:[{kind:'poly',role:'building',points:[[1010,1010],[1040,1010],[1040,1040]],compositeGroups:[{id:'rooms',opacity:.5,renderGroup:'layout'}]}]},
  levelContexts:{level2:[{layer:'main',opacity:.1}]},sharedGroups:{main:['walls']}};
test('known shapes, holes, gaps and isolated groups survive conversion',()=>{
  const svg=convertDocument(document,{width:3000,height:3000});validateSvg(svg);
  assert.match(svg,/fill-rule="evenodd"/);assert.match(svg,/data-composite="rooms" opacity="0.5"/);
  assert.match(svg,/id="level2"/);assert.match(svg,/>Room<\/text>/);
  assert.deepEqual(wallSegments(document.layers.main[1]),[[[1000,1120],[1040,1120]],[[1060,1120],[1100,1120]]]);
});
test('gaps cross vertices and closed-loop boundaries',()=>{
  assert.deepEqual(wallSegments({kind:'wall',role:'wall',points:[[0,0],[10,0],[10,10]],gaps:[{seg:0,t:1,w:10}]}),[[[0,0],[5,0]],[[10,5],[10,10]]]);
  assert.deepEqual(wallSegments({kind:'wall',role:'wall',closed:true,points:[[0,0],[10,0],[10,10],[0,10]],gaps:[{seg:0,t:0,w:10}]}),[[[5,0],[10,0],[10,10],[0,10],[0,5]]]);
});
test('unknown versions, shapes, fields and references fail instead of dropping data',()=>{
  const mutations=[d=>d.version=3,d=>d.layers.main[0].kind='mesh',d=>d.layers.main[0].unknownTransform={},
    d=>d.layers.main[0].role='unseen',d=>d.layers.main[0].style={fill:'#000',blendMode:'multiply'},
    d=>d.layers.main[0].points[0][0]=NaN,d=>d.layers.main[1].gaps[0].seg=50,
    d=>d.levelContexts.level2[0].layer='missing',d=>d.sharedGroups.missing=['walls'],
    d=>d.layers.level2.push({...d.layers.level2[0],compositeGroups:[{id:'rooms',opacity:.8,renderGroup:'layout'}]})];
  for(const mutate of mutations){const input=clone(document);mutate(input);assert.throws(()=>validateDocument(input));}
});
test('SVG rejects script, external references, encoded and CSS-obfuscated URLs',()=>{
  for(const body of ['<script>alert(1)</script>','<image href="https://example.invalid/a"/>','<rect onload="alert(1)"/>',
    '<rect style="fill:url(https://example.invalid/a)"/>','<rect style="fill:u\\72l(https://example.invalid/a)"/>',
    '<rect style="fill:url/**/(https://example.invalid/a)"/>','<use href="&#104;ttps://example.invalid/a"/>']) {
    assert.throws(()=>validateSvg(`<svg xmlns="http://www.w3.org/2000/svg">${body}</svg>`));
  }
});
async function fixtureBundle(directory) {
  const mapDirectory=path.join(directory,'maps','fixture');await mkdir(mapDirectory,{recursive:true});
  await writeFile(path.join(directory,'manifest.json'),JSON.stringify({schemaVersion:1,maps:['fixture']}));
  await writeFile(path.join(mapDirectory,'map.svg'),convertDocument(document,{width:3000,height:3000}));
  await writeFile(path.join(mapDirectory,'meta.json'),JSON.stringify({schemaVersion:1,mapId:'fixture',title:'Fixture',size:{width:3000,height:3000},zoom:1,minZoom:.1,maxZoom:10,
    transform:{rotate:0,xOffset:1000,yOffset:1000,invertX:false,invertY:false,ratio:1},levels:[{id:'main',sourceLevel:1,label:'Main',terrainGroupId:'main',defaultVisible:true},{id:'level2',sourceLevel:2,label:'Level 2',terrainGroupId:'level2',defaultVisible:false}]}));
  await writeFile(path.join(mapDirectory,'markers.json'),JSON.stringify({schemaVersion:1,mapId:'fixture',coordinateSpace:'map',factions:[{id:'pmc'},{id:'scav'}],categories:[{id:'extraction',subtypes:[{id:'pmc',factions:['pmc']}]}],markers:[{id:'exit',category:'extraction',subtype:'pmc',levelId:'main',position:{x:1050,y:1050}}]}));
}
test('broken SVG, missing files, extra files, duplicate UID, bad heights and bad viewBox fail',async()=>{
  const temp=await mkdtemp(path.join(os.tmpdir(),'tanuki-bundle-test-')),base=path.join(temp,'base');await fixtureBundle(base);
  try {
    await checkResources(base,{expectedMaps:['fixture']});
    const cases=[
      async d=>writeFile(path.join(d,'maps/fixture/map.svg'),'<svg/>'),
      async d=>rm(path.join(d,'maps/fixture/markers.json')),
      async d=>writeFile(path.join(d,'maps/fixture/notes.txt'),'not part of the map data'),
      async d=>{const file=path.join(d,'maps/fixture/markers.json'),value=JSON.parse(await readFile(file));value.markers.push(value.markers[0]);await writeFile(file,JSON.stringify(value));},
      async d=>{const file=path.join(d,'maps/fixture/markers.json'),value=JSON.parse(await readFile(file));value.source={listedCounts:{'PMC Extraction':1}};await writeFile(file,JSON.stringify(value));},
      async d=>{const file=path.join(d,'maps/fixture/meta.json'),value=JSON.parse(await readFile(file));value.levels[0].height=[0];await writeFile(file,JSON.stringify(value));},
      async d=>{const file=path.join(d,'maps/fixture/map.svg'),value=await readFile(file,'utf8');await writeFile(file,value.replace('viewBox="0 0 3000 3000"','viewBox="0 0 3001 3000"'));},
    ];
    for(let i=0;i<cases.length;i++){const target=path.join(temp,`case-${i}`);await cp(base,target,{recursive:true});await cases[i](target);await assert.rejects(checkResources(target,{expectedMaps:['fixture']}));}
    await assert.rejects(checkResources(base,{expectedMaps:['fixture','missing-map']}),/MapConfiguration/);
  }finally{assert.equal(path.dirname(temp),os.tmpdir());await rm(temp,{recursive:true,force:true});}
});
test('failed collection and existing destination preserve a complete bundle',async()=>{
  const temp=await mkdtemp(path.join(os.tmpdir(),'tanuki-preserve-test-')),active=path.join(temp,'active'),input=path.join(temp,'input');await fixtureBundle(active);await mkdir(input);
  const before=await readFile(path.join(active,'manifest.json'));
  await writeFile(path.join(input,'collection.json'),JSON.stringify({adapterVersion:1,maps:[]}));
  try {
    await assert.rejects(buildCandidate(input,active,{expectedMaps:['fixture']}),/already exists/);
    await assert.rejects(buildCandidate(input,path.join(temp,'candidate'),{expectedMaps:['fixture']}),/Incomplete/);
    assert.deepEqual(await readFile(path.join(active,'manifest.json')),before);await checkResources(active,{expectedMaps:['fixture']});
    assert.deepEqual((await readdir(temp)).sort(),['active','input']);
  }finally{assert.equal(path.dirname(temp),os.tmpdir());await rm(temp,{recursive:true,force:true});}
});
test('failure in the last map removes staging and preserves the active revision',async()=>{
  const temp=await mkdtemp(path.join(os.tmpdir(),'tanuki-last-map-test-')),active=path.join(temp,'active'),input=path.join(temp,'input');await fixtureBundle(active);await mkdir(input);
  const before=await readFile(path.join(active,'manifest.json')),maps=['fixture-a','fixture-b'];
  const settings={size:{width:3000,height:3000},zoom:1,minZoom:.1,maxZoom:10,transform:{rotate:0,xOffset:1000,yOffset:1000,invertX:false,invertY:false,ratio:1}};
  const settingsLiteral='{size:{width:3000,height:3000},zoom:1,minZoom:.1,maxZoom:10,transform:{rotate:0,xOffset:1000,yOffset:1000,invertX:false,invertY:false,ratio:1}}';
  const entry='{'+maps.map(map=>`"${map}":${settingsLiteral}`).join(',')+'}';
  await writeFile(path.join(input,'entry.js'),entry);await writeFile(path.join(input,'renderer.js'),'fixture renderer');await writeFile(path.join(input,'symbols.js'),'fixture symbols');
  for(const mapId of maps){const doc=clone(document);doc.map=mapId;if(mapId===maps.at(-1))doc.layers.main[0].kind='unsupported';
    const snapshot={mapId,collectedAt:'2000-01-01T00:00:00Z',source:{hash:'fixture',page:'https://example.invalid',url:'https://example.invalid/doc'},bundle:{doc},title:'Fixture',settings,
      levels:[{id:'main',sourceLevel:1,label:'Main',defaultVisible:true},{id:'level2',sourceLevel:2,label:'Second',defaultVisible:false}],markers:[],categories:{Extractions:{}},
      coordinateChecks:[{game:{x:0,y:0},map:{x:1000,y:1000}},{game:{x:1,y:2},map:{x:999,y:998}},{game:{x:-3,y:4},map:{x:1003,y:996}},{game:{x:5,y:-6},map:{x:995,y:1006}}],
      entry:{file:'entry.js',sha256:sha256(entry)},renderer:{file:'renderer.js',sha256:sha256('fixture renderer')},symbols:{file:'symbols.js',sha256:sha256('fixture symbols')}};
    await writeFile(path.join(input,`${mapId}.json`),JSON.stringify(snapshot));}
  await writeFile(path.join(input,'collection.json'),JSON.stringify({adapterVersion:1,maps:maps.map(mapId=>({mapId,file:`${mapId}.json`,sourceHash:'fixture'}))}));
  try{await assert.rejects(buildCandidate(input,path.join(temp,'candidate'),{expectedMaps:maps}),/Unknown shape/);
    assert.deepEqual(await readFile(path.join(active,'manifest.json')),before);await checkResources(active,{expectedMaps:['fixture']});
    assert.deepEqual((await readdir(temp)).sort(),['active','input']);
  }finally{assert.equal(path.dirname(temp),os.tmpdir());await rm(temp,{recursive:true,force:true});}
});
