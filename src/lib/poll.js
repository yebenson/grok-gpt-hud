"use strict";

const { classifyHttpError, MESSAGES } = require("./errors");
const { parseChatgptUsage, parseGrokBilling, decorateNeedle } = require("./quota");
const { formatReset } = require("./dashboard");

async function mapIsolated(items, mapper) {
  const settled = await Promise.allSettled(items.map((item) => Promise.resolve().then(() => mapper(item))));
  return settled.map((result, index) => {
    if (result.status === "fulfilled") return result.value;
    const classified = classifyHttpError(result.reason);
    return {
      account: items[index],
      ok: false,
      error: classified,
      quota: null,
    };
  });
}

async function pollAccounts({ grokAccounts, chatgptAccounts, fetchGrok, fetchChatgpt }) {
  const grokTask = mapIsolated(grokAccounts, async (account) => {
    const payload = await fetchGrok(account);
    const parsed = parseGrokBilling(payload);
    if (parsed.missing || parsed.remainingPercent == null) {
      return {
        account,
        ok: false,
        error: { code: "parse", message: MESSAGES.missingQuota },
        quota: decorateNeedle(parsed),
      };
    }
    return {
      account,
      ok: true,
      error: null,
      quota: { ...decorateNeedle(parsed), resetLabel: formatReset(parsed.resetAt) },
    };
  });

  const chatgptTask = mapIsolated(chatgptAccounts, async (account) => {
    const payload = await fetchChatgpt(account);
    const parsed = parseChatgptUsage(payload);
    return {
      account,
      ok: true,
      error: null,
      quota: {
        fiveHour: { ...decorateNeedle(parsed.fiveHour), resetLabel: formatReset(parsed.fiveHour.resetAt) },
        weekly: { ...decorateNeedle(parsed.weekly), resetLabel: formatReset(parsed.weekly.resetAt) },
        planType: parsed.planType,
      },
    };
  });

  const [grok, chatgpt] = await Promise.all([grokTask, chatgptTask]);
  return { grok, chatgpt };
}

module.exports = { pollAccounts, mapIsolated };
