#!/usr/bin/env node
/**
 * extract-resources.mjs - 저장된 사이트 사본에서 읽을 수 있는 맵 리소스를 추출한다
 *
 * 목적: archive의 최소화된 사이트 번들을 사람이 고칠 수 있는 map.svg와 meta.json으로 바꾼다.
 * 실측값을 손으로 옮기지 않고 원본 blob과 함께 기록해야 사이트가 바뀌었을 때 차이를 추적할 수 있다.
 *
 * 현재 범위: 1단계 스파이크인 shoreline만 지원한다. 다른 맵은 설정과 지형을 찾는 공통 경로를
 * 그대로 쓰되, 레벨 의미를 확인하기 전에는 지원 목록에 넣지 않는다.
 *
 * 사용법:
 *   node tools/extract-resources.mjs
 *   node tools/extract-resources.mjs --maps shoreline
 *   node tools/extract-resources.mjs --archive D:\archive --out D:\resources
 *
 * 입력은 archive-maps.mjs가 만든 색인과 blob뿐이다. 네트워크와 브라우저를 사용하지 않는다.
 */
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';

const SUPPORTED_MAPS = new Set(['shoreline']);
const LEVEL_LABELS = {
  basement: '지하',
  main: '지상',
  level2: '2층',
  level3: '3층',
};

const args = process.argv.slice(2);

function argValue(name, fallback) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  if (!args[index + 1] || args[index + 1].startsWith('--')) {
    throw new Error(`${name} 뒤에 값을 지정해야 합니다.`);
  }
  return args[index + 1];
}

const archivePath = path.resolve(argValue('--archive', path.join(process.cwd(), 'archive')));
const outputPath = path.resolve(argValue('--out', path.join(process.cwd(), 'resources')));
const requestedMaps = argValue('--maps', 'shoreline')
  .split(',')
  .map((mapId) => mapId.trim())
  .filter(Boolean);

if (requestedMaps.length === 0) throw new Error('추출할 맵을 하나 이상 지정해야 합니다.');

for (const mapId of requestedMaps) {
  if (!SUPPORTED_MAPS.has(mapId)) {
    throw new Error(`${mapId}: 레벨 구조를 아직 검증하지 않아 추출할 수 없습니다.`);
  }
}

const archiveManifest = await readJson(path.join(archivePath, 'manifest.json'), 'archive manifest');
const blobCache = new Map();

async function readJson(filePath, description) {
  try {
    return JSON.parse(await readFile(filePath, 'utf8'));
  } catch (error) {
    throw new Error(`${description}를 읽지 못했습니다: ${filePath}\n${error.message}`);
  }
}

async function readBlob(blobHash) {
  if (!blobCache.has(blobHash)) {
    const blobPath = path.join(archivePath, 'blobs', blobHash);
    blobCache.set(blobHash, await readFile(blobPath, 'utf8'));
  }
  return blobCache.get(blobHash);
}

