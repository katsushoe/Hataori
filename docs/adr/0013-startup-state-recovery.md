# ADR 0013: 起動時のRun・Session・Message復旧

## Status

Accepted

## Context

Serverが異常終了すると、DB上のAgent Run、Conversation Session、Message Processingが`starting`または`running`のまま残る。状態を放置すると同一Conversationの後続処理が停止する一方、生存中のAgent Processを誤って失敗扱いしてはならない。

## Decision

Server起動時にactive Agent RunのPIDとProcess開始日時を照合する。Processが不在または別Processへ再利用されている場合、Runを`failed`、関連する未完了Messageを`failed`、対応するrunning Sessionを`invalid`へ更新する。生存Processに対応する状態は維持する。Activation Workerは復旧完了ゲートを待ってからQueue処理を開始する。

## Alternatives

- すべてのrunning状態を無条件に失敗へ変更する案は、生存Agent Processを破壊するため不採用とした。
- 状態を変更せず手動復旧する案は、Conversation Queueが恒久停止し得るため不採用とした。

## Impact

起動直後にRun、Message、Sessionの状態が変化する場合がある。invalid Sessionは次回Activationで新規Sessionとして再作成される。復旧はterminal状態を対象外とするため冪等である。

## Security

PIDだけでなく開始日時も照合し、PID再利用による誤認を避ける。Processへ接続、停止、入力送信は行わず、生存確認だけを行う。

## Operations

復旧結果は変更件数と生存件数だけを構造化ログへ記録する。復旧自体が失敗した場合はActivationを開始せず、Hostの重大エラーとして扱う。

## Implementation and verification

Process Probe、復旧Service、起動Worker、Activation Gateを実装する。自動テストでは不在Processの関連状態変更、再実行時の冪等性、生存Processの状態維持を確認する。
