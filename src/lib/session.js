"use strict";

const { pollAccounts } = require("./poll");
const { createBackoffTracker } = require("./backoff");
const { classifyHttpError, MESSAGES } = require("./errors");
const { buildDashboardState } = require("./dashboard");
const { isAutoRefreshAllowed } = require("./refresh-window");

function sideErrorMessage(error) {
  if (!error) return null;
  return classifyHttpError(error).message;
}

function emptySnapshot(source = "hermes") {
  return {
    source,
    grokAccounts: [],
    chatgptAccounts: [],
    grokSideError: null,
    chatgptSideError: null,
    grokResults: [],
    chatgptResults: [],
    filesRead: [],
  };
}

function applyLoaded(snapshot, source, loaded) {
  snapshot.source = source;
  snapshot.filesRead = loaded.filesRead || [];
  snapshot.grokAccounts = loaded.grok.accounts;
  snapshot.chatgptAccounts = loaded.chatgpt.accounts;
  snapshot.grokSideError = sideErrorMessage(loaded.grok.error);
  snapshot.chatgptSideError = sideErrorMessage(loaded.chatgpt.error);
}

function createSession({
  visibility,
  loadSource,
  fetchGrok,
  fetchChatgpt,
  onChange,
} = {}) {
  if (!visibility || typeof loadSource !== "function") {
    throw new Error("createSession requires visibility and loadSource");
  }

  const backoff = createBackoffTracker();
  const snapshot = emptySnapshot(visibility.getSource());
  let generation = 0;
  let inflight = null;
  let loadedOnce = false;
  let lastAutoPollAt = null;

  function isVisible(source, side, id) {
    return visibility.isVisible(source, side, id);
  }

  function currentState(phase = loadedOnce ? "ready" : "loading") {
    return buildDashboardState({
      source: snapshot.source,
      phase,
      loaded: loadedOnce,
      grokSideError: snapshot.grokSideError,
      chatgptSideError: snapshot.chatgptSideError,
      grokResults: snapshot.grokResults,
      chatgptResults: snapshot.chatgptResults,
      isVisible,
    });
  }

  function notify() {
    if (typeof onChange === "function") onChange(currentState());
  }

  function accountIds(results) {
    return (results || []).map((item) => item.account?.id).filter(Boolean);
  }

  async function runPoll(grokAccounts, chatgptAccounts, { ignoreBackoff = false } = {}) {
    const grokToFetch = grokAccounts.filter((account) => ignoreBackoff || !backoff.shouldSkip(account.id));
    const chatgptToFetch = chatgptAccounts.filter((account) => ignoreBackoff || !backoff.shouldSkip(account.id));
    const { grok, chatgpt } = await pollAccounts({
      grokAccounts: grokToFetch,
      chatgptAccounts: chatgptToFetch,
      fetchGrok,
      fetchChatgpt,
    });
    const grokById = new Map(grok.map((item) => [item.account.id, item]));
    const chatgptById = new Map(chatgpt.map((item) => [item.account.id, item]));
    for (const result of grok) {
      if (result.ok) backoff.recordSuccess(result.account.id);
      else backoff.recordFailure(result.account.id);
    }
    for (const result of chatgpt) {
      if (result.ok) backoff.recordSuccess(result.account.id);
      else backoff.recordFailure(result.account.id);
    }
    return {
      grok: grokAccounts.map((account) => {
        if (grokById.has(account.id)) return grokById.get(account.id);
        return { account, ok: false, error: { code: "network", message: MESSAGES.retry }, quota: null };
      }),
      chatgpt: chatgptAccounts.map((account) => {
        if (chatgptById.has(account.id)) return chatgptById.get(account.id);
        return { account, ok: false, error: { code: "network", message: MESSAGES.retry }, quota: null };
      }),
    };
  }

  async function refreshNow({ manual = false } = {}) {
    const myGen = (generation += 1);
    if (!manual) lastAutoPollAt = Date.now();
    const work = (async () => {
      const source = visibility.getSource();
      const loaded = loadSource(source);
      if (myGen !== generation) return { discarded: true, state: currentState() };
      applyLoaded(snapshot, source, loaded);
      const grokAccounts = snapshot.grokAccounts.slice();
      const chatgptAccounts = snapshot.chatgptAccounts.slice();
      const hasAccounts = grokAccounts.length + chatgptAccounts.length > 0;
      if (!hasAccounts) {
        snapshot.grokResults = [];
        snapshot.chatgptResults = [];
        loadedOnce = true;
        notify();
        return { discarded: false, state: currentState() };
      }
      if (!manual) notify();
      const polled = await runPoll(grokAccounts, chatgptAccounts, { ignoreBackoff: manual });
      if (myGen !== generation) return { discarded: true, state: currentState() };
      snapshot.grokResults = polled.grok;
      snapshot.chatgptResults = polled.chatgpt;
      loadedOnce = true;
      if (manual || isAutoRefreshAllowed(new Date())) lastAutoPollAt = Date.now();
      notify();
      return { discarded: false, state: currentState() };
    })();
    inflight = work;
    try {
      return await work;
    } finally {
      if (inflight === work) inflight = null;
    }
  }

  async function setSource(source) {
    generation += 1;
    visibility.setSource(source);
    loadedOnce = false;
    snapshot.source = source === "terminal" ? "terminal" : "hermes";
    snapshot.grokAccounts = [];
    snapshot.chatgptAccounts = [];
    snapshot.grokResults = [];
    snapshot.chatgptResults = [];
    snapshot.grokSideError = null;
    snapshot.chatgptSideError = null;
    notify();
    return refreshNow({ manual: true });
  }

  return {
    snapshot,
    refreshNow,
    setSource,
    currentState,
    accountIds,
    get generation() {
      return generation;
    },
    get loadedOnce() {
      return loadedOnce;
    },
    get lastAutoPollAt() {
      return lastAutoPollAt;
    },
    get busy() {
      return inflight != null;
    },
  };
}

module.exports = { createSession };
