# AiChatCLI

`AiChatCLI` は、CLI から OpenAI モデルと対話できる .NET 9 コンソールアプリです。

関連ドキュメント:

- リポジトリ全体の概要: [`../../README.md`](../../README.md)
- 開発支援 workflow: [`../../devtools/workflow/README.md`](../../devtools/workflow/README.md)

## セットアップ

1. ローカル設定ファイルを用意する
   - `appsettings.local.example.json` を `appsettings.local.json` にコピー
   - `appsettings.local.json` の `OpenAI:ApiKey` に自分のキーを設定
   - Tavily 検索を使う場合は `Tavily:ApiKey` も設定
   - `appsettings.local.json` は `.gitignore` 済みで Git 管理しません
   - 環境変数 `OPENAI_API_KEY` / `TAVILY_API_KEY` も引き続き使えますが、ローカル設定ファイルを優先できます
2. ビルドして実行する

```bash
dotnet build
dotnet run --project src/AiChatCLI/AiChatCLI.csproj
```

テスト:

```bash
dotnet test AiChatCLI.sln
```

## 内部構成

現在の主要な責務分割:

- `Program.cs` は `AppPaths.Discover(...)`、`AppComposition.Create(...)`、`ThreadSessionManager.Initialize()`、`ChatLoop.RunAsync(...)` だけを呼ぶ薄い entry point です
- `Bootstrap/` は `AppConfig`、`AppPaths`、`AppComposition` による config / path discovery と manual wiring を担います
- `Conversation/` は `ChatLoop`、`ChatTurnPipeline`、`OpenAIChatService`、`ConversationCodec` など、REPL と 1 ターン処理、モデル送信、transcript logging、live message と persisted thread message の変換をまとめます
- `Agents/` は agent 定義、current agent 選択、`AgentToolCatalog`、`OpenAIAgentFactory`、sub-agent 実行と関連 DTO をまとめます。利用可能 tool は `Tools:Enabled` の base tool 設定と、main agent / sub-agent ごとのコード内ルールを合成して決まります
- `Threads/` は current thread の lifecycle / replay、append-only event 書き込み、projection、thread record / summary を扱います
- `Prompts/` は prompt template の読み込み、保存、展開を扱います
- `Commands/` は slash command の contract / handler / help metadata / 実装群をまとめます。command は `ISlashCommand.Execute(string[] args, TextWriter output)` を実装します
- `Ui/` は console input、prompt buffer rendering、display width 計算、clipboard access、output teeing を扱います
- `Memory/` は設定された memory 保存ファイルの durable store と Function Calling 用 memory tool を扱います

## 基本操作

起動すると現在のエージェント名を含むプロンプトが表示され、入力内容が AI に送られます。
全角文字を含むエージェント名でも、入力カーソルは `>` の直後から自然に始まります。
入力欄では `Ctrl+V` または `Shift+Insert` で clipboard の文字列を貼り付けでき、複数行テキストもそのまま保持されます。

```text
AI Chat CLI (model: gpt-4o-mini, agent: default, exitで終了)
defaultエージェント> こんにちは
defaultエージェント> こんにちは！何かお手伝いできることはありますか？
defaultエージェント> exit
```

- `exit` と入力すると終了します
- `ChatHistory:Enabled` が有効なら、起動時に空の current thread が 1 つ自動作成されます

## スラッシュコマンド

`/` で始まる入力はコマンドとして処理されます。

