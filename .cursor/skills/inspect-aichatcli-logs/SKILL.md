---
name: inspect-aichatcli-logs
description: Inspect AiChatCLI chat history and thread logs. Use when debugging src/AiChatCLI behavior, analyzing logs/ chat_*.txt files, thread_*.jsonl files, subagent_thread_*.jsonl files, slash command output, tool calls, command approval results, or thread replay issues.
---

# Inspect AiChatCLI Logs

## When To Use

Use this skill when `src/AiChatCLI` behavior is easier to debug from persisted logs than from terminal output alone:

- a `devtools/cli-session` smoke check returns unexpected `stdout`, `stderr`, `state`, or `timed_out`
- slash command output differs from expectations
- prompt template expansion, agent switching, thread switching, or replay looks wrong
- tool calls, command approvals, command denials, or sub-agent calls need inspection
- a test involves `logs/`, `thread_*.jsonl`, or `subagent_thread_*.jsonl`

## Log Locations

AiChatCLI writes logs relative to the app content root, normally `src/AiChatCLI/`.

- Text chat log: `logs/chat_yyyyMMdd_HHmmss_fff.txt`
- Thread log: `logs/threads/thread_*.jsonl`
- Sub-agent thread log: `logs/threads/subagents/subagent_thread_*.jsonl`

Configuration can override these:

- `Paths:ChatHistoryDirectory`
- `Paths:ThreadsDirectory`
- `Paths:SubAgentThreadsDirectory`
- `ChatHistory:Enabled`

Relative `Paths:*` values resolve from the app content root, not necessarily from the shell working directory.

## Find The Relevant Log

Prefer paths printed by the running app over guessing:

1. On startup, `AiChatCLI` prints `会話ログ: <path>` when text logging is enabled.
2. Send `/thread current` to get the current thread ID and exact JSONL path.
3. Send `/status` to confirm whether current thread is created or disabled.
4. Use `Glob` for `src/AiChatCLI/logs/**/thread_*.jsonl` or `src/AiChatCLI/logs/chat_*.txt` when no path was printed.

Use `ReadFile` for known log paths. For very large logs, read the end first with a negative offset, then widen only as needed.

## Text Chat Log

Use `chat_*.txt` for a chronological, human-readable view of one process run.

Important tags:

- `[SESSION]`: process start and end
- `[USER]`: raw user input
- `[TRANSFORM_RAW]` and `[TRANSFORM_FINAL]`: prompt template processing
- `[REQUEST]`: final text sent to the model
- `[AI]`: assistant reply
- `[SLASH]` and `[SLASH_OUT]`: slash command and captured command output
- `[TOOL]`, `[TOOL_ARGS]`, `[TOOL_RESULT]`: model tool execution details
- `[SUBAGENT]`: sub-agent thread ID linked from a tool result

Start with the text log when diagnosing what a user or `cli-session` observed in one run.

## Thread JSONL Log

Use `thread_*.jsonl` as the source of truth for persisted conversation state and replay.

Each line is one JSON event. Key event types:

- `thread_created`: new thread metadata and initial agent snapshot
- `session_attached` / `session_detached`: process lifecycle around a thread
- `agent_changed`: current agent and system prompt snapshot
- `user_message`: raw input accepted into the thread
- `prompt_transformed`: raw and processed prompt values
- `model_request`: text sent to the model
- `assistant_message`: persisted assistant message
- `tool_call`: assistant tool call request
- `tool_result`: tool response persisted for replay
- `subagent_invoked`: sub-agent thread ID linked to the parent thread

When replay or `/thread use <id>` is involved, inspect this log before the text log. Verify the event sequence, agent snapshots, and tool call/result pairing.

## Sub-Agent Logs

When the parent text log has `[SUBAGENT] thread=<id>` or the parent thread JSONL has `subagent_invoked`, inspect:

```text
src/AiChatCLI/logs/threads/subagents/<sub-agent-thread-id>.jsonl
```

Sub-agent logs use the same JSONL event shape as parent threads. They are independent conversation histories; do not expect parent chat messages to appear there unless explicitly passed in the sub-agent prompt.

## Debugging Order

For `cli-session` driven failures:

1. Check the latest `cli-session` JSON response: `state`, `timed_out`, `stdout`, `stderr`.
2. If startup succeeded, capture the printed `会話ログ` path from `stdout`.
3. Send `/thread current` when possible and capture the exact thread JSONL path.
4. Read the text log for what happened in the process.
5. Read the thread JSONL when the issue concerns replay, persisted message shape, agent switching, prompt transformation, tool calls, or sub-agents.
6. For command approval issues, compare `cli-session` state transitions with `[TOOL]` / `[TOOL_RESULT]` and `tool_call` / `tool_result` entries.
7. For sub-agent issues, follow the `[SUBAGENT]` or `subagent_invoked` thread ID into `logs/threads/subagents/`.

Avoid matching generated timestamps, GUID suffixes, absolute paths, or full model prose unless the bug specifically concerns them.

## Common Clues

- No `会話ログ` line: `ChatHistory:Enabled` may be false, startup may have failed before `ChatLoop`, or stdout was not read yet.
- `/thread current` says history is disabled: thread logs are intentionally unavailable.
- Text log has `[SLASH]` but no `[REQUEST]`: the input was handled as a slash command and was not sent to the model.
- Text log has `[REQUEST]` but no `[AI]`: the model turn likely failed, timed out, or is still running; check `stderr` and `cli-session` state.
- `tool_call` without matching `tool_result`: inspect command approval flow or tool execution failure.
- Parent log has `subagent_invoked` but no expected answer: inspect the matching sub-agent JSONL.
