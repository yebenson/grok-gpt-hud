"use strict";

const loadingEl = document.getElementById("loading");
const dashboardEl = document.getElementById("dashboard");
const grokAccountsEl = document.getElementById("grokAccounts");
const gptAccountsEl = document.getElementById("gptAccounts");
const sourcePill = document.getElementById("sourcePill");
const grokLogo = document.querySelector(".grok-logo");
const gptLogo = document.querySelector(".gpt-logo");

function api() {
  return window.quotaWidget;
}

function colorClass(color) {
  if (color === "blue") return "color-blue";
  if (color === "yellow") return "color-yellow";
  if (color === "red") return "color-red";
  return "";
}

function sideBlock(message, extraClass = "") {
  const div = document.createElement("div");
  div.className = `error ${extraClass}`.trim();
  div.textContent = message;
  return div;
}

function grokCard(account) {
  const wrap = document.createElement("div");
  wrap.className = "account";
  const email = document.createElement("div");
  email.className = "email";
  email.textContent = account.email ? maskFallback(account) : account.label;
  wrap.appendChild(email);
  if (account.error) {
    wrap.appendChild(sideBlock(account.error));
    return wrap;
  }
  const gauge = document.createElement("div");
  gauge.className = `gauge ${colorClass(account.quota?.color)}`;
  gauge.innerHTML = window.QuotaGauges.needleGauge(account.quota || {});
  wrap.appendChild(gauge);
  if (account.quota?.resetLabel) {
    const reset = document.createElement("div");
    reset.className = "reset";
    reset.textContent = account.quota.resetLabel;
    wrap.appendChild(reset);
  }
  return wrap;
}

function gptCard(account) {
  const wrap = document.createElement("div");
  wrap.className = "account";
  const email = document.createElement("div");
  email.className = "email";
  email.textContent = account.email ? maskFallback(account) : account.label;
  wrap.appendChild(email);
  if (account.error) {
    wrap.appendChild(sideBlock(account.error));
    return wrap;
  }
  const row = document.createElement("div");
  row.className = "gauges";
  const five = document.createElement("div");
  five.className = `gauge ${colorClass(account.quota?.fiveHour?.color)}`;
  five.innerHTML = window.QuotaGauges.ringGauge({
    ...(account.quota?.fiveHour || {}),
    caption: "5h",
  });
  const week = document.createElement("div");
  week.className = `gauge ${colorClass(account.quota?.weekly?.color)}`;
  week.innerHTML = window.QuotaGauges.ringGauge({
    ...(account.quota?.weekly || {}),
    caption: "7d",
  });
  row.appendChild(five);
  row.appendChild(week);
  wrap.appendChild(row);
  const resetBits = [account.quota?.fiveHour?.resetLabel, account.quota?.weekly?.resetLabel].filter(Boolean);
  if (resetBits.length) {
    const reset = document.createElement("div");
    reset.className = "reset";
    reset.textContent = resetBits[0];
    wrap.appendChild(reset);
  }
  return wrap;
}

function maskFallback(account) {
  const email = account.email || "";
  if (!email.includes("@")) return account.label || email;
  const at = email.lastIndexOf("@");
  const local = email.slice(0, at);
  const domain = email.slice(at + 1);
  if (local.length <= 2) return `${local[0] || "*"}****@${domain}`;
  return `${local[0]}****${local[local.length - 1]}@${domain}`;
}

function firstColor(accounts, path) {
  for (const account of accounts) {
    const node = path(account);
    if (node?.color) return node.color;
  }
  return null;
}

function render(state) {
  if (!state) return;
  sourcePill.textContent = state.source === "terminal" ? "Terminal" : "Hermes";
  const ready = state.phase === "ready" && state.loaded;
  loadingEl.classList.toggle("hidden", ready);
  dashboardEl.classList.toggle("hidden", !ready);
  if (!ready) return;

  grokAccountsEl.innerHTML = "";
  if (state.grok.sideError && state.grok.accounts.length === 0) {
    grokAccountsEl.appendChild(sideBlock(state.grok.sideError, "side-error"));
  } else if (state.grok.accounts.length === 0) {
    grokAccountsEl.appendChild(sideBlock("无可见账号"));
  } else {
    for (const account of state.grok.accounts) grokAccountsEl.appendChild(grokCard(account));
  }

  gptAccountsEl.innerHTML = "";
  if (state.chatgpt.sideError && state.chatgpt.accounts.length === 0) {
    gptAccountsEl.appendChild(sideBlock(state.chatgpt.sideError, "side-error"));
  } else if (state.chatgpt.accounts.length === 0) {
    gptAccountsEl.appendChild(sideBlock("无可见账号"));
  } else {
    for (const account of state.chatgpt.accounts) gptAccountsEl.appendChild(gptCard(account));
  }

  const grokColor = firstColor(state.grok.accounts, (a) => a.quota);
  const gptColor = firstColor(state.chatgpt.accounts, (a) => a.quota?.fiveHour) || firstColor(state.chatgpt.accounts, (a) => a.quota?.weekly);
  grokLogo.className = `logo grok-logo ${colorClass(grokColor)}`;
  gptLogo.className = `logo gpt-logo ${colorClass(gptColor)}`;
}

async function boot() {
  const bridge = api();
  if (!bridge) return;
  if (bridge.onState) bridge.onState(render);
  try {
    const state = await bridge.getState();
    render(state);
  } catch {
    render({
      phase: "ready",
      loaded: true,
      source: "hermes",
      grok: { sideError: "拉取失败", accounts: [] },
      chatgpt: { sideError: "拉取失败", accounts: [] },
    });
  }
}

boot();
