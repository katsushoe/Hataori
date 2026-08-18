# ADR 0015: Itoguruma未連携でもHataori Serverを起動可能にする

## Status

Accepted

## Context

Hataoriは「Itogurumaが無くても単独動作する」ことを想定した設計である（`ItogurumaConnectionWorker`は接続失敗を`degraded`状態として扱い、再接続を試行し続けるだけでServer全体をクラッシュさせない）。

しかし実際には、Server起動時に次の2箇所が無条件でItoguruma連携済みであることを要求しており、設計意図と矛盾していた。

- `ItogurumaClientOptionsValidator`が`AuthenticationToken`の非空を必須としており、`.ValidateOnStart()`によりASP.NET Coreの起動シーケンス自体が例外で停止する。
- `Program.cs`がWindows Service実行時に限り、`hataori.service.json`（`hataori service setup`が作成するItoguruma token保存file）を`optional: false`で読み込んでおり、fileが存在しないだけで起動が停止する。

この2つにより、実機で「MSI Install直後、`hataori service setup`未実施の状態でServiceを起動すると即座に失敗する」という事象が発生した（`docs/installation.md`は当初この挙動を「認証設定前の誤起動を防ぐための意図的な仕様」と説明していたが、実際にはItoguruma接続失敗時の`degraded`処理という既存の設計意図を反映できていなかっただけの不具合だった）。

## Decision

Itoguruma未連携をServer起動の妨げにしない。

- `ItogurumaClientOptionsValidator`から`AuthenticationToken`必須チェックを削除する（Endpoint形式・AgentId・MonitoredAgentIds等、他の妥当性検証は維持する）。
- `Program.cs`の`hataori.service.json`読み込みを`optional: true`に変更する。
- 結果として、token未設定時は`ItogurumaConnectionWorker`が既存の`degraded`ループへ自然に入り、Task管理・MCP・CLIなどItoguruma非依存の機能は正常に利用できる。

## Alternatives

- `ItogurumaClientOptions`に`Enabled`フラグを追加し、明示的に無効化できるようにする案: より丁寧だが、現状のdegradedループが既に「未連携時は再試行し続けるだけで実害がない」という設計であるため、追加のフラグと分岐を導入するコストに見合わないと判断し見送った。将来、Itoguruma接続試行自体を完全に止めたい要件が出た場合に再検討する。

## Impact

- MSI Install直後、`hataori config init`／`hataori service setup`を実行する前でもServiceを起動できるようになる。これにより`installer/Package.wxs`の`ServiceControl/@Start="install"`（Install直後の自動起動）を安全に使えるようになった。
- `hataori doctor`の`itoguruma`チェックはtoken未設定時、引き続き接続失敗として`ok: false`を報告する（起動可否とは別の診断情報として維持）。

## Security

Itoguruma未連携時、Server自体はMCP・CLI経由のTask操作を引き続き受け付ける。認証トークンを必須にしないことがセキュリティ境界を弱めるわけではない（Itoguruma経路が使えないだけで、MCP endpointは既定でloopback限定のまま）。

## Operations

`hataori doctor`の`itoguruma`チェックがNGの場合、`hataori service setup`未実施または token不整合を疑う。Server自体が起動していない場合は、`hataori.service.json`ではなく他の起動要因（`configuration`、`sqlite`等）を確認する。

## Implementation and verification

`ItogurumaClientOptionsValidatorTests.cs`でtoken未設定時に検証が成功することを自動テストする。`dotnet test`全133件成功を確認。実機での起動確認（`hataori.service.json`削除状態からのService起動）は別途実施する。
