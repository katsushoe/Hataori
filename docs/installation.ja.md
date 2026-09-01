# Hataoriインストール

[English](installation.md) | [日本語](installation.ja.md)

HataoriのWindows標準成果物はx64 MSIです。MSIはServer、CLI、Monitor、Windows Service、CLIのシステムPATH、Monitorのスタートメニューショートカットを管理します。

## 標準構成

| パス | 用途 | Upgrade／Uninstall |
| :--- | :--- | :--- |
| `%INSTALL_ROOT%/bin` | 実行ファイルとHookテンプレート | MSIが更新・削除 |
| `%INSTALL_ROOT%/config` | 通常設定とサービス秘密設定 | 保持 |
| `%INSTALL_ROOT%/logs` | ログ | 保持 |
| `%INSTALL_ROOT%/data` | SQLite等のアプリデータ | 保持 |

既定の`%INSTALL_ROOT%`は64bit Program Files配下の`Hataori`です。本Projectの実機環境は`C:\Hataori`を使用します。非対話インストールでこの場所へ導入する場合は、管理者ターミナルから正確なMSI property名`INSTALL_ROOT`を指定します。

```powershell
msiexec.exe /i Hataori-3.1.21.0-x64.msi INSTALL_ROOT="C:\Hataori" /qn /norestart
```

`INSTALLFOLDER`は未対応です。`INSTALL_ROOT`を省略または誤記すると、Windows Installerは既定のProgram Files配下へ導入します。Upgrade時も同じ`INSTALL_ROOT`を必ず指定してください。

## 初回設定

新規インストール時、MSIは日本語または英語を選択し、選択した`application.language`を含む`config\hataori.json`を作成します。アップグレードでは既存設定を保持します。秘密情報、利用者データ、ログは同梱しません。次のCLIも既存ファイルを変更しません。

```powershell
hataori config init --language ja-JP
hataori service setup
Start-Service Hataori
```

`service setup`はItogurumaが発行した認証トークンを表示せず`config/hataori.service.json`へ保存し、ACLをSYSTEMとAdministratorsだけに制限します。MSIはServiceをAutomaticで登録して起動します。このfileがない場合、ServerはItoguruma未連携のdegraded状態で動作します（Task管理・MCP・CLIは利用可能）。`service setup`は後から実行しても構いません。

## Upgrade

新しいVersionのMSIを同じ`INSTALL_ROOT`で実行します。MSIは旧バイナリとサービス登録を置換し、`config`、`logs`、`data`を保持してServiceを起動します。

Hataoriの4パートVersionは、どのパートが変わった場合もUpgradeとして扱います。Windows Installerは先頭3パートだけを比較するため、第4パートだけの更新でも既存版を置き換えられるよう、MSIで同一VersionのMajorUpgradeを明示的に有効化しています。この仕様では第4パートだけが異なるDowngradeもWindows Installerで阻止できないため、必ず新しいMSIを実行してください。

## Uninstall

Windowsの「インストールされているアプリ」または次のコマンドで削除します。

```powershell
msiexec.exe /x Hataori-3.1.7.0-x64.msi
```

Uninstallはバイナリ、サービス、PATH、ショートカットを削除します。設定、秘密情報、データ、ログは保持します。不要な場合だけ、利用者が内容を確認して`%INSTALL_ROOT%/config`、`logs`、`data`を別途削除してください。

## MSI生成とHash確認

```powershell
./scripts/Build-Installer.ps1
Get-FileHash ./artifacts/installer/Hataori-3.1.7.0-x64.msi -Algorithm SHA256
```

生成スクリプトはServer、CLI、Monitorをwin-x64 self-contained single-fileとしてpublishし、WiX Toolset 5.0.2でMSIを作成します。
