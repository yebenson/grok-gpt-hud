"use strict";

const { describe, it } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const os = require("os");
const path = require("path");
const { createVisibilityStore } = require("../src/lib/visibility");
const { memoryStore } = require("./helpers");

describe("hide-list persistence without writing auth.json", () => {
  it("stores visibility only in the widget store", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "quota-hud-"));
    const authPath = path.join(dir, "auth.json");
    const original = JSON.stringify(
      {
        credential_pool: {
          "openai-codex": [{ id: "acctA", label: "A", access_token: "tok-a" }],
        },
      },
      null,
      2,
    );
    fs.writeFileSync(authPath, original);
    const before = fs.readFileSync(authPath, "utf8");

    const store = memoryStore();
    const visibility = createVisibilityStore(store);
    visibility.setSource("hermes");
    visibility.setVisible("hermes", "chatgpt", "acctA", false);
    visibility.setVisible("hermes", "grok", "g1", false);

    assert.equal(visibility.isVisible("hermes", "chatgpt", "acctA"), false);
    assert.equal(visibility.isVisible("hermes", "chatgpt", "acctB"), true);
    assert.deepEqual(visibility.listHidden("hermes", "chatgpt"), ["acctA"]);
    assert.equal(store.data.source, "hermes");
    assert.ok(store.data.hidden.hermes.chatgpt.includes("acctA"));
    assert.equal(fs.readFileSync(authPath, "utf8"), before);
    assert.equal(JSON.stringify(store.data).includes("tok-a"), false);
  });

  it("keeps terminal and hermes hide lists independent", () => {
    const visibility = createVisibilityStore(memoryStore());
    visibility.setVisible("hermes", "chatgpt", "shared", false);
    assert.equal(visibility.isVisible("terminal", "chatgpt", "shared"), true);
    assert.equal(visibility.isVisible("hermes", "chatgpt", "shared"), false);
  });
});
