"use strict";

function decodeJwtPayload(token) {
  if (typeof token !== "string" || !token.includes(".")) return {};
  const parts = token.split(".");
  if (parts.length < 2) return {};
  try {
    const padded = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const pad = padded.length % 4 === 0 ? "" : "=".repeat(4 - (padded.length % 4));
    const json = Buffer.from(padded + pad, "base64").toString("utf8");
    const parsed = JSON.parse(json);
    return parsed && typeof parsed === "object" ? parsed : {};
  } catch {
    return {};
  }
}

function nested(obj, path) {
  let cur = obj;
  for (const key of path) {
    if (!cur || typeof cur !== "object") return undefined;
    cur = cur[key];
  }
  return cur;
}

function firstString(...values) {
  for (const value of values) {
    if (typeof value === "string" && value.trim()) return value.trim();
  }
  return null;
}

function emailFromClaims(claims) {
  if (!claims || typeof claims !== "object") return null;
  const profile = claims["https://api.openai.com/profile"] || {};
  return firstString(
    claims.email,
    claims.preferred_username,
    claims.upn,
    profile.email,
    profile.email_address,
  );
}

function chatgptAccountIdFromClaims(claims) {
  if (!claims || typeof claims !== "object") return null;
  const auth = claims["https://api.openai.com/auth"] || {};
  return firstString(
    claims.chatgpt_account_id,
    claims.account_id,
    auth.chatgpt_account_id,
    auth.account_id,
  );
}

module.exports = {
  decodeJwtPayload,
  emailFromClaims,
  chatgptAccountIdFromClaims,
};
