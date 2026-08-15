# TODO.md Version
2026.08.16

# 変更履歴

- 2026.08.16

# Hataori TODO

このファイルはHataoriの短期タスクと実装計画を管理します。

# 残件サマリ

## すぐやる

- [-] Monitorアプリ本体と`monitor` CLIを実装する。
- [-] DB MaintenanceとRetention設定を実装する。

## 次にやる

- [-] Agent Hook連携とdoctor診断を実装する。
- [-] 異常終了復旧とGraceful Shutdownを統合検証する。

## 後でやる

- [-] 利用者向け文書とRelease配布手順を整備する。
- [-] Itoguruma、MCP、Agent resume、Windows Serviceを実環境で受入試験する。

# 優先タスク

## P0

- [-] Monitor参照機能を完成させる。
- [-] DB Maintenanceを完成させる。

## P1

- [-] Hook、復旧、実環境統合試験を完成させる。

## P2

- [-] 文書、配布、インストール手順を完成させる。

# フェーズ計画

## Phase 1: 基盤・必須運用機能

- [X] Server、SQLite、Task、MCP、主要CLI
- [X] Itoguruma、Queue、Reply、Retry
- [X] Session、Activation、Agent Driver
- [-] Monitor
- [-] 運用・復旧・実環境受入

## Phase 2: 運用強化

- [-] Dynamic Permission Approval
- [-] Priority Queue、強化Retry、Session recovery
- [-] Workspace・Monitor管理操作
