"use strict";

const MIN_MS = 15_000;
const MAX_MS = 15 * 60 * 1000;

function computeBackoffMs(consecutiveFailures) {
  const n = Number(consecutiveFailures) || 0;
  if (n <= 0) return 0;
  return Math.min(MAX_MS, MIN_MS * 2 ** (n - 1));
}

function createBackoffTracker() {
  const failures = new Map();
  const nextAllowedAt = new Map();

  return {
    recordSuccess(id) {
      failures.delete(id);
      nextAllowedAt.delete(id);
    },
    recordFailure(id, now = Date.now()) {
      const count = (failures.get(id) || 0) + 1;
      failures.set(id, count);
      nextAllowedAt.set(id, now + computeBackoffMs(count));
      return computeBackoffMs(count);
    },
    shouldSkip(id, now = Date.now()) {
      const until = nextAllowedAt.get(id);
      return typeof until === "number" && now < until;
    },
    consecutiveFailures(id) {
      return failures.get(id) || 0;
    },
  };
}

module.exports = { computeBackoffMs, createBackoffTracker, MIN_MS, MAX_MS };
