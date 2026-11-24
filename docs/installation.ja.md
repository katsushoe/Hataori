# Hataoriインストール

[English](installation.md) | [日本語](installation.ja.md)

## 3.2.0.0以降のユーザー単位インストール

`Hataori-<version>-user-x64.msi` はWindowsサービスを登録しません。Windowsにログオンすると、ユーザーの `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` からMonitorが通常権限で起動し、通知領域に常駐します。Monitorは同じユーザーでServerを起動します。「終了」またはログオフでServerも停止します。

新規インストールの既定先は `%LOCALAPPDATA%\Hataori` です。旧サービス版を固定場所で使っている場合は、旧版MSIを手元に保持し、管理者のPowerShellで `installer/Initialize-UserInstall.ps1` に `-InstallRoot`、対象ユーザーの `-UserSid`、新規の保護されたバックアップ先を `-BackupDirectory` で指定します。スクリプトは設定とSQLiteのバックアップを検証し、旧サービス版を削除してから対象ユーザーに書き込みを許可します。Itoguruma認証の値は表示せずにユーザー用ファイルへ複製します。

同じユーザーの通常権限でユーザー単位MSIを実行し、既存の場所を使う場合は `INSTALL_ROOT` に同じパスを指定します。スタートメニューの **Hataori Monitor** からすぐに起動できます。セットアップ後の自動更新ではユーザー単位MSIだけを使います。移行後に旧版へ戻す必要がある場合は、先にユーザー版を削除し、管理者がインストール先の書き込み権限をサービス向けに戻してから旧MSIを使います。

以下は3.1系までの旧サービス版の手順です。

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
