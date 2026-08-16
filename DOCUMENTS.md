# DOCUMENTS.md Version
2026.08.16

この文書は、Hataoriリポジトリ内のディレクトリ構成と文書正本配置を管理します。

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
| `README.md` | `README.md` | Yes | GitHubと利用者向けのプロジェクト入口。 |
| `MCP_SETUP.md` | `MCP_SETUP.md` | Yes | Codex／Claude Code向けMCPセットアップ英語正本。 |
| `MCP_SETUP.ja.md` | `MCP_SETUP.ja.md` | Yes | Codex／Claude Code向けMCPセットアップ日本語版。 |
| `DOCUMENTS.md` | `DOCUMENTS.md` | Yes | 文書正本配置一覧。 |
| `PROGRESS.md` | `PROGRESS.md` | Yes | 機能別進捗率、完了内容、残作業。 |
| `TODO.md` | `TODO.md` | Yes | 短期タスクと実装計画。 |
| `progress-chart.svg` | `progress/progress-chart.svg` | Yes | 進捗率の可視化。 |
| ADR | `docs/adr/*.md` | Yes | 設計判断。 |
| 実機検証結果 | `docs/validation/*.md` | Yes | 実機での合否、修正、未完了事項。 |
| Itoguruma認証セットアップ | `docs/setup-itoguruma.md` | Yes | 認証トークンの非表示連携と接続試験手順。 |
| インストール | `docs/installation.md` | Yes | MSIのInstall、Upgrade、Uninstallと標準配置。 |

## Git管理外情報

秘密情報、実環境設定、raw log、顧客情報は上記文書へ記載しません。
