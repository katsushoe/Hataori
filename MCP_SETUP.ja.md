# Hataori MCPセットアップ

[English](MCP_SETUP.md) | [日本語](MCP_SETUP.ja.md)

このガイドでは、ローカルで稼働するHataori Windows ServiceをStreamable HTTPでCodexまたはClaude Codeへ接続します。

## 前提条件

- MCPクライアントと同じWindowsマシンへHataoriをインストールします。
- [README.md](README.md)の初期設定を完了します。
- `Hataori` Windows ServiceがRunningであることを確認します。

既定のMCP設定は次のとおりです。

| 設定 | 値 |
| --- | --- |
| Server名 | `hataori` |
| Transport | Streamable HTTP（`http`） |
| URL | `http://127.0.0.1:45440/mcp` |
| MCP認証 | なし |

Hataoriは既定でloopbackだけを待ち受けます。Itoguruma認証トークンは別用途であり、MCPクライアント設定へ追加しないでください。

## Server確認

PowerShellで次を実行します。

```powershell
hataori service status
hataori mcp status
```

ServiceがRunningであることを確認します。`mcp status`は`connected: true`、設定済みendpoint、1以上のtool countを返す必要があります。

## Codex

`%USERPROFILE%\.codex\config.toml`へ次のtableを追加します。他の設定は保持し、`mcp_servers.hataori`が既にある場合はそのtableだけを置き換えます。

```toml
[mcp_servers.hataori]
url = "http://127.0.0.1:45440/mcp"
enabled = true
required = true
default_tools_approval_mode = "writes"
```

保存後にChatGPTデスクトップアプリ、Codex CLI、またはIDE拡張を再起動します。`/mcp`を開き、`hataori`が有効かつ接続済みであることを確認します。

## Claude Code

Hataoriをユーザースコープへ登録します。

```powershell
claude mcp add --transport http --scope user hataori http://127.0.0.1:45440/mcp
```

Claude Codeを再起動またはreloadし、登録を確認します。

```powershell
claude mcp get hataori
claude mcp list
```

1プロジェクトだけで使う場合は`--scope user`を`--scope project`へ変更します。意図的でない限り両スコープへ重複登録しないでください。

## 公開ツール

Hataoriは次のTaskツールを公開します。

- 読み取り専用: `task_get`、`task_list`、`task_history`、`task_relations`
- 状態更新: `task_start`、`task_heartbeat`、`task_complete`、`task_relation_add`
- 破壊的状態変更: `task_cancel`、`task_fail`、`task_expire`

接続確認では、Task状態を変更しない`task_list`を最初に呼び出します。`task_heartbeat`では必ず`progressPercent`を指定してください。

## Endpointを変更した場合

`%INSTALL_ROOT%\config\hataori.json`の`server.mcpHost`、`server.mcpPort`、`server.mcpPath`を変更した場合、クライアント側も同じURLへ変更します。Server設定変更後はHataori Serviceを再起動してください。

リモート公開を別途設計・保護していない限り、Serverはloopbackへ限定してください。現在のMCP endpointはbearer tokenを要求しません。

## トラブルシューティング

- 接続拒否: `hataori service status`を確認し、Serviceを起動してから`hataori mcp status`を再実行します。
- HTTP 404: クライアントURLを`server.mcpPath`へ合わせます。既定値は`/mcp`です。
- クライアントに`hataori`がない: ユーザーまたはプロジェクト設定を確認し、クライアントを再起動します。
- Claude Codeが承認待ち: プロジェクトをtrustし、プロジェクトスコープのMCP登録を承認します。
- 接続後にToolが失敗: `%INSTALL_ROOT%\logs`を確認し、`hataori doctor`を実行します。
- Itogurumaエラー: `hataori itoguruma test`を実行します。Itoguruma tokenをMCP設定へ追加しないでください。
