# Itoguruma認証セットアップ

[English](setup-itoguruma.md) | [日本語](setup-itoguruma.ja.md)

Itogurumaをインストールまたは修復した後、次のコマンドを実行します。

```powershell
hataori setup itoguruma
```

このコマンドは、Itogurumaインストーラーがユーザー環境へ発行した認証トークンを値を表示せずHataoriへ連携し、接続試験を実行します。成功後はHataori Serverを再起動してください。

接続試験を後で行う場合は次を使用します。

```powershell
hataori setup itoguruma --skip-test
hataori itoguruma test
```

トークンが見つからない場合は、Itogurumaをインストールまたは修復してから再実行してください。秘密値を設定ファイル、ログ、Git、チャットへ記載しないでください。

現在のコマンドは対話ユーザーの環境を設定します。Windows Service用認証の設定は、Service導入手順が提供する専用設定方法を使用してください。
