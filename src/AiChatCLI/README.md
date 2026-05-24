# AiChatCLI

`AiChatCLI` は、CLI から OpenAI モデルと対話できる .NET 9 コンソールアプリです。

関連ドキュメント:

- リポジトリ全体の概要: [`../../README.md`](../../README.md)
- 開発支援 workflow: [`../../devtools/workflow/README.md`](../../devtools/workflow/README.md)

## クイックスタート

1. `appsettings.local.example.json` を `appsettings.local.json` にコピーします
2. `appsettings.local.json` の `OpenAI:ApiKey` に API キーを設定します
3. Tavily 検索を使う場合は `Tavily:ApiKey` も設定します
4. ビルドして実行します

```bash
dotnet build
dotnet run --project src/AiChatCLI/AiChatCLI.csproj
```

テスト:

```bash
dotnet test AiChatCLI.sln
```

## ドキュメント構成

README 本体は入口に絞り、詳細は目的別ドキュメントへ分割しています。

- [`docs/usage.md`](docs/usage.md): コマンド利用者向けの基本操作、slash command、template、agent、thread
- [`docs/configuration.md`](docs/configuration.md): `appsettings.local.json`、環境変数、`Tools:Enabled`、ログ保存先、各種設定ファイル
- [`docs/development.md`](docs/development.md): 内部構成、責務分割、拡張ポイント、テスト方針
- [`docs/reference.md`](docs/reference.md): コマンド一覧、agent-callable tool 一覧、ログ出力先、主要設定キーの早見表

## まずはこれだけ

起動すると現在のモデル名とエージェント名を含むプロンプトが表示され、通常入力は AI に送られます。

```text
AI Chat CLI (model: gpt-4o-mini, agent: default, exitで終了)
defaultエージェント> こんにちは
defaultエージェント> こんにちは！何かお手伝いできることはありますか？
defaultエージェント> exit
```

- `exit` で終了します
- `/help` でコマンド一覧を表示できます
- `/status` で現在のモデル、agent、tool、thread、memory の状態を確認できます
- `ChatHistory:Enabled` が有効なら、起動時に空の current thread が自動作成されます
- `Ctrl+V` または `Shift+Insert` で複数行テキストも貼り付けできます

## 主な機能

- slash command による agent / prompt / thread の操作
- `memory` による短い長期メモリ保存
- `sub_agent` による独立コンテキストのサブエージェント実行
- approval-gated な `command` によるローカルコマンド実行
- `read_file` によるローカルファイルの読み取り
- `search` による Tavily Web 検索
- `logs/threads/*.jsonl` を正本にした thread replay と会話継続

## アーキテクチャの入口

内部構成の全体像だけ先に押さえるなら次の通りです。

- `Program.cs` は `AppPaths.Discover(...)`、`AppComposition.Create(...)`、`ThreadSessionManager.Initialize()`、`ChatLoop.RunAsync(...)` を呼ぶ薄い entry point です
- `Bootstrap/` は config / path discovery と manual wiring を担います
- `Conversation/` は REPL と 1 ターン処理、モデル送信、live / persisted message 変換を担います
- `Agents/` は agent 定義、tool catalog、factory、sub-agent 実行を担います
- `Threads/` は current thread lifecycle、append-only event 記録、thread replay を担います
- `Commands/` は slash command 実装、`Prompts/` は template 管理、`Ui/` は interactive prompt を担います

詳細は [`docs/development.md`](docs/development.md) を参照してください。

## ライブラリとプロバイダ

- ライブラリ: [AutoGen](https://github.com/microsoft/autogen/tree/main/dotnet)
- プロバイダ: OpenAI
