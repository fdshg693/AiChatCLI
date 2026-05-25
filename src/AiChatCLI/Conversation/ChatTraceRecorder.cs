namespace AiChatCLI;

internal interface IChatTraceRecorder
{
    string? TranscriptFilePath { get; }
    bool HasPersistentTrace { get; }

    void RecordSessionStart(string modelName, string agentName);
    void RecordSessionEnd(string agentName, string reason = "exit");
    void RecordUserInput(string rawInput, string agentName);
    void RecordPromptTransformation(string rawInput, string processedInput, string agentName);
    void RecordModelRequest(string processedInput, string agentName);
    void RecordTurn(ChatTurnResult turnResult, string agentName);
    void RecordSlashCommand(string rawInput, string capturedConsoleOutput, string agentName);
    void RecordAgentChanged(string agentName, string reason);
    void RecordThreadChanged(string threadId, string agentName, string reason);
}

internal sealed class ChatTraceRecorder : IChatTraceRecorder
{
    private readonly ChatHistoryLogger _chatHistory;
    private readonly ThreadSessionManager _threadSessionManager;

    public ChatTraceRecorder(
        ChatHistoryLogger chatHistory,
        ThreadSessionManager threadSessionManager)
    {
        _chatHistory = chatHistory;
        _threadSessionManager = threadSessionManager;
    }

    public string? TranscriptFilePath => _chatHistory.LogFilePath;

    public bool HasPersistentTrace => _chatHistory.IsEnabled || _threadSessionManager.IsEnabled;

    public void RecordSessionStart(string modelName, string agentName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var threadId = _threadSessionManager.CurrentThreadId;

        _chatHistory.LogSessionStart(modelName, agentName, threadId, timestamp);
        _threadSessionManager.RecordSessionStarted(agentName, timestamp);
    }

    public void RecordSessionEnd(string agentName, string reason = "exit")
    {
        var timestamp = DateTimeOffset.UtcNow;
        var threadId = _threadSessionManager.CurrentThreadId;

        _chatHistory.LogSessionEnd(agentName, threadId, reason, timestamp);
        _threadSessionManager.RecordSessionEnded(agentName, reason, timestamp);
        _threadSessionManager.Shutdown(reason);
    }

    public void RecordUserInput(string rawInput, string agentName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        _chatHistory.LogUserInput(rawInput, agentName, timestamp);
        _threadSessionManager.RecordUserInput(rawInput, agentName, timestamp);
    }

    public void RecordPromptTransformation(string rawInput, string processedInput, string agentName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        _chatHistory.LogPromptTransformation(rawInput, processedInput, agentName, timestamp);
        _threadSessionManager.RecordPromptTransformation(rawInput, processedInput, agentName, timestamp);
    }

    public void RecordModelRequest(string processedInput, string agentName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        _chatHistory.LogMessageSentToModel(processedInput, agentName, timestamp);
        _threadSessionManager.RecordModelRequest(processedInput, agentName, timestamp);
    }

    public void RecordTurn(ChatTurnResult turnResult, string agentName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        _threadSessionManager.RecordTurn(turnResult, agentName, timestamp);
        _chatHistory.LogToolExecutions(turnResult.ToolExecutions, agentName, timestamp);
        _chatHistory.LogAiReply(turnResult.FinalReply, agentName, timestamp);
    }

    public void RecordSlashCommand(string rawInput, string capturedConsoleOutput, string agentName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        _chatHistory.LogSlashCommand(rawInput, capturedConsoleOutput, agentName, timestamp);
        _threadSessionManager.RecordSlashCommand(rawInput, capturedConsoleOutput, agentName, timestamp);
    }

    public void RecordAgentChanged(string agentName, string reason)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var threadId = _threadSessionManager.CurrentThreadId;

        _chatHistory.LogAgentChanged(agentName, reason, threadId, timestamp);
        _threadSessionManager.RecordAgentChange(reason, timestamp);
    }

    public void RecordThreadChanged(string threadId, string agentName, string reason)
    {
        _chatHistory.LogThreadChanged(threadId, agentName, reason, DateTimeOffset.UtcNow);
    }
}
