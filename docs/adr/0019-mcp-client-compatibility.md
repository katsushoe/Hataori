# ADR 0019: MCP Client互換性

## Status

Accepted（2026-08-25）

## Context

Hataori MCPはCodexとClaude Codeの双方から利用される。Client登録方法は異なるが、Client名や固有拡張によってToolの検出、入力schema、構造化出力、状態遷移、権限annotation、エラーが変わると、同じHataori操作がClientごとに異なる結果になる。

## Decision

- Hataori MCPは標準Streamable HTTPを共通経路とし、Client固有分岐を設けない。
- CodexとClaude CodeのClient情報でinitialize、Tool discovery、入力・出力schemaとannotationを含むTool契約、`get_version`のstructured contentが同一になることを`hataori mcp compatibility`で検証する。
- 業務ToolはClientから独立したApplication Serviceを呼び出し、入力、状態遷移、権限条件、永続結果、エラー契約を共通化する。
- Client固有機能が必要な場合は標準MCPの代替経路を維持し、差異を実装前に承認・文書化する。

## Verification

`hataori mcp compatibility`が`compatible: true`を返し、`codex`と`claude-code`で同じTool名一覧、Tool数、`get_version`結果を示すことを確認する。代表的な読み取り、書き込み、エラー操作はMCP Toolの共通テストで検証する。
