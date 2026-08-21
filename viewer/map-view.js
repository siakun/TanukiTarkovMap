import {
  mapPositionToScreenPosition,
  screenPositionToMapPosition,
} from './coords.js';

/**
 * 맵 뷰는 외부 라이브러리 없이 SVG 한 장과 그 위의 마커 층을 같은 좌표계로 움직인다.
 * 배율과 이동값은 한 상태에서만 관리해 휠, 포인터, 키보드 조작이 서로 덮어쓰지 않게 한다.
 */
const WHEEL_ZOOM_FACTOR = 1.2;
const BUTTON_ZOOM_FACTOR = 1.25;

function clamp(value, minimum, maximum) {
  return Math.min(maximum, Math.max(minimum, value));
}

function assertPassiveSvg(svg) {
  if (svg.querySelector('script, foreignObject')) {
    throw new Error('지형 SVG에 실행 가능한 요소가 들어 있습니다.');
  }

  for (const element of svg.querySelectorAll('*')) {
    for (const attribute of element.attributes) {
      if (/^on/i.test(attribute.name)) {
        throw new Error('지형 SVG에 이벤트 속성이 들어 있습니다.');
      }
      if (/^(?:href|xlink:href)$/i.test(attribute.name)
        && /^(?:https?:|javascript:|data:)/i.test(attribute.value.trim())) {
        throw new Error('지형 SVG에 외부 또는 실행 URL이 들어 있습니다.');
      }
    }
  }
}

async function loadSvg(mapUrl, meta) {
  const response = await fetch(mapUrl);
  if (!response.ok) throw new Error(`지형 SVG 응답이 ${response.status}입니다.`);

  const source = await response.text();
  const svgDocument = new DOMParser().parseFromString(source, 'image/svg+xml');
  const parserError = svgDocument.querySelector('parsererror');
  if (parserError) throw new Error('지형 SVG 문법이 올바르지 않습니다.');

  const sourceSvg = svgDocument.documentElement;
  if (sourceSvg.localName !== 'svg') throw new Error('지형 리소스의 루트가 SVG가 아닙니다.');
  if (sourceSvg.getAttribute('viewBox') !== `0 0 ${meta.size.width} ${meta.size.height}`) {
    throw new Error('meta.json의 크기와 map.svg의 viewBox가 다릅니다.');
  }

  assertPassiveSvg(sourceSvg);
  const svg = document.importNode(sourceSvg, true);
  svg.classList.add('map-svg');
  svg.setAttribute('aria-hidden', 'true');
  return svg;
}

