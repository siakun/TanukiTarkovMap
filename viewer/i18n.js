/**
 * Local 미니맵이 직접 그리는 안내 문구(로딩과 오류)의 번역.
 * 앱이 주소의 ?lang=<코드>로 첫 화면의 언어를 넘기고, 화면 언어를 바꾸면 window.tanukiViewer.setLanguage(코드)를
 * 부른다. 모르는 언어는 영어로 표시한다. Levels 패널 같은 조작 UI는 사이트 화면을 그대로 옮긴 것이고 층 이름도
 * 사이트 데이터라 사이트처럼 영어로 둔다.
 * 언어 목록은 앱의 화면 언어(Strings.resx와 그 번역 파일)와 같아야 하고, 언어마다 키와 {이름} 자리 표시자가
 * 같아야 한다. tools/verify-localization.mjs가 확인한다.
 */
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

/** 번역이 있는 언어 코드면 그대로, 아니면 영어 */
export function resolveLanguage(code) {
  return typeof code === 'string' && Object.hasOwn(MESSAGES, code) ? code : FALLBACK_LANGUAGE;
}

/** 그 언어의 문구를 만든다. {이름} 자리는 params의 같은 이름 값으로 채운다 */
export function message(language, key, params = {}) {
  return MESSAGES[language][key].replace(/\{(\w+)\}/g,
    (placeholder, name) => (Object.hasOwn(params, name) ? String(params[name]) : placeholder));
}
