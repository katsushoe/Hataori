# Monitor WinForms regression verification

## Result

- Date: 2026-11-01 (JST)
- Environment: Windows, .NET SDK 10.0.401, target framework `net9.0-windows`
- Result: 5 tests passed, 0 failed.
- Command: `dotnet test tests\Hataori.Monitor.Tests\Hataori.Monitor.Tests.csproj --no-restore --disable-build-servers -m:1 --logger "console;verbosity=normal"`
- The test project builds successfully and is registered in `Hataori.sln`.

## Covered behavior

- Snapshot refresh binds Task, Agent, Session, and Agent Run grids and updates queue, server, and connection status values. The form is rendered through WinForms `DrawToBitmap` as part of this check.
- Task cancellation is enabled for an active Task and disabled for a completed Task.
- Agent Run cancellation is enabled for queued, starting, and running states and disabled for a completed Run.
- Confirmed cancellation sends the selected identifier and refreshes the displayed snapshot.
- Declining confirmation sends no cancellation request. A rejected Run cancellation response is reported through the warning-dialog boundary.
- Tests instantiate real WinForms controls on an STA thread and use a fake Control Pipe client. No Hataori service, persisted data, or modal dialog is involved.

## Repeat

Run the command above on Windows. The assertions and rendered-control path are part of the test suite, so no manual setup or test data is required.

## Scope

This is repeatable Windows UI-control and rendering evidence using controlled responses. It does not replace a live-service acceptance run against an installed Hataori instance.
