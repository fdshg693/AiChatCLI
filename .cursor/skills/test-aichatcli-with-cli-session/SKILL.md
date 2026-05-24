---
name: test-aichatcli-with-cli-session
description: Test AiChatCLI interactive behavior through devtools/cli-session. Use when checking src/AiChatCLI REPL behavior, slash commands, command approval prompts, or end-to-end CLI turns with a persistent stdin/stdout session.
---

# Test AiChatCLI With CLI Session

## When To Use

Use `devtools/cli-session` when the behavior under test needs a live `src/AiChatCLI` process that waits for stdin:

- prompt rendering and readiness after startup
- slash commands such as `/status`, `/agent list`, `/thread current`, `/prompt template list`
- multi-turn state changes across inputs
- command tool approval states: `approval` and `denial_reason`
- smoke checks that focused xUnit tests do not cover

For pure unit or component coverage, run `dotnet test AiChatCLI.sln` instead.

## Required Context

Before changing behavior, read the nearest docs:

- `README.md`
- `src/AiChatCLI/README.md`
- `devtools/cli-session/README.md`

Keep user-facing behavior documented in the nearest README when it changes.

## Companion Log Skill

When a `cli-session` run fails, times out, returns unexpected `stdout` / `stderr`, or needs deeper debugging, also use `inspect-aichatcli-logs`.

That skill covers how to locate and inspect:

- startup text logs printed as `会話ログ: <path>`
- current thread JSONL paths from `/thread current`
- `logs/chat_*.txt`
- `logs/threads/thread_*.jsonl`
- `logs/threads/subagents/subagent_thread_*.jsonl`

Use the log skill before changing code when the failure could be explained by prompt transformation, slash command handling, thread replay, tool calls, command approval, or sub-agent execution.

## Setup

Run `cli-session` commands from `devtools/cli-session` unless the editable package is already installed:

```powershell
cd devtools/cli-session
py -3 -m venv .venv
.venv/Scripts/Activate.ps1
python -m pip install -e .
```

The app needs normal `AiChatCLI` configuration. Prefer local config or environment variables already used by the repo:

- `src/AiChatCLI/appsettings.local.json`
- `OPENAI_API_KEY`
- `TAVILY_API_KEY` only when testing the search tool

## Basic Test Flow

Use a unique session ID per task. Stop a stale session before reusing an ID:

```powershell
python -m cli_session list
python -m cli_session stop --id aichatcli-test
```

Start `AiChatCLI` from the repository root by setting `--cwd ../..`:

```powershell
python -m cli_session start --id aichatcli-test --cwd ../.. --timeout 60 -- dotnet run --project src/AiChatCLI/AiChatCLI.csproj
```

Inspect the JSON response:

- `ok: true` means the wrapper command succeeded.
- `state: "ready"` means `AiChatCLI` is waiting for the next input.
- `state: "approval"` means the app is waiting for `YES` or `NO`.
- `state: "denial_reason"` means the app is waiting for an optional denial reason.
- `timed_out: true` means the process is still alive; follow with `read --timeout`.
- Always check both `stdout` and `stderr`.

Send inputs one line at a time:

```powershell
python -m cli_session send --id aichatcli-test "/status" --timeout 30
python -m cli_session send --id aichatcli-test "/agent list" --timeout 30
python -m cli_session read --id aichatcli-test --timeout 5
```

Stop the session at the end:

```powershell
python -m cli_session stop --id aichatcli-test
```

## Recommended Smoke Checks

For a quick interactive regression check:

1. Run `dotnet build` or `dotnet test AiChatCLI.sln` first when code changed.
2. Start a fresh `cli-session` session.
3. Send `/status` and verify model, current agent, tools, and thread information are printed.
4. Send `/agent list` and verify default agents are visible.
5. Send `/thread current` when chat history is enabled and verify a thread ID or thread path is shown.
6. Send `exit` or run `stop --id`.

Prefer assertions based on stable substrings in `stdout`; avoid matching timestamps, generated thread IDs, or absolute log paths unless the change specifically concerns them.

## Command Approval Checks

When testing the agent-callable `command` tool, the normal state progression is:

1. A model response requests command execution.
2. `cli-session` returns `state: "approval"` and `stdout` ends with `実行しますか? YES/NO: `.
3. Send `NO` to test denial handling.
4. If the next response is `state: "denial_reason"`, send either a reason or an empty input.
5. Verify the conversation returns to `state: "ready"`.

Example denial sequence:

```powershell
python -m cli_session send --id aichatcli-test "dotnet test を実行して" --timeout 60
python -m cli_session send --id aichatcli-test "NO" --timeout 10
python -m cli_session send --id aichatcli-test "テスト実行は今は不要" --timeout 30
```

Only send `YES` when running the requested command is safe and relevant to the test.

## Troubleshooting

- If `start` returns `timed_out: true`, use `read --id <id> --timeout 30`; first `dotnet run` may spend time restoring or building.
- If a session ID already exists, run `stop --id <id>` or choose a new ID.
- If output stays `running`, read `stderr` and check whether app configuration or API keys are missing.
- If prompt detection breaks after changing the prompt text, update `devtools/cli-session` defaults or pass `--ready-suffix`.
- If a test leaves a process behind, run `python -m cli_session stop-all`.
