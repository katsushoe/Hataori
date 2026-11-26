# Hataori

[English](README.md) | [日本語](README.ja.md)

Hataori is a local Windows orchestration service for Codex Desktop, Codex CLI, and Claude Code. It persists tasks, conversation sessions, messages, agent definitions, and agent runs in SQLite, integrates with Itoguruma, and exposes a Streamable HTTP MCP server, a management CLI, and a monitor with status display and cancellation controls. Codex-addressed messages are leased to a fixed receiver inside Codex Desktop so it can create visible tasks under saved projects.

## Getting Started

Install the user x64 MSI and open **Hataori Monitor** from the Windows Start menu. Monitor starts Server in your session. In a regular PowerShell terminal, check the connection:

```powershell
hataori mcp status
```

The MCP status response shows `connected: true` and a positive tool count when Server is ready.

## Installation

### Installer

The standard artifact is the self-contained Windows user x64 MSI. Install it in the signed-in user's ordinary session. For a fixed installation root prepared for that user:

```powershell
msiexec.exe /i Hataori-3.2.0.0-user-x64.msi INSTALL_ROOT="E:\Hataori"
```

The MSI adds the user's `PATH` entry, logon startup and Start menu shortcut. See [Installation](docs/installation.md) for migrating from the old Windows service.

### Binary Archive

No supported binary archive is currently published. Use the MSI or build from source.

### Source Build

Prerequisites are Windows x64, the .NET 9 SDK, and WiX Toolset 5.0.2 for MSI generation.

```powershell
dotnet restore Hataori.sln
dotnet build Hataori.sln --configuration Release --no-restore
dotnet test Hataori.sln --configuration Release --no-build
./scripts/Test-DocumentationLinks.ps1
./scripts/Build-Installer.ps1
```

Generated artifacts are written below `artifacts/` and are not tracked by Git.

## Configuration

The standard mutable directories are `%INSTALL_ROOT%\config`, `logs`, and `data`. To link a new Itoguruma token, run `hataori setup itoguruma` under the ordinary user account and restart Monitor.

See [Configuration](CONFIG.md) for every setting, precedence rule, constraint, and safe sample.

## Usage

```powershell
hataori doctor
hataori task list --database C:\Hataori\data\hataori.db
hataori logs --lines 100
hataori monitor
```

CLI output is JSON except for streamed log lines and error text. See [Commands](COMMANDS.md) for command syntax, results, exit codes, and safety notes. To register an MCP client, follow [MCP Setup](MCP_SETUP.md).

## Monitor

Open **Hataori Monitor** from the Windows Start menu or run `hataori monitor`. Closing the window keeps it in the system tray by default; minimizing also hides it there. Double-click the tray icon to reopen it. Right-click the icon for **Open Monitor**, **Settings**, **Restart Server**, **MCP server**, or **Exit**. **MCP server** shows a status lamp (green running, yellow not responding, red error, gray stopped or unknown) with **Check status**, **Start**, and **Stop**, and rechecks `/health` each time it opens. **Start**, **Stop**, and **Restart Server** apply only to the Server that Monitor started; **Stop** and **Restart Server** ask for confirmation. Codex and Claude Code sessions that use Hataori cannot connect while Server is stopped or restarting. See [Monitor settings](CONFIG.md#monitor-settings). Windows starts Monitor in the notification area at sign-in. Exit stops Monitor and the Server it started.

Use **Settings** in the window or tray menu to change tray behavior, minimized startup, startup automatic updates, refresh interval, and language. Startup updates verify and install a newer official user MSI and then restart Hataori under the same user account. The **Update application** button checks manually. Failures leave the current version usable. See [Monitor settings](CONFIG.md#monitor-settings).

## Documentation

- [Configuration](CONFIG.md)
- [Commands](COMMANDS.md)
- [MCP Setup](MCP_SETUP.md)
- [Installation](docs/installation.md)
- [Itoguruma Setup](docs/setup-itoguruma.md)
- [Release process](RELEASE.md) ([日本語](RELEASE.ja.md))
- [Packages](PACKAGES.md) ([日本語](PACKAGES.ja.md))
- [Security](SECURITY.md) ([日本語](SECURITY.ja.md))
- [Architecture decisions](docs/adr/)

## Security

Hataori binds its MCP endpoint to loopback by default. Itoguruma tokens must not be committed, logged, pasted into chat, or placed in MCP client settings. `hataori service setup` is a legacy service-edition command. The user edition reads the protected `config/hataori.user.json` when present.

See [Security](SECURITY.md) before changing bind addresses, permissions, or secret storage.

## License

Hataori is distributed under the [MIT License](LICENSE).
