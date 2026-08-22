# ADR 0008: Activation Manager

## Status

Superseded in part by [ADR 0017](0017-project-addressed-provider-selection.md). Queue activation and session rules remain accepted; recipient and provider-selection rules are replaced.

## Context

Itogurumaから永続Queueへ取り込んだメッセージを、対象Agentの新規Sessionまたは既存Sessionへ安全に配送する調停処理が必要です。Queue、Conversation Mutex、Session、Agent Run、Driverの状態は一貫した順序で更新する必要があります。

## Decision

- Itogurumaでは `codex` と `claude-code` を監視Agentとして登録し、受信Agent IDをQueueへ保存します。
- ActivationはFIFO Queueから1件をclaimし、Agent IDに一致する `IAgentDriver` を選択します。
- `(conversation_id, agent_id)` のMutexを取得してからSessionを判定し、idleならresume、未登録またはinvalidなら新規実行します。
- Agent Runを `queued`、`starting`、Process開始時に `running`、終了時に終端状態へ更新します。
- Driver成功後にSession IDを登録または更新します。resume失敗時は旧Sessionをinvalidにします。
- Agentへ `HATAORI_ROOT`、Conversation ID、Message ID、Agent ID、MCP URLを環境変数で渡します。
- Agentごとに `maxConcurrentRuns` 数の逐次laneを起動し、Agent単位の最大並列数を保証します。
- Queue claimはAgentで絞り込み、同じConversationに `starting` または `running` のMessageがある間は後続MessageをQueueに残します。
- Agent応答を元送信者へ同じConversationのreplyとして返し、Itoguruma送信成功後だけMessage処理状態を `responded` にします。
- Replyのidempotency keyは `hataori-reply:<message-id>` とし、送信後のローカル更新失敗でも安全に再送できるようにします。
- Reply失敗時も成功済みAgent RunとSessionは維持し、Messageを再送待ちにします。再送上限到達時だけ `failed` にします。
- 誤ったWorkspaceでAgentを起動しないよう、Activationは既定で無効とし、有効化時は既存の絶対Working Directoryを必須とします。

## Alternatives

- Watcher内で直接Agentを起動: 通信再試行とAgent実行の責務が混在するため不採用です。
- Session状態を確認せず常に新規実行: Conversation文脈を失うため不採用です。
- 相対Working DirectoryをWindows Serviceで使用: 起動元によって解決先が変わるため不採用です。

## Consequences

Activation有効化には `HATAORI_ACTIVATION__ENABLED=true` と `HATAORI_ACTIVATION__WORKINGDIRECTORY=<absolute-path>` が必要です。既定の最大並列数はCodex、Claude Codeともに2です。同一Conversationは並列数に空きがあっても直列処理します。

## Verification

実SQLiteとDomain Serviceを用い、Driverのみをモックして、新規Session、resume、環境変数、Run完了、resume失敗時のRun失敗とSession無効化を統合テストします。
