# Hataori

Hataoriは、Codex CLIとClaude Codeの実行を調整し、タスク、会話セッション、メッセージ、Agent RunをSQLiteへ永続化するWindows向けのローカル実行基盤です。ItogurumaとMCPで連携し、Windows Service、CLI、読み取り専用Monitorを提供します。

## 主な機能

- Codex CLI／Claude Codeの開始・再開とLifecycle Hook
- Task／Conversation Session／Message Queue／Agent Runの永続化
- Itoguruma受信、Reply Retry、MCP Server／Client連携
- Windows Serviceによる自動運転と異常終了後の状態復旧
- CLIによる状態確認、診断、設定、ログ、DB、Service操作
- 読み取り専用Monitorによる稼働状況表示

## 動作環境

- Windows x64
- Hataori 3.0.2.0
- インストールには管理者権限が必要です

配布標準は、Server、CLI、Monitorを含む自己完結型x64 MSIです。

## インストール

管理者ターミナルでMSIを実行します。標準のインストール先は64bit Program Files配下です。任意の場所へ入れる場合は、次のように指定します。

```powershell
msiexec.exe /i Hataori-3.0.2.0-x64.msi INSTALL_ROOT="F:\Hataori"
```

インストール後、Itogurumaが発行した認証トークンを表示せずService用設定へ保存し、Serviceを起動します。

```powershell
hataori config init
hataori service setup
Start-Service Hataori
hataori service status
```

詳しいInstall／Upgrade／Uninstall手順は[インストールガイド](docs/installation.md)、認証連携は[Itoguruma認証セットアップ](docs/setup-itoguruma.md)を参照してください。

## 標準ディレクトリ構成

```text
%INSTALL_ROOT%/
├─ bin/       実行ファイルとHookテンプレート
├─ config/    通常設定とService秘密設定
├─ logs/      ログ
└─ data/      SQLite等の永続データ
```

`config`、`logs`、`data`はUpgradeおよびUninstall後も保持されます。認証トークンをGit、ログ、チャットへ記載しないでください。

## 開発と検証

```powershell
dotnet test Hataori.sln --configuration Release
./scripts/Build-Installer.ps1
```

インストーラ生成にはWiX Toolset 5.0.2を使用します。現在の実装状況は[PROGRESS.md](PROGRESS.md)、設計判断は[docs/adr](docs/adr)、実機検証結果は[docs/validation](docs/validation)で確認できます。
