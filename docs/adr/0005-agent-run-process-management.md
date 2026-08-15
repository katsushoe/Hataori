# ADR 0005: Agent Runと子プロセス管理

## Status

Accepted

## Context

CodexおよびClaude Code Driverは1ターンごとに子プロセスを起動し、PID、終了コード、出力、Session IDをHataoriへ返す必要があります。Agentプロセスの異常終了や大量出力でHataori Server自体を停止させてはなりません。

## Decision

- Agent Runの状態と結果をSQLiteの `agent_runs` へ永続化します。
- 状態遷移は `queued`、`starting`、`running`、`completed`、`failed`、`cancelled` とし、Task状態とは分離します。
- Process Managerは `UseShellExecute=false` と `ArgumentList` を使用し、シェルを介さず起動します。
- stdoutとstderrは同時に読み切り、既定4 MiB相当の文字数で保存内容を打ち切ります。上限後もストリームは読み続け、子プロセスの停止を防ぎます。
- キャンセルおよび破棄時は子孫を含むProcess Treeを終了します。
- Working Directoryと環境変数はDriverから明示的に渡します。

## Alternatives

- shell経由のコマンド文字列: 引数の解釈とコマンド注入リスクがあるため不採用です。
- stdout/stderrの無制限保存: 長期実行時のメモリ枯渇リスクがあるため不採用です。

## Consequences

Codex DriverとClaude Code Driverは同じProcess境界を利用できます。Agent固有JSONの解釈とfinal message抽出は各Driverが担当します。

## Verification

RunのDomain状態遷移、SQLite永続化・絞り込み、実子プロセスの終了コード・stdout取得、出力上限を自動テストします。
