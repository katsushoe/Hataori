# Hataori 3.0.2.0 MSI実機検証結果

## 実施日

2026-08-17

## 成果物

- MSI: `Hataori-3.0.2.0-x64.msi`
- SHA-256: `EB2C61FAB8856E16E76904FAAD2DA0D7378655C1F472AA445ABB1893F35DACA2`
- WiX Toolset 5.0.2
- win-x64 self-contained single-file

## 合格

- WiX ICE検証、警告0、エラー0
- 管理者MSIによる`F:\Hataori`へのInstall
- `bin`、`config`、`logs`、`data`の標準構成
- Server、CLI、Monitorの配置
- Version 3.0.2.0表示
- Hataori Windows ServiceのLocalSystem／Automatic登録
- CLIのシステムPATH登録
- Monitorのスタートメニューショートカット登録
- `config init`による秘密情報を含まない初期設定生成
- `service setup`による認証トークンの非表示保存
- サービス秘密設定ACLがSYSTEM／Administratorsのみ
- Uninstallでバイナリとサービスを削除し、設定・Data・Logを保持
- 3.0.0系から3.0.1.0、3.0.2.0へのMajor Upgradeと旧製品登録の除去
- Upgrade前後で通常設定Hashが一致
- Upgrade後のService起動、Running、MCP応答、Itoguruma通信
- 全124件の自動テスト

## 判明・修正

- 管理者トークンなしのper-machine InstallはMSI Error 1925でロールバックされることを確認し、UAC昇格手順を検証した。
- Codex実行ユーザーと対話ユーザーのSIDが異なる環境向けに、サービス設定はユーザー環境から取得できない場合だけプロセス環境へフォールバックするよう修正した。
- Windows InstallerはVersionの4番目をUpgrade比較に使わないため、Upgrade対象Versionは比較可能な3番目を増加させる運用とした。
