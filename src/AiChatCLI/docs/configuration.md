# AiChatCLI Configuration Guide

`AiChatCLI` の設定ファイル、環境変数、agent ごとの tool 定義、ログ保存先をまとめたガイドです。日常利用の操作は [`usage.md`](usage.md)、一覧性を重視した要約は [`reference.md`](reference.md) を参照してください。

## セットアップ

1. `appsettings.local.example.json` を `appsettings.local.json` にコピーします
2. `appsettings.local.json` の `OpenAI:ApiKey` に API キーを設定します
3. Tavily 検索を使う場合は `Tavily:ApiKey` も設定します
4. `appsettings.local.json` は `.gitignore` 済みで Git 管理しません

```bash
dotnet build
dotnet run --project src/AiChatCLI/AiChatCLI.csproj
```

テスト:

```bash
dotnet test AiChatCLI.sln
```

## 設定の読み込み順

設定は次の順で読み込まれ、後ろのものが前を上書きします。

1. `appsettings.json`
2. `appsettings.local.json`
3. 環境変数

代表例:

- `OPENAI_API_KEY`
- `TAVILY_API_KEY`
- `ChatHistory__Enabled`
- `Paths__ChatHistoryDirectory`

## 主な設定項目

| 項目 | 設定方法 | デフォルト値 |
|---|---|---|
| OpenAI API キー | `OpenAI:ApiKey` または `OPENAI_API_KEY` | 必須 |
| Tavily API キー | `Tavily:ApiKey` または `TAVILY_API_KEY` | 未設定 |
| モデル | `OpenAI:Model` | `gpt-4o-mini` |
| テンプレート展開の最大深度 | `Template:MaxDepth` | `10` |
| 会話ログの有効化 | `ChatHistory:Enabled` | `true` |
| agent 定義ファイル | `Paths:Agents` | `agents.json` |
| テンプレート定義ファイル | `Paths:Prompts` | `prompts.json` |
| メモリ保存ファイル | `Paths:Memory` | `memory.json` |
| 会話ログの保存ディレクトリ | `Paths:ChatHistoryDirectory` | `logs` |
| thread ログの保存ディレクトリ | `Paths:ThreadsDirectory` | 未設定時は `<ChatHistoryDirectory>/threads` |
| sub-agent thread ログの保存ディレクトリ | `Paths:SubAgentThreadsDirectory` | 未設定時は `<ThreadsDirectory>/subagents` |

## パス解決の考え方

- `Paths:*` に相対パスを指定した場合は `appsettings.json` と同じ基準ディレクトリから解決されます
- `ChatHistory:Directory` も互換性のため読み取りますが、`Paths:ChatHistoryDirectory` が優先されます
- `ThreadsDirectory` が未設定なら `<ChatHistoryDirectory>/threads` を使います
- `SubAgentThreadsDirectory` が未設定なら `<ThreadsDirectory>/subagents` を使います

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
  }
}
```

## agent ごとの tool 定義

利用可能な tool は `appsettings.local.json` ではなく `agents.json` で agent ごとに定義します。

- 各 agent は `prompt` と `tools` を持つ object で表現します
- `tools` は tool 名の配列です
- `tools: []` にすると、その agent は tool を 1 つも公開しません
- sub-agent は「現在の agent に許可された tool 群」を引き継ぎますが、`sub_agent` 自体は自動的に除外されます
- `search` をどれか 1 つでも有効にした場合は `Tavily:ApiKey` または `TAVILY_API_KEY` が必須です

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
    "translator": {
      "prompt": "Translate the user's input accurately into Japanese.",
      "tools": []
    },
    "coder": {
      "prompt": "You are an expert programmer.\n\n%SYSTEM_INFO%",
      "tools": ["memory", "sub_agent", "command", "read_file", "search"]
    }
  }
}
```

利用できる tool 名:

- `memory`
- `read_file`
- `command`
- `sub_agent`
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

- agent 定義は `agents.json` が既定です
- `agents.json` は `defaults` と `agents` を持つ structured schema です
- `defaults.systemPromptPrefix` に空でない文字列を設定すると、その内容を各 agent の system prompt 先頭へ共通プレフィックスとして追加します
- 各 `agents.<name>` は `prompt` と `tools` を持つ object です
- prompt template 定義は `prompts.json` が既定です
- 外部で JSON を直接編集した場合は `/agent reload` または `/prompt template reload` を実行します

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

起動ごとに 1 ファイルのテキストログと、thread ごとの構造化ログを保存します。

- 保存先は既定で `logs/`
- テキストログは `chat_yyyyMMdd_HHmmss_fff.txt`
- thread ログは `logs/threads/thread_*.jsonl`
- サブエージェントログは `logs/threads/subagents/subagent_thread_*.jsonl`
- `ChatHistory:Enabled` を `false` にすると thread 機能と structured thread log は無効になります

詳しい一覧は [`reference.md`](reference.md) を参照してください。
