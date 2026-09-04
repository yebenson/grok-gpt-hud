# grok-gpt-hud

[中文](README.zh-CN.md) | **English**

A Windows always-on-top desktop widget that **read-only** displays SuperGrok and ChatGPT/Codex quota. Chinese UI: Grok on top, ChatGPT below.

A single exe reads credentials, refreshes tokens in memory, fetches quota, and draws the HUD. No extra local service. Requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0), HTTPS, and signed-in Hermes / Terminal / OpenClaw credentials.

Features: borderless, optional always-on-top, notification-area icon (no taskbar button); edge snap when the mouse is released (using the window’s monitor); layout refresh does not move the window while dragging.

This app **never writes** tokens and **never modifies** any `auth.json`, OpenClaw SQLite, or CLI / Hermes files. When an access token expires, it uses the credential’s `refresh_token` **in memory only**; refreshed tokens are not saved to disk. If you still get 401, sign in again in the matching tool.

## Screenshots

| Windows Hermes | OpenClaw | Windows Terminal |
| --- | --- | --- |
| ![Hermes](docs/Hermes.png) | ![OpenClaw](docs/OpenClaw.png) | ![Terminal](docs/Terminal.png) |

## License

Released under the [MIT License](LICENSE).

## Stack

| Layer | Detail |
| --- | --- |
| Runtime | .NET 8 WinForms, `net8.0-windows` |
| Publish | Framework-dependent single file for `win-x64`: `dist/grok-gpt-hud.exe` |
| UI | Owner-drawn cards + DWM acrylic; logos under `QuotaHud/Brand/` for cards / exe / window / tray |
| Credentials | Read-only; right-click switch among **Windows Hermes** / **Windows Terminal** / **OpenClaw** |
| Quota | HTTPS GET; Grok and ChatGPT in parallel; one account failure does not block the other side |
| Local state | `%APPDATA%\QuotaHud\settings.json` (source, hidden accounts, always-on-top) |

Entry: `QuotaHud/Program.cs` → `HudForm`. Domain logic in `QuotaHud/Domain/`, UI in `QuotaHud/Ui/`.

## Credential paths (read-only)

Switching source changes both Grok and ChatGPT paths together. If both sides are missing for the current source, the app falls back to another source that has credentials and remembers it.

Optional environment variables:

- `HERMES_HOME`: Hermes directory (reads `auth.json` under it)
- `OPENCLAW_HOME`: OpenClaw home (reads `state/openclaw.sqlite`)
- `QUOTA_WIDGET_HOME`: user home override (affects Terminal `.codex` / `.grok` and the default OpenClaw directory)

### Windows Hermes (default)

| | Path |
| --- | --- |
| auth.json | `%LOCALAPPDATA%\hermes\auth.json` |

- ChatGPT: `credential_pool["openai-codex"]` (identical `access_token` values collapse to one card)
- SuperGrok: `credential_pool["xai-oauth"]`

### Windows Terminal

| Side | Path |
| --- | --- |
| ChatGPT / Codex | `%USERPROFILE%\.codex\auth.json` |
| SuperGrok | `%USERPROFILE%\.grok\auth.json` |

Per side: credential pool → `accounts` → CLI singleton / key map.

### OpenClaw

| | Path |
| --- | --- |
| State DB | `%USERPROFILE%\.openclaw\state\openclaw.sqlite` |

Table `config_machine_state`, key `authProfiles.store`:

- `provider=openai` → ChatGPT (all profiles; identical `access` values collapse to one)
- `provider=xai` → SuperGrok

Cards do not show Grok `device_code` or ChatGPT emails. The context menu still lists distinguishable account names for hide/show; if every account on a side is hidden, that whole side (including the header) is omitted.

## Refresh schedule

- Fetch once immediately on startup (ignores work hours)
- Every **5 minutes**, check whether an automatic poll is due
- Auto poll requires local time **09:00 ≤ hour &lt; 18:00** and at least **15 minutes** since the last auto poll
- Right-click **Refresh now** works anytime

Failed accounts use independent exponential backoff (about 15s up to 15 minutes). When expired and a `refresh_token` is present, tokens are refreshed against OpenAI (`https://auth.openai.com/oauth/token`) or xAI (default `https://auth.x.ai` `oauth2/token`) and only the in-memory `Account` is updated.

## Quota APIs and display

- ChatGPT: `GET https://chatgpt.com/backend-api/wham/usage`
  - **5h**: `limit_window_seconds` &lt; 86400; uncapped shows **∞**
  - **7d**: `limit_window_seconds` ≥ 86400
- SuperGrok: `GET https://cli-chat-proxy.grok.com/v1/billing?format=credits` (remaining-percent ring)

Remaining colors: ≥67% blue, 33–67% yellow, &lt;33% red. ChatGPT plan badges: Go / Plus / Pro / Team / Business / Enterprise / Edu. Grok shows no plan badge when no reliable plan field is available.

## Build

```bash
dotnet publish QuotaHud/QuotaHud.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

Output: `dist/grok-gpt-hud.exe` (generated locally; not committed). Dev: `dotnet run --project QuotaHud/QuotaHud.csproj`.
