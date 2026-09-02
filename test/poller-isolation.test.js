"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const { pollAccounts } = require("../src/lib/poll");

describe("one failed account does not block others", () => {
  it("returns success and 401 side by side", async () => {
    const order = [];
    const { grok, chatgpt } = await pollAccounts({
      grokAccounts: [
        { id: "g-ok", side: "grok", accessToken: "a" },
        { id: "g-bad", side: "grok", accessToken: "b" },
      ],
      chatgptAccounts: [
        { id: "c-ok", side: "chatgpt", accessToken: "c", accountId: "acct" },
        { id: "c-slow", side: "chatgpt", accessToken: "d", accountId: "acct2" },
      ],
      fetchGrok: async (account) => {
        order.push(account.id);
        if (account.id === "g-bad") {
          const err = new Error("expired");
          err.status = 401;
          throw err;
        }
        return { config: { creditUsagePercent: 28, billingPeriodEnd: "2026-09-08T00:00:00Z" } };
      },
      fetchChatgpt: async (account) => {
        order.push(account.id);
        if (account.id === "c-slow") {
          await new Promise((resolve) => setTimeout(resolve, 20));
        }
        return {
          rate_limit: {
            primary_window: { used_percent: 10, limit_window_seconds: 18000 },
            secondary_window: { used_percent: 40, limit_window_seconds: 604800 },
          },
        };
      },
    });

    const grokById = Object.fromEntries(grok.map((item) => [item.account.id, item]));
    const gptById = Object.fromEntries(chatgpt.map((item) => [item.account.id, item]));

    assert.equal(grokById["g-ok"].ok, true);
    assert.equal(grokById["g-ok"].quota.remainingPercent, 72);
    assert.equal(grokById["g-bad"].ok, false);
    assert.match(grokById["g-bad"].error.message, /凭证过期/);
    assert.equal(gptById["c-ok"].ok, true);
    assert.equal(gptById["c-ok"].quota.fiveHour.remainingPercent, 90);
    assert.equal(gptById["c-slow"].ok, true);
    assert.equal(grok.length + chatgpt.length, 4);
    assert.ok(order.includes("g-ok"));
    assert.ok(order.includes("g-bad"));
  });

  it("does not wait to fail the whole batch when one side throws", async () => {
    const started = Date.now();
    const { chatgpt } = await pollAccounts({
      grokAccounts: [],
      chatgptAccounts: [
        { id: "fast", side: "chatgpt", accessToken: "x" },
        { id: "boom", side: "chatgpt", accessToken: "y" },
      ],
      fetchGrok: async () => ({}),
      fetchChatgpt: async (account) => {
        if (account.id === "boom") {
          const err = new Error("nope");
          err.status = 403;
          throw err;
        }
        return {
          rate_limit: {
            primary_window: { used_percent: 1, limit_window_seconds: 18000 },
            secondary_window: { used_percent: 1, limit_window_seconds: 604800 },
          },
        };
      },
    });
    assert.ok(Date.now() - started < 1000);
    assert.equal(chatgpt.find((item) => item.account.id === "fast").ok, true);
    assert.equal(chatgpt.find((item) => item.account.id === "boom").error.code, "forbidden");
  });
});
