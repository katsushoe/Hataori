# PROGRESS.md Version
2026.08.18

# 変更履歴

- 2026.08.18
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

| 機能 | 2026.08.16 | 2026.08.18 |
| :--- | ---: | ---: |
| **グループ全体** | **94%** | **96%** |
| Server / Core / SQLite | 88% | 88% |
| Itoguruma連携 | 98% | 98% |
| Session / Activation | 100% | 100% |
| Task管理 | 95% | 95% |
| CLI | 97% | 98% |
| Windows Service | 100% | 100% |
| Monitor | 95% | 95% |
| 運用・復旧 | 97% | 98% |
| 文書・配布 | 75% | 90% |
| テスト | 96% | 96% |

## 現在フェーズ

Phase 1（基盤・必須運用機能）: **95%**

算定: Core 88%、Itoguruma 98%、Session / Activation 100%、Task 95%、Monitor 95%の単純平均（95.2%）です。

## 進捗予測メモ

設定・コマンド・運用文書の拡充が主要な残量です。GitHubと利用者向けREADME、MSIインストールガイド、英語・日本語MCPセットアップは整備済みです。Windows Serviceは標準`bin/config/logs/data`構成、SYSTEM・Administrators限定認証設定、x64 MSIのInstall・Major Upgrade・Uninstall保持、Automatic起動、Running、Itoguruma接続を実機確認済みです。Monitorはデータ入り表示、手動更新、異常時の案内・ログ、Itoguruma MCPの実接続状態表示を確認済みです。Codex CLI 0.147.0とClaude Code 2.1.220はstart・resume・Reply・ACKを実機確認し、自動テスト125件、Server、MCP、Hook、Graceful Shutdown、起動異常時の安全停止は確認済みです。

2026.08.17に3.0.3.0（MCP読み取り専用ツール`get_version`追加、`tool_count` 11→12）をWiX MSIでMajor Upgrade実機検証済みです（`docs/validation/2026-08-17-installer-3.0.3.0.md`）。同検証でUninstall実機検証のみ本番環境保護のため未実施のまま残っています。2026.08.18に`hataori doctor`の`server`チェックがSYSTEM以外の実行では原理的に必ず失敗する誤検知を修正し（`Skipped`判定を追加、commit `044f69a`）、ビルド・自動テスト125件・実機確認で反映を確認しました。専用の自動テストは未追加のため、運用・復旧の進捗は満点にしていません。

同日、`DOCUMENTS.md`が2026.08.16のまま更新されておらず`COMMANDS.md`／`CONFIG.md`／`PACKAGES.md`／`SECURITY.md`／`README.ja.md`（いずれも2026.08.17追加）を記載していなかった不一致を修正しました。あわせて`PACKAGES.ja.md`／`SECURITY.ja.md`を新規作成し、実際には手順化されていなかったRelease公開手順を`RELEASE.md`／`RELEASE.ja.md`として文書化しました（既存のtag・`gh release create`運用を明文化）。文書・配布は75%→90%とし、残りはドキュメント間リンクの自動整合チェックが手動レビュー頼みである点とUninstall実機検証未実施を反映しています。

同日、Phase 2（全仕様書143節）から3項目を実装しました。Agent Run cancel（`agent_run_cancel` MCP tool、`hataori agent cancel` CLI、Control Pipe経由。実装時に`agent cancel`が不要な`--database`を要求していた不具合をテストのdeadlockから検出し修正）、Task conflict detection（`task_find_conflicts` MCP tool、CJK bigram＋汎用語stopwordによる簡易キーワード一致）、Dynamic Permission Approval（通知専用v1。原設計の一時停止・再開は現行アーキテクチャ上不可能と判断し、PreToolUseのdeny時にItogurumaへ事後通知するのみに縮小、`docs/adr/0014-dynamic-approval-notify-only.md`参照）。自動テストは125件→133件。CLIを97%→98%としました。実機での動作確認は未実施です。

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
- [x] 標準ディレクトリ構成とx64 MSIのInstall・Upgrade・Uninstall
- [x] MCP `get_version`ツール追加とMSI Major Upgrade実機検証（3.0.3.0、2026-08-17）
- [x] `hataori doctor`の`server`チェック誤検知修正（非SYSTEM実行時はSkipped扱い、2026-08-18）
- [x] Agent Run cancel（`agent_run_cancel` MCP tool、`hataori agent cancel` CLI、2026-08-18）
- [x] Task conflict detection（`task_find_conflicts` MCP tool、2026-08-18）
- [x] Dynamic Permission Approval 通知専用v1（`docs/adr/0014`、2026-08-18）
- [x] 自動テスト133件

## 部分実装

- [x] Windows Service実機検証
- [x] Monitor実機表示検証
- [x] GitHub・利用者向けREADME
- [x] Codex／Claude Code向けMCPセットアップ（英語・日本語）
- [x] 設定、コマンド、パッケージ、セキュリティ文書（英語・日本語）
- [x] Release作成・公開手順の文書化（`RELEASE.md`／`RELEASE.ja.md`）
