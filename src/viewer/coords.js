const ROUNDING = 1e4;
const NUMBER = '-?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:e[+-]?\\d+)?';

const SCREENSHOT_PATTERN = new RegExp(
  `\\d{4}-\\d{2}-\\d{2}\\[\\d{2}-\\d{2}\\]_(?<y>${NUMBER}),\\s*(?<z>${NUMBER}),\\s*(?<x>${NUMBER})` +
  `_(?<qx>${NUMBER}),\\s*(?<qy>${NUMBER}),\\s*(?<qz>${NUMBER}),\\s*(?<qw>${NUMBER})_`, 'i');

const round = (value) => Math.round(value * ROUNDING) / ROUNDING;
const normalizeDegrees = (value) => ((value % 360) + 360) % 360;

export function parseScreenshot(filename) {
  const match = SCREENSHOT_PATTERN.exec(String(filename));
  if (!match) return null;
  const value = Object.fromEntries(Object.entries(match.groups).map(([key, text]) => [key, Number(text)]));
  if (!Object.values(value).every(Number.isFinite)) return null;
  const directionX = 2 * (value.qx * value.qz + value.qw * value.qy);
  const directionZ = 1 - 2 * (value.qx * value.qx + value.qy * value.qy);
  if (Math.hypot(directionX, directionZ) === 0) return null;
  const look = normalizeDegrees(Math.atan2(directionX, directionZ) * 180 / Math.PI);
  return { x: value.x, y: value.y, z: value.z, look };
}

export function gamePositionToMapPosition(x, y, transform) {
  if (transform.rotate) {
    const radians = -transform.rotate * Math.PI / 180;
    const cos = Math.cos(radians), sin = Math.sin(radians);
    [x, y] = [round(x * cos - y * sin), round(x * sin + y * cos)];
  }
  return { x: round(transform.xOffset - x * transform.ratio), y: round(transform.yOffset - y * transform.ratio) };
}

export function gameDirectionToMapDirection(look, transform) {
  return normalizeDegrees(look + 270 - transform.rotate);
}

const inHeight = (range, height) => Array.isArray(range) && range.length === 2 && height >= range[0] && height < range[1];

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

export function levelAtPosition(levels, x, y, height) {
  const zoned = levels.find((level) => level.zones?.some((zone) => inZone(zone, x, y) === true && inHeight(zone.height, height)));
  if (zoned) return zoned;
  return levels.find((level) => level.zones?.some((zone) => inZone(zone, x, y) === null && inHeight(zone.height, height))
    || inHeight(level.height, height)) ?? null;
}
