# Hataori Commands

[English](COMMANDS.md) | [日本語](COMMANDS.ja.md)

This document is the source of truth for the Hataori CLI, service controls, integration checks, JSON results, exit codes, and safety requirements.

## Command Groups

| Group | Commands | Purpose |
| :--- | :--- | :--- |
| [Server](#server-commands) | `start`, `stop`, `restart`, `status` | Manage a foreground Server process through its executable and Control Pipe. |
| [Service](#service-commands) | `service setup/install/uninstall/start/stop/restart/status` | Configure and control the Windows Service. |
| [Task](#task-commands) | `task start/get/list/heartbeat/complete/cancel/fail/expire/history/relation-add/relations` | Manage persisted tasks and relations. |
| [Agent](#agent-commands) | `agent list/status/runs` | Inspect configured agents and persisted runs. |
| [Conversation](#conversation-commands) | `conversation list/get/reset` | Inspect or invalidate conversation sessions. |
| [Queue](#queue-commands) | `queue list/get/retry/cancel` | Inspect and operate on queued messages. |
| [Database](#database-commands) | `db status/integrity` | Run read-only SQLite diagnostics. |
| [Configuration](#configuration-commands) | `config init/show/path/check/reload` | Generate, inspect, validate, and reload settings. |
| [Integration](#integration-commands) | `setup itoguruma`, `itoguruma status/test`, `mcp status` | Configure and verify external connections. |
| [Diagnostics and UI](#diagnostics-and-ui-commands) | `doctor`, `logs`, `monitor`, `hook` | Diagnose the installation, read logs, launch Monitor, and process hooks. |
| [Metadata](#metadata-commands) | `version`, `help` | Show version and usage information. |

## Common Options

- CLI output is indented JSON on standard output, except `logs --follow`, which streams log lines. Errors are written to standard error.
- `--config <path>` or `HATAORI_CONFIG_PATH` selects the main JSON for commands that load configuration.
- `--database <path>` or `HATAORI_DATABASE_PATH` selects SQLite for Task, Agent, Conversation, Queue, and DB commands. The path must be explicit.
- `--pipe <name>` or `HATAORI_CONTROL_PIPE_NAME` selects the Control Pipe.
- `--timeout-seconds <1..300>` or `HATAORI_CONTROL_TIMEOUT_SECONDS` controls Control Pipe timeouts; the default is `10`.
- `--server <path>` or `HATAORI_SERVER_PATH` selects the Server executable for foreground `start` and Service `install`.
- `--json` is accepted as a compatibility flag; JSON is already the normal output format.

| Exit code | Meaning |
| ---: | :--- |
| `0` | Success or requested cancellation. |
| `1` | Unexpected failure converted at the CLI boundary. |
| `2` | Invalid command, argument, option, or value. |
| `3` | Required file/endpoint unavailable or timed out. |
| `4` | Requested persisted entity not found. |
| `5` | Invalid runtime state or failed external/service operation. |
| `6` | I/O or Control Pipe failure. |
| `9` | SQLite failure. |

## Commands

### Server Commands

Commands: [`start`](#hataori-start), [`stop`](#hataori-stop), [`restart`](#hataori-restart), [`status`](#hataori-status).

#### `hataori start`

| Item | Specification |
| :--- | :--- |
| Purpose | Start a foreground Hataori Server process. |
| Syntax | `hataori start --server <exe>` |
| Arguments | `--server` or `HATAORI_SERVER_PATH` is required. |
| Processing | Validates the executable and starts it without waiting for exit. |
| Result | JSON process-start result containing the started status and process metadata. |
| Example | `hataori start --server F:\Hataori\bin\server\Hataori.Server.exe` |
| Safety | This is separate from the Windows Service; do not start both against the same database and pipe. |

#### `hataori stop`

| Item | Specification |
| :--- | :--- |
| Purpose | Request graceful shutdown through the Control Pipe. |
| Syntax | `hataori stop --pipe <name> [--timeout-seconds <n>]` |
| Arguments | `--pipe` is required unless its environment variable is set. |
| Processing | Sends `stop` and waits for the Server response. |
| Result | JSON Control Pipe response whose state reflects the Server handling the request. |
| Example | `hataori stop --pipe hataori-control` |
| Safety | Stops active workers; confirm running agent work can be interrupted. |

#### `hataori restart`

| Item | Specification |
| :--- | :--- |
| Purpose | Gracefully stop a foreground Server and start a new process. |
| Syntax | `hataori restart --pipe <name> --server <exe> [--timeout-seconds <n>]` |
| Arguments | Pipe and Server executable are required. |
| Processing | Sends `stop`, waits until the pipe closes, then starts the executable. |
| Result | JSON process-start result; timeout or I/O failures produce a nonzero exit. |
| Example | `hataori restart --pipe hataori-control --server F:\Hataori\bin\server\Hataori.Server.exe` |
| Safety | Use `service restart` for an installed Service. |

#### `hataori status`

| Item | Specification |
| :--- | :--- |
| Purpose | Read foreground Server state through the Control Pipe. |
| Syntax | `hataori status --pipe <name> [--timeout-seconds <n>]` |
| Arguments | Pipe is required. |
| Processing | Sends the read-only `status` request. |
| Result | JSON Control Pipe response derived from current Server and worker state. |
| Example | `hataori status --pipe hataori-control` |
| Safety | Read-only. |

### Service Commands

Commands: [`setup`](#hataori-service-setup), [`install`](#hataori-service-install), [`uninstall`](#hataori-service-uninstall), [`start`](#hataori-service-start), [`stop`](#hataori-service-stop), [`restart`](#hataori-service-restart), [`status`](#hataori-service-status).

#### `hataori service setup`

| Item | Specification |
| :--- | :--- |
| Purpose | Link the Itoguruma token to the Windows Service without displaying it. |
| Syntax | `hataori service setup` |
| Arguments | Reads `ITOGURUMA_AUTH_TOKEN` from user scope, then process scope. |
| Processing | Writes the Service secret file and restricts ACL to `SYSTEM` and `Administrators`. |
| Result | JSON `configured`, `configuration_path`, and `restart_required`; no token value. |
| Example | `hataori service setup` |
| Safety | Requires an elevated terminal and overwrites only the Service secret file. |

#### `hataori service install`

| Item | Specification |
| :--- | :--- |
| Purpose | Register a Windows Service by invoking `sc.exe create`. |
| Syntax | `hataori service install --server <exe> [--name <service>]` |
| Arguments | `--server` is required; `--name` defaults to `Hataori`. |
| Processing | Registers an Automatic own-process service with the selected executable. |
| Result | JSON `service_name`, `command`, `success`, and sanitized `output`. |
| Example | `hataori service install --server F:\Hataori\bin\server\Hataori.Server.exe` |
| Safety | Requires administrator rights. MSI installations already register the Service. |

#### `hataori service uninstall`

| Item | Specification |
| :--- | :--- |
| Purpose | Delete the selected Windows Service registration. |
| Syntax | `hataori service uninstall [--name <service>]` |
| Arguments | `--name` defaults to `Hataori`. |
| Processing | Invokes `sc.exe delete`. |
| Result | JSON service command result based on the actual `sc.exe` exit. |
| Example | `hataori service uninstall --name Hataori-Test` |
| Safety | Destructive; confirm the exact Service name. Prefer MSI Uninstall for MSI-managed installations. |

#### `hataori service start`

| Item | Specification |
| :--- | :--- |
| Purpose | Start the selected Windows Service. |
| Syntax | `hataori service start [--name <service>]` |
| Arguments | `--name` defaults to `Hataori`. |
| Processing | Invokes `sc.exe start`. |
| Result | JSON service command result. |
| Example | `hataori service start` |
| Safety | Ensure `service setup` completed first. |

#### `hataori service stop`

| Item | Specification |
| :--- | :--- |
| Purpose | Stop the selected Windows Service. |
| Syntax | `hataori service stop [--name <service>]` |
| Arguments | `--name` defaults to `Hataori`. |
| Processing | Invokes `sc.exe stop`. |
| Result | JSON service command result. |
| Example | `hataori service stop` |
| Safety | May interrupt active agent work. |

#### `hataori service restart`

| Item | Specification |
| :--- | :--- |
| Purpose | Stop and then start the selected Windows Service. |
| Syntax | `hataori service restart [--name <service>]` |
| Arguments | `--name` defaults to `Hataori`. |
| Processing | Runs `sc.exe stop`, then `sc.exe start`; a failed stop prevents start. |
| Result | JSON start-command result after both operations succeed. |
| Example | `hataori service restart` |
| Safety | Interrupts active agent work. |

#### `hataori service status`

| Item | Specification |
| :--- | :--- |
| Purpose | Query the selected Windows Service. |
| Syntax | `hataori service status [--name <service>]` |
| Arguments | `--name` defaults to `Hataori`. |
| Processing | Invokes `sc.exe query`. |
| Result | JSON service result containing the actual `sc.exe` output. |
| Example | `hataori service status` |
| Safety | Read-only. |

### Task Commands

Commands: [`start`](#hataori-task-start), [`get`](#hataori-task-get), [`list`](#hataori-task-list), [`heartbeat`](#hataori-task-heartbeat), [`complete`](#hataori-task-complete), [`cancel`](#hataori-task-cancel), [`fail`](#hataori-task-fail), [`expire`](#hataori-task-expire), [`history`](#hataori-task-history), [`relation-add`](#hataori-task-relation-add), [`relations`](#hataori-task-relations). Every command requires `--database <path>` or `HATAORI_DATABASE_PATH`.

#### `hataori task start`

| Item | Specification |
| :--- | :--- |
| Purpose | Create an active task. |
| Syntax | `hataori task start --id <id> --name <name> --agent <id> [--conversation <id>] [--message <id>] [--summary <text>] [--current-work <text>] --database <path>` |
| Arguments | `id`, `name`, and `agent` are required; optional context is persisted. |
| Processing | Validates uniqueness and writes the task plus initial history. |
| Result | JSON task whose fields depend on supplied context and persisted timestamps. |
| Example | `hataori task start --id DOC-1 --name docs --agent codex --database F:\Hataori\data\hataori.db` |
| Safety | State-changing; task IDs must be unique. |

#### `hataori task get`

| Item | Specification |
| :--- | :--- |
| Purpose | Read one task with history and relations. |
| Syntax | `hataori task get <id> --database <path>` |
| Arguments | Task ID may be positional or `--id`. |
| Processing | Reads persisted task, ordered history, and relations. |
| Result | JSON object with `task`, `history`, and `relations`; missing ID exits `4`. |
| Example | `hataori task get DOC-1 --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori task list`

| Item | Specification |
| :--- | :--- |
| Purpose | List tasks using persisted filters. |
| Syntax | `hataori task list [--status <status>] [--agent <id>] [--conversation <id>] [--all] --database <path>` |
| Arguments | Status values: `active`, `completed`, `cancelled`, `failed`, `expired`; default is `active`; `--all` removes status filtering. |
| Processing | Queries SQLite and optionally filters by conversation. |
| Result | JSON array whose membership varies with persisted state and filters. |
| Example | `hataori task list --all --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori task heartbeat`

| Item | Specification |
| :--- | :--- |
| Purpose | Update active-task work text and progress. |
| Syntax | `hataori task heartbeat <id> --current-work <text> --progress <0..100> [--message <id>] --database <path>` |
| Arguments | Task ID, current work, and integer progress are required. |
| Processing | Validates active state and appends heartbeat history. |
| Result | Updated JSON task; values depend on prior state and supplied progress. |
| Example | `hataori task heartbeat DOC-1 --current-work "Writing" --progress 50 --database F:\Hataori\data\hataori.db` |
| Safety | State-changing; always provide an accurate progress percentage. |

#### `hataori task complete`

| Item | Specification |
| :--- | :--- |
| Purpose | Mark an active task completed. |
| Syntax | `hataori task complete <id> (--result <text> | --message <text>) --database <path>` |
| Arguments | Task ID and result text are required; `--message` takes precedence. |
| Processing | Performs a terminal state transition and appends history. |
| Result | Completed JSON task. |
| Example | `hataori task complete DOC-1 --result "Done" --database F:\Hataori\data\hataori.db` |
| Safety | Terminal state change; verify the task ID and outcome. |

#### `hataori task cancel`

| Item | Specification |
| :--- | :--- |
| Purpose | Cancel an active task. |
| Syntax | `hataori task cancel <id> [--result <text> | --message <text>] --database <path>` |
| Arguments | Task ID is required; result is optional. |
| Processing | Performs a terminal cancelled transition. |
| Result | Cancelled JSON task. |
| Example | `hataori task cancel DOC-1 --result "Superseded" --database F:\Hataori\data\hataori.db` |
| Safety | Destructive state change; verify the task ID. |

#### `hataori task fail`

| Item | Specification |
| :--- | :--- |
| Purpose | Mark an active task failed. |
| Syntax | `hataori task fail <id> --result <text> --database <path>` |
| Arguments | Task ID and failure result are required. |
| Processing | Performs a terminal failed transition and records the reason. |
| Result | Failed JSON task. |
| Example | `hataori task fail DOC-1 --result "Validation failed" --database F:\Hataori\data\hataori.db` |
| Safety | Destructive state change; do not include secrets in the result. |

#### `hataori task expire`

| Item | Specification |
| :--- | :--- |
| Purpose | Mark an inactive task expired. |
| Syntax | `hataori task expire <id> --database <path>` |
| Arguments | Task ID is required. |
| Processing | Validates eligibility and performs a terminal expired transition. |
| Result | Expired JSON task. |
| Example | `hataori task expire DOC-1 --database F:\Hataori\data\hataori.db` |
| Safety | Destructive state change; normally maintenance handles stale tasks. |

#### `hataori task history`

| Item | Specification |
| :--- | :--- |
| Purpose | Read ordered task history. |
| Syntax | `hataori task history <id> --database <path>` |
| Arguments | Task ID is required. |
| Processing | Reads all persisted history entries for the task. |
| Result | JSON array ordered by recorded event time. |
| Example | `hataori task history DOC-1 --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori task relation-add`

| Item | Specification |
| :--- | :--- |
| Purpose | Add an idempotent relation between existing tasks. |
| Syntax | `hataori task relation-add --id <id> --related-id <id> --type <text> --database <path>` |
| Arguments | Both task IDs and a non-empty relation type are required. |
| Processing | Validates both tasks and inserts the relation if absent. |
| Result | JSON relation with task, related task, and type. |
| Example | `hataori task relation-add --id DOC-1 --related-id DOC-2 --type blocks --database F:\Hataori\data\hataori.db` |
| Safety | State-changing but idempotent for the same relation. |

#### `hataori task relations`

| Item | Specification |
| :--- | :--- |
| Purpose | Read all relations involving a task. |
| Syntax | `hataori task relations --id <id> --database <path>` |
| Arguments | `--id` is required. |
| Processing | Queries persisted incoming and outgoing relations. |
| Result | JSON relation array. |
| Example | `hataori task relations --id DOC-1 --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

### Agent Commands

Commands: [`list`](#hataori-agent-list), [`status`](#hataori-agent-status), [`runs`](#hataori-agent-runs), [`cancel`](#hataori-agent-cancel). `list`, `status`, and `runs` require a database path; `cancel` uses the Control Pipe instead.

#### `hataori agent list`

| Item | Specification |
| :--- | :--- |
| Purpose | List configured agent summaries. |
| Syntax | `hataori agent list --database <path> [--config <path>]` |
| Arguments | Database is required. |
| Processing | Combines configured drivers, activation limits, and running-run counts. |
| Result | JSON array with `agent_id`, `enabled`, `running`, and `max_runs`; values vary with config and DB state. |
| Example | `hataori agent list --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori agent status`

| Item | Specification |
| :--- | :--- |
| Purpose | Read one configured agent summary. |
| Syntax | `hataori agent status <agent-id> --database <path> [--config <path>]` |
| Arguments | Agent ID is positional or `--agent`; database is required. |
| Processing | Selects the matching summary from Codex and Claude Code configuration. |
| Result | One JSON agent summary; unknown agent exits `4`. |
| Example | `hataori agent status codex --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori agent runs`

| Item | Specification |
| :--- | :--- |
| Purpose | List persisted agent runs. |
| Syntax | `hataori agent runs [--status <status>] [--agent <id>] --database <path>` |
| Arguments | Status values: `queued`, `starting`, `running`, `completed`, `failed`, `cancelled`. |
| Processing | Queries SQLite using optional filters. |
| Result | JSON run array whose membership varies with filters and persisted state. |
| Example | `hataori agent runs --status running --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori agent cancel`

| Item | Specification |
| :--- | :--- |
| Purpose | Cancel a queued, starting, or running agent run and terminate its process if live. |
| Syntax | `hataori agent cancel <run-id> [--pipe <name>] [--timeout-seconds <1..300>]` |
| Arguments | Run ID is positional or `--run`; does not take `--database` (the run is reached through the running Server, not the database directly). |
| Processing | Sends `agent-cancel` with the run ID over the Control Pipe. The `agent_run_cancel` MCP tool reaches the same live process registry directly and does not have this limitation. |
| Result | `{ "run_id": ..., "status": "cancelled" \| "cancelled_db_only" }`; unknown run ID exits `4`. |
| Example | `hataori agent cancel run-1a2b3c` |
| Safety | Destructive. Like `start`/`stop`/`restart`/`status`, the Control Pipe is restricted to the account that runs the Hataori Service, so this CLI path only reaches a live process when invoked from that same account; prefer the MCP tool for reliable cancellation from an agent. |

### Conversation Commands

Commands: [`list`](#hataori-conversation-list), [`get`](#hataori-conversation-get), [`reset`](#hataori-conversation-reset).

#### `hataori conversation list`

| Item | Specification |
| :--- | :--- |
| Purpose | List persisted conversation sessions. |
| Syntax | `hataori conversation list [--status <status>] [--agent <id>] --database <path>` |
| Arguments | Status values: `idle`, `running`, `invalid`; database is required. |
| Processing | Queries SQLite with optional filters. |
| Result | JSON session array. |
| Example | `hataori conversation list --status running --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori conversation get`

| Item | Specification |
| :--- | :--- |
| Purpose | Read one conversation session. |
| Syntax | `hataori conversation get <conversation-id> --agent <id> --database <path>` |
| Arguments | Conversation ID and agent ID are required. |
| Processing | Reads the composite conversation/agent key. |
| Result | JSON session; missing session exits `4`. |
| Example | `hataori conversation get conv-1 --agent codex --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori conversation reset`

| Item | Specification |
| :--- | :--- |
| Purpose | Invalidate one conversation session so later activation can recreate it. |
| Syntax | `hataori conversation reset <conversation-id> --agent <id> --database <path>` |
| Arguments | Conversation ID and agent ID are required. |
| Processing | Changes the persisted session to `invalid`. |
| Result | Updated JSON session. |
| Example | `hataori conversation reset conv-1 --agent codex --database F:\Hataori\data\hataori.db` |
| Safety | Destructive state change; active continuity is lost. |

### Queue Commands

Commands: [`list`](#hataori-queue-list), [`get`](#hataori-queue-get), [`retry`](#hataori-queue-retry), [`cancel`](#hataori-queue-cancel).

#### `hataori queue list`

| Item | Specification |
| :--- | :--- |
| Purpose | List queued messages. |
| Syntax | `hataori queue list [--agent <id>] --database <path>` |
| Arguments | Database is required; agent filter is optional. |
| Processing | Reads persisted queued messages. |
| Result | JSON message array. |
| Example | `hataori queue list --agent codex --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori queue get`

| Item | Specification |
| :--- | :--- |
| Purpose | Read one queued message. |
| Syntax | `hataori queue get <message-id> --database <path>` |
| Arguments | Message ID and database are required. |
| Processing | Reads the persisted queue record. |
| Result | JSON message; missing message exits `4`. |
| Example | `hataori queue get msg-1 --database F:\Hataori\data\hataori.db` |
| Safety | Read-only; message content may be sensitive. |

#### `hataori queue retry`

| Item | Specification |
| :--- | :--- |
| Purpose | Make a queued message eligible for retry now. |
| Syntax | `hataori queue retry <message-id> --database <path>` |
| Arguments | Message ID and database are required. |
| Processing | Updates retry state using the current UTC time. |
| Result | Updated JSON message based on its prior persisted state. |
| Example | `hataori queue retry msg-1 --database F:\Hataori\data\hataori.db` |
| Safety | State-changing; may cause agent execution or reply delivery. |

#### `hataori queue cancel`

| Item | Specification |
| :--- | :--- |
| Purpose | Cancel a queued message. |
| Syntax | `hataori queue cancel <message-id> --database <path>` |
| Arguments | Message ID and database are required. |
| Processing | Persists cancellation at the current UTC time. |
| Result | JSON `message_id` and `status: "cancelled"`. |
| Example | `hataori queue cancel msg-1 --database F:\Hataori\data\hataori.db` |
| Safety | Destructive state change; the message will not be processed normally. |

### Database Commands

Commands: [`status`](#hataori-db-status), [`integrity`](#hataori-db-integrity). Both open SQLite read-only.

#### `hataori db status`

| Item | Specification |
| :--- | :--- |
| Purpose | Read basic database metadata. |
| Syntax | `hataori db status --database <path>` |
| Arguments | Existing database path is required. |
| Processing | Opens SQLite read-only and counts application tables. |
| Result | JSON `path`, `exists`, `table_count`, and `size_bytes`; values come from the selected file. |
| Example | `hataori db status --database F:\Hataori\data\hataori.db` |
| Safety | Read-only. |

#### `hataori db integrity`

| Item | Specification |
| :--- | :--- |
| Purpose | Run SQLite `PRAGMA integrity_check`. |
| Syntax | `hataori db integrity --database <path>` |
| Arguments | Existing database path is required. |
| Processing | Opens SQLite read-only and executes the integrity pragma. |
| Result | JSON `ok` and raw SQLite `result`; `ok` is true only when result equals `ok`. |
| Example | `hataori db integrity --database F:\Hataori\data\hataori.db` |
| Safety | Read-only but may be I/O intensive on a large database. |

### Configuration Commands

Commands: [`init`](#hataori-config-init), [`show`](#hataori-config-show), [`path`](#hataori-config-path), [`check`](#hataori-config-check), [`reload`](#hataori-config-reload).

#### `hataori config init`

| Item | Specification |
| :--- | :--- |
| Purpose | Create the embedded non-secret default main configuration. |
| Syntax | `hataori config init [--config <path>]` |
| Arguments | Destination defaults to the standard main settings path. |
| Processing | Creates directories and uses create-new semantics; existing files are preserved. |
| Result | JSON `path` and `created`; `created` is false when a file already exists. |
| Example | `hataori config init` |
| Safety | Does not overwrite existing configuration. |

#### `hataori config show`

| Item | Specification |
| :--- | :--- |
| Purpose | Display effective main configuration values. |
| Syntax | `hataori config show [--config <path>]` |
| Arguments | File must exist. |
| Processing | Loads JSON plus `HATAORI_` overrides and masks secret-like keys. |
| Result | JSON `path` and flattened `values`; values vary with environment overrides. |
| Example | `hataori config show` |
| Safety | Redaction is defensive, but review output before sharing. |

#### `hataori config path`

| Item | Specification |
| :--- | :--- |
| Purpose | Resolve the selected main configuration path. |
| Syntax | `hataori config path [--config <path>]` |
| Arguments | No existing file is required. |
| Processing | Resolves the absolute path and checks file existence. |
| Result | JSON `path` and `exists`. |
| Example | `hataori config path` |
| Safety | Read-only. |

#### `hataori config check`

| Item | Specification |
| :--- | :--- |
| Purpose | Validate settings with Server validators. |
| Syntax | `hataori config check [--config <path>]` |
| Arguments | File must exist. |
| Processing | Loads effective settings and evaluates all implemented validators. |
| Result | JSON `path`, `valid`, and `errors`; values depend on file and environment. |
| Example | `hataori config check` |
| Safety | Read-only; errors contain no token value. |

#### `hataori config reload`

| Item | Specification |
| :--- | :--- |
| Purpose | Ask a running Server to reload configuration. |
| Syntax | `hataori config reload --pipe <name> [--timeout-seconds <n>]` |
| Arguments | Control Pipe is required. |
| Processing | Sends the `reload` request through the pipe. |
| Result | JSON Control Pipe response based on reload success. |
| Example | `hataori config reload --pipe hataori-control` |
| Safety | New values can change runtime behavior; run `config check` first. |

### Integration Commands

Commands: [`setup itoguruma`](#hataori-setup-itoguruma), [`itoguruma status`](#hataori-itoguruma-status), [`itoguruma test`](#hataori-itoguruma-test), [`mcp status`](#hataori-mcp-status).

#### `hataori setup itoguruma`

| Item | Specification |
| :--- | :--- |
| Purpose | Link the user-scoped Itoguruma token and optionally test it. |
| Syntax | `hataori setup itoguruma [--config <path>] [--skip-test]` |
| Arguments | Reads user `ITOGURUMA_AUTH_TOKEN`; `--skip-test` avoids the immediate connection. |
| Processing | Sets user/process `HATAORI_ITOGURUMA__AUTHENTICATIONTOKEN` without printing the token. |
| Result | JSON setup fields, test status, restart requirement, next action, and connection metadata when tested. |
| Example | `hataori setup itoguruma` |
| Safety | Changes the user environment. Use `service setup` separately for `LocalSystem`. |

#### `hataori itoguruma status`

| Item | Specification |
| :--- | :--- |
| Purpose | Connect and read Itoguruma status. |
| Syntax | `hataori itoguruma status [--config <path>]` |
| Arguments | Effective Itoguruma settings and token are required. |
| Processing | Creates an MCP client, connects, and calls status. |
| Result | JSON `connected`, `name`, `version`, and `tested: false`; values come from Itoguruma. |
| Example | `hataori itoguruma status` |
| Safety | Read-only external call; token is not returned. |

#### `hataori itoguruma test`

| Item | Specification |
| :--- | :--- |
| Purpose | Perform the same connection and mark it as an explicit test. |
| Syntax | `hataori itoguruma test [--config <path>]` |
| Arguments | Effective Itoguruma settings and token are required. |
| Processing | Connects and retrieves current status. |
| Result | JSON `connected`, `name`, `version`, and `tested: true`. |
| Example | `hataori itoguruma test` |
| Safety | Read-only external call. |

#### `hataori mcp status`

| Item | Specification |
| :--- | :--- |
| Purpose | Verify Hataori MCP initialize and tool discovery. |
| Syntax | `hataori mcp status [--config <path>]` |
| Arguments | Server MCP settings are loaded from effective config. |
| Processing | Connects by Streamable HTTP and calls `tools/list`. |
| Result | JSON `connected`, `endpoint`, and `tool_count`; count reflects the running Server. |
| Example | `hataori mcp status` |
| Safety | Read-only. |

### Diagnostics and UI Commands

Commands: [`doctor`](#hataori-doctor), [`logs`](#hataori-logs), [`monitor`](#hataori-monitor), [`hook`](#hataori-hook).

#### `hataori doctor`

| Item | Specification |
| :--- | :--- |
| Purpose | Run configuration, Server, Itoguruma, MCP, SQLite, agent CLI, Service, and hook checks. |
| Syntax | `hataori doctor [--config <path>] [--timeout-seconds <n>]` |
| Arguments | Uses effective settings and standard installation paths. |
| Processing | Runs all checks and records individual failures instead of stopping at the first check. |
| Result | JSON `healthy` and `checks`; each check contains `name`, `ok`, optional `error`, and `skipped`. |
| Example | `hataori doctor` |
| Safety | Diagnostic; it makes read-only connection calls and executable `--version` calls. |

#### `hataori logs`

| Item | Specification |
| :--- | :--- |
| Purpose | Read or follow structured log lines. |
| Syntax | `hataori logs [--lines <1..100000>] [--agent <id>] [--run <id>] [--log-directory <path>] [--follow] [--config <path>]` |
| Arguments | Default line count is `200`; directory defaults from `fileLogging`. |
| Processing | Reads matching log files; `--follow` streams until cancelled. |
| Result | Without follow: JSON `directory_path` and `lines`; with follow: raw lines and no final JSON. |
| Example | `hataori logs --lines 100 --agent codex` |
| Safety | Logs may contain operational data; sanitize before sharing. |

#### `hataori monitor`

| Item | Specification |
| :--- | :--- |
| Purpose | Launch the read-only Hataori Monitor. |
| Syntax | `hataori monitor [--monitor <exe>] [--pipe <name>]` |
| Arguments | Executable defaults to the standard installed Monitor path or `HATAORI_MONITOR_PATH`. |
| Processing | Starts Monitor with shell execution and optionally passes the pipe name. |
| Result | JSON `status: "started"` and resolved `path`. |
| Example | `hataori monitor --pipe hataori-control` |
| Safety | Read-only UI; launching an untrusted override executable is unsafe. |

#### `hataori hook`

| Item | Specification |
| :--- | :--- |
| Purpose | Process one Codex or Claude Code lifecycle event from standard input. |
| Syntax | `<event-json> | hataori hook --pipe <name> [--timeout-seconds <n>]` |
| Arguments | Non-empty JSON standard input and Control Pipe are required; hook context may come from `HATAORI_CONVERSATION_ID`, `HATAORI_AGENT_ID`, `HATAORI_MESSAGE_ID`, and `HATAORI_MCP_URL`. |
| Processing | Reads a read-only monitor snapshot and derives hook output. |
| Result | JSON hook response determined by event type, snapshot state, and context variables. |
| Example | `Get-Content event.json -Raw | hataori hook --pipe hataori-control` |
| Safety | Intended for installed hook templates; do not pipe untrusted JSON containing secrets. |

### Metadata Commands

Commands: [`version`](#hataori-version), [`help`](#hataori-help).

#### `hataori version`

| Item | Specification |
| :--- | :--- |
| Purpose | Show the CLI assembly version. |
| Syntax | `hataori version` or `hataori --version` |
| Arguments | None. |
| Processing | Reads the executing assembly version. |
| Result | JSON `version`. |
| Example | `hataori --version` |
| Safety | Read-only. |

#### `hataori help`

| Item | Specification |
| :--- | :--- |
| Purpose | Show top-level or group usage text. |
| Syntax | `hataori help`, `hataori --help`, or `hataori <group> --help` |
| Arguments | Optional group name. |
| Processing | Returns built-in usage text without loading configuration. |
| Result | JSON `help`. |
| Example | `hataori task --help` |
| Safety | Read-only. |

## Safety Notes

- Run Service setup and Service Control Manager mutations from an elevated terminal.
- Confirm exact task, message, conversation, service, executable, database, and installation targets before state-changing commands.
- Back up `config` and `data` before recovery, Upgrade, Uninstall cleanup, or direct database work.
- Never place Itoguruma tokens in command arguments, documentation, logs, source control, or MCP client settings.
- Prefer read-only commands (`status`, `list`, `get`, `history`, `relations`, `db status`, `db integrity`, `config check`, `mcp status`, `doctor`) for diagnosis.
