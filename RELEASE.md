# Release Process

[English](RELEASE.md) | [日本語](RELEASE.ja.md)

This document is the source of truth for cutting and publishing a Hataori release: version bump, build, real-machine validation, MSI packaging, and the GitHub Release.

## When to Cut a Release

Cut a release after a change that ships to installed machines (server, CLI, MCP tools, Monitor, service behavior, installer). Documentation-only changes do not require a new version unless the change also touches a versioned artifact (for example the MCP tool count).

## 1. Choose the Version

The product version lives in `Directory.Build.props` (`<Version>`). Windows Installer major-upgrade comparison does **not** consider the fourth version component, so the upgrade-relevant component is the third one. See `docs/validation/2026-08-17-installer.md` for the real-machine finding behind this rule.

- Bump the **third** component for any change you want a Major Upgrade to detect (almost all releases): `3.0.2.0` -> `3.0.3.0`.
- Do not rely on the fourth component alone to trigger an upgrade.

Update `Directory.Build.props` and commit it with the change it accompanies, or as its own `Bump version to X.Y.Z.W` commit.

## 2. Build and Test

```powershell
dotnet build Hataori.sln --configuration Release
dotnet test Hataori.sln --configuration Release --no-build
```

Both must complete with 0 warnings, 0 errors, and all tests passing before continuing.

## 3. Build the MSI

```powershell
./scripts/Build-Installer.ps1
```

This publishes the server, CLI, and Monitor as self-contained `win-x64` single-file executables, builds the WiX installer project, and prints the resulting MSI path and SHA-256 hash. The MSI is written to `artifacts/installer/Hataori-<version>-x64.msi` and is not tracked by Git.

## 4. Validate on a Real Machine

Perform an administrator-elevated install or Major Upgrade against a real installation (see `docs/installation.md` for the standard layout). At minimum, confirm:

- WiX ICE validation and the MSI build itself completed with 0 warnings, 0 errors.
- `msiexec /i` (or Major Upgrade over an existing install) exits 0.
- The Hataori service restarts and reaches `Running`.
- `hataori version` reports the new version.
- `hataori mcp status` reports `connected: true` and the expected `tool_count`.
- `hataori doctor` checks relevant to the change pass (see `SECURITY.md` and `docs/installation.md` for what each check verifies; a check that requires the same account as the service, such as `server`, is expected to report `skipped: true` outside that account).

Uninstall validation may be deferred when the only available machine is the maintainer's production install; note the deferral explicitly rather than silently skipping it.

Record the result in `docs/validation/<date>-installer-<version>.md`, following the existing files in that directory as the template: artifact name, SHA-256, WiX version, what passed, what changed, known unresolved items, and anything intentionally not executed.

## 5. Tag and Push

```powershell
git tag v<version>
git push origin v<version>
```

Use the exact `<version>` from `Directory.Build.props` (for example `v3.0.3.0`).

## 6. Publish the GitHub Release

```powershell
gh release create v<version> "artifacts/installer/Hataori-<version>-x64.msi" `
  --title "Hataori <version>" `
  --notes-file <path-to-release-notes>
```

Release notes should summarize, in the repository's working language:

- What changed (functional summary, not a commit log dump).
- Validation performed (build/test result, MSI build result, real-machine checks).
- The MSI SHA-256 hash, so users can verify the download.
- Upgrade instructions (`msiexec.exe /i Hataori-<version>-x64.msi INSTALL_ROOT="..."`) and a note that `config`, `logs`, and `data` are preserved across Upgrade.

The validation document from step 4 is the primary source for this content; the release notes are a condensed, user-facing version of it, not a separate investigation.

## 7. Update Project Documents

- `PROGRESS.md`: add a dated column reflecting the release and any validation outcome, per its own update cadence.
- `TODO.md`: check off any item the release completed.
- `DOCUMENTS.md`: if the release added or removed a tracked document, reconcile the listing in the same task (see the 2026-08-18 correction for the released-but-undocumented `COMMANDS.md`/`CONFIG.md`/`PACKAGES.md`/`SECURITY.md` set as an example of what happens when this step is skipped).

## Rollback

If a Major Upgrade fails on a target machine, Windows Installer rolls back automatically on a non-zero exit; no manual rollback script exists. To remove a bad release from GitHub, use `gh release delete v<version>` and `git push origin :refs/tags/v<version>` after confirming with the repository owner — this is a destructive, externally visible action and must not be automated without explicit confirmation.
