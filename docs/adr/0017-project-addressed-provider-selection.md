# ADR 0017: Project-addressed provider selection

## Status

Accepted

## Context

Itoguruma messages are addressed to projects, not agent implementations. Hataori must choose the execution provider after receiving a project-addressed message. The sender provider is trusted only when Itoguruma attaches it from the registered sender identity. A project may be available to more than one configured provider, so fallback order must be deterministic and operator-controlled.

## Decision

- Hataori leases messages only for its configured project ID (`itoguruma.agentId`). It no longer registers or monitors `codex` and `claude-code` as message recipients.
- The Itoguruma message adapter accepts an optional `provider` response field. Absence remains compatible with Itoguruma versions predating automatic provider attribution.
- Hataori resolves the destination project as a direct child directory of `activation.workingDirectory`, using a case-insensitive project ID comparison and rejecting path-shaped IDs.
- If the source provider has a configured driver, it is selected first. Otherwise Hataori selects the first available driver in `activation.providerPriority`.
- The selected provider and resolved project directory are persisted with the local queue item before the Itoguruma message is acknowledged. Activation always starts or resumes in that persisted directory.
- Provider priority is readable and writable through the configuration file, CLI, and MCP Tools. All interfaces use `ProviderPriorityService` as the shared operation.
- Activation-disabled instances do not lease project messages because they cannot safely persist a runnable provider and directory decision.

## Alternatives

- Caller-supplied provider: rejected because the caller cannot prove that the target project exists for that provider and it weakens the Itoguruma trust boundary.
- Payload-only provider metadata: rejected because it does not provide a first-class, server-attributed message contract.
- Always use a single configured provider: rejected because it prevents same-provider routing and automatic fallback.

## Impact

The local message database gains `working_directory`. Existing rows receive an empty value during migration; new project-addressed messages always persist an absolute resolved directory. `itoguruma.monitoredAgentIds` is removed from the active configuration contract.

## Security conditions

Project IDs cannot be absolute paths or contain directory separators. Resolution is limited to immediate children of the configured projects root. Provider metadata is treated as a preference, never as authority to execute an unconfigured driver.

## Operations

Operators configure a non-empty, duplicate-free provider order. Each entry must have a matching `maxConcurrentRuns` lane when Activation is enabled. Configuration file reload updates message selection without restarting the service.

## Implementation and verification

Application selection logic, queue persistence, configuration validation, CLI and MCP operations, migration, unit tests, and user documentation must remain aligned with this ADR.
