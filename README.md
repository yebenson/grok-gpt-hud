# 配额小组件

Windows 桌面常驻小组件：只读 SuperGrok 与 ChatGPT/Codex 额度。界面为中文，窗口置顶、无边框。布局为**上 Grok、下 ChatGPT**。

本程序**不会**写入 token、不会改写 `auth.json` 或 Codex/Grok CLI 文件。Grok 与 ChatGPT 的 access token 过期时，会用文件里的 `refresh_token` **仅在内存中**换新票再拉额度，效果与 CLI / Hermes 静默续期相同，但仍不把新票写回磁盘。若刷新后仍 401，再到 Hermes 或对应 CLI 重新登录。

技术栈：C# / .NET 8 WinForms 单文件。需要本机已安装 [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（有 SDK 即可）。产物约 250KB，不再内嵌 Chromium。

## 运行与打包

```bash
dotnet test
dotnet publish QuotaHud/QuotaHud.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

产物：`dist/配额小组件.exe`。放到任意目录双击即可。开发时也可用：

```bash
dotnet run --project QuotaHud/QuotaHud.csproj
```

## 凭证来源（只读）

右键整个窗口，在 **Windows Hermes** 与 **Windows Terminal** 之间切换。切换只改变读取哪一组文件，Grok 与 ChatGPT 一起切换。若当前来源的文件不存在，会自动改用另一来源。

| 来源 | ChatGPT / Codex | SuperGrok |
| --- | --- | --- |
| Windows Hermes | `%LOCALAPPDATA%\hermes\auth.json`（`HERMES_HOME` 优先；若缺失再试 `%USERPROFILE%\.hermes\auth.json`）→ `credential_pool["openai-codex"]` | 同一文件 → `credential_pool["xai-oauth"]` |
| Windows Terminal | `%USERPROFILE%\.codex\auth.json` | `%USERPROFILE%\.grok\auth.json` |

卡片上不显示 Grok `device_code`、也不显示 ChatGPT 邮箱；右键菜单里仍用可区分的账号名。隐藏某个侧的全部账号后，该侧整块（含「无可用账号」占位）都不再绘制。可见性与背景透明度（0–90%）只存在 `%APPDATA%\QuotaHud\settings.json`，**绝不**写回 `auth.json`。透明度只作用在毛玻璃底，额度圆环与账号卡片保持清晰。

能识别时会在卡片上显示套餐名（如 Plus / Pro / Business / Team / SuperGrok）；识别不到则不显示。重置时间画在对应圆环下方。

## 额度接口

- ChatGPT/Codex：`GET https://chatgpt.com/backend-api/wham/usage`
- SuperGrok：`GET https://cli-chat-proxy.grok.com/v1/billing?format=credits`

Logo 使用仓库内 `chatgpt.png` / `supergrok.png`。中文用微软雅黑，英文与数字用 Segoe UI。右键透明度 0–90% 会改变整个窗口不透明度（90% 时仍保留约 10% 可见，避免完全看不见）。

## 刷新

- 工作时段自动刷新：本机本地时区 **09:00–18:00**，每 15 分钟一次
- 启动时立刻拉一次
- 右键 **立即刷新** 随时可用

## 测试

```bash
dotnet test
```
