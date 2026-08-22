# ADR 0018: Codex Desktop Task Launch Bridge

## Status

Accepted (2026-08-23)

## Context

`codex exec` starts a headless CLI session and does not create a task visible under a saved Codex Desktop project. Hataori must hand Codex-addressed messages to a receiver already running inside Codex Desktop, where the internal project and task creation tools are available.

## Decision

- Codex-addressed queue items are excluded from CLI activation lanes.
- A fixed Codex Desktop receiver claims the oldest Codex launch request through `codex_task_claim`.
- A claim has a 30–3600 second lease. An expired claim becomes claimable again.
- After creating the destination task, the receiver records its Codex task ID through `codex_task_started`.
- If project resolution or task creation fails, the receiver calls `codex_task_release`; the original request remains queued.
- MCP and CLI use the same `CodexTaskLaunchService` and SQLite repository.

## Alternatives

- Continue using `codex exec`: rejected because it does not create a Codex Desktop task.
- Let Hataori call an undocumented Desktop API: rejected because no stable external contract is available.
- Delete a request immediately on claim: rejected because a receiver crash would lose the request.

## Impact

Codex task startup requires Codex Desktop and its fixed receiver to be running. Claude Code and other configured providers retain the existing process-based activation path. Completion and reply synchronization are outside this change.

## Security Conditions

The MCP endpoint remains loopback-only. A claim returns the message prompt and working directory, so only the trusted receiver may use these tools. Started/release operations require the unguessable claim token.

## Operational Conditions

Only saved Codex projects can be resolved by the receiver. The receiver must release claims it cannot start, and should use a heartbeat rather than standalone cron tasks to avoid accumulating receiver chats.

## Implementation, Tests, and Documentation

The SQLite repository performs claim and state transitions in immediate transactions. Tests cover started removal, expired-lease reclaim, release/retry, MCP wiring, CLI behavior, and exclusion of Codex from process lanes.
