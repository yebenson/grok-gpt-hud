# grok-gpt-hud

**中文** | [English](README.md)

Windows 桌面常驻小组件：只读显示 SuperGrok 与 ChatGPT/Codex 额度。界面中文，上 Grok、下 ChatGPT。

单文件 exe 自行完成读凭证、换票、拉额度与绘制；不依赖其它本地服务。需要本机 [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)、HTTPS 网络，以及已登录的 Hermes / Terminal / OpenClaw 凭证。

特性：无边框、可置顶、通知区图标（不占任务栏）；松开鼠标时按窗口所在屏贴边吸附；拖动过程中刷新不改窗口位置。

本程序**不写入** token，也**不改写**任何 `auth.json`、OpenClaw SQLite 或 CLI / Hermes 文件。access token 过期时只用凭证里的 `refresh_token` **在内存中**换新票；新票不落盘。仍 401 时请到对应工具重新登录。

## 许可证

以 [MIT License](LICENSE) 开源发布。

## 技术路线

| 层 | 实现 |
| --- | --- |
| 运行时 | .NET 8 WinForms，`net8.0-windows` |
| 发布 | `win-x64` 框架依赖单文件：`dist/grok-gpt-hud.exe` |
| UI | 自绘卡片 + DWM acrylic；`QuotaHud/Brand/` 中 logo 用于卡片 / exe / 窗口 / 托盘 |
| 凭证 | 只读；右键切换 **Windows Hermes** / **Windows Terminal** / **OpenClaw** |
| 额度 | HTTPS GET；Grok 与 ChatGPT 并行；单账号失败不阻塞另一侧 |
| 本地状态 | `%APPDATA%\QuotaHud\settings.json`（来源、隐藏账号、是否置顶） |

入口：`QuotaHud/Program.cs` → `HudForm`。逻辑在 `QuotaHud/Domain/`，界面在 `QuotaHud/Ui/`。

## 凭证路径（只读）

一次切换同时改变 Grok 与 ChatGPT 的凭证来源。若当前来源两侧都缺失，会自动改用其它有凭证的来源并记住。

可选环境变量：

- `HERMES_HOME`：Hermes 目录（读取其下 `auth.json`）
- `OPENCLAW_HOME`：OpenClaw 主目录（读取 `state/openclaw.sqlite`）
- `QUOTA_WIDGET_HOME`：用户主目录（影响 Terminal 的 `.codex` / `.grok`，以及默认 OpenClaw 目录）

### Windows Hermes（默认）

| | 路径 |
| --- | --- |
| auth.json | `%LOCALAPPDATA%\hermes\auth.json` |

- ChatGPT：`credential_pool["openai-codex"]`（相同 `access_token` 只保留一条）
- SuperGrok：`credential_pool["xai-oauth"]`

### Windows Terminal

| 侧 | 路径 |
| --- | --- |
| ChatGPT / Codex | `%USERPROFILE%\.codex\auth.json` |
| SuperGrok | `%USERPROFILE%\.grok\auth.json` |

每侧解析：凭证池 → `accounts` → CLI 单例 / 键值表。

### OpenClaw

| | 路径 |
| --- | --- |
| 状态库 | `%USERPROFILE%\.openclaw\state\openclaw.sqlite` |

表 `config_machine_state`，键 `authProfiles.store`：

- `provider=openai` → ChatGPT（多 profile；相同 `access` 只保留一条）
- `provider=xai` → SuperGrok

卡片不画 Grok `device_code`、不画 ChatGPT 邮箱。右键可用账号名勾选隐藏；某一侧全部隐藏后该侧整块不绘制。

## 定时刷新

- 启动立刻拉一次（不看工作时段）
- 每 **5 分钟**检查是否该自动轮询
- 自动轮询需同时满足：本地时间 **09:00 ≤ 时刻 &lt; 18:00**，且距上次自动轮询 ≥ **15 分钟**
- 右键 **立即刷新** 随时可用

失败账号独立指数退避（约 15 秒起，上限 15 分钟）。过期且带 `refresh_token` 时，向 OpenAI（`https://auth.openai.com/oauth/token`）或 xAI（默认 `https://auth.x.ai` 的 `oauth2/token`）换票，只改内存中的 Account。

## 额度与展示

- ChatGPT：`GET https://chatgpt.com/backend-api/wham/usage`
  - **5h**：`limit_window_seconds` &lt; 86400；无 cap 显示 **∞**
  - **7d**：`limit_window_seconds` ≥ 86400
- SuperGrok：`GET https://cli-chat-proxy.grok.com/v1/billing?format=credits`（剩余百分比环）

剩余颜色：≥67% 蓝，33–67% 黄，&lt;33% 红。ChatGPT 官方套餐徽章：Go / Plus / Pro / Team / Business / Enterprise / Edu。Grok 暂无可靠套餐字段时不显示徽章。

## 运行与打包

```bash
dotnet publish QuotaHud/QuotaHud.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

产物：`dist/grok-gpt-hud.exe`（本地生成，不纳入 Git）。开发：`dotnet run --project QuotaHud/QuotaHud.csproj`。
