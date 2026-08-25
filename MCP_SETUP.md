# MCP Setup

[English](MCP_SETUP.md) | [日本語](MCP_SETUP.ja.md)

This guide connects a locally running Hataori Windows Service to Codex or Claude Code through Streamable HTTP.

## Values and Placeholders

| Value | How to obtain it | Default/example | Change when |
| :--- | :--- | :--- | :--- |
| `$HataoriRoot` | The directory selected by the MSI | `F:\Hataori` | Hataori is installed elsewhere. |
| MCP server name | Client-visible logical name | `hataori` | A different display name is intentional. |
| MCP URL | `server.mcpHost`, `mcpPort`, and `mcpPath` in `hataori.json` | `http://127.0.0.1:45440/mcp` | Any of those Server settings differs. |

`<install-root>` and similar angle-bracket text are notation only. Do not paste angle brackets into a terminal or configuration file.

## Prerequisites

- Install Hataori on the same Windows machine as the MCP client.
- Run `hataori config init` and `hataori service setup` as described in [README](README.md).
- Install Codex or Claude Code and open a new terminal after MSI `PATH` changes.
- Ensure the `Hataori` Windows Service is registered.

## Authentication and Environment

Hataori MCP does not require client credentials, authorization headers, or environment variables. It binds to loopback and accepts the configured local host names. Do not add the Itoguruma token to MCP client settings; that token authenticates Hataori's separate outbound Itoguruma connection.

Client scope controls where a client can see the registration. It does not change Hataori task permissions, agent sandbox rules, or the Server bind address.

## Start the Server

In an elevated PowerShell terminal, set only the installation path when it differs:

```powershell
$HataoriRoot = 'F:\Hataori'
& "$HataoriRoot\bin\cli\Hataori.Cli.exe" service start
& "$HataoriRoot\bin\cli\Hataori.Cli.exe" service status
& "$HataoriRoot\bin\cli\Hataori.Cli.exe" mcp status
```

Pass conditions are a Running Service and MCP JSON containing `connected: true`, the expected URL, and a positive `tool_count`.

## Register Clients

### Codex — recommended manual user configuration

Add this complete table to `%USERPROFILE%\.codex\config.toml`. Preserve all other content and replace only an existing `[mcp_servers.hataori]` table.

```toml
[mcp_servers.hataori]
url = "http://127.0.0.1:45440/mcp"
enabled = true
required = true
default_tools_approval_mode = "writes"
```

- `hataori` is the client-visible server name.
- `url` must match the Hataori Server settings; HTTP selects Streamable HTTP and no local start command is needed.
- `enabled` loads the registration, `required` reports endpoint failure at client startup, and `default_tools_approval_mode` leaves write decisions to Codex policy.
- Authentication fields are omitted because Hataori MCP has no client authentication.

Restart the ChatGPT desktop app, Codex CLI, or IDE extension after saving, then open `/mcp` and confirm that `hataori` is connected.

### Claude Code — recommended automatic user registration

Run this command with the default URL:

```powershell
claude mcp add --transport http --scope user hataori http://127.0.0.1:45440/mcp
```

Claude Code updates its user configuration at `~/.claude.json` while preserving other settings. The generated logical entry is:

```json
{
  "mcpServers": {
    "hataori": {
      "type": "http",
      "url": "http://127.0.0.1:45440/mcp"
    }
  }
}
```

Do not paste this JSON after running the command; it documents the generated result. The server name is `hataori`, `type` selects HTTP, the URL must match Hataori, and authentication fields are omitted because none are required. Restart or reload Claude Code, then run `claude mcp get hataori` and `claude mcp list`.

### Alternative project-scoped registration

Use this only when Hataori should be visible in one project:

- Codex: place the same TOML table in `<project-root>\.codex\config.toml`.
- Claude Code: run `claude mcp add --transport http --scope project hataori http://127.0.0.1:45440/mcp`, which updates project scope while preserving other entries.

Project scope may require workspace trust and MCP approval. Do not also register user scope unless duplicate visibility is intentional.

## Multiple Workspaces

One Hataori Service can coordinate agents whose `activation.workingDirectory` points to one configured workspace. Hataori does not expose a per-client MCP workspace allowlist in version 3.0.5.0. To change the automatic agent workspace, update the absolute `activation.workingDirectory`, run `hataori config check`, and restart the Service.

Client user/project scope changes registration visibility only; it does not select an agent working directory or bypass agent sandbox and workspace-trust controls.

## Verify the Connection

Stop at the first failed check.

1. **Server endpoint:** run `hataori mcp status`. Pass: `connected: true` and the expected endpoint.
2. **Client compatibility:** run `hataori mcp compatibility`. Pass: `compatible: true`, with identical tool names, tool count, and `get_version` structured result for `codex` and `claude-code`.
3. **Client registration:** Codex `/mcp` or `claude mcp get hataori` reports connected.
4. **Read-only tool call:** call `task_list` with no status or agent filter. Pass: a structured task array, including an empty array when no tasks match.
5. **Complete diagnosis:** run `hataori doctor`. Pass: `healthy: true` and every non-skipped check has `ok: true`.

State-changing and destructive tool annotations are documented in [Commands](COMMANDS.md). When calling `task_heartbeat`, always provide `progressPercent`.

## Troubleshooting

- Connection refused: run `hataori service status`, start the Service, and repeat endpoint verification.
- HTTP 404: make the client URL match `server.mcpPath`; the default is `/mcp`.
- Host or bind error: keep `server.mcpHost` on loopback and align `allowedHosts` with the local URL.
- Codex has no `hataori` entry: verify the selected `config.toml` scope and restart Codex.
- Claude Code has no entry: rerun `claude mcp add`, then inspect it with `claude mcp get hataori`.
- Claude Code shows pending approval: trust the project and approve its project-scoped MCP entry.
- Tool calls fail after connection: run `hataori doctor` and inspect `%INSTALL_ROOT%\logs` after sanitizing any shared output.
- Itoguruma errors: run `hataori itoguruma test`; never add the Itoguruma token to MCP settings.
