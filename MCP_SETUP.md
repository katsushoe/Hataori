# Hataori MCP Setup

[English](MCP_SETUP.md) | [日本語](MCP_SETUP.ja.md)

This guide connects the locally running Hataori Windows Service to Codex or Claude Code through Streamable HTTP.

## Prerequisites

- Install Hataori on the same Windows machine as the MCP client.
- Complete the initial setup in [README.md](README.md).
- Confirm that the `Hataori` Windows Service is running.

The default MCP settings are:

| Setting | Value |
| --- | --- |
| Server name | `hataori` |
| Transport | Streamable HTTP (`http`) |
| URL | `http://127.0.0.1:45440/mcp` |
| MCP authentication | None |

Hataori listens on loopback by default. The Itoguruma authentication token is separate and must not be added to MCP client settings.

## Verify the Server

Run these commands in PowerShell:

```powershell
hataori service status
hataori mcp status
```

The service must be running. `mcp status` must report `connected: true`, the configured endpoint, and a positive tool count.

## Codex

Add this table to `%USERPROFILE%\.codex\config.toml`. Preserve all other settings and replace only the existing `mcp_servers.hataori` table if present.

```toml
[mcp_servers.hataori]
url = "http://127.0.0.1:45440/mcp"
enabled = true
required = true
default_tools_approval_mode = "writes"
```

Restart the ChatGPT desktop app, Codex CLI, or IDE extension after saving. Open `/mcp` and confirm that `hataori` is enabled and connected.

## Claude Code

Register Hataori in the user scope:

```powershell
claude mcp add --transport http --scope user hataori http://127.0.0.1:45440/mcp
```

Restart or reload Claude Code, then verify the registration:

```powershell
claude mcp get hataori
claude mcp list
```

For project-only registration, use `--scope project` instead of `--scope user`. Do not register both scopes unless that duplication is intentional.

## Available Tools

Hataori exposes the following task tools:

- Read-only: `task_get`, `task_list`, `task_history`, `task_relations`
- State updates: `task_start`, `task_heartbeat`, `task_complete`, `task_relation_add`
- Destructive state changes: `task_cancel`, `task_fail`, `task_expire`

Call `task_list` first to verify the connection without changing task state. When calling `task_heartbeat`, always provide `progressPercent`.

## Custom Endpoint

If `server.mcpHost`, `server.mcpPort`, or `server.mcpPath` differs in `%INSTALL_ROOT%\config\hataori.json`, use the matching URL in both client settings. Restart the Hataori Service after changing server settings.

Keep the server bound to loopback unless remote exposure has been separately designed and secured. The current MCP endpoint does not require a bearer token.

## Troubleshooting

- Connection refused: run `hataori service status`, start the service, and retry `hataori mcp status`.
- HTTP 404: make the client URL match `server.mcpPath`; the default is `/mcp`.
- Client has no `hataori` entry: verify the user or project configuration, then restart the client.
- Claude Code shows pending approval: trust the project and approve its project-scoped MCP entry.
- Tool calls fail after connection: inspect `%INSTALL_ROOT%\logs` and run `hataori doctor`.
- Itoguruma errors: use `hataori itoguruma test`; do not add the Itoguruma token to MCP settings.
