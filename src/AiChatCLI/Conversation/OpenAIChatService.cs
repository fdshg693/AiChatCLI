using AutoGen.Core;

namespace AiChatCLI;

internal sealed class OpenAIChatService : IChatService
{
    private readonly OpenAIAgentFactory _agentFactory;
    private readonly ConversationCodec _conversationCodec;
    private readonly IAgentTurnExecutor _turnExecutor;
    private readonly List<IMessage> _conversationHistory = [];
    private IAgent _agent;

    public OpenAIChatService(
        OpenAIAgentFactory agentFactory,
        ConversationCodec conversationCodec,
        IAgentTurnExecutor turnExecutor,
        string agentName,
        string systemPrompt,
        IReadOnlySet<string> enabledTools)
    {
        _agentFactory = agentFactory;
        _conversationCodec = conversationCodec;
        _turnExecutor = turnExecutor;
        _agent = _agentFactory.CreateMainAgent(agentName, systemPrompt, enabledTools);
    }

    public void SetAgent(string agentName, string systemPrompt, IReadOnlySet<string> enabledTools)
    {
        ResetAgent(agentName, systemPrompt, enabledTools);
    }

    public void RestoreConversation(
        string agentName,
        string systemPrompt,
        IReadOnlySet<string> enabledTools,
        IReadOnlyList<ThreadMessageRecord> conversation)
    {
        ResetAgent(agentName, systemPrompt, enabledTools);
        _conversationHistory.Clear();

        foreach (var message in conversation)
            _conversationHistory.Add(_conversationCodec.ToMessage(message));
    }

    public async Task<ChatTurnResult> SendAsync(string message)
    {
        var turnHistory = new List<IMessage>(_conversationHistory)
        {
            new TextMessage(Role.User, message)
        };
        var execution = await _turnExecutor.ExecuteAsync(_agent, turnHistory);

        _conversationHistory.Clear();
        _conversationHistory.AddRange(execution.TurnHistory);

        return execution.TurnResult;
    }

    private void ResetAgent(string agentName, string systemPrompt, IReadOnlySet<string> enabledTools)
    {
        _agent = _agentFactory.CreateMainAgent(agentName, systemPrompt, enabledTools);
    }
}
