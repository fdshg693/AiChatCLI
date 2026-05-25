# AiChatCLI Skills

`AiChatCLI` の skill は `.ai_chat/settings.json` で指定した `Paths:SkillsDirectory/<skill-directory>/SKILL.md` に配置します。

最小構成:

```md
---
name: dotnet-test
description: Run focused dotnet build/test commands before finalizing a change.
---
# dotnet-test

1. Run `dotnet build`.
2. Run focused `dotnet test` commands for the touched seams.
```

注意点:

- front matter で使えるキーは `name` と `description` のみです
- AI に最初に渡るのは `name` と `description` だけです
- 本文は `skill` ツールが呼ばれたときだけ読み込まれ、`SKILL.md` と skill ディレクトリの絶対パスも返ります
- `Paths:SkillsDirectory` に相対パスを指定した場合は `.ai_chat/settings.json` からの相対パスで解決されます
- 画像や補助テキストなどのリソースを使う場合は、同じ skill ディレクトリへ置いて `SKILL.md` から相対参照できます
