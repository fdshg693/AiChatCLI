namespace AiChatCLI;

internal sealed record ThreadEvent
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string Type { get; init; } = string.Empty;
    public string ThreadId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string? SessionId { get; init; }
    public string? ModelName { get; init; }
    public string? AgentName { get; init; }
    public string? SystemPrompt { get; init; }
    public string? MessageFrom { get; init; }
    public string? Reason { get; init; }
    public string? RawInput { get; init; }
    public string? ProcessedInput { get; init; }
    public string? Content { get; init; }
    public ThreadMessageRecord? Message { get; init; }
    public IReadOnlyList<ThreadToolCallRecord>? ToolCalls { get; init; }
    public string? SubAgentThreadId { get; init; }
    public string? ToolCallId { get; init; }

    public static ThreadEvent ThreadCreated(string threadId, string modelName, string agentName, string systemPrompt) =>
        Create("thread_created", threadId) with
        {
            ModelName = modelName,
            AgentName = agentName,
            SystemPrompt = systemPrompt
        };

    public static ThreadEvent SessionAttached(
        string threadId,
        string sessionId,
        string modelName,
        string agentName,
        string systemPrompt,
        string reason) =>
        Create("session_attached", threadId) with
        {
            SessionId = sessionId,
            ModelName = modelName,
            AgentName = agentName,
            SystemPrompt = systemPrompt,
            Reason = reason
        };

    public static ThreadEvent SessionDetached(string threadId, string sessionId, string reason) =>
        Create("session_detached", threadId) with
        {
            SessionId = sessionId,
            Reason = reason
        };

    public static ThreadEvent AgentChanged(
        string threadId,
        string sessionId,
        string agentName,
        string systemPrompt,
        string reason) =>
        Create("agent_changed", threadId) with
        {
            SessionId = sessionId,
            AgentName = agentName,
            SystemPrompt = systemPrompt,
            Reason = reason
        };

    public static ThreadEvent UserMessage(string threadId, string sessionId, string agentName, string rawInput) =>
        Create("user_message", threadId) with
        {
            SessionId = sessionId,
            AgentName = agentName,
            RawInput = rawInput
        };

    public static ThreadEvent PromptTransformed(
        string threadId,
        string sessionId,
        string agentName,
        string rawInput,
        string processedInput) =>
        Create("prompt_transformed", threadId) with
        {
            SessionId = sessionId,
            AgentName = agentName,
            RawInput = rawInput,
            ProcessedInput = processedInput
        };

    public static ThreadEvent ModelRequest(string threadId, string sessionId, string agentName, string processedInput) =>
        Create("model_request", threadId) with
        {
            SessionId = sessionId,
            AgentName = agentName,
            ProcessedInput = processedInput
        };

    public static ThreadEvent AssistantMessage(
        string threadId,
        string sessionId,
        string agentName,
        ThreadMessageRecord message) =>
        Create("assistant_message", threadId) with
        {
            SessionId = sessionId,
            AgentName = agentName,
            Message = message
        };

    public static ThreadEvent ToolCall(
        string threadId,
        string sessionId,
        string agentName,
        string? messageFrom,
        string? content,
        IReadOnlyList<ThreadToolCallRecord> toolCalls) =>
        Create("tool_call", threadId) with
        {
            SessionId = sessionId,
            AgentName = agentName,
            MessageFrom = messageFrom,
            Content = content,
            ToolCalls = toolCalls
        };

    public static ThreadEvent ToolResult(
        string threadId,
        string sessionId,
        string agentName,
        string? messageFrom,
        IReadOnlyList<ThreadToolCallRecord> toolCalls) =>
        Create("tool_result", threadId) with
        {
            SessionId = sessionId,
            AgentName = agentName,
            MessageFrom = messageFrom,
            ToolCalls = toolCalls
        };

    public static ThreadEvent SubAgentInvoked(
        string threadId,
        string sessionId,
        string agentName,
        string subAgentThreadId,
        string? toolCallId) =>
        Create("subagent_invoked", threadId) with
        {
            SessionId = sessionId,
            AgentName = agentName,
            SubAgentThreadId = subAgentThreadId,
            ToolCallId = toolCallId
        };

    private static ThreadEvent Create(string type, string threadId) =>
        new()
        {
            Type = type,
            ThreadId = threadId,
            Timestamp = DateTimeOffset.UtcNow
        };
}
