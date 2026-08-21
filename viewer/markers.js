/**
 * 위치 마커는 맵 좌표만 받아 지형과 같은 좌표계에 놓인다. 화면에서 읽을 수 있는 크기는 유지하되
 * 위치 계산에는 관여하지 않아 좌표 변환과 표현을 섞지 않는다.
 */
export function createPositionMarker(markerLayer) {
  const marker = document.createElement('div');
  marker.className = 'position-marker';
  marker.hidden = true;

  const ring = document.createElement('span');
  ring.className = 'position-marker__ring';
  const core = document.createElement('span');
  core.className = 'position-marker__core';
  const label = document.createElement('span');
  label.className = 'position-marker__label';
  label.textContent = 'YOU';
  marker.append(ring, core, label);
  markerLayer.append(marker);

  return {
    show(mapPosition) {
      marker.style.left = `${mapPosition.x}px`;
      marker.style.top = `${mapPosition.y}px`;
      marker.dataset.mapX = String(mapPosition.x);
      marker.dataset.mapY = String(mapPosition.y);
      marker.hidden = false;
      marker.classList.remove('is-pinging');
      void marker.offsetWidth;
      marker.classList.add('is-pinging');
    },
    hide() {
      marker.hidden = true;
    },
  };
}
