"use strict";

function formatReset(resetAt, now = new Date()) {
  if (resetAt == null) return "";
  let date;
  if (typeof resetAt === "number") {
    date = new Date(resetAt > 1e12 ? resetAt : resetAt * 1000);
  } else if (typeof resetAt === "object" && resetAt.afterSeconds != null) {
    date = new Date(now.getTime() + Number(resetAt.afterSeconds) * 1000);
  } else if (typeof resetAt === "string") {
    date = new Date(resetAt);
  } else {
    return "";
  }
  if (Number.isNaN(date.getTime())) return "";
  const text = date.toLocaleString("zh-CN", {
    month: "numeric",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
  return `重置 ${text}`;
}

function redactAccount(account) {
  if (!account) return null;
  const { accessToken, ...rest } = account;
  void accessToken;
  return rest;
}

function toRendererAccount(result, visible) {
  const account = redactAccount(result.account);
  const base = {
    id: account.id,
    side: account.side,
    label: account.label,
    email: account.email,
    visible,
  };
  if (!result.ok) {
    return {
      ...base,
      error: result.error?.message || "拉取失败",
      quota: result.quota || null,
    };
  }
  return {
    ...base,
    error: null,
    quota: result.quota,
  };
}

function buildDashboardState({
  source,
  phase,
  loaded,
  grokSideError,
  chatgptSideError,
  grokResults,
  chatgptResults,
  isVisible,
}) {
  const grokAll = grokResults.map((result) =>
    toRendererAccount(result, isVisible(source, "grok", result.account.id)),
  );
  const chatgptAll = chatgptResults.map((result) =>
    toRendererAccount(result, isVisible(source, "chatgpt", result.account.id)),
  );

  return {
    phase,
    loaded: Boolean(loaded),
    source,
    sourceLabel: source === "terminal" ? "Windows Terminal" : "Windows Hermes",
    grok: {
      sideError: grokSideError,
      accounts: grokAll.filter((item) => item.visible),
      all: grokAll.map(({ id, label, email, visible, error }) => ({ id, label, email, visible, error })),
    },
    chatgpt: {
      sideError: chatgptSideError,
      accounts: chatgptAll.filter((item) => item.visible),
      all: chatgptAll.map(({ id, label, email, visible, error }) => ({ id, label, email, visible, error })),
    },
  };
}

module.exports = { formatReset, buildDashboardState, redactAccount };
