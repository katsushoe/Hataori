# Hataori設定

[English](CONFIG.md) | [日本語](CONFIG.ja.md)

この文書はHataoriの設定file、優先順位、全設定項目、制約、安全なsampleの正本です。

## Configuration Directory

| File | 標準配置 | 作成主体 | 用途 |
| :--- | :--- | :--- | :--- |
| 通常設定 | `%INSTALL_ROOT%\config\hataori.json` | MSI、利用者、または`hataori config init` | 秘密情報を含まないアプリ言語、Server、Agent、Retry、Log、Maintenance、Hook設定。 |
| Service秘密設定 | `%INSTALL_ROOT%\config\hataori.service.json` | `hataori service setup` | `LocalSystem` Windows Service用Itoguruma token。 |

相対`databasePath`、log、hook pathは`%INSTALL_ROOT%`を基準に解決します。`HATAORI_CONFIG_PATH`へ絶対pathを指定すると別の通常設定fileを使用できます。

## File Generation

- 新規インストール時、MSIは選択された言語を含む`hataori.json`を作成します。アップグレード時は既存fileを保持します。
- `hataori config init [--language <ja-JP|en-US>]`は出力先が存在しない場合だけ組込み既定`hataori.json`を作成します。
- `hataori service setup`は`hataori.service.json`を作成または置換し、ACLを`SYSTEM`と`Administrators`だけに制限します。
- tokenの実値をsample、Source Control、log、chatへ記載しないでください。

## Main Settings

Serverは次の順で設定を読み、後のsourceが前を上書きします。

1. .NET Host既定設定。
2. `HATAORI_CONFIG_PATH`または標準pathで選択した通常`hataori.json`。
3. Windows Service実行時の`hataori.service.json`。
4. `HATAORI_`接頭辞の環境変数。JSON階層には`__`を使います。例: `HATAORI_SERVER__MCPPORT=45440`。

Server設定を読むCLI commandは通常JSONの後に`HATAORI_`環境変数を適用します。対応commandでは`--config`で通常JSONを選択します。CLI専用path変数は[コマンド](COMMANDS.ja.md)を参照してください。

既定file内の各sectionはServer起動に必要です。`hooks`だけは無効として省略できます。`itoguruma.authenticationToken`は意図的に通常fileへ含めず、Serviceでは秘密設定fileから取得します。

## Profile Settings

Hataoriには名前付きprofile fileがありません。通常file path、環境変数override、Windows Service実行かどうかでruntime設定を分けます。MCPは認証なし・loopback限定で、Itoguruma認証は別のoutbound client設定です。

## Settings Reference

### `application.language`

- 型/必須: string、必須。
- 既定値: `ja-JP`。対応値は`ja-JP`と`en-US`です。
- 動作: インストール時に選択されたアプリ表示言語を保存します。

### `server`

