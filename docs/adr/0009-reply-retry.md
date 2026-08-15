# ADR 0009: Itoguruma Reply Retry

## Status

Accepted

## Context

Agent実行が完了しても、Itogurumaの一時停止や通信障害で返信だけが失敗する場合があります。この場合にAgentを再実行すると二重作業や異なる回答が発生するため、完了済みRunの応答だけを安全に再送する必要があります。

## Decision

- `message_processing` に返信試行回数、次回時刻、返信エラー、Itoguruma Reply Message IDを保存します。
- 再送本文は最新のcompleted Agent Runの `final_message` を使用し、Agentは再実行しません。
- idempotency keyは初回と同じ `hataori-reply:<message-id>` を使用します。
- 既定で最大5回、5秒から最大300秒までの指数バックオフを使用します。
- 再送予定がある間はMessage状態を `running` に保ち、同じConversationの後続Messageを先行させません。
- 送信成功後に `responded` とReply Message IDを同一のローカル更新で保存します。
- 最大試行回数に達したMessageは `failed` のまま保持し、自動再送を停止します。
- 旧DBは起動時に不足する返信管理列だけを追加し、既存データを保持します。

## Alternatives

- Agent Run全体を再実行: 二重作業になるため不採用です。
- 毎回異なるidempotency key: 送信成功後のDB更新失敗で二重返信になるため不採用です。
- 無制限retry: 恒久障害時に負荷とログが増え続けるため不採用です。

## Consequences

上限到達後の手動retryと状態確認はCLI実装で提供する必要があります。単一Hataori Serverを前提とし、Reply Retry Workerは1プロセス内で1つだけ起動します。

## Verification

再送成功、固定idempotency key、指数バックオフ後の上限到達、再スケジュール停止、旧Schemaへの列追加を自動テストします。
