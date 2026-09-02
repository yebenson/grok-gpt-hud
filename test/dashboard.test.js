"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const { buildDashboardState } = require("../src/lib/dashboard");

describe("dashboard renderer payload", () => {
  it("never forwards access tokens and masks emails", () => {
    const state = buildDashboardState({
      source: "hermes",
      phase: "ready",
      loaded: true,
      grokSideError: null,
      chatgptSideError: null,
      grokResults: [
        {
          account: { id: "g1", side: "grok", email: "benson@outlook.com", label: "work", accessToken: "secret-token" },
          ok: true,
          quota: { remainingPercent: 72, color: "blue", display: "72%" },
        },
      ],
      chatgptResults: [],
      isVisible: () => true,
    });
    const dumped = JSON.stringify(state);
    assert.equal(dumped.includes("secret-token"), false);
    assert.equal(state.grok.accounts[0].email, "b****n@outlook.com");
    assert.equal(state.grok.accounts[0].accessToken, undefined);
  });
});
