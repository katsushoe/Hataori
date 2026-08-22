# ADR 0016: MCP／CLI対称化の境界

## Status

Accepted（2026-08-23）

## Context

HataoriはAI Agent向けMCP Toolsとローカル管理者向けCLIを提供する。Task、Agent Run、Versionの同一ユースケースに片側だけの操作があると自動化経路によって機能差が生じる。一方、Service登録、設定生成、ログ追跡、Monitor起動、Hook処理までMCPへ公開すると、loopback MCP ClientへOS管理権限と対話処理を不必要に広げる。

## Decision

- MCPが公開するTask、Agent Run、Versionの各ユースケースにはCLI相当コマンドを提供し、同じApplication ServiceまたはControl Pipe処理を使用する。
- `task_find_conflicts`のCLI相当を`hataori task find-conflicts`として追加する。
- CLIのOS管理、設定、診断、ストリーム、UI、Hook機能はMCP対称化の対象外とする。
- 入力名や出力の包み方は各プロトコルの規約に従ってよいが、状態遷移、フィルター、権限条件、永続結果は一致させる。

## Alternatives

- 全CLIコマンドを個別MCP Toolとして公開する案は、権限面を過度に拡大し、対話・ストリーム処理をMCPへ不自然に持ち込むため不採用とした。
- 任意CLI引数を受ける単一MCP Tool案は、Tool schema、annotation、入力検証、承認判断を失うため不採用とした。
- MCPとCLIを別実装のまま維持する案は、状態遷移の差異が再発するため不採用とした。

## Impact

Task基本操作、Agent Run cancel、VersionはMCP／CLIの両方から利用できる。ローカル管理専用機能はCLIに限定されるため、製品全体のコマンド数は一致しない。

## Security Conditions

破壊的MCP Toolは`Destructive` annotationを維持する。CLI固有の管理者操作、秘密設定、ローカルファイル操作を汎用MCP経由で公開しない。

[ADR 0017](0017-project-addressed-provider-selection.md)で承認された`providerPriority`は例外とする。これは秘密値やOS管理設定を含まない起動選択ポリシーであり、CLIとMCP Toolsの双方から同一Serviceを介して変更できる。

## Operational Conditions

MCP／CLI対応表を利用者文書で維持し、同一ユースケース追加時は両経路の実装とテストを同じ変更で行う。

## Implementation, Tests, and Documentation

CLIとMCPは共通Application Serviceを呼び出す。CLI統合テストとMCP Toolテストで同等結果を検証し、`COMMANDS.md`／`COMMANDS.ja.md`へ構文と境界を反映する。
