/**
 * 마커 층은 resources의 지도 좌표를 그대로 사용하고, 종류와 레벨 가시성만 함께 판정한다.
 * 영역형 원본도 markers.json이 정한 중심점 하나로 표시하므로 원본 사이트의 도형 형식이 표현
 * 코드로 새어 나오지 않는다. 화면에서 읽을 수 있는 크기는 CSS의 역배율이 유지한다.
 */
function assertMarkerData(markerData) {
  if (markerData.coordinateSpace !== 'map') {
    throw new Error(`지원하지 않는 마커 좌표계입니다: ${markerData.coordinateSpace}`);
  }
  if (!Array.isArray(markerData.categories) || !Array.isArray(markerData.markers)) {
    throw new Error('markers.json에 categories 또는 markers 배열이 없습니다.');
  }

  const categoryIds = new Set();
  const subtypeIds = new Map();
  for (const category of markerData.categories) {
    if (!category.id || categoryIds.has(category.id) || !Array.isArray(category.subtypes)) {
      throw new Error(`마커 종류가 없거나 중복됩니다: ${category.id}`);
    }
    const subtypeList = category.subtypes.map((subtype) => subtype.id);
    if (subtypeList.some((subtypeId) => !subtypeId)
      || new Set(subtypeList).size !== subtypeList.length) {
      throw new Error(`${category.id}: 마커 세부 종류가 없거나 중복됩니다.`);
    }
    categoryIds.add(category.id);
    subtypeIds.set(category.id, new Set(subtypeList));
  }

  const markerIds = new Set();
  for (const marker of markerData.markers) {
    if (!marker.id || markerIds.has(marker.id)) {
      throw new Error(`마커 ID가 없거나 중복됩니다: ${marker.id}`);
    }
    markerIds.add(marker.id);
    if (!categoryIds.has(marker.category)
      || !subtypeIds.get(marker.category).has(marker.subtype)) {
      throw new Error(`${marker.id}: 알 수 없는 마커 종류입니다.`);
    }
    if (typeof marker.name !== 'string' || !marker.levelId || !Number.isFinite(marker.position?.x)
      || !Number.isFinite(marker.position?.y)) {
      throw new Error(`${marker.id}: 이름, 레벨 또는 지도 좌표가 올바르지 않습니다.`);
    }
  }
}

export function createMapMarkers(markerLayer, markerData, initiallyVisibleLevels) {
  assertMarkerData(markerData);
  const categories = new Map(markerData.categories.map((category) => [category.id, category]));
  const categoryVisibility = new Map(
    markerData.categories.map((category) => [category.id, category.defaultVisible !== false])
  );
  const visibleLevels = new Set(initiallyVisibleLevels);
  const renderedMarkers = [];
  const fragment = document.createDocumentFragment();

  for (const markerDataEntry of markerData.markers) {
    const category = categories.get(markerDataEntry.category);
    const subtype = category.subtypes.find((entry) => entry.id === markerDataEntry.subtype);
    const markerName = markerDataEntry.name.trim() || subtype.label;
    const marker = document.createElement('span');
    marker.className = 'map-marker';
    marker.style.left = `${markerDataEntry.position.x}px`;
    marker.style.top = `${markerDataEntry.position.y}px`;
    marker.dataset.markerId = markerDataEntry.id;
    marker.dataset.markerCategory = markerDataEntry.category;
    marker.dataset.markerSubtype = markerDataEntry.subtype;
    marker.dataset.markerLevel = markerDataEntry.levelId;
    marker.dataset.markerName = markerName;
    marker.dataset.mapX = String(markerDataEntry.position.x);
    marker.dataset.mapY = String(markerDataEntry.position.y);
    marker.setAttribute('role', 'img');
    marker.setAttribute('aria-label', `${markerName}, ${subtype.label}`);

    const symbol = document.createElement('span');
    symbol.className = 'map-marker__symbol';
    const label = document.createElement('span');
    label.className = 'map-marker__label';
    label.textContent = markerName;
    marker.append(symbol, label);
    fragment.append(marker);
    renderedMarkers.push({ element: marker, data: markerDataEntry });
  }
  markerLayer.append(fragment);

  function updateVisibility() {
    for (const marker of renderedMarkers) {
      marker.element.hidden = !categoryVisibility.get(marker.data.category)
        || !visibleLevels.has(marker.data.levelId);
    }
  }

  updateVisibility();
  return {
    setCategoryVisibility(categoryId, visible) {
      if (!categoryVisibility.has(categoryId)) {
        throw new Error(`markers.json에 ${categoryId} 종류가 없습니다.`);
      }
      categoryVisibility.set(categoryId, visible);
      updateVisibility();
    },
    setLevelVisibility(levelId, visible) {
      visible ? visibleLevels.add(levelId) : visibleLevels.delete(levelId);
      updateVisibility();
    },
  };
}

export function createPositionMarker(markerLayer) {
  const marker = document.createElement('div');
  marker.className = 'position-marker';
  marker.hidden = true;
  marker.setAttribute('aria-hidden', 'true');

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
