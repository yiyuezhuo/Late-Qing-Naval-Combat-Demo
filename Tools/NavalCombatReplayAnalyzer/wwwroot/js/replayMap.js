window.replayMap = (() => {
  let map;
  let replay;
  let currentLanguage = "english";
  const layers = {
    tracks: [],
    markers: new Map(),
    shotLine: null,
  };

  function init(id) {
    if (map) return;
    const root = document.getElementById(id);
    if (!window.L) {
      map = createSvgMap(root);
      return;
    }
    map = L.map(id, { worldCopyJump: true }).setView([39, 121], 7);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 12,
      attribution: "&copy; OpenStreetMap contributors"
    }).addTo(map);
  }

  function clearReplay() {
    if (map && map.kind === "svg") {
      map.clear();
      return;
    }
    layers.tracks.forEach(layer => layer.remove());
    layers.tracks = [];
    layers.markers.forEach(marker => marker.remove());
    layers.markers.clear();
    if (layers.shotLine) {
      layers.shotLine.remove();
      layers.shotLine = null;
    }
  }

  function setReplay(nextReplay) {
    replay = nextReplay;
    clearReplay();
    if (!map || !replay) return;
    if (map.kind === "svg") {
      map.setReplay(replay);
      return;
    }

    const bounds = [];
    replay.ships.forEach(ship => {
      const latLngs = (ship.track || []).map(p => [p.lat, p.lon]);
      if (latLngs.length > 0) {
        const track = L.polyline(latLngs, {
          color: ship.color || "#4c78a8",
          weight: 2,
          opacity: 0.72
        }).addTo(map);
        track.bindTooltip(`${localizeName(ship.nameVariants, ship.name)}<br>${localizeName(ship.groupNameVariants, ship.groupName)}`);
        layers.tracks.push(track);
        latLngs.forEach(p => bounds.push(p));
      }

      const marker = L.circleMarker(latLngs[0] || [0, 0], {
        radius: 6,
        color: "#111827",
        weight: 1,
        fillColor: ship.color || "#4c78a8",
        fillOpacity: 0.95
      }).addTo(map);
      layers.markers.set(ship.id, marker);
    });

    if (bounds.length > 0) {
      map.fitBounds(bounds, { padding: [30, 30] });
    }
  }

  function setTime(timeValue) {
    if (!map || !replay) return;
    if (map.kind === "svg") {
      map.setTime(timeValue);
      return;
    }
    const current = new Date(timeValue).getTime();

    replay.ships.forEach(ship => {
      const point = pointAtOrBefore(ship.track || [], current);
      const marker = layers.markers.get(ship.id);
      if (!point || !marker) return;

      marker.setLatLng([point.lat, point.lon]);
      marker.bindTooltip(`
        <strong>${escapeHtml(localizeName(ship.nameVariants, ship.name))}</strong><br>
        ${escapeHtml(localizeName(ship.groupNameVariants, ship.groupName))}<br>
        ${point.lat.toFixed(4)}, ${point.lon.toFixed(4)}<br>
        ${point.speedKnots.toFixed(1)} kt, ${point.headingDeg.toFixed(0)} deg<br>
        ${escapeHtml(point.mapState)} / ${escapeHtml(point.operationalState)}
      `);
    });

    const shot = latestShotAtOrBefore(current);
    if (layers.shotLine) {
      layers.shotLine.remove();
      layers.shotLine = null;
    }
    if (shot && shot.shooterPoint && shot.targetPoint) {
      layers.shotLine = L.polyline(
        [
          [shot.shooterPoint.lat, shot.shooterPoint.lon],
          [shot.targetPoint.lat, shot.targetPoint.lon]
        ],
        {
          color: "#f97316",
          weight: 3,
          opacity: 0.92,
          dashArray: "8 8"
        }
      ).addTo(map);
      layers.shotLine.bindTooltip(`${shot.weapon}: ${localizeName(shot.shooterNameVariants, shot.shooterName)} -> ${localizeName(shot.targetNameVariants, shot.targetName)}`);
    }
  }

  function setLanguage(language) {
    currentLanguage = language || "english";
    if (map && replay) {
      setReplay(replay);
    }
  }

  function pointAtOrBefore(points, current) {
    if (!points || points.length === 0) return null;
    let selected = points[0];
    for (const point of points) {
      if (new Date(point.time).getTime() > current) break;
      selected = point;
    }
    return selected;
  }

  function latestShotAtOrBefore(current) {
    if (!replay || !replay.shots) return null;
    let latest = null;
    for (const shot of replay.shots) {
      const shotTime = new Date(shot.time).getTime();
      if (shotTime <= current && (!latest || shotTime > new Date(latest.time).getTime())) {
        latest = shot;
      }
    }
    return latest;
  }

  function downloadText(fileName, text, mimeType) {
    const blob = new Blob([text], { type: mimeType || "text/plain" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName || "replay.txt";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }

  function createSvgMap(root) {
    root.classList.add("fallback-map");
    root.innerHTML = `
      <svg class="fallback-map-svg" viewBox="0 0 1000 1000" preserveAspectRatio="xMidYMid meet">
        <rect x="0" y="0" width="1000" height="1000" class="fallback-water"></rect>
        <g class="fallback-grid"></g>
        <g class="fallback-tracks"></g>
        <g class="fallback-shot"></g>
        <g class="fallback-markers"></g>
      </svg>
      <div class="fallback-map-label">Map fallback: Leaflet unavailable, drawing projected tracks locally.</div>
    `;

    const svg = root.querySelector("svg");
    const grid = root.querySelector(".fallback-grid");
    const tracks = root.querySelector(".fallback-tracks");
    const markers = root.querySelector(".fallback-markers");
    const shotLayer = root.querySelector(".fallback-shot");
    let model;
    let bounds;

    function clear() {
      tracks.innerHTML = "";
      markers.innerHTML = "";
      shotLayer.innerHTML = "";
      grid.innerHTML = "";
    }

    function setReplay(nextReplay) {
      model = nextReplay;
      clear();
      bounds = computeBounds(model);
      drawGrid();
      for (const ship of model.ships || []) {
        const points = (ship.track || []).map(project);
        if (points.length === 0) continue;
        const polyline = document.createElementNS("http://www.w3.org/2000/svg", "polyline");
        polyline.setAttribute("points", points.map(p => `${p.x},${p.y}`).join(" "));
        polyline.setAttribute("fill", "none");
        polyline.setAttribute("stroke", ship.color || "#4c78a8");
        polyline.setAttribute("stroke-width", "3");
        polyline.setAttribute("stroke-opacity", "0.75");
        tracks.appendChild(polyline);
      }
    }

    function setTime(timeValue) {
      if (!model) return;
      const current = new Date(timeValue).getTime();
      markers.innerHTML = "";
      shotLayer.innerHTML = "";

      for (const ship of model.ships || []) {
        const point = pointAtOrBefore(ship.track || [], current);
        if (!point) continue;
        const projected = project(point);
        const circle = document.createElementNS("http://www.w3.org/2000/svg", "circle");
        circle.setAttribute("cx", projected.x);
        circle.setAttribute("cy", projected.y);
        circle.setAttribute("r", "8");
        circle.setAttribute("fill", ship.color || "#4c78a8");
        circle.setAttribute("stroke", "#111827");
        circle.setAttribute("stroke-width", "2");
        markers.appendChild(circle);

        const label = document.createElementNS("http://www.w3.org/2000/svg", "text");
        label.setAttribute("x", projected.x + 12);
        label.setAttribute("y", projected.y - 10);
        label.textContent = localizeName(ship.nameVariants, ship.name);
        markers.appendChild(label);
      }

      const shot = latestShotAtOrBefore(current);
      if (shot && shot.shooterPoint && shot.targetPoint) {
        const a = project(shot.shooterPoint);
        const b = project(shot.targetPoint);
        const line = document.createElementNS("http://www.w3.org/2000/svg", "line");
        line.setAttribute("x1", a.x);
        line.setAttribute("y1", a.y);
        line.setAttribute("x2", b.x);
        line.setAttribute("y2", b.y);
        line.setAttribute("stroke", "#f97316");
        line.setAttribute("stroke-width", "4");
        line.setAttribute("stroke-dasharray", "10 10");
        shotLayer.appendChild(line);
      }
    }

    function computeBounds(model) {
      const all = (model.ships || []).flatMap(ship => ship.track || []);
      if (all.length === 0) {
        return { minLat: 0, maxLat: 1, minLon: 0, maxLon: 1 };
      }
      const minLat = Math.min(...all.map(p => p.lat));
      const maxLat = Math.max(...all.map(p => p.lat));
      const minLon = Math.min(...all.map(p => p.lon));
      const maxLon = Math.max(...all.map(p => p.lon));
      const latPad = Math.max(0.01, (maxLat - minLat) * 0.12);
      const lonPad = Math.max(0.01, (maxLon - minLon) * 0.12);
      return { minLat: minLat - latPad, maxLat: maxLat + latPad, minLon: minLon - lonPad, maxLon: maxLon + lonPad };
    }

    function project(point) {
      const x = 60 + ((point.lon - bounds.minLon) / Math.max(0.000001, bounds.maxLon - bounds.minLon)) * 880;
      const y = 940 - ((point.lat - bounds.minLat) / Math.max(0.000001, bounds.maxLat - bounds.minLat)) * 880;
      return { x, y };
    }

    function drawGrid() {
      for (let i = 0; i <= 10; i++) {
        const pos = 60 + i * 88;
        const h = document.createElementNS("http://www.w3.org/2000/svg", "line");
        h.setAttribute("x1", "60");
        h.setAttribute("x2", "940");
        h.setAttribute("y1", pos);
        h.setAttribute("y2", pos);
        grid.appendChild(h);
        const v = document.createElementNS("http://www.w3.org/2000/svg", "line");
        v.setAttribute("x1", pos);
        v.setAttribute("x2", pos);
        v.setAttribute("y1", "60");
        v.setAttribute("y2", "940");
        grid.appendChild(v);
      }
    }

    return { kind: "svg", clear, setReplay, setTime };
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;");
  }

  function localizeName(name, fallback) {
    if (!name) return fallback || "";
    const selected = name[currentLanguage];
    return firstText(selected, name.english, name.japanese, name.chineseSimplified, name.chineseTraditional, fallback);
  }

  function firstText(...values) {
    for (const value of values) {
      if (value && String(value).trim() && value !== "none" && value !== "[Not Specified]") {
        return value;
      }
    }
    return "";
  }

  return { init, setReplay, setTime, setLanguage, downloadText };
})();
