# PROGRESS.md Version
2026.08.16

# 変更履歴

- 2026.08.16

# Hataori 進捗率履歴

この文書は、Hataoriの機能別進捗率、完了内容、残作業の正本です。

## グラフ

![Hataori進捗グラフ](progress/progress-chart.svg)

## 運用ルール

- 原則3日周期で日付列を追加します。
- 各機能は、実装・自動テスト・実環境確認・利用者文書の充足度から算定します。
- プロジェクト全体は、下表10機能の単純平均を整数四捨五入します。
- Phase 1は、仕様書のCore、Itoguruma、Session / Activation、Task、Monitorの単純平均です。

## 履歴

### ≪Hataori≫

| 機能 | 2026.08.16 |
| :--- | ---: |
| **グループ全体** | **89%** |
| Server / Core / SQLite | 88% |
| Itoguruma連携 | 98% |
| Session / Activation | 100% |
| Task管理 | 95% |
| CLI | 95% |
| Windows Service | 98% |
| Monitor | 95% |
| 運用・復旧 | 97% |
| 文書・配布 | 22% |
| テスト | 95% |

## 現在フェーズ

Phase 1（基盤・必須運用機能）: **95%**

算定: Core 88%、Itoguruma 98%、Session / Activation 100%、Task 95%、Monitor 95%の単純平均（95.2%）です。

## 進捗予測メモ

利用者向け文書・配布整備が主要な残量です。Windows ServiceはProgramDataのSYSTEM・Administrators限定認証設定、install、start、Running維持、Itoguruma接続、restart、Windows再起動後のAutomatic起動、stop、uninstallを実機確認済みです。Monitorはデータ入り表示、手動更新、異常時の案内・ログ、Itoguruma MCPの実接続状態表示を確認済みです。Codex CLI 0.147.0とClaude Code 2.1.220はstart・resume・Reply・ACKを実機確認し、自動テスト118件、Server、MCP、Hook、Graceful Shutdown、起動異常時の安全停止は確認済みです。

# 実装機能一覧（チェックリスト）

## 完了済み

- [x] Server基盤、Control Pipe、MCP Server
- [x] SQLite Task・Session・Message Queue・Agent Run永続化
- [x] Task lifecycleとMCP Tools
- [x] Itoguruma受信、Queue、Reply、永続Reply Retry
- [x] Codex / Claude Codeのstart・resume Driver
- [x] Conversation Mutex、Activation Manager、並列lane
- [x] Server・Service・Task・Agent・Conversation・Queue・Config・DB・診断CLI
- [x] 構造化ファイルログと`logs` CLI
- [x] 読み取り専用Monitorアプリ、Control Pipeスナップショット、`monitor` CLI
- [x] DB Maintenance、Retention purge、VACUUM、stale Task expiry
- [x] Codex／Claude Code Lifecycle Hookランナーとdoctor診断
- [x] 異常終了時のRun・Session・Message起動復旧
- [x] Itoguruma認証トークンの非表示セットアップCLI
- [x] 自動テスト118件

## 部分実装

- [x] Windows Service実機検証
- [x] Monitor実機表示検証
- [ ] 利用者向けREADME、設定、コマンド文書

## 未実装

- [ ] Agent・Windows Serviceの実環境統合・受入試験
- [ ] Release配布手順とインストーラ
