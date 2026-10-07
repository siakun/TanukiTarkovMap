/**
 * 지도 카메라. 지형 SVG(.map-wrap)를 확대하고 옮기며, 마커 층은 onChange로 같은 상태를 받는다.
 *
 * 조작 감각은 사이트가 쓰는 anvaka/panzoom 설정을 따른다. 휠 한 칸의 배율 식, 커서를 고정점으로
 * 삼는 확대, 커서와 1:1인 끌기, 손을 뗀 뒤 관성 없음, 맵별 최소/최대 배율이 그 값이다.
 * 사이트에 없는 규칙 하나를 더한다. 화면 한가운데에는 언제나 지형이 있어야 한다. 사이트의 경계는
 * 빈 여백까지 포함한 좌표 공간 전체를 기준으로 삼아 지형이 화면 밖으로 사라질 수 있기 때문이다.
 * 이 규칙은 Online의 map-keep-visible.js와 같은 값(가운데에서 화면의 10%)을 쓰며, 끌기뿐 아니라
 * 휠, 창 크기 변경, 층 전환, 위치 이동에 모두 적용한다.
 */

// 지형이 화면 가운데를 지나 이만큼 더 들어와 있어야 한다 (화면 크기에 대한 비율)
const MARGIN_RATIO = 0.1;

// 열 때 지형이 화면에서 차지할 비율. Online 맞춤의 합격 범위(0.75~0.98)의 가운데다
const FIT_RATIO = 0.9;

// 위치로 옮기는 애니메이션 시간. 사이트의 smoothMoveTo처럼 짧게 미끄러진다
const MOVE_DURATION = 250;

const clamp = (value, minimum, maximum) => Math.min(maximum, Math.max(minimum, value));

/** anvaka/panzoom의 휠 배율 식(zoomSpeed 1). deltaY 100이면 0.75배, -100이면 1.25배다 */
function wheelMultiplier(event) {
  let delta = event.deltaY;
  if (event.deltaMode > 0) delta *= 100;
  return 1 - Math.sign(delta) * Math.min(0.25, Math.abs(delta) / 128);
}

export function createCamera({ container, wrap, minZoom, maxZoom, terrainBounds, onChange, onAltWheel }) {
  const view = { x: 0, y: 0, zoom: 1 };
  let animation = 0;
  let drag = null;
  let lastSize = { width: container.clientWidth, height: container.clientHeight };

  function viewSize() {
    return { width: container.clientWidth, height: container.clientHeight };
  }

  // 지형의 화면 범위가 가운데를 덮도록 이동값을 제한한다. 지형이 작으면 여유도 지형의 절반까지만 요구한다.
  function keepTerrainCentered() {
    const bounds = terrainBounds();
    if (!bounds) return;
    const { width, height } = viewSize();
    const terrainWidth = (bounds.right - bounds.left) * view.zoom;
    const terrainHeight = (bounds.bottom - bounds.top) * view.zoom;
    const marginX = Math.min(width * MARGIN_RATIO, terrainWidth / 2);
    const marginY = Math.min(height * MARGIN_RATIO, terrainHeight / 2);
    view.x = clamp(view.x, width / 2 + marginX - bounds.right * view.zoom, width / 2 - marginX - bounds.left * view.zoom);
    view.y = clamp(view.y, height / 2 + marginY - bounds.bottom * view.zoom, height / 2 - marginY - bounds.top * view.zoom);
  }

  function render() {
    keepTerrainCentered();
    wrap.style.transform = `matrix(${view.zoom}, 0, 0, ${view.zoom}, ${view.x}, ${view.y})`;
    onChange();
  }

  function stopAnimation() {
    if (animation) cancelAnimationFrame(animation);
    animation = 0;
  }

  function zoomAt(multiplier, screenX, screenY) {
    const zoom = clamp(view.zoom * multiplier, minZoom, maxZoom);
    const ratio = zoom / view.zoom;
    view.x = screenX - (screenX - view.x) * ratio;
    view.y = screenY - (screenY - view.y) * ratio;
    view.zoom = zoom;
    render();
  }

  /** 지형 전체가 화면의 FIT_RATIO를 채우도록 배율을 정하고 가운데에 둔다 */
  function fit() {
    stopAnimation();
    const bounds = terrainBounds();
    const { width, height } = viewSize();
    if (!bounds || !width || !height) return;
    const terrainWidth = bounds.right - bounds.left, terrainHeight = bounds.bottom - bounds.top;
    view.zoom = clamp(Math.min(width * FIT_RATIO / terrainWidth, height * FIT_RATIO / terrainHeight), minZoom, maxZoom);
    view.x = width / 2 - (bounds.left + terrainWidth / 2) * view.zoom;
    view.y = height / 2 - (bounds.top + terrainHeight / 2) * view.zoom;
    render();
  }

  /** 지도 좌표를 화면 가운데로 옮긴다. 배율은 바꾸지 않는다 */
  function centerOn(point) {
    stopAnimation();
    const { width, height } = viewSize();
    const from = { x: view.x, y: view.y };
    const to = { x: width / 2 - point.x * view.zoom, y: height / 2 - point.y * view.zoom };
    const start = performance.now();
    const step = (now) => {
      const progress = Math.min(1, (now - start) / MOVE_DURATION);
      const eased = 1 - Math.pow(1 - progress, 3);
      view.x = from.x + (to.x - from.x) * eased;
      view.y = from.y + (to.y - from.y) * eased;
      render();
      animation = progress < 1 ? requestAnimationFrame(step) : 0;
    };
    animation = requestAnimationFrame(step);
  }

  function toScreen(point) {
    return { x: view.x + point.x * view.zoom, y: view.y + point.y * view.zoom };
  }

  container.addEventListener('wheel', (event) => {
    event.preventDefault();
    // 사이트처럼 Alt 휠은 배율 대신 층을 바꾼다. 휠을 위로 굴리면 위층이다.
    if (event.altKey) {
      if (event.deltaY) onAltWheel(event.deltaY < 0);
      return;
    }
    stopAnimation();
    const box = container.getBoundingClientRect();
    zoomAt(wheelMultiplier(event), event.clientX - box.left, event.clientY - box.top);
  }, { passive: false });

  container.addEventListener('pointerdown', (event) => {
    if (event.button !== 0 || event.target.closest('.panel_right')) return;
    stopAnimation();
    drag = { id: event.pointerId, x: event.clientX, y: event.clientY };
    container.setPointerCapture(event.pointerId);
    container.classList.add('panning');
  });
  container.addEventListener('pointermove', (event) => {
    if (drag?.id !== event.pointerId) return;
    // 제한에 걸린 뒤에도 기준점을 매번 갱신해, 방향을 바꾸면 바로 따라오게 한다
    view.x += event.clientX - drag.x;
    view.y += event.clientY - drag.y;
    drag.x = event.clientX;
    drag.y = event.clientY;
    render();
  });
  const endDrag = (event) => {
    if (drag?.id !== event.pointerId) return;
    drag = null;
    container.classList.remove('panning');
  };
  container.addEventListener('pointerup', endDrag);
  container.addEventListener('pointercancel', endDrag);

  // 창 크기가 바뀌면 화면 가운데에 있던 지점을 그대로 가운데에 둔다
  new ResizeObserver(() => {
    const size = viewSize();
    view.x += (size.width - lastSize.width) / 2;
    view.y += (size.height - lastSize.height) / 2;
    lastSize = size;
    render();
  }).observe(container);

  return { view, fit, centerOn, toScreen, render, viewSize };
}
