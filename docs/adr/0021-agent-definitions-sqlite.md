# ADR 0021: Agent definitions in workspace-scoped SQLite

## Context

Agent availability and concurrency were inferred from configuration. Operators could not manage definitions independently per workspace or inspect an audit history.

## Decision

- Persist Agent definitions in SQLite with `(workspace_id, agent_id)` as the primary key.
- Store enabled state, maximum concurrent runs, timestamps, and append-only create/update history.
- On first startup for a workspace, seed definitions for registered drivers from `activation.maxConcurrentRuns`; do not overwrite existing rows afterward.
- Use enabled persisted definitions as the source for Activation lanes. Changes take effect after service restart because lanes are created at startup.
- Expose list, set, and history operations through CLI and MCP.

## Status

Accepted.

## Alternatives considered

- Keep configuration as the source of truth: rejected because it cannot provide workspace scope or audit history.
- Re-read definitions dynamically for every queued message: deferred because changing a running lane topology requires a separate lifecycle design.

## Impact

Existing installations retain their effective initial values through automatic seeding. Database backup and restore now includes Agent definitions and their history. The update API validates concurrency to 0 through 64; unknown Agent IDs can be stored but do not run without a registered driver.

## Security and operations

Definitions contain no credentials. Write access remains limited to local CLI database permissions and the local MCP endpoint. Operators should restart the service after changing concurrency or enabled state.

## Verification and documentation

Repository tests cover workspace isolation, normalization, validation, updates, and audit history. `COMMANDS.md`, `COMMANDS.ja.md`, `CONFIG.md`, and `CONFIG.ja.md` describe management and bootstrap behavior.
