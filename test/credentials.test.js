"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const { parseChatgptAuth, parseGrokAuth, loadFromSource } = require("../src/lib/credentials");
const { fakeJwt } = require("./helpers");

describe("credential file shapes", () => {
  it("reads every Hermes openai-codex and xai-oauth pool entry", () => {
    const tokenA = fakeJwt({ email: "a@x.com", "https://api.openai.com/auth": { chatgpt_account_id: "org-a" } });
    const tokenB = fakeJwt({ email: "b@x.com" });
    const json = {
      credential_pool: {
        "openai-codex": [
          { id: "acctA", label: "account-A", access_token: tokenA },
          { id: "acctB", label: "account-B", access_token: tokenB },
        ],
        "xai-oauth": [
          { id: "g1", label: "grok-1", access_token: "grok-a" },
          { id: "g2", label: "grok-2", access_token: "grok-b" },
        ],
      },
    };
    assert.equal(parseChatgptAuth(json).length, 2);
    assert.equal(parseGrokAuth(json).length, 2);
    assert.equal(parseChatgptAuth(json)[0].accountId, "org-a");
    assert.equal(parseChatgptAuth(json)[0].email, "a@x.com");
  });

  it("does not hard-cap CLI auth.json at one account", () => {
    const json = {
      accounts: [
        { id: "one", tokens: { access_token: "t1", account_id: "org-1" } },
        { id: "two", tokens: { access_token: "t2", account_id: "org-2" } },
      ],
    };
    assert.equal(parseChatgptAuth(json).length, 2);
  });

  it("reads Grok CLI map entries", () => {
    const json = {
      "https://auth.x.ai::client": { key: "aaa", expires_at: "2999-01-01T00:00:00Z", user_id: "u1" },
      "https://auth.x.ai::other": { key: "bbb", expires_at: "2999-01-01T00:00:00Z", user_id: "u2" },
    };
    const accounts = parseGrokAuth(json);
    assert.equal(accounts.length, 2);
    assert.equal(accounts[0].accessToken, "aaa");
  });

  it("reports missing file vs empty pool separately", () => {
    const fsImpl = {
      readFileSync() {
        const err = new Error("missing");
        err.code = "ENOENT";
        throw err;
      },
    };
    const missing = loadFromSource("hermes", fsImpl, { hermes: "/nope" });
    assert.equal(missing.chatgpt.error.code, "missingFile");
    assert.equal(missing.grok.error.code, "missingFile");

    const emptyFs = {
      readFileSync: () => JSON.stringify({ credential_pool: { "openai-codex": [], "xai-oauth": [] } }),
    };
    const empty = loadFromSource("hermes", emptyFs, { hermes: "/empty" });
    assert.equal(empty.chatgpt.error.code, "emptyPool");
    assert.equal(empty.grok.error.code, "emptyPool");
  });

  it("empty xai-oauth pool does not ingest leftover ChatGPT tokens.access_token", () => {
    const chatgptSingleton = "chatgpt-singleton-token";
    const json = {
      tokens: { access_token: chatgptSingleton, account_id: "org-chatgpt" },
      credential_pool: {
        "openai-codex": [{ id: "codex-1", label: "codex", access_token: chatgptSingleton }],
        "xai-oauth": [],
      },
    };
    const grok = parseGrokAuth(json);
    assert.equal(grok.length, 0);
    assert.equal(
      grok.some((account) => account.accessToken === chatgptSingleton),
      false,
    );
    const loaded = loadFromSource(
      "hermes",
      { readFileSync: () => JSON.stringify(json) },
      { hermes: "/hermes-auth.json" },
    );
    assert.equal(loaded.grok.accounts.length, 0);
    assert.equal(loaded.grok.error.code, "emptyPool");
    assert.equal(
      loaded.grok.accounts.some((account) => account.accessToken === chatgptSingleton),
      false,
    );
  });

  it("empty openai-codex pool does not steal Grok singleton tokens", () => {
    const grokSingleton = "grok-singleton-token";
    const json = {
      tokens: { access_token: grokSingleton },
      providers: {
        "xai-oauth": { tokens: { access_token: grokSingleton } },
      },
      credential_pool: {
        "openai-codex": [],
        "xai-oauth": [{ id: "g1", label: "grok-pool", access_token: grokSingleton }],
      },
    };
    const chatgpt = parseChatgptAuth(json);
    assert.equal(chatgpt.length, 0);
    assert.equal(
      chatgpt.some((account) => account.accessToken === grokSingleton),
      false,
    );
  });
});
