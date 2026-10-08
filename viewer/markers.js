/**
 * 추출구 마커와 내 위치 마커.
 *
 * 추출구는 사이트 지도 페이지의 마커 캔버스(markers-canvas) 그리기 규칙을 옮긴다. 아이콘 경로와
 * 크기, 진영별 색, 세 겹 그림자, 배율에 따른 축소, 항상 보이는 라벨의 글꼴과 반투명 바탕, 라벨이
 * 겹치면 반대편으로 옮기고 그래도 겹치면 생략하는 규칙, 다른 층 표시가 모두 사이트의 값이다.
 * 사이트처럼 캔버스에 그리는 이유는 라벨 겹침을 화면 좌표에서 판정해야 하기 때문이다.
 *
 * 내 위치는 사이트의 .marker 요소(보라 원, 초록 테두리)에 앱의 방향 삼각형을 붙인 모양이다.
 * Online의 map-markers.js가 사이트 지도 위에 그리는 내 위치도 같은 크기, 색, 삼각형 위치, 핑을 쓴다.
 * 모양을 바꾸면 두 곳을 함께 바꾼다.
 */

// 사이트 아이콘 표의 Extractions 항목: 달리는 사람 아이콘, 기본 크기 25px, 외곽선 굵기 20
const EXTRACTION_ICON = {
  path: 'm350.111,262.315l-40.726.326c-4.817.038-9.609-.7-14.191-2.186-6.575-2.133-12.567-5.756-17.51-10.588l-32.345-31.618c-.436-.426-1.148-.367-1.507.126l-54.254,74.432c-.231.317-.256.74-.063,1.081l100.682,178.383c.342.627-1.67,4.451-2.381,4.524l-9.188.942c-4.723.484-9.494.096-14.075-1.147-7.561-2.051-14.322-6.349-19.387-12.325l-95.282-112.424-12.502,28.231c-1.105,2.496-2.612,4.794-4.46,6.803l-.295.321c-4.734,5.146-11.406,8.074-18.398,8.074l-104.229.457v-5.592c0-8.486,3.036-16.693,8.559-23.136,6.755-7.88,16.615-12.415,26.993-12.415l45.199-.457c2.256,0,4.233-1.51,4.825-3.687l18.654-68.542,46.423-99.606c.309-.663-.175-1.422-.906-1.422h-40.653c-1.865,0-3.575,1.038-4.436,2.692l-35.441,68.125c-2.685,5.162-7.783,8.631-13.571,9.234-7.539.785-14.713-3.422-17.707-10.385l-.051-.118c-2.038-4.737-1.858-10.135.489-14.727l43.323-84.548c3.447-6.728,10.37-10.959,17.929-10.959h75.839c11.245,0,22.407,1.92,33.005,5.676,13.248,4.696,25.341,12.163,35.472,21.905l49.272,47.377c4.267,4.103,10.003,6.321,15.92,6.157l30.864.028.11,40.988Zm-97.165-142.289c25.412,0,46.013-20.601,46.013-46.013s-20.601-46.013-46.013-46.013-46.013,20.601-46.013,46.013,20.601,46.013,46.013,46.013Z',
  defaultSize: 25,
  strokeWidth: 20,
};

// 사이트 마커 색 표: PMC와 Transit은 초록, Scav는 회색, Co-Op을 비롯한 나머지는 황토색. 외곽선은 검정
const SUBTYPE_COLORS = { pmc: '#70a800', transit: '#70a800', scav: '#aeaeb0' };
const DEFAULT_COLOR = '#9a8866';
const STROKE_COLOR = '#000';

