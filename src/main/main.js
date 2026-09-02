"use strict";

const { app, BrowserWindow, Menu, ipcMain, screen } = require("electron");
const path = require("path");
const fs = require("fs");
const Store = require("electron-store");

const { credentialPaths, allWatchedPaths } = require("../lib/paths");
const { loadFromSource } = require("../lib/credentials");
const { createVisibilityStore } = require("../lib/visibility");
const { shouldAutoPoll, AUTO_INTERVAL_MS } = require("../lib/refresh-window");
const { displayLabel } = require("../lib/mask");
const { createSession } = require("../lib/session");

const CHATGPT_USAGE_URL =
  process.env.QUOTA_CHATGPT_USAGE_URL || "https://chatgpt.com/backend-api/wham/usage";
const GROK_BILLING_URL =
  process.env.QUOTA_GROK_BILLING_URL || "https://cli-chat-proxy.grok.com/v1/billing?format=credits";

let mainWindow = null;
let store = null;
let visibility = null;
let session = null;

function createStore() {
  return new Store({
    name: "quota-hud",
    defaults: {
      source: "hermes",
      hidden: { hermes: { grok: [], chatgpt: [] }, terminal: { grok: [], chatgpt: [] } },
    },
  });
}

function readOnlyFs() {
  return {
    readFileSync: (file, enc) => fs.readFileSync(file, enc),
    existsSync: (file) => fs.existsSync(file),
  };
}

async function httpGetJson(url, headers) {
  const response = await fetch(url, {
    method: "GET",
    headers,
    signal: AbortSignal.timeout(20_000),
  });
  if (!response.ok) {
    const error = new Error(`http ${response.status}`);
    error.status = response.status;
    throw error;
  }
  return response.json();
}

async function fetchChatgpt(account) {
  const headers = {
    Authorization: `Bearer ${account.accessToken}`,
    Accept: "application/json",
  };
  if (account.accountId) {
    headers["ChatGPT-Account-Id"] = account.accountId;
    headers["ChatGPT-Account-ID"] = account.accountId;
  }
  return httpGetJson(CHATGPT_USAGE_URL, headers);
}

async function fetchGrok(account) {
  const headers = {
    Authorization: `Bearer ${account.accessToken}`,
    Accept: "application/json",
    "X-XAI-Token-Auth": "xai-grok-cli",
  };
  if (account.userId) headers["x-userid"] = String(account.userId);
  return httpGetJson(GROK_BILLING_URL, headers);
}

function snapshot() {
  return session.snapshot;
}

function isVisible(source, side, id) {
  return visibility.isVisible(source, side, id);
}

function currentState() {
  return session.currentState();
}

function broadcast() {
  if (!mainWindow || mainWindow.isDestroyed()) return;
  mainWindow.webContents.send("quota:state", currentState());
}

function resizeToContent() {
  if (!mainWindow || mainWindow.isDestroyed() || !session) return;
  const snap = snapshot();
  const grokVisible = snap.grokResults.filter((r) => isVisible(snap.source, "grok", r.account.id)).length;
  const gptVisible = snap.chatgptResults.filter((r) => isVisible(snap.source, "chatgpt", r.account.id)).length;
  const grokRows = snap.grokSideError ? 1 : Math.max(grokVisible, 1);
  const gptRows = snap.chatgptSideError ? 1 : Math.max(gptVisible, 1);
  const height = Math.min(720, 36 + grokRows * 168 + gptRows * 148 + 28);
  const width = 268;
  mainWindow.setContentSize(width, height);
}

function accountMenuItems(side) {
  const snap = snapshot();
  const accounts = side === "grok" ? snap.grokAccounts : snap.chatgptAccounts;
  return accounts.map((account) => {
    const id = account.id;
    return {
      label: displayLabel(account),
      type: "checkbox",
      checked: isVisible(snap.source, side, id),
      click: () => {
        const next = !isVisible(snap.source, side, id);
        visibility.setVisible(snap.source, side, id, next);
        broadcast();
        resizeToContent();
      },
    };
  });
}

