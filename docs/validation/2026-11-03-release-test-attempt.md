# Release test run results

## Result

- Date: 2026-11-03 (JST)
- Environment: Windows, .NET SDK 10.0.401, solution target framework `net9.0`
- Final result: Passed; all 217 tests passed, 0 failed, 0 skipped.
- Final command: `dotnet test Hataori.sln --configuration Release --no-restore --maxcpucount:1 -p:BuildInParallel=false -p:TestTfmsInParallel=false --logger "console;verbosity=minimal"`
- Final execution: sandbox-external sequential Release run; exit code 0.
- Per-project totals: Core 17, Infrastructure 49, CLI 54, Server 92, Monitor 5; total 217.
- Initial command: `dotnet test Hataori.sln --configuration Release`
- Retry command: `dotnet test Hataori.sln --configuration Release --no-restore --maxcpucount:1 -p:BuildInParallel=false -p:TestTfmsInParallel=false`
- The initial parallel run started many `dotnet` processes and yielded no output or completion during observation; it was interrupted.
- Sequential Release results: Core 17 passed; Infrastructure 49 passed; Server 92 passed; Monitor 5 passed.
- CLI excluding the three named-pipe cases: 51 passed, 0 failed, 0 skipped.
- `RunAsync_ServerStatus_UsesNamedPipeAndReturnsJson` passed in an isolated run outside the sandbox (1 passed in 4.5 seconds).
- `RunAsync_AgentCancel_SendsRunIdAsControlPipeArgument` and `RunAsync_DoctorItoguruma_ReflectsLiveServerConnectionStateNotCliSideReconnect` each hung in the sandbox and were stopped by a 30-second blame-hang timeout.
- The sandboxed full CLI run also hung at `RunAsync_ServerStatus_UsesNamedPipeAndReturnsJson` after 33 tests had passed. The test host was stopped by the 45-second blame-hang timeout.
- Totals across non-overlapping completed results: 215 passed; 2 CLI cases remain unverified in the sandbox. The expected suite total is 217 (prior 212 plus 5 Monitor tests).
- Earlier sandbox attempts hung on named-pipe tests. The two remaining CLI named-pipe cases were then run together outside the sandbox and passed (2/2), followed by the successful full Solution run above.
- An earlier request to run the full Solution outside the sandbox was not executed because automatic approval review could not start while workspace credits were unavailable. A later approval succeeded, and the final full run completed.

## Follow-up

The full Solution Release test run is complete: 217 passed, 0 failed, 0 skipped. No test follow-up remains for the Monitor test addition.
