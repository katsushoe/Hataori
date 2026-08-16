# ADR 0012: 共通Agent Lifecycle Hookランナー

## Status

Accepted

## Context

CodexとClaude CodeのLifecycle HookからTask ProtocolのContext注入、変更前確認、終了時確認を行う必要がある。Agent別スクリプトへ業務判断を重複実装すると挙動が分岐する。

## Decision

`hataori hook`をstdin JSON／stdout JSONの共通ランナーとする。SessionStartとUserPromptSubmitはContextを追加し、PreToolUseは変更操作時にactive Taskがなければ拒否し、Stopは未完了Taskがあれば1回だけ継続を要求する。CodexとClaude Code用JSONテンプレートを配布し、doctorは必須イベントとJSON構造を検査する。

## Alternatives

- Agent別スクリプトへTask検索を実装する案は、Control Pipe契約と判断ロジックが重複するため不採用とした。
- HookからSQLiteを直接読む案は、Serverを正本とする境界を迂回するため不採用とした。

## Impact

CLIに`hook`コマンド、設定に`hooks`節、配布物にHookテンプレートが追加される。既存設定に`hooks`節がない場合の設定検証は後方互換を維持する。

## Security

HookはControl Pipeの読み取り専用Monitorスナップショットだけを使用する。PreToolUseはTask未登録の変更操作を拒否するが、DBや権限境界の代替にはしない。Codexでは利用者がHook定義を確認して信頼する必要がある。

## Operations

`codexConfigPath`と`claudeConfigPath`は設定ファイル基準の相対パスまたは絶対パスで指定する。doctorは両ファイルのSessionStart、UserPromptSubmit、PreToolUse、Stopを確認する。

## Implementation and verification

Hook入力処理、Context生成、変更拒否、Stop継続上限、設定ファイル診断を自動テストする。実AgentでのHook信頼・起動は実環境受入試験で確認する。
