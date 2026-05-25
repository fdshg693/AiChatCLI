# AiChatCLI

CLI から AI と対話する .NET アプリと、その開発を支援する Cursor CLI 向け workflow 群を同居させたリポジトリです。

## リポジトリ構成

- アプリ本体: `src/AiChatCLI/`
- アプリ向け focused test: `tests/AiChatCLI.Tests/`
- 開発支援 workflow: `devtools/workflow/`
- 対話 CLI セッションラッパー: `devtools/cli-session/`

## ドキュメント

- アプリ本体の入口ドキュメント: [`src/AiChatCLI/README.md`](src/AiChatCLI/README.md)
- アプリ利用ガイド: [`src/AiChatCLI/docs/usage.md`](src/AiChatCLI/docs/usage.md)
- アプリ設定ガイド: [`src/AiChatCLI/docs/configuration.md`](src/AiChatCLI/docs/configuration.md)
- アプリ開発ガイド: [`src/AiChatCLI/docs/development.md`](src/AiChatCLI/docs/development.md)
- アプリ早見表: [`src/AiChatCLI/docs/reference.md`](src/AiChatCLI/docs/reference.md)
- workflow の入口: [`devtools/workflow/README.md`](devtools/workflow/README.md)
- workflow の日常利用ガイド: [`devtools/workflow/docs/usage.md`](devtools/workflow/docs/usage.md)
- workflow の設定・カスタマイズ: [`devtools/workflow/docs/customization.md`](devtools/workflow/docs/customization.md)
- workflow の実装概要: [`devtools/workflow/docs/architecture.md`](devtools/workflow/docs/architecture.md)
- CLI セッションラッパーの使い方: [`devtools/cli-session/README.md`](devtools/cli-session/README.md)
- AI 向けの恒久ルール: `.cursor/rules/always/*.mdc`

## アプリ本体の主な機能

- slash command による agent / prompt / thread の操作
- settings で指定した memory ファイルを使った Function Calling ベースの長期メモリ
- `sub_agent` ツールによる、独立した会話コンテキストのサブエージェント実行
- 承認付き `command` ツールによるローカルコマンド実行
- `skill` ツールによるローカル skill markdown の遅延読み込み
- `logs/threads/*.jsonl` を正本にした thread 履歴復元と会話継続
- 既存の人間向け `chat_*.txt` テキストログ出力

## クイックスタート

1. API キーを設定する
   - `src/AiChatCLI/appsettings.local.example.json` を `src/AiChatCLI/appsettings.local.json` にコピー
   - `src/AiChatCLI/appsettings.local.json` の `OpenAI:ApiKey`
   - または環境変数 `OPENAI_API_KEY`
2. 非秘密設定を確認する
   - `OpenAI:Model`、`Paths:*`、`ChatHistory:*` は `.ai_chat/settings.json`
3. ビルドして実行する

```bash
dotnet build
dotnet run --project src/AiChatCLI/AiChatCLI.csproj
```

テスト:

```bash
dotnet test AiChatCLI.sln
```

`src/AiChatCLI/README.md` は入口に絞り、利用方法・設定・開発・早見表は `src/AiChatCLI/docs/*.md` へ分割しています。

## 実装の要点

- `Program.cs` は薄い entry point として `AppPaths` で project root / repo root / settings base を解決し、manual wiring は `Bootstrap/AppComposition.cs` に寄せています
- `ChatLoop` は REPL 入出力だけを担い、1 ターンの処理は `ChatTurnPipeline` に集約されています
- `ConversationCodec` は live 会話と thread replay の message / tool-call 変換を共通化します
- `OpenAIAgentFactory` は親 agent とサブエージェント用 agent のツール登録を分け、サブ側から `sub_agent` を呼べないようにしています
- `SkillCatalog` / `SkillPromptAugmenter` は `Paths:SkillsDirectory` 配下の `SKILL.md` から name / description を prompt へ注入し、本文は `skill` ツール呼び出し時だけ返します
- `ThreadSessionManager` は current thread の切り替えと replay を担い、append-only の event 書き込みは `ThreadRecorder` に寄せています
- `tests/AiChatCLI.Tests/` には path 解決、slash command 出力、thread projector、conversation codec の focused test を置いています
