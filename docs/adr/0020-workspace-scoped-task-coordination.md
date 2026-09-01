# ADR 0020: Workspace-scoped task coordination

## Status

Accepted

## Context

Hataori previously treated every task as part of one implicit global workspace. Project discovery already used one configured projects root, but clients could not discover that boundary or isolate task and conflict queries when several workspaces are coordinated.

## Decision

- A workspace is identified by a normalized `^[a-z][a-z0-9]*$` ID.
- `activation.workspaces` configures multiple workspace roots. Each entry has a workspace ID and working directory whose immediate child directories are registered projects.
- Legacy `activation.workspaceId` and `activation.workingDirectory` remain a compatible single-root representation when `workspaces` is absent.
- Workspace IDs, root paths, and project IDs must be unique across configured roots. Duplicate project IDs are rejected because one Itoguruma Project Inbox ID cannot identify two roots.
- `list_workspaces` exposes every configured workspace and its project IDs. `list_projects` returns the distinct union and remains empty when no root is configured.
- Tasks persist `workspace_id`. Existing rows and legacy MCP tools use `default` for backward compatibility.
- Conversation sessions, incoming messages, agent runs, and Monitor snapshots persist or expose the same workspace ID.
- Session identity is `(workspace_id, conversation_id, agent_id)`. Conversation mutexes also include the workspace ID, so equal conversation IDs in different workspaces do not block each other.
- CLI list/get operations for runs, sessions, and queued messages accept an optional `--workspace` filter.
- New workspace-scoped MCP tools start, list, and check conflicts within one workspace.
- Monitor task snapshots include the workspace ID.

Run IDs and message IDs remain globally unique. Itoguruma polling resolves each received project through its configured workspace root and persists that workspace ID before queue activation.

## Alternatives considered

- Infer workspace from project IDs: rejected because project names can overlap between roots.
- Break existing task tools by requiring a workspace argument: rejected because installed clients and persisted data require compatibility.
- Use a separate lane pool per workspace: rejected because queued messages already carry their workspace and working directory, while concurrency limits are intentionally global per provider.

## Consequences

Existing databases migrate in place by assigning `default` to prior Task, Session, Message, and Agent Run rows. The Session table is rebuilt transactionally to extend its primary key without losing data. Workspace IDs are configuration data and must not contain paths or credentials. Operators can adopt multiple roots and scoped tools incrementally; legacy single-root configuration and clients continue to operate in `default`.

## Verification

Unit and integration tests cover ID validation, multi-root binding and validation, duplicate rejection, workspace discovery, multi-root Itoguruma ingestion, scoped lists and conflicts, runtime propagation, same-conversation isolation across workspaces, and migration of pre-workspace SQLite schemas.