export async function createMapView({ viewport, meta, mapUrl, onViewChange }) {
  const svg = await loadSvg(mapUrl, meta);
  const stage = document.createElement('div');
  stage.className = 'map-stage';
  stage.style.width = `${meta.size.width}px`;
  stage.style.height = `${meta.size.height}px`;

  const markerLayer = document.createElement('div');
  markerLayer.className = 'marker-layer';
  markerLayer.setAttribute('aria-hidden', 'true');
  stage.append(svg, markerLayer);
  viewport.replaceChildren(stage);

  const view = {
    x: 0,
    y: 0,
    zoom: clamp(meta.zoom, meta.minZoom, meta.maxZoom),
  };
  let dragging = false;
  let pointerStart = null;
  let previousSize = { width: viewport.clientWidth, height: viewport.clientHeight };

  function renderView() {
    stage.style.transform = `translate3d(${view.x}px, ${view.y}px, 0) scale(${view.zoom})`;
    stage.style.setProperty('--inverse-zoom', String(1 / view.zoom));
    viewport.dataset.zoom = String(view.zoom);
    onViewChange?.({ ...view });
  }

  function centerAtZoom(zoom) {
    view.zoom = clamp(zoom, meta.minZoom, meta.maxZoom);
    view.x = (viewport.clientWidth - meta.size.width * view.zoom) / 2;
    view.y = (viewport.clientHeight - meta.size.height * view.zoom) / 2;
    renderView();
  }

  function zoomAt(factor, screenPoint) {
    const nextZoom = clamp(view.zoom * factor, meta.minZoom, meta.maxZoom);
    if (nextZoom === view.zoom) return;

    const mapPoint = screenPositionToMapPosition(screenPoint.x, screenPoint.y, view);
    view.zoom = nextZoom;
    const nextScreenPoint = mapPositionToScreenPosition(mapPoint.x, mapPoint.y, view);
    view.x += screenPoint.x - nextScreenPoint.x;
    view.y += screenPoint.y - nextScreenPoint.y;
    renderView();
  }

  function zoomFromCenter(factor) {
    zoomAt(factor, { x: viewport.clientWidth / 2, y: viewport.clientHeight / 2 });
  }

  function setLevelVisibility(levelId, visible) {
    const group = svg.querySelector(`[id="${CSS.escape(levelId)}"]`);
    if (!group) throw new Error(`map.svg에 #${levelId} 레벨이 없습니다.`);
    group.style.display = visible ? '' : 'none';
  }

  for (const level of meta.levels) setLevelVisibility(level.id, level.defaultVisible);

  viewport.addEventListener('wheel', (event) => {
    event.preventDefault();
    const bounds = viewport.getBoundingClientRect();
    zoomAt(event.deltaY < 0 ? WHEEL_ZOOM_FACTOR : 1 / WHEEL_ZOOM_FACTOR, {
      x: event.clientX - bounds.left,
      y: event.clientY - bounds.top,
    });
  }, { passive: false });

  viewport.addEventListener('pointerdown', (event) => {
    if (event.button !== 0) return;
    dragging = true;
    pointerStart = { clientX: event.clientX, clientY: event.clientY, x: view.x, y: view.y };
    viewport.classList.add('is-dragging');
    viewport.setPointerCapture(event.pointerId);
  });

  viewport.addEventListener('pointermove', (event) => {
    if (!dragging || !pointerStart) return;
    view.x = pointerStart.x + event.clientX - pointerStart.clientX;
    view.y = pointerStart.y + event.clientY - pointerStart.clientY;
    renderView();
  });

  function stopDragging(event) {
    if (!dragging) return;
    dragging = false;
    pointerStart = null;
    viewport.classList.remove('is-dragging');
    if (viewport.hasPointerCapture(event.pointerId)) viewport.releasePointerCapture(event.pointerId);
  }

  viewport.addEventListener('pointerup', stopDragging);
  viewport.addEventListener('pointercancel', stopDragging);

  viewport.addEventListener('keydown', (event) => {
    const panDistance = event.shiftKey ? 120 : 40;
    const moves = {
      ArrowLeft: [panDistance, 0],
      ArrowRight: [-panDistance, 0],
      ArrowUp: [0, panDistance],
      ArrowDown: [0, -panDistance],
    };

    if (moves[event.key]) {
      event.preventDefault();
      view.x += moves[event.key][0];
      view.y += moves[event.key][1];
      renderView();
    } else if (event.key === '+' || event.key === '=') {
      event.preventDefault();
      zoomFromCenter(BUTTON_ZOOM_FACTOR);
    } else if (event.key === '-') {
      event.preventDefault();
      zoomFromCenter(1 / BUTTON_ZOOM_FACTOR);
    } else if (event.key === '0') {
      event.preventDefault();
      centerAtZoom(meta.zoom);
    }
  });

  const resizeObserver = new ResizeObserver(() => {
    const nextSize = { width: viewport.clientWidth, height: viewport.clientHeight };
    view.x += (nextSize.width - previousSize.width) / 2;
    view.y += (nextSize.height - previousSize.height) / 2;
    previousSize = nextSize;
    renderView();
  });
  resizeObserver.observe(viewport);

  centerAtZoom(meta.zoom);

  return {
    markerLayer,
    setLevelVisibility,
    zoomIn: () => zoomFromCenter(BUTTON_ZOOM_FACTOR),
    zoomOut: () => zoomFromCenter(1 / BUTTON_ZOOM_FACTOR),
    reset: () => centerAtZoom(meta.zoom),
    centerOn(mapPosition) {
      view.x = viewport.clientWidth / 2 - mapPosition.x * view.zoom;
      view.y = viewport.clientHeight / 2 - mapPosition.y * view.zoom;
      renderView();
    },
  };
}
