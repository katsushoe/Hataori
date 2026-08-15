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
| **グループ全体** | **66%** |
| Server / Core / SQLite | 85% |
| Itoguruma連携 | 90% |
| Session / Activation | 90% |
| Task管理 | 95% |
| CLI | 92% |
| Windows Service | 70% |
| Monitor | 0% |
| 運用・復旧 | 45% |
| 文書・配布 | 20% |
| テスト | 75% |

## 現在フェーズ

Phase 1（基盤・必須運用機能）: **72%**

算定: Core 85%、Itoguruma 90%、Session / Activation 90%、Task 95%、Monitor 0%の単純平均です。

## 進捗予測メモ

Monitor未着手、Hook・異常終了復旧・DB Maintenance・実環境統合試験が主要な残量です。自動テストは90件合格していますが、Itoguruma、MCP、Agent resume、Windows Serviceの実機確認は未実施です。

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
- [x] 自動テスト90件

## 部分実装

- [ ] Windows Service実機検証
- [ ] doctorのHook診断
- [ ] 異常終了時のRun・Session・Message復旧
- [ ] 利用者向けREADME、設定、コマンド文書

## 未実装

- [ ] Monitorアプリと`monitor` CLI
- [ ] DB MaintenanceとRetention purge
- [ ] Agent Hook連携
- [ ] 実環境統合・受入試験
- [ ] Release配布手順とインストーラ
