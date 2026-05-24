using AutoGen.Core;

namespace AiChatCLI;

internal sealed class SubAgentRunner
{
    public const string AgentName = "subagent";
    public const string ThreadIdPrefix = "subagent_thread";
    public const string SystemPrompt =
        "You are a focused sub-agent. Work only from the user prompt you receive, use available tools when useful, and return a concise textual result to the main agent.";

    private readonly Func<IAgent> _createAgent;
    private readonly IAgentTurnExecutor _turnExecutor;
    private readonly ConversationCodec _conversationCodec;
    private readonly ThreadRepository? _repository;
    private readonly string _modelName;

    public SubAgentRunner(
        Func<IAgent> createAgent,
        IAgentTurnExecutor turnExecutor,
        ConversationCodec conversationCodec,
        ThreadRepository? repository,
        string modelName)
    {
        _createAgent = createAgent;
        _turnExecutor = turnExecutor;
        _conversationCodec = conversationCodec;
        _repository = repository;
        _modelName = modelName;
    }

    public async Task<SubAgentRunResult> RunAsync(string prompt)
    {
        var threadId = _repository is null
            ? ThreadRepository.CreateThreadId(ThreadIdPrefix)
            : _repository.CreateThread(_modelName, AgentName, SystemPrompt, ThreadIdPrefix);
        var sessionId = $"session_{Guid.NewGuid():N}";
        var recorder = _repository is null
            ? null
            : new ThreadRecorder(_repository, _conversationCodec, sessionId);

        recorder?.RecordSessionAttached(threadId, _modelName, AgentName, SystemPrompt, "subagent_start");
        recorder?.RecordUserInput(threadId, AgentName, prompt);
        recorder?.RecordModelRequest(threadId, AgentName, prompt);

        using var _ = SubAgentExecutionContext.Enter();

        try
        {
            var turnHistory = new List<IMessage>
            {
                new TextMessage(Role.User, prompt)
            };
            var execution = await _turnExecutor.ExecuteAsync(_createAgent(), turnHistory);
            recorder?.RecordTurn(threadId, AgentName, execution.TurnResult);
            recorder?.RecordSessionDetached(threadId, "subagent_complete");

            return new SubAgentRunResult(threadId, execution.TurnResult.FinalReply);
        }
        catch
        {
            recorder?.RecordSessionDetached(threadId, "subagent_error");
            throw;
        }
    }
}

internal sealed record SubAgentRunResult(
    string ThreadId,
    string FinalReply);
