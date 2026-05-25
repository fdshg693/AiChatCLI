# AiChatCLI Configuration Guide

`AiChatCLI` の設定ファイル、環境変数、agent ごとの tool 定義、ログ保存先をまとめたガイドです。日常利用の操作は [`usage.md`](usage.md)、一覧性を重視した要約は [`reference.md`](reference.md) を参照してください。

## セットアップ

1. `appsettings.local.example.json` を `appsettings.local.json` にコピーします
2. `appsettings.local.json` の `OpenAI:ApiKey` に API キーを設定します
3. Tavily 検索を使う場合は `Tavily:ApiKey` も設定します
4. `.ai_chat/settings.json` でモデル、各種 `Paths:*`、ログ設定を確認します
5. `appsettings.local.json` は `.gitignore` 済みで Git 管理しません

```bash
dotnet build
dotnet run --project src/AiChatCLI/AiChatCLI.csproj
```

テスト:

```bash
dotnet test AiChatCLI.sln
```

## 設定の読み分け

設定は用途ごとに分離しています。

- 非秘密設定: repo root の `.ai_chat/settings.json` を読み、必要なら環境変数で上書きします
- 秘密情報: `appsettings.local.json` を読み、必要なら環境変数で上書きします
- `src/AiChatCLI/appsettings.json` は runtime 設定ソースとしては使いません

代表例:

- `OPENAI_API_KEY`
- `TAVILY_API_KEY`
- `ChatHistory__Enabled`
- `Logging__TranscriptEnabled`
- `Logging__ThreadEnabled`
- `Logging__SubAgentThreadEnabled`
- `Paths__ChatHistoryDirectory`

## 主な設定項目

| 項目 | 設定方法 | デフォルト値 |
|---|---|---|
| OpenAI API キー | `OpenAI:ApiKey` または `OPENAI_API_KEY` | 必須 |
| Tavily API キー | `Tavily:ApiKey` または `TAVILY_API_KEY` | 未設定 |
| モデル | `OpenAI:Model` | `gpt-4o-mini` |
| テンプレート展開の最大深度 | `Template:MaxDepth` | `10` |
| transcript の既定有効化 | `ChatHistory:Enabled` | `true` |
| transcript の有効化 | `Logging:TranscriptEnabled` | 未設定時は `ChatHistory:Enabled` を継承 |
| thread ログの有効化 | `Logging:ThreadEnabled` | 未設定時は `ChatHistory:Enabled` を継承 |
| sub-agent thread ログの有効化 | `Logging:SubAgentThreadEnabled` | 未設定時は `Logging:ThreadEnabled` を継承 |
| agent 定義ファイル | `Paths:Agents` | `agents.json` |
| テンプレート定義ファイル | `Paths:Prompts` | `prompts.json` |
| メモリ保存ファイル | `Paths:Memory` | `memory.json` |
| skill ルートディレクトリ | `Paths:SkillsDirectory` | `skills` |
| 会話ログの保存ディレクトリ | `Paths:ChatHistoryDirectory` | `logs` |
| thread ログの保存ディレクトリ | `Paths:ThreadsDirectory` | 未設定時は `<ChatHistoryDirectory>/threads` |
| sub-agent thread ログの保存ディレクトリ | `Paths:SubAgentThreadsDirectory` | 未設定時は `<ThreadsDirectory>/subagents` |

## Logging フラグ

ログの永続化は用途ごとに個別制御できます。

- `Logging:TranscriptEnabled`: 人間向けの `chat_*.txt` transcript を保存します
- `Logging:ThreadEnabled`: main thread の JSONL と `/thread` replay を有効にします
- `Logging:SubAgentThreadEnabled`: sub-agent 用 JSONL を有効にします
- 旧 `ChatHistory:Enabled` は互換用の親フラグで、新しい `Logging:*Enabled` が未設定のときの既定値として使われます
- `Logging:ThreadEnabled=false` の場合、`/thread` コマンドと current thread の自動作成は無効です
- `Paths:ChatHistoryDirectory` は transcript 保存先であると同時に、`Paths:ThreadsDirectory` 未設定時の基底ディレクトリでもあります

## パス解決の考え方

- `Paths:*` に相対パスを指定した場合は `.ai_chat/settings.json` が置かれたディレクトリから解決されます
- `ThreadsDirectory` が未設定なら `<ChatHistoryDirectory>/threads` を使います
- `SubAgentThreadsDirectory` が未設定なら `<ThreadsDirectory>/subagents` を使います
- このリポジトリ同梱の `.ai_chat/settings.json` では、`agents.json` / `prompts.json` は `src/AiChatCLI/` を参照し、`SkillsDirectory` は `.cursor/skills` を参照します

例:

