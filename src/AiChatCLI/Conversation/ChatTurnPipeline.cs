namespace AiChatCLI;

internal sealed class ChatTurnPipeline
{
    private readonly IChatService _chatService;
    private readonly AgentSelection _agentSelection;
    private readonly IPromptTemplateProcessor _templateProcessor;
    private readonly SlashCommandHandler _slashCommandHandler;
    private readonly IChatTraceRecorder _chatTraceRecorder;

    public ChatTurnPipeline(
        IChatService chatService,
        AgentSelection agentSelection,
        IPromptTemplateProcessor templateProcessor,
        SlashCommandHandler slashCommandHandler,
        IChatTraceRecorder chatTraceRecorder)
    {
        _chatService = chatService;
        _agentSelection = agentSelection;
        _templateProcessor = templateProcessor;
        _slashCommandHandler = slashCommandHandler;
        _chatTraceRecorder = chatTraceRecorder;
    }

    public async Task<ChatInputResult> ProcessAsync(string input, TextWriter output)
    {
        var currentAgentName = _agentSelection.CurrentName;

        if (input.StartsWith('/') && TryHandleSlashCommand(input, currentAgentName, output))
            return ChatInputResult.SlashCommandHandled();

        _chatTraceRecorder.RecordUserInput(input, currentAgentName);

        var processedInput = _templateProcessor.Process(input);
        var messageAgentName = _agentSelection.CurrentName;

        // Keep the structured trace and transcript projection in the same order.
        _chatTraceRecorder.RecordPromptTransformation(input, processedInput, messageAgentName);
        _chatTraceRecorder.RecordModelRequest(processedInput, messageAgentName);

        var turnResult = await _chatService.SendAsync(processedInput);

        _chatTraceRecorder.RecordTurn(turnResult, messageAgentName);

        return ChatInputResult.ChatReply(messageAgentName, turnResult.FinalReply);
    }

    private bool TryHandleSlashCommand(string input, string agentName, TextWriter output)
    {
        using var capture = new StringWriter();
        var tee = new TeeTextWriter(output, capture);
        var handled = _slashCommandHandler.TryHandle(input, tee);
        tee.Flush();

        if (handled)
            _chatTraceRecorder.RecordSlashCommand(input, capture.ToString(), agentName);

        return handled;
    }
}
