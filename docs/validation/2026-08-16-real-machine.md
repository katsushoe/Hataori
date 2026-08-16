# Hataori 2.3.1.3 実機検証結果

## 実施日

2026-08-16

## 合格

- Serverのコンソール起動とControl Pipe `status`
- Startup Recovery、Activation Gate、Database Maintenanceの起動順序
- MCP `server/discover`と`tools/list`
- Hookテンプレート診断とSessionStart Context出力
- CLI `stop`によるGraceful Shutdown
- Shutdown後のControl Pipe閉鎖
- 全114件の自動テスト
- 無効な起動設定でも未処理例外をOSへ漏らさず、利用者向け対処案を標準エラーへ表示して緊急ログへ例外詳細を保存
- Itoguruma 0.3.5へのBearer認証接続、受信、SQLite永続化後のACK
- ItogurumaへのReply送信と、同一冪等キーによる再送時のMessage ID一致
- Claude Code 2.1.220のstart・同一Sessionへのresume・Reply・ACK
- npm版Codex CLI 0.147.0のstart・同一Sessionへのresume・Reply・ACK

## 実機検証で検出・修正

- 新規DBでSchema初期化より先にRecovery／Maintenanceが走る競合を、初期化と復旧ゲートで修正した。
- コンソール起動時にWindows Event Log権限エラーが発生する問題を、Windows Service実行時だけService連携を登録することで修正した。
- Itoguruma再接続上限到達時にServerを停止せず、原因・対処をログへ記録して縮退運転を継続するよう修正した。
- Startup Recovery失敗を依存Workerへ例外として伝播させず、原因・対処をログへ記録して安全停止するよう修正した。
- Itoguruma 0.3.5の構造化結果が`data`ラッパーとcamelCaseプロパティを使うため受信値が欠落した問題を、互換デシリアライズで修正した。
- Windows既定コードページでClaude CodeのUTF-8 JSONが破損する問題を、Agentプロセスの標準入出力をUTF-8へ固定して修正した。
- Codex CLIで相互排他となる`--sandbox`と`--approve-for-me`を同時指定していた問題を修正し、自動承認時はCLI既定のworkspace-write sandboxを使用するようにした。

## 未完了

- Monitorは起動したがComputerUseで同名ウィンドウが重複検出され、一意に選択できなかったため画面内容は未確認。
- Windows Serviceは未インストールのため、Service実行・再起動試験は未実施。
- CLIとServerの別配置では相対Database Pathの基準が異なるため、doctorのSQLite診断には明示的な共通パス設定が必要。
