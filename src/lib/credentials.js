"use strict";

const { decodeJwtPayload, emailFromClaims, chatgptAccountIdFromClaims } = require("./jwt");

function asObject(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : null;
}

function uniquePush(list, account) {
  if (!account) return;
  const key = `${account.side}:${account.id}`;
  if (list.some((item) => `${item.side}:${item.id}` === key)) return;
  list.push(account);
}

function tokenFromEntry(entry) {
  if (typeof entry === "string" && entry.trim()) return entry.trim();
  const obj = asObject(entry);
  if (!obj) return "";
  const tokens = asObject(obj.tokens) || asObject(obj.token);
  return (
    obj.access_token ||
    obj.accessToken ||
    obj.key ||
    tokens?.access_token ||
    tokens?.accessToken ||
    tokens?.key ||
    ""
  );
}

function emailFromEntry(entry, token) {
  const obj = asObject(entry) || {};
  const claims = decodeJwtPayload(token);
  return (
    obj.email ||
    obj.label_email ||
    asObject(obj.tokens)?.email ||
    emailFromClaims(claims) ||
    null
  );
}

function accountIdFromEntry(entry, token) {
  const obj = asObject(entry) || {};
  const tokens = asObject(obj.tokens) || {};
  const claims = decodeJwtPayload(token);
  return (
    obj.account_id ||
    obj.accountId ||
    obj.chatgpt_account_id ||
    obj.chatgptAccountId ||
    tokens.account_id ||
    tokens.accountId ||
    chatgptAccountIdFromClaims(claims) ||
    null
  );
}

function labelFromEntry(entry, fallback) {
  const obj = asObject(entry) || {};
  if (typeof obj.label === "string" && obj.label.trim()) return obj.label.trim();
  if (typeof obj.name === "string" && obj.name.trim()) return obj.name.trim();
  return fallback;
}

function makeAccount({ side, id, entry, token, extra = {} }) {
  const accessToken = (token || tokenFromEntry(entry) || "").trim();
  if (!accessToken) return null;
  const email = emailFromEntry(entry, accessToken);
  const accountId = accountIdFromEntry(entry, accessToken);
  return {
    side,
    id: String(id),
    label: labelFromEntry(entry, email || String(id)),
    email,
    accountId,
    accessToken,
    userId: asObject(entry)?.user_id || asObject(entry)?.userId || extra.userId || null,
    ...extra,
  };
}

function collectFromPool(json, provider, side) {
  const pool = asObject(json?.credential_pool) || asObject(json?.credentialPool);
  const entries = pool?.[provider];
  if (!Array.isArray(entries)) return [];
  const out = [];
  entries.forEach((entry, index) => {
    const id = entry?.id || entry?.label || `${provider}-${index}`;
    uniquePush(out, makeAccount({ side, id, entry }));
  });
  return out;
}

function collectCodexSingleton(json) {
  const tokens = asObject(json?.tokens);
  if (!tokens && !json?.access_token) return [];
  const entry = tokens ? { ...json, tokens } : json;
  const id = accountIdFromEntry(entry, tokenFromEntry(entry)) || "codex-default";
  const account = makeAccount({ side: "chatgpt", id, entry });
  return account ? [account] : [];
}

function collectAccountsArray(json, side) {
  const list = Array.isArray(json?.accounts) ? json.accounts : Array.isArray(json) ? json : null;
  if (!list) return [];
  const out = [];
  list.forEach((entry, index) => {
    const nested = asObject(entry?.auth) || entry;
    const id =
      entry?.id ||
      entry?.accountId ||
      entry?.account_id ||
      accountIdFromEntry(nested, tokenFromEntry(nested)) ||
      `${side}-${index}`;
    uniquePush(out, makeAccount({ side, id, entry: nested }));
  });
  return out;
}

function collectGrokCliMap(json) {
  const obj = asObject(json);
  if (!obj) return [];
  const out = [];
  for (const [key, value] of Object.entries(obj)) {
    if (
      key === "credential_pool" ||
      key === "credentialPool" ||
      key === "providers" ||
      key === "tokens" ||
      key === "version" ||
      key === "accounts" ||
      key === "auth_mode" ||
      key === "last_refresh" ||
      key === "OPENAI_API_KEY"
    ) {
      continue;
    }
    const entry = asObject(value);
    if (!entry) continue;
    if (!entry.key && !entry.access_token && !asObject(entry.tokens)) continue;
    uniquePush(
      out,
      makeAccount({
        side: "grok",
        id: entry.user_id || entry.userId || key,
        entry,
        extra: { userId: entry.user_id || entry.userId || null },
      }),
    );
  }
  return out;
}