| コマンド | 説明 |
|---|---|
| `/help` | コマンド体系と利用可能な操作を表示 |
| `/status` | 現在のモデル、選択中の agent、利用可能 tool、current thread、定義ファイル、memory 状態などを表示 |
| `/agent` | agent 系コマンドのヘルプを表示 |
| `/agent list` | エージェント一覧を表示 |
| `/agent show <名前>` | 指定したエージェントの内容を表示 |
| `/agent use <名前>` | エージェントを切り替え |
| `/agent reload` | 設定済みの agent 定義ファイルを再読み込み |
| `/thread` | thread 系コマンドのヘルプを表示 |
| `/thread list` | 保存済み thread 一覧を表示 |
| `/thread current` | 現在の thread を表示 |
| `/thread use <id>` | 過去の thread を読み込んで切り替え |
| `/thread new` | 新しい空の thread を作成して切り替え |
| `/prompt` | prompt 系コマンドのヘルプを表示 |
| `/prompt template list` | テンプレート一覧を表示 |
| `/prompt template show <キー>` | 指定テンプレートの内容を表示 |
| `/prompt template set <キー> <本文...>` | テンプレートを追加または更新 |
| `/prompt template delete <キー>` | テンプレートを削除 |
| `/prompt template reload` | 設定済みの template 定義ファイルを再読み込み |

## テンプレート

`@キー名` を入力の先頭に付けると、定義済みテンプレートに展開されてから AI に送信されます。文中でも `@キー名` の前が空白または行頭であれば展開対象になります。該当テンプレートがなければ通常文字として扱われます。`@` を単独で入力した直後は候補一覧が表示され、上下キーで選択し、`Tab` または `Enter` で確定できます。

```text
You> @translate Hello, world!
→ 「以下を日本語に翻訳してください: Hello, world!」としてAIに送信
```

デフォルトで用意されているテンプレート:

| キー | 展開内容 |
|---|---|
| `@translate` | 以下を日本語に翻訳してください: |
| `@summarize` | 以下を要約してください: |
| `@review` | 以下のコードをレビューしてください: |

テンプレートは既定では `prompts.json` で追加・編集できます。CLI からは `/prompt template ...` で操作します。外部で JSON を直接編集した場合は `/prompt template reload` で再読み込みできます。保存先は `Paths:Prompts` で変更できます。

テンプレート内で `%KEY%` を使うと別テンプレートを参照できます。この `%KEY%` 展開はテンプレート本文の内部参照にのみ適用され、ユーザー入力中の `%KEY%` はそのまま扱われます。

```text
You> /prompt template set explain 以下をわかりやすく説明してください:
テンプレート 'explain' を追加し、設定済みの template 定義ファイルに保存しました。

You> /prompt template show explain
@explain
以下をわかりやすく説明してください:
```

## エージェント

AI の振る舞いは、会話途中でもエージェント単位で切り替えられます。

```text
defaultエージェント> /agent use translator
エージェントを 'translator' に切り替えました。

translatorエージェント> Hello, world!
translatorエージェント> こんにちは、世界！
```

デフォルトで用意されているエージェント:

| 名前 | 役割 |
|---|---|
| `default` | 汎用アシスタント |
| `translator` | 翻訳者 |
| `coder` | プログラマー |

既定では `agents.json` で追加・編集でき、CLI からは `/agent ...` で操作します。外部で JSON を直接編集した場合は `/agent reload` で再読み込みできます。保存先は `Paths:Agents` で変更できます。互換性のため、agent 定義ファイルが無い場合は `Paths:LegacySystemPrompts`（既定では `system_prompts.json`）も読み込み対象になります。

## スレッド

会話は thread 単位で構造化ログに保存され、起動時に current thread が自動作成されます。あとから `/thread use <id>` で読み込んで継続できます。

```text
defaultエージェント> /thread list
  thread_20260422_110000_123_abcd1234 [現在] : agent=default, messages=0, updated=2026-04-22 20:00:00

defaultエージェント> /thread use thread_20260421_230000_456_ef567890
スレッド 'thread_20260421_230000_456_ef567890' に切り替えました。
```

- thread を切り替えても、過去メッセージは端末に再描画しません
- 読み込み時は、過去の user / assistant メッセージ、agent 状態、tool 呼び出しと結果を復元し、そのまま次の発話を継続できます
- 新しい空の会話を始めるときは `/thread new` を使います
- 現在の thread ID と対応する JSONL パスは `/thread current` または `/status` で確認できます
- `ChatHistory:Enabled` を `false` にすると thread 機能と structured thread log は無効になります
- `/agent use`、`/agent reload`、`/thread use`、`/thread new` のような状態変更は thread ログにも記録されます

