# AiChatCLI Quick Reference

一覧性を優先した早見表です。詳細な説明は [`usage.md`](usage.md)、[`configuration.md`](configuration.md)、[`development.md`](development.md) を参照してください。

## slash command 一覧

| コマンド | 役割 |
|---|---|
| `/help` | コマンド体系のヘルプ表示 |
| `/status` | モデル、agent、現在の agent / sub-agent tool、thread、memory、定義ファイルの状態確認 |
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

| tool | 利用条件 | 公開範囲 | 主な用途 | 補足 |
|---|---|---|---|---|
| `memory` | agent の `tools` に含める | main / sub | 短い長期メモリ保存 | `Paths:Memory` を使う |
| `sub_agent` | agent の `tools` に含める | main のみ | 独立コンテキストへ作業委譲 | sub-agent から再帰起動不可 |
| `command` | agent の `tools` に含める | main / sub | 承認付きローカルコマンド実行 | 実行前に `YES/NO` が必要 |
| `read_file` | agent の `tools` に含める | main / sub | ローカルファイルの読み取り | 読み取り専用 |
| `skill` | agent の `tools` に含める | main / sub | ローカル skill markdown の遅延読み込み | prompt へは name / description のみ注入 |
| `search` | agent の `tools` に含める | main / sub | Tavily Web 検索 | `Tavily:ApiKey` または環境変数が必要 |

## 主な設定キー

| キー | 既定値 | 用途 |
|---|---|---|
| `OpenAI:Model` | `gpt-4o-mini` | モデル指定 |
| `Template:MaxDepth` | `10` | `%KEY%` 展開の最大深度 |
| `ChatHistory:Enabled` | `true` | 互換用の親フラグ。未設定時の `Logging:*Enabled` 既定値 |
| `Logging:TranscriptEnabled` | `<ChatHistory:Enabled>` | `chat_*.txt` transcript の保存可否 |
| `Logging:ThreadEnabled` | `<ChatHistory:Enabled>` | main thread JSONL と `/thread` replay の有効化 |
| `Logging:SubAgentThreadEnabled` | `<Logging:ThreadEnabled>` | sub-agent JSONL の有効化 |
| `Paths:Agents` | `agents.json` | agent 定義 |
| `Paths:Prompts` | `prompts.json` | prompt template 定義 |
| `Paths:Memory` | `memory.json` | memory 保存先 |
| `Paths:SkillsDirectory` | `skills` | skill ルート |
| `Paths:ChatHistoryDirectory` | `logs` | テキストログ基底ディレクトリ |
| `Paths:ThreadsDirectory` | `<ChatHistoryDirectory>/threads` | thread JSONL 保存先 |
| `Paths:SubAgentThreadsDirectory` | `<ThreadsDirectory>/subagents` | sub-agent JSONL 保存先 |

## 組み込みプレースホルダー

- `%SYSTEM_INFO%`: OS、.NET runtime、command ツールのシェルと文字コード設定
- `%CURRENT_DIRECTORY_ENTRIES%`: セッション current directory 直下のファイル・フォルダ一覧

## skill 配置

- skill ルートは `Paths:SkillsDirectory` で指定する
- 各 skill は `<SkillsDirectory>/<skill-directory>/SKILL.md` に置く
- front matter は `name` と `description` のみ対応
- `skill` ツールの戻り値には `SKILL.md` と skill ディレクトリの絶対パスが含まれる
- 相対パスの基準は `.ai_chat/settings.json` のあるディレクトリ

## ログ出力先

| 種類 | 既定パス | 内容 |
|---|---|---|
| transcript | `.ai_chat/logs/chat_yyyyMMdd_HHmmss_fff.txt` | session / 会話 / slash command / agent / thread の人間向け transcript |
| thread ログ | `.ai_chat/logs/threads/thread_*.jsonl` | replay 正本になる append-only structured event log |
| sub-agent thread ログ | `.ai_chat/logs/threads/subagents/subagent_thread_*.jsonl` | サブエージェント用の append-only structured event log |

## thread replay の要点

- `/thread use <id>` は thread JSONL を正本として会話状態を復元します
- user / assistant message、tool call / result、agent 状態を復元して継続します
- thread を切り替えても過去メッセージは端末に再描画しません
- `Logging:ThreadEnabled=false` では thread 機能と main thread JSONL は無効です
- slash command、session lifecycle、agent 切り替えも replay 互換な trace event として JSONL に追記されます

## 実装把握の入口

| 関心事 | まず見る場所 |
|---|---|
| 起動と wiring | `Program.cs`, `Bootstrap/AppComposition.cs` |
| 設定解決 | `Bootstrap/AppConfig.cs`, `Bootstrap/AppPaths.cs` |
| 1 ターン処理 | `Conversation/ChatLoop.cs`, `Conversation/ChatTurnPipeline.cs`, `Conversation/ChatTraceRecorder.cs` |
| live/replay の message 変換 | `Conversation/ConversationCodec.cs` |
| thread lifecycle / structured trace | `Threads/ThreadSessionManager.cs`, `Threads/ThreadRecorder.cs`, `Threads/ThreadProjector.cs`, `Threads/ThreadEvent.cs` |
| tool 公開 | `Agents/AgentToolCatalog.cs`, `Agents/OpenAIAgentFactory.cs`, `Skills/` |
| slash command | `Commands/` |
