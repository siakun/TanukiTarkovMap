#!/usr/bin/env node
/**
 * Structure checks for the passive map resources (resources/) that the local viewer reads.
 * The builder runs them on every candidate, and CI runs them on the committed resources:
 * every configured map must be present, SVG must stay passive (no scripts or external
 * references), and metadata, levels and markers must refer to each other consistently.
 * Usage: node tools/resource-bundle.mjs check [resources-directory]
 */
import { createHash } from 'node:crypto';
import { readFile, readdir, lstat, realpath } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
export async function configuredMaps() {
  const source = await readFile(path.join(root, 'src/TanukiTarkovMap/Models/Data/MapConfiguration.cs'), 'utf8');
  const maps = [...source.matchAll(/new MapInfo\("([a-z0-9-]+)"/g)].map(match => match[1]);
  requireValue(maps.length && new Set(maps).size === maps.length, 'Invalid configured map list');
  return maps;
}
export function requireValue(condition, message) {
  if (!condition) throw new Error(message);
}
export function safeRelative(name) {
  requireValue(typeof name === 'string' && /^[a-zA-Z0-9_.-]+(?:\/[a-zA-Z0-9_.-]+)*$/.test(name)
    && !name.split('/').some(part => part === '.' || part === '..' || part.endsWith('.')),
  `Unsafe resource path: ${name}`);
  return name;
}
async function readLocal(directory, name) {
  safeRelative(name);
  const base = await realpath(directory);
  let target = base;
  for (const part of name.split('/')) {
    target = path.join(target, part);
    requireValue(!(await lstat(target)).isSymbolicLink(), `Symbolic link in bundle: ${name}`);
  }
  requireValue((await realpath(target)).startsWith(base + path.sep), `Resource escapes bundle: ${name}`);
  return readFile(target);
}
const finite = value => typeof value === 'number' && Number.isFinite(value);
const passiveTags = new Set(['svg','g','defs','path','rect','ellipse','circle','line','polyline','polygon',
  'text','tspan','pattern','linearGradient','radialGradient','stop','clipPath','mask','filter','feOffset',
  'feGaussianBlur','feFlood','feComposite','feMerge','feMergeNode','feColorMatrix','title','desc','use']);

// An allowlist prevents new SVG executable/reference features from silently entering
// the data contract. Entity declarations, CSS escapes and external paint servers fail.
export function validateSvg(svg, label = 'SVG') {
  requireValue(!/<!DOCTYPE|<!ENTITY|<\?[^x]|<\?(?!xml\s)/i.test(svg), `${label}: XML declarations are forbidden`);
  const clean = svg.replace(/<\?xml[^?]*\?>/g, '').replace(/<!--[\s\S]*?-->/g, '');
  const ids = new Set(); const refs = []; const stack = [];
  let cursor = 0, roots = 0;
  for (const match of clean.matchAll(/<([^>]+)>/g)) {
    requireValue(!clean.slice(cursor, match.index).includes('<'), `${label}: malformed XML`);
    cursor = match.index + match[0].length;
    if (match[1].startsWith('/')) {
      requireValue(stack.pop() === match[1].slice(1).trim(), `${label}: unbalanced XML`); continue;
    }
    const token = /^([\w:-]+)([\s\S]*?)(\/?)$/.exec(match[1]);
    requireValue(token && passiveTags.has(token[1]), `${label}: unsupported SVG tag ${token?.[1]}`);
    if (stack.length === 0) { roots++; requireValue(token[1] === 'svg', `${label}: missing SVG root`); }
    const seen = new Set(); let end = 0;
    for (const attribute of token[2].matchAll(/([\w:-]+)\s*=\s*("[^"]*"|'[^']*')/g)) {
      requireValue(!token[2].slice(end, attribute.index).trim(), `${label}: malformed attribute`);
      end = attribute.index + attribute[0].length;
      const key = attribute[1], rawValue = attribute[2].slice(1, -1);
      requireValue(!/&(?!(?:amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);)/.test(rawValue), `${label}: invalid entity`);
      const value = rawValue.replace(/&(#x[0-9a-fA-F]+|#\d+|amp|lt|gt|quot|apos);/g, (_, entity) =>
        entity.startsWith('#') ? String.fromCodePoint(entity[1] === 'x' ? parseInt(entity.slice(2), 16) : Number(entity.slice(1)))
          : ({ amp:'&', lt:'<', gt:'>', quot:'"', apos:"'" })[entity]);
      requireValue(!seen.has(key), `${label}: duplicate attribute ${key}`); seen.add(key);
      requireValue(!/^on/i.test(key) && !['src','xml:base'].includes(key), `${label}: active attribute ${key}`);
      requireValue(!/[\\]|\/\*|\*\/|@|javascript:|data:|expression\s*\(/i.test(value), `${label}: unsafe attribute ${key}`);
      if (key === 'id') { requireValue(!ids.has(value), `${label}: duplicate id ${value}`); ids.add(value); }
      if (/^(?:xlink:)?href$/i.test(key)) { requireValue(/^#[\w.-]+$/.test(value), `${label}: external reference`); refs.push(value.slice(1)); }
      for (const paint of value.matchAll(/url\s*\(([^)]*)\)/gi)) {
        const ref = paint[1].trim().replace(/^['"]|['"]$/g, '');
        requireValue(/^#[\w.-]+$/.test(ref), `${label}: external paint reference`); refs.push(ref.slice(1));
      }
    }
    requireValue(!token[2].slice(end).trim(), `${label}: malformed attributes`);
    if (!token[3]) stack.push(token[1]);
  }
  requireValue(roots === 1 && stack.length === 0 && !clean.slice(cursor).trim(), `${label}: malformed SVG document`);
  for (const ref of refs) requireValue(ids.has(ref), `${label}: missing SVG reference ${ref}`);
  return ids;
}
export function validateMap(meta, markers, svg, mapId) {
  requireValue(meta.schemaVersion === 1 && markers.schemaVersion === 1, `${mapId}: unsupported schema`);
  requireValue(meta.mapId === mapId && markers.mapId === mapId, `${mapId}: map ID mismatch`);
  requireValue(finite(meta.size?.width) && meta.size.width > 0 && finite(meta.size?.height) && meta.size.height > 0, `${mapId}: invalid size`);
  const viewBox = /<svg\b[^>]*\bviewBox="([^"]+)"/.exec(svg)?.[1].trim().split(/[\s,]+/).map(Number);
  requireValue(viewBox?.length === 4 && viewBox[0] === 0 && viewBox[1] === 0
    && viewBox[2] === meta.size.width && viewBox[3] === meta.size.height, `${mapId}: SVG viewBox does not match metadata`);
  for (const key of ['zoom','minZoom','maxZoom']) requireValue(finite(meta[key]) && meta[key] > 0, `${mapId}: invalid ${key}`);
  requireValue(meta.minZoom <= meta.zoom && meta.zoom <= meta.maxZoom, `${mapId}: invalid zoom range`);
  for (const key of ['rotate','xOffset','yOffset','ratio']) requireValue(finite(meta.transform?.[key]), `${mapId}: invalid transform ${key}`);
  requireValue(meta.transform.ratio > 0 && ['invertX','invertY'].every(key => typeof meta.transform[key] === 'boolean'), `${mapId}: invalid transform`);
  const ids = validateSvg(svg, mapId), levels = new Set(), numbers = new Set();
  requireValue(Array.isArray(meta.levels) && meta.levels.length > 0, `${mapId}: no levels`);
  for (const level of meta.levels) {
    requireValue(typeof level.id === 'string' && !levels.has(level.id) && finite(level.sourceLevel) && !numbers.has(level.sourceLevel), `${mapId}: invalid or duplicate level`);
    requireValue(typeof level.defaultVisible === 'boolean' && typeof level.label === 'string', `${mapId}: invalid level metadata`);
    requireValue(level.terrainGroupId === null || ids.has(level.terrainGroupId), `${mapId}: missing terrain group ${level.terrainGroupId}`);
    // Optional site height ranges used to select the level from the screenshot height.
    const range = value => value === undefined || (Array.isArray(value) && (value.length === 0 || (value.length === 2 && value.every(finite))));
    requireValue(range(level.height), `${mapId}: invalid level height`);
    for (const zone of level.zones ?? []) {
      requireValue(range(zone.height) && (zone.rect === undefined || (zone.rect.length === 2 && zone.rect.flat().every(finite)))
        && (zone.poly === undefined || (zone.poly.length >= 3 && zone.poly.flat().every(finite))), `${mapId}: invalid level zone`);
    }
    levels.add(level.id); numbers.add(level.sourceLevel);
  }
  requireValue(meta.levels.filter(level => level.defaultVisible).length === 1, `${mapId}: invalid default level`);
  requireValue(markers.coordinateSpace === 'map' && Array.isArray(markers.markers), `${mapId}: invalid markers`);
  const factions = new Set(markers.factions?.map(faction => faction.id));
  requireValue(factions.size === 2 && factions.has('pmc') && factions.has('scav'), `${mapId}: invalid factions`);
  const types = new Set();
  for (const category of markers.categories ?? []) for (const subtype of category.subtypes ?? []) {
    const key = `${category.id}/${subtype.id}`;
    requireValue(!types.has(key) && subtype.factions?.length > 0 && subtype.factions.every(faction => factions.has(faction)), `${mapId}: invalid marker category`);
    types.add(key);
  }
  const markerIds = new Set();
  for (const marker of markers.markers) {
    requireValue(typeof marker.id === 'string' && marker.id && !markerIds.has(marker.id), `${mapId}: duplicate/invalid marker UID`);
    requireValue(types.has(`${marker.category}/${marker.subtype}`) && levels.has(marker.levelId), `${mapId}: invalid marker reference ${marker.id}`);
    requireValue(finite(marker.position?.x) && finite(marker.position?.y), `${mapId}: invalid marker position`);
    markerIds.add(marker.id);
  }
  if (markers.source?.listedCounts) {
    for (const category of markers.categories) for (const subtype of category.subtypes) {
      const listed = markers.source.listedCounts[category.id]?.[subtype.id];
      requireValue(Number.isInteger(listed) && listed >= 0
        && listed === markers.markers.filter(marker => marker.category === category.id && marker.subtype === subtype.id).length,
      `${mapId}: source listing differs for ${category.id}/${subtype.id}`);
    }
  }
}
/**
 * Check a whole resource directory: the configured map set, every map's files and nothing else.
 * Returns the manifest. Throws on the first problem so a partial bundle is never accepted.
 */
export async function checkResources(directory, options = {}) {
  const expectedMaps = options.expectedMaps ?? await configuredMaps();
  const manifest = JSON.parse(await readLocal(directory, 'manifest.json'));
  requireValue(manifest.schemaVersion === 1 && Array.isArray(manifest.maps), 'Unsupported manifest schema');
  const maps = manifest.maps;
  requireValue(maps.length && maps.every(map => typeof map === 'string' && /^[a-z0-9-]+$/.test(map)) && new Set(maps).size === maps.length, 'Invalid manifest maps');
  requireValue(JSON.stringify([...maps].sort()) === JSON.stringify([...expectedMaps].sort()), 'Resources do not match the maps in MapConfiguration.cs');
  const files = new Set(['manifest.json']);
  for (const mapId of maps) {
    const [svg, meta, markers] = await Promise.all(['map.svg','meta.json','markers.json'].map(async file => {
      const name = `maps/${mapId}/${file}`;
      files.add(name);
      return (await readLocal(directory, name)).toString('utf8');
    }));
    validateMap(JSON.parse(meta), JSON.parse(markers), svg, mapId);
  }
  // No unlisted payload is shipped accidentally. Source evidence belongs outside resources.
  async function walk(name = '') {
    for (const entry of await readdir(path.join(directory, name), { withFileTypes: true })) {
      const relative = name ? `${name}/${entry.name}` : entry.name;
      requireValue(!entry.isSymbolicLink(), `Symbolic link in resources: ${relative}`);
      if (entry.isDirectory()) await walk(relative);
      else requireValue(files.has(relative), `Unexpected resource file: ${relative}`);
    }
  }
  await walk();
  return manifest;
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const [command, directory = path.join(root, 'resources')] = process.argv.slice(2);
    requireValue(command === 'check', 'Usage: resource-bundle.mjs check [directory]');
    const manifest = await checkResources(path.resolve(directory));
    console.log(`check: ${manifest.maps.length} maps, collected ${manifest.collectedAt}`);
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
