# Release手順

[English](RELEASE.md) | [日本語](RELEASE.ja.md)

この文書は、HataoriのReleaseを作成・公開する手順（Versionの決定、build、実機検証、MSIパッケージング、GitHub Release公開）の正本です。

## Releaseを作成するタイミング

インストール済みマシンへ影響する変更（Server、CLI、MCPツール、Monitor、Service挙動、installer）を行った後にReleaseを作成します。文書のみの変更は、MCPツール件数のようなVersion管理対象の成果物に影響しない限り、新規Versionを必要としません。

## 1. Versionを決定する

製品Versionは`Directory.Build.props`の`<Version>`にあります。Windows InstallerのMajor Upgrade比較は**4番目**のVersion要素を見ないため、Upgrade判定に有効なのは3番目の要素です。この規則の実機での根拠は`docs/validation/2026-08-17-installer.md`を参照してください。

- Major Upgradeで検知させたい変更（ほぼ全てのRelease）は**3番目**の要素を上げます: `3.0.2.0` → `3.0.3.0`。
- 4番目の要素だけではUpgradeが検知されないため、それだけに頼らないでください。

`Directory.Build.props`を更新し、対応する変更と同じcommit、または独立した`Bump version to X.Y.Z.W`commitとして記録します。

## 2. Buildとテスト

```powershell
dotnet build Hataori.sln --configuration Release
dotnet test Hataori.sln --configuration Release --no-build
```

両方とも警告0、エラー0、全テスト合格で完了してから次へ進みます。

## 3. MSIをbuildする

```powershell
./scripts/Build-Installer.ps1
```

Server、CLI、Monitorを自己完結型`win-x64` single-fileとしてpublishし、WiX installerプロジェクトをbuildし、生成されたMSIのpathとSHA-256 hashを表示します。MSIは`artifacts/installer/Hataori-<version>-x64.msi`へ出力され、Git管理対象外です。

## 4. 実機で検証する

`docs/installation.md`の標準配置に基づき、管理者権限でのInstallまたは既存Installへの Major Upgradeを実施します。最低限、次を確認します。

- WiX ICE検証とMSI build自体が警告0、エラー0で完了していること。
- `msiexec /i`（または既存Installへの Major Upgrade）がExit Code 0で完了すること。
- Hataori Serviceが再起動し、`Running`になること。
- `hataori version`が新しいVersionを返すこと。
- `hataori mcp status`が`connected: true`と想定通りの`tool_count`を返すこと。
- 変更に関連する`hataori doctor`のチェックが合格すること（各チェックの検証内容は`SECURITY.md`と`docs/installation.md`を参照。`server`のようにServiceと同一アカウントが必要なチェックは、そのアカウント以外では`skipped: true`になるのが正しい挙動）。

利用可能なマシンがMaintainerの本番Installしかない場合、Uninstall検証は延期してかまいませんが、黙って省略せず延期した旨を明記してください。

結果は`docs/validation/<date>-installer-<version>.md`へ記録します。同ディレクトリの既存fileをテンプレートとし、成果物名、SHA-256、WiX Version、合格項目、変更内容、既知の未解消事項、意図的に未実施とした項目を含めます。

## 5. Tagを付けてpushする

```powershell
git tag v<version>
git push origin v<version>
```

`<version>`は`Directory.Build.props`の値をそのまま使用します（例: `v3.0.3.0`）。

## 6. GitHub Releaseを公開する

```powershell
gh release create v<version> "artifacts/installer/Hataori-<version>-x64.msi" `
  --title "Hataori <version>" `
  --notes-file <release-note-fileへのpath>
```

Release noteには次を含めます（プロジェクトの作業言語で記述）。

- 変更内容の要約（commit logの羅列ではなく機能単位の要約）。
- 実施した検証（build・test結果、MSI build結果、実機確認項目）。
- MSIのSHA-256 hash（利用者がダウンロードを検証できるように）。
- Upgrade手順（`msiexec.exe /i Hataori-<version>-x64.msi INSTALL_ROOT="..."`）と、`config`・`logs`・`data`がUpgrade後も保持される旨。

手順4の実機検証記録がRelease noteの一次情報源です。Release noteはその要約であり、別途調査するものではありません。

## 7. プロジェクト文書を更新する

- `PROGRESS.md`: Releaseと検証結果を反映した日付列を、既存の更新周期に従って追加します。
- `TODO.md`: Releaseで完了した項目にチェックを付けます。
- `DOCUMENTS.md`: Releaseで文書を追加・削除した場合、同じ作業内で一覧を整合させます（この手順を省略すると何が起きるかは、2026-08-18に発覚した`COMMANDS.md`／`CONFIG.md`／`PACKAGES.md`／`SECURITY.md`の記載漏れが実例です）。

## Rollback

対象マシンでMajor Upgradeが失敗した場合、Windows Installerは非ゼロExit Codeで自動的にrollbackします。手動rollback用のscriptはありません。GitHubから不良なReleaseを取り除く場合は、Repository所有者の確認を得たうえで`gh release delete v<version>`と`git push origin :refs/tags/v<version>`を使用してください。これは破壊的かつ外部から見える操作のため、明示確認なしに自動実行してはなりません。
