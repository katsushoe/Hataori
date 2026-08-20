# DOCUMENTS.md Version
2026.08.20

[English](DOCUMENTS.md) | [日本語](DOCUMENTS.ja.md)

この文書は、Hataoriリポジトリ内のDirectory構成と文書正本配置を管理します。

## 配置方針

公開可能な設計・進捗・計画文書はGit管理します。実環境設定、認証情報、raw log、一時メモはGit管理しません。

## プロジェクト内ディレクトリ構成

| パス | Git管理 | 用途 |
| :--- | :--- | :--- |
| `.` | Yes | Solution、入口文書、進捗・計画の正本。 |
| `src/` | Yes | Core、Application、Infrastructure、Server、CLI、Monitorの実装。 |
| `tests/` | Yes | 自動テスト。 |
| `installer/` | Yes | WiX x64 MSI定義。 |
| `scripts/` | Yes | publish・MSI生成スクリプト。 |
| `docs/adr/` | Yes | アーキテクチャ判断の正本。 |
| `docs/validation/` | Yes | 実環境検証結果。 |
| `progress/` | Yes | 進捗グラフ。 |
| `**/bin/`、`**/obj/` | No | build・publish生成物。 |

## プロジェクト内ドキュメント一覧

| 文書名 | 正本パス | Git管理 | 用途 |
| :--- | :--- | :--- | :--- |
| `README.md` | `README.md` | Yes | GitHubと利用者向けのプロジェクト入口（英語正本）。 |
| `README.ja.md` | `README.ja.md` | Yes | プロジェクト入口の日本語版。 |
| インストール | `docs/installation.md` | Yes | MSIのInstall、Upgrade、Uninstallと標準配置の英語正本。 |
| インストール日本語版 | `docs/installation.ja.md` | Yes | インストールGuideの日本語版。 |
| Itoguruma認証セットアップ | `docs/setup-itoguruma.md` | Yes | Itoguruma認証連携の英語正本。 |
| Itoguruma認証セットアップ日本語版 | `docs/setup-itoguruma.ja.md` | Yes | Itoguruma認証連携の日本語版。 |
| `MCP_SETUP.md` | `MCP_SETUP.md` | Yes | Codex／Claude Code向けMCPセットアップ英語正本。 |
| `MCP_SETUP.ja.md` | `MCP_SETUP.ja.md` | Yes | Codex／Claude Code向けMCPセットアップ日本語版。 |
| `COMMANDS.md` | `COMMANDS.md` | Yes | CLIコマンドリファレンス英語正本。 |
| `COMMANDS.ja.md` | `COMMANDS.ja.md` | Yes | CLIコマンドリファレンス日本語版。 |
| `CONFIG.md` | `CONFIG.md` | Yes | 設定項目リファレンス英語正本。 |
| `CONFIG.ja.md` | `CONFIG.ja.md` | Yes | 設定項目リファレンス日本語版。 |
| `RELEASE.md` | `RELEASE.md` | Yes | Release作成・公開手順（Version決定、build、実機検証、MSI、GitHub Release）英語正本。 |
| `RELEASE.ja.md` | `RELEASE.ja.md` | Yes | Release作成・公開手順日本語版。 |
| `PACKAGES.md` | `PACKAGES.md` | Yes | NuGetパッケージ台帳英語正本。 |
| `PACKAGES.ja.md` | `PACKAGES.ja.md` | Yes | NuGetパッケージ台帳日本語版。 |
| `SECURITY.md` | `SECURITY.md` | Yes | セキュリティ方針・脆弱性報告手順英語正本。GitHubのSecurity機能はこのfile名を参照するため、正本パスは変更しない。 |
| `SECURITY.ja.md` | `SECURITY.ja.md` | Yes | セキュリティ方針・脆弱性報告手順日本語版。 |
| `DOCUMENTS.md` | `DOCUMENTS.md` | Yes | 文書正本配置一覧。 |
| `DOCUMENTS.ja.md` | `DOCUMENTS.ja.md` | Yes | 文書正本配置一覧の日本語版。 |
| `PROGRESS.md` | `PROGRESS.md` | Yes | 機能別進捗率、完了内容、残作業。 |
| `TODO.md` | `TODO.md` | Yes | 短期タスクと実装計画。 |
| `progress-chart.svg` | `progress/progress-chart.svg` | Yes | 進捗率の可視化。 |
| ADR | `docs/adr/*.md` | Yes | 設計判断。 |
| 実機検証結果 | `docs/validation/*.md` | Yes | 実機での合否、修正、未完了事項。 |

## Git管理外情報

秘密情報、実環境設定、raw log、顧客情報は上記文書へ記載しません。
