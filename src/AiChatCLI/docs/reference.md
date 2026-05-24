# AiChatCLI Quick Reference

一覧性を優先した早見表です。詳細な説明は [`usage.md`](usage.md)、[`configuration.md`](configuration.md)、[`development.md`](development.md) を参照してください。

## slash command 一覧

| コマンド | 役割 |
|---|---|
| `/help` | コマンド体系のヘルプ表示 |
| `/status` | モデル、agent、tool、thread、memory、定義ファイルの状態確認 |
| `/agent list` | agent 一覧表示 |
| `/agent show <名前>` | agent 詳細表示 |
| `/agent use <名前>` | agent 切り替え |
| `/agent reload` | agent 定義の再読み込み |
| `/thread list` | thread 一覧表示 |
| `/thread current` | current thread 表示 |
| `/thread use <id>` | 過去 thread の再開 |
| `/thread new` | 空の thread を新規作成 |
| `/prompt template list` | template 一覧表示 |
| `/prompt template show <キー>` | template 詳細表示 |
| `/prompt template set <キー> <本文...>` | template 追加・更新 |
| `/prompt template delete <キー>` | template 削除 |
| `/prompt template reload` | template 定義の再読み込み |

## agent-callable tool 一覧

| tool | 既定 | 公開範囲 | 主な用途 | 補足 |
|---|---|---|---|---|
| `memory` | 有効 | main / sub | 短い長期メモリ保存 | `Paths:Memory` を使う |
| `sub_agent` | 有効 | main のみ | 独立コンテキストへ作業委譲 | sub-agent から再帰起動不可 |
| `command` | 有効 | main / sub | 承認付きローカルコマンド実行 | 実行前に `YES/NO` が必要 |
| `read_file` | 有効 | main / sub | ローカルファイルの読み取り | 読み取り専用 |
| `search` | 無効 | main / sub | Tavily Web 検索 | `Tools:Enabled` と API キーが必要 |

## 主な設定キー

| キー | 既定値 | 用途 |
|---|---|---|
| `OpenAI:Model` | `gpt-4o-mini` | モデル指定 |
| `Template:MaxDepth` | `10` | `%KEY%` 展開の最大深度 |
| `ChatHistory:Enabled` | `true` | thread / log の有効化 |
| `Paths:Agents` | `agents.json` | agent 定義 |
| `Paths:Prompts` | `prompts.json` | prompt template 定義 |
| `Paths:Memory` | `memory.json` | memory 保存先 |
| `Paths:ChatHistoryDirectory` | `logs` | テキストログ基底ディレクトリ |
| `Paths:ThreadsDirectory` | `<ChatHistoryDirectory>/threads` | thread JSONL 保存先 |
| `Paths:SubAgentThreadsDirectory` | `<ThreadsDirectory>/subagents` | sub-agent JSONL 保存先 |
| `Tools:Enabled` | `memory`, `sub_agent`, `command`, `read_file` | base tool の公開制御 |

## 組み込みプレースホルダー

- `%SYSTEM_INFO%`: OS、.NET runtime、command ツールのシェルと文字コード設定
- `%CURRENT_DIRECTORY_ENTRIES%`: セッション current directory 直下のファイル・フォルダ一覧

## ログ出力先

| 種類 | 既定パス | 内容 |
|---|---|---|
| テキストログ | `logs/chat_yyyyMMdd_HHmmss_fff.txt` | セッション全体の人間向け transcript |
| thread ログ | `logs/threads/thread_*.jsonl` | append-only の structured event log |
| sub-agent thread ログ | `logs/threads/subagents/subagent_thread_*.jsonl` | サブエージェント用 structured event log |

## thread replay の要点

- `/thread use <id>` は thread JSONL を正本として会話状態を復元します
- user / assistant message、tool call / result、agent 状態を復元して継続します
- thread を切り替えても過去メッセージは端末に再描画しません
- `ChatHistory:Enabled=false` では thread 機能と structured thread log は無効です

## 実装把握の入口

| 関心事 | まず見る場所 |
|---|---|
| 起動と wiring | `Program.cs`, `Bootstrap/AppComposition.cs` |
| 設定解決 | `Bootstrap/AppConfig.cs`, `Bootstrap/AppPaths.cs` |
| 1 ターン処理 | `Conversation/ChatLoop.cs`, `Conversation/ChatTurnPipeline.cs` |
| live/replay の message 変換 | `Conversation/ConversationCodec.cs` |
| thread lifecycle | `Threads/ThreadSessionManager.cs`, `Threads/ThreadRecorder.cs`, `Threads/ThreadProjector.cs` |
| tool 公開 | `Agents/AgentToolCatalog.cs`, `Agents/OpenAIAgentFactory.cs` |
| slash command | `Commands/` |
