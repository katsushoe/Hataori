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
| `docs/adr/` | Yes | アーキテクチャ判断の正本。 |
| `progress/` | Yes | 進捗グラフ。 |
| `**/bin/`、`**/obj/` | No | build・publish生成物。 |

## プロジェクト内ドキュメント一覧

| 文書名 | 正本パス | Git管理 | 用途 |
| :--- | :--- | :--- | :--- |
| `DOCUMENTS.md` | `DOCUMENTS.md` | Yes | 文書正本配置一覧。 |
| `PROGRESS.md` | `PROGRESS.md` | Yes | 機能別進捗率、完了内容、残作業。 |
| `TODO.md` | `TODO.md` | Yes | 短期タスクと実装計画。 |
| `progress-chart.svg` | `progress/progress-chart.svg` | Yes | 進捗率の可視化。 |
| ADR | `docs/adr/*.md` | Yes | 設計判断。 |

## Git管理外情報

秘密情報、実環境設定、raw log、顧客情報は上記文書へ記載しません。