子項目: [`databasePath`](#serverdatabasepath)、[`controlPipeName`](#servercontrolpipename)、[`mcpHost`](#servermcphost)、[`mcpPort`](#servermcpport)、[`mcpPath`](#servermcppath)。

#### `server.databasePath`

- 型/必須: 空でないstring、必須。
- 既定値: `data/hataori.db`。省略時は起動検証に失敗します。
- 動作/制約: 絶対pathはそのまま使用し、相対pathは`%INSTALL_ROOT%`基準です。
- 例: `"databasePath": "data/hataori.db"`。

#### `server.controlPipeName`

- 型/必須: 空でないstring、必須。
- 既定値: `hataori-control`。省略時は検証に失敗します。
- 制約: `/`と`\`を含められません。
- 例: `"controlPipeName": "hataori-control"`。

#### `server.mcpHost`

- 型/必須: IP address string、必須。
- 既定値: `127.0.0.1`。省略時は検証に失敗します。
- 制約: loopback IP addressだけを許可し、remote bind addressは拒否します。
- 例: `"mcpHost": "127.0.0.1"`。

#### `server.mcpPort`

- 型/必須: integer、必須。
- 既定値: `45440`。省略すると`0`となり検証に失敗します。
- 範囲: `1`から`65535`。
- 例: `"mcpPort": 45440`。

#### `server.mcpPath`

- 型/必須: 空でないstring、必須。
- 既定値: `/mcp`。省略時は検証に失敗します。
- 制約: `/`で開始し、MCP Client URLと一致させます。
- 例: `"mcpPath": "/mcp"`。

### `itoguruma`

子項目: [`endpoint`](#itogurumaendpoint)、[`authenticationToken`](#itogurumaauthenticationtoken)、[`agentId`](#itogurumaagentid)、[`agentType`](#itogurumaagenttype)、[`connectionTimeoutSeconds`](#itogurumaconnectiontimeoutseconds)、[`pollIntervalSeconds`](#itogurumapollintervalseconds)、[`maxReconnectAttempts`](#itogurumamaxreconnectattempts)、[`receiveBatchSize`](#itogurumareceivebatchsize)、[`leaseSeconds`](#itogurumaleaseseconds)。

#### `itoguruma.endpoint`

- 型/必須: 絶対URI、必須。
- 既定値: `http://127.0.0.1:47631/mcp`。省略時は検証に失敗します。
- 制約: HTTPまたはHTTPSのloopback URIだけを許可します。
- 例: `"endpoint": "http://127.0.0.1:47631/mcp"`。

#### `itoguruma.authenticationToken`

- 型/必須: 空でない秘密string、runtimeで必須。
- 既定値: なし。`hataori.json`から意図的に除外しています。
- 動作: 対話設定は`HATAORI_ITOGURUMA__AUTHENTICATIONTOKEN`、Service設定は秘密fileを使います。値をSource Controlへ保存しないでください。
- 安全な例: `hataori service setup`で設定します。token literalのJSON例は掲載しません。

#### `itoguruma.agentId`

- 型/必須: 空でないstring、必須。
- 既定値: `hataori`。省略時は検証に失敗します。
- 動作: Hataori Service自身が返信を送信するときの送信元IDです。監視対象プロジェクトを限定しません。監視対象は`activation.workingDirectory`直下から自動検出されます。
- 例: `"agentId": "hataori"`。

#### `itoguruma.agentType`

- 型/必須: 空でないstring、必須。
- 既定値: `hataori`。省略時は検証に失敗します。
- 例: `"agentType": "hataori"`。

#### `itoguruma.connectionTimeoutSeconds`

- 型/必須: integer、必須。
- 既定値: `10`。省略すると`0`となり検証に失敗します。
- 範囲: `1`から`120`秒。
- 例: `"connectionTimeoutSeconds": 10`。

#### `itoguruma.pollIntervalSeconds`

- 型/必須: integer、必須。
- 既定値: `5`。省略すると`0`となり検証に失敗します。
- 範囲: `1`から`300`秒。
- 例: `"pollIntervalSeconds": 5`。

#### `itoguruma.maxReconnectAttempts`

- 型/必須: integer、必須。
- 既定値: `5`。省略すると`0`となり検証に失敗します。
- 範囲: `1`から`100`。
- 例: `"maxReconnectAttempts": 5`。

#### `itoguruma.receiveBatchSize`

- 型/必須: integer、必須。
- 既定値: 省略時`50`。
- 範囲: `1`から`500` message。
- 例: `"receiveBatchSize": 50`。

#### `itoguruma.leaseSeconds`

- 型/必須: integer、必須。
- 既定値: 省略時`300`。
- 範囲: `1`から`3600`秒。
- 例: `"leaseSeconds": 300`。

### `agents.codex`

子項目: [`executablePath`](#agentscodexexecutablepath)、[`sandboxMode`](#agentscodexsandboxmode)、[`approveForMe`](#agentscodexapproveforme)、[`model`](#agentscodexmodel)、[`maxCapturedCharacters`](#agentscodexmaxcapturedcharacters)。

#### `agents.codex.executablePath`

- 型/必須: 空でないstring、必須。
- 既定値: 省略時`codex`。
- 動作: Codex CLI起動に使う実行file名または絶対pathです。
- 例: `"executablePath": "codex"`。

#### `agents.codex.sandboxMode`

- 型/必須: string enum、必須。
- 既定値: `workspace-write`。
- 全値: `read-only`はworkspace書込を拒否し、`workspace-write`はAgent sandbox内の書込を許可します。
- 制約: `approveForMe: true`には`workspace-write`が必要です。
- 例: `"sandboxMode": "workspace-write"`。

#### `agents.codex.approveForMe`

- 型/必須: boolean、任意。
- 既定値: `true`。
- 動作: Codex自動承認modeを有効化し、`sandboxMode`は`workspace-write`に限ります。
- 例: `"approveForMe": true`。

#### `agents.codex.model`

- 型/必須: stringまたは`null`、任意。
- 既定値: `null`。Codex側の既定modelを使用します。
- 例: `"model": null`。

#### `agents.codex.maxCapturedCharacters`

- 型/必須: integer、任意。
- 既定値: `4194304`。
- 範囲: `1024`から`16777216`文字。
- 例: `"maxCapturedCharacters": 4194304`。

### `agents.claudeCode`

子項目: [`executablePath`](#agentsclaudecodeexecutablepath)、[`permissionMode`](#agentsclaudecodepermissionmode)、[`model`](#agentsclaudecodemodel)、[`maxCapturedCharacters`](#agentsclaudecodemaxcapturedcharacters)。

#### `agents.claudeCode.executablePath`

- 型/必須: 空でないstring、必須。
- 既定値: 省略時`claude`。
- 例: `"executablePath": "claude"`。

#### `agents.claudeCode.permissionMode`

- 型/必須: string enum、必須。
- 既定値: `acceptEdits`。
- 全値: `acceptEdits`は編集受入を許可し、`plan`はClaude Codeをplan動作へ制限します。
- 例: `"permissionMode": "acceptEdits"`。

#### `agents.claudeCode.model`

- 型/必須: stringまたは`null`、任意。
- 既定値: `null`。Claude Code側の既定modelを使用します。
- 例: `"model": null`。

#### `agents.claudeCode.maxCapturedCharacters`

- 型/必須: integer、任意。
- 既定値: `4194304`。
- 範囲: `1024`から`16777216`文字。
- 例: `"maxCapturedCharacters": 4194304`。

### `activation`

子項目: [`enabled`](#activationenabled)、[`workingDirectory`](#activationworkingdirectory)、[`pollIntervalMilliseconds`](#activationpollintervalmilliseconds)、[`providerPriority`](#activationproviderpriority)、[`maxConcurrentRuns`](#activationmaxconcurrentruns)。

#### `activation.enabled`

- 型/必須: boolean、任意。
- 既定値: `false`。
- 動作: `true`の場合、Queue messageから設定済みAgentを自動起動できます。
- 例: `"enabled": false`。

#### `activation.workingDirectory`

- 型/必須: string、条件付き必須。
- 既定値: 空string。
- 制約: Activation有効時は存在する絶対directoryが必要です。
- 動作: 直下の全directoryをプロジェクトとしてItogurumaへ自動登録・監視するProjects rootです。directory名を宛先プロジェクトIDとして使用します。
- 例: `"workingDirectory": "F:\\Workspace\\Projects"`。

#### `activation.pollIntervalMilliseconds`

- 型/必須: integer、任意。
- 既定値: `1000`。
- 範囲: `100`から`60000`ms。
- 例: `"pollIntervalMilliseconds": 1000`。

#### `activation.providerPriority`

- 型/必須: Provider IDのstring array、必須。
- 既定値: `["codex", "claude-code"]`。
- 動作: 送信元Providerに対象プロジェクトを割り当てられない場合の検索順です。設定ファイルのほか、CLI `hataori provider priority`とMCP Toolsから変更できます。
- 制約: 1件以上で、大文字小文字を無視して重複不可です。Activation有効時は各Providerが`maxConcurrentRuns`に必要です。

#### `activation.maxConcurrentRuns`

- 型/必須: Agent IDをkey、integerをvalueとするobject/dictionary、条件付き必須。
- 既定値: 生成fileでは`{"codex": 2, "claude-code": 2}`。
- 制約: Activation有効時は空でないkeyが1件以上必要で、各valueは`1`から`32`です。
- 例: `"maxConcurrentRuns": {"codex": 2, "claude-code": 2}`。

### `replyRetry`

子項目: [`enabled`](#replyretryenabled)、[`maxAttempts`](#replyretrymaxattempts)、[`initialDelaySeconds`](#replyretryinitialdelayseconds)、[`maximumDelaySeconds`](#replyretrymaximumdelayseconds)、[`batchSize`](#replyretrybatchsize)、[`pollIntervalMilliseconds`](#replyretrypollintervalmilliseconds)。

#### `replyRetry.enabled`

- 型/必須: boolean、任意。
- 既定値: `true`。
- 動作: 未送信Replyの永続Retry処理を有効化します。
- 例: `"enabled": true`。

#### `replyRetry.maxAttempts`

- 型/必須: integer、任意。
- 既定値: `5`。範囲は`1`から`20`です。
- 例: `"maxAttempts": 5`。

#### `replyRetry.initialDelaySeconds`

- 型/必須: integer、任意。
- 既定値: `5`。
- 範囲: `1`から`3600`で、`maximumDelaySeconds`以下です。
- 例: `"initialDelaySeconds": 5`。

#### `replyRetry.maximumDelaySeconds`

- 型/必須: integer、任意。
- 既定値: `300`。
- 範囲: `initialDelaySeconds`以上、`86400`以下です。
- 例: `"maximumDelaySeconds": 300`。

#### `replyRetry.batchSize`

- 型/必須: integer、任意。
- 既定値: `20`。範囲は`1`から`500` Replyです。
- 例: `"batchSize": 20`。

#### `replyRetry.pollIntervalMilliseconds`

- 型/必須: integer、任意。
- 既定値: `1000`。範囲は`100`から`60000`msです。
- 例: `"pollIntervalMilliseconds": 1000`。

### `fileLogging`

子項目: [`enabled`](#fileloggingenabled)、[`directoryPath`](#fileloggingdirectorypath)、[`minimumLevel`](#fileloggingminimumlevel)、[`retentionDays`](#fileloggingretentiondays)。

#### `fileLogging.enabled`

- 型/必須: boolean、任意。
- 既定値: `true`。構造化file logを有効化します。
- 例: `"enabled": true`。

#### `fileLogging.directoryPath`

- 型/必須: 空でないstring、必須。
- 既定値: `logs`。省略時は検証に失敗します。
- 動作: 絶対pathはそのまま使用し、相対pathは`%INSTALL_ROOT%`基準です。
- 例: `"directoryPath": "logs"`。

#### `fileLogging.minimumLevel`

- 型/必須: .NET log level string enum、必須。
- 既定値: `Information`。
- 全値: `Trace`、`Debug`、`Information`、`Warning`、`Error`、`Critical`。`None`は拒否します。
- 例: `"minimumLevel": "Information"`。

#### `fileLogging.retentionDays`

- 型/必須: integer、任意。
- 既定値: `30`。範囲は`1`から`3650`日です。
- 例: `"retentionDays": 30`。

### `databaseMaintenance`

子項目: [`enabled`](#databasemaintenanceenabled)、[`intervalHours`](#databasemaintenanceintervalhours)、[`staleTaskHours`](#databasemaintenancestaletaskhours)、[`taskRetentionDays`](#databasemaintenancetaskretentiondays)、[`agentRunRetentionDays`](#databasemaintenanceagentrunretentiondays)、[`messageRetentionDays`](#databasemaintenancemessageretentiondays)、[`vacuum`](#databasemaintenancevacuum)。

#### `databaseMaintenance.enabled`

- 型/必須: boolean、任意。
- 既定値: `true`。Stale state expiry、Retention purge、任意のvacuumを有効化します。
- 例: `"enabled": true`。

#### `databaseMaintenance.intervalHours`

- 型/必須: integer、任意。
- 既定値: `24`。範囲は`1`から`720`時間です。
- 例: `"intervalHours": 24`。

#### `databaseMaintenance.staleTaskHours`

- 型/必須: integer、任意。
- 既定値: `24`。範囲は`1`から`8760`時間です。
- 例: `"staleTaskHours": 24`。

#### `databaseMaintenance.taskRetentionDays`

- 型/必須: integer、任意。
- 既定値: `90`。範囲は`1`から`3650`日です。
- 例: `"taskRetentionDays": 90`。

#### `databaseMaintenance.agentRunRetentionDays`

- 型/必須: integer、任意。
- 既定値: `30`。範囲は`1`から`3650`日です。
- 例: `"agentRunRetentionDays": 30`。

#### `databaseMaintenance.messageRetentionDays`

- 型/必須: integer、任意。
- 既定値: `30`。範囲は`1`から`3650`日です。
- 例: `"messageRetentionDays": 30`。

#### `databaseMaintenance.vacuum`

- 型/必須: boolean、任意。
- 既定値: `true`。Retention処理後のMaintenanceでSQLite `VACUUM`を要求します。
- 例: `"vacuum": true`。

### `hooks`

子項目: [`enabled`](#hooksenabled)、[`codexConfigPath`](#hookscodexconfigpath)、[`claudeConfigPath`](#hooksclaudeconfigpath)。

#### `hooks.enabled`

- 型/必須: boolean、任意。
- 既定値: 生成fileでは`true`。Lifecycle Hook診断とtemplateを有効化します。
- 例: `"enabled": true`。

#### `hooks.codexConfigPath`

- 型/必須: Hook有効時に空でないstring。
- 既定値: `bin/cli/hooks/codex-hooks.json`。相対pathは`%INSTALL_ROOT%`基準です。
- 例: `"codexConfigPath": "bin/cli/hooks/codex-hooks.json"`。

#### `hooks.claudeConfigPath`

- 型/必須: Hook有効時に空でないstring。
- 既定値: `bin/cli/hooks/claude-settings.json`。相対pathは`%INSTALL_ROOT%`基準です。
- 例: `"claudeConfigPath": "bin/cli/hooks/claude-settings.json"`。

### `allowedHosts`

- 型/必須: semicolon区切りのhost string、任意。
- 既定値: `127.0.0.1;localhost`。
- 動作: loopback bind addressとは別にASP.NET Core Host Filteringを設定します。
- 例: `"allowedHosts": "127.0.0.1;localhost"`。

## Samples

古い文書断片をcopyせず、完全な秘密情報なしsampleを生成します。

```powershell
hataori config init
hataori config check
hataori config show
```

`config show`は秘密情報と判断したkeyをmaskします。別fileは次のように検証します。

```powershell
hataori config check --config F:\SafePath\hataori.json
```
