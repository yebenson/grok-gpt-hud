"use strict";

function normalizeHidden(raw) {
  const out = { hermes: { grok: [], chatgpt: [] }, terminal: { grok: [], chatgpt: [] } };
  if (!raw || typeof raw !== "object") return out;
  for (const source of ["hermes", "terminal"]) {
    const sideMap = raw[source];
    if (!sideMap || typeof sideMap !== "object") continue;
    for (const side of ["grok", "chatgpt"]) {
      const list = sideMap[side];
      if (Array.isArray(list)) {
        out[source][side] = [...new Set(list.map(String))];
      }
    }
  }
  return out;
}

function createVisibilityStore(store) {
  if (!store || typeof store.get !== "function" || typeof store.set !== "function") {
    throw new Error("visibility store requires get/set");
  }

  function readHidden() {
    return normalizeHidden(store.get("hidden"));
  }

  function writeHidden(hidden) {
    store.set("hidden", normalizeHidden(hidden));
  }

  return {
    getSource() {
      const value = store.get("source");
      return value === "terminal" ? "terminal" : "hermes";
    },
    setSource(source) {
      store.set("source", source === "terminal" ? "terminal" : "hermes");
    },
    isVisible(source, side, id) {
      const hidden = readHidden();
      const list = hidden[source]?.[side] || [];
      return !list.includes(String(id));
    },
    setVisible(source, side, id, visible) {
      const hidden = readHidden();
      const key = String(id);
      const list = new Set(hidden[source][side]);
      if (visible) list.delete(key);
      else list.add(key);
      hidden[source][side] = [...list];
      writeHidden(hidden);
    },
    listHidden(source, side) {
      return [...(readHidden()[source]?.[side] || [])];
    },
    dump() {
      return {
        source: this.getSource(),
        hidden: readHidden(),
      };
    },
  };
}

module.exports = { createVisibilityStore, normalizeHidden };