## メモリツール

モデルは Function Calling を使って、必要に応じて設定された memory 保存ファイルに簡単な長期メモリを保存できます。

- 保存先は `Paths:Memory`（既定では `memory.json`）
- 1 ファイルの JSON に `key: value` 形式で保存
- 主にユーザー設定、好み、継続中タスクの前提などの短い事実を保持
- モデルは `upsert` / `get` / `list` / `delete` を内部的に使い分ける
- `appsettings.json` の `Tools:Enabled` から `memory` を外すと memory tool を agent へ公開しません

例:

```text
You> 私の出力言語は基本的に日本語でお願いします
AI> 承知しました。以後は日本語を基本に回答します。
```

この種の継続的な好みは、モデルが必要だと判断すると memory 保存ファイルに保存され、次のやり取りでも参照できます。`/status` では保存ファイルの場所と件数を確認できます。

## Search ツール

Tavily Search API を使う `search` ツールを追加できます。`query` と `search_depth` を受け取り、関連度順の検索結果を JSON で返します。

- `search` は opt-in です。`Tools:Enabled` に `search` を含めたときだけ main agent / sub-agent の両方へ公開されます
- API キーは `appsettings.local.json` の `Tavily:ApiKey`、または環境変数 `TAVILY_API_KEY` で設定します
- `search_depth` は `basic` が標準、`advanced` は高精度、`fast` / `ultra-fast` は低遅延向けです
- `search` を有効にしたのに Tavily API キーが無い場合は、起動時に設定エラーで停止します

例:

```json
{
  "Tools": {
    "Enabled": ["memory", "sub_agent", "search"]
  },
  "Tavily": {
    "ApiKey": "tvly-your-tavily-api-key"
  }
}
```

## サブエージェントツール

モデルは Function Calling の `sub_agent` ツールを使って、現在の会話履歴を持たない新しいサブエージェントに作業を委譲できます。

- `sub_agent` は `prompt` を受け取り、その prompt だけを user message としてサブエージェントを実行します
- サブエージェントは `Paths:Memory` で指定されたメモリツールを利用できます
- サブエージェント側の利用可能ツールには `sub_agent` を含めないため、サブエージェントからさらにサブエージェントを起動することはできません
- ツール結果は JSON 文字列で返り、`subAgentThreadId` とサブエージェントの最終回答を含みます
- `appsettings.json` の `Tools:Enabled` から `sub_agent` を外すと main agent からも `sub_agent` を公開しません

## 会話履歴ログ

起動ごとに 1 ファイルのテキストログと、thread ごとの構造化ログを保存します。

- 保存先: 既定では `appsettings.json` と同じ基準ディレクトリ配下の `logs` フォルダ。`Paths:ChatHistoryDirectory` で別パスを指定できます
- `Paths:*` に相対パスを指定した場合は、いずれも `appsettings.json` と同じ基準ディレクトリから解決します
- テキストログ: 基本は `chat_yyyyMMdd_HHmmss_fff.txt`
- thread ログ: 既定では `logs/threads/thread_*.jsonl`。`Paths:ThreadsDirectory` で変更できます
- サブエージェントログ: 既定では `logs/threads/subagents/subagent_thread_*.jsonl`。`Paths:SubAgentThreadsDirectory` で変更できます
- 起動時: 記録が有効ならコンソールにテキストログの絶対パスを表示。current thread の詳細は `/thread current` で確認できます
- テキストログの記録内容: セッション開始・終了、ユーザー入力、プロンプト変換結果、モデル送信文、ツール呼び出し（関数名・引数・結果）、サブエージェント thread ID、AI 応答、スラッシュコマンドとその出力
- thread ログの記録内容: append-only JSONL での thread 作成・接続/切断、ユーザー入力、プロンプト変換結果、モデル送信文、assistant メッセージ、tool 呼び出しと結果、`subagent_invoked` によるサブエージェント thread ID、agent 切り替え時の system prompt snapshot。UTF-8 の日本語などは `\uXXXX` ではなく、そのまま読める形で保存します
- サブエージェントログには、サブエージェント自身の user/model/tool/assistant イベントを親 thread と同じ JSONL 形式で保存します
- `/thread use` は設定済みの thread ログディレクトリ配下の `*.jsonl` を正本として会話状態を復元します
- `ConversationCodec` と `ThreadProjector` が共通の message / tool-call 仕様で復元するため、live turn と replay の整合を保ちます

