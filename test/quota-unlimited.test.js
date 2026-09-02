"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const { parseFiveHourWindow, parseChatgptUsage, decorateNeedle } = require("../src/lib/quota");

describe("5h window infinity when there is no cap", () => {
  it("treats a missing primary window as unlimited", () => {
    const five = parseFiveHourWindow({
      rate_limit: {
        secondary_window: { used_percent: 40, limit_window_seconds: 604800 },
      },
    });
    assert.equal(five.unlimited, true);
    assert.equal(five.remainingPercent, null);
    assert.equal(decorateNeedle(five).display, "∞");
  });

  it("treats unlimited flag as ∞ instead of 0% red", () => {
    const five = parseFiveHourWindow({
      rate_limit: {
        primary_window: { unlimited: true, used_percent: 0 },
      },
    });
    assert.equal(five.unlimited, true);
    assert.notEqual(decorateNeedle(five).color, "red");
    assert.equal(decorateNeedle(five).display, "∞");
  });

  it("treats a window with no cap field as ∞ even if used_percent is present", () => {
    const five = parseFiveHourWindow({
      rate_limit: {
        primary_window: { used_percent: 100 },
      },
    });
    assert.equal(five.unlimited, true);
    assert.equal(decorateNeedle(five).display, "∞");
    assert.notEqual(decorateNeedle(five).color, "red");
  });

  it("still draws a weekly needle when that window is capped", () => {
    const parsed = parseChatgptUsage({
      rate_limit: {
        primary_window: { used_percent: 10 },
        secondary_window: { used_percent: 88, limit_window_seconds: 604800, reset_at: 1700000000 },
      },
    });
    assert.equal(parsed.fiveHour.unlimited, true);
    assert.equal(parsed.weekly.unlimited, false);
    assert.equal(parsed.weekly.remainingPercent, 12);
    assert.equal(decorateNeedle(parsed.weekly).color, "red");
  });

  it("uses remaining percent when the 5h window is actually capped", () => {
    const five = parseFiveHourWindow({
      rate_limit: {
        primary_window: { used_percent: 19, limit_window_seconds: 18000, reset_at: 1700000000 },
      },
    });
    assert.equal(five.unlimited, false);
    assert.equal(five.remainingPercent, 81);
  });
});
