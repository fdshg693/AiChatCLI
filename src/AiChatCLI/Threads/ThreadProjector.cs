using AutoGen.Core;

namespace AiChatCLI;

public sealed class ThreadProjector
{
    private const string DefaultAgentName = "default";
    private const string DefaultSystemPrompt = "You are a helpful assistant.";
    private readonly ConversationCodec _conversationCodec;

    public ThreadProjector(ConversationCodec? conversationCodec = null)
    {
        _conversationCodec = conversationCodec ?? new ConversationCodec();
    }

    public ThreadSnapshot Project(string filePath, IReadOnlyList<ThreadEvent> events)
    {
        if (events.Count == 0)
            throw new InvalidDataException($"スレッドログが空です: {filePath}");

        var threadId = events[0].ThreadId;
        var currentAgentName = DefaultAgentName;
        var currentSystemPrompt = DefaultSystemPrompt;
        string? modelName = null;
        var createdAt = events[0].Timestamp;
        var lastUpdatedAt = events[0].Timestamp;
        var conversation = new List<ThreadMessageRecord>();
        ThreadEvent? pendingToolCall = null;

        foreach (var threadEvent in events)
        {
            lastUpdatedAt = threadEvent.Timestamp;

            switch (threadEvent.Type)
            {
                case "thread_created":
                    createdAt = threadEvent.Timestamp;
                    ApplyAgentState(threadEvent, ref currentAgentName, ref currentSystemPrompt);
                    modelName = threadEvent.ModelName ?? modelName;
                    break;
                case "session_attached":
                case "agent_changed":
                    ApplyAgentState(threadEvent, ref currentAgentName, ref currentSystemPrompt);
                    modelName = threadEvent.ModelName ?? modelName;
                    break;
                case "model_request":
                    conversation.Add(ThreadMessageRecord.CreateText(
                        Role.User,
                        threadEvent.ProcessedInput ?? string.Empty));
                    break;
                case "assistant_message":
                    if (threadEvent.Message is not null)
                        conversation.Add(threadEvent.Message);
                    break;
                case "tool_call":
                    pendingToolCall = threadEvent;
                    break;
                case "tool_result":
                    conversation.Add(BuildToolAggregate(pendingToolCall, threadEvent));
                    pendingToolCall = null;
                    break;
                case "subagent_invoked":
                    break;
            }
        }

        if (pendingToolCall?.ToolCalls is { Count: > 0 } toolCalls)
        {
            conversation.Add(_conversationCodec.CreateToolCallAggregateRecord(
                toolCalls,
                null,
                pendingToolCall.Content,
                pendingToolCall.MessageFrom));
        }

        return new ThreadSnapshot(
            threadId,
            currentAgentName,
            currentSystemPrompt,
            modelName,
            createdAt,
            lastUpdatedAt,
            conversation,
            filePath);
    }

    public ThreadSummary CreateSummary(ThreadSnapshot snapshot) =>
        new(
            snapshot.ThreadId,
            snapshot.CurrentAgentName,
            snapshot.CurrentSystemPrompt,
            snapshot.ModelName,
            snapshot.CreatedAt,
            snapshot.LastUpdatedAt,
            snapshot.MessageCount,
            snapshot.FilePath);

    private static void ApplyAgentState(
        ThreadEvent threadEvent,
        ref string currentAgentName,
        ref string currentSystemPrompt)
    {
        if (!string.IsNullOrWhiteSpace(threadEvent.AgentName))
            currentAgentName = threadEvent.AgentName.Trim();

        if (threadEvent.SystemPrompt is not null)
            currentSystemPrompt = threadEvent.SystemPrompt;
    }

    private ThreadMessageRecord BuildToolAggregate(ThreadEvent? toolCallEvent, ThreadEvent toolResultEvent)
    {
        return _conversationCodec.CreateToolCallAggregateRecord(
            toolCallEvent?.ToolCalls,
            toolResultEvent.ToolCalls,
            toolCallEvent?.Content,
            toolCallEvent?.MessageFrom ?? toolResultEvent.MessageFrom);
    }
}
