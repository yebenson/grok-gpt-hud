"use strict";

const WORK_START_HOUR = 9;
const WORK_END_HOUR = 18;
const AUTO_INTERVAL_MS = 15 * 60 * 1000;

/**
 * Auto-refresh is allowed only between 09:00 (inclusive) and 18:00 (exclusive)
 * in the machine's local timezone.
 */
function isAutoRefreshAllowed(date = new Date()) {
  const d = date instanceof Date ? date : new Date(date);
  if (Number.isNaN(d.getTime())) return false;
  const hour = d.getHours();
  return hour >= WORK_START_HOUR && hour < WORK_END_HOUR;
}

function shouldAutoPoll({ now = new Date(), lastPollAt = null, intervalMs = AUTO_INTERVAL_MS } = {}) {
  if (!isAutoRefreshAllowed(now)) return false;
  if (lastPollAt == null) return true;
  const nowMs = now instanceof Date ? now.getTime() : Number(now);
  const lastMs = lastPollAt instanceof Date ? lastPollAt.getTime() : Number(lastPollAt);
  return nowMs - lastMs >= intervalMs;
}

module.exports = {
  WORK_START_HOUR,
  WORK_END_HOUR,
  AUTO_INTERVAL_MS,
  isAutoRefreshAllowed,
  shouldAutoPoll,
};