```json
{
  "Paths": {
    "Agents": "../src/AiChatCLI/agents.json",
    "Prompts": "../src/AiChatCLI/prompts.json",
    "Memory": "state/memory.json",
    "SkillsDirectory": "../.cursor/skills",
    "ChatHistoryDirectory": "artifacts/logs",
    "ThreadsDirectory": "artifacts/threads",
    "SubAgentThreadsDirectory": "artifacts/threads/subagents"
  }
}
```

## agent ごとの tool 定義

利用可能な tool は `appsettings.local.json` ではなく `Paths:Agents` で指定した agent 定義 JSON で agent ごとに定義します。

- 各 agent は `prompt` と `tools` を持つ object で表現します
- `tools` は tool 名の配列です
- `tools: []` にすると、その agent は tool を 1 つも公開しません
- sub-agent は「現在の agent に許可された tool 群」を引き継ぎますが、`sub_agent` 自体は自動的に除外されます
- `skill` を含めると、`Paths:SkillsDirectory` 配下の `SKILL.md` にある skill の `name` / `description` が system prompt に追加され、本文は tool 呼び出し時だけ読み込まれます
- `search` をどれか 1 つでも有効にした場合は `Tavily:ApiKey` または `TAVILY_API_KEY` が必須です

```json
{
  "defaults": {
    "systemPromptPrefix": "Always reply in Japanese."
  },
  "agents": {
    "default": {
      "prompt": "You are a helpful assistant.",
      "tools": ["memory", "sub_agent", "command", "read_file", "skill"]
    },
    "translator": {
      "prompt": "Translate the user's input accurately into Japanese.",
      "tools": []
    },
    "coder": {
      "prompt": "You are an expert programmer.\n\n%SYSTEM_INFO%",
      "tools": ["memory", "sub_agent", "command", "read_file", "skill", "search"]
    }
  }
}
```

利用できる tool 名:

- `memory`
- `read_file`
- `command`
- `sub_agent`
- `skill`
- `search`

### `memory`

Function Calling を使って、設定された memory 保存ファイルに短い長期メモリを保存します。

- 保存先は `Paths:Memory`
- `key: value` 形式の JSON を使います
- 主にユーザー設定、好み、継続中タスクの前提などの短い事実を保持します
- モデルは `upsert` / `get` / `list` / `delete` を内部的に使い分けます
- agent の `tools` に `memory` を含めたときだけ公開します

### `read_file`

ローカルファイルをシェルコマンドなしで読み取るための読み取り専用ツールです。

- main agent / sub-agent の両方へ公開されます
- `path` には絶対パス、または CLI プロセスの現在作業ディレクトリからの相対パスを指定できます
- `start_line` と `end_line` は 1-based / inclusive です
- `end_line` を省略または `0` にすると末尾まで読み取ります
- BOM を判定し、BOM が無い場合は UTF-8 を優先しつつ、必要に応じて Windows 系コードページへフォールバックします
- 結果は `ok`、`status`、`path`、`resolvedPath`、`encoding`、`totalLines`、`startLine`、`endLine`、`content`、`error` などを含む JSON 文字列です

```json
{
  "path": "src/AiChatCLI/README.md",
  "start_line": 1,
  "end_line": 20
}
```

### `search`

Tavily Search API を使う opt-in の検索ツールです。

- agent の `tools` に `search` を含めたときだけ公開されます
- API キーは `Tavily:ApiKey` または `TAVILY_API_KEY` で設定します
- `search_depth` は `basic` が標準、`advanced` は高精度、`fast` / `ultra-fast` は低遅延向けです
- どれかの agent で `search` を有効にしたのに API キーが無い場合は、起動時に設定エラーで停止します

```json
{
  "Tavily": {
    "ApiKey": "tvly-your-tavily-api-key"
  }
}
```

### `skill`

ローカルの `Paths:SkillsDirectory` 配下の `SKILL.md` から skill 本文を遅延読み込みするツールです。

- agent の `tools` に `skill` を含めたときだけ公開されます
- system prompt には各 skill の `name` と `description` だけが追加されます
- モデルが `skill` ツールを呼ぶと、その skill の markdown 本文、`SKILL.md` の絶対パス、skill ディレクトリの絶対パスが JSON で返ります
- front matter は `name` と `description` だけをサポートし、それ以外のキーは未対応です
- skill ファイルは `Paths:SkillsDirectory/<skill-directory>/SKILL.md` に置き、必要なら同じディレクトリへ補助リソースを置けます

```json
{
  "agents": {
    "coder": {
      "prompt": "Use local skills when they match the task.",
      "tools": ["read_file", "skill"]
    }
  }
}
```