function buildContextMenu() {
  const source = snapshot().source;
  const template = [
    {
      label: "Windows Hermes",
      type: "radio",
      checked: source === "hermes",
      click: () => setSource("hermes"),
    },
    {
      label: "Windows Terminal",
      type: "radio",
      checked: source === "terminal",
      click: () => setSource("terminal"),
    },
    { type: "separator" },
    { label: "立即刷新", click: () => refreshNow({ manual: true }) },
    { type: "separator" },
    { label: "Grok", enabled: false },
    ...accountMenuItems("grok"),
    { type: "separator" },
    { label: "ChatGPT", enabled: false },
    ...accountMenuItems("chatgpt"),
    { type: "separator" },
    { label: "退出", click: () => app.quit() },
  ];
  return Menu.buildFromTemplate(template);
}

function refreshNow(opts) {
  return session.refreshNow(opts).then((result) => result.state);
}

function setSource(source) {
  return session.setSource(source).then((result) => result.state);
}

function createWindow() {
  const point = screen.getCursorScreenPoint();
  const display = screen.getDisplayNearestPoint(point);
  const x = display.workArea.x + display.workArea.width - 292;
  const y = display.workArea.y + 24;

  const winOptions = {
    width: 268,
    height: 420,
    x,
    y,
    frame: false,
    transparent: true,
    alwaysOnTop: true,
    skipTaskbar: false,
    resizable: false,
    maximizable: false,
    fullscreenable: false,
    hasShadow: true,
    show: false,
    webPreferences: {
      preload: path.join(__dirname, "../preload/preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      backgroundThrottling: false,
    },
  };

  if (process.platform === "win32") {
    winOptions.backgroundMaterial = "acrylic";
    winOptions.roundedCorners = true;
  }

  mainWindow = new BrowserWindow(winOptions);
  mainWindow.setAlwaysOnTop(true, "screen-saver");
  mainWindow.loadFile(path.join(__dirname, "../renderer/index.html"));
  mainWindow.once("ready-to-show", () => {
    mainWindow.show();
    resizeToContent();
  });
  mainWindow.webContents.on("context-menu", () => {
    buildContextMenu().popup({ window: mainWindow });
  });
  if (process.env.QUOTA_WIDGET_DEVTOOLS === "1") {
    mainWindow.webContents.openDevTools({ mode: "detach" });
  }
}

function tickAutoRefresh() {
  if (!session || !session.loadedOnce || session.busy) return;
  if (!shouldAutoPoll({ now: new Date(), lastPollAt: session.lastAutoPollAt, intervalMs: AUTO_INTERVAL_MS })) {
    return;
  }
  refreshNow({ manual: false }).catch(() => {});
}

function registerIpc() {
  ipcMain.handle("quota:getState", () => currentState());
  ipcMain.handle("quota:refreshNow", () => refreshNow({ manual: true }));
  ipcMain.handle("quota:setSource", (_event, source) => setSource(source));
  ipcMain.handle("quota:setVisible", (_event, payload) => {
    visibility.setVisible(payload.source, payload.side, payload.id, payload.visible);
    broadcast();
    resizeToContent();
    return currentState();
  });
  ipcMain.handle("quota:quit", () => app.quit());
}

if (process.platform === "linux") {
  app.commandLine.appendSwitch("enable-transparent-visuals");
}

app.whenReady().then(async () => {
  if (process.platform === "win32") {
    app.setAppUserModelId("com.quotahud.widget");
  }
  Menu.setApplicationMenu(null);

  store = createStore();
  visibility = createVisibilityStore(store);
  session = createSession({
    visibility,
    loadSource: (source) =>
      loadFromSource(source, readOnlyFs(), credentialPaths(source, process.env, process.platform)),
    fetchGrok,
    fetchChatgpt,
    onChange: () => {
      broadcast();
      resizeToContent();
    },
  });
  registerIpc();
  createWindow();

  // Startup fetch once, even outside the 09:00–18:00 auto window.
  await refreshNow({ manual: true });
  setInterval(tickAutoRefresh, 30_000);

  void allWatchedPaths;
});

app.on("window-all-closed", () => app.quit());
