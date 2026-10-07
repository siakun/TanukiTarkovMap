/**
 * 좌표와 방향 식은 사이트(tarkov-market) 지도 번들의 식을 연산 그대로 옮긴다.
 * 같은 파일명이 Online과 Local에서 같은 자리, 같은 방향에 찍히도록 축 순서와 반올림까지 맞춘다.
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
