namespace AiChatCLI.Commands;

public sealed class AgentCommand : ISlashCommand
{
    private static readonly CommandHelpEntry[] HelpEntryDefinitions =
    [
        new(
            "/agent",
            "agent の一覧表示・参照・切り替え・再読み込み",
            [
                "/agent",
                "/agent list",
                "/agent show <name>",
                "/agent use <name>",
                "/agent reload"
            ])
    ];

    private readonly AgentCatalog _agentCatalog;
    private readonly AgentSelection _agentSelection;
    private readonly IChatService _chatService;
    private readonly ThreadSessionManager _threadSessionManager;

    public AgentCommand(
        AgentCatalog agentCatalog,
        AgentSelection agentSelection,
        IChatService chatService,
        ThreadSessionManager threadSessionManager)
    {
        _agentCatalog = agentCatalog;
        _agentSelection = agentSelection;
        _chatService = chatService;
        _threadSessionManager = threadSessionManager;
    }

    public string Name => "agent";
    public string Description => "agent を操作";
    public IReadOnlyList<CommandHelpEntry> HelpEntries => HelpEntryDefinitions;

    public void Execute(string[] args, TextWriter output)
    {
        if (args.Length == 0)
        {
            ShowHelp(output);
            return;
        }

        var action = args[0].ToLowerInvariant();
        switch (action)
        {
            case "list":
                ListAgents(output);
                break;
            case "show":
                ShowAgent(args, output);
                break;
            case "use":
                UseAgent(args, output);
                break;
            case "reload":
                ReloadAgents(output);
                break;
            default:
                output.WriteLine($"不明な agent アクション: {args[0]}");
                ShowHelp(output);
                break;
        }
    }

    private static void ShowHelp(TextWriter output)
    {
        CommandConventions.ShowHelpEntry(HelpEntryDefinitions[0], output);
    }

    private void ListAgents(TextWriter output)
    {
        var agents = _agentCatalog.GetAgents();
        output.WriteLine("--- エージェント一覧 ---");
        foreach (var (key, value) in agents.OrderBy(agent => agent.Key, StringComparer.Ordinal))
        {
            var marker = key == _agentSelection.CurrentName ? " [現在]" : "";
            output.WriteLine($"  {key}{marker} : {value}");
        }

        output.WriteLine("------------------------");
    }

    private void ShowAgent(string[] args, TextWriter output)
    {
        if (args.Length < 2)
        {
            output.WriteLine("使い方: /agent show <name>");
            return;
        }

        var name = string.Join(' ', args[1..]).Trim();
        if (string.IsNullOrEmpty(name))
        {
            output.WriteLine("名前を指定してください。");
            return;
        }

        if (!_agentCatalog.TryGetAgentPrompt(name, out var prompt))
        {
            output.WriteLine($"'{name}' は登録されていません。");
            return;
        }

        var marker = name == _agentSelection.CurrentName ? " [現在]" : "";
        output.WriteLine($"{name}{marker}");
        output.WriteLine(prompt);
    }

    private void UseAgent(string[] args, TextWriter output)
    {
        if (args.Length < 2)
        {
            output.WriteLine("使い方: /agent use <name>");
            return;
        }

        var name = string.Join(' ', args[1..]).Trim();
        if (string.IsNullOrEmpty(name))
        {
            output.WriteLine("名前を指定してください。");
            return;
        }

        if (!_agentSelection.TrySelect(name))
        {
            output.WriteLine($"'{name}' は登録されていません。");
            return;
        }

        _chatService.SetAgent(_agentSelection.CurrentName, _agentSelection.CurrentPrompt);
        _threadSessionManager.RecordAgentChange("agent_use");
        output.WriteLine($"エージェントを '{name}' に切り替えました。");
    }

    private void ReloadAgents(TextWriter output)
    {
        if (!_agentCatalog.ReloadAgentsFromDisk())
        {
            output.WriteLine("再読み込みに失敗しました（ファイルが無い、または JSON が不正など）。");
            return;
        }

        _agentSelection.EnsureCurrentSelection();
        _chatService.SetAgent(_agentSelection.CurrentName, _agentSelection.CurrentPrompt);
        _threadSessionManager.RecordAgentChange("agent_reload");
        output.WriteLine($"{Path.GetFileName(_agentCatalog.SourcePath)} をエージェント定義として再読み込みしました。");
    }
}