// 사이트 마커 캔버스의 상수
const ICON_BOX = 48;              // 아이콘을 그리는 칸
const ICON_PADDING = 8;           // 그림자가 들어갈 여백
const ICON_SHADOWS = [{ dx: 1, dy: 2, alpha: 0.2 }, { dx: 2, dy: 4, alpha: 0.25 }, { dx: 3, dy: 5, alpha: 0.15 }];
const LABEL_SIZE = 20;            // 항상 보이는 라벨의 글자 크기
const LABEL_MIN_SIZE = 13;        // 축소해도 이보다 작게 쓰지 않는다
const LABEL_PADDING_X = 5;
const LABEL_PADDING_Y = 3;
const LABEL_GAP = 6;              // 아이콘 가장자리와 라벨 사이
const LABEL_BACKGROUND = 'hsla(0, 0%, 0%, 50%)';
const LEVEL_BADGE_SIZE = 14;
const LEVEL_BADGE_BACKGROUND = 'hsla(0, 0%, 0%, 80%)';
const MARKER_MIN_SCALE = 0.5;     // 기본 배율보다 축소하면 마커도 줄이되 절반까지만
const VIEW_MARGIN = 100;          // 화면 밖 이만큼(지도 좌표)까지는 그린다
const FONT_FAMILY = 'bender, pretendard, "Source Sans Pro", Arial, sans-serif';

// 사이트처럼 아이콘 경로의 실제 범위로 비율을 정한다. 경로만 재면 되므로 화면 밖 SVG에서 한 번 잰다
function measurePath(path, strokeWidth) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.style.position = 'absolute';
  svg.style.visibility = 'hidden';
  svg.style.width = '0';
  svg.style.height = '0';
  const element = document.createElementNS('http://www.w3.org/2000/svg', 'path');
  element.setAttribute('d', path);
  svg.append(element);
  document.body.append(svg);
  const box = element.getBBox();
  svg.remove();
  return { width: Math.ceil(box.x + box.width + strokeWidth), height: Math.ceil(box.y + box.height + strokeWidth) };
}

function createIconCache() {
  const cache = new Map();
  const shape = new Path2D(EXTRACTION_ICON.path);
  const extent = measurePath(EXTRACTION_ICON.path, EXTRACTION_ICON.strokeWidth);
  return (fill, pixelRatio) => {
    const key = `${fill}@${pixelRatio}`;
    if (cache.has(key)) return cache.get(key);
    const full = ICON_BOX + ICON_PADDING * 2;
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = Math.ceil(full * pixelRatio);
    const context = canvas.getContext('2d');
    context.scale(pixelRatio, pixelRatio);
    const aspect = extent.width / extent.height;
    const width = aspect >= 1 ? ICON_BOX : ICON_BOX * aspect;
    const height = aspect >= 1 ? ICON_BOX / aspect : ICON_BOX;
    const scaleX = width / extent.width, scaleY = height / extent.height;
    const left = ICON_PADDING + (ICON_BOX - width) / 2, top = ICON_PADDING + (ICON_BOX - height) / 2;
    for (const shadow of ICON_SHADOWS) {
      context.save();
      context.translate(left + shadow.dx, top + shadow.dy);
      context.scale(scaleX, scaleY);
      context.globalAlpha = shadow.alpha;
      context.fillStyle = '#000';
      context.fill(shape);
      context.restore();
    }
    context.save();
    context.translate(left, top);
    context.scale(scaleX, scaleY);
    context.fillStyle = fill;
    context.fill(shape);
    if (EXTRACTION_ICON.strokeWidth * Math.min(scaleX, scaleY) > 0.5) {
      context.lineWidth = EXTRACTION_ICON.strokeWidth * 1.5;
      context.strokeStyle = STROKE_COLOR;
      context.stroke(shape);
    }
    context.restore();
    const icon = { canvas, size: full };
    cache.set(key, icon);
    return icon;
  };
}

/**
 * markers.json의 추출구를 캔버스에 그린다.
 * levelNumber(levelId)는 다른 층 표시에 쓸 층 번호를 돌려준다. 단층 맵이면 null이다.
 */
