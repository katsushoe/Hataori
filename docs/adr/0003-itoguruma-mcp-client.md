# ADR 0003: ItogurumaとのMCPクライアント連携

## Status

Accepted

## Context

Hataori Serverは、エージェント間の通知・問い合わせ・回答をItoguruma経由で扱う必要があります。現在のItogurumaはBearer認証付きStreamable HTTP MCPサーバーとして、エージェント登録、メッセージ送受信、ACK、バージョン確認を公開しています。

## Decision

- Application層に `IItogurumaClient` を置き、MCP SDK固有型を境界外へ出しません。
- Infrastructure層では公式C# MCP Client SDKのStreamable HTTP transportを使用します。
- Server起動時にエージェント登録と疎通確認を行い、失敗時は指数バックオフで設定回数まで再接続します。
- 接続先はloopback HTTP(S)に限定し、Bearer tokenは設定ファイルへ保存せず、`HATAORI_ITOGURUMA__AUTHENTICATIONTOKEN` 環境変数から渡します。
- 受信APIはItogurumaのlease/ACKモデルをそのまま表現します。永続キュー完成前にバックグラウンドでメッセージをleaseすると配送を阻害し得るため、自動ポーリングはキュー実装と同時に開始します。
- 返信時は呼び出し元がidempotency keyを指定し、再試行でも同じ値を使用します。

## Alternatives

- 独自HTTP呼び出し: MCPプロトコル処理と将来互換性を重複実装するため不採用です。
- 自動ポーリングを先行実装: 永続化前のクラッシュで処理状態を失うため不採用です。

## Consequences

接続・認証・受信・返信・ACKの境界が確立され、Itogurumaの変更はAdapter内へ隔離されます。受信メッセージはSQLiteへ永続化してからACKし、再配信時は `message_id` で重複排除します。キューから先のSession起動とタスク変換はActivation実装まで行いません。

## Security and Operations

tokenはログへ出力しません。接続先をloopbackへ限定し、設定不備はServer起動時に検出します。再接続上限を超えた場合はBackgroundServiceを異常終了させ、ホストの監視機構へ通知します。

## Verification

設定validatorの正常系、token欠落、非loopback接続先を自動テストします。ソリューション全体のビルドとテストでSDK統合を検証します。
