# ADR 0008: Activation Manager

## Status

Accepted

## Context

Itogurumaから永続Queueへ取り込んだメッセージを、対象Agentの新規Sessionまたは既存Sessionへ安全に配送する調停処理が必要です。Queue、Conversation Mutex、Session、Agent Run、Driverの状態は一貫した順序で更新する必要があります。

## Decision

- Itogurumaでは `codex` と `claude-code` を監視Agentとして登録し、受信Agent IDをQueueへ保存します。
- ActivationはFIFO Queueから1件をclaimし、Agent IDに一致する `IAgentDriver` を選択します。
- `(conversation_id, agent_id)` のMutexを取得してからSessionを判定し、idleならresume、未登録またはinvalidなら新規実行します。
- Agent Runを `queued`、`starting`、Process開始時に `running`、終了時に終端状態へ更新します。
- Driver成功後にSession IDを登録または更新します。resume失敗時は旧Sessionをinvalidにします。
- Agentへ `HATAORI_ROOT`、Conversation ID、Message ID、Agent ID、MCP URLを環境変数で渡します。
- Agent応答をItogurumaへ返信するまではMessage処理状態を `running` に保持します。
- 誤ったWorkspaceでAgentを起動しないよう、Activationは既定で無効とし、有効化時は既存の絶対Working Directoryを必須とします。

## Alternatives

- Watcher内で直接Agentを起動: 通信再試行とAgent実行の責務が混在するため不採用です。
- Session状態を確認せず常に新規実行: Conversation文脈を失うため不採用です。
- 相対Working DirectoryをWindows Serviceで使用: 起動元によって解決先が変わるため不採用です。

## Consequences

Activation有効化には `HATAORI_ACTIVATION__ENABLED=true` と `HATAORI_ACTIVATION__WORKINGDIRECTORY=<absolute-path>` が必要です。現段階のWorkerは逐次実行であり、Agent別並列数制御は次の実装で追加します。

## Verification

実SQLiteとDomain Serviceを用い、Driverのみをモックして、新規Session、resume、環境変数、Run完了、resume失敗時のRun失敗とSession無効化を統合テストします。
