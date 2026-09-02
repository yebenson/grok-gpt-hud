"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const { createSession } = require("../src/lib/session");
const { createVisibilityStore } = require("../src/lib/visibility");
const { memoryStore } = require("./helpers");

function delayed(ms, value) {
  return new Promise((resolve) => setTimeout(() => resolve(value), ms));
}

function loaded(source, grokIds, chatgptIds) {
  return {
    source,
    filesRead: [`/${source}.json`],
    grok: {
      accounts: grokIds.map((id) => ({ id, side: "grok", accessToken: `${id}-token`, label: id })),
      error: grokIds.length ? null : Object.assign(new Error("empty"), { code: "emptyPool" }),
    },
    chatgpt: {
      accounts: chatgptIds.map((id) => ({ id, side: "chatgpt", accessToken: `${id}-token`, label: id })),
      error: chatgptIds.length ? null : Object.assign(new Error("empty"), { code: "emptyPool" }),
    },
  };
}

describe("source switch while refresh is in-flight", () => {
  it("keeps only the new source results and drops the old source accounts", async () => {
    const visibility = createVisibilityStore(memoryStore({ source: "hermes" }));
    const grokStarted = {};
    let grokRelease;
    const grokGate = new Promise((resolve) => {
      grokRelease = resolve;
    });

    const session = createSession({
      visibility,
      loadSource: (source) =>
        source === "hermes"
          ? loaded("hermes", ["hermes-grok"], ["hermes-gpt"])
          : loaded("terminal", ["terminal-grok"], ["terminal-gpt"]),
      fetchGrok: async (account) => {
        grokStarted[account.id] = true;
        if (account.id === "hermes-grok") await grokGate;
        return { config: { creditUsagePercent: account.id === "hermes-grok" ? 10 : 40 } };
      },
      fetchChatgpt: async () => ({
        rate_limit: {
          primary_window: { used_percent: 10, limit_window_seconds: 18000 },
          secondary_window: { used_percent: 20, limit_window_seconds: 604800 },
        },
      }),
    });

    const stale = session.refreshNow({ manual: false });
    while (!grokStarted["hermes-grok"]) {
      await delayed(5);
    }
    assert.equal(grokStarted["hermes-grok"], true);

    const switched = session.setSource("terminal");
    grokRelease();

    const staleResult = await stale;
    const freshResult = await switched;
    const state = session.currentState();

    assert.equal(staleResult.discarded, true);
    assert.equal(freshResult.discarded, false);
    assert.equal(state.source, "terminal");

    const grokIds = state.grok.all.map((item) => item.id);
    const gptIds = state.chatgpt.all.map((item) => item.id);
    assert.deepEqual(grokIds, ["terminal-grok"]);
    assert.deepEqual(gptIds, ["terminal-gpt"]);
    assert.equal(grokIds.includes("hermes-grok"), false);
    assert.equal(gptIds.includes("hermes-gpt"), false);
  });
});
