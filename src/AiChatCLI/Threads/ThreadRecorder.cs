namespace AiChatCLI;

internal sealed class ThreadRecorder
{
    private readonly ThreadRepository _repository;
    private readonly ConversationCodec _conversationCodec;
    private readonly string _sessionId;

    public ThreadRecorder(ThreadRepository repository, ConversationCodec conversationCodec, string sessionId)
    {
        _repository = repository;
        _conversationCodec = conversationCodec;
        _sessionId = sessionId;
    }

    public void RecordSessionStarted(
        string threadId,
        string modelName,
        string agentName,
        string systemPrompt,
        DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.SessionStarted(
            threadId,
            _sessionId,
            modelName,
            agentName,
            systemPrompt,
            timestamp));

    public void RecordSessionEnded(
        string threadId,
        string modelName,
        string agentName,
        string systemPrompt,
        string reason,
        DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.SessionEnded(
            threadId,
            _sessionId,
            modelName,
            agentName,
            systemPrompt,
            reason,
            timestamp));

    public void RecordSessionAttached(
        string threadId,
        string modelName,
        string agentName,
        string systemPrompt,
        string reason,
        DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.SessionAttached(
            threadId,
            _sessionId,
            modelName,
            agentName,
            systemPrompt,
            reason,
            timestamp));

    public void RecordSessionDetached(string threadId, string reason, DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.SessionDetached(threadId, _sessionId, reason, timestamp));

    public void RecordUserInput(string threadId, string agentName, string rawInput, DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.UserMessage(threadId, _sessionId, agentName, rawInput, timestamp));

    public void RecordPromptTransformation(
        string threadId,
        string agentName,
        string rawInput,
        string processedInput,
        DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.PromptTransformed(
            threadId,
            _sessionId,
            agentName,
            rawInput,
            processedInput,
            timestamp));

    public void RecordModelRequest(string threadId, string agentName, string processedInput, DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.ModelRequest(threadId, _sessionId, agentName, processedInput, timestamp));

    public void RecordSlashCommand(
        string threadId,
        string agentName,
        string rawInput,
        string capturedConsoleOutput,
        DateTimeOffset? timestamp = null)
    {
        Append(ThreadEvent.SlashCommand(threadId, _sessionId, agentName, rawInput, timestamp));

        if (string.IsNullOrEmpty(capturedConsoleOutput))
            return;

        var normalized = capturedConsoleOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        foreach (var line in normalized.Split('\n', StringSplitOptions.None))
            Append(ThreadEvent.SlashCommandOutput(threadId, _sessionId, agentName, line, timestamp));
    }

    public void RecordTurn(string threadId, string agentName, ChatTurnResult turnResult, DateTimeOffset? timestamp = null)
    {
        foreach (var message in turnResult.ResponseMessages)
        {
            switch (message.Kind)
            {
                case ThreadMessageKind.Text:
                    Append(ThreadEvent.AssistantMessage(threadId, _sessionId, agentName, message, timestamp));
                    break;
                case ThreadMessageKind.ToolCallAggregate:
                    _conversationCodec.SplitToolCallAggregate(message, out var toolCalls, out var toolResults);
                    Append(ThreadEvent.ToolCall(
                        threadId,
                        _sessionId,
                        agentName,
                        message.From,
                        message.Content,
                        toolCalls,
                        timestamp));
                    Append(ThreadEvent.ToolResult(
                        threadId,
                        _sessionId,
                        agentName,
                        message.From,
                        toolResults,
                        timestamp));
                    RecordSubAgentInvocations(threadId, agentName, toolResults, timestamp);
                    break;
            }
        }
    }

    public void RecordSubAgentInvocation(
        string threadId,
        string agentName,
        string subAgentThreadId,
        string? toolCallId,
        DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.SubAgentInvoked(threadId, _sessionId, agentName, subAgentThreadId, toolCallId, timestamp));

    public void RecordAgentChange(string threadId, string agentName, string systemPrompt, string reason, DateTimeOffset? timestamp = null) =>
        Append(ThreadEvent.AgentChanged(
            threadId,
            _sessionId,
            agentName,
            systemPrompt,
            reason,
            timestamp));

    private void Append(ThreadEvent threadEvent)
    {
        _repository.AppendEvent(threadEvent);
    }

    private void RecordSubAgentInvocations(
        string threadId,
        string agentName,
        IReadOnlyList<ThreadToolCallRecord> toolResults,
        DateTimeOffset? timestamp)
    {
        foreach (var toolResult in toolResults)
        {
            if (!string.Equals(toolResult.FunctionName, SubAgentTools.FunctionName, StringComparison.Ordinal))
                continue;

            var response = SubAgentToolResponseParser.TryParse(toolResult.Result);
            if (string.IsNullOrWhiteSpace(response?.SubAgentThreadId))
                continue;

            RecordSubAgentInvocation(
                threadId,
                agentName,
                response.SubAgentThreadId,
                toolResult.ToolCallId,
                timestamp);
        }
    }
}
