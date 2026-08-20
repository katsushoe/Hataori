# Itoguruma Authentication Setup

[English](setup-itoguruma.md) | [日本語](setup-itoguruma.ja.md)

After installing or repairing Itoguruma, run:

```powershell
hataori setup itoguruma
```

The command transfers the authentication token issued to the user environment without displaying it and tests the connection. Restart Hataori Server after success.

To defer the connection test:

```powershell
hataori setup itoguruma --skip-test
hataori itoguruma test
```

If no token is found, install or repair Itoguruma and retry. Never place the secret value in settings, logs, Git, or chat.

This command configures the interactive user environment. For the Windows Service account, use the dedicated `hataori service setup` flow described in [Installation](installation.md).
