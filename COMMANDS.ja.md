# Hataoriコマンド

[English](COMMANDS.md) | [日本語](COMMANDS.ja.md)

この文書はHataori CLI、Service制御、連携確認、JSON戻り値、終了Code、安全要件の正本です。

## Command Groups

| Group | Command | 用途 |
| :--- | :--- | :--- |
| [Server](#server-commands) | `start`、`stop`、`restart`、`status` | 実行fileとControl Pipeによるforeground Server管理。 |
| [Service](#service-commands) | `service setup/install/uninstall/start/stop/restart/status` | Windows Service設定と制御。 |
| [Task](#task-commands) | `task start/get/list/find-conflicts/heartbeat/complete/cancel/fail/expire/history/relation-add/relations` | 永続TaskとRelation管理。 |
| [Agent](#agent-commands) | `agent list/status/runs` | 設定済みAgentとRun参照。 |
| [Conversation](#conversation-commands) | `conversation list/get/reset` | Conversation Session参照と無効化。 |
| [Queue](#queue-commands) | `queue list/get/retry/cancel` | Queue Message参照と操作。 |
| [Database](#database-commands) | `db status/integrity` | 読み取り専用SQLite診断。 |
| [Configuration](#configuration-commands) | `config init/show/path/check/reload` | 設定生成、参照、検証、reload。 |
| [Integration](#integration-commands) | `setup itoguruma`、`itoguruma status/test`、`mcp status` | 外部接続設定と確認。 |
| [Diagnostics and UI](#diagnostics-and-ui-commands) | `doctor`、`logs`、`monitor`、`hook` | 診断、log参照、Monitor起動、Hook処理。 |
| [Metadata](#metadata-commands) | `version`、`help` | VersionとUsage表示。 |

## Common Options

- `logs --follow`のstream行を除き、標準出力はindent済みJSONです。Errorは標準errorへ出力します。
- `--config <path>`または`HATAORI_CONFIG_PATH`は通常JSONを選択します。
- `--database <path>`または`HATAORI_DATABASE_PATH`はTask、Agent、Conversation、Queue、DB用SQLiteを選択し、明示が必要です。
- `--pipe <name>`または`HATAORI_CONTROL_PIPE_NAME`はControl Pipeを選択します。
- `--timeout-seconds <1..300>`または`HATAORI_CONTROL_TIMEOUT_SECONDS`はPipe timeoutを指定し、既定値は`10`です。
- `--server <path>`または`HATAORI_SERVER_PATH`はforeground `start`とService `install`用Server実行fileを選択します。
- `--json`は互換flagとして受け付けます。通常出力もJSONです。

| 終了Code | 意味 |
| ---: | :--- |
| `0` | 成功または要求されたcancel。 |
| `1` | CLI境界で変換した予期しない失敗。 |
| `2` | Command、引数、option、値が不正。 |
| `3` | 必須file/endpointなし、またはtimeout。 |
| `4` | 指定した永続entityなし。 |
| `5` | Runtime状態不正、外部操作またはService操作失敗。 |
| `6` | I/OまたはControl Pipe失敗。 |
| `9` | SQLite失敗。 |

## Commands

### Server Commands

Command: [`start`](#hataori-start)、[`stop`](#hataori-stop)、[`restart`](#hataori-restart)、[`status`](#hataori-status)。

#### `hataori start`

- 目的: foreground Hataori Server processを起動します。
- 構文: `hataori start --server <exe>`。
- 引数: `--server`または`HATAORI_SERVER_PATH`が必須です。
- 処理・戻り値: 実行fileを検証して待機せず起動し、started状態とprocess metadataのJSONを返します。
- 例: `hataori start --server F:\Hataori\bin\server\Hataori.Server.exe`。
- 安全: 同じDBとPipeを使うWindows Serviceと同時起動しないでください。

#### `hataori stop`

- 目的: Control PipeからGraceful Shutdownを要求します。
- 構文: `hataori stop --pipe <name> [--timeout-seconds <n>]`。
- 引数: 環境変数がなければ`--pipe`が必須です。
- 処理・戻り値: `stop`を送信し、Server状態に基づくControl Pipe response JSONを返します。
- 例: `hataori stop --pipe hataori-control`。
- 安全: Active Agent作業を中断できることを確認してください。

#### `hataori restart`

- 目的: foreground ServerをGraceful Stopして新processを起動します。
- 構文: `hataori restart --pipe <name> --server <exe> [--timeout-seconds <n>]`。
- 引数: PipeとServer実行fileが必須です。
- 処理・戻り値: Pipe閉鎖まで待って起動し、process-start JSONを返します。
- 例: `hataori restart --pipe hataori-control --server F:\Hataori\bin\server\Hataori.Server.exe`。
- 安全: 導入済みServiceには`service restart`を使います。

#### `hataori status`

- 目的: Control Pipeからforeground Server状態を読みます。
- 構文: `hataori status --pipe <name> [--timeout-seconds <n>]`。
- 引数: Pipeが必須です。
- 処理・戻り値: 読み取り専用`status`を送り、現在のServer/Worker状態由来のJSONを返します。
- 例: `hataori status --pipe hataori-control`。
- 安全: 読み取り専用です。

### Service Commands

Command: [`setup`](#hataori-service-setup)、[`install`](#hataori-service-install)、[`uninstall`](#hataori-service-uninstall)、[`start`](#hataori-service-start)、[`stop`](#hataori-service-stop)、[`restart`](#hataori-service-restart)、[`status`](#hataori-service-status)。

#### `hataori service setup`

- 目的: Itoguruma tokenを表示せずWindows Serviceへ連携します。
- 構文: `hataori service setup`。
- 引数: User scope、次にprocess scopeの`ITOGURUMA_AUTH_TOKEN`を読みます。
- 処理・戻り値: ACLを`SYSTEM`/`Administrators`へ制限した秘密fileを書き、`configured`、`configuration_path`、`restart_required`を返します。
- 例: `hataori service setup`。
- 安全: 管理者terminalが必要で、Service秘密fileだけを置換します。

#### `hataori service install`

- 目的: `sc.exe create`でWindows Serviceを登録します。
- 構文: `hataori service install --server <exe> [--name <service>]`。
- 引数: `--server`必須、`--name`既定値は`Hataori`です。
- 処理・戻り値: Automatic Serviceを登録し、`service_name`、`command`、`success`、`output`のJSONを返します。
- 例: `hataori service install --server F:\Hataori\bin\server\Hataori.Server.exe`。
- 安全: 管理者権限が必要です。MSIは既にServiceを登録します。

#### `hataori service uninstall`

- 目的: 選択したWindows Service登録を削除します。
- 構文: `hataori service uninstall [--name <service>]`。
- 引数: `--name`既定値は`Hataori`です。
- 処理・戻り値: `sc.exe delete`の実結果をJSONで返します。
- 例: `hataori service uninstall --name Hataori-Test`。
- 安全: 破壊的です。MSI管理対象はMSI Uninstallを優先します。

#### `hataori service start`

- 目的: 選択したWindows Serviceを起動します。
- 構文: `hataori service start [--name <service>]`。
- 引数: `--name`既定値は`Hataori`です。
- 処理・戻り値: `sc.exe start`を実行し、Service command JSONを返します。
- 例: `hataori service start`。
- 安全: 先に`service setup`を完了してください。

#### `hataori service stop`

- 目的: 選択したWindows Serviceを停止します。
- 構文: `hataori service stop [--name <service>]`。
- 引数: `--name`既定値は`Hataori`です。
- 処理・戻り値: `sc.exe stop`を実行し、Service command JSONを返します。
- 例: `hataori service stop`。
- 安全: Active Agent作業を中断する可能性があります。

#### `hataori service restart`

- 目的: 選択したServiceを停止後に起動します。
- 構文: `hataori service restart [--name <service>]`。
- 引数: `--name`既定値は`Hataori`です。
- 処理・戻り値: `sc.exe stop`成功後に`start`し、最終start結果JSONを返します。
- 例: `hataori service restart`。
- 安全: Active Agent作業を中断します。

#### `hataori service status`

- 目的: 選択したWindows Serviceを照会します。
- 構文: `hataori service status [--name <service>]`。
- 引数: `--name`既定値は`Hataori`です。
- 処理・戻り値: `sc.exe query`の実出力を含むJSONを返します。
- 例: `hataori service status`。
- 安全: 読み取り専用です。

### Task Commands

Command: [`start`](#hataori-task-start)、[`get`](#hataori-task-get)、[`list`](#hataori-task-list)、[`find-conflicts`](#hataori-task-find-conflicts)、[`heartbeat`](#hataori-task-heartbeat)、[`complete`](#hataori-task-complete)、[`cancel`](#hataori-task-cancel)、[`fail`](#hataori-task-fail)、[`expire`](#hataori-task-expire)、[`history`](#hataori-task-history)、[`relation-add`](#hataori-task-relation-add)、[`relations`](#hataori-task-relations)。全commandで`--database`または`HATAORI_DATABASE_PATH`が必要です。

#### `hataori task start`

- 目的: Active Taskを作成します。
- 構文: `hataori task start --id <id> --name <name> --agent <id> [--conversation <id>] [--message <id>] [--summary <text>] [--current-work <text>] --database <path>`。
- 引数: `id`、`name`、`agent`必須、contextは任意です。
- 処理・戻り値: 一意性を検証し初期historyとTaskを書き、入力contextと永続時刻に応じたTask JSONを返します。
- 例: `hataori task start --id DOC-1 --name docs --agent codex --database F:\Hataori\data\hataori.db`。
- 安全: 状態変更です。Task IDは一意にします。

#### `hataori task get`

- 目的: Task、history、relationを読みます。
- 構文: `hataori task get <id> --database <path>`。
- 引数: Task IDは位置引数または`--id`です。
- 処理・戻り値: `task`、順序付き`history`、`relations`のJSONを返し、未存在時は終了`4`です。
- 例: `hataori task get DOC-1 --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori task list`

- 目的: 永続Taskをfilterして一覧化します。
- 構文: `hataori task list [--status <status>] [--agent <id>] [--conversation <id>] [--all] --database <path>`。
- 引数: Statusは`active/completed/cancelled/failed/expired`、既定`active`、`--all`でStatus filterなしです。
- 処理・戻り値: SQLiteと指定filterに応じたTask JSON arrayを返します。
- 例: `hataori task list --all --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori task find-conflicts`

- 目的: 作業名・概要のキーワードから、他AgentのActive Taskとの重複候補を検索します。
- 構文: `hataori task find-conflicts --name <name> [--summary <text>] [--agent <exclude-agent>] --database <path>`。
- 引数: `--name`必須、`--summary`は任意、`--agent`は検索結果から除外する自Agent IDです。
- 処理・戻り値: MCP `task_find_conflicts`と同じApplication Serviceを使用し、候補TaskのJSON arrayを返します。結果は参考情報です。
- 例: `hataori task find-conflicts --name "認証処理を修正" --summary "ログイン画面" --agent codex --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori task heartbeat`

- 目的: Active Taskのcurrent workと進捗率を更新します。
- 構文: `hataori task heartbeat <id> --current-work <text> --progress <0..100> [--message <id>] --database <path>`。
- 引数: Task ID、current work、integer progressが必須です。
- 処理・戻り値: Active状態を検証してhistoryを追記し、更新Task JSONを返します。
- 例: `hataori task heartbeat DOC-1 --current-work "Writing" --progress 50 --database F:\Hataori\data\hataori.db`。
- 安全: 状態変更です。正確な進捗率を必ず指定します。

#### `hataori task complete`

- 目的: Active TaskをCompletedへ変更します。
- 構文: `hataori task complete <id> (--result <text> | --message <text>) --database <path>`。
- 引数: Task IDと結果text必須、`--message`優先です。
- 処理・戻り値: Terminal遷移とhistoryを保存しCompleted Task JSONを返します。
- 例: `hataori task complete DOC-1 --result "Done" --database F:\Hataori\data\hataori.db`。
- 安全: Terminal状態変更です。対象と結果を確認します。

#### `hataori task cancel`

- 目的: Active TaskをCancelledへ変更します。
- 構文: `hataori task cancel <id> [--result <text> | --message <text>] --database <path>`。
- 引数: Task ID必須、結果は任意です。
- 処理・戻り値: Terminal Cancelled遷移を保存しTask JSONを返します。
- 例: `hataori task cancel DOC-1 --result "Superseded" --database F:\Hataori\data\hataori.db`。
- 安全: 破壊的状態変更です。Task IDを確認します。

#### `hataori task fail`

- 目的: Active TaskをFailedへ変更します。
- 構文: `hataori task fail <id> --result <text> --database <path>`。
- 引数: Task IDと失敗理由が必須です。
- 処理・戻り値: Terminal Failed遷移と理由を保存しTask JSONを返します。
- 例: `hataori task fail DOC-1 --result "Validation failed" --database F:\Hataori\data\hataori.db`。
- 安全: 破壊的状態変更です。理由へ秘密情報を書きません。

#### `hataori task expire`

- 目的: inactive TaskをExpiredへ変更します。
- 構文: `hataori task expire <id> --database <path>`。
- 引数: Task IDが必須です。
- 処理・戻り値: 適格性を検証してTerminal Expired遷移を保存しTask JSONを返します。
- 例: `hataori task expire DOC-1 --database F:\Hataori\data\hataori.db`。
- 安全: 破壊的状態変更です。通常はMaintenanceが処理します。

#### `hataori task history`

- 目的: Task historyを順序付きで読みます。
- 構文: `hataori task history <id> --database <path>`。
- 引数: Task ID必須です。
- 処理・戻り値: 記録時刻順のHistory JSON arrayを返します。
- 例: `hataori task history DOC-1 --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori task relation-add`

- 目的: 既存Task間へ冪等Relationを追加します。
- 構文: `hataori task relation-add --id <id> --related-id <id> --type <text> --database <path>`。
- 引数: 両Task IDと空でないTypeが必須です。
- 処理・戻り値: Taskを検証して未存在時だけ追加しRelation JSONを返します。
- 例: `hataori task relation-add --id DOC-1 --related-id DOC-2 --type blocks --database F:\Hataori\data\hataori.db`。
- 安全: 状態変更ですが同一Relationには冪等です。

#### `hataori task relations`

- 目的: Taskに関係する全Relationを読みます。
- 構文: `hataori task relations --id <id> --database <path>`。
- 引数: `--id`必須です。
- 処理・戻り値: inbound/outbound Relation JSON arrayを返します。
- 例: `hataori task relations --id DOC-1 --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

### Agent Commands

Command: [`list`](#hataori-agent-list)、[`status`](#hataori-agent-status)、[`runs`](#hataori-agent-runs)、[`cancel`](#hataori-agent-cancel)。`list/status/runs`はDB path必須、`cancel`はControl Pipeを使用しDBを使いません。

#### `hataori agent list`

- 目的: 設定済みAgent summaryを一覧化します。
- 構文: `hataori agent list --database <path> [--config <path>]`。
- 引数: DB必須です。
- 処理・戻り値: Driver設定、Activation上限、Running件数を統合し、`agent_id/enabled/running/max_runs` arrayを返します。
- 例: `hataori agent list --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori agent status`

- 目的: 1 Agent summaryを読みます。
- 構文: `hataori agent status <agent-id> --database <path> [--config <path>]`。
- 引数: Agent IDは位置引数または`--agent`、DB必須です。
- 処理・戻り値: Codex/Claude設定から一致summaryを返し、未知Agentは終了`4`です。
- 例: `hataori agent status codex --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori agent runs`

- 目的: 永続Agent Runを一覧化します。
- 構文: `hataori agent runs [--status <status>] [--agent <id>] --database <path>`。
- 引数: Statusは`queued/starting/running/completed/failed/cancelled`です。
- 処理・戻り値: Filterと永続状態に応じたRun JSON arrayを返します。
- 例: `hataori agent runs --status running --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori agent cancel`

- 目的: queued／starting／running状態のAgent Runを中断し、実行中Processがあれば終了させます。
- 構文: `hataori agent cancel <run-id> [--pipe <name>] [--timeout-seconds <1..300>]`。
- 引数: Run IDは位置引数または`--run`です。`--database`は使いません（DBを直接触らず、稼働中のServer経由でRunへ到達するため）。
- 処理・戻り値: Run IDをControl Pipe経由で`agent-cancel`として送信します。`{"run_id": ..., "status": "cancelled" | "cancelled_db_only"}`を返し、未知のRun IDは終了`4`です。MCP tool `agent_run_cancel`は同じ実行中Process registryへ直接到達するため、下記の制約を受けません。
- 例: `hataori agent cancel run-1a2b3c`。
- 安全: 破壊的操作です。`start/stop/restart/status`と同様、Control PipeはHataori Serviceを実行しているアカウントに限定されるため、このCLI経路は同一アカウントから呼び出した場合のみ実行中Processへ到達できます。確実にAgentから中断する場合はMCP toolを使用してください。

### Conversation Commands

Command: [`list`](#hataori-conversation-list)、[`get`](#hataori-conversation-get)、[`reset`](#hataori-conversation-reset)。

#### `hataori conversation list`

- 目的: 永続Conversation Sessionを一覧化します。
- 構文: `hataori conversation list [--status <status>] [--agent <id>] --database <path>`。
- 引数: Statusは`idle/running/invalid`、DB必須です。
- 処理・戻り値: Filterに応じたSession JSON arrayを返します。
- 例: `hataori conversation list --status running --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori conversation get`

- 目的: 1 Conversation Sessionを読みます。
- 構文: `hataori conversation get <conversation-id> --agent <id> --database <path>`。
- 引数: Conversation ID、Agent ID、DB必須です。
- 処理・戻り値: Composite keyでSession JSONを返し、未存在は終了`4`です。
- 例: `hataori conversation get conv-1 --agent codex --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori conversation reset`

- 目的: Sessionを無効化して次回Activationで再生成可能にします。
- 構文: `hataori conversation reset <conversation-id> --agent <id> --database <path>`。
- 引数: Conversation ID、Agent ID、DB必須です。
- 処理・戻り値: 状態を`invalid`へ変更し更新Session JSONを返します。
- 例: `hataori conversation reset conv-1 --agent codex --database F:\Hataori\data\hataori.db`。
- 安全: 破壊的状態変更で、会話継続性を失います。

### Queue Commands

Command: [`list`](#hataori-queue-list)、[`get`](#hataori-queue-get)、[`retry`](#hataori-queue-retry)、[`cancel`](#hataori-queue-cancel)。

#### `hataori queue list`

- 目的: Queue Messageを一覧化します。
- 構文: `hataori queue list [--agent <id>] --database <path>`。
- 引数: DB必須、Agent filter任意です。
- 処理・戻り値: 永続Queue Message JSON arrayを返します。
- 例: `hataori queue list --agent codex --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori queue get`

- 目的: 1 Queue Messageを読みます。
- 構文: `hataori queue get <message-id> --database <path>`。
- 引数: Message IDとDB必須です。
- 処理・戻り値: 永続Message JSONを返し、未存在は終了`4`です。
- 例: `hataori queue get msg-1 --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用ですがMessage本文は機密を含む場合があります。

#### `hataori queue retry`

- 目的: Queue Messageを直ちにRetry可能にします。
- 構文: `hataori queue retry <message-id> --database <path>`。
- 引数: Message IDとDB必須です。
- 処理・戻り値: 現在UTC時刻でRetry状態を更新しMessage JSONを返します。
- 例: `hataori queue retry msg-1 --database F:\Hataori\data\hataori.db`。
- 安全: Agent実行やReply送信を起こす状態変更です。

#### `hataori queue cancel`

- 目的: Queue MessageをCancelします。
- 構文: `hataori queue cancel <message-id> --database <path>`。
- 引数: Message IDとDB必須です。
- 処理・戻り値: 現在UTC時刻でcancelを保存し、`message_id`と`status: "cancelled"`を返します。
- 例: `hataori queue cancel msg-1 --database F:\Hataori\data\hataori.db`。
- 安全: 破壊的状態変更で、Messageは通常処理されません。

### Codex Desktop Task Launch Commands

MCP Tools `codex_task_claim`／`codex_task_started`／`codex_task_release`と、次のCLIは同じApplication ServiceとSQLite状態を使用します。

- `hataori codex claim [--lease-seconds <30..3600>] --database <path>`: 次のCodex起動要求を期限付きで取得します。要求がなければ`{"status":"empty"}`です。
- `hataori codex started <message-id> --claim-token <token> --codex-task-id <task-id> --database <path>`: `create_thread`成功後のCodex Task IDを保存し、元MessageをQueueから除きます。
- `hataori codex release <message-id> --claim-token <token> --error <message> --database <path>`: 起動失敗したclaimを解放し、元Messageを直ちに再取得可能にします。

Codex Desktop内の固定受信Taskは、claim結果の`project_name`を保存済みProjectから解決し、`prompt`でTaskを作成します。完了応答の同期はこのコマンド群の対象外です。

### Database Commands

Command: [`status`](#hataori-db-status)、[`integrity`](#hataori-db-integrity)。どちらもSQLiteをread-onlyで開きます。

#### `hataori db status`

- 目的: DBの基本metadataを読みます。
- 構文: `hataori db status --database <path>`。
- 引数: 存在するDB path必須です。
- 処理・戻り値: Application table数を数え、`path/exists/table_count/size_bytes`を返します。
- 例: `hataori db status --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用です。

#### `hataori db integrity`

- 目的: SQLite `PRAGMA integrity_check`を実行します。
- 構文: `hataori db integrity --database <path>`。
- 引数: 存在するDB path必須です。
- 処理・戻り値: `ok`とSQLite raw `result`を返し、結果が`ok`の場合だけtrueです。
- 例: `hataori db integrity --database F:\Hataori\data\hataori.db`。
- 安全: 読み取り専用ですが大きなDBではI/O負荷があります。

### Configuration Commands

Command: [`init`](#hataori-config-init)、[`show`](#hataori-config-show)、[`path`](#hataori-config-path)、[`check`](#hataori-config-check)、[`reload`](#hataori-config-reload)。

#### `hataori config init`

- 目的: 秘密情報なしの組込み既定設定を作成します。
- 構文: `hataori config init [--config <path>] [--language <ja-JP|en-US>]`。
- 引数: 保存先の省略時は標準通常設定path、言語の省略時は組込み既定値`ja-JP`です。
- 処理・戻り値: Directoryを作成しcreate-newで保存して`path/created/language`を返し、既存fileなら`created: false`です。
- 例: `hataori config init`。
- 安全: 既存設定を上書きしません。

#### `hataori config show`

- 目的: 有効な通常設定値を表示します。
- 構文: `hataori config show [--config <path>]`。
- 引数: Fileが存在する必要があります。
- 処理・戻り値: JSONと`HATAORI_` overrideを読み、secret-like keyをmaskした`path/values`を返します。
- 例: `hataori config show`。
- 安全: Mask後も共有前に内容を確認します。

#### `hataori config path`

- 目的: 選択した通常設定pathを解決します。
- 構文: `hataori config path [--config <path>]`。
- 引数: Fileは未作成でも構いません。
- 処理・戻り値: 絶対`path`と`exists`を返します。
- 例: `hataori config path`。

#### `hataori provider priority get`

- 目的: 起動Agent自動選択で使用するProvider優先順位を取得します。
- 構文: `hataori provider priority get [--config <path>]`。
- 戻り値: 優先順位どおりの`providers`配列です。

#### `hataori provider priority set`

- 目的: Provider優先順位を設定ファイルへ保存します。
- 構文: `hataori provider priority set --providers <ID,ID> [--config <path>]`。
- 制約: 1件以上、大文字小文字を無視して重複不可です。Serverは設定変更を自動読込します。
- 例: `hataori provider priority set --providers codex,claude-code`。
- 安全: 読み取り専用です。

#### `hataori config check`

- 目的: Serverと同じValidatorで設定を検証します。
- 構文: `hataori config check [--config <path>]`。
- 引数: Fileが存在する必要があります。
- 処理・戻り値: 有効設定を検証し、`path/valid/errors`を返します。
- 例: `hataori config check`。
- 安全: 読み取り専用で、errorへtoken値を含めません。

#### `hataori config reload`

- 目的: Running Serverへ設定reloadを要求します。
- 構文: `hataori config reload --pipe <name> [--timeout-seconds <n>]`。
- 引数: Control Pipe必須です。
- 処理・戻り値: `reload`を送り、成功状態に基づくControl Pipe JSONを返します。
- 例: `hataori config reload --pipe hataori-control`。
- 安全: Runtime動作を変更するため先に`config check`を実行します。

### Integration Commands

Command: [`setup itoguruma`](#hataori-setup-itoguruma)、[`itoguruma status`](#hataori-itoguruma-status)、[`itoguruma test`](#hataori-itoguruma-test)、[`mcp status`](#hataori-mcp-status)。

#### `hataori setup itoguruma`

- 目的: User scope Itoguruma tokenを連携し任意でtestします。
- 構文: `hataori setup itoguruma [--config <path>] [--skip-test]`。
- 引数: User `ITOGURUMA_AUTH_TOKEN`を読み、`--skip-test`で即時接続を省略します。
- 処理・戻り値: tokenを表示せずUser/process環境へ設定し、setup、test、restart、next action、接続metadataを返します。
- 例: `hataori setup itoguruma`。
- 安全: User環境を変更します。`LocalSystem`には別途`service setup`を使います。

#### `hataori itoguruma status`

- 目的: Itogurumaへ接続してstatusを読みます。
- 構文: `hataori itoguruma status [--config <path>]`。
- 引数: 有効なItoguruma設定とtoken必須です。
- 処理・戻り値: MCP Clientで接続し、Itoguruma由来の`connected/name/version`と`tested: false`を返します。
- 例: `hataori itoguruma status`。
- 安全: 読み取り専用外部callでtokenを返しません。

#### `hataori itoguruma test`

- 目的: 同じ接続を明示的testとして実行します。
- 構文: `hataori itoguruma test [--config <path>]`。
- 引数: 有効なItoguruma設定とtoken必須です。
- 処理・戻り値: 接続とstatus取得を行い`connected/name/version/tested: true`を返します。
- 例: `hataori itoguruma test`。
- 安全: 読み取り専用外部callです。

#### `hataori mcp status`

- 目的: Hataori MCP initializeとTool検出を確認します。
- 構文: `hataori mcp status [--config <path>]`。
- 引数: Server MCP設定を有効設定から読みます。
- 処理・戻り値: Streamable HTTP接続と`tools/list`を実行し、`connected/endpoint/tool_count`を返します。
- 例: `hataori mcp status`。
- 安全: 読み取り専用です。

### Diagnostics and UI Commands

Command: [`doctor`](#hataori-doctor)、[`logs`](#hataori-logs)、[`monitor`](#hataori-monitor)、[`hook`](#hataori-hook)。

#### `hataori doctor`

- 目的: 設定、Server、Itoguruma、MCP、SQLite、Agent CLI、Service、Hookを診断します。
- 構文: `hataori doctor [--config <path>] [--timeout-seconds <n>]`。
- 引数: 有効設定と標準配置を使います。
- 処理・戻り値: 全checkを継続し、`healthy`と`name/ok/error/skipped`を持つ`checks`を返します。
- 例: `hataori doctor`。
- 安全: 読み取り接続と実行fileの`--version` callを行います。

#### `hataori logs`

- 目的: 構造化log行を読み、またはfollowします。
- 構文: `hataori logs [--lines <1..100000>] [--agent <id>] [--run <id>] [--log-directory <path>] [--follow] [--config <path>]`。
- 引数: 既定行数`200`、directoryは`fileLogging`設定です。
- 処理・戻り値: 通常は`directory_path/lines` JSON、`--follow`時はcancelまでraw行をstreamし最終JSONなしです。
- 例: `hataori logs --lines 100 --agent codex`。
- 安全: Logは運用情報を含むため共有前にsanitizeします。

#### `hataori monitor`

- 目的: 読み取り専用Hataori Monitorを起動します。
- 構文: `hataori monitor [--monitor <exe>] [--pipe <name>]`。
- 引数: 実行fileは標準配置または`HATAORI_MONITOR_PATH`です。
- 処理・戻り値: ShellでMonitorを起動し`status: "started"`とresolved `path`を返します。
- 例: `hataori monitor --pipe hataori-control`。
- 安全: 不審なoverride実行fileを指定しないでください。

#### `hataori hook`

- 目的: 標準入力からCodex/Claude Code Lifecycle Eventを1件処理します。
- 構文: `<event-json> | hataori hook --pipe <name> [--timeout-seconds <n>]`。
- 引数: 空でないJSONとPipe必須で、contextには`HATAORI_CONVERSATION_ID`、`HATAORI_AGENT_ID`、`HATAORI_MESSAGE_ID`、`HATAORI_MCP_URL`を使えます。
- 処理・戻り値: Monitor snapshotを読み、Event、状態、contextに応じたHook JSONを返します。
- 例: `Get-Content event.json -Raw | hataori hook --pipe hataori-control`。
- 安全: 導入済みHook template用です。秘密情報を含む不審JSONをpipeしません。

### Metadata Commands

Command: [`version`](#hataori-version)、[`help`](#hataori-help)。

#### `hataori version`

- 目的: CLI Assembly Versionを表示します。
- 構文: `hataori version`または`hataori --version`。
- 引数: なし。
- 処理・戻り値: 実行Assemblyを読み`version` JSONを返します。
- 例: `hataori --version`。
- 安全: 読み取り専用です。

#### `hataori help`

- 目的: Top-levelまたはGroup usageを表示します。
- 構文: `hataori help`、`hataori --help`、`hataori <group> --help`。
- 引数: 任意のGroup名です。
- 処理・戻り値: 設定を読まず組込みUsageを`help` JSONで返します。
- 例: `hataori task --help`。
- 安全: 読み取り専用です。

## Safety Notes

- Service設定とService Control Manager変更は管理者terminalで実行します。
- 状態変更前にTask、Message、Conversation、Service、実行file、DB、Install先を確認します。
- Recovery、Upgrade、Uninstall後の手動削除、直接DB作業前に`config`と`data`をbackupします。
- Itoguruma tokenをCommand引数、文書、log、Source Control、MCP Client設定へ記載しません。
- 診断では読み取り専用Command（`status`、`list`、`get`、`history`、`relations`、`db status`、`db integrity`、`config check`、`mcp status`、`doctor`）を優先します。
