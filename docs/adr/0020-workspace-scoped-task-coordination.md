# ADR 0020: Workspace-scoped task coordination

## Status

Accepted

## Context

Hataori previously treated every task as part of one implicit global workspace. Project discovery already used one configured projects root, but clients could not discover that boundary or isolate task and conflict queries when several workspaces are coordinated.

## Decision

- A workspace is identified by a normalized `^[a-z][a-z0-9]*$` ID.
- The configured `activation.workingDirectory` is one workspace root. Its immediate child directories are registered projects.
- `list_workspaces` exposes the configured workspace and its project IDs. `list_projects` remains available and returns an empty list when no root is configured.
- Tasks persist `workspace_id`. Existing rows and legacy MCP tools use `default` for backward compatibility.
- New workspace-scoped MCP tools start, list, and check conflicts within one workspace.
- Monitor task snapshots include the workspace ID.

Conversation sessions, messages, and agent runs retain their existing identifiers in this version. They are linked to workspace-scoped tasks through existing task and conversation identifiers; propagating a workspace column to every runtime record is deferred until multiple activation roots can run concurrently.

## Alternatives considered

- Infer workspace from project IDs: rejected because project names can overlap between roots.
- Break existing task tools by requiring a workspace argument: rejected because installed clients and persisted data require compatibility.
- Introduce multiple activation roots immediately: deferred because it would expand configuration, scheduling, and lifecycle semantics beyond the current single-root runtime.

## Consequences

Existing databases migrate in place by adding a non-null `workspace_id` with the `default` value. Workspace IDs are configuration data and must not contain paths or credentials. Operators can adopt scoped tools incrementally; legacy clients continue to operate in `default`.

## Verification

Unit and integration tests cover ID validation, workspace discovery, scoped lists and conflicts, and migration of a pre-workspace SQLite schema.
