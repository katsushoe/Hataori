# ADR 0014: Dynamic Permission Approval（通知専用v1）

## Status

Accepted

## Context

全仕様書117節「Dynamic Approval」は、Agentが権限不足に達した際にItoguruma経由でUser承認を仲介し、承認後にAgent実行を再開する設計を示す。Phase 1では未実装のまま据え置かれていた。

実装可否を調査した結果、現行アーキテクチャでは実行中のTool呼び出しを一時停止し、Itoguruma経由でUser承認を待って再開する、原設計どおりの仕組みは現実的な工数で作れないと判明した。

- CodexDriver／ClaudeCodeDriverは`codex exec`／`claude -p`を非対話・one-shotで実行し、プロセスが終了するまでstdoutを解析しない（streamingや対話的な入力受付を行わない）。
- `hataori hook`は1イベントごとに起動・終了する短命プロセスであり、stdin／stdoutのJSON応答を返した時点で終了する。Hook呼び出しをHataori Serverからの外部シグナルで長時間ブロックし続ける仕組みは存在しない。
- Task／Session／AgentRunのいずれのstatus enumにも「Waiting」「PendingApproval」に相当する状態がない。

したがって、Tool呼び出しを実際に一時停止し、承認後に同じ実行を再開するには、Hook起動中もHataori Serverへライブ接続し続けられる新しいIPC機構（またはstreaming／対話的なAgent起動方式）が前提として必要であり、これは本変更の範囲を超える。

## Decision

原設計の縮小版として、**事後通知のみ**を実装する。

`hataori hook`の`PreToolUse`が、Task未登録の変更操作を検知して`permissionDecision: deny`を返す場合（唯一の実在する権限判定ポイント）、Hook応答をstdoutへ返した後、Itoguruma経由でMessage送信者へ通知を送る。通知は`ReplyAsync`をbest-effortで1回呼び出すのみで、Agent実行の一時停止・再開は行わない。Userは通常のMessageで指示をやり直す。

通知の宛先を得るため、`ActivationManager.CreateEnvironment`が新たに`HATAORI_SENDER_AGENT_ID`環境変数（Itoguruma上のMessage送信者ID）をAgentプロセスへ渡す。`HookProcessor.Process`の戻り値を`object`から`HookResult(Payload, PermissionDenied, DenialReason)`へ変更し、Hookのstdout契約（`Payload`）自体は変更しない。

## Alternatives

- 原設計どおりの一時停止・再開: 上記Contextの理由により、現行アーキテクチャ上で実現不可能と判断し不採用。
- Hookを対話的（stdin待ち受け）に変更する案: Codex／Claude Code側がHookプロセスをどこまで待機させるか未検証であり、CLI側の挙動保証がないため見送った。将来、対話的またはstreamingなAgent起動方式を採用する場合に再検討する。
- 承認をpre-run policy設定（`approveForMe`／`sandboxMode`／`permissionMode`）に限定する案: 既に実装済みで別物であり、今回のDynamic Approval対応としては採用しなかった（実行前の静的設定であり、実行中の動的承認ではないため）。

## Impact

- `ActivationManager`が渡すAgent実行環境変数に`HATAORI_SENDER_AGENT_ID`が追加される。
- `HookProcessor.Process`の戻り値型が`object`から`HookResult`へ変わる（`Hataori.Cli`内部の型であり、Hookのstdout JSON契約は変わらない）。
- `hataori hook`はPreToolUseがdenyした場合のみ、追加でItogurumaへ1回のReplyAsync呼び出しを行う。

## Security

通知はbest-effortであり、失敗（Itoguruma未接続、設定不足等）してもHookの`deny`判定自体には影響しない。通知本文にはdenyの理由のみを含み、secretやtoken、ファイル内容は含めない。

## Operations

Itoguruma設定（`itoguruma`節）が存在しない、または接続に失敗する環境では通知は送信されないが、`hataori hook`自体は従来どおり動作する。運用者はItoguruma接続状態を`hataori itoguruma status`で確認できる。

## Implementation and verification

`HookProcessor`の`PermissionDenied`判定を自動テストする（`tests/Hataori.Cli.Tests/HookProcessorTests.cs`）。Itoguruma通知の送信自体はCLI境界のbest-effort I/Oであり、実機でのHook denyトリガーと`hataori itoguruma status`によるログ確認で検証する。

将来、対話的またはstreamingなAgent起動方式が導入された場合、原設計どおりの一時停止・再開型Dynamic Approvalを別ADRとして再検討する。
