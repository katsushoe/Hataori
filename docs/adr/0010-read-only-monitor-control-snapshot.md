# ADR 0010: 読み取り専用Monitorスナップショット

## Status

Accepted

## Context

Phase 1のWindows Forms MonitorはTask、Agent、Conversation / Session、Queue件数、基盤状態を監視する必要がある。GUIがSQLiteを直接編集・解析するとServerの責務と永続化境界が崩れる。

## Decision

Control Pipeへ`monitor`要求を追加し、Serverが各Repositoryから読み取り専用スナップショットを生成する。Monitorは3秒周期または利用者操作で取得し、編集機能を持たない。CLIの`hataori monitor`は同一配置先の`Hataori.Monitor.exe`を起動する。

## Alternatives

- GUIからSQLiteを直接読む案は、Serverを正本とする境界を迂回するため不採用とした。
- MCP経由で取得する案は、MCPがAgent向け、Control Pipeがローカル管理向けという既存方針と合わないため不採用とした。

## Impact

Control応答にMonitorデータが任意追加される。既存のstatus、stop、reload応答との後方互換性は維持される。Server、CLI、Monitorを同じ版として配布する。

## Security

Named Pipeは現在ユーザー限定とし、Monitorは読み取り要求だけを送る。任意SQL、Task編集、Agent操作、Message本文変更は提供しない。

## Operations

MonitorとCLIは同じディレクトリへ配置する。Control Pipe名は`--pipe`または`HATAORI_CONTROL_PIPE_NAME`で指定できる。

## Implementation and verification

Serverのスナップショット生成、CLI起動、WinForms表示を実装し、ビルド・自動テスト・publishを行う。GUI実機表示はWindows Serviceを含む実環境受入試験で確認する。
