# AiChatCLI Development Guide

`src/AiChatCLI/` を改善するときの責務分割、拡張ポイント、テスト観点をまとめたガイドです。利用者向けの操作は [`usage.md`](usage.md)、設定と tool 公開は [`configuration.md`](configuration.md) を参照してください。

## 主要ディレクトリ

- `Bootstrap/`: `AppConfig`、`AppPaths`、`AppComposition` による repo / project path 発見、設定読み込み、manual wiring
- `Conversation/`: `ChatLoop`、`ChatTurnPipeline`、`OpenAIChatService`、`ConversationCodec`、`AgentTurnExecutor`
- `Agents/`: agent 定義、selection、組み込み placeholder、OpenAI agent factory
- `Tools/`: `AgentToolCatalog`、agent-callable tool adapter、sub-agent tool support type
- `Skills/`: `Paths:SkillsDirectory` 配下の `SKILL.md` の front matter / markdown 解析、skill metadata の prompt 注入
- `Threads/`: current thread lifecycle、append-only event 記録、thread replay、projection
- `Prompts/`: prompt template の読み込み、保存、展開
- `Commands/`: slash command の contract、handler、help metadata、実装
- `Ui/`: interactive prompt、clipboard 貼り付け、出力補助
- `IO/`: encoding-aware file read と文字コード既定値
- `Memory/`: durable memory store

## 起動シーケンス

`Program.cs` は薄い entry point です。

1. `TextEncodingDefaults.RegisterCodePagesProvider()` を呼ぶ
2. `AppPaths.Discover(...)` で project root、repo root、settings base directory を確定する
3. `AppComposition.Create(...)` で依存関係を組み立てる
4. `ThreadSessionManager.Initialize()` で current thread を初期化する
5. `ChatLoop.RunAsync(...)` で REPL を開始する

依存関係の追加や slash command / tool の登録は、原則として `AppComposition.Create(...)` に集約します。

## 1 ターンの流れ

通常入力は次の順に処理されます。

1. `InteractivePromptReader` が入力を受け取る
2. `ChatLoop` が REPL と表示を扱う
3. `ChatTurnPipeline` が slash command 判定、template 展開、`ChatTraceRecorder` への trace 発行、`IChatService` 呼び出しをまとめる
4. `OpenAIChatService` と `AgentTurnExecutor` がモデル応答と tool 実行を処理する
5. `ChatTraceRecorder` が transcript と structured thread trace へ配信する

`ChatTurnPipeline` は trace 発行順を定義する責務を持ちます。structured trace と人間向け transcript は `ChatTraceRecorder` を介して同じ順序で流れるため、この順序を変える変更は replay やデバッグ体験を壊しやすい点に注意してください。

## thread replay とログ

- `ThreadSessionManager` が current thread の切り替えと replay を担当します
- `ThreadRecorder` は append-only の thread event 書き込みを担当します
- `ThreadRepository` は `*.jsonl` の読み書き入口です
- `ThreadProjector` は event 列から current state を再構築します
- `ConversationCodec` は live 会話と persisted thread message の変換を共有します
- `ThreadEvent` は replay 正本となる structured trace schema で、session lifecycle や slash command もここへ集約されます
- `ChatHistoryLogger` は別系統の記録 API ではなく、`ChatTraceRecorder` から供給される同一 trace の text projection です

message 形状、tool call 記録、agent 切り替えの扱いを変える場合は、`ConversationCodec` と thread 系の projector / recorder / repository をまとめて見直してください。

## tool と agent の構成

- `AgentToolCatalog` が登録済み tool と consumer ごとの公開範囲を一元管理します
- agent-callable tool の入口は `Tools/` 配下に集約し、`MemoryStore`、`SkillCatalog`、`TavilySearchClient` などのドメイン実体は既存フォルダへ残します
- 各 agent がどの tool を使えるかは `agents.json` の `tools` 配列で決まります
- main agent / sub-agent の公開差は `AgentToolScope` で表現します
- `OpenAIAgentFactory` は main agent と sub-agent の binding を組み立て、`skill` が有効なときは available skill の name / description を system prompt へ追記します
- `SubAgentRunner` は `Tools/SubAgent/` 配下で独立コンテキストのサブエージェント実行と sub-agent thread log を担当します

新しい tool を追加するときは、まず `Tools/` 配下へ入口を追加し、必要なドメイン実装を既存フォルダへ置いたうえで、`AgentToolCatalog`、`/status` 表示、README 配下の関連文書、必要な focused test まで同じ変更でそろえてください。

## 拡張ポイント

### 新しい slash command

- `ISlashCommand.Execute(string[] args, TextWriter output)` を実装します
- `SlashCommandHandler` に登録し、`HelpCommand` / `StatusCommand` との整合を確認します
- 状態変更系コマンドは `AgentSelection`、`IChatService`、`ThreadSessionManager` の同期を崩さないようにし、trace 追加は `ChatTraceRecorder` 経由で行います

### 新しい chat backend

- `IChatService` を実装します
- thread replay が有効なときに persisted 状態を復元できることを前提に設計します
- live turn と replay の message 形状がずれないよう `ConversationCodec` を流用します

### 新しい prompt/template 処理

- `IPromptTemplateProcessor` を実装するか、既存の `PromptTemplateProcessor` / `PlaceholderExpander` を拡張します
- ad hoc な文字列置換を REPL 周辺へ直接書き込まず、template 処理へ寄せます

### 新しい file I/O

- `TextFileReader` と `TextEncodingDefaults` を優先し、ad hoc な encoding 判定を増やさないでください
- Windows の日本語環境も含めて、BOM なし UTF-8 と代表的な code page を意識します

## テスト方針

`tests/AiChatCLI.Tests/` には seam 単位の focused xUnit test を置きます。変更内容に応じて次を候補にします。

- path / config 変更: `AppConfig`、`AppPaths`
- tool 公開や status 出力の変更: `AgentToolCatalog`、`StatusCommand`
- slash command の挙動変更: `SlashCommandHandler`、各 command test
- replay / logging 変更: `ConversationCodec`、`ThreadProjector`、`ThreadRepository`、`ThreadRecorder`
- command approval や file read の変更: `CommandTools`、`FileReadTools`
- sub-agent 挙動の変更: `SubAgentRunner`

REPL の実地確認が必要な変更では `devtools/cli-session/` も活用します。

## ドキュメント更新の目安

- 使い方が変わる変更: [`usage.md`](usage.md)
- 設定や tool 公開が変わる変更: [`configuration.md`](configuration.md)
- 早見表が変わる変更: [`reference.md`](reference.md)
- 入口や導線が変わる変更: [`../README.md`](../README.md)
