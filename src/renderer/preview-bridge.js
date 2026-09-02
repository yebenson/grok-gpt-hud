"use strict";

const fixture = {
  phase: "ready",
  loaded: true,
  source: "hermes",
  sourceLabel: "Windows Hermes",
  grok: {
    sideError: null,
    accounts: [
      {
        id: "grok-1",
        side: "grok",
        label: "b****n@outlook.com",
        email: "benson@outlook.com",
        visible: true,
        error: null,
        quota: {
          remainingPercent: 72,
          unlimited: false,
          display: "72%",
          color: "blue",
          hex: "#4da3ff",
          resetLabel: "重置 16:32",
        },
      },
    ],
  },
  chatgpt: {
    sideError: null,
    accounts: [
      {
        id: "gpt-1",
        side: "chatgpt",
        label: "b****n@outlook.com",
        email: "benson@outlook.com",
        visible: true,
        error: null,
        quota: {
          fiveHour: {
            remainingPercent: 81,
            unlimited: false,
            display: "81%",
            color: "blue",
            hex: "#4da3ff",
            resetLabel: "重置 14:05",
          },
          weekly: {
            remainingPercent: 38,
            unlimited: false,
            display: "38%",
            color: "yellow",
            hex: "#f5c542",
            resetLabel: "重置 周六 09:00",
          },
        },
      },
      {
        id: "gpt-2",
        side: "chatgpt",
        label: "w****e@gmail.com",
        email: "wayne@gmail.com",
        visible: true,
        error: null,
        quota: {
          fiveHour: {
            remainingPercent: null,
            unlimited: true,
            display: "∞",
            color: "blue",
            hex: "#4da3ff",
            resetLabel: "",
          },
          weekly: {
            remainingPercent: 12,
            unlimited: false,
            display: "12%",
            color: "red",
            hex: "#ff5a5f",
            resetLabel: "重置 周日 21:00",
          },
        },
      },
    ],
  },
};

window.quotaWidget = {
  getState: async () => structuredClone(fixture),
  onState: (handler) => {
    handler(structuredClone(fixture));
    return () => {};
  },
  refreshNow: async () => structuredClone(fixture),
  setSource: async () => structuredClone(fixture),
  setVisible: async () => structuredClone(fixture),
  quit: async () => {},
};