```md
---
name: dotnet-test
description: Run focused dotnet build/test commands before finalizing a change.
---
# dotnet-test

1. Run `dotnet build`.
2. Run focused `dotnet test` for touched seams.
```

```json
{
  "agents": {
    "researcher": {
      "prompt": "Search the web before answering.",
      "tools": ["read_file", "search"]
    }
  }
}
```

### `command`

ローカルシェルコマンドの実行を要求できる approval-gated なツールです。

- main agent / sub-agent の両方へ公開されます
- Windows では PowerShell、その他の OS では `/bin/sh -lc` で実行します
- Windows では stdout/stderr を UTF-8 にそろえるため、日本語などの Unicode 出力も崩れにくくしています
- `timeout_seconds` は既定で `120` 秒、最大 `600` 秒です
- 実行前に必ずコンソールへコマンドが表示され、ユーザーが `YES` と入力した場合だけ実行されます
- `NO` の場合は `status: "denied"` と `denied: true` を含む JSON が AI に返ります
- `NO` の理由を入力した場合だけ、その理由が `reason` として AI に渡されます

```text
AI がコマンド実行を要求しています:
dotnet test AiChatCLI.sln
実行しますか? YES/NO: NO
NO の理由 (任意): 今はテストを走らせたくない
```

### `sub_agent`

現在の会話履歴を持たない新しいサブエージェントに作業を委譲します。

- `prompt` だけを user message としてサブエージェントを実行します
- サブエージェントは現在の agent に許可された tool を引き継ぎます
- サブエージェント側の利用可能ツールには `sub_agent` を含めません
- 結果は `subAgentThreadId` と最終回答を含む JSON 文字列です
- agent の `tools` に `sub_agent` を含めたときだけ main agent から公開します

## agent / prompt 定義ファイル

- agent 定義は `Paths:Agents` が既定で参照する JSON です
- `agents.json` は `defaults` と `agents` を持つ structured schema です
- `defaults.systemPromptPrefix` に空でない文字列を設定すると、その内容を各 agent の system prompt 先頭へ共通プレフィックスとして追加します
- 各 `agents.<name>` は `prompt` と `tools` を持つ object です
- prompt template 定義は `Paths:Prompts` が既定で参照する JSON です
- 外部で JSON を直接編集した場合は `/agent reload` または `/prompt template reload` を実行します
- skill 定義は `Paths:SkillsDirectory` 配下の `SKILL.md` が対象で、`/agent reload` や agent 切り替え時に利用可能 skill の name / description が再評価されます

```json
{
  "defaults": {
    "systemPromptPrefix": "Always reply in Japanese."
  },
  "agents": {
    "default": {
      "prompt": "You are a helpful assistant.",
      "tools": ["memory", "sub_agent", "command", "read_file"]
    },
    "coder": {
      "prompt": "You are an expert programmer.\n\n%SYSTEM_INFO%\n\n%CURRENT_DIRECTORY_ENTRIES%",
      "tools": ["memory", "sub_agent", "command", "read_file"]
    }
  }
}
```

- 組み込みプレースホルダーとして `%SYSTEM_INFO%` と `%CURRENT_DIRECTORY_ENTRIES%` を使えます
- `%CURRENT_DIRECTORY_ENTRIES%` はセッション current directory 直下のファイル・フォルダ一覧を展開します

## ログと履歴

trace は `ChatTraceRecorder` から各保存先へ配信されます。起動ごとに transcript を、thread ごとに構造化ログを必要に応じて保存します。

- 保存先は既定で `.ai_chat/logs/`
- transcript は `chat_yyyyMMdd_HHmmss_fff.txt`
- main thread ログは `.ai_chat/logs/threads/thread_*.jsonl`
- サブエージェントログは `.ai_chat/logs/threads/subagents/subagent_thread_*.jsonl`
- transcript には session 開始/終了、通常会話、tool 実行、slash command 出力、agent / thread の状態変更が記録されます
- thread JSONL は replay 正本で、通常会話に加えて slash command、session lifecycle、agent 状態変更も append-only event として保持します
- `Logging:ThreadEnabled=false` では thread 機能と main thread JSONL は無効です
- `Logging:SubAgentThreadEnabled=false` では sub-agent JSONL だけを停止できます

| 種類 | 主な記録契機 | 用途 |
|---|---|---|
| transcript | REPL session 開始/終了、通常会話、slash command、agent / thread 切り替え | 人間向けの追跡とトラブルシュート |
| main thread JSONL | user / assistant、tool call / result、slash command、session / agent lifecycle | `/thread use <id>` の replay 正本 |
| sub-agent thread JSONL | sub-agent への prompt、tool 実行、最終応答、sub-agent session lifecycle | サブエージェント単位の再現と調査 |

詳しい一覧は [`reference.md`](reference.md) を参照してください。
