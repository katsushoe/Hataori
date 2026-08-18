# PACKAGES.md Version

2026.08.18

# 変更履歴

- 2026.08.18: 日本語版を新規作成。

# Hataoriパッケージ台帳

[English](PACKAGES.md) | [日本語](PACKAGES.ja.md)

この文書は、Hataoriが参照するパッケージ、その用途、取得元、更新方針の正本です。

# 対象プロジェクト

| プロジェクト群 | Target Framework | 参照方式 |
| :--- | :--- | :--- |
| `Hataori.Core`、`Hataori.Application`、`Hataori.Infrastructure`、`Hataori.Server`、`Hataori.Cli` | `net9.0` | SDK-style project と `PackageReference` |
| `Hataori.Monitor` | `net9.0-windows` | SDK-style Windows Forms project と `PackageReference` |
| `tests/*.Tests` | `net9.0` | SDK-style test project と `PackageReference` |
| `Hataori.Installer` | WiX Toolset SDK | `installer/Hataori.Installer.wixproj`内の`WixToolset.Sdk/5.0.2` |

# パッケージ取得元

リポジトリに`nuget.config`は存在しません。そのためRestoreは、導入済み.NET SDKと現在のユーザーに設定されたNuGetソースを使用します。サポート対象のパッケージソースは公開NuGetフィードのみで、credentialや非公開フィードのURLをコミットしてはなりません。

# 直接参照パッケージ

| パッケージ | Version | 対象プロジェクト | 用途 | 更新方針 |
| :--- | :--- | :--- | :--- | :--- |
| `Microsoft.Data.Sqlite` | `9.0.8` | Infrastructure | SQLite永続化 | .NET 9のサポート対象servicing lineを維持し、repositoryとmigrationのテストを実行する。 |
| `Microsoft.Extensions.Hosting.WindowsServices` | `9.0.8` | Server | Windows Serviceライフタイム統合 | .NET 9 runtimeパッケージと足並みを揃える。 |
| `Microsoft.Extensions.Logging.Abstractions` | `9.0.8` | Monitor | Monitorのログ抽象化 | .NET 9 runtimeパッケージと足並みを揃える。 |
| `ModelContextProtocol` | `2.0.0` | Infrastructure | MCP clientトランスポート | 更新前にプロトコルとAPI互換性を確認する。 |
| `ModelContextProtocol.AspNetCore` | `2.0.0` | Server | Streamable HTTP MCP server | `ModelContextProtocol`と同時に更新し、initialize、`tools/list`、tool呼び出しを検証する。 |
| `coverlet.collector` | `6.0.2` | 全テストプロジェクト | Coverage収集 | 開発専用。テスト検出を確認する。 |
| `FluentAssertions` | `8.6.0` | 全テストプロジェクト | テストAssertion | 開発専用。Assertion APIの変更点を確認する。 |
| `Microsoft.NET.Test.Sdk` | `17.12.0` | 全テストプロジェクト | VSTestホスト | 開発専用。全テストプロジェクトの実行を確認する。 |
| `NSubstitute` | `5.3.0` | Serverテスト | Test double | 開発専用。substituteの挙動を確認する。 |
| `xunit` | `2.9.2` | 全テストプロジェクト | テストフレームワーク | runnerとanalyzerを同時に更新する。 |
| `xunit.runner.visualstudio` | `2.8.2` | 全テストプロジェクト | VSTest xUnit adapter | `xunit`と同時に更新し、CLIとIDEでの検出を確認する。 |

`Hataori.Cli`は共有フレームワーク参照`Microsoft.AspNetCore.App`も使用しますが、これは選択したruntimeから供給されるため、NuGetパッケージ参照ではありません。

# 推移的パッケージ

Restore済みの`project.assets.json`には、次の主要な推移的依存グループが含まれます。

| 起点 | 主な推移的パッケージ | 方針 |
| :--- | :--- | :--- |
| `Microsoft.Data.Sqlite` | `Microsoft.Data.Sqlite.Core`、`SQLitePCLRaw.bundle_e_sqlite3`、`SQLitePCLRaw.core`、`SQLitePCLRaw.lib.e_sqlite3`、`SQLitePCLRaw.provider.e_sqlite3` | 直接参照している直接パッケージで提供されないAPIが必要な場合を除き、直接追加しない。 |
| MCPパッケージ | `ModelContextProtocol.Core`、`Microsoft.Extensions.AI.Abstractions`、`System.Net.ServerSentEvents`、`System.IO.Pipelines`、.NET 10の`Microsoft.Extensions.*`抽象化 | MCPパッケージによる解決に任せ、更新前にmajorをまたぐ依存変更を確認する。 |
| Windows Services | `System.ServiceProcess.ServiceController`、`System.Diagnostics.EventLog` | Windows Servicesパッケージによる解決に任せる。 |
| テストパッケージ | `Microsoft.TestPlatform.*`、`Microsoft.CodeCoverage`、`xunit.*`、`Castle.Core`、`Newtonsoft.Json` | テスト専用。別途必要がない限り製品プロジェクトへ昇格させない。 |

正本となる完全な推移的依存集合は、各プロジェクトでRestoreされた`obj/project.assets.json`です。生成されたasset fileはコミット対象外です。

# 更新ルール

1. 依存パッケージ群は一度に1系統ずつ更新する。
2. Microsoft .NET 9製品パッケージは互換servicing versionを維持する。
3. `ModelContextProtocol`と`ModelContextProtocol.AspNetCore`は、上流の互換性が明示的に許容しない限り同時に更新する。
4. 更新前にrelease note、license変更、脆弱性報告、target framework要件を確認する。
5. Restore・build・全テスト実行・対象実行fileのpublishを行い、runtime挙動が変わる場合はMCPまたはWindows Serviceの検証をやり直す。
6. 非公開ソースのcredentialやsecretをリポジトリファイルへ追加しない。

# 検証コマンド

```powershell
dotnet restore Hataori.sln
dotnet list Hataori.sln package --include-transitive
dotnet list Hataori.sln package --vulnerable --include-transitive
dotnet build Hataori.sln --configuration Release --no-restore
dotnet test Hataori.sln --configuration Release --no-build
```
