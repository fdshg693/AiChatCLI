using AutoGen.Core;
using System.Text.Json;

namespace AiChatCLI.Tests;

public sealed class SubAgentRunnerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "AiChatCLI.Tests",
        Guid.NewGuid().ToString("N"));

    public SubAgentRunnerTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task RunAsync_UsesPromptAsFreshConversationAndWritesSubThread()
    {
        var executor = new CapturingTurnExecutor("調査結果です");
        var repository = ThreadRepository.CreateSubAgentRepository(Path.Combine(_tempRoot, "threads", "subagents"));
        var runner = new SubAgentRunner(
            () => null!,
            executor,
            new ConversationCodec(),
            repository,
            "gpt-4o-mini");

        var result = await runner.RunAsync("この件を調査して");

        Assert.StartsWith(SubAgentRunner.ThreadIdPrefix, result.ThreadId, StringComparison.Ordinal);
        Assert.Equal("調査結果です", result.FinalReply);
        var userMessage = Assert.IsType<TextMessage>(Assert.Single(executor.CapturedHistory));
        Assert.Equal(Role.User, userMessage.Role);
        Assert.Equal("この件を調査して", userMessage.Content);

        var events = repository.ReadEvents(result.ThreadId);
        Assert.Contains(events, threadEvent => threadEvent.Type == "thread_created");
        Assert.Contains(events, threadEvent => threadEvent.Type == "model_request" && threadEvent.ProcessedInput == "この件を調査して");
        Assert.Contains(events, threadEvent => threadEvent.Type == "assistant_message");
        Assert.Contains(events, threadEvent => threadEvent.Type == "session_detached" && threadEvent.Reason == "subagent_complete");
    }

    [Fact]
    public async Task SubAgentTool_ReturnsErrorWhenCalledInsideSubAgent()
    {
        var runner = new SubAgentRunner(
            () => null!,
            new CapturingTurnExecutor("unused"),
            new ConversationCodec(),
            null,
            "gpt-4o-mini");
        var tools = new SubAgentTools(runner);

        using var _ = SubAgentExecutionContext.Enter();
        var result = await tools.sub_agent("nested work");

        var response = JsonSerializer.Deserialize<SubAgentToolResponse>(
            result,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.False(response!.Ok);
        Assert.Contains("サブエージェント内では使用できません", response.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void SubAgentTool_ExposesExpectedFunctionName()
    {
        var runner = new SubAgentRunner(
            () => null!,
            new CapturingTurnExecutor("unused"),
            new ConversationCodec(),
            null,
            "gpt-4o-mini");
        var tools = new SubAgentTools(runner);

        Assert.Equal(SubAgentTools.FunctionName, tools.sub_agentFunctionContract.Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private sealed class CapturingTurnExecutor : IAgentTurnExecutor
    {
        private readonly string _reply;

        public CapturingTurnExecutor(string reply)
        {
            _reply = reply;
        }

        public IReadOnlyList<IMessage> CapturedHistory { get; private set; } = [];

        public Task<AgentTurnExecution> ExecuteAsync(IAgent agent, List<IMessage> turnHistory)
        {
            CapturedHistory = turnHistory.ToArray();
            var turnResult = new ChatTurnResult(
                _reply,
                [],
                [ThreadMessageRecord.CreateText(Role.Assistant, _reply, "assistant")]);

            return Task.FromResult(new AgentTurnExecution(turnResult, turnHistory));
        }
    }
}
