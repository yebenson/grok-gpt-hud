# grok-gpt-hud

Windows 桌面常驻小组件：只读 SuperGrok 与 ChatGPT/Codex 额度。界面中文，上 Grok、下 ChatGPT。无边框、可置顶、贴边吸附；不占任务栏，图标在通知区。

本程序**不写入** token，也**不改写**任何 `auth.json` 或 CLI / Hermes 文件。access token 过期时只用文件里的 `refresh_token` **在内存中**换新票再拉额度；新票不会落盘。刷新后仍 401，请到 Hermes 或对应 CLI 重新登录。

## 技术路线

| 层 | 实现 |
| --- | --- |
| 运行时 | .NET 8 WinForms，`net8.0-windows`，需要本机 [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0) |
| 发布 | `win-x64` 框架依赖单文件：`dist/grok-gpt-hud.exe` |
| UI | 自绘卡片 + DWM acrylic（毛玻璃采桌面）；工具 logo 用于 exe / 窗口 / 托盘 |
| 凭证 | 只读 JSON；来源在 **Windows Hermes** 与 **Windows Terminal** 之间切换 |
| 额度 | HTTPS GET；Grok 与 ChatGPT 并行拉，单账号失败不阻塞另一侧 |
| 本地状态 | 仅 `%APPDATA%\QuotaHud\settings.json`（来源、隐藏账号、是否置顶） |

代码入口：`QuotaHud/Program.cs` → `HudForm`。领域逻辑在 `QuotaHud/Domain/`，绘制在 `QuotaHud/Ui/`。

## 凭证路径（只读）

右键在 **Windows Hermes** / **Windows Terminal** 之间切换。一次切换同时改变 Grok 与 ChatGPT 读哪一组文件。若当前来源的文件两侧都缺失，会自动改用另一来源并记住。

可选环境变量：

- `HERMES_HOME`：覆盖 Hermes 目录（其下读取 `auth.json`）
- `QUOTA_WIDGET_HOME`：覆盖用户主目录（影响 Terminal 的 `.codex` / `.grok`）

### Windows Hermes（默认）

| | 路径 |
| --- | --- |
| auth.json | `%LOCALAPPDATA%\hermes\auth.json` |

JSON 池：

- ChatGPT / Codex → `credential_pool["openai-codex"]`（有几条有效登录显示几条；相同 `access_token` 只保留一条）
- SuperGrok → `credential_pool["xai-oauth"]`

### Windows Terminal

| 侧 | 路径 |
| --- | --- |
| ChatGPT / Codex | `%USERPROFILE%\.codex\auth.json` |
| SuperGrok | `%USERPROFILE%\.grok\auth.json` |

解析顺序（每侧独立）：凭证池 → `accounts` 数组 → CLI 单例 / 键值表。文件缺失记为「读不到凭证」；池为空记为「池里没有账号」。

卡片上不画 Grok `device_code`、也不画 ChatGPT 邮箱。右键菜单仍用可区分的账号名，可勾选隐藏；某一侧全部隐藏后，该侧整块（含标题）不再绘制。

## 定时刷新

启动时**立刻拉一次**（不看工作时段）。之后每 **30 秒**检查一次是否该自动轮询。

自动轮询同时满足：

1. 本机本地时区 **09:00 ≤ 时刻 &lt; 18:00**（18:00 起不再自动拉）
2. 距上次自动轮询至少 **15 分钟**

右键 **立即刷新** 随时可用，不受工作时段限制。

失败账号独立指数退避（约 15 秒起，上限 15 分钟），自动轮询会跳过仍在退避中的账号；手动刷新会重试它们。一侧失败不冻结另一侧。

过期处理：若账号带 `refresh_token` 且 JWT / `expires_at` 已过期（提前 30 秒），先向 OpenAI（`https://auth.openai.com/oauth/token`）或 xAI（issuer 的 `oauth2/token`，默认 `https://auth.x.ai`）换票，**只改内存中的 Account**，再请求额度。401 时再换一次。换票失败则显示「凭证过期，去 Hermes 或对应 CLI 重新登录」。

## 额度接口与展示

- ChatGPT / Codex：`GET https://chatgpt.com/backend-api/wham/usage`（Bearer，可选 `ChatGPT-Account-Id`）
  - **5h**：`limit_window_seconds` &lt; 86400；无 cap / unlimited 时显示 **∞**，不画重置时间
  - **7d**：`limit_window_seconds` ≥ 86400
- SuperGrok：`GET https://cli-chat-proxy.grok.com/v1/billing?format=credits`（Bearer + CLI 头）
  - 一根剩余百分比环；有 cap 时在环下显示重置时间

颜色按**剩余**百分比：≥67% 蓝，33–67% 黄，&lt;33% 红。缺数据不画假的 100%。能识别套餐时显示徽章（Plus / Pro / Team / Business / SuperGrok 等），识别不到则不显示。

## 运行与打包

```bash
dotnet test
dotnet publish QuotaHud/QuotaHud.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

产物：`dist/grok-gpt-hud.exe`。开发：`dotnet run --project QuotaHud/QuotaHud.csproj`。

Logo 在 `QuotaHud/Brand/`：`chatgpt.png`、`supergrok.png` 用于卡片；`app.ico` / `app.png` 用于 exe、窗口、托盘。中文微软雅黑，英文与数字 Segoe UI。
