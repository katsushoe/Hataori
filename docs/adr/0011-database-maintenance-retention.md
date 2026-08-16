# ADR 0011: Server管理のDatabase Maintenance

## Status

Accepted

## Context

期限切れTask、終了済みAgent Run、処理済みMessage、SQLite空き領域を継続的に管理する必要がある。CLIやGUIがDBを直接更新するとServerを正本とする責務境界を破る。

## Decision

ServerのBackgroundServiceが起動時と設定周期ごとにMaintenanceを実行する。stale active Taskは`expired`へ状態遷移し履歴を追加する。保持期限を過ぎた終了済みTask、Agent Run、Messageをトランザクション内で削除し、その後任意でVACUUMする。ログ保持は既存FileLoggerの`retentionDays`を正とする。

## Alternatives

- CLIから直接purgeする案は、DB更新経路が分散するため不採用とした。
- 終了データを無期限保持する案は、DB肥大化を制御できないため不採用とした。

## Impact

`databaseMaintenance`設定が追加される。既定は24時間周期、stale Task 24時間、Task 90日、Agent RunとMessage 30日保持、VACUUM有効である。

## Security

SQLは固定文とパラメータで構成する。active Task、進行中Run、未処理Messageはpurgeしない。Messageは外部キー制約に従いQueue行を先に削除する。

## Operations

各保持期間と周期は設定で変更でき、範囲外はServer起動時に拒否する。実行結果は件数のみ構造化ログへ記録する。

## Implementation and verification

InfrastructureにMaintenance本体、Serverに設定・Validator・Workerを配置する。テストでは古い終了データの削除、stale Taskの状態遷移、対象外データ保持を検証する。
