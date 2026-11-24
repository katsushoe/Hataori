# Hataori設定

[English](CONFIG.md) | [日本語](CONFIG.ja.md)

この文書はHataoriの設定file、優先順位、全設定項目、制約、安全なsampleの正本です。

## Configuration Directory

| File | 標準配置 | 作成主体 | 用途 |
| :--- | :--- | :--- | :--- |
| 通常設定 | `%INSTALL_ROOT%\config\hataori.json` | MSI、利用者、または`hataori config init` | 秘密情報を含まないアプリ言語、Server、Agent、Retry、Log、Maintenance、Hook設定。 |
| ユーザー秘密設定 | `%INSTALL_ROOT%\config\hataori.user.json` | 旧版移行手順 | 対象ユーザー、SYSTEM、Administratorsに制限した任意のItoguruma認証。 |
| Monitor設定 | `%LOCALAPPDATA%\Hataori\Monitor\settings.json` | Monitorの設定画面 | Server設定から独立したユーザー単位の設定。 |

相対`databasePath`、log、hook pathは`%INSTALL_ROOT%`を基準に解決します。`HATAORI_CONFIG_PATH`へ絶対pathを指定すると別の通常設定fileを使用できます。

## File Generation

- 新規インストール時、MSIは選択された言語を含む`hataori.json`を作成します。アップグレード時は既存fileを保持します。
- `hataori config init [--language <ja-JP|en-US>]`は出力先が存在しない場合だけ組込み既定`hataori.json`を作成します。
- `hataori service setup`は`hataori.service.json`を作成または置換し、ACLを`SYSTEM`と`Administrators`だけに制限します。
- tokenの実値をsample、Source Control、log、chatへ記載しないでください。

## Monitor Settings

Windowsのスタートメニューで **Hataori Monitor** を開き、画面上部の **設定** ボタン、またはトレイアイコンの右クリックメニューの **設定** を選びます。**保存** で設定を原子的に保存し、**キャンセル** なら変更しません。保存先は設定画面に表示します。Server設定や認証情報は編集しません。MSIのアップグレード・アンインストール後も設定は保持します。

全キーは任意です。Server用環境変数による上書きは適用しません。

| キー | 型 | 既定値 | 有効値 |
| :--- | :--- | :--- | :--- |
| `close_to_tray` | boolean | `true` | `true`、`false` |
| `start_minimized` | boolean | `false` | `true`、`false` |
| `auto_update` | boolean | `true` | `true`、`false` |
| `refresh_seconds` | integer | `3` | 1～300 |
| `language` | string | `ja-JP`。初回画面は通常設定の言語を継承 | `ja-JP`、`en-US` |

### close_to_tray

booleanで、既定値・省略時は`true`です。例: `{"close_to_tray":true}`。閉じるボタンでトレイへ格納します。`false`なら閉じるボタンでMonitorを終了します。最小化は常にトレイへ格納します。**終了** とWindows終了時は通常どおり終了します。同じユーザー・セッション・インストール先・接続先で再起動すると既存のMonitor画面を開きます。

### start_minimized

booleanで、既定値・省略時は`false`です。例: `{"start_minimized":true}`。次回起動時に画面をトレイへ格納して起動します。Windowsログオン時の自動起動登録は行いません。

### auto_update

booleanで、既定値・省略時は`true`です。例: `{"auto_update":false}`。次回Monitor起動時に新しい正式版MSIを確認し、公式URL・サイズ・APIのSHA-256を検証して自動インストールします。更新先は`src/Hataori.Monitor/monitor-update.json`から組み込み、利用者が任意のダウンロード先へ変更する設定は提供しません。標準の`bin/monitor`配置へMSIでインストールした環境だけで更新できます。無効時も **アプリの更新** ボタンで手動確認できます。ユーザー単位MSIでServer・CLI・Monitorを通常権限で更新します。インストール失敗後は既存のMonitorを再起動し、更新確認を1回省略します。Windowsを自動再起動しません。終了コード3010では手動再起動を案内します。

起動時の取得失敗はトレイ通知とログへ記録し、Monitorは引き続き使用できます。手動確認の失敗はダイアログで案内します。オフライン、MSI・digest欠落、非公開Release、API制限、不正メタデータは回復可能なエラーです。他アプリの認証情報は読みません。更新ファイルとインストールログは`%LOCALAPPDATA%\Hataori\Monitor\updates`に残します。古いディレクトリは更新処理がないときだけ削除してください。20分の期限後もWindows Installerが動作中なら強制終了せず、その完了を待ってからMonitorを開いてください。

### refresh_seconds

integerで、既定値・省略時は`3`、有効範囲は1～300です。例: `{"refresh_seconds":10}`。保存直後に反映し、画面表示中・トレイ格納中のスナップショット更新間隔へ適用します。

### language

stringで、`ja-JP`（日本語）または`en-US`（英語）、キー省略時は`ja-JP`です。例: `{"language":"en-US"}`。Monitor設定ファイルがまだない初回は通常設定の`application.language`を継承します。保存済みのMonitor言語を優先し、保存直後に反映します。Serverの言語は変更しません。

設定を読み込めない、または検証に失敗した場合は元ファイルを保持し、エラーを案内して、その起動に限り自動更新を無効にした既定設定を使います。設定画面を開いて保存すると不正なファイルを置き換えます。不正値や書き込み失敗時は前の設定を保持します。