export function createExtractionLayer({ canvas, markerData, baseZoom, levelNumber, camera }) {
  const context = canvas.getContext('2d');
  const iconFor = createIconCache();
  const subtypes = new Map(markerData.categories.flatMap((category) =>
    category.subtypes.map((subtype) => [`${category.id}/${subtype.id}`, subtype])));
  const markers = markerData.markers.map((marker) => {
    const subtype = subtypes.get(`${marker.category}/${marker.subtype}`);
    return { ...marker, factions: subtype?.factions ?? [], color: SUBTYPE_COLORS[marker.subtype] ?? DEFAULT_COLOR,
      label: marker.name.trim() || subtype?.label || '' };
  });
  let faction = 'pmc';
  let selectedLevel = null;
  let pixelRatio = 1;
  let dirty = true;

  // 사이트는 기본 배율보다 축소하면 마커를 제곱근 비율로 줄인다
  const markerScale = () => camera.view.zoom >= baseZoom ? 1
    : Math.max(MARKER_MIN_SCALE, Math.sqrt(camera.view.zoom / baseZoom));

  function resize() {
    const { width, height } = camera.viewSize();
    pixelRatio = window.devicePixelRatio || 1;
    canvas.style.width = `${width}px`;
    canvas.style.height = `${height}px`;
    canvas.width = Math.round(width * pixelRatio);
    canvas.height = Math.round(height * pixelRatio);
  }

  function inView(point, width, height) {
    const margin = VIEW_MARGIN * camera.view.zoom;
    return point.x >= -margin && point.y >= -margin && point.x <= width + margin && point.y <= height + margin;
  }

  // 사이트의 다른 층 표시: 아이콘 오른쪽 아래에 그 마커의 층 번호를 적는다
  function drawLevelBadge(marker, point, scale) {
    const size = EXTRACTION_ICON.defaultSize * scale;
    const fontSize = Math.max(10, LEVEL_BADGE_SIZE * scale);
    const text = String(levelNumber(marker.levelId));
    context.save();
    context.font = `bold ${fontSize}px ${FONT_FAMILY}`;
    context.textBaseline = 'middle';
    context.textAlign = 'center';
    const width = Math.max(fontSize + 2, context.measureText(text).width + 6), height = fontSize + 2;
    const left = point.x + size / 2 - width / 2 - 4 * scale, top = point.y + size / 2 - height / 2 - 4 * scale;
    context.fillStyle = LEVEL_BADGE_BACKGROUND;
    context.fillRect(left, top, width, height);
    context.fillStyle = marker.color;
    context.fillText(text, left + width / 2, top + height / 2);
    context.restore();
  }

  function draw() {
    if (canvas.width !== Math.round(camera.viewSize().width * (window.devicePixelRatio || 1))
      || canvas.height !== Math.round(camera.viewSize().height * (window.devicePixelRatio || 1))) resize();
    const { width, height } = camera.viewSize();
    context.setTransform(1, 0, 0, 1, 0, 0);
    context.clearRect(0, 0, canvas.width, canvas.height);
    context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
    const scale = markerScale();
    const visible = [];
    for (const marker of markers) {
      if (!marker.factions.includes(faction)) continue;
      const point = camera.toScreen(marker.position);
      if (!inView(point, width, height)) continue;
      const otherLevel = selectedLevel !== null && levelNumber(marker.levelId) !== null && marker.levelId !== selectedLevel;
      visible.push({ marker, point, otherLevel });
      const icon = iconFor(marker.color, pixelRatio);
      const drawn = icon.size * EXTRACTION_ICON.defaultSize * scale / ICON_BOX;
      context.drawImage(icon.canvas, point.x - drawn / 2, point.y - drawn / 2, drawn, drawn);
      // 사이트는 추출구를 모든 층에 표시하고 다른 층의 추출구에 층 번호를 붙인다
      if (otherLevel) drawLevelBadge(marker, point, scale);
    }

    // 라벨은 현재 층을 먼저 놓고, 아이콘 자리와 먼저 놓은 라벨을 피한다
    const ordered = [...visible.filter((entry) => !entry.otherLevel), ...visible.filter((entry) => entry.otherLevel)];
    const taken = ordered.map(({ point }) => {
      const size = EXTRACTION_ICON.defaultSize * scale;
      return [point.x - size / 2, point.y - size / 2, size, size];
    });
    const overlaps = (box) => taken.some((other) => box[0] < other[0] + other[2] && box[0] + box[2] > other[0]
      && box[1] < other[1] + other[3] && box[1] + box[3] > other[1]);
    const fontSize = Math.max(LABEL_MIN_SIZE, LABEL_SIZE * scale);
    context.save();
    context.font = `bold ${fontSize}px ${FONT_FAMILY}`;
    context.textBaseline = 'middle';
    for (const { marker, point } of ordered) {
      if (!marker.label) continue;
      const textWidth = context.measureText(marker.label).width;
      const offset = EXTRACTION_ICON.defaultSize * scale / 2 + LABEL_GAP;
      const top = point.y - fontSize / 2 - LABEL_PADDING_Y;
      const boxWidth = textWidth + LABEL_PADDING_X * 2, boxHeight = fontSize + LABEL_PADDING_Y * 2;
      let textX = point.x + offset;
      let box = [textX - LABEL_PADDING_X, top, boxWidth, boxHeight];
      if (overlaps(box)) {
        textX = point.x - offset - textWidth;
        box = [textX - LABEL_PADDING_X, top, boxWidth, boxHeight];
        if (overlaps(box)) continue;
      }
      taken.push(box);
      context.fillStyle = LABEL_BACKGROUND;
      context.fillRect(box[0], box[1], box[2], box[3]);
      context.fillStyle = marker.color;
      context.fillText(marker.label, textX, point.y);
    }
    context.restore();
  }

  function scheduleDraw() {
    if (dirty) return;
    dirty = true;
    requestAnimationFrame(() => { dirty = false; draw(); });
  }

  resize();
  dirty = false;
  draw();
  return {
    redraw: scheduleDraw,
    setFaction(value) { faction = value; scheduleDraw(); },
    setLevel(levelId) { selectedLevel = levelId; scheduleDraw(); },
  };
}

