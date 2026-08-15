# ADR-0002: MCP Streamable HTTP

## Status

Accepted

## Context

AI AgentはSQLiteへ直接接続せず、HataoriのApplication ServiceをMCP Toolとして利用する。常駐Serverへ複数Agentから接続できるHTTP Transportが必要である。

## Decision

公式C# SDK 2.0.0のstateless Streamable HTTPを`/mcp`へ公開する。Kestrelは設定されたloopback IPだけでlistenし、Host filteringもloopback名へ限定する。MCP ToolはTask Application Serviceのみを呼び出す。

## Alternatives

- stdio: Agent終了時にMCPも停止し常駐要件を満たさないため不採用。
- legacy SSE: HTTPレベルのbackpressureが弱く、公式SDKでも非推奨のため不採用。
- 独自JSON-RPC: MCP互換性とSDKのschema生成を失うため不採用。

## Impact

Task操作はMCPのTool discoveryと入力schemaを通じて利用できる。stateless構成のため、server-to-client通知や接続単位の状態は保持しない。

## Security

listen先はloopback IPに限定し、`AllowedHosts`は`127.0.0.1`と`localhost`だけを許可する。CORSは有効化しない。外部公開する場合は別ADRで認証・TLS・公開境界を定義する。

## Operations

host、port、pathは`hataori.json`または環境変数で設定する。loopback以外のhostは起動時検証で拒否する。

## Implementation and verification

公式`ModelContextProtocol.AspNetCore`を使用し、`MapMcp`でendpointを登録する。ToolとApplication Serviceの縦断テスト、設定検証、全体ビルドを完了条件とする。
