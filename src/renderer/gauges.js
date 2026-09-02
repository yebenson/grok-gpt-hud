"use strict";

function polar(cx, cy, r, angleDeg) {
  const rad = ((angleDeg - 90) * Math.PI) / 180;
  return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
}

function arcPath(cx, cy, r, startDeg, endDeg) {
  const start = polar(cx, cy, r, endDeg);
  const end = polar(cx, cy, r, startDeg);
  const large = endDeg - startDeg <= 180 ? 0 : 1;
  return `M ${start.x} ${start.y} A ${r} ${r} 0 ${large} 0 ${end.x} ${end.y}`;
}

function needleGauge({ remainingPercent, unlimited, hex, size = 126 }) {
  const cx = size / 2;
  const cy = size / 2 + 6;
  const r = size * 0.36;
  const start = 220;
  const sweep = 280;
  const color = hex || "rgba(255,255,255,0.35)";
  const ratio = unlimited ? 1 : Math.max(0, Math.min(1, (remainingPercent ?? 0) / 100));
  const valueAngle = start + sweep * ratio;
  const needle = polar(cx, cy, r - 2, valueAngle);
  const label = unlimited ? "∞" : remainingPercent == null ? "—" : `${Math.round(remainingPercent)}%`;
  const showNeedle = !unlimited && remainingPercent != null;
  const valueFill = remainingPercent == null && !unlimited ? "rgba(255,255,255,0.55)" : "#fff";

  return `
    <svg viewBox="0 0 ${size} ${size}" width="${size}" height="${size}">
      <path d="${arcPath(cx, cy, r, start, start + sweep)}" stroke="rgba(255,255,255,0.12)" stroke-width="7" fill="none" stroke-linecap="round"/>
      <path d="${arcPath(cx, cy, r, start, start + sweep * (unlimited || remainingPercent != null ? ratio : 0))}" stroke="${remainingPercent == null && !unlimited ? "transparent" : color}" stroke-width="7" fill="none" stroke-linecap="round" style="filter: drop-shadow(0 0 6px ${color})"/>
      ${showNeedle ? `<line x1="${cx}" y1="${cy}" x2="${needle.x}" y2="${needle.y}" stroke="${color}" stroke-width="2" stroke-linecap="round"/>` : ""}
      ${showNeedle ? `<circle cx="${cx}" cy="${cy}" r="3.2" fill="${color}"/>` : ""}
      <text class="value" x="${cx}" y="${cy + (showNeedle ? 22 : 6)}" fill="${valueFill}" font-family="Segoe UI, PingFang SC, Microsoft YaHei, sans-serif" font-size="15" font-weight="650">${label}</text>
    </svg>
  `;
}

function ringGauge({ remainingPercent, unlimited, hex, size = 86, caption = "" }) {
  const cx = size / 2;
  const cy = size / 2;
  const r = size * 0.34;
  const c = 2 * Math.PI * r;
  const color = hex || "rgba(255,255,255,0.35)";
  const ratio = unlimited ? 1 : remainingPercent == null ? 0 : Math.max(0, Math.min(1, remainingPercent / 100));
  const label = unlimited ? "∞" : remainingPercent == null ? "—" : `${Math.round(remainingPercent)}%`;
  const stroke = remainingPercent == null && !unlimited ? "transparent" : color;
  return `
    <svg viewBox="0 0 ${size} ${size}" width="${size}" height="${size}">
      <circle cx="${cx}" cy="${cy}" r="${r}" fill="none" stroke="rgba(255,255,255,0.12)" stroke-width="7"/>
      <circle cx="${cx}" cy="${cy}" r="${r}" fill="none" stroke="${stroke}" stroke-width="7"
        stroke-linecap="round"
        stroke-dasharray="${c}"
        stroke-dashoffset="${c * (1 - ratio)}"
        transform="rotate(-90 ${cx} ${cy})"
        style="filter: drop-shadow(0 0 6px ${stroke})"
      />
      <text class="value" x="${cx}" y="${cy + 5}" font-family="Segoe UI, PingFang SC, Microsoft YaHei, sans-serif" font-size="14" font-weight="650">${label}</text>
    </svg>
    <div class="caption">${caption}</div>
  `;
}

window.QuotaGauges = { needleGauge, ringGauge };
