"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const { remainingColor } = require("../src/lib/colors");

describe("remaining color thirds", () => {
  it("maps ≥67% to blue", () => {
    assert.equal(remainingColor(67), "blue");
    assert.equal(remainingColor(100), "blue");
    assert.equal(remainingColor(80.1), "blue");
  });

  it("maps 33–67% to yellow", () => {
    assert.equal(remainingColor(33), "yellow");
    assert.equal(remainingColor(66.9), "yellow");
    assert.equal(remainingColor(50), "yellow");
  });

  it("maps <33% to red", () => {
    assert.equal(remainingColor(32.9), "red");
    assert.equal(remainingColor(0), "red");
    assert.equal(remainingColor(12), "red");
  });

  it("does not invent a color when data is missing", () => {
    assert.equal(remainingColor(null), null);
    assert.equal(remainingColor(undefined), null);
    assert.equal(remainingColor(Number.NaN), null);
    assert.equal(remainingColor("72"), null);
  });
});
