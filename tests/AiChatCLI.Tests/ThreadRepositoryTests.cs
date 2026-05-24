namespace AiChatCLI.Tests;

public sealed class ThreadRepositoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public ThreadRepositoryTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void SubAgentRepository_WritesUnderSubagentsAndStaysOutOfParentList()
    {
        var repository = new ThreadRepository(Path.Combine(_tempRoot, "threads"));
        var subAgentRepository = ThreadRepository.CreateSubAgentRepository(Path.Combine(_tempRoot, "threads", "subagents"));
        var parentThreadId = repository.CreateThread("gpt-4o-mini", "default", "main prompt");

        var subAgentThreadId = subAgentRepository.CreateThread(
            "gpt-4o-mini",
            SubAgentRunner.AgentName,
            SubAgentRunner.SystemPrompt,
            SubAgentRunner.ThreadIdPrefix);

        Assert.StartsWith(SubAgentRunner.ThreadIdPrefix, subAgentThreadId, StringComparison.Ordinal);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(_tempRoot, "threads", "subagents", $"{subAgentThreadId}.jsonl")),
            Path.GetFullPath(subAgentRepository.GetThreadFilePath(subAgentThreadId)));

        var summary = Assert.Single(repository.ListThreads(new ThreadProjector()));
        Assert.Equal(parentThreadId, summary.ThreadId);
    }

    [Fact]
    public void AppendEvent_WritesUnicodeTextWithoutJsonEscaping()
    {
        var repository = new ThreadRepository(Path.Combine(_tempRoot, "threads"));
        var threadId = repository.CreateThread("gpt-4o-mini", "default", "日本語のsystem prompt");

        repository.AppendEvent(ThreadEvent.UserMessage(threadId, "session_1", "default", "こんにちは、日本語ログ"));

        var filePath = repository.GetThreadFilePath(threadId);
        var contents = File.ReadAllText(filePath);

        Assert.Contains("日本語のsystem prompt", contents, StringComparison.Ordinal);
        Assert.Contains("こんにちは、日本語ログ", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", contents, StringComparison.Ordinal);

        var userEvent = Assert.Single(repository.ReadEvents(threadId), threadEvent => threadEvent.Type == "user_message");
        Assert.Equal("こんにちは、日本語ログ", userEvent.RawInput);
    }

    [Fact]
    public void AppendEvent_WritesNestedToolJsonWithReadableEscapes()
    {
        var repository = new ThreadRepository(Path.Combine(_tempRoot, "threads"));
        var threadId = repository.CreateThread("gpt-4o-mini", "default", "system prompt");

        repository.AppendEvent(ThreadEvent.ToolResult(
            threadId,
            "session_1",
            "default",
            "default",
            [
                new ThreadToolCallRecord(
                    "command",
                    "{\"command\":\"date\",\"timeout_seconds\":30}",
                    "{\"ok\":true,\"stdout\":\"2026年5月24日 15:30:15\"}",
                    "call_1")
            ]));

        var filePath = repository.GetThreadFilePath(threadId);
        var contents = File.ReadAllText(filePath);

        Assert.Contains("\\\"command\\\":\\\"date\\\"", contents, StringComparison.Ordinal);
        Assert.Contains("2026年5月24日 15:30:15", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u0022", contents, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_UsesConfiguredDirectoryAsIs()
    {
        var configuredDirectory = Path.Combine(_tempRoot, "custom-store", "nested");

        var repository = new ThreadRepository(configuredDirectory);

        Assert.Equal(Path.GetFullPath(configuredDirectory), repository.DirectoryPath);
        Assert.True(Directory.Exists(configuredDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
