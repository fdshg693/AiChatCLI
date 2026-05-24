namespace AiChatCLI.Tests;

public sealed class ThreadRecorderTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public ThreadRecorderTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void RecordTurn_AddsSubAgentInvocationEventFromSubAgentToolResult()
    {
        var repository = new ThreadRepository(Path.Combine(_tempRoot, "threads"));
        var recorder = new ThreadRecorder(repository, new ConversationCodec(), "session_1");
        var threadId = repository.CreateThread("gpt-4o-mini", "default", "main prompt");
        const string subAgentThreadId = "subagent_thread_20260426_000000_000_12345678";
        var turnResult = new ChatTurnResult(
            "done",
            [],
            [
                ThreadMessageRecord.CreateToolCallAggregate(
                    [
                        new ThreadToolCallRecord(
                            SubAgentTools.FunctionName,
                            "{\"prompt\":\"調査して\"}",
                            $$"""{"ok":true,"subAgentThreadId":"{{subAgentThreadId}}","result":"調査結果","error":null}""",
                            "call_1")
                    ])
            ]);

        recorder.RecordTurn(threadId, "default", turnResult);

        var subAgentEvent = Assert.Single(repository.ReadEvents(threadId), threadEvent => threadEvent.Type == "subagent_invoked");
        Assert.Equal(subAgentThreadId, subAgentEvent.SubAgentThreadId);
        Assert.Equal("call_1", subAgentEvent.ToolCallId);
        Assert.Equal("default", subAgentEvent.AgentName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
