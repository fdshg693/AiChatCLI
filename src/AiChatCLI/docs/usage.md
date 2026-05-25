# AiChatCLI Usage Guide

`AiChatCLI` を対話型 CLI として日常利用するためのガイドです。セットアップや設定ファイルの詳細は [`configuration.md`](configuration.md)、実装改善時の内部構成は [`development.md`](development.md) を参照してください。

## 起動後の基本操作

起動すると、現在のモデル名と選択中エージェント名を含むプロンプトが表示されます。

```text
AI Chat CLI (model: gpt-4o-mini, agent: default, exitで終了)
defaultエージェント> こんにちは
defaultエージェント> こんにちは！何かお手伝いできることはありますか？
defaultエージェント> exit
```

- 通常の入力は AI へ送信されます
- `exit` と入力すると終了します
- `Ctrl+V` または `Shift+Insert` で clipboard の文字列を貼り付けできます
- 複数行テキストもそのまま保持されます
- `ChatHistory:Enabled` が有効なら、起動時に空の current thread が 1 つ自動作成されます

## スラッシュコマンド

`/` で始まる入力はコマンドとして処理されます。

| コマンド | 用途 |
|---|---|
| `/help` | コマンド体系と利用可能な操作を表示 |
| `/status` | 現在のモデル、選択中 agent、現在の agent / sub-agent に公開される tool、current thread、定義ファイル、memory 状態などを表示 |
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

`@キー名` を入力の先頭に付けると、定義済みテンプレートに展開されてから AI に送信されます。文中でも `@キー名` の前が空白または行頭であれば展開対象になります。該当テンプレートがなければ通常文字として扱われます。

`@` を単独で入力した直後は候補一覧が表示され、上下キーで選択し、`Tab` または `Enter` で確定できます。

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

既定では `agents.json` で追加・編集でき、CLI からは `/agent ...` で操作します。`/agent list` と `/agent show <名前>` では、その agent に許可された tool も確認できます。外部で JSON を直接編集した場合は `/agent reload` で再読み込みできます。保存先は `Paths:Agents` で変更できます。

`agents.json` は `defaults` と `agents` を持つ structured schema です。`defaults.systemPromptPrefix` に空でない値を設定すると、その内容が各 agent の system prompt 先頭に共通プレフィックスとして追加されます。各 agent は `prompt` と `tools` を持ち、利用可能な tool は agent ごとに切り替えられます。

`tools` に `skill` を含めると、`skills/*/SKILL.md` にある skill の `name` / `description` だけが system prompt に追加されます。本文は必要になったときだけ `skill` ツール呼び出しで取得されます。

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
    "coder": {
      "prompt": "You are an expert programmer.\n\n%SYSTEM_INFO%\n\n%CURRENT_DIRECTORY_ENTRIES%",
      "tools": ["memory", "sub_agent", "command", "read_file", "skill"]
    }
  }
}
```

agent prompt 内で `%KEY%` を使うと、アプリ組み込み値または同じ `agents` オブジェクト内の別 agent prompt を参照できます。アプリ組み込み値が同名の agent より優先されます。未定義の `%KEY%` はそのまま残ります。既定の組み込み値は `%SYSTEM_INFO%` と `%CURRENT_DIRECTORY_ENTRIES%` です。`%SYSTEM_INFO%` は OS、.NET runtime、command ツールが使うシェルと文字コード設定を含み、`%CURRENT_DIRECTORY_ENTRIES%` はセッション current directory 直下のファイル・フォルダ一覧を含みます。`defaults.systemPromptPrefix` の値でも同じ組み込みプレースホルダー展開を使えます。sub-agent は現在の agent が許可された tool を引き継ぎますが、`sub_agent` 自体は再帰呼び出し防止のため公開されません。

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

## 次に読むべき文書

- agent ごとの tool 定義、`appsettings.local.json`、`Paths:*` は [`configuration.md`](configuration.md)
- 内部構成、拡張ポイント、テスト方針は [`development.md`](development.md)
- コマンド、ツール、ログ出力先の一覧は [`reference.md`](reference.md)
