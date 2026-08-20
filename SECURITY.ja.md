# セキュリティ

[English](SECURITY.md) | [日本語](SECURITY.ja.md)

この文書は、Hataoriの公開セキュリティモデルと安全な運用要件を説明します。

## サポート対象Version

| Version | セキュリティサポート |
| :--- | :--- |
| `3.0.3.0` | サポート対象 |
| それ以前のVersion | サポート対象外。Version固有の問題を報告する前にUpgradeしてください。 |

## 脆弱性の報告

疑われる脆弱性、token、host詳細、database内容、再現用secretを公開issueへ記載しないでください。Repository所有者へ非公開で連絡するか、Repositoryで有効な場合はGitHubのprivate vulnerability reporting機能を使用してください。

影響を受けるHataori Version、OS、影響範囲、無害化した再現手順、既に悪用されているかどうかを含めてください。現時点で固定の対応時間は保証されていません。Maintainerは受領確認、深刻度評価、公開議論前の開示調整を行います。

## セキュリティモデル

- Hataoriは単一のWindowsマシン向けに設計されており、MCPをloopback IPアドレスへbindします。`server:mcpHost`はloopback以外のアドレスを拒否します。
- MCP endpointは現時点でbearer token認証を持ちません。loopback bindとhost filteringがネットワーク境界であり、独立してレビューされた認証層を追加せずにproxyやport forward経由でendpointを公開しないでください。
- Control Pipeはローカル限定で、foreground管理のためcurrent-userアクセス制限付きで作成されます。
- Windows Serviceは`LocalSystem`として実行されます。Service install、setup、start、stop、removeには適切な管理者権限が必要です。
- Task cancel、fail、expire、Agent Run cancel、queue cancel、conversation reset、service制御、Uninstallは状態を変更または削除できます。実行前に対象ID、Service名、インストール先を確認してください。
- Agent実行は設定済みの作業ディレクトリと、CodexまたはClaude Codeの権限モードを継承します。Hataoriは各AgentのSandbox、Workspace trust、承認ポリシーを代替しません。

## Secretの取り扱い

- Itogurumaは`ITOGURUMA_AUTH_TOKEN`を発行します。tokenをコミット、表示、ログ出力、貼り付け、文書やMCP Client設定への埋め込みをしないでください。
- `hataori setup itoguruma`は、値を表示せずに対話ユーザー向け`HATAORI_ITOGURUMA__AUTHENTICATIONTOKEN`へtokenをコピーします。
- `hataori service setup`はtokenを`%INSTALL_ROOT%\config\hataori.service.json`へ保存し、ACLを`SYSTEM`と`Administrators`だけに制限します。
- 通常設定`%INSTALL_ROOT%\config\hataori.json`にtokenを含めてはなりません。MSIパッケージには可変設定、secret、利用者データ、logを含みません。
- CLI設定出力は、token・password・secret・credential・keyを示すキーを含む値をマスクします。
- Logとreportには無害化済みerrorだけを含めてください。診断資料をissueへ添付する前にsecretと個人情報を除去してください。

## 利用者の責任

- Windows、Hataori、.NET runtimeコンポーネント、Itoguruma、Codex CLI、Claude Codeを更新済みに保ってください。
- 管理者アクセスを制限し、`%INSTALL_ROOT%\config`、`data`、`logs`を適切なfilesystem権限で保護してください。
- `server:mcpHost`をloopbackに維持し、`allowedHosts`を意図したローカル名だけに限定してください。
- 自動activationを有効化する前に、`activation:workingDirectory`、Agent権限モード、hook、同時実行数上限を確認してください。
- Upgrade、復旧作業、手動削除の前に`config`と`data`をバックアップしてください。Uninstallは意図的に`config`、`logs`、`data`を保持します。
- インストール前に、信頼できるリリースチャネル経由でMSIのhashを検証してください。
