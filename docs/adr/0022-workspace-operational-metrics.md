# ADR 0022: Workspace-scoped operational metrics from persisted state

## Context

Hataori persisted lifecycle data but exposed no consolidated operational view. Operators had to inspect Task, Message, and Agent Run records separately.

## Decision

- Calculate metrics on demand from the existing SQLite source of truth without adding counters or a second persistence model.
- Scope every snapshot to one normalized Workspace ID.
- Report lifecycle counts, terminal success rates, reply retry totals, average durations, queue wait, and Agent-level Run breakdowns.
- Expose the same structured snapshot through read-only CLI `metrics show` and MCP `metrics_get` operations.
- Round duration and percentage values to three decimal places.

## Status

Accepted.

## Alternatives considered

- Persist incrementing counters: rejected because transaction failures could diverge from lifecycle records.
- Add a Prometheus endpoint immediately: deferred until remote scraping, authentication, and cardinality requirements are defined.

## Impact

Queries add read load proportional to retained rows. Existing retention settings define the observation window, so values describe currently retained data rather than lifetime totals.

## Security and operations

Metrics contain counts, timings, Workspace IDs, and Agent IDs but no message bodies, errors, credentials, or task summaries. Both interfaces are read-only.

## Verification and documentation

SQLite tests verify Workspace isolation, counts, rates, retries, and duration calculations. CLI behavior is documented in `COMMANDS.md` and `COMMANDS.ja.md`.
