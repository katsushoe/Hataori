# Hataori

[English](README.md) | [日本語](README.ja.md)

Hataori is a local Windows orchestration service for Codex Desktop, Codex CLI, and Claude Code. It persists tasks, conversation sessions, messages, agent definitions, and agent runs in SQLite, integrates with Itoguruma, and exposes a Streamable HTTP MCP server, a management CLI, and a monitor with status display and cancellation controls. Codex-addressed messages are leased to a fixed receiver inside Codex Desktop so it can create visible tasks under saved projects.

## Getting Started

Install the x64 MSI, open an elevated PowerShell terminal, and run:

```powershell
hataori config init
hataori service setup
Start-Service Hataori
hataori service status
hataori mcp status
```

Pass conditions are a running `Hataori` service and an MCP status response containing `connected: true` and a positive tool count.

## Installation

### Installer

The standard artifact is the self-contained Windows x64 MSI. Double-click it and approve UAC, or specify a custom installation root from an elevated terminal:

```powershell
msiexec.exe /i Hataori-3.1.16.0-x64.msi INSTALL_ROOT="C:\Hataori"
```

The MSI installs Server, CLI, Monitor, the Windows Service registration, the system `PATH` entry, and a Start menu shortcut. See [Installation](docs/installation.md) for Install, Upgrade, and Uninstall behavior.

### Binary Archive

No supported binary archive is currently published. Use the MSI or build from source.

### Source Build

Prerequisites are Windows x64, the .NET 9 SDK, and WiX Toolset 5.0.2 for MSI generation.

```powershell
dotnet restore Hataori.sln
dotnet build Hataori.sln --configuration Release --no-restore
dotnet test Hataori.sln --configuration Release --no-build
./scripts/Build-Installer.ps1
```

Generated artifacts are written below `artifacts/` and are not tracked by Git.

## Configuration

The standard mutable directories are `%INSTALL_ROOT%\config`, `logs`, and `data`; they survive Upgrade and Uninstall. Generate the non-secret main settings with `hataori config init`. Link the Itoguruma token without displaying it by running `hataori service setup` from an elevated terminal.

See [Configuration](CONFIG.md) for every setting, precedence rule, constraint, and safe sample.

## Usage

```powershell
hataori doctor
hataori task list --database C:\Hataori\data\hataori.db
hataori logs --lines 100
hataori monitor
```

CLI output is JSON except for streamed log lines and error text. See [Commands](COMMANDS.md) for command syntax, results, exit codes, and safety notes. To register an MCP client, follow [MCP Setup](MCP_SETUP.md).

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

Hataori binds its MCP endpoint to loopback by default. Itoguruma tokens must not be committed, logged, pasted into chat, or placed in MCP client settings. `hataori service setup` writes the service token to a separate file restricted to `SYSTEM` and `Administrators`.

See [Security](SECURITY.md) before changing bind addresses, permissions, or secret storage.

## License

Hataori is distributed under the [MIT License](LICENSE).
