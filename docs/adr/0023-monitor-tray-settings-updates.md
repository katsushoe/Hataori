# 0023: Monitor tray lifecycle, user settings, and startup updates

## Status

Accepted for 3.1.23.0. Runtime installation acceptance is required before publication.

## Context

Monitor is an interactive Windows Forms client. Server already runs independently as a Windows service. Users need persistent tray access, a graphical settings dialog, and automatic application updates at Monitor startup.

## Decision

Monitor owns the tray icon and settings UI. Closing hides the window by default, minimizing always hides it, and the tray menu opens Monitor, opens Settings, or exits Monitor. Windows shutdown and explicit Exit terminate normally. A user/session/installation/pipe-scoped mutex prevents duplicate instances; another launch signals the existing window to reopen.

Monitor settings are stored atomically in `%LOCALAPPDATA%/Hataori/Monitor/settings.json`, independently of administrator-owned Server configuration and credentials. The initial language comes from `hataori.json`; a saved Monitor language takes precedence. Settings control close-to-tray, minimized startup, startup auto-update, refresh interval, and display language. Language and refresh interval apply immediately; startup preferences apply on the next launch. This adds no new Server business operations, CLI commands, or MCP tools.

At startup, when enabled, Monitor asynchronously queries the official GitHub latest-release endpoint embedded in `monitor-update.json`. The manual Update application button uses the same path. Only newer stable four-part versions with one exactly named x64 MSI, the official repository download URL, a SHA-256 digest, and a positive size up to 512 MiB are accepted. Metadata is limited to 1 MiB. Asset redirects are explicitly restricted to HTTPS GitHub and GitHub release-asset hosts, at most six requests. The HTTP timeout is two minutes per request and the complete check/download deadline is ten minutes. Authentication is not read from other applications; private or unavailable releases produce a recoverable update failure.

The MSI is written to a unique user update directory and verified before any execution. A copy of the published Monitor executable acts as the update coordinator outside the MSI-managed directory. It waits up to 60 seconds for the original Monitor to exit, rechecks the MSI digest while holding a read-only file lock, then invokes Windows Installer with normal Windows elevation, passive UI, a log, no automatic reboot, and the existing installation root. MSI installation is permitted only from the installed `bin/monitor` layout with an existing configuration. The coordinator waits up to 20 minutes overall and restarts Monitor after installer completion or a cancelled elevation request; restart skips one update check to prevent a failure loop. Exit code 3010 requests a Windows restart. An installer still running after timeout is not killed and Monitor is not restarted over it.

## Alternatives

- Running tray UI in Server was rejected because a service cannot provide the interactive user's desktop UI.
- Replacing binaries directly was rejected because it bypasses MSI ownership, service lifecycle, and rollback.
- Opening a release web page alone was rejected because it does not implement automatic installation.

## Impact and security conditions

Only Monitor and its dependencies change; MSI remains the distribution format for Server, CLI, and Monitor. Updates can restart the service and interrupt agent operations, so Windows elevation remains visible. The package digest is authenticated through the official HTTPS release API; it is not a replacement for release signing. Existing signing and explicit publication approval requirements remain in force. No credentials or Server configuration are exposed in the settings dialog. No Windows logon registration is added. Update caches and installer logs remain under the user's Monitor directory for diagnosis and can be removed when no update is running.

## Implementation, tests, and documentation

Monitor lifecycle, settings round-trip and corruption handling, metadata rejection, download integrity and redirects, and installer arguments are covered by Windows tests. Settings are rendered in both supported languages. Live UAC cancellation, MSI upgrade/rollback, custom installation path retention, service recovery, and post-upgrade restart require installation acceptance. `CONFIG.md` and `CONFIG.ja.md` document settings and failures; README documents the tray entry points.

## References

- [Microsoft: NotifyIcon component](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-windows-forms)
- [GitHub: release assets, including digest](https://docs.github.com/en/rest/releases/assets)
