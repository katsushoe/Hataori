# Monitor live-service acceptance

## Status

- Date: 2026-11-03 (JST)
- Hataori Service preflight: `Running`, startup type `Automatic`.
- Installed Monitor executable: `%INSTALL_ROOT%\bin\monitor\Hataori.Monitor.exe` exists.
- Live UI verification: Not started; explicit user authorization for ComputerUse is pending.
- Cancellation scope: Use only a uniquely identified disposable test Task and a disposable Agent Run. Do not cancel existing user work.

## Acceptance procedure

1. Launch the installed Monitor executable and confirm the Hataori Service, connection, queue, Task, and Agent Run state are displayed without an error dialog.
2. Wait for one automatic refresh interval, then request a manual refresh. Confirm the displayed snapshot refreshes and the Service remains running.
3. Create a uniquely named disposable active Task in the Hataori data store. In Monitor, select only that Task, request cancellation, confirm the dialog, and verify its state becomes `cancelled` after refresh.
4. Use only a disposable Agent Run created for this check. In Monitor, request cancellation, confirm the dialog, and verify its state becomes `cancelled` after refresh. Never select an existing user Run.
5. Record the observed rows, refresh behavior, both final states, and any errors below. If a disposable Agent Run cannot be created without starting user work, stop and leave that subcheck pending.

## Result

Pending explicit user authorization for the UI operations and creation/cancellation of disposable test records. No UI was opened and no test Task or Agent Run was created during preflight.
