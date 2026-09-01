# DOCUMENTS.md Version
2026.08.20

[English](DOCUMENTS.md) | [日本語](DOCUMENTS.ja.md)

This document is the source of truth for the Hataori repository layout and canonical document locations.

## Placement Policy

Public documentation, architecture decisions, progress, and plans are tracked in Git for this project. Environment-specific settings, credentials, raw logs, and temporary notes are not tracked.

## Project Directories

| Path | Git | Purpose |
| :--- | :--- | :--- |
| `.` | Yes | Solution, entry documents, and project-management sources. |
| `src/` | Yes | Core, Application, Infrastructure, Server, CLI, and Monitor implementations. |
| `tests/` | Yes | Automated tests. |
| `installer/` | Yes | WiX x64 MSI definition. |
| `scripts/` | Yes | Publish, MSI build, and documentation-link validation scripts. |
| `docs/adr/` | Yes | Architecture decision records. |
| `docs/validation/` | Yes | Sanitized real-machine validation records. |
| `progress/` | Yes | Progress visualization. |
| `artifacts/`, `**/bin/`, `**/obj/` | No | Build, publish, and installer outputs. |

## Canonical Documents

| Document | Canonical path | Git | Purpose |
| :--- | :--- | :--- | :--- |
| README | `README.md`, `README.ja.md` | Yes | Public project entry in English and Japanese. |
| Configuration | `CONFIG.md`, `CONFIG.ja.md` | Yes | Complete settings reference. |
| Commands | `COMMANDS.md`, `COMMANDS.ja.md` | Yes | CLI reference, results, exit codes, and safety notes. |
| MCP setup | `MCP_SETUP.md`, `MCP_SETUP.ja.md` | Yes | Codex and Claude Code MCP registration. |
| Installation | `docs/installation.md`, `docs/installation.ja.md` | Yes | MSI install, upgrade, uninstall, and standard layout. |
| Itoguruma setup | `docs/setup-itoguruma.md`, `docs/setup-itoguruma.ja.md` | Yes | Safe authentication-token transfer and connection test. |
| Release process | `RELEASE.md`, `RELEASE.ja.md` | Yes | Versioning, build, validation, MSI, and GitHub Release procedure. |
| Packages | `PACKAGES.md`, `PACKAGES.ja.md` | Yes | NuGet dependency inventory. |
| Security | `SECURITY.md`, `SECURITY.ja.md` | Yes | Security policy and vulnerability reporting. |
| Document index | `DOCUMENTS.md`, `DOCUMENTS.ja.md` | Yes | Canonical path inventory. |
| Progress | `PROGRESS.md` | Yes | Feature progress, completed work, and remaining work. |
| TODO | `TODO.md` | Yes | Short-term tasks and implementation plan. |
| Progress chart | `progress/progress-chart.svg` | Yes | Progress visualization. |
| Architecture decisions | `docs/adr/*.md` | Yes | Design decisions and consequences. |
| Validation records | `docs/validation/*.md` | Yes | Sanitized pass/fail evidence and deferred checks. |

## Untracked Information

Do not place secrets, environment-specific settings, raw logs, customer information, or temporary notes in tracked documents. Use an ignored local location and sanitize any evidence before publication.
