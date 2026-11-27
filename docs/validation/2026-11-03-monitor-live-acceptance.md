# Monitor live-service acceptance

## Status

- Date: 2026-11-03 (JST)
- Hataori Service preflight: `Running`, startup type `Automatic`.
- Installed Monitor executable: `%INSTALL_ROOT%\bin\monitor\Hataori.Monitor.exe` exists.
- Live UI verification: Performed on 2026-11-27 (JST) with the user's explicit ComputerUse authorization (Hataori Monitor window only).
- Cancellation scope: Use only a uniquely identified disposable test Task and a disposable Agent Run. Do not cancel existing user work.

## Acceptance procedure

1. Launch the installed Monitor executable and confirm the Hataori Service, connection, queue, Task, and Agent Run state are displayed without an error dialog.
2. Wait for one automatic refresh interval, then request a manual refresh. Confirm the displayed snapshot refreshes and the Service remains running.
3. Create a uniquely named disposable active Task in the Hataori data store. In Monitor, select only that Task, request cancellation, confirm the dialog, and verify its state becomes `cancelled` after refresh.
4. Use only a disposable Agent Run created for this check. In Monitor, request cancellation, confirm the dialog, and verify its state becomes `cancelled` after refresh. Never select an existing user Run.
5. Record the observed rows, refresh behavior, both final states, and any errors below. If a disposable Agent Run cannot be created without starting user work, stop and leave that subcheck pending.

## Result

### 2026-11-27 (Hataori 3.2.8.0, user-session edition at `E:\Hataori`)

1. Display: Monitor 3.2.8.0 opened without an error dialog. Status line `接続中: hataori-control / 14:58:15`; the Task list showed active and completed Tasks. The status tab showed Hataori Server `running`, Itoguruma `connected`, MCP `running`, SQLite `connected`, queue `0`. Pass.
2. Refresh: Manual **更新** changed the status time to 14:58:28 and loaded the list; automatic refresh followed at 14:58:33, 14:58:42 and 14:58:54 while Server kept running. Pass.
3. Task cancellation: Created the disposable active Task `monitor-acceptance-disposable-20261127` (`[TEST] Monitor acceptance disposable task (cancel me)`, agent `claude-code-acceptance`) through Hataori MCP. Selected only that row, pressed **Taskをキャンセル**, confirmed the dialog that named exactly that Task ID, and the row showed `cancelled` after the next refresh. Hataori MCP `task_get` returned `Cancelled` with result `Cancelled from Monitor.`. Pass.
4. Agent Run cancellation: Not performed. `activation.enabled` is `false` in the installed configuration and the Agent Run list is empty, so a disposable Agent Run cannot be created without enabling activation, which could start user work. Remains pending per step 5.
5. Observation: After each automatic refresh (every 3 seconds) the Task grid rebinds, the selection returns to the first row, and **Taskをキャンセル** is shown disabled until a row is clicked again. Cancellation worked when the row was selected and the button pressed before the next refresh. Recorded as a usability issue to fix. Fixed in 3.2.9.0: the selected row (identified by Task ID / Run ID) and the scroll position are kept across refreshes, and the cancel buttons are re-evaluated after each refresh. Live check on 3.2.9.0: the fourth row (an active Task, not cancelled) stayed selected with **Taskをキャンセル** enabled across automatic refreshes (22:19:22 to 22:19:28).
