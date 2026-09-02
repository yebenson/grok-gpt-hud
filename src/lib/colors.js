"use strict";

const COLOR_BLUE = "blue";
const COLOR_YELLOW = "yellow";
const COLOR_RED = "red";

const HEX = {
  [COLOR_BLUE]: "#4da3ff",
  [COLOR_YELLOW]: "#f5c542",
  [COLOR_RED]: "#ff5a5f",
};

/**
 * Map remaining quota percent to a HUD color.
 * Missing / non-finite values must not paint a fake 100% (or any color).
 *
 * ≥67% blue, 33–67% yellow, <33% red.
 */
function remainingColor(remainingPercent) {
  if (remainingPercent == null) return null;
  if (typeof remainingPercent !== "number" || !Number.isFinite(remainingPercent)) {
    return null;
  }
  if (remainingPercent >= 67) return COLOR_BLUE;
  if (remainingPercent >= 33) return COLOR_YELLOW;
  return COLOR_RED;
}

function remainingHex(remainingPercent) {
  const name = remainingColor(remainingPercent);
  return name ? HEX[name] : null;
}

module.exports = {
  COLOR_BLUE,
  COLOR_YELLOW,
  COLOR_RED,
  HEX,
  remainingColor,
  remainingHex,
};
