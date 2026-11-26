# Hataori

[English](README.md) | [日本語](README.ja.md)

Hataoriは、Codex Desktop、Codex CLI、Claude Codeを調整するWindows向けローカル実行サービスです。Task、Conversation Session、Message、Agent定義、Agent RunをSQLiteへ永続化し、Itoguruma連携、Streamable HTTP MCP Server、管理CLI、状態表示とキャンセル操作を備えたMonitorを提供します。Codex宛MessageはCodex Desktop内の固定受信Taskが期限付きで取得し、保存済みProject配下へ表示可能なTaskを作成します。

## Getting Started

ユーザー単位x64 MSIをインストールし、Windowsのスタートメニューから **Hataori Monitor** を開きます。同じユーザーでServerも起動します。通常のPowerShellで接続を確認します。

```powershell
hataori mcp status
```

MCP statusに`connected: true`と1以上のtool countが表示されればServerは接続可能です。

## Installation

### Installer

標準成果物は自己完結型のWindowsユーザー単位x64 MSIです。ログオンユーザーの通常権限で実行します。そのユーザー用に準備済みの固定パスを使う場合は次のように指定します。

```powershell
msiexec.exe /i Hataori-3.2.0.0-user-x64.msi INSTALL_ROOT="E:\Hataori"
```

MSIはユーザーの`PATH`、ログオン時の自動起動、スタートメニューのショートカットを登録します。旧サービス版からの移行は[インストール](docs/installation.ja.md)を参照してください。

### Binary Archive

現在、サポート対象のBinary Archiveは公開していません。MSIを使用するかSourceからbuildしてください。

### Source Build

前提条件はWindows x64、.NET 9 SDK、MSI生成用のWiX Toolset 5.0.2です。

```powershell
dotnet restore Hataori.sln
dotnet build Hataori.sln --configuration Release --no-restore
dotnet test Hataori.sln --configuration Release --no-build
./scripts/Test-DocumentationLinks.ps1
./scripts/Build-Installer.ps1
```

生成物はGit管理外の`artifacts/`配下へ出力されます。

## Configuration

可変データの標準配置は`%INSTALL_ROOT%\config`、`logs`、`data`です。新規のItoguruma認証は、通常権限で`hataori setup itoguruma`を実行してMonitorを再起動します。

全設定、優先順位、制約、安全なsampleは[設定](CONFIG.ja.md)を参照してください。

## Usage

```powershell
hataori doctor
hataori task list --database C:\Hataori\data\hataori.db
hataori logs --lines 100
hataori monitor
```

streamされるlog行とerror textを除き、CLI出力はJSONです。Command構文、結果、終了Code、安全上の注意は[コマンド](COMMANDS.ja.md)を参照してください。MCP Clientの登録は[MCPセットアップ](MCP_SETUP.ja.md)に従ってください。

## Monitor

Windowsのスタートメニューで **Hataori Monitor** を開くか、`hataori monitor`を実行します。既定では閉じるボタンを押してもタスクトレイに常駐し、最小化した場合もトレイへ格納します。トレイアイコンをダブルクリックすると画面が開きます。右クリックメニューから **Monitorを開く**、**設定**、**Serverを再起動**、**MCPサーバー**、**終了** を選べます。**MCPサーバー** には状態ランプ（稼働中は緑、応答なしは黄、エラーは赤、停止中・未確認は灰）と **状態を確認**、**起動**、**停止** があり、開くたびに `/health` で状態を確認し直します。**起動**・**停止**・**Serverを再起動** はMonitorが起動したServerだけを対象とし、**停止** と **Serverを再起動** は確認後に実行します。停止中や再起動が終わるまで、Hataoriを使うCodexとClaude Codeのセッションは接続できません。詳細は[Monitor設定](CONFIG.ja.md#monitor-settings)を参照してください。Windowsログオン時にも通知領域に起動します。「終了」はMonitorと同時に起動したServerを停止します。

画面またはトレイメニューの **設定** で、閉じる動作、最小化起動、起動時の自動更新、表示間隔、言語を変更できます。起動時の自動更新は、新しい正式版のユーザー単位MSIを検証して通常権限でインストールし、Hataoriを再起動します。**アプリの更新** ボタンで手動確認もできます。更新できない場合は現在の版を引き続き使用できます。[Monitor設定](CONFIG.ja.md#monitor-settings)を参照してください。

## Documentation

- [設定](CONFIG.ja.md)
- [コマンド](COMMANDS.ja.md)
- [MCPセットアップ](MCP_SETUP.ja.md)
- [インストール](docs/installation.ja.md)
- [Itogurumaセットアップ](docs/setup-itoguruma.ja.md)
- [Release手順](RELEASE.ja.md)（[English](RELEASE.md)）
- [パッケージ](PACKAGES.ja.md)（[English](PACKAGES.md)）
- [セキュリティ](SECURITY.ja.md)（[English](SECURITY.md)）
- [アーキテクチャ判断](docs/adr/)

## Security

Hataoriは既定でMCP endpointをloopbackへbindします。Itoguruma tokenをGit、log、chat、MCP Client設定へ記載しないでください。`hataori service setup`は旧サービス版向けです。ユーザー版はアクセスを制限した`config/hataori.user.json`があれば読み込みます。

bind address、権限、秘密情報の保存方法を変更する前に[セキュリティ](SECURITY.ja.md)を確認してください。

## License

Hataoriは[MIT License](LICENSE)で配布します。
