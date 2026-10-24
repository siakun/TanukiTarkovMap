const MARGIN_RATIO = 0.1;

const FIT_RATIO = 0.9;

const MOVE_DURATION = 250;

const clamp = (value, minimum, maximum) => Math.min(maximum, Math.max(minimum, value));

function wheelMultiplier(event) {
  let delta = event.deltaY;
  if (event.deltaMode > 0) delta *= 100;
  return 1 - Math.sign(delta) * Math.min(0.25, Math.abs(delta) / 128);
}

export function createCamera({ container, wrap, minZoom, maxZoom, terrainBounds, onChange, onAltWheel, rotation = 0, mapSize }) {
  const view = { x: 0, y: 0, zoom: 1 };
  const [a, b, c, d, e, f] = rotation === 90 ? [0, 1, -1, 0, mapSize.height, 0]
    : rotation === 180 ? [-1, 0, 0, -1, mapSize.width, mapSize.height]
      : rotation === 270 ? [0, -1, 1, 0, 0, mapSize.width] : [1, 0, 0, 1, 0, 0];
  const rotatePoint = ({ x, y }) => ({ x: a * x + c * y + e, y: b * x + d * y + f });

  function viewBounds() {
    const bounds = terrainBounds();
    if (!bounds || rotation === 0) return bounds;
    const first = rotatePoint({ x: bounds.left, y: bounds.top });
    const last = rotatePoint({ x: bounds.right, y: bounds.bottom });
    return { left: Math.min(first.x, last.x), top: Math.min(first.y, last.y),
      right: Math.max(first.x, last.x), bottom: Math.max(first.y, last.y) };
  }
  let animation = 0;
  let drag = null;
  let lastSize = { width: container.clientWidth, height: container.clientHeight };

  function viewSize() {
    return { width: container.clientWidth, height: container.clientHeight };
  }

  function keepTerrainCentered() {
    const bounds = viewBounds();
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
    wrap.style.transform = `matrix(${a * view.zoom}, ${b * view.zoom}, ${c * view.zoom}, ${d * view.zoom}, ${view.x + e * view.zoom}, ${view.y + f * view.zoom})`;
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

  function fit() {
    stopAnimation();
    const bounds = viewBounds();
    const { width, height } = viewSize();
    if (!bounds || !width || !height) return;
    const terrainWidth = bounds.right - bounds.left, terrainHeight = bounds.bottom - bounds.top;
    view.zoom = clamp(Math.min(width * FIT_RATIO / terrainWidth, height * FIT_RATIO / terrainHeight), minZoom, maxZoom);
    view.x = width / 2 - (bounds.left + terrainWidth / 2) * view.zoom;
    view.y = height / 2 - (bounds.top + terrainHeight / 2) * view.zoom;
    render();
  }

  function centerOn(point) {
    stopAnimation();
    point = rotatePoint(point);
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
    point = rotatePoint(point);
    return { x: view.x + point.x * view.zoom, y: view.y + point.y * view.zoom };
  }

  container.addEventListener('wheel', (event) => {
    event.preventDefault();
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

  new ResizeObserver(() => {
    const size = viewSize();
    view.x += (size.width - lastSize.width) / 2;
    view.y += (size.height - lastSize.height) / 2;
    lastSize = size;
    render();
  }).observe(container);

  return { view, fit, centerOn, toScreen, render, viewSize };
}
