"use strict";

function maskEmail(email) {
  if (typeof email !== "string" || !email.includes("@")) return email || "";
  const at = email.lastIndexOf("@");
  const local = email.slice(0, at);
  const domain = email.slice(at + 1);
  if (!local) return `****@${domain}`;
  if (local.length === 1) return `${local}****@${domain}`;
  if (local.length === 2) return `${local[0]}****${local[1]}@${domain}`;
  return `${local[0]}****${local[local.length - 1]}@${domain}`;
}

function displayLabel(account) {
  if (!account) return "账号";
  const masked = account.email ? maskEmail(account.email) : "";
  if (masked) return masked;
  if (typeof account.label === "string" && account.label.trim()) return account.label.trim();
  if (typeof account.id === "string" && account.id.trim()) {
    const id = account.id.trim();
    return id.length > 12 ? `${id.slice(0, 6)}…` : id;
  }
  return "账号";
}

module.exports = { maskEmail, displayLabel };
