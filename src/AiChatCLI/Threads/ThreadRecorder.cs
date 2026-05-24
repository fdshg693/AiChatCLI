using System.Text.Json;

namespace AiChatCLI;

public sealed class ThreadRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ThreadRepository _repository;
    private readonly ConversationCodec _conversationCodec;
    private readonly string _sessionId;

    public ThreadRecorder(ThreadRepository repository, ConversationCodec conversationCodec, string sessionId)
    {
        _repository = repository;
        _conversationCodec = conversationCodec;
        _sessionId = sessionId;
    }

    public void RecordSessionAttached(
        string threadId,
        string modelName,
        string agentName,
        string systemPrompt,
        string reason) =>
        Append(ThreadEvent.SessionAttached(
            threadId,
            _sessionId,
            modelName,
            agentName,
            systemPrompt,
            reason));

    public void RecordSessionDetached(string threadId, string reason) =>
        Append(ThreadEvent.SessionDetached(threadId, _sessionId, reason));

    public void RecordUserInput(string threadId, string agentName, string rawInput) =>
        Append(ThreadEvent.UserMessage(threadId, _sessionId, agentName, rawInput));

    public void RecordPromptTransformation(
        string threadId,
        string agentName,
        string rawInput,
        string processedInput) =>
        Append(ThreadEvent.PromptTransformed(
            threadId,
            _sessionId,
            agentName,
            rawInput,
            processedInput));

    public void RecordModelRequest(string threadId, string agentName, string processedInput) =>
        Append(ThreadEvent.ModelRequest(threadId, _sessionId, agentName, processedInput));

    public void RecordTurn(string threadId, string agentName, ChatTurnResult turnResult)
    {
        foreach (var message in turnResult.ResponseMessages)
        {
            switch (message.Kind)
            {
                case ThreadMessageKind.Text:
                    Append(ThreadEvent.AssistantMessage(threadId, _sessionId, agentName, message));
                    break;
                case ThreadMessageKind.ToolCallAggregate:
                    _conversationCodec.SplitToolCallAggregate(message, out var toolCalls, out var toolResults);
                    Append(ThreadEvent.ToolCall(
                        threadId,
                        _sessionId,
                        agentName,
                        message.From,
                        message.Content,
                        toolCalls));
                    Append(ThreadEvent.ToolResult(
                        threadId,
                        _sessionId,
                        agentName,
                        message.From,
                        toolResults));
                    RecordSubAgentInvocations(threadId, agentName, toolResults);
                    break;
            }
        }
    }

    public void RecordSubAgentInvocation(
        string threadId,
        string agentName,
        string subAgentThreadId,
        string? toolCallId) =>
        Append(ThreadEvent.SubAgentInvoked(threadId, _sessionId, agentName, subAgentThreadId, toolCallId));

    public void RecordAgentChange(string threadId, string agentName, string systemPrompt, string reason) =>
        Append(ThreadEvent.AgentChanged(
            threadId,
            _sessionId,
            agentName,
            systemPrompt,
            reason));

    private void Append(ThreadEvent threadEvent)
    {
        _repository.AppendEvent(threadEvent);
    }

    private void RecordSubAgentInvocations(
        string threadId,
        string agentName,
        IReadOnlyList<ThreadToolCallRecord> toolResults)
    {
        foreach (var toolResult in toolResults)
        {
            if (!string.Equals(toolResult.FunctionName, SubAgentTools.FunctionName, StringComparison.Ordinal))
                continue;

            var response = TryReadSubAgentResponse(toolResult.Result);
            if (string.IsNullOrWhiteSpace(response?.SubAgentThreadId))
                continue;

            RecordSubAgentInvocation(
                threadId,
                agentName,
                response.SubAgentThreadId,
                toolResult.ToolCallId);
        }
    }

    private static SubAgentToolResponse? TryReadSubAgentResponse(string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SubAgentToolResponse>(result, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
