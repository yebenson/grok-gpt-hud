# 配额小组件

Windows 桌面常驻小组件：只读 SuperGrok 与 ChatGPT/Codex 额度。界面为中文，窗口置顶、无边框、毛玻璃风格。布局为**上 Grok、下 ChatGPT**。

本程序**不会**写入 token、不会刷新 token、不会改写 `auth.json` 或 Codex/Grok CLI 文件，也不走 gRPC、不是 Cursor 插件。401 只表示当前读取到的凭证已过期，请到 Hermes 或对应 CLI 重新登录。

## 运行

```bash
npm install
npm start
```

开发机可用 `npm test` 跑自动化测试，`npm run preview` 在浏览器里查看 HUD 样式预览。

## Windows 便携版

在 Windows 上：

```bash
npm install
npm run dist:win
```

产物在 `dist/quota-hud-portable.exe`（electron-builder portable）。把 exe 放到任意目录即可运行，无需安装。

Linux / macOS 上同样可以 `npm start` 做逻辑验证；Acrylic 毛玻璃是 Windows 11 的 `backgroundMaterial: acrylic`，其他系统回退为 CSS `backdrop-filter`。

## 凭证来源（只读）

右键整个窗口，在 **Windows Hermes** 与 **Windows Terminal** 之间切换。切换只改变读取哪一组文件，Grok 与 ChatGPT 一起切换。

| 来源 | ChatGPT / Codex | SuperGrok |
| --- | --- | --- |
| Windows Hermes | `%USERPROFILE%\.hermes\auth.json` → `credential_pool["openai-codex"]`（全部条目） | 同一文件 → `credential_pool["xai-oauth"]`（全部条目） |
| Windows Terminal | `%USERPROFILE%\.codex\auth.json`（文件里有几条就显示几条，不硬编码为 1） | `%USERPROFILE%\.grok\auth.json`（同上） |

隐藏账号：右键菜单勾选框。未勾选的账号仍会在后台拉额度，只是 HUD 不画出来。可见性只存在小组件自己的 `electron-store` 里，**绝不**写回 `auth.json`，也不影响 CLI / Hermes。

窗口会等到**当前来源下每一个账号**都有最终结果（成功或失败）后才展示仪表盘。某一个账号 401 不会卡住整窗。

## 额度接口

- ChatGPT/Codex：`GET https://chatgpt.com/backend-api/wham/usage`，带 Bearer 与 `ChatGPT-Account-Id`。展示 5 小时窗口、周窗口，以及本地时区的 `reset_at`。若 5 小时窗口缺失 / unlimited / 没有 cap 字段，5h 指针显示 **∞**，而不是 0% 红灯。周额度在有 cap 时仍画指针。
- SuperGrok：`GET https://cli-chat-proxy.grok.com/v1/billing?format=credits`，使用读到的 xAI OAuth。一根剩余百分比指针。失败则在该账号上显示错误，**不**回退 grok.com gRPC。

颜色（剩余百分比）：≥67% 蓝，33–67% 黄，&lt;33% 红。数据缺失时不会画假的 100%。

## 刷新

- 工作时段自动刷新：本机本地时区 **09:00–18:00**，每 15 分钟一次（例如 18:01 不会自动拉）。
- 启动时立刻拉一次，即使不在该时段，避免空白窗。
- 右键 **立即刷新** 随时可用。
- 失败账号独立退避；一侧失败不会冻住另一侧。

## 错误文案

- 文件缺失或损坏：该来源「读不到凭证」
- 池为空：该侧「池里没有账号」
- 401 / 过期：「凭证过期，去 Hermes 或对应 CLI 重新登录」
- 403：「接口拒绝」
- 网络：「拉取失败」并重试

## 测试

```bash
npm test
```

覆盖：三色分档、无 5h cap 时显示 ∞、隐藏列表持久化且不写 `auth.json`、18:01 不自动轮询、切换来源不写凭证文件、单个失败账号不阻塞其余账号。
