# Hataori Configuration

[English](CONFIG.md) | [日本語](CONFIG.ja.md)

This document is the source of truth for Hataori configuration files, precedence, settings, constraints, and safe examples.

## Configuration Directory

| File | Standard location | Owner | Purpose |
| :--- | :--- | :--- | :--- |
| Main settings | `%INSTALL_ROOT%\config\hataori.json` | MSI, user, or `hataori config init` | Non-secret application language, Server, agent, retry, logging, maintenance, and hook settings. |
| Service secret settings | `%INSTALL_ROOT%\config\hataori.service.json` | `hataori service setup` | Itoguruma token for the `LocalSystem` Windows Service. |

Relative `databasePath`, log, and hook paths are resolved from `%INSTALL_ROOT%`. An explicitly supplied `HATAORI_CONFIG_PATH` may point to another absolute main settings file.

## File Generation

- On a new installation, the MSI creates `hataori.json` with the selected language. It preserves an existing file during upgrades.
- `hataori config init [--language <ja-JP|en-US>]` creates the embedded default `hataori.json` only when the destination does not exist.
- `hataori service setup` creates or replaces `hataori.service.json` and restricts its ACL to `SYSTEM` and `Administrators`.
- Do not hand-create a token value in examples, source control, logs, or chat.

## Main Settings

The Server loads settings in this effective order, with later sources overriding earlier sources:

1. .NET host defaults.
2. The main `hataori.json` selected by `HATAORI_CONFIG_PATH` or the standard path.
3. `hataori.service.json` when running as a Windows Service.
4. Environment variables with the `HATAORI_` prefix; use `__` for JSON nesting, for example `HATAORI_SERVER__MCPPORT=45440`.

CLI commands that load Server settings use the main JSON followed by `HATAORI_` environment variables. `--config` selects the main JSON for commands that accept it. CLI-only path variables are documented in [Commands](COMMANDS.md).

All sections in the default file are required for Server startup except `hooks`, which is optional when hooks are disabled by omission. The default file intentionally omits `itoguruma.authenticationToken`; Service mode obtains it from the service secret file.

## Profile Settings

Hataori has no named profile file. Runtime variants are selected by the main file path, environment overrides, and whether the process runs as the Windows Service. MCP access is unauthenticated and loopback-only; Itoguruma authentication is a separate outbound-client setting.

## Settings Reference

### `application.language`

- Type/required: string, required.
- Default: `ja-JP`; supported values are `ja-JP` and `en-US`.
- Behavior: stores the application display language selected during installation.

### `server`

