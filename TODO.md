# TODO.md Version
2026.08.18

# 変更履歴

- 2026.08.18: 全仕様書143節の11項目に基づきPhase 2の内訳を実装状況で更新。
- 2026.08.18
- 2026.08.16

# Hataori TODO

このファイルはHataoriの短期タスクと実装計画を管理します。

# 残件サマリ

## すぐやる

- [X] Monitorアプリ本体と`monitor` CLIを実装する。
- [X] DB MaintenanceとRetention設定を実装する。

## 次にやる

- [X] Itoguruma側の認証トークンstatus／rotateコマンドを連携する。
- [X] Itoguruma認証トークンの非表示セットアップコマンドを実装する。
- [X] Agent Hook連携とdoctor診断を実装する。
- [X] 異常終了時のRun・Session・Message起動復旧を実装する。
- [X] Graceful Shutdownを実環境で統合検証する。

## 後でやる

- [X] 利用者向け文書とRelease配布手順を整備する。
- [X] Itoguruma、MCP、Agent resume、Windows Serviceを実環境で受入試験する。

# 優先タスク

## P0

- [X] Monitor参照機能を実装する。
- [X] DB Maintenanceを完成させる。

## P1

- [X] Hook、復旧、実環境統合試験を完成させる。

## P2

- [X] 文書、配布、インストール手順を完成させる。

# フェーズ計画

## Phase 1: 基盤・必須運用機能

- [X] Server、SQLite、Task、MCP、主要CLI
- [X] Itoguruma、Queue、Reply、Retry
- [X] Session、Activation、Agent Driver
- [X] Monitor実装
- [X] Monitor実機表示検証
- [X] 運用・復旧・実環境受入

## Phase 2: 運用強化

Obsidian「Hataori 全仕様書」143節の11項目に対する実装状況（2026-08-18時点）。

- [X] Priority Queue（`message_queue.priority`によるDESC優先順位付けは実装済み）
- [X] Retry Policy（`replyRetry.*`設定による再試行方針は実装済み）
- [X] Pending Reply管理（`PendingReply`とReply Retryは実装済み）
- [ ] Dynamic Permission Approval
- [ ] Agent Run cancel強化（`agent cancel`／`agent run`のCLI・MCPは未実装）
- [ ] Session recovery強化（異常終了時の基本復旧は完了。追加の堅牢化は未着手）
- [ ] Workspace管理（`workspace_id`概念は未導入）
- [ ] Monitor管理操作（Monitorは引き続き読み取り専用）
- [ ] Agent definitions DB化（Agent定義は設定fileのまま）
- [ ] Task conflict detection強化（`task_find_conflicts`は未実装）
- [ ] 詳細なMetrics
