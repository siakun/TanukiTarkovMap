/**
 * 좌표, 방향, 층 판정 식은 사이트(tarkov-market) 지도 번들의 식을 연산 그대로 옮긴다.
 * 같은 파일명이 Online과 Local에서 같은 자리, 같은 방향, 같은 층에 찍히도록 축 순서와 반올림까지 맞춘다.
 * Online은 이 식을 쓰지 않고 사이트가 직접 계산하므로, 검사 도구가 이 식으로 두 모드를 대조한다.
 */
const ROUNDING = 1e4;
const NUMBER = '-?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:e[+-]?\\d+)?';

// 사이트의 파일명 정규식은 좌표 세 수를 y, z, x 순서로 읽는다. z는 높이다.
// 게임은 쉼표 뒤에 공백 하나를 쓰지만 앱의 Online 브리지와 같은 범위를 받도록 공백 수는 따지지 않는다.
const SCREENSHOT_PATTERN = new RegExp(
  `\\d{4}-\\d{2}-\\d{2}\\[\\d{2}-\\d{2}\\]_(?<y>${NUMBER}),\\s*(?<z>${NUMBER}),\\s*(?<x>${NUMBER})` +
  `_(?<qx>${NUMBER}),\\s*(?<qy>${NUMBER}),\\s*(?<qz>${NUMBER}),\\s*(?<qw>${NUMBER})_`, 'i');

const round = (value) => Math.round(value * ROUNDING) / ROUNDING;
const normalizeDegrees = (value) => ((value % 360) + 360) % 360;

/** 파일명에서 게임 좌표(x, y는 평면, z는 높이)와 바라보는 방향(도)을 읽는다. 읽지 못하면 null */
export function parseScreenshot(filename) {
  const match = SCREENSHOT_PATTERN.exec(String(filename));
  if (!match) return null;
  const value = Object.fromEntries(Object.entries(match.groups).map(([key, text]) => [key, Number(text)]));
  if (!Object.values(value).every(Number.isFinite)) return null;
  // 사이트는 쿼터니언을 정규화하지 않고 수평 시선 벡터를 구한 뒤 (0, 1)과의 각을 잰다.
  const directionX = 2 * (value.qx * value.qz + value.qw * value.qy);
  const directionZ = 1 - 2 * (value.qx * value.qx + value.qy * value.qy);
  if (Math.hypot(directionX, directionZ) === 0) return null;
  const look = normalizeDegrees(Math.atan2(directionX, directionZ) * 180 / Math.PI);
  return { x: value.x, y: value.y, z: value.z, look };
}

/** 게임 평면 좌표를 지도 좌표로 바꾼다. 사이트의 gamePosToMapPos와 같은 식이다 */
export function gamePositionToMapPosition(x, y, transform) {
  if (transform.rotate) {
    const radians = -transform.rotate * Math.PI / 180;
    const cos = Math.cos(radians), sin = Math.sin(radians);
    [x, y] = [round(x * cos - y * sin), round(x * sin + y * cos)];
  }
  return { x: round(transform.xOffset - x * transform.ratio), y: round(transform.yOffset - y * transform.ratio) };
}

/** 게임 방향을 화면 회전각으로 바꾼다. 사이트가 방향 마커에 쓰는 식이다(위가 0도, 시계 방향) */
export function gameDirectionToMapDirection(look, transform) {
  return normalizeDegrees(look + 270 - transform.rotate);
}

const inHeight = (range, height) => Array.isArray(range) && range.length === 2 && height >= range[0] && height < range[1];

// 구역 안이면 true, 밖이면 false, 모양이 없는 구역(높이만 가진 구역)이면 null
function inZone(zone, x, y) {
  if (zone.rect?.length === 2) {
    let [[left, top], [right, bottom]] = zone.rect;
    if (zone.rotate) {
      const radians = -zone.rotate * Math.PI / 180;
      const rotate = ([px, py]) => [px * Math.cos(radians) - py * Math.sin(radians), px * Math.sin(radians) + py * Math.cos(radians)];
      [x, y] = rotate([x, y]);
      [left, top] = rotate([left, top]);
      [right, bottom] = rotate([right, bottom]);
    }
    return x >= Math.min(left, right) && x <= Math.max(left, right) && y >= Math.min(top, bottom) && y <= Math.max(top, bottom);
  }
  if (zone.poly?.length >= 3) {
    let inside = false;
    for (let i = 0, j = zone.poly.length - 1; i < zone.poly.length; j = i++) {
      const [xi, yi] = zone.poly[i], [xj, yj] = zone.poly[j];
      if ((yi > y) !== (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
    }
    return inside;
  }
  return null;
}

/**
 * 게임 위치(평면 x, y와 높이)가 속한 층을 고른다. 사이트의 층 자동 선택과 같은 규칙이다. 구역(zones) 안이고
 * 그 구역의 높이가 맞는 층을 먼저 찾고, 없으면 모양 없는 구역의 높이나 층의 높이 범위(height)로 찾는다.
 * levels는 위층부터 놓인 { height, zones }를 가진 층 목록이고, 찾은 층 객체를 돌려준다. 없으면 null
 */
export function levelAtPosition(levels, x, y, height) {
  const zoned = levels.find((level) => level.zones?.some((zone) => inZone(zone, x, y) === true && inHeight(zone.height, height)));
  if (zoned) return zoned;
  return levels.find((level) => level.zones?.some((zone) => inZone(zone, x, y) === null && inHeight(zone.height, height))
    || inHeight(level.height, height)) ?? null;
}
