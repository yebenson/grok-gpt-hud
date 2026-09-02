"use strict";

const MESSAGES = {
  missingFile: "读不到凭证",
  emptyPool: "池里没有账号",
  expired: "凭证过期，去 Hermes 或对应 CLI 重新登录",
  forbidden: "接口拒绝",
  network: "拉取失败",
  retry: "拉取失败，将自动重试",
  parse: "额度数据无法解析",
  missingQuota: "没有额度数据",
};

function classifyHttpError(error) {
  if (!error) return { code: "network", message: MESSAGES.retry };
  const status = error.status || error.statusCode || error.httpStatus;
  if (status === 401) return { code: "expired", message: MESSAGES.expired };
  if (status === 403) return { code: "forbidden", message: MESSAGES.forbidden };
  if (error.code === "ENOENT" || error.code === "missingFile") {
    return { code: "missingFile", message: MESSAGES.missingFile };
  }
  if (error.code === "emptyPool") {
    return { code: "emptyPool", message: MESSAGES.emptyPool };
  }
  if (error.code === "ECONNREFUSED" || error.code === "ENOTFOUND" || error.code === "ETIMEDOUT") {
    return { code: "network", message: MESSAGES.retry };
  }
  if (error.name === "AbortError" || error.cause?.code === "UND_ERR_CONNECT_TIMEOUT") {
    return { code: "network", message: MESSAGES.retry };
  }
  if (typeof error.message === "string" && /fetch|network|socket|EAI_AGAIN/i.test(error.message)) {
    return { code: "network", message: MESSAGES.retry };
  }
  return { code: "network", message: MESSAGES.retry };
}

module.exports = { MESSAGES, classifyHttpError };
