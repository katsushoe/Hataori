# TODO.md Version
2026.11.03

# 変更履歴

- 2026.11.03: Monitor回帰テスト後のSolution Releaseテスト217件がすべて合格し、テスト残件を完了。
- 2026.11.03: Releaseテストをプロジェクト別に実行し215件合格。CLIの名前付きパイプテスト2件がsandbox内でハングし、Solution全体の完了確認は継続。
- 2026.11.01: 進捗率の4軸算定基準を定義し、10カテゴリの得点と総合・Phase 1率を再計算。未確認の実サービスMonitor操作と全Solutionテスト実行をP2へ追加。
- 2026.11.01: ADR 0010に沿ったMonitor専用WinForms回帰テストを追加し、Windows上で表示データ・更新・Task／Agent Runキャンセルの5テストを検証。詳細は`docs/validation/2026-11-01-monitor-regression.md`。残る進捗率算定基準の整理は別P2で継続。
- 2026.11.01: 進捗率の根拠を調査。ADR 0010がMonitor表示の自動テストを検証対象に含めている一方、Monitor専用テストがないことを確認し、回帰テスト追加をP2へ登録。Itoguruma・CLIの具体的未完了機能は確認できず、旧数値の算定基準も不明なため、評価根拠の整理を別P2として残す。
- 2026.10.01: Claude Code 2.1.233でPostToolUse hook発火と`layer-reviewer`起動の実機検証を完了。関連実装TODOの詳細をPROGRESS.mdへ移管。
- 2026.09.30: `KotodamaSpecGuard` 0.1.0をprivate GitHub Repositoryへcommit・push済みと確認。残件をClaude Code実機hook検証の1件へ整理。
- 2026.09.27: Hataori 3.1.22.0をGitHub Releaseとして公開。Hataori側の実装・配布を完了し、残件を`KotodamaSpecGuard`のGitHub公開とClaude Code実機hook検証へ整理。
- 2026.09.26: Hataori 3.1.22.0としてClaude Codeプラグイン有効化診断をMSI化し、実機Major Upgradeを検証。
- 2026.09.25: `doctor`へClaude Codeプラグイン有効化診断を追加し、Hataori側の実装項目を完了。
- 2026.09.24: `kotodama-spec-guard` 0.1.0の本体を別リポジトリ`KotodamaSpecGuard`へ実装（仕様書Markdown書込時のPostToolUse hook、`layer-reviewer`、手動スキル、テスト6件）。GitHub公開・実行時検証・Hataori側有効化確認は未完了。
- 2026.09.04: 契約／実装レイヤー混同防止のClaude Codeプラグイン化を、優先順位・対象バージョン未定の将来対応として追加。
- 2026.09.02: VirtualBox Windows 11隔離環境で3.1.21.0の新規Install／Uninstallを検証し、削除対象と保持対象を確認。
- 2026.09.02: 3.1.21.0をRelease。文書リンク自動検証、64文書、207テスト、MSI、実機`C:\Hataori`稼働を検証。
- 2026.09.02: 3.1.21.0へVersion更新。文書リンク自動検証をMSI化し、実機`C:\Hataori`へのMajor Upgradeと稼働確認を完了。
- 2026.09.02: Git管理中Markdownの相対リンクを検証する自動チェックとGitHub Actionsを追加。
- 2026.09.02: 3.1.20.0をRelease。`doctor`のService Account差異テスト、207テスト、MSI、実機`C:\Hataori`稼働を検証。
- 2026.09.02: 3.1.20.0へVersion更新。`doctor`のService Account差異テストをMSI化し、実機`C:\Hataori`へのMajor Upgradeと稼働確認を完了。
- 2026.09.02: 3.1.19.0へVersion更新。詳細MetricsをMSI化し、実機`C:\Hataori`へのMajor Upgradeと稼働確認を完了。
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

（進捗率の算定基準・再計算とSolution Releaseテストは完了。実機受入確認が1件残っています。）
- [X] ADR 0010の検証方針に沿ってMonitorのWinForms表示・更新・Task／Agent Runキャンセル操作の回帰テストを追加し、Windows上で再現できる検証結果を`docs/validation/2026-11-01-monitor-regression.md`へ記録する。
- [X] 進捗率の4軸算定基準を定義し、Itoguruma・Monitor・CLI・テストを含む10カテゴリの得点と総合・Phase 1率を再計算する（根拠は`PROGRESS.md`）。
- [X] 利用者向け文書とRelease配布手順を整備する。
- [X] Itoguruma、MCP、Agent resume、Windows Serviceを実環境で受入試験する。
- [X] Monitor回帰テスト追加後の全Solution Releaseテスト217件を実行し、全件合格を記録する（`docs/validation/2026-11-03-release-test-attempt.md`）。
- [x] 起動中のHataori ServerへMonitorを接続し、表示・更新とテスト専用Task／Agent Runのキャンセルを確認して記録する（手順: `docs/validation/2026-11-03-monitor-live-acceptance.md`）。2026.11.27に表示・更新・Taskキャンセル、2026.11.28にAgent Runキャンセルが合格（完了: 2026.11.28）。

# 優先タスク

## P0

- [X] Monitor参照機能を実装する。
- [X] DB Maintenanceを完成させる。

## P1

- [X] Hook、復旧、実環境統合試験を完成させる。

## P2

- [X] 文書、配布、インストール手順を完成させる。
- [X] VirtualBox Windows 11隔離環境でMSI Uninstallと設定・データ・ログ保持を実機検証する。
- [X] ADR 0010の検証方針に沿ってMonitorのWinForms表示・更新・Task／Agent Runキャンセル操作の回帰テストを追加し、Windows上で再現できる検証結果を`docs/validation/2026-11-01-monitor-regression.md`へ記録する。
- [X] 進捗率の4軸算定基準を定義し、Itoguruma・Monitor・CLI・テストを含む10カテゴリの得点と総合・Phase 1率を再計算する（根拠は`PROGRESS.md`）。
- [X] Monitor回帰テスト追加後の全Solution Releaseテスト217件を実行し、全件合格を記録する（`docs/validation/2026-11-03-release-test-attempt.md`）。
- [x] 起動中のHataori ServerへMonitorを接続し、表示・更新とテスト専用Task／Agent Runのキャンセルを確認して記録する（手順: `docs/validation/2026-11-03-monitor-live-acceptance.md`）。2026.11.27に表示・更新・Taskキャンセル、2026.11.28にAgent Runキャンセルが合格（完了: 2026.11.28）。

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
