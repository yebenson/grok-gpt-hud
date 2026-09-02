"use strict";

const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("quotaWidget", {
  getState: () => ipcRenderer.invoke("quota:getState"),
  onState: (handler) => {
    const listener = (_event, state) => handler(state);
    ipcRenderer.on("quota:state", listener);
    return () => ipcRenderer.removeListener("quota:state", listener);
  },
  refreshNow: () => ipcRenderer.invoke("quota:refreshNow"),
  setSource: (source) => ipcRenderer.invoke("quota:setSource", source),
  setVisible: (payload) => ipcRenderer.invoke("quota:setVisible", payload),
  quit: () => ipcRenderer.invoke("quota:quit"),
});