Children: [`databasePath`](#serverdatabasepath), [`controlPipeName`](#servercontrolpipename), [`mcpHost`](#servermcphost), [`mcpPort`](#servermcpport), and [`mcpPath`](#servermcppath).

#### `server.databasePath`

- Type/required: non-empty string, required.
- Default: `data/hataori.db`; omission fails startup validation.
- Behavior/constraint: absolute paths are used directly; relative paths resolve from `%INSTALL_ROOT%`.
- Example: `"databasePath": "data/hataori.db"`.

#### `server.controlPipeName`

- Type/required: non-empty string, required.
- Default: `hataori-control`; omission fails validation.
- Constraint: must not contain `/` or `\`.
- Example: `"controlPipeName": "hataori-control"`.

#### `server.mcpHost`

- Type/required: IP-address string, required.
- Default: `127.0.0.1`; omission fails validation.
- Constraint: must parse as a loopback IP address. Remote bind addresses are rejected.
- Example: `"mcpHost": "127.0.0.1"`.

#### `server.mcpPort`

- Type/required: integer, required.
- Default: `45440`; omission binds as `0` and fails validation.
- Range: `1` through `65535`.
- Example: `"mcpPort": 45440`.

#### `server.mcpPath`

- Type/required: non-empty string, required.
- Default: `/mcp`; omission fails validation.
- Constraint: must begin with `/` and must match MCP client URLs.
- Example: `"mcpPath": "/mcp"`.

### `itoguruma`

Children: [`endpoint`](#itogurumaendpoint), [`authenticationToken`](#itogurumaauthenticationtoken), [`agentId`](#itogurumaagentid), [`agentType`](#itogurumaagenttype), [`connectionTimeoutSeconds`](#itogurumaconnectiontimeoutseconds), [`pollIntervalSeconds`](#itogurumapollintervalseconds), [`maxReconnectAttempts`](#itogurumamaxreconnectattempts), [`receiveBatchSize`](#itogurumareceivebatchsize), and [`leaseSeconds`](#itogurumaleaseseconds).

#### `itoguruma.endpoint`

- Type/required: absolute URI, required.
- Default: `http://127.0.0.1:47631/mcp`; omission fails validation.
- Constraint: HTTP or HTTPS loopback URI only.
- Example: `"endpoint": "http://127.0.0.1:47631/mcp"`.

#### `itoguruma.authenticationToken`

- Type/required: non-empty secret string, required at runtime.
- Default: none. It is intentionally absent from `hataori.json`.
- Behavior: Interactive setup supplies `HATAORI_ITOGURUMA__AUTHENTICATIONTOKEN`; Service setup writes the secret file. Never put the value in source control.
- Safe example: set by `hataori service setup`; no literal JSON secret example is provided.

#### `itoguruma.agentId`

- Type/required: non-empty string, required.
- Default: `hataori`; omission fails validation.
- Behavior: sender ID used by the Hataori Service for replies. It does not limit monitored projects; those are discovered under `activation.workingDirectory`.
- Example: `"agentId": "hataori"`.

#### `itoguruma.agentType`

- Type/required: non-empty string, required.
- Default: `hataori`; omission fails validation.
- Example: `"agentType": "hataori"`.

#### `itoguruma.connectionTimeoutSeconds`

- Type/required: integer, required.
- Default: `10`; omission becomes `0` and fails validation.
- Range: `1` through `120` seconds.
- Example: `"connectionTimeoutSeconds": 10`.

#### `itoguruma.pollIntervalSeconds`

- Type/required: integer, required.
- Default: `5`; omission becomes `0` and fails validation.
- Range: `1` through `300` seconds.
- Example: `"pollIntervalSeconds": 5`.

#### `itoguruma.maxReconnectAttempts`

- Type/required: integer, required.
- Default: `5`; omission becomes `0` and fails validation.
- Range: `1` through `100`.
- Example: `"maxReconnectAttempts": 5`.

#### `itoguruma.receiveBatchSize`

- Type/required: integer, required.
- Default: `50` when omitted.
- Range: `1` through `500` messages.
- Example: `"receiveBatchSize": 50`.

#### `itoguruma.leaseSeconds`

- Type/required: integer, required.
- Default: `300` when omitted.
- Range: `1` through `3600` seconds.
- Example: `"leaseSeconds": 300`.

### `agents.codex`

Children: [`executablePath`](#agentscodexexecutablepath), [`sandboxMode`](#agentscodexsandboxmode), [`approveForMe`](#agentscodexapproveforme), [`model`](#agentscodexmodel), and [`maxCapturedCharacters`](#agentscodexmaxcapturedcharacters).

#### `agents.codex.executablePath`

- Type/required: non-empty string, required.
- Default: `codex` when omitted.
- Behavior: executable name or absolute executable path used to start Codex CLI.
- Example: `"executablePath": "codex"`.

#### `agents.codex.sandboxMode`

- Type/required: string enum, required.
- Default: `workspace-write`.
- Values: `read-only` denies workspace writes; `workspace-write` permits writes within the agent sandbox.
- Constraint: `approveForMe: true` requires `workspace-write`.
- Example: `"sandboxMode": "workspace-write"`.

#### `agents.codex.approveForMe`

- Type/required: boolean, optional.
- Default: `true`.
- Behavior: enables Codex automatic approval mode; requires `sandboxMode` to be `workspace-write`.
- Example: `"approveForMe": true`.

#### `agents.codex.model`

- Type/required: string or `null`, optional.
- Default: `null`, which lets Codex select its configured default.
- Example: `"model": null`.

#### `agents.codex.maxCapturedCharacters`

- Type/required: integer, optional.
- Default: `4194304`.
- Range: `1024` through `16777216` characters.
- Example: `"maxCapturedCharacters": 4194304`.

### `agents.claudeCode`

Children: [`executablePath`](#agentsclaudecodeexecutablepath), [`permissionMode`](#agentsclaudecodepermissionmode), [`model`](#agentsclaudecodemodel), and [`maxCapturedCharacters`](#agentsclaudecodemaxcapturedcharacters).

#### `agents.claudeCode.executablePath`

- Type/required: non-empty string, required.
- Default: `claude` when omitted.
- Example: `"executablePath": "claude"`.

#### `agents.claudeCode.permissionMode`

- Type/required: string enum, required.
- Default: `acceptEdits`.
- Values: `acceptEdits` permits edit acceptance; `plan` limits Claude Code to planning behavior.
- Example: `"permissionMode": "acceptEdits"`.

#### `agents.claudeCode.model`

- Type/required: string or `null`, optional.
- Default: `null`, which lets Claude Code select its configured default.
- Example: `"model": null`.

#### `agents.claudeCode.maxCapturedCharacters`

- Type/required: integer, optional.
- Default: `4194304`.
- Range: `1024` through `16777216` characters.
- Example: `"maxCapturedCharacters": 4194304`.

### `activation`

Children: [`enabled`](#activationenabled), [`workingDirectory`](#activationworkingdirectory), [`pollIntervalMilliseconds`](#activationpollintervalmilliseconds), [`providerPriority`](#activationproviderpriority), and [`maxConcurrentRuns`](#activationmaxconcurrentruns).

#### `activation.enabled`

- Type/required: boolean, optional.
- Default: `false`.
- Behavior: when `true`, queued messages may activate configured agents automatically.
- Example: `"enabled": false`.

#### `activation.workingDirectory`

- Type/required: string, conditionally required.
- Default: empty string.
- Constraint: when activation is enabled, it must be an existing absolute directory.
- Behavior: projects root whose direct children are automatically registered and monitored in Itoguruma; each directory name is a destination project ID.
- Example: `"workingDirectory": "F:\\Workspace\\Projects"`.

#### `activation.pollIntervalMilliseconds`

- Type/required: integer, optional.
- Default: `1000`.
- Range: `100` through `60000` milliseconds.
- Example: `"pollIntervalMilliseconds": 1000`.

#### `activation.providerPriority`

- Type/required: array of provider ID strings, required.
- Default: `["codex", "claude-code"]`.
- Behavior: fallback search order when the source provider cannot open the project. It can also be changed through `hataori provider priority` and MCP Tools.
- Constraint: at least one value, unique ignoring case. When Activation is enabled, every provider must exist in `maxConcurrentRuns`.

#### `activation.maxConcurrentRuns`

- Type/required: object/dictionary of agent ID to integer, conditionally required.
- Default: `{"codex": 2, "claude-code": 2}` in the generated file.
- Constraint: when activation is enabled, at least one non-empty key is required; every value must be `1` through `32`.
- Example: `"maxConcurrentRuns": {"codex": 2, "claude-code": 2}`.

### `replyRetry`

Children: [`enabled`](#replyretryenabled), [`maxAttempts`](#replyretrymaxattempts), [`initialDelaySeconds`](#replyretryinitialdelayseconds), [`maximumDelaySeconds`](#replyretrymaximumdelayseconds), [`batchSize`](#replyretrybatchsize), and [`pollIntervalMilliseconds`](#replyretrypollintervalmilliseconds).

#### `replyRetry.enabled`

- Type/required: boolean, optional.
- Default: `true`.
- Behavior: enables persistent retry processing for unsent replies.
- Example: `"enabled": true`.

#### `replyRetry.maxAttempts`

- Type/required: integer, optional.
- Default: `5`.
- Range: `1` through `20`.
- Example: `"maxAttempts": 5`.

#### `replyRetry.initialDelaySeconds`

- Type/required: integer, optional.
- Default: `5`.
- Range: `1` through `3600`; must not exceed `maximumDelaySeconds`.
- Example: `"initialDelaySeconds": 5`.

#### `replyRetry.maximumDelaySeconds`

- Type/required: integer, optional.
- Default: `300`.
- Range: at least `initialDelaySeconds` and at most `86400`.
- Example: `"maximumDelaySeconds": 300`.

#### `replyRetry.batchSize`

- Type/required: integer, optional.
- Default: `20`.
- Range: `1` through `500` replies.
- Example: `"batchSize": 20`.

#### `replyRetry.pollIntervalMilliseconds`

- Type/required: integer, optional.
- Default: `1000`.
- Range: `100` through `60000` milliseconds.
- Example: `"pollIntervalMilliseconds": 1000`.

### `fileLogging`

Children: [`enabled`](#fileloggingenabled), [`directoryPath`](#fileloggingdirectorypath), [`minimumLevel`](#fileloggingminimumlevel), and [`retentionDays`](#fileloggingretentiondays).

#### `fileLogging.enabled`

- Type/required: boolean, optional.
- Default: `true`.
- Behavior: enables structured file logging.
- Example: `"enabled": true`.

#### `fileLogging.directoryPath`

- Type/required: non-empty string, required.
- Default: `logs`; omission fails validation.
- Behavior: absolute paths are used directly; relative paths resolve from `%INSTALL_ROOT%`.
- Example: `"directoryPath": "logs"`.

#### `fileLogging.minimumLevel`

- Type/required: .NET log-level string enum, required.
- Default: `Information`.
- Values: `Trace`, `Debug`, `Information`, `Warning`, `Error`, and `Critical`; `None` is rejected.
- Example: `"minimumLevel": "Information"`.

#### `fileLogging.retentionDays`

- Type/required: integer, optional.
- Default: `30`.
- Range: `1` through `3650` days.
- Example: `"retentionDays": 30`.

### `databaseMaintenance`

Children: [`enabled`](#databasemaintenanceenabled), [`intervalHours`](#databasemaintenanceintervalhours), [`staleTaskHours`](#databasemaintenancestaletaskhours), [`taskRetentionDays`](#databasemaintenancetaskretentiondays), [`agentRunRetentionDays`](#databasemaintenanceagentrunretentiondays), [`messageRetentionDays`](#databasemaintenancemessageretentiondays), and [`vacuum`](#databasemaintenancevacuum).

#### `databaseMaintenance.enabled`

- Type/required: boolean, optional.
- Default: `true`.
- Behavior: enables periodic stale-state expiry, retention purge, and optional vacuum.
- Example: `"enabled": true`.

#### `databaseMaintenance.intervalHours`

- Type/required: integer, optional.
- Default: `24`; range `1` through `720` hours.
- Example: `"intervalHours": 24`.

#### `databaseMaintenance.staleTaskHours`

- Type/required: integer, optional.
- Default: `24`; range `1` through `8760` hours.
- Example: `"staleTaskHours": 24`.

#### `databaseMaintenance.taskRetentionDays`

- Type/required: integer, optional.
- Default: `90`; range `1` through `3650` days.
- Example: `"taskRetentionDays": 90`.

#### `databaseMaintenance.agentRunRetentionDays`

- Type/required: integer, optional.
- Default: `30`; range `1` through `3650` days.
- Example: `"agentRunRetentionDays": 30`.

#### `databaseMaintenance.messageRetentionDays`

- Type/required: integer, optional.
- Default: `30`; range `1` through `3650` days.
- Example: `"messageRetentionDays": 30`.

#### `databaseMaintenance.vacuum`

- Type/required: boolean, optional.
- Default: `true`.
- Behavior: requests SQLite `VACUUM` during maintenance after retention work.
- Example: `"vacuum": true`.

### `hooks`

Children: [`enabled`](#hooksenabled), [`codexConfigPath`](#hookscodexconfigpath), and [`claudeConfigPath`](#hooksclaudeconfigpath).

#### `hooks.enabled`

- Type/required: boolean, optional.
- Default: `true` in the generated file.
- Behavior: enables lifecycle hook diagnostics and templates.
- Example: `"enabled": true`.

#### `hooks.codexConfigPath`

- Type/required: non-empty string when hooks are enabled.
- Default: `bin/cli/hooks/codex-hooks.json`.
- Behavior: relative paths resolve from `%INSTALL_ROOT%`.
- Example: `"codexConfigPath": "bin/cli/hooks/codex-hooks.json"`.

#### `hooks.claudeConfigPath`

- Type/required: non-empty string when hooks are enabled.
- Default: `bin/cli/hooks/claude-settings.json`.
- Behavior: relative paths resolve from `%INSTALL_ROOT%`.
- Example: `"claudeConfigPath": "bin/cli/hooks/claude-settings.json"`.

### `allowedHosts`

- Type/required: semicolon-delimited host string, optional.
- Default: `127.0.0.1;localhost`.
- Behavior: configures ASP.NET Core host filtering independently of the loopback bind address.
- Example: `"allowedHosts": "127.0.0.1;localhost"`.

## Samples

Generate the complete non-secret sample instead of copying a stale document fragment:

```powershell
hataori config init
hataori config check
hataori config show
```

`config show` redacts secret-like keys. To validate an alternate file:

```powershell
hataori config check --config F:\SafePath\hataori.json
```
