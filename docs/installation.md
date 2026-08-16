# Hataori インストール

HataoriのWindows標準成果物はx64 MSIです。MSIはServer、CLI、Monitor、Windows Service、CLIのシステムPATH、Monitorのスタートメニューショートカットを管理します。

## 標準構成

| パス | 用途 | Upgrade／Uninstall |
| :--- | :--- | :--- |
| `%INSTALL_ROOT%/bin` | 実行ファイルとHookテンプレート | MSIが更新・削除 |
| `%INSTALL_ROOT%/config` | 通常設定とサービス秘密設定 | 保持 |
| `%INSTALL_ROOT%/logs` | ログ | 保持 |
| `%INSTALL_ROOT%/data` | SQLite等のアプリデータ | 保持 |

既定の`%INSTALL_ROOT%`は64bit Program Files配下の`Hataori`です。別の場所へ導入する場合は、管理者ターミナルから`INSTALL_ROOT`を指定します。

```powershell
msiexec.exe /i Hataori-3.0.3.0-x64.msi INSTALL_ROOT="D:\Hataori"
```

## 初回設定

MSIは設定、秘密情報、利用者データ、ログを同梱しません。初期設定はCLIで生成し、既存ファイルがある場合は変更しません。

```powershell
hataori config init
hataori service setup
Start-Service Hataori
```

`service setup`はItogurumaが発行した認証トークンを表示せず`config/hataori.service.json`へ保存し、ACLをSYSTEMとAdministratorsだけに制限します。初回インストールでは認証設定前の誤起動を防ぐため、サービスはAutomaticで登録しますが自動起動しません。

## Upgrade

新しいVersionのMSIを同じ`INSTALL_ROOT`で実行します。MSIは旧バイナリとサービス登録を置換し、`config`、`logs`、`data`を保持します。Upgrade後はサービスを起動してください。

## Uninstall

Windowsの「インストールされているアプリ」または次のコマンドで削除します。

```powershell
msiexec.exe /x Hataori-3.0.3.0-x64.msi
```

Uninstallはバイナリ、サービス、PATH、ショートカットを削除します。設定、秘密情報、データ、ログは保持します。不要な場合だけ、利用者が内容を確認して`%INSTALL_ROOT%/config`、`logs`、`data`を別途削除してください。

## MSI生成とHash確認

```powershell
./scripts/Build-Installer.ps1
Get-FileHash ./artifacts/installer/Hataori-3.0.3.0-x64.msi -Algorithm SHA256
```

生成スクリプトはServer、CLI、Monitorをwin-x64 self-contained single-fileとしてpublishし、WiX Toolset 5.0.2でMSIを作成します。
