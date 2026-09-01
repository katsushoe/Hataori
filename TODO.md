# TODO.md Version
2026.08.31

# 変更履歴

- 2026.09.02: 3.1.18.0へVersion更新し、実機`C:\Hataori`へのMajor Upgradeと稼働確認を完了。
- 2026.09.01: 3.1.17.0へVersion更新。Agent定義SQLite永続化をMSI化し、実機`C:\Hataori`へのMajor Upgradeと稼働確認を完了。

- 2026.09.01: 3.1.16.0をRelease。MonitorのTask／Agent Runキャンセル操作をMSI化し、実機`C:\Hataori`移設と稼働確認を完了。
- 2026.09.01: 3.1.16.0実機環境を`C:\Hataori`へ統一し、インストール手順と検証記録を訂正。
- 2026.09.01: Monitor管理操作を3.1.16.0へ反映し、MSI buildと実機Major Upgradeを完了。
- 2026.09.01: 3.1.15.0をRelease。複数Activation root対応をMSI化し、実機Major Upgradeと2 root MCP検証を完了。
- 2026.09.01: 3.1.14.0をRelease。Workspace管理v2をMSI化し、実機Major Upgradeと稼働確認を完了。
- 2026.09.01: 複数Activation root設定、重複検証、Workspace別Itoguruma取り込みを実装。
- 2026.08.31: Workspace管理v2としてSession・Message・Agent Runへ`workspace_id`を伝播し、既存SQLiteデータ移行とCLI Workspace filterを実装。
- 2026.08.31: 3.1.13.0をRelease。Workspace単位のTask管理、`list_workspaces`、SQLite移行、Monitor・会話Hook連携を実装し、189テストと実機Major Upgradeを検証。
- 2026.08.31: 3.1.12.0をRelease。MCP `list_projects`、未登録Project候補返却、Task登録前Project選択案内を実装し、179テストと実機Major Upgradeを検証。
- 2026.08.29: Agent Run作成前の異常終了で孤立したactive Messageを起動時にfailedへ復旧し、Reply Retry待機中Messageを保護するSession recovery強化を実装。
- 2026.08.26: 3.1.9.0をRelease。MCP Server Instructionsと`hataori_workflow` Promptを追加し、実機Major UpgradeとMCP配信を検証。
- 2026.08.25: 3.1.8.0をRelease。Claude Code接続不可の原因（MCP outputSchema契約不正、Activation既定値のBind時重複による新規Install起動クラッシュ）を修正。
- 2026.08.18: Agent Run cancel、Task conflict detection、Dynamic Permission Approval（通知専用v1）を実装。
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
- [X] Dynamic Permission Approval（通知専用v1。PreToolUseのdeny時にItogurumaへ事後通知。原設計の一時停止・再開は現行アーキテクチャ上不可能と判断し不採用、`docs/adr/0014-dynamic-approval-notify-only.md`参照）
- [X] Agent Run cancel強化（`agent_run_cancel` MCP tool、`hataori agent cancel` CLIを実装。CLI経路はServiceと同一アカウントが必要）
- [X] Task conflict detection強化（`task_find_conflicts` MCP toolを実装。CJK bigramベースの簡易キーワード一致、参考情報扱い）
- [X] Session recovery強化（Agent Run作成前に孤立したactive Messageの失敗復旧とReply Retry待機中Messageの保護を実装）
- [X] Project候補検索（MCP `list_projects`、未登録Project指定時の候補返却、Task登録前の選択案内を実装）
- [X] Workspace管理v2（Task・Session・Message・Agent Runへ`workspace_id`を導入し、`list_workspaces`、Workspace単位のTask MCP、SQLite移行、Monitor・会話Hook・CLI filter、複数Activation root設定へ伝播）
- [X] Monitor管理操作（選択したTaskとAgent Runのキャンセル）
- [X] Agent definitions DB化（Workspace単位のSQLite正本、初回設定移行、CLI／MCP管理、監査履歴）
- [X] 詳細なMetrics（Workspace単位のTask／Message／Agent Run件数、成功率、再試行、平均時間、Agent別内訳をCLI／MCPで公開）
