# Hataori Installation

[English](installation.md) | [日本語](installation.ja.md)

The supported Windows artifact is the x64 MSI. It manages the Server, CLI, Monitor, Windows Service, system `PATH`, and Monitor Start menu shortcut.

## Standard Layout

| Path | Purpose | Upgrade / Uninstall |
| :--- | :--- | :--- |
| `%INSTALL_ROOT%\bin` | Executables and hook templates | Updated and removed by the MSI |
| `%INSTALL_ROOT%\config` | Main and service-secret settings | Preserved |
| `%INSTALL_ROOT%\logs` | Logs | Preserved |
| `%INSTALL_ROOT%\data` | SQLite and application data | Preserved |

The default `%INSTALL_ROOT%` is `Hataori` under 64-bit Program Files. To use another location, run this from an elevated terminal:

```powershell
msiexec.exe /i Hataori-3.1.6.0-x64.msi INSTALL_ROOT="F:\Hataori"
```

## Initial Configuration

On a new installation, the MSI asks for Japanese or English and creates `config\hataori.json` with the selected `application.language`. Upgrades preserve an existing configuration. Secrets, user data, and logs are not packaged. These commands also preserve existing files:

```powershell
hataori config init --language en-US
hataori service setup
Start-Service Hataori
```

`service setup` copies the Itoguruma authentication token without displaying it and restricts `config\hataori.service.json` to `SYSTEM` and `Administrators`. To prevent an unauthenticated first start, the MSI registers the Service as Automatic but does not start it. If started manually without this file, the Server runs in a degraded, unlinked state while task management, MCP, and CLI remain available.

## Upgrade

Run the newer MSI with the same `INSTALL_ROOT`. It replaces binaries and service registration while preserving `config`, `logs`, and `data`. Start the Service after the upgrade.

## Uninstall

Use Windows Installed apps or:

```powershell
msiexec.exe /x Hataori-3.1.6.0-x64.msi
```

Uninstall removes binaries, the Service, `PATH`, and shortcuts. Review and manually remove preserved mutable directories only when their data is no longer needed.

## Build and Verify the MSI

```powershell
./scripts/Build-Installer.ps1
Get-FileHash ./artifacts/installer/Hataori-3.1.6.0-x64.msi -Algorithm SHA256
```

The script publishes self-contained, single-file `win-x64` Server, CLI, and Monitor executables and builds the MSI with WiX Toolset 5.0.2.
