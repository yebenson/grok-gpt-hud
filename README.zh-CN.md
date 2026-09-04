# grok-gpt-hud

**中文** | [English](README.md)

Windows 桌面常驻额度小组件：只读显示 SuperGrok 与 ChatGPT/Codex 配额。上 Grok、下 ChatGPT。界面语言默认 **英语**，可在右键菜单切换为中文。

单文件 `grok-gpt-hud.exe` 负责读凭证、内存换票、拉额度与绘制。需本机 [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)、HTTPS，以及下方至少一种来源的已登录凭证。

- 无边框、可置顶、通知区图标（不占任务栏）
- 松开鼠标时按窗口所在屏贴边吸附；拖动中刷新不改位置
- 不写入 token；不改写 `auth.json`、OpenClaw SQLite 或 CLI / Hermes 文件

## 截图


| Hermes Agent | OpenClaw | Windows Terminal |
| ------------ | -------- | ---------------- |
| Hermes Agent | OpenClaw | Terminal         |


## 许可证

[MIT License](LICENSE)

## 技术路线


| 层   | 实现                                                        |
| --- | --------------------------------------------------------- |
| 运行时 | .NET 8 WinForms（`net8.0-windows`）                         |
| 发布  | `win-x64` 框架依赖单文件 → `dist/grok-gpt-hud.exe`               |
| UI  | 自绘卡片 + DWM acrylic；logo 在 `QuotaHud/Brand/`               |
| 凭证  | 右键：**Hermes Agent** / **Windows Terminal** / **OpenClaw** |
| 额度  | Grok 与 ChatGPT 并行 HTTPS GET                               |
| 设置  | `%APPDATA%\QuotaHud\settings.json`（来源、语言、隐藏账号、置顶）         |


入口：`QuotaHud/Program.cs` → `HudForm`。

## 凭证路径（只读）

一次切换同时作用于两侧。当前来源两侧都缺失时，自动改用其它有凭证的来源并记住。

环境变量：`HERMES_HOME`、`OPENCLAW_HOME`、`QUOTA_WIDGET_HOME`。

### Hermes Agent（默认）

`%LOCALAPPDATA%\hermes\auth.json`

- ChatGPT：`credential_pool["openai-codex"]`（相同 `access_token` 只保留一条）
- SuperGrok：`credential_pool["xai-oauth"]`

### Windows Terminal


| 侧               | 路径                               |
| --------------- | -------------------------------- |
| ChatGPT / Codex | `%USERPROFILE%\.codex\auth.json` |
| SuperGrok       | `%USERPROFILE%\.grok\auth.json`  |


解析顺序：池 → `accounts` → CLI 单例 / 键值表。

### OpenClaw

`%USERPROFILE%\.openclaw\state\openclaw.sqlite` → `config_machine_state` / `authProfiles.store`

- `provider=openai` → ChatGPT
- `provider=xai` → SuperGrok

卡片不画 Grok `device_code` 与 ChatGPT 邮箱；右键可隐藏账号。

## 刷新

- 启动立刻拉一次
- 每 **5 分钟**检查是否自动轮询
- 自动轮询：本地 **09:00 ≤ 时刻 < 18:00**，且距上次自动轮询 ≥ **15 分钟**
- **立即刷新** 不受工作时段限制

带 `refresh_token` 的过期 access token 仅在内存中向 OpenAI / xAI 换票。

## 额度展示

- ChatGPT `wham/usage`：**5h** / **7d**（无 cap 为 **∞**）
- SuperGrok billing：剩余百分比环
- 剩余颜色：≥67% 蓝，33–67% 黄，<33% 红
- ChatGPT 套餐徽章：Go / Plus / Pro / Team / Business / Enterprise / Edu

## 打包

```bash
dotnet publish QuotaHud/QuotaHud.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

产物：`dist/grok-gpt-hud.exe`（不纳入 Git）。开发：`dotnet run --project QuotaHud/QuotaHud.csproj`。