無効にするには `appsettings.json` または環境変数で `ChatHistory:Enabled` を `false` にします。環境変数では `ChatHistory__Enabled` や `Paths__ChatHistoryDirectory` のように `__` でセクションとキーを区切ります。互換性のため `ChatHistory:Directory` も引き続き読み取りますが、`Paths:ChatHistoryDirectory` が優先されます。

## 設定

| 項目 | 設定方法 | デフォルト値 |
|---|---|---|
| OpenAI API キー | `appsettings.local.json` の `OpenAI:ApiKey` または環境変数 `OPENAI_API_KEY` | - |
| Tavily API キー | `appsettings.local.json` の `Tavily:ApiKey` または環境変数 `TAVILY_API_KEY` | 未設定 |
| モデル | `appsettings.json` の `OpenAI:Model` | `gpt-4o-mini` |
| テンプレート展開の最大深度 | `appsettings.json` の `Template:MaxDepth` | `10` |
| 会話ログの有効化 | `ChatHistory:Enabled` | `true` |
| agent 定義ファイル | `Paths:Agents` | `agents.json` |
| legacy system prompt 定義ファイル | `Paths:LegacySystemPrompts` | `system_prompts.json` |
| テンプレート定義ファイル | `Paths:Prompts` | `prompts.json` |
| メモリ保存ファイル | `Paths:Memory` | `memory.json` |
| 会話ログの保存ディレクトリ | `Paths:ChatHistoryDirectory` | `logs` |
| thread ログの保存ディレクトリ | `Paths:ThreadsDirectory` | 未設定時は `<ChatHistoryDirectory>/threads` |
| sub-agent thread ログの保存ディレクトリ | `Paths:SubAgentThreadsDirectory` | 未設定時は `<ThreadsDirectory>/subagents` |
| base tool の公開一覧 | `Tools:Enabled` | 未設定時は `memory`, `sub_agent` |

例:

```json
{
  "Paths": {
    "Agents": "config/agents.json",
    "Prompts": "config/prompts.json",
    "Memory": "state/memory.json",
    "ChatHistoryDirectory": "artifacts/logs",
    "ThreadsDirectory": "artifacts/threads",
    "SubAgentThreadsDirectory": "artifacts/threads/subagents"
  },
  "Tools": {
    "Enabled": ["memory", "sub_agent"]
  }
}
```

検索ツールをローカルだけで有効にしたい場合は、`appsettings.local.json` に次を追加します。

```json
{
  "Tavily": {
    "ApiKey": "tvly-your-tavily-api-key"
  },
  "Tools": {
    "Enabled": ["memory", "sub_agent", "search"]
  }
}
```

`Tools:Enabled` は base tool の採用可否だけを決めます。たとえば `sub_agent` を含めても、sub-agent 側ではコード内ルールにより `sub_agent` は公開しません。

環境変数では `Tools__Enabled__0=memory`、`Tools__Enabled__1=sub_agent` のように `__` 区切りで同じ設定を上書きできます。

## テスト

- `tests/AiChatCLI.Tests/` に focused xUnit test を置いています
- 現在は `AppConfig`、`AppPaths`、`AgentToolCatalog`、`ConversationCodec`、`SlashCommandHandler`、`StatusCommand`、`ThreadProjector`、`ThreadRepository`、`ThreadRecorder`、`SubAgentRunner` を対象に、base tool 設定・consumer ごとの tool 公開・path 解決・tool aggregate・slash command 出力・thread replay・サブエージェントログの回帰を確認しています

## ライブラリ

- [AutoGen](https://github.com/microsoft/autogen/tree/main/dotnet)（Microsoft）

## プロバイダ

- OpenAI
