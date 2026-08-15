# ADR 0007: Claude Code CLI Driver

## Status

Accepted

## Context

Phase 1ではClaude Codeをprint modeの子プロセスとして1ターンずつ起動し、新規Sessionとresumeを共通のAgent Driver境界で扱います。

## Decision

- 新規実行は `claude -p --output-format json`、resumeは明示的な `--resume <session-id>` を使用します。
- Promptはコマンドラインへ含めず標準入力から渡します。
- JSONの `session_id`、`result`、`subtype`、`is_error` を解析します。
- Permission modeは `acceptEdits` を既定とし、`plan` も許可します。`bypassPermissions` は設定検証で拒否します。
- CLIパス、model、permission mode、出力上限を設定可能とします。
- CLI未導入環境でもServer設定は可能ですが、実行時の起動エラーをAgent Run失敗として扱います。

## Alternatives

- 対話モードの常駐Process: Phase 1の1ターン実行方針を超えるため不採用です。
- Promptをコマンドラインへ指定: Process情報への露出を避けるため不採用です。
- `bypassPermissions`: 安全境界を失うため不採用です。

## Consequences

Claude Code固有の引数とJSON形式はCommand BuilderとParserへ局所化されます。CLIが利用可能な環境でhelpおよびend-to-end診断を追加確認する必要があります。

## Verification

新規・resumeの引数、JSON成功・エラー・不正入力、危険なpermission modeの拒否を自動テストします。