function collectProviderSingleton(json, provider, side) {
  const providers = asObject(json?.providers);
  const state = asObject(providers?.[provider]);
  if (!state) return [];
  const entry = asObject(state.tokens) ? { ...state, tokens: state.tokens } : state;
  const id = state.label || accountIdFromEntry(entry, tokenFromEntry(entry)) || provider;
  const account = makeAccount({ side, id, entry });
  return account ? [account] : [];
}

function parseChatgptAuth(json) {
  const fromPool = collectFromPool(json, "openai-codex", "chatgpt");
  if (fromPool.length) return fromPool;
  const fromAccounts = collectAccountsArray(json, "chatgpt");
  if (fromAccounts.length) return fromAccounts;
  const fromSingleton = collectCodexSingleton(json);
  if (fromSingleton.length) return fromSingleton;
  return collectProviderSingleton(json, "openai-codex", "chatgpt");
}

function parseGrokAuth(json) {
  const fromPool = collectFromPool(json, "xai-oauth", "grok");
  if (fromPool.length) return fromPool;
  const fromAccounts = collectAccountsArray(json, "grok");
  if (fromAccounts.length) return fromAccounts;
  const fromMap = collectGrokCliMap(json);
  if (fromMap.length) return fromMap;
  const fromTokens = collectCodexSingleton({ tokens: json?.tokens, ...json });
  if (fromTokens.length) {
    return fromTokens.map((item) => ({ ...item, side: "grok", id: item.id === "codex-default" ? "grok-default" : item.id }));
  }
  return collectProviderSingleton(json, "xai-oauth", "grok");
}

function readJsonFile(fsImpl, filePath) {
  try {
    const raw = fsImpl.readFileSync(filePath, "utf8");
    try {
      return { ok: true, json: JSON.parse(raw), path: filePath };
    } catch (error) {
      const err = new Error("corrupt");
      err.code = "missingFile";
      err.cause = error;
      err.path = filePath;
      return { ok: false, error: err, path: filePath };
    }
  } catch (error) {
    const err = new Error("missing");
    err.code = error.code === "ENOENT" ? "missingFile" : "missingFile";
    err.cause = error;
    err.path = filePath;
    return { ok: false, error: err, path: filePath };
  }
}

function loadFromSource(source, fsImpl, paths) {
  if (source === "hermes") {
    const file = readJsonFile(fsImpl, paths.hermes || paths.chatgpt);
    if (!file.ok) {
      return {
        source,
        grok: { accounts: [], error: file.error },
        chatgpt: { accounts: [], error: file.error },
        filesRead: [file.path],
      };
    }
    const chatgpt = parseChatgptAuth(file.json);
    const grok = parseGrokAuth(file.json);
    return {
      source,
      grok: { accounts: grok, error: grok.length ? null : Object.assign(new Error("empty"), { code: "emptyPool" }) },
      chatgpt: {
        accounts: chatgpt,
        error: chatgpt.length ? null : Object.assign(new Error("empty"), { code: "emptyPool" }),
      },
      filesRead: [file.path],
    };
  }

  const chatgptFile = readJsonFile(fsImpl, paths.chatgpt);
  const grokFile = readJsonFile(fsImpl, paths.grok);
  let chatgptAccounts = [];
  let chatgptError = null;
  if (!chatgptFile.ok) chatgptError = chatgptFile.error;
  else {
    chatgptAccounts = parseChatgptAuth(chatgptFile.json);
    if (!chatgptAccounts.length) chatgptError = Object.assign(new Error("empty"), { code: "emptyPool" });
  }

  let grokAccounts = [];
  let grokError = null;
  if (!grokFile.ok) grokError = grokFile.error;
  else {
    grokAccounts = parseGrokAuth(grokFile.json);
    if (!grokAccounts.length) grokError = Object.assign(new Error("empty"), { code: "emptyPool" });
  }

  return {
    source,
    grok: { accounts: grokAccounts, error: grokError },
    chatgpt: { accounts: chatgptAccounts, error: chatgptError },
    filesRead: [chatgptFile.path, grokFile.path],
  };
}

module.exports = {
  parseChatgptAuth,
  parseGrokAuth,
  loadFromSource,
  tokenFromEntry,
};
