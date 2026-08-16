# PACKAGES.md Version

2026.08.17

# Change History

- 2026.08.17

# Hataori Package Inventory

This document is the source of truth for Hataori package references, their purpose, sources, and update policy.

# Target Projects

| Project group | Target framework | Reference model |
| :--- | :--- | :--- |
| `Hataori.Core`, `Hataori.Application`, `Hataori.Infrastructure`, `Hataori.Server`, `Hataori.Cli` | `net9.0` | SDK-style project and `PackageReference` |
| `Hataori.Monitor` | `net9.0-windows` | SDK-style Windows Forms project and `PackageReference` |
| `tests/*.Tests` | `net9.0` | SDK-style test projects and `PackageReference` |
| `Hataori.Installer` | WiX Toolset SDK | `WixToolset.Sdk/5.0.2` in `installer/Hataori.Installer.wixproj` |

# Package Sources

No repository `nuget.config` is present. Restore therefore uses the NuGet sources configured by the installed .NET SDK and the current user. The supported package source is the public NuGet feed; credentials and private feed URLs must not be committed.

# Direct Package References

| Package | Version | Projects | Purpose | Update policy |
| :--- | :--- | :--- | :--- | :--- |
| `Microsoft.Data.Sqlite` | `9.0.8` | Infrastructure | SQLite persistence | Keep on the supported .NET 9 servicing line; run repository and migration tests. |
| `Microsoft.Extensions.Hosting.WindowsServices` | `9.0.8` | Server | Windows Service lifetime integration | Keep aligned with the .NET 9 runtime packages. |
| `Microsoft.Extensions.Logging.Abstractions` | `9.0.8` | Monitor | Monitor logging abstraction | Keep aligned with the .NET 9 runtime packages. |
| `ModelContextProtocol` | `2.0.0` | Infrastructure | MCP client transport | Review protocol and API compatibility before updating. |
| `ModelContextProtocol.AspNetCore` | `2.0.0` | Server | Streamable HTTP MCP server | Update with `ModelContextProtocol`; verify initialize, `tools/list`, and tool calls. |
| `coverlet.collector` | `6.0.2` | All test projects | Coverage collection | Development-only; verify test discovery. |
| `FluentAssertions` | `8.6.0` | All test projects | Test assertions | Development-only; review assertion API changes. |
| `Microsoft.NET.Test.Sdk` | `17.12.0` | All test projects | VSTest host | Development-only; verify all test projects run. |
| `NSubstitute` | `5.3.0` | Server tests | Test doubles | Development-only; verify substitute behavior. |
| `xunit` | `2.9.2` | All test projects | Test framework | Update with the runner and analyzers. |
| `xunit.runner.visualstudio` | `2.8.2` | All test projects | VSTest xUnit adapter | Update with `xunit`; verify discovery in CLI and IDE. |

`Hataori.Cli` also uses the shared framework reference `Microsoft.AspNetCore.App`; it is supplied by the selected .NET runtime and is not a NuGet package reference.

# Transitive Packages

The resolved `project.assets.json` files contain these principal transitive groups:

| Origin | Main transitive packages | Policy |
| :--- | :--- | :--- |
| `Microsoft.Data.Sqlite` | `Microsoft.Data.Sqlite.Core`, `SQLitePCLRaw.bundle_e_sqlite3`, `SQLitePCLRaw.core`, `SQLitePCLRaw.lib.e_sqlite3`, `SQLitePCLRaw.provider.e_sqlite3` | Do not add directly unless an implementation requires an API unavailable through the direct package. |
| MCP packages | `ModelContextProtocol.Core`, `Microsoft.Extensions.AI.Abstractions`, `System.Net.ServerSentEvents`, `System.IO.Pipelines`, .NET 10 `Microsoft.Extensions.*` abstractions | Keep resolved by the MCP packages; inspect cross-major dependency changes before updating. |
| Windows Services | `System.ServiceProcess.ServiceController`, `System.Diagnostics.EventLog` | Keep resolved by the Windows Services package. |
| Test packages | `Microsoft.TestPlatform.*`, `Microsoft.CodeCoverage`, `xunit.*`, `Castle.Core`, `Newtonsoft.Json` | Test-only; do not promote to product projects without a separate need. |

The authoritative complete transitive set is the restored `obj/project.assets.json` for each project. Generated asset files are not committed.

# Update Rules

1. Update one dependency family at a time.
2. Keep Microsoft .NET 9 product packages on compatible servicing versions.
3. Update `ModelContextProtocol` and `ModelContextProtocol.AspNetCore` together unless upstream compatibility explicitly permits otherwise.
4. Review release notes, license changes, vulnerability reports, and target-framework requirements.
5. Restore, build, run all tests, publish affected executables, and repeat MCP or Windows Service validation when runtime behavior changes.
6. Do not add private source credentials or secrets to repository files.

# Verification Commands

```powershell
dotnet restore Hataori.sln
dotnet list Hataori.sln package --include-transitive
dotnet list Hataori.sln package --vulnerable --include-transitive
dotnet build Hataori.sln --configuration Release --no-restore
dotnet test Hataori.sln --configuration Release --no-build
```