// Online의 map-markers.js와 같은 삼각형 그림이다. 원 앞쪽에 붙어 바라보는 방향으로 돈다.
const TRIANGLE_MARKUP = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">'
  + '<path d="M50,5 L85,75 Q50,45 15,75 Z" fill="#8a2be2" stroke="#70a800" stroke-width="2"/></svg>';

/** 사이트의 .marker 요소와 앱의 방향 삼각형으로 내 위치를 표시한다 */
export function createPlayerMarker({ element, camera }) {
  const triangle = document.createElement('div');
  triangle.className = 'triangle-indicator';
  triangle.style.backgroundImage = `url("data:image/svg+xml;utf8,${encodeURIComponent(TRIANGLE_MARKUP)}")`;
  element.append(triangle);
  let position = null;

  function place() {
    if (!position) return;
    const point = camera.toScreen(position);
    element.style.left = `${point.x}px`;
    element.style.top = `${point.y}px`;
  }

  return {
    place,
    show(mapPosition, direction) {
      position = mapPosition;
      element.hidden = false;
      place();
      triangle.hidden = direction === null;
      if (direction !== null) triangle.style.setProperty('--tanuki-direction', `${direction}deg`);
      // 사이트처럼 새 위치를 받으면 한 번 크게 깜빡인다
      element.classList.remove('ping');
      void element.offsetWidth;
      element.classList.add('ping');
    },
    isVisible() {
      if (element.hidden || !position) return false;
      const box = element.getBoundingClientRect();
      return box.width > 0 && box.height > 0 && element.checkVisibility();
    },
  };
}
