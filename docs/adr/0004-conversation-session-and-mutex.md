# ADR 0004: Conversation Session Registryとプロセス内Mutex

## Status

Accepted

## Context

同じConversationとAgentに対する複数メッセージを同時にresumeすると、Agent Sessionの文脈と実行状態が競合します。一方、異なるConversationの実行は並列性を維持する必要があります。

## Decision

- Session RegistryはSQLiteの `conversation_sessions` を正本とし、論理キーを `(conversation_id, agent_id)` とします。
- Session状態は `idle`、`running`、`invalid` とし、Domain Modelで遷移を検証します。
- Phase 1はHataori Serverを単一プロセスで動かすため、Conversation Mutexはプロセス内のキー付きSemaphoreとして実装します。
- 待機者がなくなったSemaphoreは辞書から削除し、長期運用時のキー増加を抑えます。
- 同じキーだけを直列化し、異なるConversationまたはAgentは並列実行可能とします。

## Alternatives

- SQLiteによる分散ロック: Phase 1の単一プロセス構成には複雑すぎるため不採用です。
- Agent単位の大域ロック: 無関係なConversationの並列性を失うため不採用です。

## Consequences

Activation ManagerはAgent起動またはresumeの前にMutexを取得し、Session状態更新まで保持する必要があります。将来Hataoriを複数プロセス化する場合は、Mutex実装をプロセス間ロックへ差し替えます。

## Verification

Domain状態遷移、SQLiteのupsert・復元・絞り込み、同一キーの待機、異なるキーの並列取得を自動テストします。
