# ADR 0006: Codex CLI Driver

## Status

Accepted

## Context

Phase 1ではCodex App Serverを必須とせず、Codex CLIを1ターンごとの子プロセスとして起動します。新規Sessionと既存Sessionのresume、Thread ID、最終応答、異常終了を共通形式で扱う必要があります。

## Decision

- ローカルで確認したCodex CLI 0.147.0の `codex exec --json` と `codex exec resume --json` を使用します。
- プロンプトはコマンドライン引数へ含めず、`-` を指定して標準入力から渡します。
- 新規実行は `workspace-write` sandboxと自動承認レビューを既定とし、`danger-full-access` は設定検証で拒否します。
- JSONLの `thread.started` からThread IDを、最後の `item.completed` / `agent_message` から最終応答を取得します。
- `error` と `turn.failed` をDriver失敗として扱い、Process結果を例外へ保持します。
- resumeでは明示的なSession IDを必須とし、`--last` は使用しません。
- CLIパス、model、sandbox、出力上限は設定可能とします。

## Alternatives

- Codex App Server: Phase 1の必須範囲を超えるため不採用です。
- Promptをコマンドラインへ指定: Process一覧や診断情報へ内容が露出し得るため不採用です。
- `dangerously-bypass-approvals-and-sandbox`: 安全境界を失うため不採用です。

## Consequences

CLIのJSONL仕様変更はParserとCommand Builderへ局所化されます。実アカウントを使用するend-to-end試験は通常の自動テストには含めず、診断コマンドで別途行います。

## Verification

Command引数、新規・resumeのSession指定、JSONLの成功・失敗・不正入力、設定の危険sandbox拒否を自動テストします。
