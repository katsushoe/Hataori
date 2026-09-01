# ADR 0010: Monitorスナップショットと限定管理操作

## Status

Accepted

## Context

Phase 1のWindows Forms MonitorはTask、Agent、Conversation / Session、Queue件数、基盤状態を監視する必要がある。GUIがSQLiteを直接編集・解析するとServerの責務と永続化境界が崩れる。

## Decision

Control Pipeへ`monitor`要求を追加し、Serverが各Repositoryから読み取り専用スナップショットを生成する。Monitorは3秒周期または利用者操作で取得する。Phase 2では、選択したactive Taskとqueued／starting／running Agent Runに限り、確認ダイアログを経て既存のServerユースケースへキャンセルを要求できる。CLIの`hataori monitor`は同一配置先の`Hataori.Monitor.exe`を起動する。

## Alternatives

- GUIからSQLiteを直接読む案は、Serverを正本とする境界を迂回するため不採用とした。
- MCP経由で取得する案は、MCPがAgent向け、Control Pipeがローカル管理向けという既存方針と合わないため不採用とした。

## Impact

Control応答にMonitorデータが任意追加される。既存のstatus、stop、reload応答との後方互換性は維持される。Server、CLI、Monitorを同じ版として配布する。

## Security

Named Pipeは現在ユーザー限定とし、Monitorの変更操作はTaskキャンセルとAgent Runキャンセルだけに限定する。任意SQL、その他のTask編集、Message本文変更は提供しない。操作対象IDはServerが検証し、Monitorは成功・失敗後にスナップショットを再取得する。

## Operations

MonitorとCLIは同じディレクトリへ配置する。Control Pipe名は`--pipe`または`HATAORI_CONTROL_PIPE_NAME`で指定できる。

## Implementation and verification

Serverのスナップショット生成、CLI起動、WinForms表示を実装し、ビルド・自動テスト・publishを行う。GUI実機表示はWindows Serviceを含む実環境受入試験で確認する。
