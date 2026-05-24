namespace AiChatCLI.Commands;

internal class StatusCommand : ISlashCommand
{
    private static readonly CommandHelpEntry[] HelpEntryDefinitions =
    [
        new(
            "/status",
            "現在のモデルと agent / tool / prompt 設定の要約を表示",
            ["/status"])
    ];

    private readonly string _modelName;
    private readonly string _templatesPath;
    private readonly MemoryStore _memoryStore;
    private readonly PromptTemplateManager _templateManager;
    private readonly AgentCatalog _agentCatalog;
    private readonly AgentSelection _agentSelection;
    private readonly ThreadSessionManager _threadSessionManager;
    private readonly AgentToolCatalog _toolCatalog;

    public StatusCommand(
        string modelName,
        string templatesPath,
        MemoryStore memoryStore,
        PromptTemplateManager templateManager,
        AgentCatalog agentCatalog,
        AgentSelection agentSelection,
        ThreadSessionManager threadSessionManager,
        AgentToolCatalog toolCatalog)
    {
        _modelName = modelName;
        _templatesPath = templatesPath;
        _memoryStore = memoryStore;
        _templateManager = templateManager;
        _agentCatalog = agentCatalog;
        _agentSelection = agentSelection;
        _threadSessionManager = threadSessionManager;
        _toolCatalog = toolCatalog;
    }

    public string Name => "status";
    public string Description => "現在のモデルと agent / tool / prompt 設定の要約を表示";
    public IReadOnlyList<CommandHelpEntry> HelpEntries => HelpEntryDefinitions;

    public void Execute(string[] args, TextWriter output)
    {
        if (args.Length > 0)
        {
            output.WriteLine("使い方: /status");
            return;
        }

        output.WriteLine("--- 現在の状態 ---");
        output.WriteLine($"モデル: {_modelName}");
        output.WriteLine($"選択中の agent: {_agentSelection.CurrentName}");
        output.WriteLine($"agent 件数: {_agentCatalog.GetAgents().Count}");
        output.WriteLine($"template 件数: {_templateManager.GetTemplates().Count}");
        output.WriteLine($"memory 件数: {_memoryStore.EntryCount}");
        output.WriteLine($"利用可能 tool (current agent): {FormatTools(_toolCatalog.GetEnabledToolNames(_agentSelection.CurrentTools, AgentToolConsumer.MainAgent))}");
        output.WriteLine($"利用可能 tool (current sub-agent): {FormatTools(_toolCatalog.GetEnabledToolNames(_agentSelection.CurrentTools, AgentToolConsumer.SubAgent))}");
        output.WriteLine($"現在の thread: {_threadSessionManager.CurrentThreadId ?? "(未作成/無効)"}");
        output.WriteLine($"template 定義ファイル: {_templatesPath}");
        output.WriteLine($"agent 定義ファイル: {_agentCatalog.SourcePath}");
        output.WriteLine($"memory 保存ファイル: {_memoryStore.FilePath}");
        output.WriteLine("------------------");
    }

    private static string FormatTools(IReadOnlyList<string> tools) =>
        tools.Count == 0 ? "(なし)" : string.Join(", ", tools);
}
