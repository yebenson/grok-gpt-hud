"use strict";

const path = require("path");
const os = require("os");

function homeDir(env = process.env, platform = process.platform) {
  if (env.QUOTA_WIDGET_HOME) return env.QUOTA_WIDGET_HOME;
  if (platform === "win32") {
    return env.USERPROFILE || env.HOME || os.homedir();
  }
  return env.HOME || os.homedir();
}

function credentialPaths(source, env = process.env, platform = process.platform) {
  const home = homeDir(env, platform);
  if (source === "hermes") {
    return {
      source: "hermes",
      hermes: path.join(home, ".hermes", "auth.json"),
      chatgpt: path.join(home, ".hermes", "auth.json"),
      grok: path.join(home, ".hermes", "auth.json"),
    };
  }
  return {
    source: "terminal",
    chatgpt: path.join(home, ".codex", "auth.json"),
    grok: path.join(home, ".grok", "auth.json"),
  };
}

function allWatchedPaths(env = process.env, platform = process.platform) {
  const home = homeDir(env, platform);
  return [
    path.join(home, ".hermes", "auth.json"),
    path.join(home, ".codex", "auth.json"),
    path.join(home, ".grok", "auth.json"),
  ];
}

module.exports = { homeDir, credentialPaths, allWatchedPaths };
