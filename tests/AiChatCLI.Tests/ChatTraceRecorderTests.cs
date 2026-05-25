namespace AiChatCLI.Tests;

public sealed class ChatTraceRecorderTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public ChatTraceRecorderTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void Recorder_FansOutToTranscriptAndStructuredThreadTrace()
    {
        var agentCatalog = new AgentCatalog(Path.Combine(_tempRoot, "agents.json"));
        var agentSelection = new AgentSelection(agentCatalog);
        var threadDirectory = Path.Combine(_tempRoot, "threads");
        var transcriptDirectory = Path.Combine(_tempRoot, "logs");

        using var threadSessionManager = new ThreadSessionManager(
            enabled: true,
            threadDirectoryPath: threadDirectory,
            modelName: "gpt-4o-mini",
            chatService: new StubChatService(),
            agentSelection: agentSelection,
            conversationCodec: new ConversationCodec());
        threadSessionManager.Initialize();

        using var transcriptLogger = new ChatHistoryLogger(enabled: true, transcriptDirectory);
        var recorder = new ChatTraceRecorder(transcriptLogger, threadSessionManager);
        var threadId = threadSessionManager.CurrentThreadId;
        Assert.NotNull(threadId);

        recorder.RecordSessionStart("gpt-4o-mini", agentSelection.CurrentName);
        recorder.RecordSlashCommand("/status", "line1\r\nline2", agentSelection.CurrentName);
        recorder.RecordAgentChanged(agentSelection.CurrentName, "agent_reload");
        recorder.RecordSessionEnd(agentSelection.CurrentName);
        transcriptLogger.Dispose();

        var transcriptPath = recorder.TranscriptFilePath;
        Assert.NotNull(transcriptPath);
        var transcript = File.ReadAllText(transcriptPath);
        Assert.Contains("[SESSION] start", transcript, StringComparison.Ordinal);
        Assert.Contains("[SLASH] agent=default /status", transcript, StringComparison.Ordinal);
        Assert.Contains("[SLASH_OUT] agent=default line1", transcript, StringComparison.Ordinal);
        Assert.Contains("[SLASH_OUT] agent=default line2", transcript, StringComparison.Ordinal);
        Assert.Contains("[AGENT] agent=default", transcript, StringComparison.Ordinal);
        Assert.Contains("[SESSION] end", transcript, StringComparison.Ordinal);

        var repository = new ThreadRepository(threadDirectory);
        var events = repository.ReadEvents(threadId);
        Assert.Contains(events, threadEvent => threadEvent.Type == "session_started");
        Assert.Contains(events, threadEvent => threadEvent.Type == "slash_command" && threadEvent.RawInput == "/status");
        Assert.Contains(events, threadEvent => threadEvent.Type == "slash_command_output" && threadEvent.Content == "line1");
        Assert.Contains(events, threadEvent => threadEvent.Type == "slash_command_output" && threadEvent.Content == "line2");
        Assert.Contains(events, threadEvent => threadEvent.Type == "agent_changed" && threadEvent.Reason == "agent_reload");
        Assert.Contains(events, threadEvent => threadEvent.Type == "session_ended" && threadEvent.Reason == "exit");
        Assert.Contains(events, threadEvent => threadEvent.Type == "session_detached" && threadEvent.Reason == "exit");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private sealed class StubChatService : IChatService
    {
        public Task<ChatTurnResult> SendAsync(string message) =>
            Task.FromResult(new ChatTurnResult(string.Empty, [], []));

        public void SetAgent(string agentName, string systemPrompt, IReadOnlySet<string> enabledTools)
        {
        }

        public void RestoreConversation(
            string agentName,
            string systemPrompt,
            IReadOnlySet<string> enabledTools,
            IReadOnlyList<ThreadMessageRecord> conversation)
        {
        }
    }
}