## Main Settings

Serverは次の順で設定を読み、後のsourceが前を上書きします。

1. .NET Host既定設定。
2. `HATAORI_CONFIG_PATH`または標準pathで選択した通常`hataori.json`。
3. インストール先の設定フォルダーにある任意の`hataori.user.json`。
4. `HATAORI_`接頭辞の環境変数。JSON階層には`__`を使います。例: `HATAORI_SERVER__MCPPORT=45440`。

標準のItoguruma接続先 `http://127.0.0.1:47631/mcp` では、Windows資格情報マネージャーにログオンユーザーのItoguruma資格情報があれば、Serverが直接読み取って優先します。設定ファイルや環境変数には複製しません。

Server設定を読むCLI commandは通常JSONの後に`HATAORI_`環境変数を適用します。対応commandでは`--config`で通常JSONを選択します。CLI専用path変数は[コマンド](COMMANDS.ja.md)を参照してください。

既定file内の各sectionはServer起動に必要です。`hooks`だけは無効として省略できます。`itoguruma.authenticationToken`は意図的に通常fileへ含めず、ユーザー版は保護されたユーザー秘密設定またはユーザー環境変数から取得します。

## Profile Settings

Hataoriには名前付きprofile fileがありません。通常file path、環境変数override、任意のユーザー秘密設定で実行時の設定を分けます。MCPは認証なし・loopback限定で、Itoguruma認証は別のoutbound client設定です。

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
- 動作: Hataori Service自身が返信を送信するときの送信元IDです。監視対象プロジェクトを限定しません。監視対象は`activation.workspaces`の各root直下から自動検出されます。
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

子項目: [`enabled`](#activationenabled)、[`workspaces`](#activationworkspaces)、後方互換用の[`workspaceId`](#activationworkspaceid)と[`workingDirectory`](#activationworkingdirectory)、[`pollIntervalMilliseconds`](#activationpollintervalmilliseconds)、[`providerPriority`](#activationproviderpriority)、[`maxConcurrentRuns`](#activationmaxconcurrentruns)。

#### `activation.enabled`

- 型/必須: boolean、任意。
- 既定値: `false`。
- 動作: `true`の場合、Queue messageから設定済みAgentを自動起動できます。
- 例: `"enabled": false`。

#### `activation.workingDirectory`

- 型/必須: string、条件付き必須。
- 既定値: 空string。
- 制約: Activation有効時は存在する絶対directoryが必要です。
- 動作: `workspaces`未設定時だけ使用する後方互換用の単一Projects rootです。
- 例: `"workingDirectory": "F:\\Workspace\\Projects"`。

#### `activation.workspaceId`

- 型/必須: string、任意。
- 既定値: `"default"`。
- 制約: `^[a-z][a-z0-9]*$`。入力値は小文字へ正規化されます。
- 動作: `workspaces`未設定時だけ、`workingDirectory`で構成したWorkspaceを識別します。
- 例: `"workspaceId": "default"`。

#### `activation.workspaces`

- 型/必須: `workspaceId`と`workingDirectory`を持つobject array、Activation有効時は条件付き必須。
- 既定値: 生成fileでは`default` Workspace 1件。
- 制約: Workspace ID、絶対directory、直下から算出されるProject IDは全Workspace間で重複不可です。`workingDirectory`とは併用できません。
- 動作: 各root直下の全directoryをプロジェクトとしてItogurumaへ登録・監視し、受信Messageへ対応するWorkspace IDを保存します。
- 例: `"workspaces": [{"workspaceId": "main", "workingDirectory": "F:\\Workspace\\Projects"}, {"workspaceId": "labs", "workingDirectory": "F:\\Workspace\\Labs"}]`。

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

このmapはWorkspaceに永続化済みAgent定義がない初回だけ登録値として使います。初期化後はSQLiteのAgent定義が正本です。変更は`hataori agent set`で行い、Service再起動後に反映します。

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

子項目: [`enabled`](#hooksenabled)、[`codexConfigPath`](#hookscodexconfigpath)、[`claudeConfigPath`](#hooksclaudeconfigpath)、[`claudePluginSettingsPath`](#hooksclaudepluginsettingspath)、[`requiredClaudePlugin`](#hooksrequiredclaudeplugin)。

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

#### `hooks.claudePluginSettingsPath`

- 型／必須: string、任意。
- 既定値: 空。`doctor`は`%USERPROFILE%\.claude\settings.json`を読み取ります。
- 動作: 読み取り専用のプラグイン有効化確認に使う別のClaude Code設定fileを指定します。Hataoriはこのfileを書き換えません。
- 例: `"claudePluginSettingsPath": ""`。

#### `hooks.requiredClaudePlugin`

- 型／必須: string、任意。
- 生成fileの既定値: `kotodama-spec-guard@katsushoe-private`。
- 動作: 空でない場合、`doctor`は`claude_plugins`診断を追加し、`enabledPlugins`で完全一致するkeyが`true`であることを要求します。空の場合は、既存設定との後方互換性のためこの診断だけを無効にします。
- 例: `"requiredClaudePlugin": "kotodama-spec-guard@katsushoe-private"`。

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
