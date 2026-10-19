export const FALLBACK_LANGUAGE = 'en';

export const MESSAGES = {
  en: {
    loading: 'Loading the map',
    failed: 'Could not open the map. {reason}',
    resourceUnreadable: 'Could not read {path} (HTTP {status})',
    terrainInvalid: 'Could not parse map.svg',
    mapMissing: 'The bundled resources have no {map} map',
  },
  ko: {
    loading: '지도를 불러오는 중입니다',
    failed: '지도를 열지 못했습니다. {reason}',
    resourceUnreadable: '{path}를 읽지 못했습니다 (HTTP {status})',
    terrainInvalid: 'map.svg를 해석하지 못했습니다',
    mapMissing: '리소스에 {map} 맵이 없습니다',
  },
  ja: {
    loading: 'マップを読み込んでいます',
    failed: 'マップを開けませんでした。{reason}',
    resourceUnreadable: '{path} を読み込めませんでした（HTTP {status}）',
    terrainInvalid: 'map.svg を解析できませんでした',
    mapMissing: 'リソースに {map} マップがありません',
  },
};

export function resolveLanguage(code) {
  return typeof code === 'string' && Object.hasOwn(MESSAGES, code) ? code : FALLBACK_LANGUAGE;
}

export function message(language, key, params = {}) {
  return MESSAGES[language][key].replace(/\{(\w+)\}/g,
    (placeholder, name) => (Object.hasOwn(params, name) ? String(params[name]) : placeholder));
}
