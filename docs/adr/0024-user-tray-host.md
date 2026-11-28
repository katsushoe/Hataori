# 0024: Run Hataori in the Windows user session

## Status

Accepted for 3.2.0.0. This replaces the machine service packaging in ADR 0001 and the elevation requirements in ADR 0023.

## Context and decision

The legacy MSI runs Server as LocalSystem. Its current-user-only control pipe rejects Monitor running as the signed-in user, and the shared log directory cannot be written by that user.

The user MSI registers Monitor in that user's Windows Run key. At logon, Monitor begins in the notification area and starts Server as a hidden child under the same Windows account. Monitor and Server use the existing current-user-only pipe. Closing the window normally hides Monitor. Exiting Monitor closes the Server's redirected input and waits for a graceful shutdown. The input also closes if Windows terminates Monitor. Logoff ends Hataori's availability. Normal use and updates do not require elevation.

The user MSI has a separate upgrade identity and refuses installation while a legacy Windows service is registered. The administrator performs the one-time legacy removal while retaining the existing installation root, configuration, and database. The migration checks the backup before removal, then grants the chosen Windows user access to the installation root. The legacy token file remains available to administrators for rollback; the new runtime reads the protected `config/hataori.user.json` copy. Monitor diagnostic logs are written to the current user's LocalAppData.

From 3.2.1.0, the standard local Itoguruma endpoint uses the current user's Windows Credential Manager entry when present. This keeps the live credential in its existing store and supersedes stale legacy values for that endpoint. Explicit configuration remains available for other loopback endpoints.

## Alternatives and consequences

Keeping the service and adding a second pipe permission would preserve availability before logon, but retain the two account boundaries and administrative updates. Using a scheduled task under a separate account would still require coordinating file permissions and the control pipe. The user installation is intended for a single Windows user per shared installation root.

## Verification

Verify the user MSI scope, logon entry, startup and exit, same-user pipe, retained configuration, SQLite integrity, MCP and Itoguruma connectivity. Keep the previous machine MSI and the protected migration backup until acceptance is complete. See the installation guide for migration and rollback.
