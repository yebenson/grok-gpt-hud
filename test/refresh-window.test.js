"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const { isAutoRefreshAllowed, shouldAutoPoll } = require("../src/lib/refresh-window");

function atLocal(hours, minutes = 0) {
  const d = new Date();
  d.setHours(hours, minutes, 0, 0);
  return d;
}

describe("refresh window", () => {
  it("allows auto poll inside 09:00–18:00 local time", () => {
    assert.equal(isAutoRefreshAllowed(atLocal(9, 0)), true);
    assert.equal(isAutoRefreshAllowed(atLocal(12, 30)), true);
    assert.equal(isAutoRefreshAllowed(atLocal(17, 59)), true);
  });

  it("does not auto poll at 18:01", () => {
    const now = atLocal(18, 1);
    assert.equal(isAutoRefreshAllowed(now), false);
    assert.equal(
      shouldAutoPoll({ now, lastPollAt: atLocal(17, 40), intervalMs: 15 * 60 * 1000 }),
      false,
    );
  });

  it("does not auto poll before 09:00", () => {
    assert.equal(isAutoRefreshAllowed(atLocal(8, 59)), false);
    assert.equal(isAutoRefreshAllowed(atLocal(18, 0)), false);
  });

  it("respects the 15 minute interval inside the window", () => {
    const now = atLocal(10, 0);
    const recent = new Date(now.getTime() - 5 * 60 * 1000);
    const stale = new Date(now.getTime() - 15 * 60 * 1000);
    assert.equal(shouldAutoPoll({ now, lastPollAt: recent }), false);
    assert.equal(shouldAutoPoll({ now, lastPollAt: stale }), true);
  });
});
