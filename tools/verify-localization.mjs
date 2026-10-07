#!/usr/bin/env node
/**
 * INTENT
 * 화면 문구의 번역이 언어마다 빠지거나 어긋나지 않았는지 검사한다. 빌드는 resx 키 오타를 C#에서만 잡고,
 * XAML의 {loc:Text 키} 오타와 번역 파일 사이의 차이는 잡지 못한다. 번역이 빠진 키는 조용히 영어로 보이고,
 * 자리 표시자가 어긋난 번역은 그 언어로 실행했을 때만 string.Format이 예외를 던진다.
 * - 앱 문구: Localization/Strings.resx(기본 언어)와 Strings.<언어>.resx의 키가 같은지, 키마다 {0} 같은 자리 표시자
 *   번호가 같은지, 중괄호가 .NET 서식 문자열로 올바른지, 빈 번역이 없는지
 * - 지원 언어: AppLanguage.Supported 목록이 번역 파일의 언어와 같은지
 * - XAML: {loc:Text 키}의 키가 Strings.resx에 있는지, 주석 밖에 한글 문구가 직접 남지 않았는지
 * - Local 미니맵: viewer/i18n.js의 언어가 앱 언어와 같고, 언어마다 키와 {이름} 자리 표시자가 같은지
 * 기본 resx의 언어는 csproj의 NeutralLanguage에서 읽는다. 앱을 빌드하거나 실행하지 않는다.
 * Usage: node tools/verify-localization.mjs
 */
import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const project = path.join(repository, 'src/TanukiTarkovMap');
const localization = path.join(project, 'Localization');

let passed = 0, failed = 0;
async function check(name, run) {
  try { await run(); passed++; console.log(`PASS ${name}`); }
  catch (error) { failed++; console.error(`FAIL ${name}: ${error.message}`); }
}

const decodeXml = (text) => text.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"')
  .replace(/&apos;/g, "'").replace(/&amp;/g, '&');

async function readResx(file) {
  const entries = new Map();
  for (const match of (await readFile(file, 'utf8')).matchAll(/<data\s+name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) {
    if (entries.has(match[1])) throw new Error(`${path.basename(file)}: duplicate key ${match[1]}`);
    entries.set(match[1], decodeXml(match[2]));
  }
  return entries;
}

// .NET 서식 문자열의 자리 표시자 번호 목록. {{와 }}는 글자 그대로의 중괄호다. 짝이 맞지 않는 중괄호가 있으면 null
function formatIndexes(text) {
  const indexes = new Set();
  const rest = text.replace(/\{\{|\}\}/g, '')
    .replace(/\{(\d+)(?:,-?\d+)?(?::[^{}]*)?\}/g, (_, index) => { indexes.add(Number(index)); return ''; });
  return /[{}]/.test(rest) ? null : [...indexes].sort((a, b) => a - b).join(',');
}

const namedPlaceholders = (text) => [...new Set([...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]))].sort().join(',');

const sameKeys = (expected, actual) => {
  const missing = [...expected].filter((key) => !actual.has(key));
  const extra = [...actual.keys()].filter((key) => !expected.has(key));
  return missing.length || extra.length ? `missing [${missing.join(', ')}] extra [${extra.join(', ')}]` : null;
};

async function filesUnder(folder, extension) {
  const found = [];
  for (const entry of await readdir(folder, { withFileTypes: true })) {
    if (entry.isDirectory() && !['bin', 'obj'].includes(entry.name)) found.push(...await filesUnder(path.join(folder, entry.name), extension));
    else if (entry.isFile() && entry.name.endsWith(extension)) found.push(path.join(folder, entry.name));
  }
  return found;
}

