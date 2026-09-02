"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const os = require("os");
const path = require("path");
const { loadFromSource } = require("../src/lib/credentials");
const { fakeJwt } = require("./helpers");

function trackingFs(rootFiles) {
  const writes = [];
  const impl = {
    writes,
    readFileSync(file, enc) {
      if (!Object.prototype.hasOwnProperty.call(rootFiles, file)) {
        const err = new Error("ENOENT");
        err.code = "ENOENT";
        throw err;
      }
      return enc ? JSON.stringify(rootFiles[file], null, 2) : Buffer.from(JSON.stringify(rootFiles[file]));
    },
    existsSync(file) {
      return Object.prototype.hasOwnProperty.call(rootFiles, file);
    },
    writeFileSync(file, contents) {
      writes.push({ op: "writeFileSync", file, contents });
      throw new Error("credential files are read-only");
    },
    mkdirSync(file) {
      writes.push({ op: "mkdirSync", file });
      throw new Error("credential files are read-only");
    },
  };
  return impl;
}

describe("source switch does not write credential files", () => {
  it("re-reads Hermes and Terminal files without writing them", () => {
    const home = fs.mkdtempSync(path.join(os.tmpdir(), "quota-src-"));
    const hermes = path.join(home, ".hermes", "auth.json");
    const codex = path.join(home, ".codex", "auth.json");
    const grok = path.join(home, ".grok", "auth.json");
    const token = fakeJwt({
      email: "benson@outlook.com",
      "https://api.openai.com/auth": { chatgpt_account_id: "acct-1" },
    });

    const files = {
      [hermes]: {
        credential_pool: {
          "openai-codex": [{ id: "h-gpt", label: "hermes gpt", access_token: token }],
          "xai-oauth": [{ id: "h-grok", label: "hermes grok", access_token: "grok-token" }],
        },
      },
      [codex]: { tokens: { access_token: token, account_id: "acct-1" } },
      [grok]: {
        "https://auth.x.ai::client": { key: "cli-grok-token", expires_at: "2999-01-01T00:00:00Z", user_id: "u1" },
      },
    };
    const before = structuredClone(files);
    const fsImpl = trackingFs(files);

    const hermesLoaded = loadFromSource("hermes", fsImpl, { hermes, chatgpt: hermes, grok: hermes });
    const terminalLoaded = loadFromSource("terminal", fsImpl, { chatgpt: codex, grok });

    assert.equal(hermesLoaded.chatgpt.accounts.length, 1);
    assert.equal(hermesLoaded.grok.accounts.length, 1);
    assert.equal(terminalLoaded.chatgpt.accounts.length, 1);
    assert.equal(terminalLoaded.grok.accounts.length, 1);
    assert.deepEqual(fsImpl.writes, []);
    assert.deepEqual(files, before);
  });
});
