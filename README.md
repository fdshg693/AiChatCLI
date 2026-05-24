# AiChatCLI

CLI から AI と対話する .NET アプリと、その開発を支援する Cursor CLI 向け workflow 群を同居させたリポジトリです。

## リポジトリ構成

- アプリ本体: `src/AiChatCLI/`
- アプリ向け focused test: `tests/AiChatCLI.Tests/`
- 開発支援 workflow: `devtools/workflow/`

## ドキュメント

- アプリ本体の使い方と機能詳細: [`src/AiChatCLI/README.md`](src/AiChatCLI/README.md)
- workflow のセットアップと実行方法: [`devtools/workflow/README.md`](devtools/workflow/README.md)
- AI 向けの恒久ルール: `.cursor/rules/always/*.mdc`

## アプリ本体の主な機能

- slash command による agent / prompt / thread の操作
- `memory.json` を使った Function Calling ベースの長期メモリ
- `sub_agent` ツールによる、独立した会話コンテキストのサブエージェント実行
- 承認付き `command` ツールによるローカルコマンド実行
- `logs/threads/*.jsonl` を正本にした thread 履歴復元と会話継続
- 既存の人間向け `chat_*.txt` テキストログ出力

## クイックスタート

1. API キーを設定する
   - 環境変数 `OPENAI_API_KEY`
   - または `src/AiChatCLI/appsettings.json` の `OpenAI:ApiKey`
2. ビルドして実行する

```bash
dotnet build
dotnet run --project src/AiChatCLI/AiChatCLI.csproj
```

テスト:

```bash
dotnet test AiChatCLI.sln
```

`src/AiChatCLI/README.md` には `/thread use <id>` による履歴復元、ログ保存先、`ChatHistory:Enabled` 無効時の制約も含めてまとめています。

## 実装の要点

- `Program.cs` は薄い entry point として `AppPaths` で content root と runtime asset のパスを解決し、manual wiring は `Bootstrap/AppComposition.cs` に寄せています
- `ChatLoop` は REPL 入出力だけを担い、1 ターンの処理は `ChatTurnPipeline` に集約されています
- `ConversationCodec` は live 会話と thread replay の message / tool-call 変換を共通化します
- `OpenAIAgentFactory` は親 agent とサブエージェント用 agent のツール登録を分け、サブ側から `sub_agent` を呼べないようにしています
- `ThreadSessionManager` は current thread の切り替えと replay を担い、append-only の event 書き込みは `ThreadRecorder` に寄せています
- `tests/AiChatCLI.Tests/` には path 解決、slash command 出力、thread projector、conversation codec の focused test を置いています
