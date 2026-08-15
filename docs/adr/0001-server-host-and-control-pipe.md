# ADR-0001: Server HostとローカルControl Pipe

## Status

Accepted

## Context

HataoriはSQLiteを正本とするWindows常駐Serverであり、CLIやMonitorから安全に管理操作を受ける必要がある。

## Decision

.NET Generic HostでDI・設定・ログ・Windows Serviceライフサイクルを統合する。管理操作は同一ユーザー限定のNamed Pipeで受け、Phase 1では`status`と`stop`を提供する。相対DBパスは実行ファイルのディレクトリを基準に解決する。

## Alternatives

- SQLite直接操作: 責務境界と同時実行制御を壊すため不採用。
- localhost HTTP: Phase 1のローカル管理用途には待受ポートと認証面が過剰なため不採用。
- Windows Service専用Host: 開発時のforeground実行を共通化できないため不採用。

## Impact

Server起動時にDBスキーマを初期化し、Control Pipeを常時待受する。CLIのServer管理コマンドは今後このPipeへ接続する。

## Security

Control Pipeは`CurrentUserOnly`で生成し、ネットワークへ公開しない。秘密情報は設定・応答・ログへ含めない。

## Operations

`hataori.json`または`HATAORI_`接頭辞の環境変数で設定する。`stop`はGeneric Hostの停止通知を使い、Hosted Serviceのキャンセルとリソース解放を待つ。

## Implementation and verification

Serverのコンポジションルートで依存を配線する。設定検証、パス解決、停止要求を単体テストし、Named Pipeの接続テストはServer統合テストで追加する。
