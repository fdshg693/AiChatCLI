# CLI Session

`cli-session` は、対話的な CLI プロセスをセッション ID 付きで保持し、AI ツールから単発コマンドとして操作できるようにする Python ラッパーです。

主な用途は `AiChatCLI` のように標準入力を待つ CLI を、テストや自動確認から次の形で扱うことです。

1. 指定 ID で CLI を起動する
2. 別コマンドから同じ ID に 1 行入力する
3. 次の入力待ち、承認待ち、終了、または timeout までの出力を JSON で受け取る

## セットアップ

PowerShell 例:

```powershell
cd devtools/cli-session
py -3 -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
```

インストールせずに、このディレクトリから `python -m cli_session ...` として実行することもできます。

## AiChatCLI のラップ例

リポジトリルートから `AiChatCLI` を起動する例です。

```powershell
cd devtools/cli-session
python -m cli_session start --id main --cwd ../.. -- dotnet run --project src/AiChatCLI/AiChatCLI.csproj
```

同じセッションへ入力します。

```powershell
python -m cli_session send --id main "こんにちは"
python -m cli_session send --id main "/status"
python -m cli_session read --id main
```

終了します。

```powershell
python -m cli_session stop --id main
```

複数セッションは ID で切り替えます。

```powershell
python -m cli_session start --id agent-a --cwd ../.. -- dotnet run --project src/AiChatCLI/AiChatCLI.csproj
python -m cli_session start --id agent-b --cwd ../.. -- dotnet run --project src/AiChatCLI/AiChatCLI.csproj
python -m cli_session send --id agent-a "/thread current"
python -m cli_session send --id agent-b "/agent list"
```

## コマンド

- `start --id <id> -- <command...>`: 指定 ID で子プロセスを起動します。
- `send --id <id> <text>`: 指定 ID の stdin に 1 行送ります。
- `read --id <id>`: 入力せずに未読の stdout/stderr を取得します。
- `list`: manager が保持しているセッション一覧を表示します。
- `stop --id <id>`: まず `exit` を送り、終了しなければプロセスを停止します。
- `stop-all`: 全セッションを停止します。

既定では JSON を出力します。`--text` を付けると stdout/stderr をそのまま表示します。

## JSON 出力

`start` / `send` / `read` / `stop` は、おおむね次の形を返します。

```json
{
  "ok": true,
  "id": "main",
  "state": "ready",
  "stdout": "defaultエージェント> ...",
  "stderr": "",
  "exit_code": null,
  "timed_out": false,
  "command": ["dotnet", "run", "--project", "src/AiChatCLI/AiChatCLI.csproj"],
  "cwd": "C:\\CodeRoot\\AiChatCLI"
}
```

`state` は次のいずれかです。

- `ready`: 通常の入力待ちです。既定では stdout が `エージェント> ` で終わったときに検出します。
- `approval`: コマンド承認の `実行しますか? YES/NO: ` 待ちです。
- `denial_reason`: `NO の理由 (任意): ` 待ちです。
- `running`: 次の待機状態に到達していません。
- `exited`: 子プロセスが終了しました。

`timed_out: true` の場合、プロセスは継続しているため、後続の `read --id <id> --timeout <seconds>` で続きの出力を取得できます。

## 待機検出の上書き

既定の suffix は `AiChatCLI` 向けです。ほかの CLI を包む場合は `start` 時に上書きできます。

```powershell
python -m cli_session start --id demo --ready-suffix "ready> " -- python my_repl.py
```

## テスト

```powershell
cd devtools/cli-session
python -m unittest discover -s tests
```

## 注意

- 子プロセスは stdin/stdout/stderr を pipe で接続します。TTY 専用 UI は対象外です。
- `AiChatCLI` は stdin がリダイレクトされると行入力モードになるため、このツールから操作できます。
- manager との接続情報は既定で OS の一時ディレクトリに保存されます。必要なら `--state-dir` または `CLI_SESSION_STATE_DIR` で変更できます。
