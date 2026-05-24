namespace AiChatCLI;

public sealed class ChatTurnPipeline
{
    private readonly IChatService _chatService;
    private readonly AgentSelection _agentSelection;
    private readonly IPromptTemplateProcessor _templateProcessor;
    private readonly SlashCommandHandler _slashCommandHandler;
    private readonly ChatHistoryLogger _chatHistory;
    private readonly ThreadSessionManager _threadSessionManager;

    public ChatTurnPipeline(
        IChatService chatService,
        AgentSelection agentSelection,
        IPromptTemplateProcessor templateProcessor,
        SlashCommandHandler slashCommandHandler,
        ChatHistoryLogger chatHistory,
        ThreadSessionManager threadSessionManager)
    {
        _chatService = chatService;
        _agentSelection = agentSelection;
        _templateProcessor = templateProcessor;
        _slashCommandHandler = slashCommandHandler;
        _chatHistory = chatHistory;
        _threadSessionManager = threadSessionManager;
    }

    public async Task<ChatInputResult> ProcessAsync(string input, TextWriter output)
    {
        var currentAgentName = _agentSelection.CurrentName;
        _chatHistory.LogUserInput(input, currentAgentName);

        if (input.StartsWith('/') && TryHandleSlashCommand(input, currentAgentName, output))
            return ChatInputResult.SlashCommandHandled();

        var processedInput = _templateProcessor.Process(input);
        var messageAgentName = _agentSelection.CurrentName;

        // Keep thread replay and the human-readable transcript in the same order.
        _threadSessionManager.RecordUserInput(input, messageAgentName);
        _threadSessionManager.RecordPromptTransformation(input, processedInput, messageAgentName);
        _threadSessionManager.RecordModelRequest(processedInput, messageAgentName);
        _chatHistory.LogPromptTransformation(input, processedInput, messageAgentName);
        _chatHistory.LogMessageSentToModel(processedInput, messageAgentName);

        var turnResult = await _chatService.SendAsync(processedInput);

        _threadSessionManager.RecordTurn(turnResult, messageAgentName);
        _chatHistory.LogToolExecutions(turnResult.ToolExecutions, messageAgentName);
        _chatHistory.LogAiReply(turnResult.FinalReply, messageAgentName);

        return ChatInputResult.ChatReply(messageAgentName, turnResult.FinalReply);
    }

    private bool TryHandleSlashCommand(string input, string agentName, TextWriter output)
    {
        if (!_chatHistory.IsEnabled)
            return _slashCommandHandler.TryHandle(input, output);

        using var capture = new StringWriter();
        var tee = new TeeTextWriter(output, capture);
        var handled = _slashCommandHandler.TryHandle(input, tee);
        tee.Flush();

        if (handled)
            _chatHistory.LogSlashCommand(input, capture.ToString(), agentName);

        return handled;
    }
}
