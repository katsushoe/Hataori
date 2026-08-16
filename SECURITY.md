# Security

This document describes the public security model and safe operating requirements for Hataori.

## Supported Versions

| Version | Security support |
| :--- | :--- |
| `3.0.2.0` | Supported |
| Earlier versions | Not supported; upgrade before reporting a version-specific issue. |

## Reporting a Vulnerability

Do not disclose suspected vulnerabilities, tokens, host details, database contents, or reproduction secrets in a public issue. Contact the repository owner privately or use GitHub's private vulnerability reporting feature when it is enabled for the repository.

Include the affected Hataori version, operating system, impact, sanitized reproduction steps, and whether the issue is already being exploited. A fixed response time is not currently guaranteed; the maintainer should acknowledge receipt, assess severity, and coordinate disclosure before public discussion.

## Security Model

- Hataori is designed for a single Windows machine and binds MCP to a loopback IP address. `server:mcpHost` rejects non-loopback addresses.
- The MCP endpoint currently has no bearer-token authentication. Loopback binding and host filtering are the network boundary; do not expose the endpoint through a proxy or port forward without adding an independently reviewed authentication layer.
- The Control Pipe is local and created with current-user access restrictions for foreground management.
- The Windows Service runs as `LocalSystem`. Service installation, setup, start, stop, and removal require appropriate administrator rights.
- Task cancellation, failure, expiration, queue cancellation, conversation reset, service control, and Uninstall can change or remove state. Confirm the target IDs, service name, and installation root before execution.
- Agent execution inherits the configured working directory and Codex or Claude Code permission mode. Hataori does not replace each agent's sandbox, workspace trust, or approval policy.

## Secrets Handling

- Itoguruma issues `ITOGURUMA_AUTH_TOKEN`. Never commit, print, log, paste, or embed the token in documentation or MCP client settings.
- `hataori setup itoguruma` copies the token to `HATAORI_ITOGURUMA__AUTHENTICATIONTOKEN` for the interactive user without displaying the value.
- `hataori service setup` stores the token in `%INSTALL_ROOT%\config\hataori.service.json`; its ACL is restricted to `SYSTEM` and `Administrators`.
- The main `%INSTALL_ROOT%\config\hataori.json` must not contain the token. MSI packages contain no mutable configuration, secrets, user data, or logs.
- CLI configuration output redacts keys containing token, password, secret, credential, or key indicators.
- Logs and reports must contain sanitized errors only. Remove secrets and personal data before attaching diagnostic material to an issue.

## User Responsibilities

- Keep Windows, Hataori, .NET runtime components, Itoguruma, Codex CLI, and Claude Code updated.
- Restrict administrator access and protect `%INSTALL_ROOT%\config`, `data`, and `logs` with appropriate filesystem permissions.
- Keep `server:mcpHost` on loopback and keep `allowedHosts` limited to the intended local names.
- Review `activation:workingDirectory`, agent permission modes, hooks, and concurrent-run limits before enabling automatic activation.
- Back up `config` and `data` before Upgrade, recovery work, or manual deletion. Uninstall intentionally retains `config`, `logs`, and `data`.
- Verify MSI hashes through a trusted release channel before installation.
