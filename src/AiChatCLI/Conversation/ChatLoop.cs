namespace AiChatCLI;

internal class ChatLoop
{
    private readonly AgentSelection _agentSelection;
    private readonly IChatTraceRecorder _chatTraceRecorder;
    private readonly InteractivePromptReader _promptReader;
    private readonly ChatTurnPipeline _chatTurnPipeline;

    public ChatLoop(
        AgentSelection agentSelection,
        IChatTraceRecorder chatTraceRecorder,
        InteractivePromptReader promptReader,
        ChatTurnPipeline chatTurnPipeline)
    {
        _agentSelection = agentSelection;
        _chatTraceRecorder = chatTraceRecorder;
        _promptReader = promptReader;
        _chatTurnPipeline = chatTurnPipeline;
    }

    public async Task RunAsync(string modelName)
    {
        _chatTraceRecorder.RecordSessionStart(modelName, _agentSelection.CurrentName);
        Console.WriteLine($"AI Chat CLI (model: {modelName}, agent: {_agentSelection.CurrentName}, exitで終了)");
        if (_chatTraceRecorder.TranscriptFilePath is { } logPath)
            Console.WriteLine($"会話ログ: {logPath}");
        Console.WriteLine("---");

        while (true)
        {
            var input = _promptReader.ReadLine(GetPromptLabel());
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                _chatTraceRecorder.RecordSessionEnd(_agentSelection.CurrentName);
                break;
            }

            var result = await _chatTurnPipeline.ProcessAsync(input, Console.Out);
            if (result.HandledBySlashCommand)
            {
                Console.WriteLine();
                continue;
            }

            Console.WriteLine($"{result.AgentName}エージェント> {result.Reply}");
            Console.WriteLine();
        }
    }

    private string GetPromptLabel() => $"{_agentSelection.CurrentName}エージェント> ";
}
