/**
 * 좌표 변환은 archive의 지도 bundle에 있던 식을 이름이 아니라 연산 규칙으로 옮긴다.
 * 근거 blob과 당시 수식은 각 맵의 meta.json source.coordinateFormula에 함께 기록한다.
 */
const ROUNDING_FACTOR = 10_000;

export function roundCoordinate(value) {
  return Math.round(value * ROUNDING_FACTOR) / ROUNDING_FACTOR;
}

function assertNumber(value, name) {
  if (!Number.isFinite(value)) throw new TypeError(`${name}은 유한한 숫자여야 합니다.`);
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
