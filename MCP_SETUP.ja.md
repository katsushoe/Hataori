# MCPセットアップ

[English](MCP_SETUP.md) | [日本語](MCP_SETUP.ja.md)

このGuideでは、ローカルで稼働するHataori Windows ServiceをStreamable HTTPでCodexまたはClaude Codeへ接続します。

## Values and Placeholders

| 値 | 取得方法 | 既定値/例 | 変更条件 |
| :--- | :--- | :--- | :--- |
| `$HataoriRoot` | MSIで選択したdirectory | `F:\Hataori` | 別の場所へInstallした場合。 |
| MCP Server名 | Clientに表示する論理名 | `hataori` | 意図的に別表示名を使う場合。 |
| MCP URL | `hataori.json`の`server.mcpHost`、`mcpPort`、`mcpPath` | `http://127.0.0.1:45440/mcp` | いずれかのServer設定を変更した場合。 |

`<install-root>`などの山括弧表記は説明用です。山括弧をterminalや設定fileへそのまま入力しないでください。

## Prerequisites

- MCP Clientと同じWindows machineへHataoriをInstallします。
- [README](README.ja.md)に従い`hataori config init`と`hataori service setup`を実行します。
- CodexまたはClaude CodeをInstallし、MSIによる`PATH`変更後は新しいterminalを開きます。
- `Hataori` Windows Serviceが登録済みであることを確認します。

## Authentication and Environment

Hataori MCPはClient credential、Authorization Header、環境変数を要求しません。loopbackへbindし、設定済みlocal host名だけを受け付けます。Itoguruma tokenをMCP Client設定へ追加しないでください。このtokenはHataoriからItogurumaへの別のoutbound接続用です。

Client scopeは登録を見られる範囲だけを制御します。Hataori Task権限、Agent sandbox、Server bind addressは変更しません。

## Start the Server

管理者PowerShellで、Install先が異なる場合だけpathを変更します。

```powershell
$HataoriRoot = 'F:\Hataori'
& "$HataoriRoot\bin\cli\Hataori.Cli.exe" service start
& "$HataoriRoot\bin\cli\Hataori.Cli.exe" service status
& "$HataoriRoot\bin\cli\Hataori.Cli.exe" mcp status
```

ServiceがRunningで、MCP JSONに`connected: true`、期待URL、1以上の`tool_count`があれば合格です。

## Register Clients

### Codex — 推奨する手動User設定

`%USERPROFILE%\.codex\config.toml`へ次の完全なtableを追加します。他の内容は保持し、既存`[mcp_servers.hataori]`だけを置換します。

```toml
[mcp_servers.hataori]
url = "http://127.0.0.1:45440/mcp"
enabled = true
required = true
default_tools_approval_mode = "writes"
```

- `hataori`はClient表示用Server名です。
- `url`はHataori Server設定と一致させます。HTTPはStreamable HTTPを選び、local start commandは不要です。
- `enabled`は登録をloadし、`required`はEndpoint失敗をClient起動問題として扱い、`default_tools_approval_mode`はWrite判断をCodex policyへ委ねます。
- Hataori MCPにClient認証がないため認証fieldは省略します。

保存後にChatGPT Desktop、Codex CLI、またはIDE拡張を再起動し、`/mcp`で`hataori`の接続を確認します。

### Claude Code — 推奨する自動User登録

既定URLでは次を実行します。

```powershell
claude mcp add --transport http --scope user hataori http://127.0.0.1:45440/mcp
```

Claude Codeは他設定を保持しながら`~/.claude.json`のUser設定を更新します。生成される論理entryは次のとおりです。

```json
{
  "mcpServers": {
    "hataori": {
      "type": "http",
      "url": "http://127.0.0.1:45440/mcp"
    }
  }
}
```

Command実行後にこのJSONを貼り付けないでください。これは生成結果の説明です。Server名は`hataori`、`type`はHTTP、URLはHataori設定と一致させ、認証不要のため認証fieldはありません。Claude Codeを再起動またはreloadし、`claude mcp get hataori`と`claude mcp list`を実行します。

### Alternative project-scoped registration

1 ProjectだけでHataoriを表示する場合に限り使用します。

- Codex: 同じTOML tableを`<project-root>\.codex\config.toml`へ置きます。
- Claude Code: `claude mcp add --transport http --scope project hataori http://127.0.0.1:45440/mcp`を実行し、他entryを保持してProject scopeを更新します。

Project scopeではWorkspace trustとMCP承認が必要な場合があります。重複表示が意図でない限りUser scopeと併用しません。

## Multiple Workspaces

1つのHataori Serviceは、`activation.workingDirectory`で設定した1 Workspace上のAgentを調整できます。3.0.5.0にはClient別MCP Workspace allowlistがありません。自動Agent Workspaceを変更する場合は絶対`activation.workingDirectory`を更新し、`hataori config check`後にServiceを再起動します。

ClientのUser/Project scopeは登録表示範囲だけを変えます。Agent working directoryを選択せず、Agent sandboxやWorkspace trustを回避しません。

## Verify the Connection

最初に失敗した段階で停止します。

1. **Server Endpoint:** `hataori mcp status`を実行します。合格: `connected: true`と期待Endpoint。
2. **Client互換性:** `hataori mcp compatibility`を実行します。合格: `compatible: true`で、`codex`と`claude-code`のTool名、Tool数、`get_version`構造化結果が一致すること。
3. **Client登録:** Codex `/mcp`または`claude mcp get hataori`がconnectedを示します。
4. **Read-only Tool call:** Status/Agent filterなしで`task_list`を呼びます。合格: 該当なしの空arrayを含む構造化Task array。
5. **全体診断:** `hataori doctor`を実行します。合格: `healthy: true`で、skip以外の全checkが`ok: true`。

状態変更と破壊的Tool annotationは[コマンド](COMMANDS.ja.md)を参照してください。`task_heartbeat`では必ず`progressPercent`を指定します。

## Troubleshooting

- 接続拒否: `hataori service status`を確認し、Serviceを起動してEndpoint確認を再実行します。
- HTTP 404: Client URLを`server.mcpPath`へ合わせます。既定値は`/mcp`です。
- Host/bind error: `server.mcpHost`をloopbackに保ち、`allowedHosts`をlocal URLへ合わせます。
- Codexに`hataori`がない: 選択した`config.toml` scopeを確認しCodexを再起動します。
- Claude Codeにentryがない: `claude mcp add`を再実行し、`claude mcp get hataori`で確認します。
- Claude Codeが承認待ち: ProjectをtrustしProject scope MCP entryを承認します。
- 接続後にToolが失敗: `hataori doctor`を実行し、共有時にsanitizeしたうえで`%INSTALL_ROOT%\logs`を確認します。
- Itoguruma error: `hataori itoguruma test`を実行し、Itoguruma tokenをMCP設定へ追加しません。
