/**
 * 좌표 변환은 archive의 지도 bundle에 있던 식을 이름이 아니라 연산 규칙으로 옮긴다.
 * 근거 blob과 당시 수식은 각 맵의 meta.json source.coordinateFormula에 함께 기록한다.
 */
const ROUNDING_FACTOR = 10_000;
const FULL_CIRCLE = 360;
const SCREENSHOT_NUMBER = '[+-]?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:[eE][+-]?\\d+)?';

// 2026-08-19 archive의 gU/MU 파서는 파일명 좌표를 Y, Z, X 순서로 읽어 게임의 X, Y, Z에
// 다시 배치한다. 앱이 사이트와 로컬 뷰어에 같은 파일명을 넘길 수 있도록 이 순서를 유지한다.
const SCREENSHOT_PATTERN = new RegExp(
  `(?:^|[\\\\/])\\d{4}-\\d{2}-\\d{2}\\[\\d{2}-\\d{2}\\]_` +
    `(?<fileY>${SCREENSHOT_NUMBER}),\\s*(?<fileZ>${SCREENSHOT_NUMBER}),\\s*` +
    `(?<fileX>${SCREENSHOT_NUMBER})_(?<quaternionX>${SCREENSHOT_NUMBER}),\\s*` +
    `(?<quaternionY>${SCREENSHOT_NUMBER}),\\s*(?<quaternionZ>${SCREENSHOT_NUMBER}),\\s*` +
    `(?<quaternionW>${SCREENSHOT_NUMBER})_[^\\\\/]*\\.png$`,
  'i'
);

export function roundCoordinate(value) {
  return Math.round(value * ROUNDING_FACTOR) / ROUNDING_FACTOR;
}

function assertNumber(value, name) {
  if (!Number.isFinite(value)) throw new TypeError(`${name}은 유한한 숫자여야 합니다.`);
}

function normalizeDegrees(value) {
  const normalized = value % FULL_CIRCLE;
  return normalized < 0 ? normalized + FULL_CIRCLE : normalized;
}

function assertTransform(transform) {
  if (!transform || typeof transform !== 'object') {
    throw new TypeError('좌표 변환 설정이 없습니다.');
  }

  for (const name of ['rotate', 'xOffset', 'yOffset', 'ratio']) {
    assertNumber(transform[name], `transform.${name}`);
  }
  if (transform.ratio === 0) throw new RangeError('transform.ratio는 0일 수 없습니다.');
}

function rotatePosition(x, y, degrees) {
  const radians = -degrees * (Math.PI / 180);
  const cosine = Math.cos(radians);
  const sine = Math.sin(radians);
  return {
    x: roundCoordinate(x * cosine - y * sine),
    y: roundCoordinate(x * sine + y * cosine),
  };
}

export function gamePositionToMapPosition(gameX, gameY, transform) {
  assertNumber(gameX, 'gameX');
  assertNumber(gameY, 'gameY');
  assertTransform(transform);

  const rotated = transform.rotate
    ? rotatePosition(gameX, gameY, transform.rotate)
    : { x: gameX, y: gameY };

  // 2026-08-19 archive의 실제 bundle은 invertX와 invertY를 읽지 않는다.
  // 두 필드는 원본 설정을 잃지 않으려고 meta에 보존하되, 현재 사이트와 같은 식만 적용한다.
  return {
    x: roundCoordinate(transform.xOffset - rotated.x * transform.ratio),
    y: roundCoordinate(transform.yOffset - rotated.y * transform.ratio),
  };
}

export function quaternionToGameDirection(quaternion) {
  if (!quaternion || typeof quaternion !== 'object') {
    throw new TypeError('quaternion이 없습니다.');
  }

  const { x, y, z, w } = quaternion;
  for (const [name, value] of Object.entries({ x, y, z, w })) {
    assertNumber(value, `quaternion.${name}`);
  }

  // 2026-08-19 archive의 wN/LN 식을 그대로 드러낸다. atan2로 줄일 수 있지만, 사이트와
  // 대조할 핵심 수식이 보이게 두어 번들이 바뀌었을 때 같은 단위로 다시 검증한다.
  const directionX = 2 * (x * z + w * y);
  const directionZ = 1 - 2 * (x * x + y * y);
  const magnitude = Math.hypot(directionX, directionZ);
  if (magnitude === 0) throw new RangeError('quaternion에서 수평 시선 방향을 구할 수 없습니다.');

  const cosine = Math.min(1, Math.max(-1, directionZ / magnitude));
  const unsignedDegrees = Math.acos(cosine) * (180 / Math.PI);
  return normalizeDegrees(directionX < 0 ? FULL_CIRCLE - unsignedDegrees : unsignedDegrees);
}

export function gameDirectionToMapDirection(gameDirection, transform) {
  assertNumber(gameDirection, 'gameDirection');
  assertTransform(transform);

  // 2026-08-19 archive의 xa 식: 화면각 = 게임각 + (270 - transform.rotate).
  return normalizeDegrees(gameDirection + (270 - transform.rotate));
}

export function parseScreenshotPosition(filename) {
  if (typeof filename !== 'string' || filename.length === 0) {
    throw new TypeError('스크린샷 파일명이 없습니다.');
  }

  const match = SCREENSHOT_PATTERN.exec(filename);
  if (!match?.groups) {
    throw new Error('스크린샷 파일명에서 게임 좌표와 시선 방향을 찾지 못했습니다.');
  }

  const quaternion = {
    x: Number(match.groups.quaternionX),
    y: Number(match.groups.quaternionY),
    z: Number(match.groups.quaternionZ),
    w: Number(match.groups.quaternionW),
  };
  return {
    x: Number(match.groups.fileX),
    y: Number(match.groups.fileY),
    z: Number(match.groups.fileZ),
    look: quaternionToGameDirection(quaternion),
    quaternion,
  };
}

export function mapPositionToGamePosition(mapX, mapY, transform) {
  assertNumber(mapX, 'mapX');
  assertNumber(mapY, 'mapY');
  assertTransform(transform);

  const transformed = {
    x: roundCoordinate((transform.xOffset - mapX) / transform.ratio),
    y: roundCoordinate((transform.yOffset - mapY) / transform.ratio),
  };
  return transform.rotate
    ? rotatePosition(transformed.x, transformed.y, -transform.rotate)
    : transformed;
}

export function mapPositionToScreenPosition(mapX, mapY, view) {
  assertNumber(mapX, 'mapX');
  assertNumber(mapY, 'mapY');
  assertNumber(view.x, 'view.x');
  assertNumber(view.y, 'view.y');
  assertNumber(view.zoom, 'view.zoom');
  return {
    x: roundCoordinate(view.x + mapX * view.zoom),
    y: roundCoordinate(view.y + mapY * view.zoom),
  };
}

export function screenPositionToMapPosition(screenX, screenY, view) {
  assertNumber(screenX, 'screenX');
  assertNumber(screenY, 'screenY');
  assertNumber(view.x, 'view.x');
  assertNumber(view.y, 'view.y');
  assertNumber(view.zoom, 'view.zoom');
  if (view.zoom === 0) throw new RangeError('view.zoom은 0일 수 없습니다.');
  return {
    x: roundCoordinate((screenX - view.x) / view.zoom),
    y: roundCoordinate((screenY - view.y) / view.zoom),
  };
}
