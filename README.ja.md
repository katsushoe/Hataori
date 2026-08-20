# Hataori

[English](README.md) | [日本語](README.ja.md)

Hataoriは、Codex CLIとClaude Codeを調整するWindows向けローカル実行サービスです。Task、Conversation Session、Message、Agent RunをSQLiteへ永続化し、Itoguruma連携、Streamable HTTP MCP Server、管理CLI、読み取り専用Monitorを提供します。

## Getting Started

x64 MSIをインストールし、管理者PowerShellで次を実行します。

```powershell
hataori config init
hataori service setup
Start-Service Hataori
hataori service status
hataori mcp status
```

`Hataori` ServiceがRunningで、MCP statusに`connected: true`と1以上のtool countが表示されれば合格です。

## Installation

### Installer

標準成果物は自己完結型Windows x64 MSIです。ダブルクリックしてUACを承認するか、管理者ターミナルから任意のインストール先を指定します。

```powershell
msiexec.exe /i Hataori-3.0.5.0-x64.msi INSTALL_ROOT="F:\Hataori"
```

MSIはServer、CLI、Monitor、Windows Service登録、System `PATH`、Start Menu Shortcutを導入します。Install、Upgrade、Uninstallの動作は[インストール](docs/installation.ja.md)を参照してください。

### Binary Archive

現在、サポート対象のBinary Archiveは公開していません。MSIを使用するかSourceからbuildしてください。

### Source Build

前提条件はWindows x64、.NET 9 SDK、MSI生成用のWiX Toolset 5.0.2です。

```powershell
dotnet restore Hataori.sln
dotnet build Hataori.sln --configuration Release --no-restore
dotnet test Hataori.sln --configuration Release --no-build
./scripts/Build-Installer.ps1
```

生成物はGit管理外の`artifacts/`配下へ出力されます。

## Configuration

可変データの標準配置は`%INSTALL_ROOT%\config`、`logs`、`data`で、UpgradeとUninstall後も保持されます。秘密情報を含まない通常設定は`hataori config init`で生成します。Itoguruma tokenは管理者ターミナルから`hataori service setup`を実行し、値を表示せず連携します。

全設定、優先順位、制約、安全なsampleは[設定](CONFIG.ja.md)を参照してください。

## Usage

```powershell
hataori doctor
hataori task list --database F:\Hataori\data\hataori.db
hataori logs --lines 100
hataori monitor
```

streamされるlog行とerror textを除き、CLI出力はJSONです。Command構文、結果、終了Code、安全上の注意は[コマンド](COMMANDS.ja.md)を参照してください。MCP Clientの登録は[MCPセットアップ](MCP_SETUP.ja.md)に従ってください。

## Documentation

- [設定](CONFIG.ja.md)
- [コマンド](COMMANDS.ja.md)
- [MCPセットアップ](MCP_SETUP.ja.md)
- [インストール](docs/installation.ja.md)
- [Itogurumaセットアップ](docs/setup-itoguruma.ja.md)
- [パッケージ](PACKAGES.md)（英語）
- [セキュリティ](SECURITY.md)（英語）
- [アーキテクチャ判断](docs/adr/)

## Security

Hataoriは既定でMCP endpointをloopbackへbindします。Itoguruma tokenをGit、log、chat、MCP Client設定へ記載しないでください。`hataori service setup`はService tokenを別fileへ保存し、ACLを`SYSTEM`と`Administrators`だけに制限します。

bind address、権限、秘密情報の保存方法を変更する前に[セキュリティ](SECURITY.md)（英語）を確認してください。

## License

Hataoriは[MIT License](LICENSE)で配布します。
