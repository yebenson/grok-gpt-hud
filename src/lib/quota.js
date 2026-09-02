"use strict";

const { remainingColor, remainingHex } = require("./colors");

function clamp(n, min, max) {
  return Math.min(max, Math.max(min, n));
}

function asObject(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : null;
}

function hasCapField(window) {
  if (!asObject(window)) return false;
  if (window.unlimited === true) return false;
  const capKeys = ["limit", "limit_window_seconds", "cap", "max", "quota", "limit_tokens"];
  return capKeys.some((key) => {
    const value = window[key];
    if (value == null || value === false) return false;
    if (typeof value === "number") return Number.isFinite(value) && value > 0;
    if (typeof value === "string") {
      const n = Number(value);
      return Number.isFinite(n) && n > 0;
    }
    return true;
  });
}

function resetFromWindow(window) {
  if (!asObject(window)) return null;
  if (window.reset_at != null) return window.reset_at;
  if (window.resetAt != null) return window.resetAt;
  if (window.reset_after_seconds != null) {
    return { afterSeconds: Number(window.reset_after_seconds) };
  }
  return null;
}

function parseCappedWindow(window) {
  const usedRaw = window.used_percent ?? window.usedPercent;
  const used = Number(usedRaw);
  if (!Number.isFinite(used)) {
    return {
      unlimited: false,
      remainingPercent: null,
      resetAt: resetFromWindow(window),
    };
  }
  return {
    unlimited: false,
    remainingPercent: clamp(100 - used, 0, 100),
    resetAt: resetFromWindow(window),
    usedPercent: used,
  };
}

/**
 * 5-hour Codex window: missing / unlimited / no cap field → ∞ (not 0% red).
 */
function parseFiveHourWindow(payload) {
  const rate = asObject(payload?.rate_limit) || asObject(payload) || {};
  const credits = asObject(payload?.credits) || {};
  const primary = rate.primary_window ?? rate.primaryWindow;
  if (credits.unlimited === true && primary == null) {
    return { unlimited: true, remainingPercent: null, resetAt: null };
  }
  if (primary == null) {
    return { unlimited: true, remainingPercent: null, resetAt: null };
  }
  if (!asObject(primary) || primary.unlimited === true || !hasCapField(primary)) {
    return { unlimited: true, remainingPercent: null, resetAt: resetFromWindow(primary) };
  }
  return parseCappedWindow(primary);
}

function parseWeeklyWindow(payload) {
  const rate = asObject(payload?.rate_limit) || asObject(payload) || {};
  const secondary = rate.secondary_window ?? rate.secondaryWindow;
  if (secondary == null || !asObject(secondary) || secondary.unlimited === true || !hasCapField(secondary)) {
    return { unlimited: false, remainingPercent: null, resetAt: null, missing: true };
  }
  return parseCappedWindow(secondary);
}

function parseChatgptUsage(payload) {
  const fiveHour = parseFiveHourWindow(payload);
  const weekly = parseWeeklyWindow(payload);
  return {
    fiveHour,
    weekly,
    planType: typeof payload?.plan_type === "string" ? payload.plan_type : null,
  };
}

function pickGrokUsedPercent(config) {
  const products = Array.isArray(config.productUsage)
    ? config.productUsage
    : Array.isArray(config.product_usage)
      ? config.product_usage
      : [];
  const build = products.find((item) => item && (item.product === "GrokBuild" || item.product === "grok-build"));
  const fromProduct = build?.usagePercent ?? build?.usage_percent;
  if (fromProduct != null && Number.isFinite(Number(fromProduct))) return Number(fromProduct);

  const credit = config.creditUsagePercent ?? config.credit_usage_percent;
  if (credit != null && Number.isFinite(Number(credit))) return Number(credit);

  const monthly = asObject(config.monthlyLimit) || asObject(config.monthly_limit);
  const usedObj = asObject(config.used);
  const limitVal = typeof config.monthlyLimit === "number" ? config.monthlyLimit : monthly?.val;
  const usedVal = typeof config.used === "number" ? config.used : usedObj?.val;
  if (Number.isFinite(Number(limitVal)) && Number(limitVal) > 0 && Number.isFinite(Number(usedVal))) {
    return clamp((Number(usedVal) / Number(limitVal)) * 100, 0, 100);
  }
  return null;
}

function pickGrokReset(config) {
  const period = asObject(config.currentPeriod) || asObject(config.current_period) || {};
  return period.end || config.billingPeriodEnd || config.billing_period_end || null;
}

function parseGrokBilling(payload) {
  const config = asObject(payload?.config) || asObject(payload);
  if (!config) {
    return { remainingPercent: null, resetAt: null, missing: true };
  }
  const used = pickGrokUsedPercent(config);
  if (used == null) {
    return { remainingPercent: null, resetAt: pickGrokReset(config), missing: true };
  }
  return {
    remainingPercent: clamp(100 - used, 0, 100),
    usedPercent: used,
    resetAt: pickGrokReset(config),
    missing: false,
  };
}

function decorateNeedle(windowLike) {
  if (!windowLike) {
    return { remainingPercent: null, unlimited: false, color: null, hex: null };
  }
  if (windowLike.unlimited) {
    return {
      ...windowLike,
      display: "∞",
      color: remainingColor(100),
      hex: remainingHex(100),
    };
  }
  const pct = windowLike.remainingPercent;
  return {
    ...windowLike,
    display: pct == null ? "—" : `${Math.round(pct)}%`,
    color: remainingColor(pct),
    hex: remainingHex(pct),
  };
}

module.exports = {
  hasCapField,
  parseFiveHourWindow,
  parseWeeklyWindow,
  parseChatgptUsage,
  parseGrokBilling,
  decorateNeedle,
};