function escapePattern(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

const NUMBER_PATTERN = '-?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:e[+-]?\\d+)?';

function parseBoolean(value) {
  if (value === 'true' || value === '!0') return true;
  if (value === 'false' || value === '!1') return false;
  throw new Error(`알 수 없는 boolean 표기입니다: ${value}`);
}

function parseMapSettings(bundle, mapId) {
  const mapKey = `(?:["']${escapePattern(mapId)}["']|${escapePattern(mapId)})`;
  const propertyGap = '\\s*:\\s*';
  const separator = '\\s*,\\s*';
  const booleanPattern = '(?:true|false|![01])';
  const pattern = new RegExp(
    `(?:^|[,{])\\s*${mapKey}${propertyGap}\\{` +
      `size${propertyGap}\\{width${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `height${propertyGap}(${NUMBER_PATTERN})\\}${separator}` +
      `zoom${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `minZoom${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `maxZoom${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `transform${propertyGap}\\{rotate${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `xOffset${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `yOffset${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `invertX${propertyGap}(${booleanPattern})${separator}` +
      `invertY${propertyGap}(${booleanPattern})${separator}` +
      `ratio${propertyGap}(${NUMBER_PATTERN})\\}\\}`,
    'i'
  );
  const match = bundle.match(pattern);
  if (!match) return null;

  const [
    width, height, zoom, minZoom, maxZoom, rotate, xOffset, yOffset,
    invertX, invertY, ratio,
  ] = match.slice(1);

  return {
    size: { width: Number(width), height: Number(height) },
    zoom: Number(zoom),
    minZoom: Number(minZoom),
    maxZoom: Number(maxZoom),
    transform: {
      rotate: Number(rotate),
      xOffset: Number(xOffset),
      yOffset: Number(yOffset),
      invertX: parseBoolean(invertX),
      invertY: parseBoolean(invertY),
      ratio: Number(ratio),
    },
  };
}

function hasCoordinateFormula(bundle) {
  const name = '[A-Za-z_$][\\w$]*';
  const xFormula = new RegExp(`\\.${'xOffset'}-${name}\\*${name}\\.ratio`);
  const yFormula = new RegExp(`\\.${'yOffset'}-${name}\\*${name}\\.ratio`);
  const rounding = new RegExp(`Math\\.round\\(${name}\\*1e4\\)\\/1e4`);
  const clockwiseRotation = new RegExp(`${name}=-${name}\\*\\(Math\\.PI\\/180\\)`);
  return xFormula.test(bundle) && yFormula.test(bundle) && rounding.test(bundle)
    && clockwiseRotation.test(bundle);
}

function extractSvgMarkup(moduleText) {
  const defsIndex = moduleText.indexOf('<defs>');
  if (defsIndex < 0) throw new Error('지형 모듈에서 <defs>를 찾지 못했습니다.');

  const quoteIndex = moduleText.lastIndexOf("'", defsIndex);
  if (quoteIndex < 0) throw new Error('지형 SVG 문자열의 시작을 찾지 못했습니다.');

  let escaped = false;
  let markup = '';
  for (let index = quoteIndex + 1; index < moduleText.length; index++) {
    const character = moduleText[index];
    if (!escaped && character === "'") return decodeJavaScriptString(markup);
    markup += character;
    escaped = !escaped && character === '\\';
    if (character !== '\\') escaped = false;
  }

  throw new Error('지형 SVG 문자열의 끝을 찾지 못했습니다.');
}

function decodeJavaScriptString(value) {
  let decoded = '';

  for (let index = 0; index < value.length; index++) {
    const character = value[index];
    if (character !== '\\') {
      decoded += character;
      continue;
    }

    const escape = value[++index];
    const simpleEscapes = {
      "'": "'", '"': '"', '\\': '\\', n: '\n', r: '\r', t: '\t',
      b: '\b', f: '\f', v: '\v', 0: '\0',
    };
    if (escape in simpleEscapes) {
      decoded += simpleEscapes[escape];
      continue;
    }

    if (escape === 'x') {
      decoded += String.fromCharCode(Number.parseInt(value.slice(index + 1, index + 3), 16));
      index += 2;
      continue;
    }

    if (escape === 'u') {
      decoded += String.fromCharCode(Number.parseInt(value.slice(index + 1, index + 5), 16));
      index += 4;
      continue;
    }

    if (escape === '\n') continue;
    if (escape === '\r' && value[index + 1] === '\n') {
      index++;
      continue;
    }

    throw new Error(`지원하지 않는 JavaScript 문자열 이스케이프입니다: \\${escape}`);
  }

  return decoded;
}

function assertPassiveSvg(markup) {
  const forbidden = [
    { pattern: /<script\b/i, name: '<script>' },
    { pattern: /<foreignObject\b/i, name: '<foreignObject>' },
    { pattern: /\son[a-z]+\s*=/i, name: '이벤트 속성' },
    { pattern: /(?:href|xlink:href)\s*=\s*["'](?:https?:|javascript:|data:)/i, name: '외부 또는 실행 URL' },
  ];

  for (const entry of forbidden) {
    if (entry.pattern.test(markup)) {
      throw new Error(`지형 SVG에 데이터가 아닌 ${entry.name}이 들어 있습니다.`);
    }
  }
}

function formatSvg(mapId, settings, innerMarkup, source) {
  const emptyElements = innerMarkup.replace(
    /<([A-Za-z][\w:.-]*)([^>]*)><\/\1>/g,
    '<$1$2 />'
  );
  const lines = emptyElements.replace(/></g, '>\n<').split('\n');
  const formatted = [];
  let depth = 1;

  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('</')) depth--;
    formatted.push(`${'  '.repeat(Math.max(1, depth))}${trimmed}`);

    const opensElement = /^<[^!?/][^>]*>$/.test(trimmed);
    const closesOnSameLine = /<\/[^>]+>$/.test(trimmed);
    const selfClosing = /\/>$/.test(trimmed);
    if (opensElement && !closesOnSameLine && !selfClosing) depth++;
  }

  const { width, height } = settings.size;
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    `<!-- tarkov-market ${mapId} terrain; archive ${source.archiveCreatedAt}; ` +
      `source blob ${source.terrainBlob}. Generated by tools/extract-resources.mjs. -->`,
    `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" ` +
      `width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" ` +
      `class="svg-map map_${mapId}">`,
    ...formatted,
    '</svg>',
    '',
  ].join('\n');
}

function findLevels(markup) {
  const levels = Object.entries(LEVEL_LABELS)
    .filter(([levelId]) => new RegExp(`\\bid=["']${escapePattern(levelId)}["']`).test(markup))
    .map(([id, label]) => ({ id, label, defaultVisible: id === 'main' }));

  if (levels.length !== Object.keys(LEVEL_LABELS).length) {
    throw new Error(`shoreline 레벨 그룹이 ${levels.length}개만 있습니다.`);
  }
  return levels;
}

async function findSource(index, mapId) {
  const javascriptEntries = Object.entries(index)
    .filter(([, entry]) => String(entry.mime || '').includes('javascript'));
  const settingsSources = [];

  for (const [url, entry] of javascriptEntries) {
    const bundle = await readBlob(entry.blob);
    if (!bundle.includes('xOffset')) continue;

    const settings = parseMapSettings(bundle, mapId);
    if (settings) settingsSources.push({ url, entry, bundle, settings });
  }

  if (settingsSources.length !== 1) {
    throw new Error(`${mapId}: 설정 bundle을 하나로 특정하지 못했습니다 (${settingsSources.length}개).`);
  }

  const settingsSource = settingsSources[0];
  if (!hasCoordinateFormula(settingsSource.bundle)) {
    throw new Error(`${mapId}: 확인한 좌표 변환식이 bundle에서 바뀌었습니다.`);
  }

  const { width, height } = settingsSource.settings.size;
  const terrainSources = [];

  for (const [url, entry] of javascriptEntries) {
    const moduleText = await readBlob(entry.blob);
    if (!moduleText.includes(`viewBox:"0 0 ${width} ${height}"`)) continue;
    if (!moduleText.includes('<g id="wrapper">') || !moduleText.includes('<defs>')) continue;
    terrainSources.push({ url, entry, moduleText });
  }

  if (terrainSources.length !== 1) {
    throw new Error(`${mapId}: 지형 module을 하나로 특정하지 못했습니다 (${terrainSources.length}개).`);
  }

  return { settingsSource, terrainSource: terrainSources[0] };
}

const extractedMaps = [];

for (const mapId of requestedMaps) {
  const indexPath = path.join(archivePath, 'maps', `${mapId}.json`);
  const index = await readJson(indexPath, `${mapId} archive index`);
  const { settingsSource, terrainSource } = await findSource(index, mapId);
  const innerMarkup = extractSvgMarkup(terrainSource.moduleText);
  assertPassiveSvg(innerMarkup);
  const levels = findLevels(innerMarkup);
  const mapPath = path.join(outputPath, 'maps', mapId);
  const source = {
    site: archiveManifest.site,
    page: archiveManifest.maps?.[mapId]?.url || `${archiveManifest.site}/maps/${mapId}`,
    archiveCreatedAt: archiveManifest.createdAt,
    settingsUrl: settingsSource.url,
    settingsBlob: settingsSource.entry.blob,
    terrainUrl: terrainSource.url,
    terrainBlob: terrainSource.entry.blob,
    coordinateFormula: {
      rotation: '-transform.rotate degrees',
      mapX: 'xOffset - rotatedX * ratio',
      mapY: 'yOffset - rotatedY * ratio',
      rounding: '4 decimal places',
    },
  };
  const meta = {
    schemaVersion: 1,
    mapId,
    title: mapId === 'shoreline' ? 'Shoreline' : mapId,
    ...settingsSource.settings,
    levels,
    source,
  };

  await mkdir(mapPath, { recursive: true });
  await writeFile(
    path.join(mapPath, 'map.svg'),
    formatSvg(mapId, settingsSource.settings, innerMarkup, source),
    'utf8'
  );
  await writeFile(path.join(mapPath, 'meta.json'), `${JSON.stringify(meta, null, 2)}\n`, 'utf8');
  extractedMaps.push(mapId);

  console.log(
    `${mapId}: ${settingsSource.settings.size.width}x${settingsSource.settings.size.height}, ` +
    `레벨 ${levels.length}개, 지형 blob ${terrainSource.entry.blob}`
  );
}

const existingManifestPath = path.join(outputPath, 'manifest.json');
let existingMaps = [];
if (existsSync(existingManifestPath)) {
  const existingManifest = await readJson(existingManifestPath, 'resource manifest');
  if (Array.isArray(existingManifest.maps)) existingMaps = existingManifest.maps;
}

const maps = [...new Set([...existingMaps, ...extractedMaps])].sort();
const resourceManifest = {
  schemaVersion: 1,
  source: archiveManifest.site,
  collectedAt: archiveManifest.createdAt,
  maps,
};
await mkdir(outputPath, { recursive: true });
await writeFile(existingManifestPath, `${JSON.stringify(resourceManifest, null, 2)}\n`, 'utf8');

console.log(`리소스 저장 위치: ${outputPath}`);