const csproj = await readFile(path.join(project, 'TanukiTarkovMap.csproj'), 'utf8');
const neutralLanguage = csproj.match(/<NeutralLanguage>([^<]+)<\/NeutralLanguage>/)?.[1];
const resx = new Map();
for (const file of (await readdir(localization)).filter((name) => /^Strings(\.[A-Za-z-]+)?\.resx$/.test(name)).sort()) {
  resx.set(file.match(/^Strings(?:\.([A-Za-z-]+))?\.resx$/)[1] ?? neutralLanguage, await readResx(path.join(localization, file)));
}
const neutral = resx.get(neutralLanguage);
const languages = [...resx.keys()].sort();

await check('default Strings.resx language comes from NeutralLanguage', async () => {
  assert.ok(neutralLanguage, 'NeutralLanguage is missing in TanukiTarkovMap.csproj');
  assert.ok(neutral?.size > 0, `Strings.resx has no texts (${neutralLanguage})`);
});

await check('every language has the same keys', async () => {
  for (const [language, entries] of resx) {
    const difference = sameKeys(new Set(neutral.keys()), entries);
    assert.equal(difference, null, `${language}: ${difference}`);
  }
});

await check('placeholders and braces match in every language', async () => {
  for (const [key, text] of neutral) {
    const expected = formatIndexes(text);
    assert.notEqual(expected, null, `${neutralLanguage} ${key}: invalid braces in "${text}"`);
    for (const [language, entries] of resx) {
      const actual = entries.has(key) ? formatIndexes(entries.get(key)) : expected;
      assert.equal(actual, expected, `${language} ${key}: placeholders {${actual}} != {${expected}} in "${entries.get(key)}"`);
    }
  }
});

await check('no empty translations', async () => {
  for (const [language, entries] of resx)
    for (const [key, text] of entries) assert.ok(text.trim().length > 0, `${language} ${key} is empty`);
});

await check('AppLanguage.Supported lists exactly the translated languages', async () => {
  const source = await readFile(path.join(localization, 'AppLanguage.cs'), 'utf8');
  const list = source.match(/Supported\s*\{\s*get;\s*\}\s*=\s*\[([^\]]*)\]/)?.[1];
  assert.ok(list, 'AppLanguage.Supported initializer not found (update this check if its shape changed)');
  const supported = [...list.matchAll(/"([^"]+)"/g)].map((match) => match[1]).sort();
  assert.deepEqual(supported, languages);
});

await check('XAML uses only existing keys and no hard-coded Korean text', async () => {
  const problems = [];
  for (const file of await filesUnder(project, '.xaml')) {
    const xaml = (await readFile(file, 'utf8')).replace(/<!--[\s\S]*?-->/g, '');
    const name = path.relative(repository, file);
    for (const match of xaml.matchAll(/\{loc:Text\s+([^}\s]+)\s*\}/g))
      if (!neutral.has(match[1])) problems.push(`${name}: unknown key ${match[1]}`);
    for (const line of xaml.split(/\r?\n/))
      if (/[가-힣]/.test(line)) problems.push(`${name}: Korean text outside resx: ${line.trim()}`);
  }
  assert.deepEqual(problems, []);
});

await check('Local minimap notices match the app languages', async () => {
  const { MESSAGES, FALLBACK_LANGUAGE } = await import(pathToFileURL(path.join(repository, 'viewer/i18n.js')));
  assert.deepEqual(Object.keys(MESSAGES).sort(), languages);
  assert.equal(FALLBACK_LANGUAGE, neutralLanguage);
  const base = MESSAGES[FALLBACK_LANGUAGE];
  for (const [language, texts] of Object.entries(MESSAGES)) {
    const difference = sameKeys(new Set(Object.keys(base)), new Map(Object.entries(texts)));
    assert.equal(difference, null, `${language}: ${difference}`);
    for (const [key, text] of Object.entries(texts)) {
      assert.ok(text.trim().length > 0, `${language} ${key} is empty`);
      assert.equal(namedPlaceholders(text), namedPlaceholders(base[key]), `${language} ${key}: placeholders differ`);
    }
  }
});

console.log(`${passed} passed, ${failed} failed`);
process.exitCode = failed ? 1 : 0;
